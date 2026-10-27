using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace MooView;

public partial class MainWindow
{
    readonly Dictionary<string, BitmapSource> thumbnails = new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> thumbnailLoading = new(StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim thumbnailSlots = new(2);
    readonly Dictionary<string, (ListBoxItem item, CheckBox check, Image image)> browserRows = new(StringComparer.OrdinalIgnoreCase);
    string? selectionAnchor, dragPath, pendingToggle, lastVisibleCurrent;
    Point? fileDragStart;
    bool paintingChecks, paintValue, exportingFiles;
    int browserVersion;

    void InitializeBrowser()
    {
        FileList.PreviewMouseLeftButtonDown += BrowserDown;
        FileList.PreviewMouseMove += BrowserMove;
        FileList.PreviewMouseLeftButtonUp += BrowserUp;
        FileList.LostMouseCapture += (_, _) => { paintingChecks = false; fileDragStart = null; pendingToggle = null; };
        FileList.AddHandler(ScrollViewer.ScrollChangedEvent, new ScrollChangedEventHandler((_, _) => QueueVisibleThumbnails()));
        FileList.SizeChanged += (_, _) => UpdateBrowserLayout();
    }
    void RebuildList()
    {
        listUpdating = true; browserVersion++; lastVisibleCurrent = null; FileList.Items.Clear(); browserRows.Clear();
        foreach (var path in files.Where(p => Path.GetFileName(p).Contains(Search.Text, StringComparison.OrdinalIgnoreCase)))
        {
            var check = new CheckBox { IsChecked = selected.Contains(path), Focusable = false, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(3, 6, 5, 0) };
            var preview = new Image { Stretch = Stretch.Uniform, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
            var label = new TextBlock { Text = Path.GetFileName(path), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(3), ToolTip = path };
            var content = new StackPanel(); content.Children.Add(preview); content.Children.Add(label);
            var row = new DockPanel(); DockPanel.SetDock(check, Dock.Left); row.Children.Add(check); row.Children.Add(content);
            var item = new ListBoxItem { Content = row, Tag = path, Padding = new Thickness(3), Margin = new Thickness(1), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = path };
            item.SetResourceReference(ForegroundProperty, "Text"); FileList.Items.Add(item); browserRows[path] = (item, check, preview);
        }
        listUpdating = false; UpdateBrowserLayout(); SyncSelection();
    }
    void BrowserSizeChanged(object s, RoutedPropertyChangedEventArgs<double> e) { if (ready) UpdateBrowserLayout(); }
    void UpdateBrowserLayout()
    {
        double value = BrowserSize.Value, available = Math.Max(100, FileList.ActualWidth - 20);
        bool thumbs = value >= 20;
        foreach (var row in browserRows.Values)
        {
            int columns = thumbs ? Math.Max(1, (int)(available / (value + 34))) : 1;
            row.item.Width = Math.Max(90, available / columns - 4);
            row.image.Visibility = thumbs ? Visibility.Visible : Visibility.Collapsed;
            row.image.Width = thumbs ? Math.Max(36, row.item.Width - 32) : 0;
            row.image.Height = thumbs ? Math.Max(38, value * .8) : 0;
        }
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(QueueVisibleThumbnails));
    }
    async void QueueVisibleThumbnails()
    {
        if (BrowserSize.Value < 20 || !FileList.IsVisible || FileList.ActualHeight < 1) return;
        int version = browserVersion;
        foreach (var pair in browserRows.ToArray())
        {
            var path = pair.Key; var row = pair.Value;
            if (!row.item.IsVisible || row.item.ActualHeight < 1) continue;
            Rect bounds;
            try { bounds = row.item.TransformToAncestor(FileList).TransformBounds(new Rect(row.item.RenderSize)); } catch { continue; }
            if (bounds.Bottom < -160 || bounds.Top > FileList.ActualHeight + 160) { row.image.Source = null; continue; }
            if (thumbnails.TryGetValue(path, out var cached)) { row.image.Source = cached; continue; }
            if (!thumbnailLoading.Add(path)) continue;
            _ = LoadThumbnail(path, row.image, version);
        }
        await Task.CompletedTask;
    }
    async Task LoadThumbnail(string path, Image target, int version)
    {
        await thumbnailSlots.WaitAsync();
        try
        {
            if (version != browserVersion) return;
            var bitmap = await Task.Run(() => Imaging.Load(path, 256));
            if (version != browserVersion) return;
            if (thumbnails.Count >= 200) thumbnails.Remove(thumbnails.Keys.First());
            thumbnails[path] = bitmap; target.Source = bitmap;
        }
        catch { /* A corrupt image must not interrupt browsing. */ }
        finally { thumbnailLoading.Remove(path); thumbnailSlots.Release(); if (version != browserVersion) _ = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(QueueVisibleThumbnails)); }
    }
    void SyncSelection()
    {
        listUpdating = true;
        foreach (var pair in browserRows)
        {
            bool value = selected.Contains(pair.Key); pair.Value.item.IsSelected = value; pair.Value.check.IsChecked = value;
            pair.Value.item.FontWeight = files.Count > 0 && pair.Key == files[index] ? FontWeights.SemiBold : FontWeights.Normal;
        }
        listUpdating = false; SelectionLabel.Text = $"{selected.Count} " + L10n.T("Selected");
        if (files.Count > 0 && files[index] != lastVisibleCurrent && browserRows.TryGetValue(files[index], out var current)) { lastVisibleCurrent = files[index]; FileList.ScrollIntoView(current.item); }
    }
    void FileChosen(object s, SelectionChangedEventArgs e) { if (!listUpdating) SyncSelection(); }
    static T? ParentOf<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null) { if (child is T match) return match; child = child is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(child); }
        return null;
    }
    internal void SelectBrowserPath(string path, ModifierKeys modifiers, bool checkbox = false)
    {
        var visible = browserRows.Keys.ToList();
        if (modifiers.HasFlag(ModifierKeys.Alt)) selected.Remove(path);
        else if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            int start = visible.IndexOf(selectionAnchor ?? path), end = visible.IndexOf(path);
            if (start < 0) start = end;
            if (!modifiers.HasFlag(ModifierKeys.Control)) selected.Clear();
            for (int i = Math.Min(start, end); i <= Math.Max(start, end); i++) selected.Add(visible[i]);
        }
        else if (checkbox || modifiers.HasFlag(ModifierKeys.Control)) { if (!selected.Remove(path)) selected.Add(path); selectionAnchor = path; }
        else { selected.Clear(); selected.Add(path); selectionAnchor = path; }
        SyncSelection();
    }
    void BrowserDown(object s, MouseButtonEventArgs e)
    {
        if (ParentOf<ListBoxItem>(e.OriginalSource as DependencyObject) is not { Tag: string path }) return;
        FileList.Focus(); e.Handled = true; var modifiers = Keyboard.Modifiers;
        bool checkbox = ParentOf<CheckBox>(e.OriginalSource as DependencyObject) != null;
        pendingToggle = !checkbox && modifiers == ModifierKeys.None && selected.Contains(path) ? path : null;
        if (pendingToggle == null) SelectBrowserPath(path, modifiers, checkbox);
        index = files.IndexOf(path); ResetPageGrouping(); _ = RenderAsync();
        fileDragStart = e.GetPosition(FileList); dragPath = path; FileList.CaptureMouse();
        if (checkbox && !modifiers.HasFlag(ModifierKeys.Shift))
        {
            paintingChecks = true; paintValue = selected.Contains(path); FileList.CaptureMouse();
        }
    }
    void BrowserMove(object s, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || fileDragStart is not Point start) return;
        var point = e.GetPosition(FileList);
        if (paintingChecks)
        {
            if (ParentOf<ListBoxItem>(FileList.InputHitTest(point) as DependencyObject) is { Tag: string path })
            {
                PaintChecks(dragPath!, path, paintValue);
                dragPath = path; SyncSelection();
            }
            var scroll = FindVisual<ScrollViewer>(FileList);
            if (point.Y < 24) scroll?.LineUp(); else if (point.Y > FileList.ActualHeight - 24) scroll?.LineDown();
            return;
        }
        if (Math.Abs(point.X - start.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(point.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        var paths = dragPath != null && selected.Contains(dragPath) ? files.Where(selected.Contains).ToArray() : new[] { dragPath! };
        pendingToggle = null; fileDragStart = null; FileList.ReleaseMouseCapture(); ExportFiles(FileList, paths);
    }
    internal void PaintChecks(string start, string end, bool value)
    {
        var paths = browserRows.Keys.ToList(); int a = paths.IndexOf(start), b = paths.IndexOf(end);
        if (a < 0 || b < 0) return;
        foreach (var path in paths.Skip(Math.Min(a,b)).Take(Math.Abs(a-b)+1)) { if (value) selected.Add(path); else selected.Remove(path); }
        SyncSelection();
    }
    static T? FindVisual<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var c = VisualTreeHelper.GetChild(parent, i); if (c is T t) return t; if (FindVisual<T>(c) is T found) return found; }
        return null;
    }
    void BrowserUp(object s, MouseButtonEventArgs e)
    {
        if (pendingToggle is string path) { selected.Remove(path); selectionAnchor = path; SyncSelection(); }
        pendingToggle = null; fileDragStart = null; paintingChecks = false;
        if (Mouse.Captured == FileList) FileList.ReleaseMouseCapture();
    }
    internal static DataObject FileDragData(IEnumerable<string> paths)
    {
        var data = new DataObject(); data.SetData(DataFormats.FileDrop, paths.Where(File.Exists).ToArray()); return data;
    }
    void ExportFiles(DependencyObject sender, string[] paths)
    {
        if (paths.Length == 0) return;
        exportingFiles = true; Stop();
        try { DragDrop.DoDragDrop(sender, FileDragData(paths), DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link); }
        finally { exportingFiles = false; RefreshAfterFileDrag(); }
    }
    void RefreshAfterFileDrag()
    {
        string? current = files.Count > 0 ? files[index] : null;
        if (files.RemoveAll(p => !File.Exists(p)) == 0) return;
        selected.RemoveWhere(p => !File.Exists(p));
        foreach (var path in cache.Keys.Where(p => !File.Exists(p)).ToArray()) cache.Remove(path);
        index = current == null ? 0 : Math.Max(0, files.IndexOf(current)); index = Math.Min(index, Math.Max(0, files.Count - 1));
        ResetPageGrouping(); RebuildList(); _ = RenderAsync();
    }
}

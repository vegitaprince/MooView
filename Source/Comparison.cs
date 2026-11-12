using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
namespace MooView;

public partial class MainWindow
{
    internal bool Comparing => Comparison.Visibility == Visibility.Visible;
    BitmapSource? compareA, compareB;
    string? comparePathA, comparePathB, currentExrPath;
    int compareGeneration, toneGeneration, exrGeneration, sideGenerationA, sideGenerationB;
    bool advancedUpdating;
    readonly DispatcherTimer toneTimer = new() { Interval = TimeSpan.FromMilliseconds(70) };
    readonly DispatcherTimer exrTimer = new() { Interval = TimeSpan.FromMilliseconds(110) };
    readonly Slider brightnessA = ToneSlider(), brightnessB = ToneSlider(), contrastA = ToneSlider(), contrastB = ToneSlider();
    readonly Slider opacitySlider = new() { Minimum = 0, Maximum = 1, Value = 1, Width = 88, VerticalAlignment = VerticalAlignment.Center };
    readonly Slider exrExposure = new() { Minimum = -10, Maximum = 10, Value = 0, Width = 110, TickFrequency = .25, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
    readonly ComboBox exrLayers = new() { Width = 220 }, compareLayersA = new() { Width = 170 }, compareLayersB = new() { Width = 170 };
    readonly ToggleButton grayButton = new(), srgbButton = new();
    readonly ToggleButton[] channelButtons = [new(), new(), new(), new()];
    int compareChannels = 7;
    static Slider ToneSlider() => new() { Minimum = 0, Maximum = 200, Value = 100, Width = 88, TickFrequency = 1, IsSnapToTickEnabled = true, AutoToolTipPlacement = AutoToolTipPlacement.TopLeft, AutoToolTipPrecision = 0, VerticalAlignment = VerticalAlignment.Center };
    static void Tip(FrameworkElement control, string key) { control.SetBinding(ToolTipProperty, new Binding($"[{key}]") { Source = L10n.Instance }); System.Windows.Automation.AutomationProperties.SetName(control, L10n.T(key)); }
    static void IconControl(ButtonBase control, string icon, string tip) { control.Content = new Glyph { Kind = icon, Width = 19, Height = 19 }; control.Width = 34; control.Height = 34; control.Padding = new Thickness(6); if (control is ToggleButton) control.SetResourceReference(StyleProperty, "IconToggle"); Tip(control, tip); }
    Button AdvancedButton(string icon, string tip, RoutedEventHandler action) { var b = new Button(); IconControl(b, icon, tip); b.Click += action; return b; }
    void InitializeAdvanced()
    {
        Comparison.ZoomRequested += SetZoom;
        CompareBar.Children.Add(AdvancedButton("image", "CompareA", (_, _) => PickCompareSide(true)));
        CompareBar.Children.Add(AdvancedButton("folder", "CompareB", (_, _) => PickCompareSide(false)));
        CompareBar.Children.Add(AdvancedButton("convert", "CompareSwap", (_, _) => SwapComparison()));
        IconControl(grayButton, "gray", "CompareGray"); grayButton.Click += (_, _) => QueueTone(); CompareBar.Children.Add(grayButton);
        for (int i = 0; i < 4; i++) { int n = i; var button = channelButtons[i]; button.SetResourceReference(StyleProperty, "IconToggle"); button.Content = new TextBlock { Text = new[] { "RGB", "R", "G", "B" }[i], FontSize = 11, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center }; button.Width = i == 0 ? 40 : 32; button.Height = 34; button.Padding = new Thickness(3); button.ToolTip = i == 0 ? L10n.T("CompareAll") : new[] { "", "Red", "Green", "Blue" }[i]; button.Click += (_, _) => SelectChannel(n); CompareBar.Children.Add(button); }
        SyncChannels();
        AddSlider(CompareBar, "alpha", opacitySlider, "CompareOpacity"); opacitySlider.ValueChanged += (_, _) => Comparison.AOpacity = opacitySlider.Value;
        AddTonePair("A", brightnessA, contrastA, "CompareExposureA", "CompareContrastA");
        AddTonePair("B", brightnessB, contrastB, "CompareExposureB", "CompareContrastB");
        CompareBar.Children.Add(AdvancedButton("undo", "CompareReset", (_, _) => { advancedUpdating = true; brightnessA.Value = brightnessB.Value = contrastA.Value = contrastB.Value = 100; advancedUpdating = false; QueueTone(); }));
        CompareBar.Children.Add(AdvancedButton("save", "CompareExport", (_, _) => ExportComparison()));
        foreach (var combo in new[] { compareLayersA, compareLayersB }) { Tip(combo, "ExrLayer"); combo.Visibility = Visibility.Collapsed; CompareBar.Children.Add(combo); }
        compareLayersA.SelectionChanged += async (_, _) => { if (!advancedUpdating) await LoadCompareLayer(true); }; compareLayersB.SelectionChanged += async (_, _) => { if (!advancedUpdating) await LoadCompareLayer(false); };
        ExrBar.Children.Add(new Glyph { Kind = "layers", Width = 20, Height = 20, Margin = new Thickness(4, 6, 6, 6) }); Tip(exrLayers, "ExrLayer"); ExrBar.Children.Add(exrLayers);
        AddSlider(ExrBar, "exposure", exrExposure, "ExrExposure");
        IconControl(srgbButton, "color", "ExrSrgb"); srgbButton.IsChecked = true; ExrBar.Children.Add(srgbButton);
        exrLayers.SelectionChanged += (_, _) => QueueExr(); exrExposure.ValueChanged += (_, _) => QueueExr(); srgbButton.Click += (_, _) => QueueExr();
        toneTimer.Tick += async (_, _) => { toneTimer.Stop(); await ApplyCompareTone(); };
        exrTimer.Tick += async (_, _) => { exrTimer.Stop(); await ApplyExrLayer(); };
        Closed += (_, _) => { compareGeneration++; toneGeneration++; exrGeneration++; toneTimer.Stop(); exrTimer.Stop(); };
    }
    static void AddSlider(Panel panel, string icon, Slider slider, string tip) { var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(5, 0, 5, 0) }; row.Children.Add(new Glyph { Kind = icon, Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) }); Tip(row, tip); Tip(slider, tip); row.Children.Add(slider); panel.Children.Add(row); }
    void AddTonePair(string name, Slider brightness, Slider contrast, string brightnessTip, string contrastTip) { CompareBar.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) }); AddSlider(CompareBar, "exposure", brightness, brightnessTip); AddSlider(CompareBar, "contrast", contrast, contrastTip); brightness.ValueChanged += (_, _) => QueueTone(); contrast.ValueChanged += (_, _) => QueueTone(); }
    internal void SelectChannel(int n) { if (n == 0) compareChannels = 7; else { int bit = 1 << (n - 1); compareChannels = compareChannels == 7 ? bit : compareChannels ^ bit; if (compareChannels == 0) compareChannels = 7; } SyncChannels(); QueueTone(); }
    void SyncChannels() { for (int i = 0; i < 4; i++) channelButtons[i].IsChecked = i == 0 ? compareChannels == 7 : compareChannels != 7 && (compareChannels & (1 << (i - 1))) != 0; }
    void QueueTone() { if (advancedUpdating || !Comparing) return; toneGeneration++; toneTimer.Stop(); toneTimer.Start(); }
    internal async Task ApplyCompareTone()
    {
        if (!Comparing || compareA == null || compareB == null) return; int version = ++toneGeneration; var a = compareA; var b = compareB; var ta = new CompareTone(brightnessA.Value / 100, contrastA.Value / 100); var tb = new CompareTone(brightnessB.Value / 100, contrastB.Value / 100); int channels = compareChannels; bool gray = grayButton.IsChecked == true;
        try { var images = await Task.Run(() => (ComparePixels.Adjust(a, ta, channels, gray), ComparePixels.Adjust(b, tb, channels, gray))); if (version != toneGeneration || !Comparing) return; Comparison.SetImages(images.Item1, images.Item2); }
        catch (Exception ex) { if (version == toneGeneration) Status(ex.Message); }
    }
    async void ToggleCompare(object sender, RoutedEventArgs e)
    {
        if (Comparing) { ExitCompare(); return; } CompareButton.IsChecked = false;
        var dialog = new OpenFileDialog { Filter = Imaging.OpenFilter, Multiselect = files.Count == 0, Title = L10n.T(files.Count == 0 ? "Compare" : "CompareB") };
        if (dialog.ShowDialog(this) != true) return;
        if (files.Count == 0) { if (dialog.FileNames.Length < 2) { Status(L10n.Choose("비교할 이미지 두 장을 선택하세요.", "Select two images to compare.")); return; } await StartComparison(dialog.FileNames[0], dialog.FileNames[1]); }
        else await StartComparison(files[index], dialog.FileName);
    }
    internal async Task StartComparison(string first, string second)
    {
        Stop(); CancelPendingSeek(); int version = ++compareGeneration;
        try {
            var images = await Task.Run(() => (Imaging.Load(first), Imaging.Load(second)));
            var viewsA = await ReadViews(first); var viewsB = await ReadViews(second); if (version != compareGeneration) return;
            ++loadVersion; ++exrGeneration; exrTimer.Stop(); foreach (var pane in panes) pane.StopAnimation(); compareA = images.Item1; compareB = images.Item2; comparePathA = first; comparePathB = second;
            advancedUpdating = true; brightnessA.Value = brightnessB.Value = contrastA.Value = contrastB.Value = 100; opacitySlider.Value = 1; compareChannels = 7; grayButton.IsChecked = false; SyncChannels(); SetLayerItems(compareLayersA, viewsA); SetLayerItems(compareLayersB, viewsB); advancedUpdating = false;
            Comparison.SetImages(compareA, compareB, true); Comparison.SetZoom(FitMode.SelectedIndex, zoom); Comparison.Visibility = Visibility.Visible; Pages.Visibility = ExrBar.Visibility = EmptyState.Visibility = Visibility.Collapsed; CompareButton.IsChecked = true; CompareBar.Visibility = !full || ToolbarRow.Visibility == Visibility.Visible ? Visibility.Visible : Visibility.Collapsed;
            PageCount.IsEnabled = LayoutMode.IsEnabled = PlayButton.IsEnabled = FpsInput.IsEnabled = LoopButton.IsEnabled = ImageSlider.IsEnabled = false; GifButton.Visibility = Visibility.Collapsed; UpdateCompareNames(); Keyboard.Focus(Root);
        } catch (Exception ex) { if (version == compareGeneration) { CompareButton.IsChecked = Comparing; Error(ex); } }
    }
    static Task<List<ExrView>> ReadViews(string path) => Path.GetExtension(path).Equals(".exr", StringComparison.OrdinalIgnoreCase) ? Task.Run(() => ExrImage.Views(ExrImage.Inspect(path))) : Task.FromResult(new List<ExrView>());
    static void SetLayerItems(ComboBox combo, List<ExrView> views) { combo.ItemsSource = views; combo.SelectedIndex = views.Count > 0 ? 0 : -1; combo.Visibility = views.Count > 0 ? Visibility.Visible : Visibility.Collapsed; }
    internal void ExitCompare(bool render = true)
    {
        compareGeneration++; toneGeneration++; toneTimer.Stop(); if (!Comparing) return;
        Comparison.Visibility = CompareBar.Visibility = Visibility.Collapsed; Comparison.Clear(); Pages.Visibility = Visibility.Visible; CompareButton.IsChecked = false; compareA = compareB = null; comparePathA = comparePathB = null;
        PageCount.IsEnabled = LayoutMode.IsEnabled = PlayButton.IsEnabled = FpsInput.IsEnabled = LoopButton.IsEnabled = true; SyncPlaybackStrip(); if (render && ready) _ = RenderAsync();
    }
    internal bool TryCompareDrop(string[] paths, Point point)
    {
        string[] images = paths.Where(p => File.Exists(p) && Imaging.Extensions.Contains(Path.GetExtension(p))).ToArray(); if (images.Length == 0) return false;
        bool inImage = Comparing ? Comparison.IsImageInteraction(ImageArea.TranslatePoint(point, Comparison)) : panes.Any(p => p.Visibility == Visibility.Visible && p.IsImageInteraction(ImageArea.TranslatePoint(point, p)));
        if (!inImage && !(files.Count == 0 && images.Length >= 2)) return false;
        string? first = images.Length >= 2 ? images[0] : Comparing ? comparePathA : files.Count > 0 ? files[index] : null;
        if (first == null) return false; _ = StartComparison(first, images.Length >= 2 ? images[1] : images[0]); return true;
    }
    async void PickCompareSide(bool first)
    {
        var d = new OpenFileDialog { Filter = Imaging.OpenFilter, Title = L10n.T(first ? "CompareA" : "CompareB") }; if (d.ShowDialog(this) == true) await ReplaceCompareSide(first, d.FileName);
    }
    internal async Task ReplaceCompareSide(bool first, string path)
    {
        int session = compareGeneration, version = first ? ++sideGenerationA : ++sideGenerationB; try { var image = await Task.Run(() => Imaging.Load(path)); var views = await ReadViews(path); if (session != compareGeneration || version != (first ? sideGenerationA : sideGenerationB) || !Comparing) return; advancedUpdating = true; if (first) { compareA = image; comparePathA = path; SetLayerItems(compareLayersA, views); } else { compareB = image; comparePathB = path; SetLayerItems(compareLayersB, views); } advancedUpdating = false; await ApplyCompareTone(); UpdateCompareNames(); }
        catch (Exception ex) { if (session == compareGeneration && version == (first ? sideGenerationA : sideGenerationB)) Error(ex); }
    }
    async Task LoadCompareLayer(bool first)
    {
        var combo = first ? compareLayersA : compareLayersB; string? path = first ? comparePathA : comparePathB; if (combo.SelectedItem is not ExrView view || path == null || !Comparing) return;
        int session = compareGeneration, version = first ? ++sideGenerationA : ++sideGenerationB; try { var bitmap = await Task.Run(() => ExrImage.Load(path, view)); if (session != compareGeneration || version != (first ? sideGenerationA : sideGenerationB) || !Comparing) return; if (first) compareA = bitmap; else compareB = bitmap; await ApplyCompareTone(); } catch (Exception ex) { if (session == compareGeneration && version == (first ? sideGenerationA : sideGenerationB)) Error(ex); }
    }
    internal void SwapComparison()
    {
        if (!Comparing) return; compareGeneration++; (compareA, compareB) = (compareB, compareA); (comparePathA, comparePathB) = (comparePathB, comparePathA); advancedUpdating = true;
        (brightnessA.Value, brightnessB.Value) = (brightnessB.Value, brightnessA.Value); (contrastA.Value, contrastB.Value) = (contrastB.Value, contrastA.Value);
        var items = compareLayersA.ItemsSource; int selectedA = compareLayersA.SelectedIndex, selectedB = compareLayersB.SelectedIndex; compareLayersA.ItemsSource = compareLayersB.ItemsSource; compareLayersB.ItemsSource = items; compareLayersA.SelectedIndex = selectedB; compareLayersB.SelectedIndex = selectedA; compareLayersA.Visibility = compareLayersA.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed; compareLayersB.Visibility = compareLayersB.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed; advancedUpdating = false;
        UpdateCompareNames(); QueueTone();
    }
    void UpdateCompareNames() { FileNameLabel.Text = $"A: {Path.GetFileName(comparePathA) ?? "Clipboard"}   ↔   B: {Path.GetFileName(comparePathB) ?? "Clipboard"}"; FileNameLabel.ToolTip = $"A: {comparePathA ?? "Clipboard"}\nB: {comparePathB ?? "Clipboard"}"; CounterLabel.Text = "A / B"; Status(L10n.Choose("비교선 드래그 · 휠: 확대 · 이미지 드래그: 이동 · C: 종료", "Drag divider · Wheel: zoom · Drag image: pan · C: exit")); }
    async void ExportComparison()
    {
        if (!Comparing) return; toneTimer.Stop(); await ApplyCompareTone(); if (!Comparing || Comparison.ImageA == null || Comparison.ImageB == null) return;
        var d = new SaveFileDialog { Filter = "PNG|*.png|JPEG|*.jpg", FileName = "MooView-compare.png", AddExtension = true }; if (d.ShowDialog(this) != true) return;
        try { Imaging.Save(ComparePixels.Export(Comparison.ImageA, Comparison.ImageB, Comparison.Wipe, Comparison.AOpacity), d.FileName); Status(L10n.Choose("비교 이미지를 저장했습니다.", "Comparison saved.")); } catch (Exception ex) { Error(ex); }
    }
    internal void UpdateExrControls(string? path)
    {
        if (path == currentExrPath) { if (exrLayers.SelectedIndex > 0 || exrExposure.Value != 0 || srgbButton.IsChecked != true) QueueExr(); return; } currentExrPath = path != null && Path.GetExtension(path).Equals(".exr", StringComparison.OrdinalIgnoreCase) ? path : null; exrGeneration++; exrTimer.Stop();
        advancedUpdating = true; try { exrExposure.Value = 0; srgbButton.IsChecked = true; var views = currentExrPath == null ? [] : ExrImage.Views(ExrImage.Inspect(currentExrPath)); exrLayers.ItemsSource = views; exrLayers.SelectedIndex = views.Count > 0 ? 0 : -1; ExrBar.Visibility = currentExrPath != null && !Comparing && (!full || ToolbarRow.Visibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed; } catch (Exception ex) { ExrBar.Visibility = Visibility.Collapsed; Status(ex.Message); } finally { advancedUpdating = false; }
    }
    void QueueExr() { if (advancedUpdating || currentExrPath == null || Comparing) return; exrGeneration++; exrTimer.Stop(); exrTimer.Start(); }
    internal async Task ApplyExrLayer()
    {
        if (currentExrPath == null || exrLayers.SelectedItem is not ExrView view || Comparing) return; string path = currentExrPath; int version = ++exrGeneration; double ev = exrExposure.Value; bool srgb = srgbButton.IsChecked == true;
        try { var image = await Task.Run(() => ExrImage.Load(path, view, ev, srgb)); if (version != exrGeneration || Comparing || files.Count == 0 || files[index] != path) return; panes[0].SetImage(image); DetailLabel.Text = $"{image.PixelWidth:N0} × {image.PixelHeight:N0} · EXR · {view.Label} · {ev:+0.##;-0.##;0} EV"; }
        catch (Exception ex) { if (version == exrGeneration) Status(ex.Message); }
    }
    bool HandleAdvancedKey(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBoxBase or ComboBox || (Keyboard.FocusedElement is Slider && !full)) return false;
        if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.None) { ToggleCompare(this, e); e.Handled = true; return true; }
        if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Comparing) { PasteComparison(); e.Handled = true; return true; }
        if (!Comparing) return false;
        if (e.Key == Key.Escape && !full) ExitCompare();
        else if (e.Key is Key.Left or Key.Right) Comparison.Wipe += (e.Key == Key.Right ? .01 : -.01) * (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? 10 : 1);
        else if (e.Key == Key.Home) Comparison.Wipe = 0;
        else if (e.Key == Key.End) Comparison.Wipe = 1;
        else if (e.Key is not (Key.Space or Key.PageDown or Key.PageUp or Key.P)) return false;
        e.Handled = true; return true;
    }
    async void PasteComparison()
    {
        try { if (Clipboard.ContainsFileDropList()) { string? p = Clipboard.GetFileDropList().Cast<string>().FirstOrDefault(p => Imaging.Extensions.Contains(Path.GetExtension(p))); if (p != null) await ReplaceCompareSide(false, p); }
            else if (Clipboard.ContainsImage()) { var image = Clipboard.GetImage(); if (image == null) return; image.Freeze(); compareGeneration++; compareB = image; comparePathB = null; advancedUpdating = true; SetLayerItems(compareLayersB, []); advancedUpdating = false; await ApplyCompareTone(); UpdateCompareNames(); }
        } catch (Exception ex) { Error(ex); }
    }
}

using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
namespace MooView;

public partial class MainWindow
{
    double sidebarWidth = 250;
    readonly WindowChrome frame = new() { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
    void InitializeFrame()
    {
        WindowChrome.SetWindowChrome(this, frame);
        StateChanged += (_, _) => UpdateWindowButtons();
        SizeChanged += (_, _) => { if (ready && nav && !full) SetSidebarWidth(sidebarWidth); };
        UpdateWindowButtons();
    }
    void UpdateWindowButtons()
    {
        MaximizeButton.Content = new Glyph { Kind = WindowState == WindowState.Maximized ? "restore" : "maximize", Width = 17, Height = 17 };
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, L10n.T(WindowState == WindowState.Maximized ? "Restore" : "Maximize"));
        System.Windows.Automation.AutomationProperties.SetName(MinimizeButton, L10n.T("Minimize"));
        System.Windows.Automation.AutomationProperties.SetName(CloseWindowButton, L10n.T("Close"));
    }
    void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void MaximizeWindow(object sender, RoutedEventArgs e) { if (!full) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    void CloseWindow(object sender, RoutedEventArgs e) => Close();
    void TitleDrag(object sender, MouseButtonEventArgs e)
    {
        if (full || ParentOf<ButtonBase>(e.OriginalSource as DependencyObject) != null) return;
        if (e.ClickCount == 2) MaximizeWindow(this, e);
        else if (e.LeftButton == MouseButtonState.Pressed) { try { DragMove(); } catch (InvalidOperationException) { } }
        e.Handled = true;
    }
    double MaximumSidebarWidth => Math.Min(640, Math.Max(200, (Root.ActualWidth > 0 ? Root.ActualWidth : Width) * .55));
    internal void SetSidebarWidth(double value)
    {
        sidebarWidth = double.IsFinite(value) ? Math.Clamp(value, 200, MaximumSidebarWidth) : 250;
        if (!nav || full) return;
        Root.ColumnDefinitions[1].MinWidth = 200;
        Root.ColumnDefinitions[1].MaxWidth = MaximumSidebarWidth;
        Root.ColumnDefinitions[1].Width = new GridLength(sidebarWidth);
    }
    void SidebarResizeCompleted(object sender, DragCompletedEventArgs e) => SetSidebarWidth(Root.ColumnDefinitions[1].ActualWidth);
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Shell;
namespace MooView;

public partial class MainWindow
{
    double sidebarWidth = 250;
    Point? windowDragStart;
    readonly WindowChrome frame = new() { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(0), UseAeroCaptionButtons = false };
    void InitializeFrame()
    {
        WindowChrome.SetWindowChrome(this, frame);
        StateChanged += (_, _) => UpdateWindowButtons();
        SizeChanged += (_, _) => { if (ready && nav && !full) SetSidebarWidth(sidebarWidth); };
        PreviewMouseMove+=WindowDragMove;
        PreviewMouseLeftButtonUp+=(_,e)=>{if(windowDragStart!=null){CancelWindowDrag();e.Handled=true;}};
        LostMouseCapture+=(_,_)=>windowDragStart=null;
        Deactivated+=(_,_)=>CancelWindowDrag();
        UpdateWindowButtons();
    }
    void UpdateWindowButtons()
    {
        MaximizeButton.Content = new Glyph { Kind = WindowState == WindowState.Maximized ? "restore" : "maximize", Width = 20, Height = 20 };
        System.Windows.Automation.AutomationProperties.SetName(MaximizeButton, L10n.T(WindowState == WindowState.Maximized ? "Restore" : "Maximize"));
        System.Windows.Automation.AutomationProperties.SetName(MinimizeButton, L10n.T("Minimize"));
        System.Windows.Automation.AutomationProperties.SetName(CloseWindowButton, L10n.T("Close"));
    }
    void MinimizeWindow(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void MaximizeWindow(object sender, RoutedEventArgs e) { if (!full) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; }
    void CloseWindow(object sender, RoutedEventArgs e) => Close();
    internal bool CanDragWindow(DependencyObject? source,Point point)
    {
        if(full||AboutOverlay.Visibility==Visibility.Visible||source==null)return false;
        for(DependencyObject? current=source;current!=null;current=current is FrameworkContentElement content?content.Parent:System.Windows.Media.VisualTreeHelper.GetParent(current))
        {
            if(current is ButtonBase or TextBoxBase or PasswordBox or ComboBox or RangeBase or Thumb or ScrollBar or ListBoxItem or TreeViewItem or MenuItem)return false;
        }
        if(ParentOf<ComparePane>(source) is ComparePane compare && compare.IsImageInteraction(TranslatePoint(point,compare)))return false;
        var pane=ParentOf<ImagePane>(source);
        if(pane!=null&&pane.Visibility==Visibility.Visible&&pane.IsImageInteraction(TranslatePoint(point,pane)))return false;
        return true;
    }
    bool BeginWindowDrag(MouseButtonEventArgs e)
    {
        if(e.ChangedButton!=MouseButton.Left||Mouse.Captured!=null||!CanDragWindow(e.OriginalSource as DependencyObject,e.GetPosition(this)))return false;
        if(e.ClickCount==2){MaximizeWindow(this,e);e.Handled=true;return true;}
        Keyboard.Focus(Root);windowDragStart=e.GetPosition(this);
        if(!CaptureMouse()){windowDragStart=null;return false;}e.Handled=true;return true;
    }
    internal static bool DragThresholdReached(Point start,Point now)=>Math.Abs(now.X-start.X)>=SystemParameters.MinimumHorizontalDragDistance||Math.Abs(now.Y-start.Y)>=SystemParameters.MinimumVerticalDragDistance;
    void WindowDragMove(object sender,MouseEventArgs e)
    {
        if(windowDragStart is not Point start)return;
        if(e.LeftButton!=MouseButtonState.Pressed||full){CancelWindowDrag();return;}
        if(!DragThresholdReached(start,e.GetPosition(this)))return;
        CancelWindowDrag();e.Handled=true;
        try{DragMove();}catch(InvalidOperationException){/* A release during transition cancels the native drag. */}
    }
    void CancelWindowDrag(){windowDragStart=null;if(Mouse.Captured==this)ReleaseMouseCapture();}
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

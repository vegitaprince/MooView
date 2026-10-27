using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

public static class UiSmokeTest
{
    public static async Task Run()
    {
        SelfTest.Run(); var lines = new List<string>(); void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); lines.Add("PASS: " + label); }
        using var inputSource = new System.Windows.Interop.HwndSource(new System.Windows.Interop.HwndSourceParameters("MooViewTests") { WindowStyle = 0, Width = 1, Height = 1 }); var w = new MainWindow(true); object? Invoke(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(w, args); string dir = Path.Combine(AppContext.BaseDirectory, "test-data"); w.OpenPaths(new[] { Path.Combine(dir, "Sample_03.png") }); await Task.Delay(500); Check(w.TitleLabel.Text == "Sample_03.png" && w.DetailLabel.Text.Contains("1,200"), "shell file argument opens requested image");
        void Layout() { w.Root.Measure(new Size(1400, 850)); w.Root.Arrange(new Rect(0, 0, 1400, 850)); w.Root.UpdateLayout(); }
        void Capture(string filename) { Layout(); var bitmap = new RenderTargetBitmap(1400, 850, 96, 96, PixelFormats.Pbgra32); bitmap.Render(w.Root); Imaging.Save(bitmap, Path.Combine(AppContext.BaseDirectory, filename)); }
        Check(w.Sidebar.Visibility==Visibility.Collapsed&&w.Rail.Visibility==Visibility.Visible,"file sidebar hidden by default; rail remains visible"); w.PageCount.SelectedIndex = 3; await Task.Delay(300); Layout(); Check(w.Pages.Children.Count == 4 && w.Pages.RowDefinitions.Count == 2 && w.Pages.ColumnDefinitions.Count == 2, "four image grid"); Capture("preview-light.png");ThemeTests.Verify(w,lines,"light");
        w.LayoutMode.SelectedIndex = 0; await Task.Delay(100); Check(w.Pages.ColumnDefinitions.Count == 4 && w.Pages.RowDefinitions.Count == 1, "horizontal arrangement"); w.LayoutMode.SelectedIndex = 2; await Task.Delay(100); Check(w.Pages.ColumnDefinitions.Count == 1 && w.Pages.RowDefinitions.Count == 4, "vertical arrangement"); w.PageCount.SelectedIndex = 2; Check(w.Pages.Children.Count == 3, "three image view"); w.PageCount.SelectedIndex = 1; Check(w.Pages.Children.Count == 2, "two image view");
        w.LayoutMode.SelectedIndex = 1; w.PageCount.SelectedIndex = 3; Invoke("ToggleTheme", w, new RoutedEventArgs()); Capture("preview-dark.png");ThemeTests.Verify(w,lines,"dark"); Check(((SolidColorBrush)Application.Current.Resources["Bg"]).Color.R == 38, "dark palette"); Invoke("SetZoom", 400.0); Layout(); Check(w.FitMode.SelectedIndex == 4 && w.ZoomInput.Text == "400", "custom zoom"); Check(w.Pages.Children.Cast<ImagePane>().All(p => p.Children.OfType<Border>().Any(b => b.Visibility == Visibility.Visible)), "zoom minimaps visible");
        Invoke("ToggleExif", w, new RoutedEventArgs()); Check(w.ExifText.Text.Contains("Sample_03.png") && w.ExifText.Text.Contains("bytes"), "EXIF panel metadata"); Invoke("ToggleExif", w, new RoutedEventArgs());
        w.PageCount.SelectedIndex=0; w.OpenPaths(new[]{Path.Combine(dir,"Sample_03.png")}); await Task.Delay(100); Invoke("SetFull", true); Check(w.ViewToolbar.Visibility == Visibility.Collapsed && w.BottomBar.Visibility == Visibility.Collapsed && w.ViewToolbar.Visibility == Visibility.Collapsed, "fullscreen starts without menus"); Invoke("OnKey", w, new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); await Task.Delay(200); Check(w.TitleLabel.Text == "Sample_04.png" && w.ViewToolbar.Visibility == Visibility.Collapsed, "Space advances without revealing fullscreen UI"); Invoke("OnKey", w, new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent }); Check(w.ViewToolbar.Visibility == Visibility.Visible && w.WindowHeader.Visibility==Visibility.Visible&&System.Windows.Shell.WindowChrome.GetWindowChrome(w)!=null, "Escape restores window and menus");
        w.PageCount.SelectedIndex = 0; w.FitMode.SelectedIndex = 0; w.FpsInput.Text = "30"; Invoke("TogglePlay", w, new RoutedEventArgs()); Check(System.Windows.Automation.AutomationProperties.GetName(w.PlayButton)=="정지", "30fps playback starts"); await Task.Delay(250); Invoke("Stop"); Check(System.Windows.Automation.AutomationProperties.GetName(w.PlayButton)=="재생", "playback stops");
        var ed = new EditorWindow(Imaging.Load(Path.Combine(dir, "Sample_01.png")), Path.Combine(dir, "Sample_01.png")); ed.Measure(new Size(1200, 800)); ed.Arrange(new Rect(0, 0, 1200, 800)); Check(ed.Content != null, "editor initializes"); ed.Close(); var tone = new ToneWindow(Imaging.Load(Path.Combine(dir, "Sample_01.png"))); tone.Measure(new Size(1000, 700)); tone.Arrange(new Rect(0, 0, 1000, 700)); Check(tone.Content != null, "levels and curves initialize"); tone.Close(); await GifTests.Run(dir,lines);w.OpenPaths(new[]{Path.Combine(dir,"Animation.gif")});await Task.Delay(230);Check(w.GifButton.Visibility==Visibility.Visible&&w.Pages.Children.OfType<ImagePane>().Any(p=>p.IsAnimated),"GIF playback integrated into viewer");w.OpenPaths(new[]{Path.Combine(dir,"Sample_01.png")});await Task.Delay(100);Check(w.Pages.Children.OfType<ImagePane>().All(p=>!p.IsAnimated),"GIF stops on image navigation");EditorTests.Run(dir,lines);await BrowserTests.Run(w,dir,lines);await FrameAndPagingTests.Run(w,dir,lines); Check(w.FindName("TopBar")==null,"duplicate image title panel removed");L10n.Instance.English=true;Layout();Check(((ComboBoxItem)w.FitMode.Items[0]).Content.ToString()=="Fit window","English mode updates existing UI");L10n.Instance.English=false;Layout();Check(((ComboBoxItem)w.FitMode.Items[0]).Content.ToString()=="창 맞춤","Korean mode restores existing UI");File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "ui-test.txt"), lines); w.Close();
    }
}






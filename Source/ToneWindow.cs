using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
namespace MooView;

public sealed class ToneWindow : Window
{
    readonly BitmapSource original, previewSource; readonly Image preview = new() { Stretch = Stretch.Uniform }; readonly Slider black = new() { Minimum = 0, Maximum = 254, Value = 0 }, white = new() { Minimum = 1, Maximum = 255, Value = 255 }, gamma = new() { Minimum = .1, Maximum = 3, Value = 1 }; readonly CurveControl curve = new(); readonly System.Windows.Threading.DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(90) }; public BitmapSource? Result { get; private set; }
    public ToneWindow(BitmapSource source) { Style = (Style)FindResource(typeof(Window)); original = source; double scale = Math.Min(1, 700.0 / Math.Max(source.PixelWidth, source.PixelHeight)); previewSource = scale < 1 ? new TransformedBitmap(source, new ScaleTransform(scale, scale)) : source; Title = L10n.T("Tone"); Width = 1000; Height = 700; WindowStartupLocation = WindowStartupLocation.CenterOwner; var grid = new Grid { Margin = new Thickness(20) }; grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) }); Content = grid; preview.Source = source; grid.Children.Add(preview); var panel = new StackPanel { Margin = new Thickness(18, 0, 0, 0) }; Grid.SetColumn(panel, 1); grid.Children.Add(panel); LabelSlider(panel, L10n.T("Black"), black); LabelSlider(panel, L10n.T("White"), white); LabelSlider(panel, L10n.T("Gamma"), gamma); panel.Children.Add(new TextBlock { Text = L10n.T("CurveHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 16, 0, 10) }); panel.Children.Add(curve); var reset = new Button { Content = new Glyph{Kind="undo"}, ToolTip=L10n.T("Reset") }; reset.Click += (_, _) => { black.Value = 0; white.Value = 255; gamma.Value = 1; curve.Reset(); Queue(); }; panel.Children.Add(reset); var apply = new Button { Content = new Glyph{Kind="check"}, ToolTip=L10n.T("Apply") }; apply.Click += (_, _) => { if (black.Value >= white.Value) { MessageBox.Show(this, L10n.Choose("화이트 포인트는 블랙 포인트보다 커야 합니다.","The white point must be greater than the black point.")); return; } try { Result = Imaging.Adjust(original, Lut()); DialogResult = true; } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }; panel.Children.Add(apply); var cancel = new Button { Content = new Glyph{Kind="close"}, ToolTip=L10n.T("Cancel"), IsCancel = true }; panel.Children.Add(cancel); black.ValueChanged += (_, _) => Queue(); white.ValueChanged += (_, _) => Queue(); gamma.ValueChanged += (_, _) => Queue(); curve.Changed += Queue; timer.Tick += (_, _) => { timer.Stop(); if (black.Value < white.Value) preview.Source = Imaging.Adjust(previewSource, Lut()); }; Closed += (_, _) => timer.Stop(); }
    void LabelSlider(Panel p, string label, Slider s) { var text = new TextBlock { Text = label + ": " + s.Value.ToString("0.##"), Margin = new Thickness(0, 9, 0, 5) }; p.Children.Add(text); p.Children.Add(s); s.ValueChanged += (_, _) => text.Text = label + ": " + s.Value.ToString("0.##"); }
    byte[] Lut() => Imaging.ToneLut(black.Value, white.Value, gamma.Value, curve.Points); void Queue() { timer.Stop(); timer.Start(); }
}
public sealed class CurveControl : FrameworkElement
{
    public List<Point> Points { get; } = [new(0, 0), new(1, 1)]; int dragging = -1; public event Action? Changed; public CurveControl() { Width = 250; Height = 220; Cursor = Cursors.Cross; MouseLeftButtonDown += Down; MouseMove += Move; MouseLeftButtonUp += (_, _) => { dragging = -1; ReleaseMouseCapture(); }; MouseRightButtonDown += (_, e) => { int n = Nearest(e.GetPosition(this)); if (n > 0 && n < Points.Count - 1) { Points.RemoveAt(n); InvalidateVisual(); Changed?.Invoke(); } }; }
    public void Reset() { Points.Clear(); Points.Add(new(0, 0)); Points.Add(new(1, 1)); InvalidateVisual(); Changed?.Invoke(); }
    Point Screen(Point p) => new(p.X * ActualWidth, (1 - p.Y) * ActualHeight); Point Value(Point p) => new(Math.Clamp(p.X / ActualWidth, 0, 1), Math.Clamp(1 - p.Y / ActualHeight, 0, 1));
    int Nearest(Point p) { for (int i = 0; i < Points.Count; i++) if ((Screen(Points[i]) - p).Length < 12) return i; return -1; }
    void Down(object s, MouseButtonEventArgs e) { var p = e.GetPosition(this); dragging = Nearest(p); if (dragging < 0) { var v = Value(p); if (v.X < .005 || v.X > .995) return; Points.Add(v); Points.Sort((a, b) => a.X.CompareTo(b.X)); dragging = Points.IndexOf(v); } CaptureMouse(); InvalidateVisual(); Changed?.Invoke(); }
    void Move(object s, MouseEventArgs e) { if (dragging < 0 || e.LeftButton != MouseButtonState.Pressed) return; var v = Value(e.GetPosition(this)); double x = dragging == 0 ? 0 : dragging == Points.Count - 1 ? 1 : Math.Clamp(v.X, Points[dragging - 1].X + .001, Points[dragging + 1].X - .001); Points[dragging] = new Point(x, v.Y); InvalidateVisual(); Changed?.Invoke(); }
    protected override void OnRender(DrawingContext dc) { dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(35, 43, 38)), null, new Rect(0, 0, ActualWidth, ActualHeight)); var grid = new Pen(new SolidColorBrush(Color.FromRgb(65, 78, 68)), 1); for (int i = 0; i < 5; i++) { double x = ActualWidth * i / 4, y = ActualHeight * i / 4; dc.DrawLine(grid, new Point(x, 0), new Point(x, ActualHeight)); dc.DrawLine(grid, new Point(0, y), new Point(ActualWidth, y)); } dc.DrawLine(grid, new Point(0, ActualHeight), new Point(ActualWidth, 0)); for (int i = 1; i < Points.Count; i++) dc.DrawLine(new Pen(Brushes.LightGreen, 2), Screen(Points[i - 1]), Screen(Points[i])); foreach (var p in Points) dc.DrawEllipse(Brushes.White, new Pen(Brushes.SeaGreen, 2), Screen(p), 5, 5); }
}




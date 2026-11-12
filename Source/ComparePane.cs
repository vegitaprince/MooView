using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal readonly record struct CompareTone(double Exposure = 1, double Contrast = 1);
internal static class ComparePixels
{
    internal static BitmapSource Adjust(BitmapSource source, CompareTone tone, int channels, bool gray)
    {
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); int stride = checked(source.PixelWidth * 4); var bytes = new byte[checked(stride * source.PixelHeight)]; converted.CopyPixels(bytes, stride, 0);
        Parallel.For(0, source.PixelHeight, y => { for (int p = y * stride; p < (y + 1) * stride; p += 4) {
            double r = Tone(bytes[p + 2], tone), g = Tone(bytes[p + 1], tone), b = Tone(bytes[p], tone);
            if (gray) { int count = ((channels & 1) != 0 ? 1 : 0) + ((channels & 2) != 0 ? 1 : 0) + ((channels & 4) != 0 ? 1 : 0); double value = (((channels & 1) != 0 ? r : 0) + ((channels & 2) != 0 ? g : 0) + ((channels & 4) != 0 ? b : 0)) / Math.Max(1, count); r = g = b = value; }
            else { if ((channels & 1) == 0) r = 0; if ((channels & 2) == 0) g = 0; if ((channels & 4) == 0) b = 0; }
            bytes[p] = (byte)Math.Round(b); bytes[p + 1] = (byte)Math.Round(g); bytes[p + 2] = (byte)Math.Round(r);
        } });
        var image = BitmapSource.Create(source.PixelWidth, source.PixelHeight, 96, 96, PixelFormats.Bgra32, null, bytes, stride); image.Freeze(); return image;
    }
    static double Tone(byte value, CompareTone tone) => Math.Clamp((value * tone.Exposure - 127.5) * tone.Contrast + 127.5, 0, 255);
    internal static Rect Contain(Size canvas, Size image) { double scale = Math.Min(canvas.Width / image.Width, canvas.Height / image.Height); return new Rect((canvas.Width - image.Width * scale) / 2, (canvas.Height - image.Height * scale) / 2, image.Width * scale, image.Height * scale); }
    internal static BitmapSource Export(BitmapSource a, BitmapSource b, double wipe, double opacity)
    {
        int w = a.PixelWidth, h = a.PixelHeight; var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) {
            dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, w, h)); dc.DrawImage(b, Contain(new Size(w, h), new Size(b.PixelWidth, b.PixelHeight)));
            dc.PushClip(new RectangleGeometry(new Rect(0, 0, w * Math.Clamp(wipe, 0, 1), h))); dc.PushOpacity(Math.Clamp(opacity, 0, 1)); dc.DrawImage(a, new Rect(0, 0, w, h)); dc.Pop(); dc.Pop();
        } var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
}
public sealed class ComparePane : FrameworkElement
{
    BitmapSource? a, b; double wipe = .5, opacity = 1, scale = 1, panX, panY; int fit; double percent = 100; Point? drag; double dragX, dragY; int gesture;
    internal BitmapSource? ImageA => a; internal BitmapSource? ImageB => b;
    Brush backdrop = Brushes.Transparent;
    internal Brush Backdrop { get => backdrop; set { backdrop = value; InvalidateVisual(); } }
    internal double Scale => scale; internal Point Pan => new(panX, panY);
    public double Wipe { get => wipe; set { wipe = Math.Clamp(value, 0, 1); InvalidateVisual(); } }
    public double AOpacity { get => opacity; set { opacity = Math.Clamp(value, 0, 1); InvalidateVisual(); } }
    public event Action<double>? ZoomRequested;
    public ComparePane() { ClipToBounds = true; Focusable = true; SnapsToDevicePixels = true; UseLayoutRounding = true; SizeChanged += (_, _) => Fit(); LostMouseCapture += (_, _) => drag = null; }
    internal Rect ImageBounds => a == null ? Rect.Empty : new Rect((ActualWidth - a.PixelWidth * scale) / 2 + panX, (ActualHeight - a.PixelHeight * scale) / 2 + panY, a.PixelWidth * scale, a.PixelHeight * scale);
    Rect MapBounds => new(Math.Max(0, ActualWidth - 184), Math.Max(0, ActualHeight - 132), 164, 112);
    Rect HandleBounds => new(Math.Clamp(ActualWidth * wipe - 19, 1, Math.Max(1, ActualWidth - 39)), ActualHeight / 2 - 22, 38, 44);
    bool IsDivider(Point p) => Math.Abs(p.X - wipe * ActualWidth) < 12 || HandleBounds.Contains(p);
    bool MapVisible => a != null && (ImageBounds.Width > ActualWidth + 2 || ImageBounds.Height > ActualHeight + 2);
    internal bool IsImageInteraction(Point p) => a != null && (ImageBounds.Contains(p) || IsDivider(p) || (MapVisible && MapBounds.Contains(p)));
    internal void SetImages(BitmapSource first, BitmapSource second, bool reset = false) { a = first; b = second; if (reset) { panX = panY = 0; wipe = .5; } Fit(); }
    internal void Clear() { a = b = null; drag = null; if (Mouse.Captured == this) ReleaseMouseCapture(); InvalidateVisual(); }
    internal void SetZoom(int mode, double zoom) { fit = mode; percent = zoom; if (mode != 4) panX = panY = 0; Fit(); }
    void Fit() { if (a == null || ActualWidth <= 0 || ActualHeight <= 0) return; scale = fit switch { 0 => Math.Min(ActualWidth / a.PixelWidth, ActualHeight / a.PixelHeight), 1 => Math.Max(ActualWidth / a.PixelWidth, ActualHeight / a.PixelHeight), 2 => ActualWidth / a.PixelWidth, 3 => 1, _ => percent / 100 }; ClampPan(); InvalidateVisual(); }
    void ClampPan() { if (a == null) return; double maxX = Math.Max(0, (a.PixelWidth * scale - ActualWidth) / 2), maxY = Math.Max(0, (a.PixelHeight * scale - ActualHeight) / 2); panX = Math.Clamp(panX, -maxX, maxX); panY = Math.Clamp(panY, -maxY, maxY); }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(backdrop, null, new Rect(RenderSize)); if (a == null || b == null) return;
        Rect bounds = ImageBounds; dc.DrawRectangle(Brushes.Black, null, bounds);
        Rect br = ComparePixels.Contain(new Size(a.PixelWidth, a.PixelHeight), new Size(b.PixelWidth, b.PixelHeight)); br = new Rect(bounds.X + br.X * scale, bounds.Y + br.Y * scale, br.Width * scale, br.Height * scale); dc.DrawImage(b, br);
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth * wipe, ActualHeight))); dc.PushOpacity(opacity); dc.DrawImage(a, bounds); dc.Pop(); dc.Pop();
        double x = Math.Round(ActualWidth * wipe); dc.DrawLine(new Pen(Brushes.Black, 3), new Point(x, 0), new Point(x, ActualHeight)); dc.DrawLine(new Pen(Brushes.White, 1), new Point(x, 0), new Point(x, ActualHeight));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(40, 40, 40)), new Pen(Brushes.White, 1), HandleBounds, 12, 12);
        var pen = new Pen(Brushes.White, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }; double cx = Math.Clamp(x, 20, Math.Max(20, ActualWidth - 20)), cy = ActualHeight / 2;
        dc.DrawLine(pen, new Point(cx - 5, cy - 5), new Point(cx - 10, cy)); dc.DrawLine(pen, new Point(cx - 10, cy), new Point(cx - 5, cy + 5)); dc.DrawLine(pen, new Point(cx + 5, cy - 5), new Point(cx + 10, cy)); dc.DrawLine(pen, new Point(cx + 10, cy), new Point(cx + 5, cy + 5));
        if (MapVisible) { Rect map = MapBounds, image = ComparePixels.Contain(map.Size, new Size(a.PixelWidth, a.PixelHeight)); image.Offset(map.X, map.Y); dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235, 25, 25, 25)), new Pen(Brushes.Gray, 1), new Rect(map.X - 6, map.Y - 6, map.Width + 12, map.Height + 12), 8, 8); dc.DrawImage(a, image); double ratio = image.Width / bounds.Width; Rect viewport = new(image.X + Math.Max(0, -bounds.X) * ratio, image.Y + Math.Max(0, -bounds.Y) * ratio, Math.Min(bounds.Width, ActualWidth) * ratio, Math.Min(bounds.Height, ActualHeight) * ratio); dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), new Pen(Brushes.White, 1), viewport); }
    }
    internal void PanToMap(Point p) { if (a == null) return; Rect map = MapBounds, image = ComparePixels.Contain(map.Size, new Size(a.PixelWidth, a.PixelHeight)); image.Offset(map.X, map.Y); panX = (.5 - (p.X - image.X) / image.Width) * a.PixelWidth * scale; panY = (.5 - (p.Y - image.Y) / image.Height) * a.PixelHeight * scale; ClampPan(); InvalidateVisual(); }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) { var p = e.GetPosition(this); if (!IsImageInteraction(p)) return; Focus(); gesture = MapVisible && MapBounds.Contains(p) ? 2 : IsDivider(p) ? 1 : 0; drag = p; dragX = panX; dragY = panY; if (gesture == 2) PanToMap(p); else if (gesture == 1) Wipe = p.X / ActualWidth; CaptureMouse(); e.Handled = true; }
    protected override void OnMouseMove(MouseEventArgs e) { var p = e.GetPosition(this); Cursor = IsDivider(p) ? Cursors.SizeWE : IsImageInteraction(p) ? Cursors.Hand : Cursors.Arrow; if (drag is not Point start || e.LeftButton != MouseButtonState.Pressed) return; if (gesture == 1) Wipe = p.X / ActualWidth; else if (gesture == 2) PanToMap(p); else { panX = dragX + p.X - start.X; panY = dragY + p.Y - start.Y; ClampPan(); InvalidateVisual(); } e.Handled = true; }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) { if (drag == null) return; drag = null; ReleaseMouseCapture(); e.Handled = true; }
    protected override void OnMouseWheel(MouseWheelEventArgs e) { if (a == null) return; Point p = e.GetPosition(this); var old = ImageBounds; double ix = (p.X - old.X) / scale, iy = (p.Y - old.Y) / scale; double next = Math.Clamp(scale * (e.Delta > 0 ? 1.15 : 1 / 1.15) * 100, 1, 3200); ZoomRequested?.Invoke(next); panX = p.X - ActualWidth / 2 + a.PixelWidth * scale / 2 - ix * scale; panY = p.Y - ActualHeight / 2 + a.PixelHeight * scale / 2 - iy * scale; ClampPan(); InvalidateVisual(); e.Handled = true; }
}

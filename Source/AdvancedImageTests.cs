using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class AdvancedImageTests
{
    internal static async Task Run(MainWindow w, string dir, List<string> lines)
    {
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "advanced-progress.txt"), "Starting EXR / PSD / comparison tests\n");
        void Check(bool ok, string label) { if (!ok) throw new Exception("FAIL: " + label); lines.Add("PASS: " + label); File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "advanced-progress.txt"), "PASS: " + label + "\n"); }
        void Layout() { w.Root.Measure(new Size(1400, 850)); w.Root.Arrange(new Rect(0, 0, 1400, 850)); w.Root.UpdateLayout(); }
        string fixtures = Path.Combine(dir, "advanced"); Directory.CreateDirectory(fixtures);
        foreach (string name in typeof(ExrImage).Assembly.GetManifestResourceNames().Where(n => n.StartsWith("MooView.Fixtures."))) { using var source = typeof(ExrImage).Assembly.GetManifestResourceStream(name)!; using var target = File.Create(Path.Combine(fixtures, name["MooView.Fixtures.".Length..])); source.CopyTo(target); }
        foreach (string path in Directory.EnumerateFiles(fixtures, "compression-*.exr")) { var bitmap = Imaging.Load(path); byte[] pixel = Pixel(bitmap); Check(bitmap.PixelWidth == 32 && bitmap.PixelHeight == 24 && Math.Abs(pixel[2] - 137) <= 3 && Math.Abs(pixel[1] - 188) <= 3 && pixel[0] >= 252, "OpenEXR HALF decode " + Path.GetFileNameWithoutExtension(path)); }
        string layers = Path.Combine(fixtures, "layers.exr"); var parts = ExrImage.Inspect(layers); var views = ExrImage.Views(parts); Check(parts.Length == 1 && parts[0].Channels.Length == 7 && views.Any(v => v.Label == "diffuse"), "EXR named layers and scalar channels enumerated");
        var diffuse = ExrImage.Load(layers, views.Single(v => v.Label == "diffuse")); Check(Pixel(diffuse).Take(3).All(b => b == 89), "selected EXR layer shows its own FLOAT pixels");
        var z = ExrImage.Load(layers, views.Single(v => v.Red == "Z")); Check(Pixel(z).Take(3).All(b => b == 89), "EXR scalar Z displayed as grayscale");
        Check(ExrImage.Load(layers, exposure: 1, srgb: false) is BitmapSource linear && Pixel(linear)[2] == 128 && Pixel(linear)[1] == 255, "EXR EV and linear display transfer");
        var multipart = ExrImage.Inspect(Path.Combine(fixtures, "multipart.exr")); Check(multipart.Length == 2 && ExrImage.Views(multipart).Any(v => v.Label.StartsWith("lighting / diffuse")), "multipart EXR layer selection");
        Check(Pixel(ExrImage.Load(Path.Combine(fixtures, "tiled.exr")))[1] == 188, "tiled EXR base level decoded");
        var offset = ExrImage.Inspect(Path.Combine(fixtures, "offset.exr")); Check(offset[0].X == -8 && offset[0].Y == 12 && Pixel(ExrImage.Load(Path.Combine(fixtures, "offset.exr")))[2] == 137, "EXR nonzero negative data-window origin");
        string unicode = Path.Combine(fixtures, "한글_레이어.exr"); File.Copy(layers, unicode, true); Check(ExrImage.Load(unicode).PixelWidth == 32, "Unicode EXR filename");
        string bad = Path.Combine(fixtures, "broken.exr"); File.WriteAllBytes(bad, [0x76, 0x2f, 0x31, 0x01, 0, 0]); bool rejected = false; try { Imaging.Load(bad); } catch (IOException) { rejected = true; } Check(rejected, "truncated EXR returns a controlled error");
        Check(ExrImage.Display(double.NaN, 1, true) == 0 && ExrImage.Display(double.PositiveInfinity, 1, true) == 255 && ExrImage.Display(-1, 1, true) == 0, "EXR NaN infinity and negative display handling");
        foreach (int depth in new[] { 8, 16 }) { string psd = Path.Combine(fixtures, $"composite-{depth}.psd"); WritePsd(psd, depth); var image = Imaging.Load(psd); byte[] p = Pixel(image); Check(image.PixelWidth == 8 && image.PixelHeight == 6 && p[2] == 255 && p[1] == 128 && p[0] == 0, $"PSD {depth}-bit composite decoded"); }
        var red = Solid(100, 60, Colors.Red); var blue = Solid(100, 60, Colors.Blue); var export = ComparePixels.Export(red, blue, .5, 1); Check(Pixel(export, 20, 20)[2] == 255 && Pixel(export, 80, 20)[0] == 255, "comparison export uses A left and B right at original A dimensions");
        var blend = Pixel(ComparePixels.Export(red, blue, 1, .5), 20, 20); Check(Math.Abs(blend[0] - 128) <= 1 && Math.Abs(blend[2] - 128) <= 1, "A opacity composites over B");
        var letterbox = ComparePixels.Export(red, Solid(10, 60, Colors.Blue), 0, 1); Check(Pixel(letterbox, 0, 0).Take(3).All(b => b == 0) && Pixel(letterbox, 50, 20)[0] == 255, "comparison contains B without stretching");
        var filtered = ComparePixels.Adjust(Solid(1, 1, Color.FromRgb(120, 60, 30)), new(1, 1), 1, false); Check(Pixel(filtered).SequenceEqual(new byte[] { 0, 0, 120, 255 }), "RGB channel isolate preserves selected channel");
        var gray = ComparePixels.Adjust(Solid(1, 1, Color.FromRgb(120, 60, 30)), new(1, 1), 3, true); Check(Pixel(gray).Take(3).All(b => b == 90), "B/W averages only selected channels");
        string aPath = Path.Combine(fixtures, "A.png"), bPath = Path.Combine(fixtures, "B.png"); Imaging.Save(red, aPath); Imaging.Save(blue, bPath);
        w.ShowActivated = false; w.Show(); w.PageCount.SelectedIndex = 0; w.FitMode.SelectedIndex = 0; w.OpenPaths([aPath]); await Task.Delay(250); Layout();
        var pane = w.Pages.Children.OfType<ImagePane>().First(); Point drop = pane.TranslatePoint(new Point(pane.ActualWidth / 2, pane.ActualHeight / 2), w.ImageArea);
        Check(w.TryCompareDrop([bPath], drop), "file drop on displayed image routes to comparison"); for (int i = 0; i < 100 && !w.Comparing; i++) await Task.Delay(20); Layout();
        Check(w.Comparing && w.CompareButton.IsChecked == true && w.Pages.Visibility == Visibility.Collapsed && w.CompareBar.Visibility == Visibility.Visible && !w.PlayButton.IsEnabled, "comparison enters with visible controls and paused slideshow");
        Check(w.FileNameLabel.Text.Contains("A.png") && w.FileNameLabel.Text.Contains("B.png"), "comparison identifies A and B filenames");
        Point center = w.Comparison.TranslatePoint(new Point(w.Comparison.ActualWidth / 2, w.Comparison.ActualHeight / 2), w); Check(!w.CanDragWindow(w.Comparison, center), $"comparison divider and image exclude window dragging ({w.Comparison.RenderSize}; {w.Comparison.ImageBounds}; {w.TranslatePoint(center,w.Comparison)}; {w.Comparison.Wipe})");
        w.Comparison.Wipe = 1.5; Check(w.Comparison.Wipe == 1, "comparison divider clamps right edge"); w.Comparison.Wipe = -.3; Check(w.Comparison.Wipe == 0 && w.Comparison.IsImageInteraction(new Point(20, w.Comparison.ActualHeight / 2)), "comparison divider clamps left edge and handle remains interactive"); w.Comparison.Wipe = .38;
        w.SelectChannel(1); await w.ApplyCompareTone(); Check(Pixel(w.Comparison.ImageB!).Take(3).All(b => b == 0), "live comparison channel control filters B"); w.SelectChannel(0); await w.ApplyCompareTone();
        var setZoom = typeof(MainWindow).GetMethod("SetZoom", BindingFlags.Instance | BindingFlags.NonPublic)!; setZoom.Invoke(w, [1800.0]); Layout(); Check(w.Comparison.Scale == 18, "shared comparison zoom follows toolbar"); w.Comparison.PanToMap(new Point(10000, 10000)); Check(w.Comparison.Pan.X <= 0 && w.Comparison.Pan.Y <= 0, "comparison minimap pans both images with bounded coordinates");
        w.SwapComparison(); await w.ApplyCompareTone(); Check(Pixel(w.Comparison.ImageA!)[0] == 255 && w.FileNameLabel.Text.StartsWith("A: B.png"), "swap exchanges comparison pixels and filenames");
        await Task.WhenAll(w.ReplaceCompareSide(true, aPath), w.ReplaceCompareSide(false, bPath)); Check(Pixel(w.Comparison.ImageA!)[2] == 255 && Pixel(w.Comparison.ImageB!)[0] == 255, "concurrent A and B replacement preserves both selections");
        w.FitMode.SelectedIndex = 0; Layout(); var capture = new RenderTargetBitmap(1400, 850, 96, 96, PixelFormats.Pbgra32); capture.Render(w.Root); Imaging.Save(capture, Path.Combine(AppContext.BaseDirectory, "preview-compare.png"));
        void Call(string name, params object[] args) => typeof(MainWindow).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(w, args);
        Call("SetFull", true); Check(w.CompareBar.Visibility == Visibility.Collapsed && w.ToolbarRow.Visibility == Visibility.Collapsed, "comparison fullscreen starts with hidden menus");
        var inputSource = (System.Windows.Interop.HwndSource)PresentationSource.FromVisual(w)!; var space = new KeyEventArgs(Keyboard.PrimaryDevice, inputSource, 0, Key.Space) { RoutedEvent = Keyboard.PreviewKeyDownEvent }; Call("OnKey", w, space); Check(w.Comparing && w.CompareBar.Visibility == Visibility.Collapsed && space.Handled, "Space preserves frozen comparison without revealing fullscreen menus");
        Call("SetFull", false); Check(w.CompareBar.Visibility == Visibility.Visible, "comparison controls return on fullscreen exit");
        w.ExitCompare(); await Task.Delay(150); Check(!w.Comparing && w.Comparison.ImageA == null && w.Comparison.ImageB == null && w.Pages.Visibility == Visibility.Visible && w.PlayButton.IsEnabled, "comparison exit releases images and restores viewer and playback controls");
        var canceled = w.StartComparison(layers, bPath); w.ExitCompare(false); await canceled; Check(!w.Comparing, "leaving comparison cancels an in-flight image load");
        w.OpenPaths([layers]); await Task.Delay(250); Layout(); Check(w.ExrBar.Visibility == Visibility.Visible, "EXR layer toolbar appears for the current image"); var layerBox = w.ExrBar.Children.OfType<ComboBox>().Single(); layerBox.SelectedItem = layerBox.Items.Cast<ExrView>().Single(v => v.Label == "diffuse"); await w.ApplyExrLayer(); Check(Pixel(w.Pages.Children.OfType<ImagePane>().First().CurrentBitmap!)[2] == 89, "EXR layer toolbar updates displayed image");
        await w.StartComparison(layers, aPath); Layout(); Check(w.CompareBar.Children.OfType<ComboBox>().Any(c => c.Visibility == Visibility.Visible && c.Items.Count == views.Count), "EXR layers remain available in comparison mode");
        w.ExitCompare(false); w.OpenPaths([Path.Combine(dir, "Sample_01.png")]); await Task.Delay(100); Check(w.ExrBar.Visibility == Visibility.Collapsed, "EXR toolbar hides on ordinary images");
    }
    internal static byte[] Pixel(BitmapSource source, int x = 0, int y = 0) { var image = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); byte[] p = new byte[4]; image.CopyPixels(new Int32Rect(x, y, 1, 1), p, 4, 0); return p; }
    static BitmapSource Solid(int w, int h, Color color) { byte[] pixels = new byte[w * h * 4]; for (int i = 0; i < pixels.Length; i += 4) { pixels[i] = color.B; pixels[i + 1] = color.G; pixels[i + 2] = color.R; pixels[i + 3] = color.A; } var image = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, pixels, w * 4); image.Freeze(); return image; }
    static void WritePsd(string path, int depth) { using var file = File.Create(path); using var writer = new BinaryWriter(file); void U16(int n) { writer.Write((byte)(n >> 8)); writer.Write((byte)n); } void U32(int n) { U16(n >> 16); U16(n); } writer.Write("8BPS"u8); U16(1); writer.Write(new byte[6]); U16(3); U32(6); U32(8); U16(depth); U16(3); U32(0); U32(0); U32(0); U16(0); foreach (int c in new[] { 255, 128, 0 }) for (int i = 0; i < 48; i++) { if (depth == 16) U16(c * 257); else writer.Write((byte)c); } }
}

using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

public static class SelfTest
{
    public static void Run()
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "test-data"); Directory.CreateDirectory(dir); var log = new List<string>(); void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); }
        var pixels = new byte[] { 0, 0, 255, 255, 0, 255, 0, 255, 255, 0, 0, 255, 255, 255, 255, 128, 0, 0, 0, 0, 64, 64, 64, 255 }; var source = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgra32, null, pixels, 12); source.Freeze();
        foreach (var ext in new[] { ".png", ".jpg", ".bmp", ".tiff", ".gif" }) { string p = Path.Combine(dir, "roundtrip" + ext); Imaging.Save(source, p); var b = Imaging.Load(p); Check(b.PixelWidth == 3 && b.PixelHeight == 2, "format roundtrip " + ext); }
        var png = Imaging.Load(Path.Combine(dir, "roundtrip.png")); var raw = new FormatConvertedBitmap(png, PixelFormats.Bgra32, null, 0); var bytes = new byte[24]; raw.CopyPixels(bytes, 12, 0); Check(bytes[15] == 128 && bytes[19] == 0, "PNG alpha retained");
        var rotated = Imaging.Transform(source, 90); Check(rotated.PixelWidth == 2 && rotated.PixelHeight == 3, "rotation dimensions"); var flipped = Imaging.Transform(source, 0, -1, 1); var flipBytes = new byte[24]; flipped.CopyPixels(flipBytes, 12, 0); Check(flipBytes[0] == 255 && flipBytes[2] == 0, "horizontal flip pixels");
        var identity = Imaging.ToneLut(0, 255, 1, new[] { new Point(0, 0), new Point(1, 1) }); Check(identity.Select((v, i) => v == i).All(v => v), "identity tone LUT"); var inverse = Imaging.ToneLut(0, 255, 1, new[] { new Point(0, 1), new Point(1, 0) }); Check(inverse[0] == 255 && inverse[255] == 0, "curve inversion"); var adjusted = Imaging.Adjust(source, inverse); var adj = new byte[24]; adjusted.CopyPixels(adj, 12, 0); Check(adj[2] == 0 && adj[0] == 255 && adj[15] == 128, "tone changes RGB and preserves alpha");
        var crop = new CroppedBitmap(source, new Int32Rect(1, 0, 2, 2)); Check(crop.PixelWidth == 2 && crop.PixelHeight == 2, "crop dimensions");
        string a = Path.Combine(dir, "rename-a.png"), bpath = Path.Combine(dir, "rename-b.png"); File.WriteAllText(a, "A"); File.WriteAllText(bpath, "B"); Imaging.Rename(new[] { a, bpath }, new[] { bpath, a }); Check(File.ReadAllText(a) == "B" && File.ReadAllText(bpath) == "A", "transactional rename swap"); bool collision = false; try { Imaging.RenameTargets(new[] { a, bpath }, "same", 1); } catch (ArgumentException) { collision = true; }
        Check(collision, "duplicate rename rejected");
        Check(NaturalComparer.Instance.Compare("image2.png", "image10.png") < 0, "natural image ordering");
        for (int i = 1; i <= 8; i++) { var visual = new DrawingVisual(); using (var dc = visual.RenderOpen()) { dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb((byte)(25 + i * 10), (byte)(65 + i * 8), (byte)(80 + i * 10)), Color.FromRgb(20, 30, 32), 45), null, new Rect(0, 0, 1200, 800)); dc.DrawEllipse(new SolidColorBrush(Color.FromRgb((byte)(180 + i * 6), (byte)(210 - i * 5), 160)), null, new Point(820, 260), 140, 140); dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(35, 65, 57)), null, Geometry.Parse("M0,720 L350,260 650,620 900,400 1200,700 1200,800 0,800 Z")); dc.DrawText(new FormattedText("MOO / " + i.ToString("00"), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 58, Brushes.White, 1), new Point(70, 650)); } var render = new RenderTargetBitmap(1200, 800, 96, 96, PixelFormats.Pbgra32); render.Render(visual); Imaging.Save(render, Path.Combine(dir, $"Sample_{i:00}.png")); }
        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), log);
    }
}


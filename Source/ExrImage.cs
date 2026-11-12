using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal sealed record ExrChannel(string Name, int Type, int XSampling, int YSampling);
internal sealed record ExrPart(int Index, string Name, string Type, int Width, int Height, int X, int Y, ExrChannel[] Channels);
internal sealed record ExrView(ExrPart Part, string Label, string Red, string Green, string Blue, string? Alpha = null)
{
    public override string ToString() => Label;
}
internal static class ExrImage
{
    const string Dll = "MooViewExr.dll";
    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)] static extern int mv_info(string path, out IntPtr json, [Out] byte[] error, int capacity);
    [DllImport(Dll, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)] static extern int mv_read(string path, int part, [In] IntPtr[] channels, int count, out IntPtr data, out int width, out int height, [Out] byte[] error, int capacity);
    [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)] static extern void mv_free(IntPtr memory);
    static IOException Failure(byte[] error) => new("EXR: " + Encoding.UTF8.GetString(error.TakeWhile(b => b != 0).ToArray()));
    internal static ExrPart[] Inspect(string path)
    {
        var error = new byte[4096];
        if (mv_info(path, out var json, error, error.Length) == 0) throw Failure(error);
        try { return JsonSerializer.Deserialize<ExrPart[]>(Marshal.PtrToStringUTF8(json)!) ?? []; }
        finally { mv_free(json); }
    }
    internal static List<ExrView> Views(ExrPart[] parts)
    {
        var views = new List<ExrView>();
        foreach (var p in parts.Where(p => !p.Type.StartsWith("deep", StringComparison.Ordinal)))
        {
            foreach (var layer in p.Channels.GroupBy(c => c.Name.Contains('.') ? c.Name[..c.Name.LastIndexOf('.')] : ""))
            {
                string? Find(string component) => layer.FirstOrDefault(c => c.Name[(c.Name.LastIndexOf('.') + 1)..].Equals(component, StringComparison.OrdinalIgnoreCase))?.Name;
                var r = Find("R"); var g = Find("G"); var b = Find("B");
                string label = (parts.Length > 1 ? p.Name + " / " : "") + (layer.Key.Length > 0 ? layer.Key : "RGBA");
                if (r != null && g != null && b != null) views.Add(new(p, label, r, g, b, Find("A")));
            }
            foreach (var c in p.Channels) views.Add(new(p, (parts.Length > 1 ? p.Name + " / " : "") + c.Name + " · channel", c.Name, c.Name, c.Name));
        }
        return views;
    }
    internal static BitmapSource Load(string path, ExrView? view = null, double exposure = 0, bool srgb = true)
    {
        view ??= Views(Inspect(path)).FirstOrDefault() ?? throw new IOException(L10n.Choose("평면 이미지가 없는 Deep EXR은 표시할 수 없습니다.", "This deep EXR has no flat image to preview."));
        string[] names = new[] { view.Red, view.Green, view.Blue, view.Alpha }.OfType<string>().Distinct(StringComparer.Ordinal).ToArray();
        var error = new byte[4096];
        IntPtr ptr; int w, h; var encodedNames = names.Select(Marshal.StringToCoTaskMemUTF8).ToArray();
        try { if (mv_read(path, view.Part.Index, encodedNames, names.Length, out ptr, out w, out h, error, error.Length) == 0) throw Failure(error); }
        finally { foreach (var name in encodedNames) Marshal.FreeCoTaskMem(name); }
        try
        {
            int pixels = checked(w * h); var data = new float[checked(pixels * names.Length)]; Marshal.Copy(ptr, data, 0, data.Length);
            var bytes = new byte[checked(pixels * 4)]; int r = Array.IndexOf(names, view.Red) * pixels, g = Array.IndexOf(names, view.Green) * pixels, b = Array.IndexOf(names, view.Blue) * pixels, a = view.Alpha == null ? -1 : Array.IndexOf(names, view.Alpha) * pixels; double gain = Math.Pow(2, Math.Clamp(exposure, -20, 20));
            Parallel.For(0, h, y => { for (int i = y * w; i < (y + 1) * w; i++) { bytes[i * 4] = Display(data[b + i], gain, srgb); bytes[i * 4 + 1] = Display(data[g + i], gain, srgb); bytes[i * 4 + 2] = Display(data[r + i], gain, srgb); bytes[i * 4 + 3] = a < 0 ? (byte)255 : Display(data[a + i], 1, false); } });
            var bitmap = BitmapSource.Create(w, h, 96, 96, PixelFormats.Bgra32, null, bytes, w * 4); bitmap.Freeze(); return bitmap;
        }
        finally { mv_free(ptr); }
    }
    internal static byte Display(double value, double gain, bool srgb)
    {
        if (double.IsNaN(value) || value <= 0) return 0;
        value = Math.Min(1, value * gain); if (srgb) value = value <= .0031308 ? value * 12.92 : 1.055 * Math.Pow(value, 1 / 2.4) - .055;
        return (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);
    }
    internal static string Metadata(string path) => string.Join("\n", Inspect(path).Select(p => $"Part {p.Index}: {p.Name} · {p.Type}\n{p.Width} × {p.Height} · origin ({p.X}, {p.Y})\n" + string.Join("\n", p.Channels.Select(c => $"  {c.Name} · {new[] { "UINT", "HALF", "FLOAT" }[Math.Clamp(c.Type, 0, 2)]} · {c.XSampling}×{c.YSampling}"))));
}

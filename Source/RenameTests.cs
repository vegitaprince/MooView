using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MooView;

internal static class RenameTests
{
    internal static void Run(Window owner, string dir, List<string> log)
    {
        new System.Windows.Interop.WindowInteropHelper(owner).EnsureHandle();
        void Check(bool value, string label) { if (!value) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); }
        void Reject(Action action, string label) { bool rejected = false; try { action(); } catch (Exception ex) when (ex is ArgumentException or IOException) { rejected = true; } Check(rejected, label); }
        string folder = Path.Combine(dir, "rename-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string[] sources = [Path.Combine(folder, "Original.png"), Path.Combine(folder, "Second.JPG"), Path.Combine(folder, "Third.exr")];
        for (int i = 0; i < sources.Length; i++) File.WriteAllText(sources[i], "image-payload-" + i);
        string[] Names(string[] paths) => paths.Select(Path.GetFileName).ToArray()!;
        Check(Names(Imaging.RenameTargets(sources, "Image_{n}", 12, 5)).SequenceEqual(new[] { "Image_00012.png", "Image_00013.JPG", "Image_00014.exr" }), "rename custom start and width preserve each extension");
        Check(Names(Imaging.RenameTargets(sources, "{n}", 0, 1)).SequenceEqual(new[] { "0.png", "1.JPG", "2.exr" }), "rename accepts zero start and unpadded numbering");
        Check(Names(Imaging.RenameTargets(sources, "{n}", 98, 2)).SequenceEqual(new[] { "98.png", "99.JPG", "100.exr" }), "rename sequence grows past digit width without truncation");
        Check(Path.GetFileName(Imaging.RenameTargets([sources[0]], "{name}_{n}", 7, 12)[0]) == "Original_000000000007.png", "rename supports original names with twelve digit padding");
        Check(Path.GetFileName(Imaging.RenameTargets([sources[0]], "{n}", int.MaxValue, 3)[0]) == "2147483647.png", "rename accepts the largest single integer without overflow");
        Reject(() => Imaging.RenameTargets(sources, "{n}", int.MaxValue, 3), "rename rejects sequence overflow before changing files");
        Reject(() => Imaging.RenameTargets(sources, "{n}", -1, 3), "rename rejects negative start numbers");
        foreach (int digits in new[] { 0, 13 }) Reject(() => Imaging.RenameTargets(sources, "{n}", 1, digits), "rename rejects invalid digit width " + digits);
        Reject(() => Imaging.RenameTargets(sources, "../{n}", 1, 3), "rename rejects folder traversal in filename patterns");
        var sameExtension = new[] { sources[0], Path.Combine(folder, "other.png") }; File.WriteAllText(sameExtension[1], "other");
        Reject(() => Imaging.RenameTargets(sameExtension, "duplicate", 1, 3), "rename rejects duplicate names before changing files");
        string existing = Path.Combine(folder, "Collision_00012.png"); File.WriteAllText(existing, "keep-existing");
        Reject(() => Imaging.RenameTargets(sources, "Collision_{n}", 12, 5), "rename refuses to overwrite an existing target");
        Check(File.ReadAllText(existing) == "keep-existing" && sources.All(File.Exists), "failed rename validation preserves existing files and sources");

        bool english = L10n.Instance.English;
        try
        {
            L10n.Instance.English = false;
            var dialog = new RenameWindow(owner, sources);
            Check(dialog.StartInput.Text == "1" && (int)dialog.DigitsInput.SelectedItem == 3 && dialog.CanContinue, "rename dialog retains the previous 001 defaults");
            dialog.StartInput.Text = "12"; dialog.DigitsInput.SelectedItem = 5;
            Check(dialog.PreviewText.Contains("Image_00012.png") && dialog.PreviewText.Contains("Image_00014.exr"), "rename preview updates immediately for start and digit selection");
            dialog.PatternInput.Text = "{name}_{n}";
            Check(dialog.PreviewText.Contains("Original_00012.png"), "rename preview combines the original name with custom numbering");
            foreach (string invalid in new[] { "", "-1", "1.5", "abc", "2147483648" })
            {
                dialog.StartInput.Text = invalid;
                Check(!dialog.CanContinue && dialog.ValidationText.Length > 0 && !dialog.TryAccept() && dialog.Options == null, "rename dialog blocks invalid start entry: " + (invalid.Length == 0 ? "empty" : invalid));
            }
            dialog.StartInput.Text = "2147483647";
            Check(!dialog.CanContinue && dialog.ValidationText.Contains("마지막"), "rename dialog validates the final number across the full selection");
            dialog.StartInput.Text = "12"; dialog.PatternInput.Text = "Collision_{n}";
            Check(!dialog.TryAccept() && dialog.Options == null && File.ReadAllText(existing) == "keep-existing", "rename dialog rechecks target collisions before accepting");
            dialog.PatternInput.Text = "Final_{n}";
            Check(dialog.TryAccept() && dialog.Options == new RenameOptions("Final_{n}", 12, 5), "rename dialog passes the selected options to the batch operation");
            dialog.Measure(new Size(560, 700)); dialog.Arrange(new Rect(0, 0, 560, dialog.DesiredSize.Height)); dialog.UpdateLayout();
            var root = (FrameworkElement)dialog.Content; root.Measure(new Size(520, 650)); root.Arrange(new Rect(0, 0, 520, root.DesiredSize.Height)); root.UpdateLayout();
            Check(dialog.StartInput.ActualWidth > 150 && dialog.DigitsInput.ActualWidth > 130, "rename numeric controls retain readable widths");
            dialog.Content = null;
            var host = new System.Windows.Controls.Border { Width = 560, Background = (Brush)Application.Current.Resources["Bg"], Child = root };
            host.Measure(new Size(560, 700)); host.Arrange(new Rect(0, 0, 560, host.DesiredSize.Height)); host.UpdateLayout();
            var preview = new RenderTargetBitmap(560, (int)Math.Ceiling(host.ActualHeight), 96, 96, PixelFormats.Pbgra32); preview.Render(host); Imaging.Save(preview, Path.Combine(AppContext.BaseDirectory, "preview-rename.png")); host.Child = null; dialog.Close();
            L10n.Instance.English = true;
            var translated = new RenameWindow(owner, sources, new("Photo_{n}", 20, 4));
            Check(translated.StartInput.Text == "20" && (int)translated.DigitsInput.SelectedItem == 4 && translated.PreviewText.Contains("Photo_0020.png") && System.Windows.Automation.AutomationProperties.GetName(translated.StartInput) == "Start number", "rename dialog restores session choices and uses English labels"); translated.Close();
        }
        finally { L10n.Instance.English = english; }
        var target = Imaging.RenameTargets(sources, "Final_{n}", 12, 5); Imaging.Rename(sources, target);
        Check(!sources.Any(File.Exists) && target.Select((p, i) => File.ReadAllText(p) == "image-payload-" + i).All(value => value), "batch rename applies previewed numbers and preserves image contents");
        Check(File.ReadAllText(existing) == "keep-existing", "batch rename leaves unrelated files intact");
    }
}

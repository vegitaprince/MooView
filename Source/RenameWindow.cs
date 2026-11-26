using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MooView;

internal sealed record RenameOptions(string Pattern, int Start, int Digits)
{
    internal static RenameOptions Default => new("Image_{n}", 1, 3);
}

internal sealed class RenameWindow : Window
{
    readonly string[] sources;
    readonly TextBlock preview = new() { TextWrapping = TextWrapping.Wrap, LineHeight = 24 };
    readonly TextBlock error = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 6, 3, 0) };
    readonly Button next = new() { Content = L10n.T("Continue"), IsDefault = true, MinWidth = 100 };
    internal TextBox PatternInput { get; } = new();
    internal TextBox StartInput { get; } = new();
    internal ComboBox DigitsInput { get; } = new() { ItemsSource = Enumerable.Range(1, 12), MinWidth = 130 };
    internal RenameOptions? Options { get; private set; }
    internal string PreviewText => preview.Text;
    internal string ValidationText => error.Text;
    internal bool CanContinue => next.IsEnabled;

    internal RenameWindow(Window owner, string[] paths, RenameOptions? previous = null)
    {
        sources = paths.ToArray(); var options = previous ?? RenameOptions.Default;
        Owner = owner; Title = L10n.T("Rename"); Width = 560; SizeToContent = SizeToContent.Height; MaxHeight = 700;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; ResizeMode = ResizeMode.NoResize;
        var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
        AddLabel(panel, L10n.T("RenamePattern"));
        PatternInput.Text = options.Pattern; PatternInput.ToolTip = L10n.T("RenamePatternHint"); panel.Children.Add(PatternInput);
        panel.Children.Add(new TextBlock { Text = L10n.T("RenamePatternHint"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(4, 4, 4, 14) });
        var settings = new Grid(); settings.ColumnDefinitions.Add(new ColumnDefinition()); settings.ColumnDefinitions.Add(new ColumnDefinition());
        var start = new StackPanel { Margin = new Thickness(0, 0, 8, 0) }; AddLabel(start, L10n.T("RenameStart"));
        StartInput.Text = options.Start.ToString(CultureInfo.InvariantCulture); StartInput.ToolTip = L10n.T("RenameStartHint"); start.Children.Add(StartInput);
        var digits = new StackPanel { Margin = new Thickness(8, 0, 0, 0) }; AddLabel(digits, L10n.T("RenameDigits"));
        DigitsInput.SelectedItem = options.Digits; DigitsInput.ToolTip = L10n.T("RenameDigitsHint"); digits.Children.Add(DigitsInput);
        Grid.SetColumn(digits, 1); settings.Children.Add(start); settings.Children.Add(digits); panel.Children.Add(settings);
        AddLabel(panel, L10n.T("RenamePreview"), new Thickness(4, 18, 4, 6));
        var sample = new Border { CornerRadius = new CornerRadius(8), Padding = new Thickness(12), Margin = new Thickness(3), Child = preview };
        sample.SetResourceReference(Border.BackgroundProperty, "Field"); panel.Children.Add(sample); panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(new Button { Content = L10n.T("Cancel"), IsCancel = true }); buttons.Children.Add(next); panel.Children.Add(buttons);
        System.Windows.Automation.AutomationProperties.SetName(PatternInput, L10n.T("RenamePattern"));
        System.Windows.Automation.AutomationProperties.SetName(StartInput, L10n.T("RenameStart"));
        System.Windows.Automation.AutomationProperties.SetName(DigitsInput, L10n.T("RenameDigits"));
        PatternInput.TextChanged += (_, _) => UpdatePreview(); StartInput.TextChanged += (_, _) => UpdatePreview(); DigitsInput.SelectionChanged += (_, _) => UpdatePreview();
        next.Click += (_, _) => { if (TryAccept()) DialogResult = true; };
        Loaded += (_, _) => { PatternInput.Focus(); PatternInput.SelectAll(); };
        UpdatePreview();
    }

    static void AddLabel(Panel panel, string text, Thickness? margin = null) => panel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, Margin = margin ?? new Thickness(4, 4, 4, 2) });
    RenameOptions ReadOptions()
    {
        if (!int.TryParse(StartInput.Text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int start) || start < 0)
            throw new ArgumentException(L10n.T("RenameInvalidStart"));
        if (DigitsInput.SelectedItem is not int digits) throw new ArgumentException(L10n.T("RenameInvalidDigits"));
        return new(PatternInput.Text, start, digits);
    }
    void UpdatePreview()
    {
        Options = null;
        try
        {
            var options = ReadOptions();
            Imaging.ValidateRenameNumbering(sources.Length, options.Start, options.Digits);
            var names = Imaging.RenameTargets(sources.Take(3).ToArray(), options.Pattern, options.Start, options.Digits, checkExisting: false);
            preview.Text = string.Join("\n", sources.Take(3).Zip(names, (from, to) => Path.GetFileName(from) + "  →  " + Path.GetFileName(to))) + (sources.Length > 3 ? "\n…" : "");
            error.Text = ""; next.IsEnabled = sources.Length > 0;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or OverflowException)
        {
            preview.Text = "—"; error.Text = ex.Message; next.IsEnabled = false;
        }
    }
    internal bool TryAccept()
    {
        Options = null;
        try
        {
            var options = ReadOptions(); Imaging.RenameTargets(sources, options.Pattern, options.Start, options.Digits);
            Options = options; error.Text = ""; return true;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or OverflowException)
        { error.Text = ex.Message; return false; }
    }
}

public partial class MainWindow
{
    RenameOptions lastRenameOptions = RenameOptions.Default;
}

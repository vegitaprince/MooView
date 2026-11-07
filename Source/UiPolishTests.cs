using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class UiPolishTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var next in Descendants(child))yield return next;}}
    internal static void Run(MainWindow w,List<string> log)
    {
        void Check(bool value,string label){if(!value)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string name,params object[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,args);
        var choice=typeof(MainWindow).GetField("themeMode",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var darkField=typeof(MainWindow).GetField("dark",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Check(ThemePalette.Resolve(AppThemeChoice.System,true)&&!ThemePalette.Resolve(AppThemeChoice.System,false),"default theme follows both Windows app modes");
        Check(ThemePalette.Resolve(AppThemeChoice.Dark,false)&&!ThemePalette.Resolve(AppThemeChoice.Light,true),"explicit theme overrides the opposite Windows mode");
        Check(ThemePalette.Parse(null)==AppThemeChoice.System&&ThemePalette.Parse("invalid")==AppThemeChoice.System,"legacy and invalid theme settings migrate to Windows default");
        var preferences=typeof(MainWindow).GetNestedType("Preferences",BindingFlags.NonPublic)!;
        foreach(string value in new[]{"System","Light","Dark"})
        {
            var parsed=JsonSerializer.Deserialize("{\"Dark\":true,\"Checker\":true,\"Background\":\"#222222\",\"Nav\":false,\"Theme\":\""+value+"\"}",preferences)!;
            string json=JsonSerializer.Serialize(parsed,preferences);var restored=JsonSerializer.Deserialize(json,preferences)!;
            Check((string?)preferences.GetProperty("Theme")!.GetValue(restored)==value,"theme choice round trips in viewer settings: "+value);
        }
        choice.SetValue(w,AppThemeChoice.System);darkField.SetValue(w,false);Call("ApplySystemTheme",true);Check((bool)darkField.GetValue(w)!,"Windows notification applies dark mode while following the system");
        Call("ToggleTheme",w,new RoutedEventArgs());Check((AppThemeChoice)choice.GetValue(w)! == AppThemeChoice.Light,"theme button selects a manual override");
        Call("ApplySystemTheme",true);Check(!(bool)darkField.GetValue(w)!,"Windows changes leave manual light mode intact");
        Call("UseSystemTheme",w,new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Right){RoutedEvent=UIElement.PreviewMouseRightButtonUpEvent});Check((AppThemeChoice)choice.GetValue(w)! == AppThemeChoice.System,"right-click restores Windows theme following");
        foreach(bool dark in new[]{false,true})
        {
            choice.SetValue(w,dark?AppThemeChoice.Dark:AppThemeChoice.Light);typeof(MainWindow).GetField("dark",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,dark);Call("ApplyTheme");
            foreach(string key in ThemePalette.Keys){var color=((SolidColorBrush)Application.Current.Resources[key]).Color;Check(color.R==color.G&&color.G==color.B,"neutral "+(dark?"dark ":"light ")+key);}
            foreach(bool english in new[]{false,true})foreach(double width in new[]{960.0,1400.0})
            {
                L10n.Instance.English=english;
                foreach(int item in Enumerable.Range(0,5))
                {
                    w.FitMode.SelectedIndex=item;w.Root.Measure(new Size(width,640));w.Root.Arrange(new Rect(0,0,width,640));w.Root.UpdateLayout();
                    foreach(var combo in new[]{w.PageCount,w.LayoutMode,w.FitMode,w.FpsInput})
                    {
                        combo.ApplyTemplate();var display=(ContentPresenter)combo.Template.FindName("Display",combo);var arrow=(Glyph)combo.Template.FindName("Arrow",combo);
                        if(combo.IsEditable)
                        {
                            var input=(TextBox)combo.Template.FindName("PART_EditableTextBox",combo);var p=input.TranslatePoint(new Point(input.ActualWidth,0),combo);var a=arrow.TranslatePoint(new Point(),combo);
                            if(p.X>a.X-3)throw new Exception("Editable combo overlaps arrow");
                        }
                        else
                        {
                            var p=display.TranslatePoint(new Point(display.ActualWidth,0),combo);var a=arrow.TranslatePoint(new Point(),combo);
                            if(p.X>a.X-5||display.ActualWidth+1<display.DesiredSize.Width-display.Margin.Left-display.Margin.Right)throw new Exception("Combo text or icon clips/overlaps arrow: "+combo.Name);
                        }
                        if(Math.Abs(arrow.ActualWidth-12)>1||Math.Abs(arrow.ActualHeight-12)>1)throw new Exception($"Dropdown arrow squeezed: {combo.Name}, {arrow.ActualWidth} x {arrow.ActualHeight}, {width}");
                    }
                }
                Check(true,$"dropdown content stays separate from arrows: {(dark?"dark":"light")} {(english?"EN":"KO")} {width}");
                foreach(var button in Descendants(w.Root).OfType<ButtonBase>().Where(b=>b.Visibility==Visibility.Visible&&b.ActualWidth>0&&b.ActualHeight>0))foreach(var glyph in Descendants(button).OfType<Glyph>())
                {
                    var origin=glyph.TranslatePoint(new Point(),button);
                    if(origin.X<0||origin.Y<0||origin.X+glyph.ActualWidth>button.ActualWidth+.1||origin.Y+glyph.ActualHeight>button.ActualHeight+.1)throw new Exception($"Icon exceeds button bounds: {glyph.Kind} in {button.Name}: {origin} / {glyph.ActualWidth}x{glyph.ActualHeight} / {button.ActualWidth}x{button.ActualHeight}");
                }
                Check(true,$"rail toolbar and window button icons fit at {(english?"EN":"KO")} {width} {(dark?"dark":"light")}");
                if(width==1400&&!english)
                {
                    w.FitMode.SelectedIndex=0;w.Root.Measure(new Size(width,640));w.Root.Arrange(new Rect(0,0,width,640));w.Root.UpdateLayout();
                    foreach(double scale in new[]{1.0,1.25,1.5,2.0})
                    {
                        var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(w.Background,null,new Rect(0,0,width,640));
                        var bitmap=new RenderTargetBitmap((int)(width*scale),(int)(640*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(background);bitmap.Render(w.Root);Imaging.Save(bitmap,Path.Combine(AppContext.BaseDirectory,$"preview-polish-{(dark?"dark":"light")}-{scale:0.##}.png"));
                    }
                }
            }
            ThemeTests.Verify(w,log,dark?"neutral dark":"neutral light");
        }
        choice.SetValue(w,AppThemeChoice.Light);typeof(MainWindow).GetField("dark",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(w,false);Call("ApplyTheme");L10n.Instance.English=false;w.FitMode.SelectedIndex=0;
    }
}

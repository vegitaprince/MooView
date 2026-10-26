using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace MooView;
public static class ThemeTests
{
    static IEnumerable<DependencyObject> Children(DependencyObject p){for(int i=0;i<VisualTreeHelper.GetChildrenCount(p);i++){var c=VisualTreeHelper.GetChild(p,i);yield return c;foreach(var x in Children(c))yield return x;}}
    public static void Verify(MainWindow w,List<string> log,string name){var expected=((SolidColorBrush)Application.Current.Resources["Text"]).Color;
        foreach(var root in new DependencyObject[]{w.PlayButton,w.PageCount,w.FpsInput,w.ZoomInput}){foreach(var t in Children(root).OfType<TextBlock>()){if(t.Foreground is SolidColorBrush brush&&brush.Color!=expected)throw new Exception($"{name}: text foreground mismatch: {t.Text}");}}
        double Lum(Color c){double Convert(byte b){double v=b/255.0;return v<=.04045?v/12.92:Math.Pow((v+.055)/1.055,2.4);}return .2126*Convert(c.R)+.7152*Convert(c.G)+.0722*Convert(c.B);}
        foreach(string bg in new[]{"Panel","Field","Accent"}){double a=Lum(expected),b=Lum(((SolidColorBrush)Application.Current.Resources[bg]).Color);if((Math.Max(a,b)+.05)/(Math.Min(a,b)+.05)<4.5)throw new Exception(name+" insufficient contrast: "+bg);}
        log.Add("PASS: "+name+" rendered control text and 4.5:1 contrast");
    }
}


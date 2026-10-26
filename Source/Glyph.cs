using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MooView;
public sealed class Glyph:Control
{
    public static readonly DependencyProperty KindProperty=DependencyProperty.Register(nameof(Kind),typeof(string),typeof(Glyph),new FrameworkPropertyMetadata("image",FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind{get=>(string)GetValue(KindProperty);set=>SetValue(KindProperty,value);}
    static Glyph(){ForegroundProperty.OverrideMetadata(typeof(Glyph),new FrameworkPropertyMetadata(Brushes.Black,FrameworkPropertyMetadataOptions.Inherits|FrameworkPropertyMetadataOptions.AffectsRender));} public Glyph(){Width=22;Height=22;IsHitTestVisible=false;SetResourceReference(ForegroundProperty,"Text");}
    internal static readonly Dictionary<string,string> Paths=new(){
        ["language"]="M2,4 L14,4 M8,2 L8,4 M11,4 Q10,13 3,16 M4,8 Q7,13 12,15 M13,22 L18,10 23,22 M15,18 L21,18", ["undo"]="M8,4 L2,10 8,16 M2,10 L14,10 Q22,10 22,18", ["redo"]="M16,4 L22,10 16,16 M22,10 L10,10 Q2,10 2,18", ["select"]="M4,2 L20,13 12,14 9,22 Z", ["eraser"]="M3,14 L14,3 22,11 11,22 7,22 Z M8,9 L16,17 M11,22 L23,22", ["text"]="M3,5 L21,5 M12,5 L12,21 M3,5 L3,9 M21,5 L21,9 M8,21 L16,21", ["edittext"]="M2,3 L16,3 M8,3 L8,17 M4,17 L10,17 M14,19 L20,13 23,16 17,22 13,23 Z", ["trash"]="M3,6 L21,6 M9,6 L9,2 15,2 15,6 M5,6 L6,22 18,22 19,6 M10,10 L10,18 M14,10 L14,18", ["crop"]="M6,2 L6,18 22,18 M2,6 L18,6 18,22", ["check"]="M3,12 L9,19 22,4", ["rotate"]="M4,7 A9,9 0 1 1 3,16 M4,2 L4,8 10,8", ["fliph"]="M12,2 L12,22 M9,5 L2,19 9,19 Z M15,5 L22,19 15,19 Z", ["flipv"]="M2,12 L22,12 M5,9 L19,2 19,9 Z M5,15 L19,22 19,15 Z", ["curve"]="M3,2 L3,21 22,21 M3,19 C12,20 10,4 22,3", ["save"]="M3,2 L18,2 22,6 22,22 2,22 2,3 Z M7,2 L7,9 17,9 17,2 M6,22 L6,14 18,14 18,22", ["resize"]="M2,9 L2,2 9,2 M15,22 L22,22 22,15 M2,2 L9,9 M22,22 L15,15", ["pensize"]="M3,18 L21,18 M6,12 L18,12 M9,6 L15,6", ["copy"]="M8,8 L22,8 22,22 8,22 Z M16,8 L16,2 2,2 2,16 8,16", ["loop"]="M3,9 Q3,3 9,3 L18,3 22,7 M18,1 L22,7 16,7 M21,15 Q21,21 15,21 L6,21 2,17 M6,23 L2,17 8,17", ["horizontal"]="M2,4 L9,4 9,20 2,20 Z M15,4 L22,4 22,20 15,20 Z", ["vertical"]="M4,2 L20,2 20,9 4,9 Z M4,15 L20,15 20,22 4,22 Z", ["grid"]="M2,2 L10,2 10,10 2,10 Z M14,2 L22,2 22,10 14,10 Z M2,14 L10,14 10,22 2,22 Z M14,14 L22,14 22,22 14,22 Z", ["selectall"]="M3,3 L21,3 21,21 3,21 Z M6,12 L10,16 18,7", ["deselect"]="M3,3 L21,3 21,21 3,21 Z M7,7 L17,17 M17,7 L7,17", ["image"]="M4,3 L20,3 Q22,3 22,5 L22,19 Q22,21 20,21 L4,21 Q2,21 2,19 L2,5 Q2,3 4,3 M3,17 L8,11 12,15 16,10 22,17 M9,7 A1.5,1.5 0 1 1 6,7 A1.5,1.5 0 1 1 9,7",
        ["folder"]="M2,7 L2,5 Q2,3 4,3 L9,3 12,6 20,6 Q22,6 22,8 L22,19 Q22,21 20,21 L4,21 Q2,21 2,19 Z M2,9 L22,9",
        ["sidebar"]="M4,3 L20,3 Q22,3 22,5 L22,19 Q22,21 20,21 L4,21 Q2,21 2,19 L2,5 Q2,3 4,3 M9,3 L9,21 M4.5,7 L6.5,7 M4.5,11 L6.5,11 M4.5,15 L6.5,15",
        ["edit"]="M14,5 L18,1 23,6 8,21 2,22 3,16 Z M12,7 L17,12 M3,16 L8,21",
        ["info"]="M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,10 L12,17 M12,6 L12,7",
        ["theme"]="M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M12,2 L12,22 M15,5 L15,19 M18,7 L18,17",
        ["help"]="M22,12 A10,10 0 1 1 2,12 A10,10 0 1 1 22,12 M8.5,8 Q9,4 13,5 Q18,6 14,10 Q12,11 12,14 M12,17 L12,18",
        ["fullscreen"]="M2,9 L2,2 9,2 M15,2 L22,2 22,9 M22,15 L22,22 15,22 M9,22 L2,22 2,15",
        ["previous"]="M15,4 L7,12 15,20",["next"]="M9,4 L17,12 9,20",
        ["play"]="M6,3 L21,12 6,21 Z",["pause"]="M8,3 L8,21 M16,3 L16,21",
        ["alpha"]="M3,3 L21,3 21,21 3,21 Z M9,3 L9,21 M15,3 L15,21 M3,9 L21,9 M3,15 L21,15",
        ["color"]="M12,2 C9,7 4,11 4,15 A8,7 0 0 0 20,15 C20,11 15,7 12,2 Z",
        ["convert"]="M3,8 L20,8 16,4 M20,16 L3,16 7,20 M20,8 L16,12 M3,16 L7,12",
        ["rename"]="M3,5 L16,5 M9,5 L9,20 M5,20 L13,20 M18,10 L18,21 M16,10 L21,10 M16,21 L21,21",
        ["settings"]="M12,2 L14,6 18,5 19,9 23,12 19,15 18,19 14,18 12,22 10,18 6,19 5,15 1,12 5,9 6,5 10,6 Z M16,12 A4,4 0 1 1 8,12 A4,4 0 1 1 16,12",
        ["plus"]="M12,4 L12,20 M4,12 L20,12",["minus"]="M4,12 L20,12",["close"]="M5,5 L19,19 M19,5 L5,19"
    };
    protected override void OnRender(DrawingContext dc){base.OnRender(dc);double scale=Math.Min(ActualWidth,ActualHeight)/24;dc.PushTransform(new ScaleTransform(scale,scale));var pen=new Pen(Foreground,1.65){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};dc.DrawGeometry(null,pen,Geometry.Parse(Paths.GetValueOrDefault(Kind,Paths["image"])));dc.Pop();}
    public static StackPanel Label(string kind,string text){var row=new StackPanel{Orientation=Orientation.Horizontal};row.Children.Add(new Glyph{Kind=kind,Width=18,Height=18});return row;}
}


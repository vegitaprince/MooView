using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;
public static class EditorTests
{
 public static void Run(string dir,List<string> log){void Check(bool v,string text){if(!v)throw new Exception(text);log.Add("PASS: "+text);}var b=new WriteableBitmap(640,400,96,96,PixelFormats.Bgra32,null);var white=Enumerable.Repeat((byte)255,640*400*4).ToArray();b.WritePixels(new Int32Rect(0,0,640,400),white,640*4,0);b.Freeze();var e=new EditorWindow(b,Path.Combine(dir,"edit-source.png"));object? Call(string n,params object[] args)=>typeof(EditorWindow).GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(e,args);T Field<T>(string n)=>(T)typeof(EditorWindow).GetField(n,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(e)!;
 byte[] Pixels(BitmapSource image){var bytes=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(bytes,image.PixelWidth*4,0);return bytes;}
 e.AddText("MooView",new Point(70,90));Check(Field<List<TextLayer>>("texts").Count==1,"text stays editable as a layer");var first=Pixels(e.Flatten());Check(!first.SequenceEqual(white),"text appears in exported pixels");Field<TextBox>("textSize").Text="90";Call("ApplyTextSize");Field<TextBox>("angleInput").Text="35";Call("ApplyTextAngle");var changed=Field<List<TextLayer>>("texts")[0];Check(changed.Size==90&&changed.Angle==35,"selected text resizes and rotates");Check(!Pixels(e.Flatten()).SequenceEqual(first),"text transforms change exported pixels");Call("Remember",new object?[]{null}!);Field<List<TextLayer>>("texts")[0]=changed with{X=240,Y=120};Check(!Pixels(e.Flatten()).SequenceEqual(first),"text position remains adjustable");e.DeleteSelected();Check(Pixels(e.Flatten()).SequenceEqual(white),"deleting text restores original pixels");Call("Undo");Check(Field<List<TextLayer>>("texts").Count==1,"undo restores deleted editable text");Call("Redo");Check(Field<List<TextLayer>>("texts").Count==0,"redo deletes text layer again");
 var ink=Field<InkCanvas>("ink");var stroke=new Stroke(new StylusPointCollection(new[]{new StylusPoint(50,200),new StylusPoint(500,200)}),new DrawingAttributes{Color=Colors.Black,Width=14,Height=14});ink.Strokes.Add(stroke);Call("SetMode","erase");Check(ink.EditingMode==InkCanvasEditingMode.EraseByPoint,"eraser uses point erasure, not flattened pixels");var drawn=Pixels(e.Flatten());ink.Strokes.Erase(new[]{new Point(270,185),new Point(270,215)},new EllipseStylusShape(50,50));var erased=Pixels(e.Flatten());Check(drawn[(200*640+270)*4]==0&&erased[(200*640+270)*4]==255&&erased[(200*640+100)*4]==0,"eraser removes only intersected stroke area");Check(Field<BitmapSource>("source")==b,"drawing eraser leaves source image intact");
 e.AddText("MooView",new Point(130,60));Call("SetMode","select");e.Measure(new Size(1200,800));e.Arrange(new Rect(0,0,1200,800));e.UpdateLayout();var root=(FrameworkElement)e.Content;root.Measure(new Size(1160,740));root.Arrange(new Rect(0,0,1160,740));root.UpdateLayout();var preview=new RenderTargetBitmap(1160,740,96,96,PixelFormats.Pbgra32);preview.Render(root);Imaging.Save(preview,Path.Combine(AppContext.BaseDirectory,"preview-editor.png"));e.TestClose();}
}

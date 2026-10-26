using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;
public static class BrandIcon
{
    public static void Create(string path){var sizes=new[]{16,24,32,48,64,128,256};var images=new List<byte[]>();foreach(int size in sizes){var v=new DrawingVisual();using(var c=v.RenderOpen()){c.PushTransform(new ScaleTransform(size/256.0,size/256.0));c.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(45,66,54),Color.FromRgb(22,34,28),90),null,new Rect(4,4,248,248),55,55);var mint=new SolidColorBrush(Color.FromRgb(185,226,200));c.DrawRoundedRectangle(null,new Pen(mint,10){LineJoin=PenLineJoin.Round},new Rect(41,49,174,158),23,23);c.DrawEllipse(mint,null,new Point(171,91),17,17);c.DrawGeometry(null,new Pen(mint,14){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},Geometry.Parse("M47,179 L91,115 128,166 153,137 208,190"));c.Pop();}var b=new RenderTargetBitmap(size,size,96,96,PixelFormats.Pbgra32);b.Render(v);var e=new PngBitmapEncoder();e.Frames.Add(BitmapFrame.Create(b));using var ms=new MemoryStream();e.Save(ms);images.Add(ms.ToArray());if(size==256)Imaging.Save(b,Path.ChangeExtension(path,".png"));}using var f=File.Create(path);using var w=new BinaryWriter(f);w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+16*sizes.Length;for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(images[i].Length);w.Write(offset);offset+=images[i].Length;}foreach(var b in images)w.Write(b);}
}

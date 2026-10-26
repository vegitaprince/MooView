using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;
public static class GifTests
{
    // Hand-built partial-frame GIF fixture keeps the expected pixels independent of the decoder.
    public static string Fixture(string dir,int? loops=0){string path=Path.Combine(dir,"Animation.gif");using var f=File.Create(path);using var w=new BinaryWriter(f);w.Write(Encoding.ASCII.GetBytes("GIF89a"));w.Write((ushort)3);w.Write((ushort)2);w.Write((byte)0x81);w.Write((byte)0);w.Write((byte)0);w.Write(new byte[]{0,0,0,255,0,0,0,255,0,0,0,255});if(loops is int count){w.Write(new byte[]{0x21,0xff,11});w.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));w.Write(new byte[]{3,1,(byte)count,(byte)(count>>8),0});}
        void Frame(int x,int y,int width,int height,byte[] pixels,int disposal,int delay){w.Write(new byte[]{0x21,0xf9,4,(byte)((disposal<<2)|1),(byte)delay,0,0,0});w.Write((byte)0x2c);w.Write((ushort)x);w.Write((ushort)y);w.Write((ushort)width);w.Write((ushort)height);w.Write((byte)0);w.Write((byte)2);var codes=new List<int>();foreach(var pixel in pixels){codes.Add(4);codes.Add(pixel);}codes.Add(5);var data=new byte[(codes.Count*3+7)/8];for(int n=0;n<codes.Count;n++)for(int bit=0;bit<3;bit++)if((codes[n]&(1<<bit))!=0)data[(n*3+bit)/8]|=(byte)(1<<((n*3+bit)%8));w.Write((byte)data.Length);w.Write(data);w.Write((byte)0);}
        Frame(0,0,3,2,new byte[]{1,1,1,1,1,1},1,5);Frame(1,0,1,1,new byte[]{2},3,9);Frame(2,1,1,1,new byte[]{3},2,7);Frame(0,0,1,1,new byte[]{0},1,3);w.Write((byte)0x3b);return path;
    }
    public static async Task Run(string dir,List<string> lines){void Check(bool ok,string text){if(!ok)throw new Exception(text);lines.Add("PASS: "+text);}var path=Fixture(dir);var data=GifData.Load(path);Check(data.Width==3&&data.Height==2&&data.Frames.Count==4,"GIF logical size and partial frames");Check(data.Frames[0].DelayMs==50&&data.Frames[1].DelayMs==90,"GIF frame-specific delays");Check(data.TotalPlays==0,"GIF infinite loop metadata");using(var p=new GifPlayback(data)){Color Pixel(int x,int y){var b=new byte[24];p.Bitmap.CopyPixels(b,12,0);int i=(y*3+x)*4;return Color.FromArgb(b[i+3],b[i+2],b[i+1],b[i]);}Check(Pixel(0,0)==Colors.Red,"GIF first frame composition");p.Advance();Check(Pixel(1,0)==Colors.Lime&&Pixel(0,0)==Colors.Red,"GIF offset frame composition");p.Advance();Check(Pixel(1,0)==Colors.Red&&Pixel(2,1)==Colors.Blue,"GIF restore previous disposal");p.Advance();Check(Pixel(2,1).A==0&&Pixel(0,0)==Colors.Red,"GIF background disposal and transparent pixels");p.Advance();Check(p.FrameIndex==0&&Pixel(2,1)==Colors.Red,"GIF loop canvas reset");p.Start();await Task.Delay(75);Check(p.FrameIndex==1,"GIF dispatcher advances actual animation");p.Pause();int at=p.FrameIndex;await Task.Delay(130);Check(p.FrameIndex==at&&!p.IsRunning,"GIF pause holds frame");p.Start();await Task.Delay(110);Check(p.FrameIndex!=at,"GIF resumes playback");p.Dispose();Check(!p.IsRunning,"GIF timer cleanup");}
        var once=GifData.Load(Fixture(dir,null));using(var p=new GifPlayback(once)){p.Advance();p.Advance();p.Advance();Check(!p.Advance()&&p.FrameIndex==3,"non-looping GIF stops at last frame");}var finite=GifData.Load(Fixture(dir,1));Check(finite.TotalPlays==2,"GIF finite repeat count");Fixture(dir);
    }
}

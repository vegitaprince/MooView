using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace MooView;

// WIC decodes individual images. We apply GIF89a timing, offsets and disposal here.
public sealed record GifFrame(BitmapSource Bitmap, int X, int Y, int DelayMs, int Disposal, bool Transparent);
public sealed record GifData(int Width, int Height, Color Background, int TotalPlays, IReadOnlyList<GifFrame> Frames)
{
    public static GifData Load(string path)
    {
        using var stream=File.OpenRead(path);
        var decoder=new GifBitmapDecoder(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
        stream.Position=0;using var r=new BinaryReader(stream,Encoding.ASCII,true);
        if(!Encoding.ASCII.GetString(r.ReadBytes(6)).StartsWith("GIF"))throw new InvalidDataException("GIF 파일이 아닙니다.");
        int width=r.ReadUInt16(),height=r.ReadUInt16();byte flags=r.ReadByte(),bg=r.ReadByte();r.ReadByte();
        if(width==0||height==0||(long)width*height>100_000_000)throw new InvalidDataException("GIF 크기가 지원 범위를 벗어났습니다.");
        Color background=Colors.Transparent;
        if((flags&128)!=0){var table=r.ReadBytes(3*(1<<((flags&7)+1)));if(bg*3+2<table.Length)background=Color.FromRgb(table[bg*3],table[bg*3+1],table[bg*3+2]);}
        int plays=1,delay=100,disposal=0;bool transparent=false;var frames=new List<GifFrame>();
        byte[] Blocks(){using var b=new MemoryStream();int n;while((n=r.ReadByte())!=0){var chunk=r.ReadBytes(n);if(chunk.Length!=n)throw new EndOfStreamException();b.Write(chunk);}return b.ToArray();}
        while(stream.Position<stream.Length){int marker=r.ReadByte();if(marker==0x3b)break;
            if(marker==0x21){int label=r.ReadByte();var data=Blocks();if(label==0xf9&&data.Length>=4){disposal=(data[0]>>2)&7;transparent=(data[0]&1)!=0;int cs=data[1]|data[2]<<8;delay=cs==0?100:Math.Max(20,cs*10);}else if(label==0xff&&data.Length>=14){string app=Encoding.ASCII.GetString(data,0,11);if((app=="NETSCAPE2.0"||app=="ANIMEXTS1.0")&&data[11]==1){int loops=data[12]|data[13]<<8;plays=loops==0?0:loops+1;}}continue;}
            if(marker!=0x2c)throw new InvalidDataException("잘못된 GIF 블록입니다.");
            int x=r.ReadUInt16(),y=r.ReadUInt16();r.ReadUInt16();r.ReadUInt16();byte local=r.ReadByte();if((local&128)!=0)r.ReadBytes(3*(1<<((local&7)+1)));r.ReadByte();Blocks();
            if(frames.Count>=decoder.Frames.Count)throw new InvalidDataException("GIF 프레임 수가 올바르지 않습니다.");
            var bitmap=new FormatConvertedBitmap(decoder.Frames[frames.Count],PixelFormats.Bgra32,null,0);bitmap.Freeze();frames.Add(new(bitmap,x,y,delay,disposal,transparent));delay=100;disposal=0;transparent=false;
        }
        if(frames.Count==0)throw new InvalidDataException("GIF 프레임이 없습니다.");
        if(frames[0].Transparent)background=Colors.Transparent;
        return new(width,height,background,plays,frames);
    }
}
public sealed class GifPlayback:IDisposable
{
    readonly GifData data;readonly byte[] canvas;byte[]? previous;readonly DispatcherTimer timer=new(DispatcherPriority.Render);int completed;bool disposed;
    public WriteableBitmap Bitmap{get;}public int FrameIndex{get;private set;}public bool IsRunning=>timer.IsEnabled;
    public GifPlayback(GifData data){this.data=data;canvas=new byte[checked(data.Width*data.Height*4)];Bitmap=new WriteableBitmap(data.Width,data.Height,96,96,PixelFormats.Bgra32,null);Clear(new Rect(0,0,data.Width,data.Height),data.Background);Draw(0);timer.Tick+=Tick;}
    public void Start(){if(!disposed&&data.Frames.Count>1){timer.Interval=TimeSpan.FromMilliseconds(data.Frames[FrameIndex].DelayMs);timer.Start();}}
    void Tick(object? s,EventArgs e){if(!Advance()){timer.Stop();return;}timer.Interval=TimeSpan.FromMilliseconds(data.Frames[FrameIndex].DelayMs);}
    public bool Advance(){if(disposed)return false;int next=FrameIndex+1;if(next>=data.Frames.Count){completed++;if(data.TotalPlays>0&&completed>=data.TotalPlays)return false;next=0;}
        var old=data.Frames[FrameIndex];if(old.Disposal==2)Clear(new Rect(old.X,old.Y,old.Bitmap.PixelWidth,old.Bitmap.PixelHeight),old.Transparent?Colors.Transparent:data.Background);else if(old.Disposal==3&&previous!=null)Buffer.BlockCopy(previous,0,canvas,0,canvas.Length);
        if(next==0)Clear(new Rect(0,0,data.Width,data.Height),data.Background);Draw(next);return true;
    }
    void Clear(Rect rect,Color c){int x1=Math.Clamp((int)rect.X,0,data.Width),y1=Math.Clamp((int)rect.Y,0,data.Height),x2=Math.Clamp((int)rect.Right,0,data.Width),y2=Math.Clamp((int)rect.Bottom,0,data.Height);for(int y=y1;y<y2;y++)for(int x=x1;x<x2;x++){int at=(y*data.Width+x)*4;canvas[at]=c.B;canvas[at+1]=c.G;canvas[at+2]=c.R;canvas[at+3]=c.A;}}
    void Draw(int n){FrameIndex=n;var f=data.Frames[n];previous=f.Disposal==3?(byte[])canvas.Clone():null;int stride=f.Bitmap.PixelWidth*4;var src=new byte[stride*f.Bitmap.PixelHeight];f.Bitmap.CopyPixels(src,stride,0);for(int y=0;y<f.Bitmap.PixelHeight&&y+f.Y<data.Height;y++)for(int x=0;x<f.Bitmap.PixelWidth&&x+f.X<data.Width;x++){int a=y*stride+x*4;if(src[a+3]==0)continue;int b=((y+f.Y)*data.Width+x+f.X)*4;Buffer.BlockCopy(src,a,canvas,b,4);}Bitmap.WritePixels(new Int32Rect(0,0,data.Width,data.Height),canvas,data.Width*4,0);}
    public void Pause()=>timer.Stop();
    public void Dispose(){disposed=true;timer.Stop();timer.Tick-=Tick;previous=null;}
}

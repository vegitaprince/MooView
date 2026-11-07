using System.IO;
using System.Windows.Media.Imaging;
namespace MooView;

public partial class MainWindow
{
    const long ImageCacheBudget=192L*1024*1024;
    readonly Dictionary<string,GifData> gifCache=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Task<BitmapSource>> imageLoads=new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string,Task<GifData>> gifLoads=new(StringComparer.OrdinalIgnoreCase);
    readonly HashSet<string> prefetchErrors=new(StringComparer.OrdinalIgnoreCase);
    readonly SemaphoreSlim decodeSlots=new(2);int fileEpoch;bool prefetching,imageLoadingClosed;string[] prefetchWanted=[];
    void ResetImageLoading(){fileEpoch++;cache.Clear();gifCache.Clear();imageLoads.Clear();gifLoads.Clear();prefetchErrors.Clear();prefetchWanted=[];}
    async Task<BitmapSource> LoadAsync(string path)
    {
        if(cache.TryGetValue(path,out var bitmap))return bitmap;
        int epoch=fileEpoch;
        if(!imageLoads.TryGetValue(path,out var task))imageLoads[path]=task=DecodeBitmap(path);
        try{bitmap=await task;if(epoch==fileEpoch){cache[path]=bitmap;TrimImageCache();}return bitmap;}
        finally{if(imageLoads.TryGetValue(path,out var same)&&ReferenceEquals(same,task))imageLoads.Remove(path);}
    }
    async Task<BitmapSource> DecodeBitmap(string path){await decodeSlots.WaitAsync();try{return await Task.Run(()=>Imaging.Load(path));}finally{decodeSlots.Release();}}
    async Task<GifData> LoadGifAsync(string path)
    {
        if(gifCache.TryGetValue(path,out var data))return data;int epoch=fileEpoch;
        if(!gifLoads.TryGetValue(path,out var task))gifLoads[path]=task=DecodeGif(path);
        try{data=await task;if(epoch==fileEpoch){gifCache[path]=data;TrimImageCache();}return data;}
        finally{if(gifLoads.TryGetValue(path,out var same)&&ReferenceEquals(same,task))gifLoads.Remove(path);}
    }
    async Task<GifData> DecodeGif(string path){await decodeSlots.WaitAsync();try{return await Task.Run(()=>GifData.Load(path));}finally{decodeSlots.Release();}}
    static long BitmapBytes(BitmapSource bitmap)=>(long)bitmap.PixelWidth*bitmap.PixelHeight*Math.Max(4,(bitmap.Format.BitsPerPixel+7)/8);
    long CachedBytes()=>cache.Values.Sum(BitmapBytes)+gifCache.Values.Sum(data=>data.Frames.Sum(frame=>BitmapBytes(frame.Bitmap)));
    void TrimImageCache()
    {
        if(cache.Count+gifCache.Count<=256&&CachedBytes()<=ImageCacheBudget)return;
        var protectedPaths=files.Skip(index).Take(Math.Max(1,panes.Count)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        // Evict distant frames first, retaining the nearest lookahead for playback.
        var positions=files.Select((path,pos)=>(path,pos)).ToDictionary(x=>x.path,x=>x.pos,StringComparer.OrdinalIgnoreCase);
        var candidates=cache.Keys.Concat(gifCache.Keys).Distinct(StringComparer.OrdinalIgnoreCase).Where(path=>!protectedPaths.Contains(path)).OrderByDescending(path=>positions.TryGetValue(path,out int at)?(at-index+files.Count)%Math.Max(1,files.Count):int.MaxValue).ToArray();
        long bytes=CachedBytes();
        foreach(string path in candidates)
        {
            if(bytes<=ImageCacheBudget&&cache.Count+gifCache.Count<=256)break;
            if(cache.Remove(path,out var bitmap))bytes-=BitmapBytes(bitmap);
            if(gifCache.Remove(path,out var data))bytes-=data.Frames.Sum(frame=>BitmapBytes(frame.Bitmap));
        }
    }
    void RequestPrefetch()
    {
        if(files.Count==0||imageLoadingClosed)return;
        int count=Math.Min(files.Count,playing?Math.Max(24,panes.Count*12):Math.Max(4,panes.Count*2));
        prefetchWanted=Enumerable.Range(1,count).Select(offset=>(index+panes.Count-1+offset)%files.Count).Select(pos=>files[pos]).ToArray();
        if(!prefetching)_=RunPrefetch(fileEpoch);
    }
    async Task RunPrefetch(int epoch)
    {
        prefetching=true;var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            while(epoch==fileEpoch&&!imageLoadingClosed)
            {
                string? path=prefetchWanted.FirstOrDefault(p=>!visited.Contains(p)&&!prefetchErrors.Contains(p)&&!(Path.GetExtension(p).Equals(".gif",StringComparison.OrdinalIgnoreCase)?gifCache.ContainsKey(p):cache.ContainsKey(p)));
                if(path==null)break;visited.Add(path);
                try{if(Path.GetExtension(path).Equals(".gif",StringComparison.OrdinalIgnoreCase))await LoadGifAsync(path);else await LoadAsync(path);}
                catch{if(epoch==fileEpoch)prefetchErrors.Add(path);}
                if(CachedBytes()>=ImageCacheBudget)break;
            }
        }
        finally{prefetching=false;if(epoch!=fileEpoch&&files.Count>0&&!imageLoadingClosed)RequestPrefetch();}
    }
}

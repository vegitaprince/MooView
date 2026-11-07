using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class SlideshowTests
{
    internal static async Task Run(MainWindow w,string dir,List<string> log)
    {
        void Check(bool value,string label){if(!value)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string name,params object[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(w,args);
        foreach(double fps in new[]{.1,1,10,24,30})
        {
            var clock=new SlideshowClock();clock.Reset(fps);int count=0;for(int n=0;n<=3600;n++)if(clock.Take(n/60.0))count++;
            Check(Math.Abs(count-fps*60)<=1,"absolute playback deadlines maintain "+fps+" fps over one minute without timer drift");
        }
        var stalled=new SlideshowClock();stalled.Reset(30);stalled.Take(0);Check(stalled.Take(5)&&!stalled.Take(5)&&!stalled.Take(5.001),"decode stalls do not burst through or skip image groups");
        Check(!MainWindow.TryFps("NaN",out _)&&!MainWindow.TryFps("Infinity",out _)&&!MainWindow.TryFps("31",out _)&&MainWindow.TryFps("0.1",out _)&&MainWindow.TryFps("30",out _),"FPS entry validates finite range 0.1 to 30");
        string folder=Path.Combine(dir,"fps-sequence");Directory.CreateDirectory(folder);
        for(int n=0;n<96;n++)
        {
            byte[] pixels=new byte[160*120*4];for(int i=0;i<pixels.Length;i+=4){pixels[i]=(byte)(n*23);pixels[i+1]=(byte)(n*51);pixels[i+2]=(byte)(n*87);pixels[i+3]=255;}
            var bitmap=BitmapSource.Create(160,120,96,96,PixelFormats.Bgra32,null,pixels,640);bitmap.Freeze();Imaging.Save(bitmap,Path.Combine(folder,$"Frame_{n:000}.png"));
        }
        w.PageCount.SelectedIndex=0;w.OpenPaths(new[]{folder});await Task.Delay(100);
        var loads=Directory.GetFiles(folder,"*.png").Select(path=>(Task<BitmapSource>)Call("LoadAsync",path)!).ToArray();await Task.WhenAll(loads);
        var cache=(Dictionary<string,BitmapSource>)typeof(MainWindow).GetField("cache",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
        Check(cache.Count==96,"small image sequence stays decoded within bounded frame cache");
        string shared=Path.Combine(folder,"Frame_000.png");cache.Remove(shared);var first=(Task<BitmapSource>)Call("LoadAsync",shared)!;var second=(Task<BitmapSource>)Call("LoadAsync",shared)!;await Task.WhenAll(first,second);Check(ReferenceEquals(first.Result,second.Result),"foreground and prefetch requests share one bitmap decode");
        var gif1=(Task<GifData>)Call("LoadGifAsync",Path.Combine(dir,"Animation.gif"))!;var gif2=(Task<GifData>)Call("LoadGifAsync",Path.Combine(dir,"Animation.gif"))!;await Task.WhenAll(gif1,gif2);Check(ReferenceEquals(gif1.Result,gif2.Result),"GIF data is decoded once and reused for slideshow navigation");
        w.ShowActivated=true;w.Show();w.Activate();await Task.Delay(250);
        foreach(double target in new[]{10.0,30.0})
        {
            int changes=0;var previous=w.Pages.Children.OfType<ImagePane>().First().CurrentBitmap;var watch=new Stopwatch();
            EventHandler rendered=(_,_)=>{var current=w.Pages.Children.OfType<ImagePane>().First().CurrentBitmap;if(!ReferenceEquals(previous,current)){previous=current;if(watch.IsRunning)changes++;}};
            CompositionTarget.Rendering+=rendered;w.FpsInput.Text=target.ToString(System.Globalization.CultureInfo.InvariantCulture);Call("TogglePlay",w,new RoutedEventArgs());watch.Start();await Task.Delay(2000);watch.Stop();int committed=w.PresentedFrames;Call("Stop");CompositionTarget.Rendering-=rendered;
            double observed=changes/watch.Elapsed.TotalSeconds;Check(observed>=target*.8&&observed<=target*1.1,$"real WPF image changes at {target:0} fps: {observed:0.00}/s; {committed} commits in {watch.Elapsed.TotalSeconds:0.000}s");
        }
        w.FpsInput.Text="10";Call("TogglePlay",w,new RoutedEventArgs());await Task.Delay(100);w.FpsInput.Text="30";await Task.Delay(30);var scheduler=(SlideshowClock)typeof(MainWindow).GetField("slideshowClock",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;Check(scheduler.Fps==30,"changing FPS while playing immediately updates pacing");Call("Stop");
        w.LoopButton.IsChecked=false;w.OpenPaths(new[]{Path.Combine(folder,"Frame_095.png")});await Task.Delay(100);w.FpsInput.Text="30";Call("TogglePlay",w,new RoutedEventArgs());await Task.Delay(150);Check(System.Windows.Automation.AutomationProperties.GetName(w.PlayButton)=="재생"&&w.FileNameLabel.Text=="Frame_095.png","nonlooping slideshow stops on final image");w.LoopButton.IsChecked=true;
        w.Hide();
    }
}

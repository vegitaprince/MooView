using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
namespace MooView;

internal sealed class SlideshowClock
{
    internal double Fps {get;private set;}=1;double next=double.NaN;
    internal void Reset(double fps){Fps=fps;next=double.NaN;}
    internal bool Due(double now)=>double.IsNaN(next)||now+.0005>=next;
    internal bool Take(double now)
    {
        double period=1/Fps;
        if(double.IsNaN(next)){next=now+period;return false;}
        if(now+.0005<next)return false;
        next+=period;
        // Never burst through images after a decode stall or a minimized window.
        if(now-next>period)next=now+period;
        return true;
    }
}
public partial class MainWindow
{
    readonly SlideshowClock slideshowClock=new();readonly Stopwatch slideshowWatch=new();readonly DispatcherTimer slideshowPulse=new(DispatcherPriority.Render){Interval=TimeSpan.FromMilliseconds(8)};bool presentingFrame;double nextPlaybackUi,statsTime;int statsFrames,playbackGeneration;TimeSpan lastSlideshowRender=TimeSpan.MinValue;
    internal int PresentedFrames {get;private set;}
    void InitializeSlideshow()
    {
        // Request a paint at the deadline even when the visual tree is idle.
        // Image changes only occur in Rendering, once per distinct paint.
        slideshowPulse.Tick+=(_,_)=>{if(playing&&!loading&&slideshowClock.Due(slideshowWatch.Elapsed.TotalSeconds))Pages.InvalidateVisual();};
        FpsInput.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((_,_)=>
        {
            if(playing&&TryFps(FpsInput.Text,out double fps)&&fps!=slideshowClock.Fps){slideshowClock.Reset(fps);ResetPlaybackStats();}
        }));
        Closed+=(_,_)=>{playing=false;playbackGeneration++;CompositionTarget.Rendering-=SlideshowRendering;slideshowPulse.Stop();slideshowWatch.Stop();imageLoadingClosed=true;fileEpoch++;prefetchWanted=[];};
    }
    internal static bool TryFps(string text,out double fps)=>(double.TryParse(text,NumberStyles.Float,CultureInfo.CurrentCulture,out fps)||double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out fps))&&double.IsFinite(fps)&&fps>=.1&&fps<=30;
    void TogglePlay(object s,RoutedEventArgs e)
    {
        if(playing){Stop();return;}if(files.Count==0 || Comparing)return;
        if(!TryFps(FpsInput.Text,out double fps)){Status(L10n.T("InvalidFps"));return;}
        playing=true;playbackGeneration++;slideshowClock.Reset(fps);PresentedFrames=0;ResetPlaybackStats();
        CompositionTarget.Rendering+=SlideshowRendering;slideshowPulse.Start();
        PlayButton.Content=Glyph.Label("pause","정지");System.Windows.Automation.AutomationProperties.SetName(PlayButton,"정지");
        Status(L10n.Choose($"재생 {fps:0.#} fps",$"Playing at {fps:0.#} fps"));RequestPrefetch();
    }
    void ResetPlaybackStats(){slideshowWatch.Restart();statsTime=0;statsFrames=PresentedFrames;nextPlaybackUi=0;lastSlideshowRender=TimeSpan.MinValue;}
    async void SlideshowRendering(object? sender,EventArgs args)
    {
        if(!playing||loading||presentingFrame||args is not RenderingEventArgs frame||frame.RenderingTime==lastSlideshowRender||!slideshowClock.Take(slideshowWatch.Elapsed.TotalSeconds))return;
        lastSlideshowRender=frame.RenderingTime;
        if(!AdvanceGroup(1,LoopButton.IsChecked==true)){Stop();return;}
        presentingFrame=true;int generation=playbackGeneration;
        try
        {
            await RenderAsync(true);
            if(!playing||generation!=playbackGeneration)return;PresentedFrames++;
            double elapsed=slideshowWatch.Elapsed.TotalSeconds;
            if(elapsed-statsTime>=1)
            {
                double measured=(PresentedFrames-statsFrames)/(elapsed-statsTime);statsTime=elapsed;statsFrames=PresentedFrames;
                Status(L10n.Choose($"재생 {measured:0.0} / {slideshowClock.Fps:0.#} fps",$"Playing {measured:0.0} / {slideshowClock.Fps:0.#} fps"));
            }
        }
        catch(Exception ex){Stop();Error(ex);}
        finally{presentingFrame=false;}
    }
    bool ShouldUpdatePlaybackUi(bool playbackFrame)
    {
        if(!playbackFrame)return true;double now=slideshowWatch.Elapsed.TotalSeconds;
        if(now<nextPlaybackUi)return false;nextPlaybackUi=now+.2;return true;
    }
    void SyncCurrentPlaybackRow()
    {
        if(!nav||files.Count==0)return;
        if(lastVisibleCurrent!=null&&browserRows.TryGetValue(lastVisibleCurrent,out var previous))previous.item.FontWeight=FontWeights.Normal;
        if(browserRows.TryGetValue(files[index],out var current)){current.item.FontWeight=FontWeights.SemiBold;lastVisibleCurrent=files[index];FileList.ScrollIntoView(current.item);}
    }
    void Stop()
    {
        bool wasPlaying=playing;playing=false;playbackGeneration++;CompositionTarget.Rendering-=SlideshowRendering;slideshowPulse.Stop();slideshowWatch.Stop();
        PlayButton.Content=Glyph.Label("play","재생");System.Windows.Automation.AutomationProperties.SetName(PlayButton,"재생");
        if(wasPlaying&&ready){SyncPlaybackStrip();if(files.Count>0){TitleLabel.Text=Path.GetFileName(files[index]);Title=TitleLabel.Text+" — MooView";int end=PageNavigation.End(index,files.Count,Math.Max(1,panes.Count),pageOffset);CounterLabel.Text=$"{index+1:000}–{end:000} / {files.Count:000}";}SyncSelection();try{UpdateExif();}catch{/* A source may have been moved externally during playback. */}}
    }
}

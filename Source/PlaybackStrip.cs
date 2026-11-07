using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
namespace MooView;

public partial class MainWindow
{
    readonly DispatcherTimer seekTimer = new() { Interval = TimeSpan.FromMilliseconds(45) };
    bool updatingSeek;
    int pendingSeek = -1;
    void InitializePlaybackStrip()
    {
        seekTimer.Tick += (_, _) => { if (!loading) ApplyPendingSeek(); };
        ImageSlider.ScrubStarted += Stop;
        ImageSlider.ScrubFinished += () => { ApplyPendingSeek(); Keyboard.Focus(Root); };
        Closed += (_, _) => CancelPendingSeek();
    }
    void CancelPendingSeek() { pendingSeek = -1; seekTimer.Stop(); ImageSlider.CancelScrub(); }
    void SyncPlaybackStrip()
    {
        updatingSeek = true;
        try { ImageSlider.Maximum = Math.Max(0, files.Count - 1); ImageSlider.IsEnabled = files.Count > 1; if (!ImageSlider.IsScrubbing) ImageSlider.Value = Math.Clamp(index, 0, Math.Max(0, files.Count - 1)); }
        finally { updatingSeek = false; }
        int end = PageNavigation.End(index,files.Count,Math.Max(1,panes.Count),pageOffset);
        var names = files.Skip(index).Take(Math.Max(0,end-index)).Select(Path.GetFileName).ToArray();
        FileNameLabel.Text = names.Length == 0 ? "MooView" : string.Join(" · ", names);
        FileNameLabel.ToolTip = names.Length == 0 ? null : string.Join("\n",names);
        ImageSlider.ToolTip = files.Count == 0 ? L10n.T("Seek") : L10n.T("Seek") + $" · {index+1} / {files.Count}";
    }
    void ImageSliderChanged(object sender,RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || updatingSeek || files.Count == 0) return;
        Stop(); pendingSeek = Math.Clamp((int)Math.Round(e.NewValue),0,files.Count-1);
        if (ImageSlider.IsScrubbing) seekTimer.Start(); else ApplyPendingSeek();
    }
    void ApplyPendingSeek()
    {
        if (pendingSeek < 0) { seekTimer.Stop(); return; }
        int target = Math.Clamp(pendingSeek,0,Math.Max(0,files.Count-1)); pendingSeek = -1; seekTimer.Stop();
        if (files.Count == 0 || target == index) return;
        index = target; ResetPageGrouping(); _ = RenderAsync();
    }
}

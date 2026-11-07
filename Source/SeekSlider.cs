using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace MooView;

public sealed class SeekSlider : Slider
{
    public bool IsScrubbing { get; private set; }
    public event Action? ScrubStarted;
    public event Action? ScrubFinished;
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEnabled) return;
        Focus(); IsScrubbing = true; CaptureMouse(); ScrubStarted?.Invoke(); MoveTo(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        if (!IsScrubbing) return;
        if (e.LeftButton != MouseButtonState.Pressed) { Finish(); return; }
        MoveTo(e.GetPosition(this)); e.Handled = true;
    }
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsScrubbing) return;
        MoveTo(e.GetPosition(this)); Finish(); e.Handled = true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) { base.OnLostMouseCapture(e); Finish(); }
    internal void MoveTo(Point point)
    {
        var track = GetTemplateChild("PART_Track") as Track;
        double width = track?.ActualWidth ?? ActualWidth, thumb = track?.Thumb?.ActualWidth ?? 0;
        double x = track == null ? point.X : TranslatePoint(point,track).X;
        double ratio = (x-thumb/2)/Math.Max(1,width-thumb);
        if(IsDirectionReversed)ratio=1-ratio;
        double value = Minimum + ratio*(Maximum-Minimum);
        if (double.IsFinite(value)) Value = Math.Clamp(Math.Round(value), Minimum, Maximum);
    }
    void Finish()
    {
        if (!IsScrubbing) return;
        IsScrubbing = false; if (Mouse.Captured == this) ReleaseMouseCapture(); ScrubFinished?.Invoke();
    }
    internal void CancelScrub() { IsScrubbing = false; if (Mouse.Captured == this) ReleaseMouseCapture(); }
}

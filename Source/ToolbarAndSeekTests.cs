using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

public static class ToolbarAndSeekTests
{
    public static async Task Run(MainWindow w,string dir,List<string> log)
    {
        void Check(bool condition,string label){if(!condition)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,args);
        void Layout(double width=1400){w.Root.Measure(new Size(width,850));w.Root.Arrange(new Rect(0,0,width,850));w.Root.UpdateLayout();}
        string[] paths=Enumerable.Range(1,8).Select(n=>Path.Combine(dir,$"Sample_{n:00}.png")).ToArray();
        w.PageCount.SelectedIndex=0;w.OpenPaths(paths);await Task.Delay(120);Layout();
        Check(w.Root.RowDefinitions.Count==2&&Grid.GetRow(w.Stage)==0,"main image panel starts at top without a separate caption row");
        var first=w.ViewToolbar.Children.OfType<Button>().First();double menuY=first.TranslatePoint(new Point(0,first.ActualHeight/2),w.Root).Y;double closeY=w.CloseWindowButton.TranslatePoint(new Point(0,w.CloseWindowButton.ActualHeight/2),w.Root).Y;
        Check(Math.Abs(menuY-closeY)<=1&&w.WindowHeader.IsDescendantOf(w.Stage),"window controls align with image toolbar buttons");
        Check(w.FileNameLabel.Text=="Sample_01.png"&&w.ImageSlider.Maximum==7&&w.ImageSlider.Value==0,"footer displays filename and zero-based image timeline");
        w.ImageSlider.Value=5;await Task.Delay(120);Check(w.TitleLabel.Text=="Sample_06.png"&&w.FileNameLabel.Text=="Sample_06.png"&&w.CounterLabel.Text.StartsWith("006"),"timeline seeks directly to matching image and filename");
        Call("Move",1);await Task.Delay(100);Check(w.ImageSlider.Value==6&&w.FileNameLabel.Text=="Sample_07.png","normal image navigation keeps timeline synchronized");
        w.PageCount.SelectedIndex=2;w.ImageSlider.Value=2;await Task.Delay(100);Check(w.FileNameLabel.Text=="Sample_03.png · Sample_04.png · Sample_05.png","footer lists every filename in multi-image view");
        Call("Move",1);await Task.Delay(100);Check(w.ImageSlider.Value==5&&w.TitleLabel.Text=="Sample_06.png","next after seeking still advances the full image group");
        w.PageCount.SelectedIndex=0;w.OpenPaths(paths);await Task.Delay(100);w.FpsInput.Text="1";Call("TogglePlay",w,new RoutedEventArgs());w.ImageSlider.Value=4;await Task.Delay(100);
        Check(System.Windows.Automation.AutomationProperties.GetName(w.PlayButton)=="재생"&&w.TitleLabel.Text=="Sample_05.png","manual timeline seeking pauses slideshow playback");
        var scrubbing=typeof(SeekSlider).GetProperty(nameof(SeekSlider.IsScrubbing))!;scrubbing.SetValue(w.ImageSlider,true);w.ImageSlider.Value=1;w.ImageSlider.Value=3;w.ImageSlider.Value=7;
        Check((int)typeof(MainWindow).GetField("pendingSeek",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)! == 7,"fast scrubbing coalesces intermediate positions");
        Call("ApplyPendingSeek");await Task.Delay(120);Check(w.ImageSlider.Value==7&&w.FileNameLabel.Text=="Sample_08.png","image rendering does not pull the timeline thumb backwards during drag");scrubbing.SetValue(w.ImageSlider,false);
        Layout();w.ImageSlider.MoveTo(new Point(-100,10));await Task.Delay(100);Check(w.ImageSlider.Value==0&&w.FileNameLabel.Text=="Sample_01.png","dragging before track start clamps to first image");w.ImageSlider.MoveTo(new Point(w.ImageSlider.ActualWidth+100,10));await Task.Delay(100);Check(w.ImageSlider.Value==7&&w.FileNameLabel.Text=="Sample_08.png","dragging past track end clamps to last image");
        w.ImageSlider.MoveTo(new Point(w.ImageSlider.ActualWidth*.2,10));w.ImageSlider.MoveTo(new Point(w.ImageSlider.ActualWidth*.8,10));w.ImageSlider.MoveTo(new Point(w.ImageSlider.ActualWidth*.5,10));await Task.Delay(100);Check(w.ImageSlider.Value==4&&w.FileNameLabel.Text=="Sample_05.png","rapid absolute track dragging remains stable before layout updates");
        scrubbing.SetValue(w.ImageSlider,true);w.ImageSlider.Value=2;w.OpenPaths(paths);scrubbing.SetValue(w.ImageSlider,false);await Task.Delay(150);Check(w.ImageSlider.Value==0&&w.TitleLabel.Text=="Sample_01.png","opening another file list cancels stale scrub request");
        string singleDir=Path.Combine(dir,"single-image");Directory.CreateDirectory(singleDir);string single=Path.Combine(singleDir,"Only.png");File.Copy(paths[0],single,true);w.OpenPaths(new[]{single});await Task.Delay(100);Check(!w.ImageSlider.IsEnabled&&w.ImageSlider.Maximum==0&&w.FileNameLabel.Text=="Only.png","single image disables timeline without hiding filename");
        w.OpenPaths(paths);await Task.Delay(100);Layout(960);menuY=first.TranslatePoint(new Point(0,first.ActualHeight/2),w.Root).Y;closeY=w.CloseWindowButton.TranslatePoint(new Point(0,w.CloseWindowButton.ActualHeight/2),w.Root).Y;Check(Math.Abs(menuY-closeY)<=1&&w.WindowHeader.ActualWidth>100,"window controls stay on first toolbar row when menus wrap");
        Check(w.ImageSlider.ActualWidth>100&&w.FileNameLabel.ActualWidth>0,"footer timeline remains usable in narrow window");
        Call("SetFull",true);Layout();Check(w.ToolbarRow.Visibility==Visibility.Collapsed&&w.BottomBar.Visibility==Visibility.Collapsed,"fullscreen removes complete toolbar row and timeline");Call("SetChrome",true);Layout();Check(w.ToolbarRow.Visibility==Visibility.Visible&&w.WindowHeader.Visibility==Visibility.Collapsed,"fullscreen mouse menus reveal toolbar without window controls");Call("SetFull",false);Layout();
        Check(TextOptions.GetTextRenderingMode(w)==TextRenderingMode.Grayscale,"UI text uses grayscale antialiasing without RGB fringes");
        Check(Glyph.Paths.Keys.All(k=>Glyph.Vector(k).shape.IsFrozen)&&ReferenceEquals(Glyph.Vector("image").shape,Glyph.Vector("image").shape),"all menu icon geometries are frozen reusable vectors");
        w.ImageSlider.ApplyTemplate();Check(w.ImageSlider.Template.FindName("PART_Track",w.ImageSlider) is System.Windows.Controls.Primitives.Track,"timeline uses vector track and thumb template");
        foreach(bool dark in new[]{true,false})
        {
            if(((SolidColorBrush)Application.Current.Resources["Bg"]).Color.R==38!=dark)Call("ToggleTheme",w,new RoutedEventArgs());
            Layout();foreach(double scale in new[]{1.0,1.25,1.5,2.0})
            {
                var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(w.Background,null,new Rect(0,0,1400,850));
                var bitmap=new RenderTargetBitmap((int)(1400*scale),(int)(850*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(background);bitmap.Render(w.Root);Imaging.Save(bitmap,Path.Combine(AppContext.BaseDirectory,$"preview-new-{(dark?"dark":"light")}-{scale:0.##}.png"));
            }
        }
    }
}

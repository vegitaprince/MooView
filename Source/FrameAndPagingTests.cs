using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
namespace MooView;

public static class FrameAndPagingTests
{
    public static async Task Run(MainWindow w,string dir,List<string> log)
    {
        void Check(bool condition,string label){if(!condition)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,args);
        void Layout(){w.Root.Measure(new Size(1400,850));w.Root.Arrange(new Rect(0,0,1400,850));w.Root.UpdateLayout();}
        string[] Visible()=>w.Pages.Children.OfType<ImagePane>().Where(p=>p.Visibility==Visibility.Visible).Select(p=>Path.GetFileName(p.FilePath)!).ToArray();
        string[] paths=Enumerable.Range(1,8).Select(n=>Path.Combine(dir,$"Sample_{n:00}.png")).ToArray();
        // All possible partial prefixes/tails are covered, including groups anchored
        // to an image opened directly from Explorer rather than file number one.
        for(int count=1;count<=25;count++)for(int size=1;size<=4;size++)for(int offset=0;offset<Math.Min(size,count);offset++)
        {
            var visited=new List<int>();int start=0;
            while(true)
            {
                int end=PageNavigation.End(start,count,size,offset);
                if(end<=start)throw new Exception("empty page group");
                visited.AddRange(Enumerable.Range(start,end-start));
                var next=PageNavigation.Neighbor(start,count,size,offset,1,false);
                if(next==null){if(start!=PageNavigation.Last(count,size,offset))throw new Exception("wrong last page");break;}
                if(PageNavigation.Neighbor(next.Value,count,size,offset,-1,false)!=start)throw new Exception("page navigation does not reverse");
                start=next.Value;
            }
            if(!visited.SequenceEqual(Enumerable.Range(0,count)))throw new Exception("page groups overlap or skip files");
            if(PageNavigation.Neighbor(start,count,size,offset,1,true)!=0||PageNavigation.Neighbor(0,count,size,offset,-1,true)!=start)throw new Exception("wrong group wrapping");
        }
        Check(true,"page groups cover every file exactly once across sizes 1–4 and partial tails/prefixes");
        for(int size=2;size<=4;size++)for(int layout=0;layout<3;layout++)
        {
            w.OpenPaths(paths);w.PageCount.SelectedIndex=size-1;w.LayoutMode.SelectedIndex=layout;await Task.Delay(120);
            var before=Visible();Call("Move",1);await Task.Delay(120);var after=Visible();
            Check(before.Length==size&&after.Length==size&&!before.Intersect(after).Any()&&after[0]==$"Sample_{size+1:00}.png",$"{size}-image layout {layout} advances a complete group");
            Call("Move",-1);await Task.Delay(100);if(!Visible().SequenceEqual(before))throw new Exception("previous group mismatch");
        }
        w.PageCount.SelectedIndex=2;w.OpenPaths(paths);await Task.Delay(100);Call("Move",1);await Task.Delay(100);Call("Move",1);await Task.Delay(100);
        Check(Visible().SequenceEqual(new[]{"Sample_07.png","Sample_08.png"}),"last three-image group shows only remaining two images");
        Call("Move",1);await Task.Delay(100);Check(Visible().SequenceEqual(new[]{"Sample_01.png","Sample_02.png","Sample_03.png"}),"next after partial group wraps without mixing old images");
        w.OpenPaths(paths);w.PageCount.SelectedIndex=2;await Task.Delay(100);w.LoopButton.IsChecked=false;w.FpsInput.Text="30";Call("TogglePlay",w,new RoutedEventArgs());await Task.Delay(600);Call("Stop");
        Check(Visible().SequenceEqual(new[]{"Sample_07.png","Sample_08.png"}),"slideshow advances groups and stops at partial final group");w.LoopButton.IsChecked=true;
        Check(w.WindowStyle==WindowStyle.None&&WindowChrome.GetWindowChrome(w)!=null&&w.WindowHeader.Visibility==Visibility.Visible,"native title bar replaced by integrated window controls");
        Call("MaximizeWindow",w,new RoutedEventArgs());Check(w.WindowState==WindowState.Maximized,"integrated maximize button maximizes window");Call("MaximizeWindow",w,new RoutedEventArgs());Check(w.WindowState==WindowState.Normal,"integrated maximize button restores window");
        Call("MinimizeWindow",w,new RoutedEventArgs());Check(w.WindowState==WindowState.Minimized,"integrated minimize button minimizes window");w.WindowState=WindowState.Normal;
        Call("ToggleNav",w,new RoutedEventArgs());w.SetSidebarWidth(410);Layout();Check(Math.Abs(w.Root.ColumnDefinitions[1].ActualWidth-410)<2&&w.SidebarSplitter.Visibility==Visibility.Visible,"resizable sidebar lays out at requested width");
        Call("ToggleNav",w,new RoutedEventArgs());Layout();Check(w.Root.ColumnDefinitions[1].ActualWidth==0&&w.Root.ColumnDefinitions[2].ActualWidth==0,"hidden sidebar and resize handle consume no width");Call("ToggleNav",w,new RoutedEventArgs());Layout();Check(Math.Abs(w.Root.ColumnDefinitions[1].ActualWidth-410)<2,"sidebar remembers width when reopened");
        w.SetSidebarWidth(9999);Layout();Check(w.Root.ColumnDefinitions[1].ActualWidth<=640,"sidebar width is bounded to preserve image area");w.SetSidebarWidth(100);Layout();Check(w.Root.ColumnDefinitions[1].ActualWidth>=200,"sidebar minimum keeps controls usable");w.SetSidebarWidth(320);Layout();
        Call("SetFull",true);Check(w.WindowHeader.Visibility==Visibility.Collapsed&&w.SidebarSplitter.Visibility==Visibility.Collapsed&&WindowChrome.GetWindowChrome(w)==null,"fullscreen hides custom window controls and resize handle");Call("SetFull",false);Layout();Check(w.WindowHeader.Visibility==Visibility.Visible&&WindowChrome.GetWindowChrome(w)!=null&&Math.Abs(w.Root.ColumnDefinitions[1].ActualWidth-320)<2,"fullscreen exit restores integrated frame and sidebar width");
        Check(w.UseLayoutRounding&&w.SnapsToDevicePixels&&TextOptions.GetTextFormattingMode(w)==TextFormattingMode.Display,"UI uses pixel-aligned layout and display text formatting");
        Check(w.Stage.Effect==null&&w.Sidebar.Effect==null&&w.Rail.Effect==null&&w.BottomBar.Effect==null,"UI containers no longer rasterize content through shadow effects");
        foreach(double scale in new[]{1.0,1.25,1.5,2.0}){Layout();var bitmap=new RenderTargetBitmap((int)(1400*scale),(int)(850*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(w.Root);Imaging.Save(bitmap,Path.Combine(AppContext.BaseDirectory,$"preview-frame-{scale:0.##}.png"));}
        Call("ToggleTheme",w,new RoutedEventArgs());Layout();var light=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);light.Render(w.Root);Imaging.Save(light,Path.Combine(AppContext.BaseDirectory,"preview-frame-light.png"));Call("ToggleTheme",w,new RoutedEventArgs());
    }
}

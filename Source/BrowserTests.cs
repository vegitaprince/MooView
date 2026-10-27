using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

public static class BrowserTests
{
    public static async Task Run(MainWindow w,string dir,List<string> log)
    {
        void Check(bool condition,string label){if(!condition)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,args);
        T Field<T>(string name)=>(T)typeof(MainWindow).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(w)!;
        w.OpenPaths(Enumerable.Range(1,8).Select(n=>Path.Combine(dir,$"Sample_{n:00}.png")));await Task.Delay(100);
        var paths=Field<List<string>>("files").ToArray();var selected=Field<HashSet<string>>("selected");
        w.SelectBrowserPath(paths[1],ModifierKeys.None);w.SelectBrowserPath(paths[4],ModifierKeys.Shift);
        Check(selected.SetEquals(paths.Skip(1).Take(4)),"Shift click selects contiguous file range");
        w.SelectBrowserPath(paths[7],ModifierKeys.Control);Check(selected.Contains(paths[7])&&selected.Count==5,"Ctrl click adds nonadjacent file");
        w.SelectBrowserPath(paths[2],ModifierKeys.Control);Check(!selected.Contains(paths[2])&&selected.Count==4,"Ctrl click deselects existing file");
        w.SelectBrowserPath(paths[1],ModifierKeys.Alt);Check(!selected.Contains(paths[1])&&selected.Count==3,"Alt click deselects without clearing other files");
        w.PaintChecks(paths[0],paths[3],true);Check(selected.Contains(paths[0])&&selected.Contains(paths[2]),"checkbox sweep selects skipped rows");
        w.PaintChecks(paths[3],paths[0],false);Check(!selected.Contains(paths[0])&&!selected.Contains(paths[3])&&selected.Contains(paths[7]),"reverse checkbox sweep clears range only");
        var data=MainWindow.FileDragData(new[]{paths[0],paths[7]});Check(data.GetDataPresent(DataFormats.FileDrop)&&((string[])data.GetData(DataFormats.FileDrop)!).SequenceEqual(new[]{paths[0],paths[7]}),"outgoing shell drag contains original full file paths");
        Call("ToggleNav",w,new RoutedEventArgs());w.Root.Measure(new Size(1400,850));w.Root.Arrange(new Rect(0,0,1400,850));w.Root.UpdateLayout();
        w.BrowserSize.Value=50;w.Root.UpdateLayout();var rows=Field<Dictionary<string,(ListBoxItem item,CheckBox check,Image image)>>("browserRows");Check(rows.Values.All(r=>r.image.Visibility==Visibility.Visible)&&rows.Values.First().item.Width<120,"thumbnail slider creates multiple thumbnail columns");
        await (Task)Call("LoadThumbnail",paths[0],rows[paths[0]].image,Field<int>("browserVersion"))!;
        Check(rows[paths[0]].image.Source is BitmapSource thumb&&thumb.PixelWidth<=256,"browser decodes reduced-size real image thumbnail");
        w.BrowserSize.Value=140;w.Root.UpdateLayout();Check(rows.Values.First().image.Height>100&&rows.Values.First().item.Width>150,"thumbnail slider enlarges image previews");
        var render=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);render.Render(w.Root);Imaging.Save(render,Path.Combine(AppContext.BaseDirectory,"preview-browser.png"));
        w.BrowserSize.Value=0;Check(rows.Values.All(r=>r.image.Visibility==Visibility.Collapsed),"slider returns to compact filename list");
        Check(w.LoopButton is ToggleButton&&w.LoopButton.IsChecked==true,"repeat playback uses a pressed icon toggle");w.LoopButton.IsChecked=false;Check(w.LoopButton.IsChecked==false,"repeat icon toggles off");
        typeof(MainWindow).GetField("index",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,paths.Length-1);w.FpsInput.Text="30";Call("TogglePlay",w,new RoutedEventArgs());await Task.Delay(130);Check(!Field<bool>("playing")&&Field<int>("index")==paths.Length-1,"disabled repeat stops slideshow at last image");w.LoopButton.IsChecked=true;
        Call("ToggleNav",w,new RoutedEventArgs());
        string moveDir=Path.Combine(dir,"drag-source"),destination=Path.Combine(dir,"drag-destination");Directory.CreateDirectory(moveDir);Directory.CreateDirectory(destination);
        string original=Path.Combine(moveDir,"move.png"),moved=Path.Combine(destination,"move.png");File.Copy(paths[0],original,true);w.OpenPaths(new[]{original});await Task.Delay(80);File.Move(original,moved,true);Call("RefreshAfterFileDrag");
        Check(Field<List<string>>("files").Count==0&&w.Pages.Children.Cast<ImagePane>().All(p=>p.Visibility==Visibility.Hidden),"external file move removes stale file and image from viewer");
        w.OpenPaths(new[]{paths[0]});await Task.Delay(80);
    }
}

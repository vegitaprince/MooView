using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
namespace MooView;

internal static class WindowDragTests
{
    static IEnumerable<DependencyObject> Descendants(DependencyObject root){for(int n=0;n<VisualTreeHelper.GetChildrenCount(root);n++){var child=VisualTreeHelper.GetChild(root,n);yield return child;foreach(var next in Descendants(child))yield return next;}}
    internal static async Task Run(MainWindow w,string dir,List<string> log)
    {
        void Check(bool value,string label){if(!value)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string name,params object[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,args);
        void Layout(){w.Root.Measure(new Size(1400,850));w.Root.Arrange(new Rect(0,0,1400,850));w.Root.UpdateLayout();}
        bool Can(FrameworkElement element)=>w.CanDragWindow(element,element.TranslatePoint(new Point(element.ActualWidth/2,element.ActualHeight/2),w));
        w.OpenPaths(new[]{Path.Combine(dir,"Sample_01.png")});w.PageCount.SelectedIndex=0;w.FitMode.SelectedIndex=0;await Task.Delay(150);Layout();
        Check(Can(w.Rail),"blank left rail is a window drag region");
        Check(Can(w.BottomBar)&&Can(w.FileNameLabel)&&Can(w.StatusLabel),"footer blank space filenames and status text move the window");
        Check(Can(w.ToolbarRow)&&Can(w.FolderLabel),"toolbar and sidebar labels move the window");
        foreach(var control in new FrameworkElement[]{w.AboutButton,w.PlayButton,w.LoopButton,w.PageCount,w.FitMode,w.FpsInput,w.ZoomInput,w.Search,w.ImageSlider,w.BrowserSize,w.SidebarSplitter,w.ExifText,w.CloseWindowButton})Check(!Can(control),"interactive control keeps its own drag/click: "+control.Name);
        var row=(ListBoxItem)w.FileList.Items[0];Check(!Can(row)&&!Can(Descendants(row).OfType<CheckBox>().First()),"file rows and checkbox sweep keep selection and file export gestures");
        Check(Can(w.FileList),"empty file-list space can move the window");
        foreach(int count in new[]{1,2,4})
        {
            w.PageCount.SelectedIndex=count-1;w.LayoutMode.SelectedIndex=1;await Task.Delay(130);Layout();
            foreach(var pane in w.Pages.Children.OfType<ImagePane>().Where(pane=>pane.Visibility==Visibility.Visible))
            {
                var bounds=pane.DisplayedImageBounds;bounds.Intersect(new Rect(pane.RenderSize));Check(!bounds.IsEmpty,"rendered image bounds are available for "+count+"-image view");
                var imagePoint=new Point(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);Check(!w.CanDragWindow(pane,pane.TranslatePoint(imagePoint,w)),"actual image retains export/pan gesture in "+count+"-image view");
                var blank=new[]{new Point(1,1),new Point(pane.ActualWidth-1,1),new Point(1,pane.ActualHeight-1),new Point(pane.ActualWidth-1,pane.ActualHeight-1)}.FirstOrDefault(point=>!pane.IsImageInteraction(point),new Point(-1,-1));
                Check(blank.X>=0&&w.CanDragWindow(pane,pane.TranslatePoint(blank,w)),"letterbox area moves the window in "+count+"-image view");
            }
        }
        w.PageCount.SelectedIndex=0;w.FitMode.SelectedIndex=4;Call("SetZoom",400.0);Layout();var zoomed=w.Pages.Children.OfType<ImagePane>().First();var map=Descendants(zoomed).OfType<Canvas>().First();Check(!w.CanDragWindow(map,map.TranslatePoint(new Point(40,40),w)),"zoom minimap keeps its pan interaction");
        Call("ToggleExif",w,new RoutedEventArgs());Layout();var exifLabel=Descendants(w.ExifPanel).OfType<TextBlock>().First(text=>text.Text=="EXIF");Check(Can(exifLabel)&&!Can(w.ExifText),"EXIF header can move the window while metadata text remains selectable");Call("ToggleExif",w,new RoutedEventArgs());
        Call("ShowAbout",w,new RoutedEventArgs());Check(!Can(w.AboutCard),"introduction overlay dismisses rather than dragging the window");Call("CloseAbout");
        Call("SetFull",true);Check(!Can(w.BottomBar)&&!w.CanDragWindow(zoomed,zoomed.TranslatePoint(new Point(1,1),w)),"fullscreen never starts a window move");Call("SetFull",false);
        Check(!MainWindow.DragThresholdReached(new Point(),new Point(1,1))&&MainWindow.DragThresholdReached(new Point(),new Point(SystemParameters.MinimumHorizontalDragDistance+1,0)),"a normal click does not move the window before the drag threshold");
        var empty=new MainWindow(true);Check(empty.CanDragWindow(empty.EmptyState,new Point(300,300)),"empty viewer placeholder can move the window");empty.Close();w.FitMode.SelectedIndex=0;Layout();
    }
}

using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class AboutTests
{
    internal static void Run(MainWindow w,List<string> log,System.Windows.Interop.HwndSource source)
    {
        void Check(bool value,string label){if(!value)throw new Exception(label);log.Add("PASS: "+label);}
        object? Call(string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,args);
        Check(w.AboutButton.Content is Glyph eye&&eye.Kind=="eye"&&Glyph.Paths["eye"]!=Glyph.Paths["image"],"brand eye differs from image import icon");
        Check(w.AboutOverlay.Visibility==Visibility.Collapsed,"about introduction starts hidden");
        foreach(MouseButton button in new[]{MouseButton.Left,MouseButton.Middle,MouseButton.Right})
        {
            Call("ShowAbout",w,new RoutedEventArgs());Check(w.AboutOverlay.Visibility==Visibility.Visible,"brand click opens introduction: "+button);
            var e=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=Mouse.PreviewMouseDownEvent};Call("OnMouseButton",w,e);
            Check(e.Handled&&w.AboutOverlay.Visibility==Visibility.Collapsed,"any mouse button dismisses introduction without click-through: "+button);
            var up=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,button){RoutedEvent=Mouse.PreviewMouseUpEvent};w.RaiseEvent(up);Check(up.Handled,"dismissal also consumes mouse release: "+button);
        }
        Call("ShowAbout",w,new RoutedEventArgs());var space=new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Space){RoutedEvent=Keyboard.PreviewKeyDownEvent};Call("OnKey",w,space);Check(space.Handled&&w.AboutOverlay.Visibility==Visibility.Visible,"about prevents background keyboard navigation");
        var escape=new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent};Call("OnKey",w,escape);Check(w.AboutOverlay.Visibility==Visibility.Collapsed,"Escape dismisses introduction");
        Call("ShowAbout",w,new RoutedEventArgs());typeof(Window).GetMethod("OnDeactivated",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(w,new object[]{EventArgs.Empty});Check(w.AboutOverlay.Visibility==Visibility.Collapsed,"clicking another application dismisses introduction on deactivation");
        foreach(bool dark in new[]{false,true})
        {
            typeof(MainWindow).GetField("dark",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,dark);Call("ApplyTheme");
            foreach(bool english in new[]{false,true})
            {
                L10n.Instance.English=english;Call("ShowAbout",w,new RoutedEventArgs());w.Root.Measure(new Size(1400,850));w.Root.Arrange(new Rect(0,0,1400,850));w.Root.UpdateLayout();
                Check(w.AboutVersion.Text.Contains(UpdateClient.CurrentVersion.ToString(3))&&w.AboutCard.ActualWidth<=420.8&&w.AboutCard.ActualHeight<500,"localized themed about card fits: "+(dark?"dark":"light")+" "+(english?"EN":"KO"));
                if(!english){var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(w.Background,null,new Rect(0,0,1400,850));var bitmap=new RenderTargetBitmap(1400,850,96,96,PixelFormats.Pbgra32);bitmap.Render(background);bitmap.Render(w.Root);Imaging.Save(bitmap,Path.Combine(AppContext.BaseDirectory,"preview-about-"+(dark?"dark":"light")+".png"));}
                Call("CloseAbout");
            }
        }
        typeof(MainWindow).GetField("dark",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(w,false);Call("ApplyTheme");L10n.Instance.English=false;
    }
}

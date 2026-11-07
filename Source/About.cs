using System.Windows;
using System.Windows.Input;
namespace MooView;

public partial class MainWindow
{
    bool dismissAboutRelease;
    void InitializeAbout()
    {
        AboutVersion.Text="v"+UpdateClient.CurrentVersion.ToString(3)+" · vegitaprince";
        Deactivated+=(_,_)=>{CloseAbout();dismissAboutRelease=false;};
        PreviewMouseUp+=(_,e)=>{if(dismissAboutRelease){dismissAboutRelease=false;e.Handled=true;}};
    }
    void ShowAbout(object sender,RoutedEventArgs e)
    {
        Stop();AboutOverlay.Visibility=Visibility.Visible;Keyboard.Focus(AboutOverlay);
    }
    void CloseAbout(){AboutOverlay.Visibility=Visibility.Collapsed;}
    bool DismissAbout(MouseButtonEventArgs e)
    {
        if(AboutOverlay.Visibility!=Visibility.Visible)return false;
        CloseAbout();dismissAboutRelease=true;e.Handled=true;return true;
    }
}

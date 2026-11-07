using System.Windows;
using System.Windows.Threading;
namespace MooView;

public partial class MainWindow
{
    UpdateManager? updates;readonly DispatcherTimer updateNoticeTimer=new(){Interval=TimeSpan.FromSeconds(10)};readonly DispatcherTimer updateCheckTimer=new(){Interval=TimeSpan.FromMinutes(30)};
    bool updateWindowOpen,notifiedUpdate,updateStopped,updateRestarting,updateSessionEnding;string? notifiedVersion;
    void InitializeUpdates(bool testMode)
    {
        if(testMode)return;updates=new UpdateManager();updates.Changed+=UpdateStateChanged;
        Loaded+=async(_,_)=>{try{await Task.Delay(2500);if(!updateStopped)await updates.CheckAsync();}catch{/* Offline startup never interrupts image viewing. */}};
        updateCheckTimer.Tick+=async(_,_)=>{if(!updateStopped)await updates.CheckAsync();};updateCheckTimer.Start();
        updateNoticeTimer.Tick+=(_,_)=>TryUpdateNotice();Activated+=(_,_)=>Dispatcher.BeginInvoke(TryUpdateNotice);
        Application.Current.SessionEnding+=UpdateSessionEnding;
        Closed+=(_,_)=>
        {
            updateStopped=true;updateNoticeTimer.Stop();updateCheckTimer.Stop();Application.Current.SessionEnding-=UpdateSessionEnding;
            updates.Changed-=UpdateStateChanged;if(!updateRestarting)updates.ApplyOnExit(updateSessionEnding);updates.Dispose();
        };
        UpdateStateChanged();
    }
    void UpdateSessionEnding(object sender,SessionEndingCancelEventArgs e)=>updateSessionEnding=true;
    internal static bool CanNotifyUpdate(bool fullscreen,bool active,int ownedWindows,bool dialogOpen)=>!fullscreen&&active&&ownedWindows==0&&!dialogOpen;
    void UpdateStateChanged()
    {
        if(updateStopped||updates==null)return;
        if(updates.Available!=null||updates.Ready)UpdateButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty,"Accent");
        if(updates.Ready)Status(L10n.T(updates.State)+" · "+updates.ReadyVersion);
        if(updates.Available!=null&&!updates.Preferences.DownloadOnExit&&!updates.Ready){updateNoticeTimer.Start();TryUpdateNotice();}
    }
    void TryUpdateNotice()
    {
        if(updateStopped||updates==null||updates.Preferences.DownloadOnExit||updates.Available==null||updates.Ready||AboutOverlay.Visibility==Visibility.Visible||!CanNotifyUpdate(full,IsActive,OwnedWindows.Count,updateWindowOpen))return;
        string version=updates.Available.Version.ToString(3);if(notifiedUpdate&&version==notifiedVersion)return;
        notifiedUpdate=true;notifiedVersion=version;updateNoticeTimer.Stop();ShowUpdates(this,new RoutedEventArgs());
    }
    void ShowUpdates(object sender,RoutedEventArgs e)
    {
        if(updateStopped||updateWindowOpen||updates==null)return;Stop();updateWindowOpen=true;notifiedUpdate=true;notifiedVersion=updates.Available?.Version.ToString(3);updateNoticeTimer.Stop();
        try
        {
            var dialog=new UpdateWindow(updates){Owner=this};if(dialog.ShowDialog()!=true||!updates.Ready)return;
            if(OwnedWindows.Cast<Window>().Any(w=>w.IsVisible)||UpdateInstaller.OtherInstances(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar),Environment.ProcessId)){Dialogs.Info(this,L10n.T("Updates"),L10n.T("UpdateOtherInstances"));return;}
            updateRestarting=updates.LaunchPending(true,files.Count>0?files[index]:null);if(updateRestarting)Close();
        }
        catch(Exception ex){if(!updateStopped)Error(ex);}
        finally{if(!updateStopped){updateWindowOpen=false;notifiedVersion=updates.Available?.Version.ToString(3);}}
    }
}

using System.IO;
using System.Windows;
using System.Windows.Threading;
namespace MooView;

public partial class MainWindow
{
    readonly UpdateClient updateClient=new();readonly CancellationTokenSource updateLifetime=new();readonly DispatcherTimer updateNoticeTimer=new(){Interval=TimeSpan.FromSeconds(10)};
    readonly string updateSettingsPath=Path.Combine(UpdateClient.DataRoot,"updates.json");UpdatePreferences updatePreferences=new();UpdateRelease? availableUpdate;bool updateWindowOpen,notifiedUpdate,updateStopped;
    void InitializeUpdates(bool testMode)
    {
        if(!testMode)
        {
            updatePreferences=UpdatePreferences.Load(updateSettingsPath);Loaded+=async(_,_)=>
            {
                try{await Task.Delay(2500,updateLifetime.Token);if(!updatePreferences.ShouldCheck(DateTimeOffset.UtcNow))return;
                    updatePreferences=updatePreferences with{LastCheckUtc=DateTimeOffset.UtcNow};SaveUpdatePreferences();availableUpdate=await updateClient.CheckAsync(UpdateClient.CurrentVersion,updateLifetime.Token);
                    if(availableUpdate!=null){UpdateButton.SetResourceReference(System.Windows.Controls.Control.BackgroundProperty,"Accent");updateNoticeTimer.Start();TryUpdateNotice();}}
                catch(OperationCanceledException){}catch{/* Offline startup must not interrupt image viewing. */}
            };
            updateNoticeTimer.Tick+=(_,_)=>TryUpdateNotice();Activated+=(_,_)=>Dispatcher.BeginInvoke(TryUpdateNotice);
        }
        Closed+=(_,_)=>{updateStopped=true;updateNoticeTimer.Stop();updateLifetime.Cancel();updateClient.Dispose();updateLifetime.Dispose();};
    }
    internal static bool CanNotifyUpdate(bool fullscreen,bool active,int ownedWindows,bool dialogOpen)=>!fullscreen&&active&&ownedWindows==0&&!dialogOpen;
    void TryUpdateNotice()
    {
        if(updateStopped||notifiedUpdate||availableUpdate==null||AboutOverlay.Visibility==Visibility.Visible||!CanNotifyUpdate(full,IsActive,OwnedWindows.Count,updateWindowOpen))return;
        notifiedUpdate=true;updateNoticeTimer.Stop();ShowUpdates(this,new RoutedEventArgs());
    }
    void SaveUpdatePreferences(){try{updatePreferences.Save(updateSettingsPath);}catch(Exception ex){Status(L10n.T("UpdateSettingsFailed")+" "+ex.Message);}}
    async void ShowUpdates(object sender,RoutedEventArgs e)
    {
        if(updateStopped||updateWindowOpen)return;Stop();updateWindowOpen=true;notifiedUpdate=true;updateNoticeTimer.Stop();
        try
        {
            var dialog=new UpdateWindow(updateClient,availableUpdate,updatePreferences.AutomaticChecks,value=>{updatePreferences=updatePreferences with{AutomaticChecks=value};SaveUpdatePreferences();}){Owner=this};
            if(dialog.ShowDialog()!=true||dialog.Download is not{} download)return;
            if(OwnedWindows.Cast<Window>().Any(w=>w.IsVisible)||UpdateInstaller.OtherInstances(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar),Environment.ProcessId)){Dialogs.Info(this,L10n.T("Updates"),L10n.T("UpdateOtherInstances"));return;}
            IsEnabled=false;Status(L10n.T("UpdatePreparing"));
            string plan=await UpdateInstaller.PrepareAsync(download,files.Count>0?files[index]:null,updateLifetime.Token);UpdateInstaller.Launch(plan);Close();
        }
        catch(OperationCanceledException){}catch(Exception ex){if(!updateStopped)Error(ex);}
        finally{if(!updateStopped){IsEnabled=true;updateWindowOpen=false;}}
    }
}

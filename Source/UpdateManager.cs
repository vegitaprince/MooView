using System.Diagnostics;
using System.IO;
using System.Text.Json;
namespace MooView;

internal sealed record PendingUpdate(string PlanPath,string Version,bool Portable);
internal sealed class UpdateManager:IDisposable
{
    readonly UpdateClient client;readonly Action<string> launchHelper;readonly CancellationTokenSource lifetime=new();CancellationTokenSource? operation;Task? staging;bool disposed;
    readonly string settingsPath,pendingPath,availablePath,dataRoot,downloadsRoot;readonly Version current;
    internal string DirectoryPath {get;}
    internal bool Portable {get;}
    internal UpdatePreferences Preferences {get;private set;}
    internal UpdateRelease? Available {get;private set;}
    internal string? PlanPath {get;private set;}
    internal string? ReadyVersion {get;private set;}
    internal bool Ready=>PlanPath!=null;
    internal bool Busy {get;private set;}
    internal double Progress {get;private set;}
    internal string State {get;private set;}="UpdateCurrent";
    internal string? Error {get;private set;}
    internal event Action? Changed;
    internal UpdateManager(string? directory=null,UpdateClient? updateClient=null,string? storage=null,bool? portable=null,Version? currentVersion=null,UpdateRelease? available=null,Action<string>? launcher=null)
    {
        DirectoryPath=UpdateEnvironment.Normalize(directory??AppContext.BaseDirectory);Portable=portable??UpdateEnvironment.IsPortable(DirectoryPath);client=updateClient??new UpdateClient();launchHelper=launcher??UpdateInstaller.Launch;dataRoot=storage??UpdateClient.DataRoot;downloadsRoot=Path.Combine(dataRoot,"Updates");current=currentVersion??UpdateClient.CurrentVersion;
        settingsPath=Path.Combine(dataRoot,"updates-"+UpdateEnvironment.Key(DirectoryPath)+".json");pendingPath=UpdateEnvironment.PendingPath(DirectoryPath,dataRoot);
        availablePath=Path.Combine(dataRoot,"available-"+UpdateEnvironment.Key(DirectoryPath)+".json");Available=available;
        if(Available==null)try{if(File.Exists(availablePath)&&new FileInfo(availablePath).Length<1024*1024){var cached=JsonSerializer.Deserialize<UpdateRelease>(File.ReadAllText(availablePath));if(cached?.Version>current&&cached.Portable==Portable)Available=cached;}}catch{}
        Preferences=UpdatePreferences.Load(File.Exists(settingsPath)?settingsPath:Path.Combine(dataRoot,"updates.json"));
        if(!Preferences.AutomaticChecks&&Preferences.DownloadOnExit)Preferences=Preferences with{DownloadOnExit=false};
        LoadPending();
    }
    void Notify(){if(!disposed)Changed?.Invoke();}
    internal void ReloadPreferences()
    {
        if(File.Exists(settingsPath))Preferences=UpdatePreferences.Load(settingsPath);
        if(Ready)State=Preferences.DownloadOnExit?"UpdateReadyExit":"UpdateReadyManual";
    }
    void LoadPending()
    {
        try
        {
            if(!File.Exists(pendingPath))return;var pending=JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(pendingPath));
            if(pending==null||!UpdateClient.TryVersion(pending.Version,out var version)||version<=current||pending.Portable!=Portable){File.Delete(pendingPath);return;}
            var plan=UpdateInstaller.ReadPlan(pending.PlanPath,downloadsRoot);
            if(!UpdateEnvironment.Normalize(plan.InstallDirectory).Equals(DirectoryPath,StringComparison.OrdinalIgnoreCase)||plan.Portable!=Portable||plan.Version!=pending.Version)throw new InvalidDataException("Pending update targets a different copy.");
            PlanPath=pending.PlanPath;ReadyVersion=pending.Version;State=Preferences.DownloadOnExit?"UpdateReadyExit":"UpdateReadyManual";
        }
        catch(Exception ex){State="UpdateFailed";Error=ex.Message;}
    }
    internal void SetPreferences(bool checks,bool download)
    {
        bool wasDownload=Preferences.DownloadOnExit;Preferences=Preferences with{AutomaticChecks=checks,DownloadOnExit=checks&&download};
        try{Preferences.Save(settingsPath);}catch(Exception ex){State="UpdateSettingsFailed";Error=ex.Message;}
        if(wasDownload&&!Preferences.DownloadOnExit&&Busy)Cancel();
        if(Ready)State=Preferences.DownloadOnExit?"UpdateReadyExit":"UpdateReadyManual";
        Notify();
    }
    internal async Task CheckAsync(bool force=false)
    {
        if(disposed||Busy)return;
        ReloadPreferences();
        if(!force&&!Preferences.ShouldCheck(DateTimeOffset.UtcNow)){if(Preferences.DownloadOnExit&&Available!=null&&!Ready)await StageAsync();return;}
        if(Ready){Notify();return;}
        Busy=true;Error=null;State="UpdateChecking";Notify();operation?.Dispose();operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        try
        {
            Preferences=Preferences with{LastCheckUtc=DateTimeOffset.UtcNow};Preferences.Save(settingsPath);
            Available=await client.CheckPackageAsync(current,operation.Token,Portable);State=Available==null?"UpdateCurrent":"UpdateAvailable";
            if(Available!=null)File.WriteAllText(availablePath,JsonSerializer.Serialize(Available));else if(File.Exists(availablePath))File.Delete(availablePath);
        }
        catch(OperationCanceledException){State="UpdateCancelled";}
        catch(Exception ex){State="UpdateFailed";Error=ex.Message;}
        finally{Busy=false;Notify();}
        if(State=="UpdateAvailable"&&Available!=null&&Preferences.DownloadOnExit&&!disposed)await StageAsync();
    }
    internal Task StageAsync()
    {
        if(disposed||Ready||Available==null)return Task.CompletedTask;
        if(staging is{IsCompleted:false})return staging;
        staging=StageCoreAsync();return staging;
    }
    async Task StageCoreAsync()
    {
        Busy=true;Error=null;Progress=0;State="UpdateDownloading";Notify();operation?.Dispose();operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);var token=operation.Token;
        try
        {
            var release=Available!;
            var download=await client.DownloadAsync(release,downloadsRoot,new Progress<double>(value=>{if(disposed)return;Progress=value;Notify();}),token);
            State="UpdatePreparing";Notify();
            string plan=await UpdateInstaller.PrepareAsync(download,null,token,downloadsRoot,false,DirectoryPath,Path.GetFileName(pendingPath));
            token.ThrowIfCancellationRequested();if(disposed)return;
            // Persist only after the package, portable manifest and helper are ready.
            var pending=new PendingUpdate(plan,release.Version.ToString(3),Portable);Directory.CreateDirectory(dataRoot);string temporary=pendingPath+"."+Guid.NewGuid().ToString("N")+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(pending));File.Move(temporary,pendingPath,true);
            PlanPath=plan;ReadyVersion=release.Version.ToString(3);State=Preferences.DownloadOnExit?"UpdateReadyExit":"UpdateReadyManual";
        }
        catch(OperationCanceledException){State="UpdateCancelled";}
        catch(Exception ex){State="UpdateFailed";Error=ex.Message;}
        finally{Busy=false;Notify();}
    }
    internal void Cancel()=>operation?.Cancel();
    internal bool LaunchPending(bool restart,string? image=null)
    {
        if(!Ready)LoadPending();if(PlanPath==null)return false;
        var plan=UpdateInstaller.ReadPlan(PlanPath,downloadsRoot);
        if(!UpdateEnvironment.Normalize(plan.InstallDirectory).Equals(DirectoryPath,StringComparison.OrdinalIgnoreCase)||plan.Portable!=Portable)throw new InvalidDataException("Wrong pending update target.");
        using var self=Process.GetCurrentProcess();plan=plan with{Restart=restart,Image=image,ProcessId=self.Id,ProcessStart=self.StartTime.ToUniversalTime().Ticks};
        File.WriteAllText(PlanPath,JsonSerializer.Serialize(plan));if(File.Exists(PlanPath+".defer"))File.Delete(PlanPath+".defer");launchHelper(PlanPath);return true;
    }
    internal static bool CanApplyOnExit(bool consent,bool ready,bool sessionEnding)=>consent&&ready&&!sessionEnding;
    internal void ApplyOnExit(bool sessionEnding)
    {
        ReloadPreferences();
        if(!Preferences.DownloadOnExit)return;
        // A different window of the same copy may have finished the download.
        if(!Ready)LoadPending();if(sessionEnding){if(PlanPath!=null)try{File.WriteAllText(PlanPath+".defer","Windows session ending");}catch{}return;}
        if(!CanApplyOnExit(Preferences.DownloadOnExit,Ready,sessionEnding))return;
        try{LaunchPending(false);}catch(Exception ex){try{File.WriteAllText(Path.Combine(dataRoot,"last-update-error-"+UpdateEnvironment.Key(DirectoryPath)+".txt"),ex.ToString());}catch{}}
    }
    public void Dispose(){if(disposed)return;disposed=true;lifetime.Cancel();operation?.Cancel();operation?.Dispose();lifetime.Dispose();client.Dispose();}
}

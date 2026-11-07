using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace MooView;

internal sealed record UpdatePlan(string Installer,string Sha256,string Version,string InstallDirectory,int ProcessId,long ProcessStart,string? Image,bool Portable=false,bool Restart=true,string? PayloadPath=null,string? PendingFileName=null);
internal static class UpdateInstaller
{
    internal static bool OtherInstances(string directory,int except)
    {
        string executable=Path.Combine(directory,"MooView.exe");
        foreach(var process in Process.GetProcessesByName("MooView"))using(process)
        {
            if(process.Id==except)continue;
            try{if(string.Equals(process.MainModule?.FileName,executable,StringComparison.OrdinalIgnoreCase))return true;}
            catch{ return true; }
        }
        return false;
    }
    internal static async Task VerifyAsync(string path,string expected,CancellationToken token)
    {
        await using var stream=File.OpenRead(path);var hash=await SHA256.HashDataAsync(stream,token);
        if(!System.Text.RegularExpressions.Regex.IsMatch(expected,@"^[a-fA-F0-9]{64}$")||!CryptographicOperations.FixedTimeEquals(hash,Convert.FromHexString(expected)))throw new InvalidDataException(L10n.T("UpdateHashFailed"));
    }
    static UpdatePlan ReadEnvelope(string path,string? downloadsRoot=null)
    {
        path=Path.GetFullPath(path);string folder=Path.GetDirectoryName(path)!,root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(downloadsRoot??UpdateClient.DownloadsRoot))+Path.DirectorySeparatorChar;
        if(!folder.StartsWith(root,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path)!="handoff.json"||!Guid.TryParseExact(Path.GetFileName(folder),"N",out _))throw new InvalidDataException("Invalid update handoff.");
        return JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(path))??throw new InvalidDataException("Missing update plan.");
    }
    internal static UpdatePlan ReadPlan(string path,string? downloadsRoot=null)
    {
        var plan=ReadEnvelope(path,downloadsRoot);string folder=Path.GetDirectoryName(Path.GetFullPath(path))!;
        if(!UpdateClient.TryVersion(plan.Version,out var version)||Path.GetFullPath(plan.Installer)!=Path.Combine(folder,UpdateClient.PackageName(version,plan.Portable))||!Path.IsPathFullyQualified(plan.InstallDirectory)||!File.Exists(Path.Combine(plan.InstallDirectory,"MooView.exe")))throw new InvalidDataException("Invalid update target.");
        var product=FileVersionInfo.GetVersionInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")).ProductName;
        if(product!="MooView")throw new InvalidDataException("Unexpected update target.");
        if(plan.Portable&&(plan.PayloadPath==null||Path.GetDirectoryName(Path.GetFullPath(plan.PayloadPath))!=folder||!System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(plan.PayloadPath),@"^payload-[a-f0-9]{32}$")||!Directory.Exists(plan.PayloadPath)))throw new InvalidDataException("Invalid staged portable payload.");
        if(plan.PendingFileName!=null&&plan.PendingFileName!="pending-"+UpdateEnvironment.Key(plan.InstallDirectory)+".json")throw new InvalidDataException("Invalid pending-update state.");
        return plan;
    }
    internal static async Task<string> PrepareAsync(DownloadedUpdate update,string? image,CancellationToken token,string? downloadsRoot=null,bool restart=true,string? targetDirectory=null,string? pendingFileName=null)
    {
        await VerifyAsync(update.FilePath,update.Release.Sha256,token);
        string? payload=update.Release.Portable?await PortableUpdate.StageAsync(update.FilePath,update.Release.Version.ToString(3),token):null;
        targetDirectory=UpdateEnvironment.Normalize(targetDirectory??AppContext.BaseDirectory);
        if(update.Release.Portable){PortableUpdate.Target(targetDirectory,"MooView.exe");string probe=Path.Combine(targetDirectory,".mooview-write-"+Guid.NewGuid().ToString("N"));using var access=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,1,FileOptions.DeleteOnClose);}
        string folder=Path.GetDirectoryName(update.FilePath)!,helper=Path.Combine(folder,"helper");Directory.CreateDirectory(helper);
        await Task.Run(()=>
        {
            foreach(string file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            {
                token.ThrowIfCancellationRequested();string name=Path.GetFileName(file);
                if(Path.GetExtension(name).Equals(".dll",StringComparison.OrdinalIgnoreCase)||name is "MooView.exe" or "MooView.deps.json" or "MooView.runtimeconfig.json")File.Copy(file,Path.Combine(helper,name),true);
            }
        },token);
        using var self=Process.GetCurrentProcess();var plan=new UpdatePlan(update.FilePath,update.Release.Sha256,update.Release.Version.ToString(3),targetDirectory,self.Id,self.StartTime.ToUniversalTime().Ticks,image,update.Release.Portable,restart,payload,pendingFileName);
        string path=Path.Combine(folder,"handoff.json");File.WriteAllText(path,JsonSerializer.Serialize(plan));ReadPlan(path,downloadsRoot);return path;
    }
    internal static void Launch(string plan)
    {
        string helper=Path.Combine(Path.GetDirectoryName(plan)!,"helper","MooView.exe");
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=Path.GetDirectoryName(helper)!};start.ArgumentList.Add("--apply-update");start.ArgumentList.Add(plan);
        using var process=Process.Start(start)??throw new IOException("Unable to start update helper.");
    }
    internal static async Task ApplyAsync(string path)
    {
        var plan=ReadPlan(path);
        Directory.CreateDirectory(UpdateClient.DataRoot);FileStream lease;
        try{lease=new FileStream(Path.Combine(UpdateClient.DataRoot,"update-lock-"+UpdateEnvironment.Key(plan.InstallDirectory)+".lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){return;}
        try
        {

            await VerifyAsync(plan.Installer,plan.Sha256,CancellationToken.None);
            using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
            try
            {
                try{using var old=Process.GetProcessById(plan.ProcessId);if(old.StartTime.ToUniversalTime().Ticks==plan.ProcessStart)await old.WaitForExitAsync(timeout.Token);}catch(ArgumentException){}
                while(OtherInstances(plan.InstallDirectory,Environment.ProcessId))await Task.Delay(200,timeout.Token);
            }
            catch(OperationCanceledException){return;} // A remaining viewer/editor keeps its files; retry on a later exit.
            // Another window can finish a newer package, request a manual
            // restart, or revoke consent while this helper waits for exit.
            if(plan.PendingFileName!=null)
            {
                string state=Path.Combine(UpdateClient.DataRoot,plan.PendingFileName);
                if(File.Exists(state))
                {
                    var pending=JsonSerializer.Deserialize<PendingUpdate>(File.ReadAllText(state));
                    if(pending!=null)
                    {
                        var newer=ReadPlan(pending.PlanPath);
                        if(UpdateEnvironment.Normalize(newer.InstallDirectory).Equals(UpdateEnvironment.Normalize(plan.InstallDirectory),StringComparison.OrdinalIgnoreCase)&&newer.Portable==plan.Portable&&Version.Parse(newer.Version)>=Version.Parse(plan.Version))
                        {plan=newer;path=pending.PlanPath;await VerifyAsync(plan.Installer,plan.Sha256,CancellationToken.None);}
                    }
                }
            }
            if(File.Exists(path+".defer"))return;
            if(!plan.Restart&&!UpdatePreferences.Load(Path.Combine(UpdateClient.DataRoot,"updates-"+UpdateEnvironment.Key(plan.InstallDirectory)+".json")).DownloadOnExit)return;
            string existing=FileVersionInfo.GetVersionInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")).ProductVersion?.Split('+')[0]??"";
            if(UpdateClient.TryVersion(existing,out var current)&&current>=Version.Parse(plan.Version)){ClearPending(plan);return;}
            try
            {
                if(plan.Portable)await PortableUpdate.ApplyAsync(plan.PayloadPath!,plan.InstallDirectory,plan.Version,CancellationToken.None);
                else
                {
                    var start=new ProcessStartInfo(plan.Installer){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(plan.Installer)!};
                    foreach(var arg in new[]{"/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/NOCLOSEAPPLICATIONS","/DIR="+plan.InstallDirectory,"/LOG="+Path.Combine(Path.GetDirectoryName(path)!,"install.log")})start.ArgumentList.Add(arg);
                    using var installer=Process.Start(start)??throw new IOException("Unable to start installer.");await installer.WaitForExitAsync();
                    if(installer.ExitCode!=0)throw new IOException("Installer exit code: "+installer.ExitCode);
                }
                string installed=FileVersionInfo.GetVersionInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")).ProductVersion?.Split('+')[0]??"";
                if(!UpdateClient.TryVersion(installed,out var version)||version<Version.Parse(plan.Version))throw new IOException("Installed version verification failed.");
                ClearPending(plan);
            }
            finally
            {
                if(plan.Restart)
                {
                    var restart=new ProcessStartInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")){UseShellExecute=true,WorkingDirectory=plan.InstallDirectory};
                    if(plan.Image!=null&&File.Exists(plan.Image))restart.ArgumentList.Add(plan.Image);Process.Start(restart)?.Dispose();
                }
            }
        }
        finally{lease.Dispose();}
    }
    static void ClearPending(UpdatePlan plan)
    {
        if(plan.PendingFileName==null)return;
        string state=Path.Combine(UpdateClient.DataRoot,plan.PendingFileName);
        if(File.Exists(state))File.Delete(state);
    }
    internal static async Task RunHelperAsync(string path)
    {
        UpdatePlan? plan=null;
        try{plan=ReadPlan(path);await ApplyAsync(path);}
        catch(Exception ex)
        {
            if(plan==null)try{plan=ReadEnvelope(path);}catch{}
            if(plan!=null)
            {
                try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(path)!,"update-error.txt"),DateTimeOffset.Now+"\n"+ex);}catch{}
                if(!plan.Restart)return;
            }
            System.Windows.MessageBox.Show(ex.Message,"MooView update",System.Windows.MessageBoxButton.OK,System.Windows.MessageBoxImage.Warning);
        }
    }
}

using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
namespace MooView;

internal sealed record UpdatePlan(string Installer,string Sha256,string Version,string InstallDirectory,int ProcessId,long ProcessStart,string? Image);
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
    internal static UpdatePlan ReadPlan(string path,string? downloadsRoot=null)
    {
        path=Path.GetFullPath(path);string folder=Path.GetDirectoryName(path)!,root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(downloadsRoot??UpdateClient.DownloadsRoot))+Path.DirectorySeparatorChar;
        if(!folder.StartsWith(root,StringComparison.OrdinalIgnoreCase)||Path.GetFileName(path)!="handoff.json"||!Guid.TryParseExact(Path.GetFileName(folder),"N",out _))throw new InvalidDataException("Invalid update handoff.");
        var plan=JsonSerializer.Deserialize<UpdatePlan>(File.ReadAllText(path))??throw new InvalidDataException("Missing update plan.");
        if(!UpdateClient.TryVersion(plan.Version,out var version)||Path.GetFullPath(plan.Installer)!=Path.Combine(folder,$"MooView-Setup-{version.ToString(3)}-x64.exe")||!Path.IsPathFullyQualified(plan.InstallDirectory)||!File.Exists(Path.Combine(plan.InstallDirectory,"MooView.exe")))throw new InvalidDataException("Invalid update target.");
        var product=FileVersionInfo.GetVersionInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")).ProductName;
        if(product!="MooView")throw new InvalidDataException("Unexpected update target.");
        return plan;
    }
    internal static async Task<string> PrepareAsync(DownloadedUpdate update,string? image,CancellationToken token,string? downloadsRoot=null)
    {
        await VerifyAsync(update.FilePath,update.Release.Sha256,token);
        string folder=Path.GetDirectoryName(update.FilePath)!,helper=Path.Combine(folder,"helper");Directory.CreateDirectory(helper);
        await Task.Run(()=>
        {
            foreach(string file in Directory.EnumerateFiles(AppContext.BaseDirectory))
            {
                token.ThrowIfCancellationRequested();string name=Path.GetFileName(file);
                if(Path.GetExtension(name).Equals(".dll",StringComparison.OrdinalIgnoreCase)||name is "MooView.exe" or "MooView.deps.json" or "MooView.runtimeconfig.json")File.Copy(file,Path.Combine(helper,name),true);
            }
        },token);
        using var self=Process.GetCurrentProcess();var plan=new UpdatePlan(update.FilePath,update.Release.Sha256,update.Release.Version.ToString(3),Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),self.Id,self.StartTime.ToUniversalTime().Ticks,image);
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
        var plan=ReadPlan(path);await VerifyAsync(plan.Installer,plan.Sha256,CancellationToken.None);
        try
        {
            using var old=Process.GetProcessById(plan.ProcessId);
            if(old.StartTime.ToUniversalTime().Ticks==plan.ProcessStart)
            {
                using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(60));await old.WaitForExitAsync(timeout.Token);
            }
        }
        catch(ArgumentException){} // The original process has already exited.
        if(OtherInstances(plan.InstallDirectory,Environment.ProcessId))throw new IOException(L10n.T("UpdateOtherInstances"));
        var start=new ProcessStartInfo(plan.Installer){UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(plan.Installer)!};
        foreach(var arg in new[]{"/VERYSILENT","/SUPPRESSMSGBOXES","/NORESTART","/SP-","/NOCLOSEAPPLICATIONS","/DIR="+plan.InstallDirectory,"/LOG="+Path.Combine(Path.GetDirectoryName(path)!,"install.log")})start.ArgumentList.Add(arg);
        try
        {
            using var installer=Process.Start(start)??throw new IOException("Unable to start installer.");await installer.WaitForExitAsync();
            if(installer.ExitCode!=0)throw new IOException("Installer exit code: "+installer.ExitCode);
            string installed=FileVersionInfo.GetVersionInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")).ProductVersion?.Split('+')[0]??"";
            if(!UpdateClient.TryVersion(installed,out var version)||version<Version.Parse(plan.Version))throw new IOException("Installed version verification failed.");
        }
        finally
        {
            var restart=new ProcessStartInfo(Path.Combine(plan.InstallDirectory,"MooView.exe")){UseShellExecute=true,WorkingDirectory=plan.InstallDirectory};
            if(plan.Image!=null&&File.Exists(plan.Image))restart.ArgumentList.Add(plan.Image);Process.Start(restart)?.Dispose();
        }
    }
}

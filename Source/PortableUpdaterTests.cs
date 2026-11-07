using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class PortableUpdaterTests
{
    sealed class Handler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}
    internal static async Task Run(string dir,List<string> log)
    {
        void Check(bool value,string label){if(!value)throw new Exception(label);log.Add("PASS: "+label);}
        async Task Reject(Func<Task> action,string label){bool rejected=false;try{await action();}catch(Exception ex)when(ex is InvalidDataException or IOException or OperationCanceledException){rejected=true;}Check(rejected,label);}
        string root=Path.Combine(dir,"portable-update-tests");Directory.CreateDirectory(root);string version=UpdateClient.CurrentVersion.ToString(3),archive=Path.Combine(root,UpdateClient.PackageName(UpdateClient.CurrentVersion,true));
        PortableUpdate.CreatePackage(AppContext.BaseDirectory,archive);byte[] bytes=File.ReadAllBytes(archive);string digest=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string Json(bool portable=true,string? hash=null)=>JsonSerializer.Serialize(new{draft=false,prerelease=false,tag_name="v"+version,body="Portable update notes",assets=new[]{new{name=UpdateClient.PackageName(UpdateClient.CurrentVersion,portable),state="uploaded",size=bytes.LongLength,digest="sha256:"+(hash??digest),browser_download_url=UpdateClient.Repository+"/releases/download/v"+version+"/"+UpdateClient.PackageName(UpdateClient.CurrentVersion,portable)}}});
        var prior=new Version(1,8,0);var release=UpdateClient.Parse(Json(),prior,true)!;
        Check(release.Portable&&release.Name.EndsWith(".zip"),"portable mode selects the exact portable ZIP asset");
        Check(!UpdateClient.Parse(Json(false),prior,false)!.Portable,"installed mode continues to select the installer asset");
        await Reject(()=>Task.FromResult(UpdateClient.Parse(Json(false),prior,true)),"portable mode never falls back to the installer");
        Check(!new UpdatePreferences().DownloadOnExit,"automatic download and apply-on-exit is opt-in");
        Check(UpdateEnvironment.Key(root)==UpdateEnvironment.Key(root+Path.DirectorySeparatorChar)&&UpdateEnvironment.Key(root)!=UpdateEnvironment.Key(root+"other"),"pending state is scoped to each application folder");
        foreach(string path in new[]{"../MooView.exe","/MooView.exe","C:/MooView.exe","settings.json","photos/a.png","ko/../../MooView.dll","a.dll:stream","NUL.dll","foo.dll.","ko\\MooView.dll"})Check(!PortableUpdate.Allowed(path),"portable payload rejects unrelated or unsafe path "+path);
        string stage=await PortableUpdate.StageAsync(archive,version,CancellationToken.None);Check(File.Exists(Path.Combine(stage,"MooView.exe"))&&File.Exists(Path.Combine(stage,PortableUpdate.ManifestName)),"portable archive extracts only verified manifest-owned files");
        string Fixture(string name)
        {
            string target=Path.Combine(root,name);Directory.CreateDirectory(target);
            foreach(string file in new[]{"MooView.exe","MooView.dll","MooView.deps.json","MooView.runtimeconfig.json"})File.Copy(Path.Combine(AppContext.BaseDirectory,file),Path.Combine(target,file),true);
            File.WriteAllText(Path.Combine(target,UpdateEnvironment.PortableMarker),"portable");File.WriteAllText(Path.Combine(target,"obsolete.dll"),"old owned runtime");
            var old=new PayloadManifest("1.8.0",new[]{new PayloadFile("obsolete.dll",17,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,"obsolete.dll")))))});File.WriteAllText(Path.Combine(target,PortableUpdate.ManifestName),JsonSerializer.Serialize(old));
            File.WriteAllText(Path.Combine(target,"photo.png"),"user image must stay");File.WriteAllText(Path.Combine(target,"settings.json"),"user settings must stay");Directory.CreateDirectory(Path.Combine(target,"photos"));File.WriteAllText(Path.Combine(target,"photos","keep.txt"),"nested user data");return target;
        }
        string target=Fixture("target");Check(UpdateEnvironment.IsPortable(target),"portable marker identifies a portable copy without registry changes");
        var snapshot=Directory.EnumerateFiles(target,"*",SearchOption.AllDirectories).ToDictionary(file=>Path.GetRelativePath(target,file),file=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))));
        await Reject(()=>PortableUpdate.ApplyAsync(stage,target,version,CancellationToken.None,n=>{if(n==2)throw new IOException("Injected replacement failure");}),"portable replacement failure triggers rollback");
        Check(snapshot.All(pair=>File.Exists(Path.Combine(target,pair.Key))&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(target,pair.Key))))==pair.Value),"rollback restores all original program and user files");
        await PortableUpdate.ApplyAsync(stage,target,version,CancellationToken.None);
        Check(!File.Exists(Path.Combine(target,"obsolete.dll"))&&File.Exists(Path.Combine(target,UpdateEnvironment.PortableMarker)),"portable update removes unchanged obsolete owned files and remains portable");
        Check(File.ReadAllText(Path.Combine(target,"photo.png"))=="user image must stay"&&File.ReadAllText(Path.Combine(target,"settings.json"))=="user settings must stay"&&File.ReadAllText(Path.Combine(target,"photos","keep.txt"))=="nested user data","portable update preserves images settings and unrelated nested files");
        Check(!File.Exists(Path.Combine(target,"unins000.exe"))&&!Directory.EnumerateDirectories(target,".mooview-update-*").Any(),"portable update leaves no installer or transaction debris");
        string Mutate(string name,Action<ZipArchive> mutate){string file=Path.Combine(root,name+".zip");File.Copy(archive,file,true);using(var zip=ZipFile.Open(file,ZipArchiveMode.Update))mutate(zip);return file;}
        string traversal=Mutate("traversal",zip=>{using var writer=new StreamWriter(zip.CreateEntry("../photo.png").Open());writer.Write("unsafe");});await Reject(()=>PortableUpdate.StageAsync(traversal,version,CancellationToken.None),"ZIP traversal is rejected before replacing files");
        string duplicate=Mutate("duplicate",zip=>{using var writer=new StreamWriter(zip.CreateEntry("mOOVIEW.dll").Open());writer.Write("duplicate");});await Reject(()=>PortableUpdate.StageAsync(duplicate,version,CancellationToken.None),"case-insensitive duplicate ZIP paths are rejected");
        string link=Mutate("symlink",zip=>{var entry=zip.CreateEntry("evil.dll");entry.ExternalAttributes=unchecked((int)0xA1FF0000);});await Reject(()=>PortableUpdate.StageAsync(link,version,CancellationToken.None),"ZIP symbolic link entries are rejected");
        await Reject(()=>PortableUpdate.StageAsync(archive,"99.0.0",CancellationToken.None),"archive manifest version must match the release");
        string tampered=Mutate("manifest-hash",zip=>{var entry=zip.GetEntry(PortableUpdate.ManifestName)!;PayloadManifest manifest;using(var reader=new StreamReader(entry.Open()))manifest=JsonSerializer.Deserialize<PayloadManifest>(reader.ReadToEnd())!;entry.Delete();var files=manifest.Files.ToArray();files[0]=files[0] with{Sha256=new string('0',64)};using var writer=new StreamWriter(zip.CreateEntry(PortableUpdate.ManifestName).Open());writer.Write(JsonSerializer.Serialize(manifest with{Files=files}));});await Reject(()=>PortableUpdate.StageAsync(tampered,version,CancellationToken.None),"individual payload file hashes are verified inside the ZIP");
        UpdateClient Client(string json,bool block=false)=>new(new Handler(async(request,token)=>
        {
            if(request.RequestUri!.AbsoluteUri==UpdateClient.LatestApi)return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)};
            if(block)await Task.Delay(Timeout.Infinite,token);
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)};
        }));
        string storage=Path.Combine(root,"manager-state");var launched=new List<UpdatePlan>();string? launchedPath=null;
        void Launch(string path){launchedPath=path;launched.Add(UpdateInstaller.ReadPlan(path,Path.Combine(storage,"Updates")));}
        using(var manager=new UpdateManager(target,Client(Json()),storage,true,prior,launcher:Launch))
        {
            manager.SetPreferences(true,true);await manager.CheckAsync(true);
            Check(manager.Ready&&manager.State=="UpdateReadyExit"&&launched.Count==0,"opt-in automatically downloads verifies and stages without updating a running app");
            Check(UpdateInstaller.ReadPlan(manager.PlanPath!,Path.Combine(storage,"Updates")).Portable,"staged portable handoff cannot invoke an installer");
            manager.ApplyOnExit(true);Check(launched.Count==0,"Windows shutdown leaves the verified update staged for a later normal exit");
            manager.SetPreferences(true,false);manager.ApplyOnExit(false);Check(launched.Count==0,"turning off automatic download also disables automatic exit application");
            manager.SetPreferences(true,true);manager.ApplyOnExit(false);Check(launched.Count==1&&!launched[0].Restart&&launched[0].Portable,"normal opted-in exit launches portable apply with no restart");
            manager.LaunchPending(true,Path.Combine(target,"photo.png"));Check(launched.Count==2&&launched[1].Restart,"manual update-and-restart remains available");
            using(var resumed=new UpdateManager(target,Client(Json()),storage,true,prior,launcher:Launch))Check(resumed.Ready&&resumed.Preferences.DownloadOnExit,"verified pending update and opt-in survive a restart");
            using(var other=new UpdateManager(Fixture("other-target"),Client(Json()),storage,true,prior,launcher:Launch))Check(!other.Ready&&!other.Preferences.DownloadOnExit,"a different portable folder cannot inherit another copy's pending update or consent");
            using(var second=new UpdateManager(target,Client(Json()),storage,true,prior,launcher:Launch))second.SetPreferences(true,false);
            manager.ApplyOnExit(false);Check(launched.Count==2,"exit reloads the latest consent changed by another window");
            bool english=L10n.Instance.English;
            foreach(bool en in new[]{false,true})
            {
                L10n.Instance.English=en;var dialog=new UpdateWindow(manager);var content=(FrameworkElement)dialog.Content;content.Measure(new Size(542,700));content.Arrange(new Rect(0,0,542,content.DesiredSize.Height));content.UpdateLayout();
                Check(content.DesiredSize.Height<700,"portable auto-update settings fit in "+(en?"English":"Korean"));var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(dialog.Background,null,new Rect(0,0,590,700));var image=new RenderTargetBitmap(590,700,96,96,PixelFormats.Pbgra32);image.Render(background);image.Render(content);Imaging.Save(image,Path.Combine(AppContext.BaseDirectory,"preview-portable-update-"+(en?"en":"ko")+".png"));dialog.Close();
            }
            L10n.Instance.English=english;
        }
        Check(!UpdateManager.CanApplyOnExit(false,true,false)&&!UpdateManager.CanApplyOnExit(true,false,false)&&!UpdateManager.CanApplyOnExit(true,true,true)&&UpdateManager.CanApplyOnExit(true,true,false),"exit application requires explicit consent a complete update and normal shutdown");
        using(var failed=new UpdateManager(target,Client(Json(hash:new string('0',64))),Path.Combine(root,"failed-state"),true,prior,launcher:_=>throw new Exception("Unexpected helper")))
        {failed.SetPreferences(true,true);await failed.CheckAsync(true);Check(!failed.Ready&&failed.Error!=null,"invalid downloaded ZIP never becomes pending");failed.ApplyOnExit(false);}
        using(var cancelled=new UpdateManager(target,Client(Json(),true),Path.Combine(root,"cancel-state"),true,prior,launcher:_=>throw new Exception("Unexpected helper")))
        {cancelled.SetPreferences(true,true);var work=cancelled.CheckAsync(true);await Task.Delay(40);cancelled.Cancel();await work;Check(!cancelled.Ready&&cancelled.State=="UpdateCancelled","cancelled automatic downloads cannot apply on exit");cancelled.ApplyOnExit(false);}
        Check(launchedPath!=null,"validated updater handoff is concrete and reviewable");
    }
}

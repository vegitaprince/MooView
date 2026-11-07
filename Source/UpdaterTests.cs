using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace MooView;

internal static class UpdaterTests
{
    sealed class FakeHandler(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>send(request,token);}
    internal static async Task Run(string dir,List<string> log)
    {
        void Check(bool ok,string label){if(!ok)throw new Exception(label);log.Add("PASS: "+label);}
        async Task Reject(Func<Task> action,string label){bool failed=false;try{await action();}catch(Exception ex)when(ex is InvalidDataException or OperationCanceledException or HttpRequestException){failed=true;}Check(failed,label);}
        string root=Path.Combine(dir,"updates-tests");Directory.CreateDirectory(root);byte[] bytes=System.Text.Encoding.UTF8.GetBytes("MooView deterministic download test");string hash=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        string Json(string tag="v9.8.7",bool draft=false,bool prerelease=false,string? digest=null,string? url=null,long? size=null,string? name=null)=>JsonSerializer.Serialize(new{tag_name=tag,draft,prerelease,body="Release notes\n수정 내용",assets=new[]{new{name=name??"MooView-Setup-9.8.7-x64.exe",state="uploaded",size=size??bytes.Length,digest=digest??"sha256:"+hash,browser_download_url=url??UpdateClient.Repository+"/releases/download/v9.8.7/MooView-Setup-9.8.7-x64.exe"}}});
        var current=new Version(1,6,0);var release=UpdateClient.Parse(Json(),current)!;
        Check(release.Version==new Version(9,8,7)&&release.Sha256==hash&&release.Notes.Contains("수정"),"updater parses newer stable release and asset SHA-256");
        Check(UpdateClient.Parse(Json("v1.6.0"),current)==null&&UpdateClient.Parse(Json("v1.5.0"),current)==null,"equal and older releases never downgrade");
        Check(UpdateClient.Parse(Json(draft:true),current)==null&&UpdateClient.Parse(Json(prerelease:true),current)==null&&UpdateClient.Parse(Json("v2.0.0-beta.1"),current)==null,"drafts prereleases and nonstable tags ignored");
        Check(!UpdateClient.TryVersion("v1.06.0",out _)&&!UpdateClient.TryVersion("v1.2.3.4",out _)&&UpdateClient.TryVersion("1.6.0",out _),"strict three-part version parsing");
        await Reject(()=>Task.FromResult(UpdateClient.Parse(Json(digest:""),current)),"release without digest rejected");
        await Reject(()=>Task.FromResult(UpdateClient.Parse(Json(url:"https://example.com/installer.exe"),current)),"external installer URL rejected");
        await Reject(()=>Task.FromResult(UpdateClient.Parse(Json(size:UpdateClient.MaximumInstallerSize+1),current)),"oversized release rejected before download");
        await Reject(()=>Task.FromResult(UpdateClient.Parse(Json(name:"random.exe"),current)),"unrelated release assets cannot be installed");
        Check(!UpdateClient.TrustedDownloadUri(new("http://github.com/file"))&&!UpdateClient.TrustedDownloadUri(new("https://github.com.evil.test/file"))&&!UpdateClient.TrustedDownloadUri(new("https://a@github.com/file"))&&!UpdateClient.TrustedDownloadUri(new("https://github.com:444/file")),"redirect trust rejects HTTP lookalikes credentials and custom ports");
        using(var client=new UpdateClient(new FakeHandler((request,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Json())}))))Check((await client.CheckAsync(current,CancellationToken.None))?.Version==release.Version,"HTTP release check integrated with version parser");
        using(var client=new UpdateClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)))))Check(await client.CheckAsync(current,CancellationToken.None)==null,"empty release repository handled without startup error");
        using(var client=new UpdateClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)))))await Reject(()=>client.CheckAsync(current,CancellationToken.None),"API rate or network errors reported for manual checks");
        int hops=0;using(var client=new UpdateClient(new FakeHandler((_,_)=>
        {
            hops++;if(hops==1){var response=new HttpResponseMessage(HttpStatusCode.Found);response.Headers.Location=new Uri("https://release-assets.githubusercontent.com/test");return Task.FromResult(response);}return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
        })))
        {
            var download=await client.DownloadAsync(release,root,null,CancellationToken.None);Check(hops==2&&File.ReadAllBytes(download.FilePath).SequenceEqual(bytes),"download follows trusted GitHub redirect and verifies bytes");await UpdateInstaller.VerifyAsync(download.FilePath,hash,CancellationToken.None);Check(true,"installer hash rechecked before handoff");
            string handoff=await UpdateInstaller.PrepareAsync(download,null,CancellationToken.None,root);var helper=Path.Combine(Path.GetDirectoryName(handoff)!,"helper","MooView.exe");
            Check(UpdateInstaller.ReadPlan(handoff,root).ProcessId==Environment.ProcessId&&File.Exists(helper),"verified download prepares independent runtime and parent process handoff");
            using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper,"--self-test"){UseShellExecute=false,CreateNoWindow=true})!)
            {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));await process.WaitForExitAsync(timeout.Token);Check(process.ExitCode==0&&File.Exists(Path.Combine(Path.GetDirectoryName(helper)!,"self-test.txt")),"copied helper boots and runs with bundled dependencies");}
            File.AppendAllText(download.FilePath,"tamper");await Reject(()=>UpdateInstaller.VerifyAsync(download.FilePath,hash,CancellationToken.None),"tampered installer rejected at installation boundary");
        }
        using(var client=new UpdateClient(new FakeHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)}))))
        {
            await Reject(()=>client.DownloadAsync(release with{Sha256=new string('0',64)},root,null,CancellationToken.None),"corrupted download never becomes executable installer");
            await Reject(()=>client.DownloadAsync(release with{Size=bytes.Length+1},root,null,CancellationToken.None),"incorrect Content-Length rejected");
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();await Reject(()=>client.DownloadAsync(release,root,null,cancelled.Token),"cancelled download never installs");
        }
        using(var client=new UpdateClient(new FakeHandler((_,_)=>{var response=new HttpResponseMessage(HttpStatusCode.Redirect);response.Headers.Location=new Uri("https://example.com/untrusted.exe");return Task.FromResult(response);})))await Reject(()=>client.DownloadAsync(release,root,null,CancellationToken.None),"download cannot redirect outside GitHub hosts");
        Check(!Directory.EnumerateFiles(root,"*.partial",SearchOption.AllDirectories).Any(),"failed and cancelled downloads remove partial files");
        var now=DateTimeOffset.UtcNow;Check(new UpdatePreferences().ShouldCheck(now)&&!new UpdatePreferences(false).ShouldCheck(now)&&!new UpdatePreferences(true,now.AddHours(-1)).ShouldCheck(now)&&new UpdatePreferences(true,now.AddHours(-13)).ShouldCheck(now),"automatic checks obey off switch and 12 hour interval");
        string prefs=Path.Combine(root,"preferences.json");new UpdatePreferences(false,now).Save(prefs);Check(!UpdatePreferences.Load(prefs).AutomaticChecks&&UpdatePreferences.Load(prefs).LastCheckUtc==now,"update preferences persist independently of viewer settings");File.WriteAllText(prefs,"broken");Check(UpdatePreferences.Load(prefs).AutomaticChecks,"invalid preference file recovers default settings");
        Check(!MainWindow.CanNotifyUpdate(true,true,0,false)&&!MainWindow.CanNotifyUpdate(false,false,0,false)&&!MainWindow.CanNotifyUpdate(false,true,1,false)&&!MainWindow.CanNotifyUpdate(false,true,0,true)&&MainWindow.CanNotifyUpdate(false,true,0,false),"automatic notices defer during fullscreen inactivity editing or existing dialogs");
        string folder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string planPath=Path.Combine(folder,"handoff.json");var plan=new UpdatePlan(Path.Combine(folder,release.Name),hash,"9.8.7",Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),Environment.ProcessId,0,null);File.WriteAllText(planPath,JsonSerializer.Serialize(plan));Check(UpdateInstaller.ReadPlan(planPath,root)==plan,"handoff validates private folder installer name and MooView target");
        File.WriteAllText(planPath,JsonSerializer.Serialize(plan with{Installer=Path.Combine(root,"evil.exe")}));await Reject(()=>Task.FromResult(UpdateInstaller.ReadPlan(planPath,root)),"handoff cannot execute arbitrary installer paths");
        await Reject(()=>Task.FromResult(UpdateInstaller.ReadPlan(planPath,Path.Combine(root,"other"))),"handoff outside update folder rejected");
        bool english=L10n.Instance.English;
        foreach(bool en in new[]{false,true})
        {
            L10n.Instance.English=en;using var uiClient=new UpdateClient();var dialog=new UpdateWindow(uiClient,release,false,_=>{});dialog.Measure(new Size(570,650));dialog.Arrange(new Rect(0,0,570,650));dialog.UpdateLayout();Check(dialog.Title==(en?"Updates":"업데이트"),"update window localized "+(en?"English":"Korean"));Check(dialog.Background==Application.Current.Resources["Bg"]&&dialog.Foreground==Application.Current.Resources["Text"],"update window follows application theme "+(en?"English":"Korean"));
            var panel=(FrameworkElement)dialog.Content;panel.Measure(new Size(522,600));panel.Arrange(new Rect(0,0,522,panel.DesiredSize.Height));panel.UpdateLayout();var background=new DrawingVisual();using(var dc=background.RenderOpen())dc.DrawRectangle(dialog.Background,null,new Rect(0,0,570,650));var image=new RenderTargetBitmap(570,650,96,96,PixelFormats.Pbgra32);image.Render(background);image.Render(panel);Imaging.Save(image,Path.Combine(AppContext.BaseDirectory,"preview-update-"+(en?"en":"ko")+".png"));dialog.Close();
        }
        L10n.Instance.English=english;
    }
}

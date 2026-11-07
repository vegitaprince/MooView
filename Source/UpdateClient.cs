using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace MooView;

internal sealed record UpdateRelease(Version Version,string Tag,string Notes,Uri Page,Uri Installer,string Name,long Size,string Sha256);
internal sealed record DownloadedUpdate(UpdateRelease Release,string FilePath);
internal sealed record UpdatePreferences(bool AutomaticChecks=true,DateTimeOffset? LastCheckUtc=null)
{
    internal bool ShouldCheck(DateTimeOffset now)=>AutomaticChecks&&(LastCheckUtc==null||now-LastCheckUtc>=TimeSpan.FromHours(12)||LastCheckUtc>now);
    internal static UpdatePreferences Load(string path){try{return JsonSerializer.Deserialize<UpdatePreferences>(File.ReadAllText(path))??new();}catch{return new();}}
    internal void Save(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(this));File.Move(temp,path,true);}
}
internal sealed class UpdateClient:IDisposable
{
    internal const string Repository="https://github.com/vegitaprince/MooView";
    internal const string LatestApi="https://api.github.com/repos/vegitaprince/MooView/releases/latest";
    internal const long MaximumInstallerSize=512L*1024*1024;
    readonly HttpClient http;
    internal static Version CurrentVersion {get{var v=Assembly.GetExecutingAssembly().GetName().Version!;return new(v.Major,v.Minor,Math.Max(0,v.Build));}}
    internal static string DataRoot=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MooView");
    internal static string DownloadsRoot=>Path.Combine(DataRoot,"Updates");
    internal UpdateClient(HttpMessageHandler? handler=null)
    {
        http=new HttpClient(handler??new HttpClientHandler{AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("MooView/"+CurrentVersion.ToString(3));http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");http.DefaultRequestHeaders.Add("X-GitHub-Api-Version","2022-11-28");
    }
    internal static bool TryVersion(string text,out Version version)
    {
        version=new(0,0,0);if(!Regex.IsMatch(text,@"^v?(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$"))return false;
        return Version.TryParse(text.TrimStart('v'),out version!);
    }
    internal static UpdateRelease? Parse(string json,Version current)
    {
        using var document=JsonDocument.Parse(json);var root=document.RootElement;
        if(root.GetProperty("draft").GetBoolean()||root.GetProperty("prerelease").GetBoolean())return null;
        string tag=root.GetProperty("tag_name").GetString()??"";if(!TryVersion(tag,out var version))return null;
        if(version<=new Version(current.Major,current.Minor,Math.Max(0,current.Build)))return null;
        string name=$"MooView-Setup-{version.ToString(3)}-x64.exe";
        string expected=Repository+"/releases/download/"+tag+"/"+name;
        foreach(var asset in root.GetProperty("assets").EnumerateArray())
        {
            if(asset.GetProperty("name").GetString()!=name)continue;
            if(asset.TryGetProperty("state",out var state)&&state.GetString()!="uploaded")continue;
            string address=asset.GetProperty("browser_download_url").GetString()??"";
            string digest=asset.TryGetProperty("digest",out var value)?value.GetString()??"":"";
            long size=asset.GetProperty("size").GetInt64();
            if(address!=expected||!Regex.IsMatch(digest,@"^sha256:[a-fA-F0-9]{64}$")||size<1||size>MaximumInstallerSize)throw new InvalidDataException(L10n.T("UpdateInvalid"));
            string notes=root.TryGetProperty("body",out var body)?body.GetString()??"":"";
            return new(version,tag,notes[..Math.Min(notes.Length,6000)],new Uri(Repository+"/releases/tag/"+tag),new Uri(address),name,size,digest[7..].ToLowerInvariant());
        }
        throw new InvalidDataException(L10n.T("UpdateNotReady"));
    }
    internal async Task<UpdateRelease?> CheckAsync(Version current,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response=await http.GetAsync(LatestApi,HttpCompletionOption.ResponseHeadersRead,timeout.Token);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;
        response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>1024*1024)throw new InvalidDataException(L10n.T("UpdateInvalid"));
        using var stream=await response.Content.ReadAsStreamAsync(timeout.Token);using var buffer=new MemoryStream();
        byte[] bytes=new byte[16384];int read;while((read=await stream.ReadAsync(bytes,timeout.Token))>0){if(buffer.Length+read>1024*1024)throw new InvalidDataException(L10n.T("UpdateInvalid"));await buffer.WriteAsync(bytes.AsMemory(0,read),timeout.Token);}
        return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()),current);
    }
    internal static bool TrustedDownloadUri(Uri uri)=>uri.Scheme=="https"&&uri.IsDefaultPort&&uri.UserInfo.Length==0&&new[]{"github.com","release-assets.githubusercontent.com","objects.githubusercontent.com","github-releases.githubusercontent.com"}.Contains(uri.Host,StringComparer.OrdinalIgnoreCase);
    async Task<HttpResponseMessage> GetInstallerAsync(Uri uri,CancellationToken token)
    {
        for(int i=0;i<6;i++)
        {
            if(!TrustedDownloadUri(uri))throw new InvalidDataException(L10n.T("UpdateInvalid"));
            var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,token);
            if((int)response.StatusCode is 301 or 302 or 303 or 307 or 308)
            {
                var location=response.Headers.Location;response.Dispose();if(location==null)throw new InvalidDataException(L10n.T("UpdateInvalid"));uri=location.IsAbsoluteUri?location:new Uri(uri,location);continue;
            }
            try{response.EnsureSuccessStatusCode();return response;}catch{response.Dispose();throw;}
        }
        throw new InvalidDataException(L10n.T("UpdateInvalid"));
    }
    internal async Task<DownloadedUpdate> DownloadAsync(UpdateRelease release,string root,IProgress<double>? progress,CancellationToken token)
    {
        string expected=Repository+"/releases/download/"+release.Tag+"/"+release.Name;
        if(!TryVersion(release.Tag,out var parsed)||parsed!=release.Version||release.Name!=$"MooView-Setup-{parsed.ToString(3)}-x64.exe"||release.Installer.AbsoluteUri!=expected||!Regex.IsMatch(release.Sha256,@"^[a-fA-F0-9]{64}$")||release.Size<1||release.Size>MaximumInstallerSize)throw new InvalidDataException(L10n.T("UpdateInvalid"));
        string folder=Path.Combine(root,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string partial=Path.Combine(folder,"download.partial"),final=Path.Combine(folder,release.Name);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromMinutes(10));
        try
        {
            using var response=await GetInstallerAsync(release.Installer,timeout.Token);
            if(response.Content.Headers.ContentLength is long length&&length!=release.Size)throw new InvalidDataException(L10n.T("UpdateInvalid"));
            using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long total=0;
            await using(var input=await response.Content.ReadAsStreamAsync(timeout.Token))
            await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            {
                byte[] bytes=new byte[65536];int read;
                while((read=await input.ReadAsync(bytes,timeout.Token))>0){total+=read;if(total>release.Size)throw new InvalidDataException(L10n.T("UpdateInvalid"));hash.AppendData(bytes,0,read);await output.WriteAsync(bytes.AsMemory(0,read),timeout.Token);progress?.Report((double)total/release.Size);}
                await output.FlushAsync(timeout.Token);
            }
            if(total!=release.Size||!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),Convert.FromHexString(release.Sha256)))throw new InvalidDataException(L10n.T("UpdateHashFailed"));
            timeout.Token.ThrowIfCancellationRequested();File.Move(partial,final);progress?.Report(1);return new(release,final);
        }
        finally {if(File.Exists(partial))File.Delete(partial);}
    }
    public void Dispose()=>http.Dispose();
}

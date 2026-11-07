using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace MooView;

internal sealed record PayloadFile(string Path,long Size,string Sha256);
internal sealed record PayloadManifest(string Version,IReadOnlyList<PayloadFile> Files);
internal static class PortableUpdate
{
    internal const string ManifestName="MooView.files.json";
    const long ExpandedLimit=2L*1024*1024*1024;
    internal static void CreatePackage(string directory,string destination)
    {
        directory=UpdateEnvironment.Normalize(directory);File.WriteAllText(Path.Combine(directory,UpdateEnvironment.PortableMarker),"MooView portable\n");
        var candidates=Directory.EnumerateFiles(directory).Concat(Directory.EnumerateDirectories(directory).Where(child=>Allowed(Path.GetFileName(child)+"/probe.dll")).SelectMany(child=>Directory.EnumerateFiles(child)));
        var files=candidates.Select(path=>Path.GetRelativePath(directory,path).Replace('\\','/')).Where(Allowed).OrderBy(path=>path,StringComparer.Ordinal).Select(path=>{string full=Target(directory,path);return new PayloadFile(path,new FileInfo(full).Length,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(full))).ToLowerInvariant());}).ToArray();
        string json=JsonSerializer.Serialize(new PayloadManifest(UpdateClient.CurrentVersion.ToString(3),files));ReadManifest(json,UpdateClient.CurrentVersion.ToString(3));File.WriteAllText(Path.Combine(directory,ManifestName),json);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))foreach(string name in files.Select(file=>file.Path).Append(ManifestName))zip.CreateEntryFromFile(Target(directory,name),name,CompressionLevel.Optimal);File.Move(temporary,destination,true);}
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    internal static bool Allowed(string path)
    {
        if(string.IsNullOrEmpty(path)||path.Contains('\\')||path.StartsWith('/')||path.Split('/').Any(segment=>segment is "" or "." or ".."||segment.EndsWith('.')||segment.EndsWith(' ')||segment.IndexOfAny(Path.GetInvalidFileNameChars())>=0||Regex.IsMatch(segment,@"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(\.|$)",RegexOptions.IgnoreCase)))return false;
        var pieces=path.Split('/');
        if(pieces.Length==1)return path.EndsWith(".dll",StringComparison.OrdinalIgnoreCase)||path is "MooView.exe" or "createdump.exe" or "MooView.deps.json" or "MooView.runtimeconfig.json" or "MooView.portable";
        if(pieces.Length!=2||!pieces[1].EndsWith(".dll",StringComparison.OrdinalIgnoreCase))return false;
        try{return CultureInfo.GetCultureInfo(pieces[0]).Name.Length>0&&Regex.IsMatch(pieces[0],@"^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8}){0,2}$");}catch{return false;}
    }
    internal static string Target(string root,string relative)
    {
        if(relative!=ManifestName&&!Allowed(relative))throw new InvalidDataException("Invalid payload path: "+relative);
        root=UpdateEnvironment.Normalize(root);string target=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Payload escapes target directory.");
        for(string? parent=target;parent!=null;parent=Path.GetDirectoryName(parent))
        {
            if((File.Exists(parent)||Directory.Exists(parent))&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("Links are not allowed in update paths.");
        }
        return target;
    }
    static PayloadManifest ReadManifest(string json,string version)
    {
        if(json.Length>1024*1024)throw new InvalidDataException("Payload manifest is too large.");
        var manifest=JsonSerializer.Deserialize<PayloadManifest>(json)??throw new InvalidDataException("Missing payload manifest.");
        if(manifest.Version!=version||manifest.Files==null||manifest.Files.Count<4||manifest.Files.Count>2000)throw new InvalidDataException("Invalid payload manifest.");
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long total=0;
        foreach(var file in manifest.Files)
        {
            if(!Allowed(file.Path)||!names.Add(file.Path)||file.Size<0||file.Size>512L*1024*1024||!Regex.IsMatch(file.Sha256,@"^[a-fA-F0-9]{64}$"))throw new InvalidDataException("Invalid payload entry.");
            total=checked(total+file.Size);if(total>ExpandedLimit)throw new InvalidDataException("Expanded update is too large.");
        }
        if(!new[]{"MooView.exe","MooView.dll","MooView.deps.json","MooView.runtimeconfig.json","MooView.portable"}.All(names.Contains))throw new InvalidDataException("Incomplete portable payload.");
        return manifest;
    }
    internal static async Task<string> StageAsync(string archive,string version,CancellationToken token)
    {
        string stage=Path.Combine(Path.GetDirectoryName(archive)!,"payload-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        using var zip=ZipFile.OpenRead(archive);var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            int attributes=entry.ExternalAttributes;
            if(((attributes>>16)&0xF000)==0xA000||(attributes&(int)FileAttributes.ReparsePoint)!=0)throw new InvalidDataException("ZIP links are not supported.");
            if(entry.FullName.EndsWith('/')){string name=entry.FullName.TrimEnd('/');if(!Regex.IsMatch(name,@"^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8}){0,2}$"))throw new InvalidDataException("Invalid ZIP directory.");continue;}
            if((entry.FullName!=ManifestName&&!Allowed(entry.FullName))||!entries.TryAdd(entry.FullName,entry))throw new InvalidDataException("Unsafe or duplicate ZIP entry.");
        }
        if(!entries.TryGetValue(ManifestName,out var manifestEntry)||manifestEntry.Length>1024*1024)throw new InvalidDataException("Missing portable manifest.");
        string json;using(var input=new StreamReader(manifestEntry.Open()))json=await input.ReadToEndAsync(token);
        var manifest=ReadManifest(json,version);
        if(entries.Count!=manifest.Files.Count+1)throw new InvalidDataException("Unexpected files in portable archive.");
        foreach(var file in manifest.Files)
        {
            token.ThrowIfCancellationRequested();if(!entries.TryGetValue(file.Path,out var entry)||entry.Length!=file.Size)throw new InvalidDataException("Portable file length mismatch.");
            string destination=Target(stage,file.Path);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await using var input=entry.Open();await using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);long bytes=0;byte[] buffer=new byte[65536];int read;
            while((read=await input.ReadAsync(buffer,token))>0){bytes+=read;if(bytes>file.Size)throw new InvalidDataException("Expanded file length mismatch.");hash.AppendData(buffer,0,read);await output.WriteAsync(buffer.AsMemory(0,read),token);}
            if(bytes!=file.Size||!CryptographicOperations.FixedTimeEquals(hash.GetHashAndReset(),Convert.FromHexString(file.Sha256)))throw new InvalidDataException(L10n.T("UpdateHashFailed"));
        }
        await File.WriteAllTextAsync(Path.Combine(stage,ManifestName),json,token);
        ValidateExecutable(stage,version);return stage;
    }
    static void ValidateExecutable(string directory,string version)
    {
        var info=FileVersionInfo.GetVersionInfo(Target(directory,"MooView.exe"));
        if(info.ProductName!="MooView"||info.ProductVersion?.Split('+')[0]!=version)throw new InvalidDataException("Portable executable version mismatch.");
    }
    internal static async Task ApplyAsync(string stage,string directory,string version,CancellationToken token,Action<int>? afterReplace=null)
    {
        string json=await File.ReadAllTextAsync(Path.Combine(stage,ManifestName),token);var manifest=ReadManifest(json,version);ValidateExecutable(stage,version);
        foreach(var file in manifest.Files){await UpdateInstaller.VerifyAsync(Target(stage,file.Path),file.Sha256,token);if(new FileInfo(Target(stage,file.Path)).Length!=file.Size)throw new InvalidDataException("Staged file size changed.");}
        directory=UpdateEnvironment.Normalize(directory);Target(directory,"MooView.exe");
        string transaction=Path.Combine(directory,".mooview-update-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(transaction);
        var changes=new List<(string path,bool existed,bool placed)>();bool safeCleanup=false;
        try
        {
            // Copy onto the target volume before touching existing program files.
            foreach(var file in manifest.Files){string next=Target(Path.Combine(transaction,"new"),file.Path);Directory.CreateDirectory(Path.GetDirectoryName(next)!);File.Copy(Target(stage,file.Path),next);await UpdateInstaller.VerifyAsync(next,file.Sha256,token);}
            File.WriteAllText(Path.Combine(transaction,"new",ManifestName),json);
            var replace=manifest.Files.Select(file=>file.Path).Append(ManifestName).ToList();
            string oldManifest=Target(directory,ManifestName);
            if(File.Exists(oldManifest))
            {
                var old=JsonSerializer.Deserialize<PayloadManifest>(File.ReadAllText(oldManifest));
                if(old?.Files!=null)foreach(var file in old.Files.Where(file=>Allowed(file.Path)&&!replace.Contains(file.Path,StringComparer.OrdinalIgnoreCase)))
                {string oldFile=Target(directory,file.Path);if(File.Exists(oldFile)&&Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(oldFile))).Equals(file.Sha256,StringComparison.OrdinalIgnoreCase))replace.Insert(0,file.Path);}
            }
            foreach(string path in replace.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();string destination=Target(directory,path),backup=Target(Path.Combine(transaction,"old"),path),next=Target(Path.Combine(transaction,"new"),path);
                if(Directory.Exists(destination))throw new IOException("A folder blocks an application file: "+path);
                bool existed=File.Exists(destination);changes.Add((path,existed,false));
                if(existed){Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Move(destination,backup);}
                if(File.Exists(next)){Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Move(next,destination);changes[^1]=(path,existed,true);}
                afterReplace?.Invoke(changes.Count);
            }
            ValidateExecutable(directory,version);safeCleanup=true;
        }
        catch
        {
            foreach(var change in changes.AsEnumerable().Reverse())
            {
                string destination=Target(directory,change.path),backup=Target(Path.Combine(transaction,"old"),change.path);
                if(change.placed&&File.Exists(destination))File.Delete(destination);
                if(change.existed&&File.Exists(backup)){Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Move(backup,destination);}
            }
            safeCleanup=true;throw;
        }
        finally
        {
            // Delete only this verified, generated transaction directory. Refuse
            // links so cleanup cannot traverse into a user's unrelated folder.
            if(safeCleanup&&Path.GetDirectoryName(transaction)==directory&&Regex.IsMatch(Path.GetFileName(transaction),@"^\.mooview-update-[a-f0-9]{32}$"))
            {
                try{CleanTransaction(transaction);}catch{/* Retain a backup if cleanup is blocked. */}
            }
        }
    }
    static void CleanTransaction(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked transaction directory.");
        foreach(string child in Directory.EnumerateDirectories(path))CleanTransaction(child);
        foreach(string file in Directory.EnumerateFiles(path)){if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked transaction file.");File.Delete(file);}
        Directory.Delete(path);
    }
}

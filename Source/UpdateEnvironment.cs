using Microsoft.Win32;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace MooView;

internal static class UpdateEnvironment
{
    internal const string PortableMarker="MooView.portable";
    internal static string Normalize(string directory)=>Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    internal static string Key(string directory)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(directory).ToUpperInvariant())))[..24].ToLowerInvariant();
    internal static bool IsPortable(string directory)
    {
        if(File.Exists(Path.Combine(directory,PortableMarker)))return true;
        // Old portable ZIPs have no marker. Only an uninstall record for this
        // exact directory counts as an installed copy.
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
        foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
        {
            try
            {
                using var root=RegistryKey.OpenBaseKey(hive,view);using var key=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\{D97071AE-3FF5-48D8-93CE-41BC4B769352}_is1");
                if(key?.GetValue("InstallLocation") is string installed&&Normalize(installed).Equals(Normalize(directory),StringComparison.OrdinalIgnoreCase))return false;
            }
            catch{ }
        }
        return true;
    }
    internal static string PendingPath(string directory,string? dataRoot=null)=>Path.Combine(dataRoot??UpdateClient.DataRoot,"pending-"+Key(directory)+".json");
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace MooView;

public static class FileAssociation
{
    public static void Unregister()
    {
        using var command=Registry.CurrentUser.OpenSubKey(@"Software\Classes\MooView.Image\shell\open\command");
        if(command?.GetValue("") is not string value || !value.StartsWith("\""+Environment.ProcessPath+"\"",StringComparison.OrdinalIgnoreCase))return;
        foreach(var ext in Imaging.Extensions){using var key=Registry.CurrentUser.OpenSubKey(@"Software\Classes\"+ext+@"\OpenWithProgids",true);key?.DeleteValue("MooView.Image",false);}
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\MooView.Image",false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\Applications\MooView.exe",false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\MooView",false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MooView.exe",false);
        using var registered=Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications",true);registered?.DeleteValue("MooView",false);
        SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
    }
    [DllImport("shell32.dll")] static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    public static void Register(bool openSettings = true)
    {
        string exe = Environment.ProcessPath!; void Set(string key, string name, string value) { using var k = Registry.CurrentUser.CreateSubKey(key); k.SetValue(name, value); }
        string prog = "MooView.Image"; Set(@"Software\Classes\" + prog, "", "MooView 이미지"); Set(@"Software\Classes\" + prog + @"\DefaultIcon", "", "\"" + exe + "\",0"); Set(@"Software\Classes\" + prog + @"\shell\open\command", "", "\"" + exe + "\" \"%1\"");
        Set(@"Software\Classes\"+prog+@"\Application","ApplicationName","MooView");
        Set(@"Software\Classes\"+prog+@"\Application","ApplicationDescription","MooView 이미지 및 GIF 뷰어");
        Set(@"Software\Classes\"+prog+@"\Application","ApplicationIcon","\""+exe+"\",0");
        Set(@"Software\Microsoft\Windows\CurrentVersion\App Paths\MooView.exe","",exe);
        Set(@"Software\Classes\Applications\MooView.exe", "FriendlyAppName", "MooView"); Set(@"Software\Classes\Applications\MooView.exe\shell\open\command", "", "\"" + exe + "\" \"%1\"");
        Set(@"Software\MooView\Capabilities", "ApplicationName", "MooView"); Set(@"Software\MooView\Capabilities", "ApplicationDescription", "다중 페이지 보기와 편집을 지원하는 이미지 뷰어"); Set(@"Software\MooView\Capabilities", "ApplicationIcon", "\"" + exe + "\",0");
        foreach (var ext in Imaging.Extensions) { using (var k = Registry.CurrentUser.CreateSubKey(@"Software\Classes\" + ext + @"\OpenWithProgids")) k.SetValue(prog, Array.Empty<byte>(), RegistryValueKind.None); Set(@"Software\Classes\Applications\MooView.exe\SupportedTypes", ext, ""); Set(@"Software\MooView\Capabilities\FileAssociations", ext, prog); }
        Set(@"Software\RegisteredApplications", "MooView", @"Software\MooView\Capabilities"); SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); if (openSettings) Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=MooView") { UseShellExecute = true });
    }
}


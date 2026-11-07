using Microsoft.Win32;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
namespace MooView;

internal enum AppThemeChoice { System, Light, Dark }
internal static class ThemePalette
{
    internal static readonly string[] Keys=["Bg","Panel","Text","Muted","Line","Field","Accent","ControlLine","Rail"];
    internal static string[] Colors(bool dark)=>dark?["#262626","#303030","#EEEEEE","#B8B8B8","#454545","#222222","#4A4A4A","#606060","#191919"]:["#E5E5E5","#F1F1F1","#242424","#606060","#FFFFFF","#E0E0E0","#D2D2D2","#AAAAAA","#282828"];
    internal static bool Resolve(AppThemeChoice mode,bool windowsDark)=>mode==AppThemeChoice.System?windowsDark:mode==AppThemeChoice.Dark;
    internal static AppThemeChoice Parse(string? value)=>Enum.TryParse<AppThemeChoice>(value,true,out var mode)&&Enum.IsDefined(mode)?mode:AppThemeChoice.System;
    internal static bool WindowsDark()
    {
        try{return Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value&&value==0;}catch{return false;}
    }
}
public partial class MainWindow
{
    AppThemeChoice themeMode=AppThemeChoice.System;bool themeClosed;
    void InitializeTheme(bool testMode)
    {
        // Tests stay deterministic and never change the Windows theme.
        dark=!testMode&&ThemePalette.Resolve(themeMode,ThemePalette.WindowsDark());ApplyTheme();
        if(!testMode)
        {
            SystemEvents.UserPreferenceChanged+=WindowsThemeChanged;
            Activated+=(_,_)=>RefreshSystemTheme();
            Closed+=(_,_)=>{themeClosed=true;SystemEvents.UserPreferenceChanged-=WindowsThemeChanged;};
        }
    }
    void WindowsThemeChanged(object sender,UserPreferenceChangedEventArgs e)
    {
        if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(()=>{if(!themeClosed)RefreshSystemTheme();});
    }
    void RefreshSystemTheme()
    {
        if(themeMode==AppThemeChoice.System)ApplySystemTheme(ThemePalette.WindowsDark());
    }
    void ApplySystemTheme(bool value){if(themeMode==AppThemeChoice.System&&value!=dark){dark=value;ApplyTheme();}}
    void ToggleTheme(object s,RoutedEventArgs e)
    {
        dark=!dark;themeMode=dark?AppThemeChoice.Dark:AppThemeChoice.Light;ApplyTheme();
    }
    void UseSystemTheme(object s,MouseButtonEventArgs e)
    {
        themeMode=AppThemeChoice.System;RefreshSystemTheme();e.Handled=true;
    }
    void ApplyTheme()
    {
        var colors=ThemePalette.Colors(dark);
        for(int i=0;i<ThemePalette.Keys.Length;i++)Application.Current.Resources[ThemePalette.Keys[i]]=new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        UpdateBackground();
    }
}

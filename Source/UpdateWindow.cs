using System.Windows;
using System.Windows.Controls;
namespace MooView;

internal sealed class UpdateWindow:Window
{
    readonly UpdateManager manager;readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,12,4,8)};
    readonly TextBox notes=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=120,MaxHeight=230};
    readonly Button check=new(),install=new(),cancel=new();readonly CheckBox automatic=new(),download=new();readonly ProgressBar progress=new(){Minimum=0,Maximum=100,Height=5,Margin=new Thickness(4,10,4,12)};bool syncing,manualBusy;
    internal UpdateWindow(UpdateManager manager)
    {
        this.manager=manager;manager.ReloadPreferences();Style=(Style)FindResource(typeof(Window));Title=L10n.T("Updates");Width=590;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(24)};Content=panel;
        var header=new StackPanel{Orientation=Orientation.Horizontal};header.Children.Add(new Glyph{Kind="update",Width=30,Height=30,Margin=new Thickness(3,0,12,0)});header.Children.Add(new TextBlock{Text="MooView "+UpdateClient.CurrentVersion.ToString(3),FontSize=24,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(header);
        var mode=new TextBlock{Text=L10n.T(manager.Portable?"UpdatePortableMode":"UpdateInstalledMode"),Margin=new Thickness(4,8,4,8),FontWeight=FontWeights.SemiBold};panel.Children.Add(mode);
        automatic.Content=L10n.T("UpdateAutomatic");automatic.Margin=new Thickness(4,8,4,8);download.Content=L10n.T("UpdateDownloadOnExit");download.Margin=new Thickness(4,0,4,8);panel.Children.Add(automatic);panel.Children.Add(download);
        automatic.Checked+=PreferencesChanged;automatic.Unchecked+=PreferencesChanged;download.Checked+=PreferencesChanged;download.Unchecked+=PreferencesChanged;
        var hint=new TextBlock{Text=L10n.T(manager.Portable?"UpdatePortableHint":"UpdateInstalledHint"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,4,4,8),FontSize=12};hint.SetResourceReference(ForegroundProperty,"Muted");panel.Children.Add(hint);panel.Children.Add(status);panel.Children.Add(notes);panel.Children.Add(progress);
        var track=new FrameworkElementFactory(typeof(Grid));track.Name="PART_Track";track.SetResourceReference(Panel.BackgroundProperty,"Field");var indicator=new FrameworkElementFactory(typeof(Border));indicator.Name="PART_Indicator";indicator.SetValue(HorizontalAlignmentProperty,HorizontalAlignment.Left);indicator.SetResourceReference(Border.BackgroundProperty,"Muted");track.AppendChild(indicator);progress.Template=new ControlTemplate(typeof(ProgressBar)){VisualTree=track};
        var row=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(row);check.Content=L10n.T("UpdateCheck");install.Content=L10n.T("UpdateInstall");cancel.Content=L10n.T("Close");row.Children.Add(check);row.Children.Add(install);row.Children.Add(cancel);
        check.Click+=async(_,_)=>{manualBusy=true;try{await manager.CheckAsync(true);}finally{manualBusy=false;}};
        install.Click+=async(_,_)=>{manualBusy=true;try{await manager.StageAsync();manualBusy=false;if(manager.Ready)DialogResult=true;}finally{manualBusy=false;}};
        cancel.Click+=(_,_)=>{if(manager.Busy)manager.Cancel();else Close();};
        Closing+=(_,e)=>{if(manualBusy&&manager.Busy){e.Cancel=true;manager.Cancel();}};Closed+=(_,_)=>manager.Changed-=Refresh;
        manager.Changed+=Refresh;Refresh();Loaded+=async(_,_)=>{if(!manager.Ready&&manager.Available==null&&!manager.Busy)await manager.CheckAsync(true);};
    }
    void PreferencesChanged(object sender,RoutedEventArgs e)
    {
        if(syncing)return;bool checks=automatic.IsChecked==true,downloads=download.IsChecked==true;
        if(ReferenceEquals(sender,download)&&downloads)checks=true;
        manager.SetPreferences(checks,checks&&downloads);
        if(checks&&downloads&&!manager.Busy&&!manager.Ready)_=manager.CheckAsync(true);
    }
    void Refresh()
    {
        syncing=true;automatic.IsChecked=manager.Preferences.AutomaticChecks;download.IsChecked=manager.Preferences.DownloadOnExit;syncing=false;
        automatic.IsEnabled=download.IsEnabled=!manager.Busy;check.IsEnabled=!manager.Busy;
        install.IsEnabled=!manager.Busy&&(manager.Available!=null||manager.Ready);cancel.Content=L10n.T(manager.Busy?"Cancel":"Close");progress.Value=manager.Progress*100;progress.Visibility=manager.Busy?Visibility.Visible:Visibility.Collapsed;
        status.Text=L10n.T(manager.State)+(manager.Ready?" · "+manager.ReadyVersion:manager.State=="UpdateAvailable"?" "+manager.Available?.Version.ToString(3):manager.State=="UpdateDownloading"?" "+(manager.Progress*100).ToString("0")+"%":"")+(manager.Error==null?"":"\n"+manager.Error);
        notes.Text=manager.Available?.Notes??"";notes.Visibility=notes.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
    }
}

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace MooView;

internal sealed class UpdateWindow:Window
{
    readonly UpdateClient client;readonly Action<bool> preferenceChanged;readonly CancellationTokenSource lifetime=new();CancellationTokenSource? operation;
    readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,12,4,8)};
    readonly TextBox notes=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=130,MaxHeight=250};
    readonly Button check=new(),install=new(),cancel=new();readonly ProgressBar progress=new(){Minimum=0,Maximum=100,Height=5,Margin=new Thickness(4,10,4,12)};
    UpdateRelease? release;bool busy;
    internal DownloadedUpdate? Download {get;private set;}
    internal UpdateWindow(UpdateClient client,UpdateRelease? available,bool automatic,Action<bool> changed)
    {
        this.client=client;preferenceChanged=changed;Style=(Style)FindResource(typeof(Window));Title=L10n.T("Updates");Width=570;SizeToContent=SizeToContent.Height;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var panel=new StackPanel{Margin=new Thickness(24)};Content=panel;
        var header=new StackPanel{Orientation=Orientation.Horizontal};header.Children.Add(new Glyph{Kind="update",Width=30,Height=30,Margin=new Thickness(3,0,12,0)});header.Children.Add(new TextBlock{Text="MooView "+UpdateClient.CurrentVersion.ToString(3),FontSize=24,VerticalAlignment=VerticalAlignment.Center});panel.Children.Add(header);
        var auto=new CheckBox{Content=L10n.T("UpdateAutomatic"),IsChecked=automatic,Margin=new Thickness(4,18,4,4)};auto.Checked+=(_,_)=>preferenceChanged(true);auto.Unchecked+=(_,_)=>preferenceChanged(false);panel.Children.Add(auto);
        var hint=new TextBlock{Text=L10n.T("UpdateHint"),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(4,4,4,8),FontSize=12};hint.SetResourceReference(ForegroundProperty,"Muted");panel.Children.Add(hint);panel.Children.Add(status);panel.Children.Add(notes);panel.Children.Add(progress);
        var row=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};panel.Children.Add(row);
        check.Content=L10n.T("UpdateCheck");install.Content=L10n.T("UpdateInstall");cancel.Content=L10n.T("Close");
        check.Click+=async(_,_)=>await CheckAsync();install.Click+=async(_,_)=>await DownloadAsync();cancel.Click+=(_,_)=>{if(busy)operation?.Cancel();else Close();};row.Children.Add(check);row.Children.Add(install);row.Children.Add(cancel);
        Closing+=(_,e)=>{if(busy){e.Cancel=true;operation?.Cancel();}else lifetime.Cancel();};Closed+=(_,_)=>{operation?.Dispose();lifetime.Dispose();};
        SetRelease(available);if(available==null)Loaded+=async(_,_)=>await CheckAsync();
    }
    void SetRelease(UpdateRelease? available)
    {
        release=available;notes.Text=available?.Notes??"";notes.Visibility=available==null?Visibility.Collapsed:Visibility.Visible;install.IsEnabled=available!=null;
        status.Text=available==null?L10n.T("UpdateCurrent"):L10n.T("UpdateAvailable")+" "+available.Version.ToString(3);
    }
    void Busy(bool value,string text)
    {
        busy=value;check.IsEnabled=!value;install.IsEnabled=!value&&release!=null;cancel.Content=L10n.T(value?"Cancel":"Close");status.Text=text;
        if(value){operation?.Dispose();operation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);}
    }
    async Task CheckAsync()
    {
        if(busy)return;Busy(true,L10n.T("UpdateChecking"));
        try{var found=await client.CheckAsync(UpdateClient.CurrentVersion,operation!.Token);Busy(false,"");SetRelease(found);}
        catch(OperationCanceledException){Busy(false,L10n.T("UpdateCancelled"));}
        catch(Exception ex){Busy(false,L10n.T("UpdateFailed")+"\n"+ex.Message);}
    }
    async Task DownloadAsync()
    {
        if(busy||release==null)return;
        if(UpdateInstaller.OtherInstances(AppContext.BaseDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar),Environment.ProcessId)){status.Text=L10n.T("UpdateOtherInstances");return;}
        Busy(true,L10n.T("UpdateDownloading"));progress.Value=0;
        try
        {
            Download=await client.DownloadAsync(release,UpdateClient.DownloadsRoot,new Progress<double>(value=>{progress.Value=value*100;status.Text=L10n.T("UpdateDownloading")+" "+(value*100).ToString("0")+"%";}),operation!.Token);
            Busy(false,L10n.T("UpdatePreparing"));DialogResult=true;
        }
        catch(OperationCanceledException){Busy(false,L10n.T("UpdateCancelled"));}
        catch(Exception ex){Busy(false,L10n.T("UpdateFailed")+"\n"+ex.Message);}
    }
}

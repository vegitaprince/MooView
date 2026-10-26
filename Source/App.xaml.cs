using System.Windows;
using System.IO;
namespace MooView;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); if(e.Args.Contains("--unregister-only")){FileAssociation.Unregister();Shutdown();return;} if(e.Args.Contains("--create-icon")){BrandIcon.Create(Path.Combine(AppContext.BaseDirectory,"MooView.ico"));Shutdown();return;} if (e.Args.Contains("--ui-test")) { ShutdownMode = ShutdownMode.OnExplicitShutdown; try { await UiSmokeTest.Run(); Shutdown(0); } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ui-test.txt"), ex.ToString()); Shutdown(1); } return; } if (e.Args.Contains("--self-test")) { try { SelfTest.Run(); Shutdown(0); } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), ex.ToString()); Shutdown(1); } return; } if (e.Args.Contains("--register") || e.Args.Contains("--register-only")) { try { FileAssociation.Register(!e.Args.Contains("--register-only")); } catch (Exception ex) { MessageBox.Show(ex.Message, "연결 프로그램 등록"); } Shutdown(); return; } var w = new MainWindow(); MainWindow = w; w.Show(); if (e.Args.Length > 0) w.OpenPaths(e.Args); }
}





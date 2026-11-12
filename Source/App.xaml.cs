using System.Windows;
using System.IO;
namespace MooView;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e) { base.OnStartup(e); if(e.Args.Contains("--advanced-test")){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{SelfTest.Run();var testWindow=new MainWindow(true);var log=new List<string>();await AdvancedImageTests.Run(testWindow,Path.Combine(AppContext.BaseDirectory,"test-data"),log);File.WriteAllLines(Path.Combine(AppContext.BaseDirectory,"advanced-test.txt"),log);testWindow.Close();Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"advanced-test.txt"),ex.ToString());Shutdown(1);}return;} if(e.Args.Length==2&&e.Args[0]=="--package-portable"){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{PortableUpdate.CreatePackage(AppContext.BaseDirectory,e.Args[1]);Shutdown(0);}catch(Exception ex){File.WriteAllText(Path.Combine(AppContext.BaseDirectory,"package-error.txt"),ex.ToString());Shutdown(1);}return;} if(e.Args.Length==2&&e.Args[0]=="--apply-update"){ShutdownMode=ShutdownMode.OnExplicitShutdown;try{await UpdateInstaller.RunHelperAsync(e.Args[1]);Shutdown(0);}catch(Exception ex){MessageBox.Show(ex.Message,"MooView update",MessageBoxButton.OK,MessageBoxImage.Warning);Shutdown(1);}return;} if(e.Args.Contains("--unregister-only")){FileAssociation.Unregister();Shutdown();return;} if(e.Args.Contains("--create-icon")){BrandIcon.Create(Path.Combine(AppContext.BaseDirectory,"MooView.ico"));Shutdown();return;} if (e.Args.Contains("--ui-test")) { ShutdownMode = ShutdownMode.OnExplicitShutdown; try { await UiSmokeTest.Run(); Shutdown(0); } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "ui-test.txt"), ex.ToString()); Shutdown(1); } return; } if (e.Args.Contains("--self-test")) { try { SelfTest.Run(); Shutdown(0); } catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "self-test.txt"), ex.ToString()); Shutdown(1); } return; } if (e.Args.Contains("--register") || e.Args.Contains("--register-only")) { try { FileAssociation.Register(!e.Args.Contains("--register-only")); } catch (Exception ex) { MessageBox.Show(ex.Message, "연결 프로그램 등록"); } Shutdown(); return; } var w = new MainWindow(); MainWindow = w; w.Show(); if (e.Args.Length > 0) w.OpenPaths(e.Args); }
}





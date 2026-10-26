using System.Windows;
using LuminanceCapture.Core;

namespace LuminanceCapture.App;

public partial class App : Application
{
    private SingleInstance? _single;
    private AppHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        _single = new SingleInstance();
        if (!_single.TryAcquire())
        {
            // Let the first instance take the foreground, wake it, and leave.
            Native.AllowSetForegroundWindow(-1);
            try { _single.SignalExisting(); } catch (Exception ex) { ErrorLog.Write("signal first instance failed", ex); }
            Environment.Exit(0);
            return;
        }

        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLog.Write("Unhandled UI exception", args.Exception);
            args.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            ErrorLog.Write("Unhandled exception (terminating=" + args.IsTerminating + ")", args.ExceptionObject as Exception);
            _host?.EmergencyRestore();
        };
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            ErrorLog.Write("Unobserved task exception", args.Exception);
            args.SetObserved();
        };
        AppDomain.CurrentDomain.ProcessExit += (_, _) => _host?.EmergencyRestore();

        try
        {
            _host = new AppHost(_single);
            var background = e.Args.Any(a => string.Equals(a, AutoStartManager.BackgroundArg, StringComparison.OrdinalIgnoreCase));
            _host.Start(background);
        }
        catch (Exception ex)
        {
            ErrorLog.Write("Startup failed", ex);
            _host?.EmergencyRestore();
            MessageBox.Show("프로그램을 시작하지 못했습니다.\n자세한 내용은 로그 파일을 확인해 주세요.", "Luminance Capture",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        _host?.Exit();
        base.OnSessionEnding(e);
    }
}

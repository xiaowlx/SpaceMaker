using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SpaceMaker;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        HookGlobalExceptions();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    private static void HookGlobalExceptions()
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceMaker");
        try { Directory.CreateDirectory(logDir); } catch { }
        var logPath = Path.Combine(logDir, "crash.log");

        void WriteLog(string header, Exception? ex)
        {
            try
            {
                var text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {header}{Environment.NewLine}{ex}{Environment.NewLine}";
                File.AppendAllText(logPath, text);
            }
            catch { }
        }

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            WriteLog("UnhandledException", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            WriteLog("UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

using System.IO;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            Log("UnhandledException: " + e.Exception);
        };
    }

    public static AppSettings Settings { get; } = new();

    public static CompletedJobsStore Completed { get; } = new();

    public static QueueManager? Queue { get; private set; }

    public static MainWindow? MainWindow { get; set; }

    public static IReadOnlyList<string> StartupFiles { get; private set; } = Array.Empty<string>();

    public static bool AutoStart { get; private set; }

    public static string StartPage { get; private set; } = "home";

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var commandLine = Environment.GetCommandLineArgs().Skip(1).ToList();
        StartupFiles = commandLine
            .Where(path => !path.StartsWith("--") && File.Exists(path) && QueueManager.IsSupportedMediaFile(path))
            .ToList();
        AutoStart = commandLine.Contains("--start") || commandLine.Contains("--autostart");
        if (commandLine.Contains("--settings"))
        {
            StartPage = "settings";
        }
        else if (commandLine.Contains("--help"))
        {
            StartPage = "help";
        }
        else if (commandLine.Contains("--about"))
        {
            StartPage = "about";
        }

        try
        {
            var dispatcher = DispatcherQueue.GetForCurrentThread();
            var python = PythonEnvironment.Locate(Settings);
            Queue = new QueueManager(Settings, Completed, python, dispatcher);
            Log($"python={python.PythonExe} valid={python.IsValid()}");

            MainWindow = new MainWindow();
            MainWindow.Activate();

            if (Queue is not null)
            {
                if (StartupFiles.Count > 0)
                {
                    Log($"adding startup files: {string.Join(" | ", StartupFiles)}");
                    Queue.AddFiles(StartupFiles);
                    Log($"queue count after add: {Queue.Jobs.Count}");
                }

                if (AutoStart)
                {
                    Log("autostart");
                    _ = Queue.StartAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Log("OnLaunched fatal: " + ex);
            throw;
        }
    }

    public static string LogPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PianoTrans-RE",
        "app.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Logging must never take the app down.
        }
    }
}

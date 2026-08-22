using System.IO;
using System.Text.Json;

namespace PianoTrans.WUI50.Services;

public sealed class AppSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private string _settingsPath;

    public AppSettings()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var root = Path.Combine(localAppData, "PianoTrans-WUI50");
        Directory.CreateDirectory(root);
        _settingsPath = Path.Combine(root, "settings.json");

        var music = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
        if (string.IsNullOrWhiteSpace(music))
        {
            music = root;
        }

        OutputFolder = Path.Combine(music, "PianoTrans");
        Load();
    }

    public string DeviceMode { get; set; } = "gpu";

    public string OutputFolder { get; set; } = "";

    public double MinNoteDurationSeconds { get; set; } = 0.05;

    public string? PythonExeOverride { get; set; }

    public string? WorkerScriptOverride { get; set; }

    public void Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
            {
                return;
            }

            var json = File.ReadAllText(_settingsPath);
            var saved = JsonSerializer.Deserialize<SettingsData>(json, JsonOptions);
            if (saved is null)
            {
                return;
            }

            DeviceMode = saved.DeviceMode is "gpu" or "cpu" ? saved.DeviceMode : "gpu";
            if (!string.IsNullOrWhiteSpace(saved.OutputFolder))
            {
                OutputFolder = saved.OutputFolder;
            }
            if (saved.MinNoteDurationSeconds is >= 0.01 and <= 2.0)
            {
                MinNoteDurationSeconds = saved.MinNoteDurationSeconds;
            }
            PythonExeOverride = saved.PythonExeOverride;
            WorkerScriptOverride = saved.WorkerScriptOverride;
        }
        catch
        {
            // A broken settings file should never prevent the app from opening.
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            var data = new SettingsData
            {
                DeviceMode = DeviceMode,
                OutputFolder = OutputFolder,
                MinNoteDurationSeconds = MinNoteDurationSeconds,
                PythonExeOverride = PythonExeOverride,
                WorkerScriptOverride = WorkerScriptOverride,
            };
            File.WriteAllText(_settingsPath, JsonSerializer.Serialize(data, JsonOptions));
        }
        catch
        {
            // Settings are best-effort; the app still works with in-memory values.
        }
    }
    private sealed class SettingsData
    {
        public string DeviceMode { get; set; } = "gpu";

        public string OutputFolder { get; set; } = "";

        public double MinNoteDurationSeconds { get; set; } = 0.05;

        public string? PythonExeOverride { get; set; }

        public string? WorkerScriptOverride { get; set; }
    }
}

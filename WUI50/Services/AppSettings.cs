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
        var root = Path.Combine(localAppData, "PianoTrans-RE");
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

    public double OnsetThreshold { get; set; } = 0.30;

    public double OffsetThreshold { get; set; } = 0.30;

    public double FrameThreshold { get; set; } = 0.10;

    public double PedalOffsetThreshold { get; set; } = 0.20;

    public int OnsetPeakNeighbor { get; set; } = 2;

    public int OffsetPeakNeighbor { get; set; } = 4;

    public int PedalOffsetPeakNeighbor { get; set; } = 4;

    public double MidiBpm { get; set; } = 120;

    public int InferenceBatchSize { get; set; } = 1;

    public double SegmentOverlapPercent { get; set; } = 50;

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
            if (saved.OnsetThreshold is >= 0.01 and <= 0.99)
            {
                OnsetThreshold = saved.OnsetThreshold;
            }
            if (saved.OffsetThreshold is >= 0.01 and <= 0.99)
            {
                OffsetThreshold = saved.OffsetThreshold;
            }
            if (saved.FrameThreshold is >= 0.01 and <= 0.99)
            {
                FrameThreshold = saved.FrameThreshold;
            }
            if (saved.PedalOffsetThreshold is >= 0.01 and <= 0.99)
            {
                PedalOffsetThreshold = saved.PedalOffsetThreshold;
            }
            if (saved.OnsetPeakNeighbor is >= 1 and <= 8)
            {
                OnsetPeakNeighbor = saved.OnsetPeakNeighbor;
            }
            if (saved.OffsetPeakNeighbor is >= 1 and <= 8)
            {
                OffsetPeakNeighbor = saved.OffsetPeakNeighbor;
            }
            if (saved.PedalOffsetPeakNeighbor is >= 1 and <= 8)
            {
                PedalOffsetPeakNeighbor = saved.PedalOffsetPeakNeighbor;
            }
            if (saved.MidiBpm is >= 20 and <= 300)
            {
                MidiBpm = saved.MidiBpm;
            }
            if (saved.InferenceBatchSize is >= 1 and <= 8)
            {
                InferenceBatchSize = saved.InferenceBatchSize;
            }
            if (saved.SegmentOverlapPercent is >= 0 and <= 75)
            {
                SegmentOverlapPercent = saved.SegmentOverlapPercent;
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
                OnsetThreshold = OnsetThreshold,
                OffsetThreshold = OffsetThreshold,
                FrameThreshold = FrameThreshold,
                PedalOffsetThreshold = PedalOffsetThreshold,
                OnsetPeakNeighbor = OnsetPeakNeighbor,
                OffsetPeakNeighbor = OffsetPeakNeighbor,
                PedalOffsetPeakNeighbor = PedalOffsetPeakNeighbor,
                MidiBpm = MidiBpm,
                InferenceBatchSize = InferenceBatchSize,
                SegmentOverlapPercent = SegmentOverlapPercent,
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

        public double OnsetThreshold { get; set; } = 0.30;

        public double OffsetThreshold { get; set; } = 0.30;

        public double FrameThreshold { get; set; } = 0.10;

        public double PedalOffsetThreshold { get; set; } = 0.20;

        public int OnsetPeakNeighbor { get; set; } = 2;

        public int OffsetPeakNeighbor { get; set; } = 4;

        public int PedalOffsetPeakNeighbor { get; set; } = 4;

        public double MidiBpm { get; set; } = 120;

        public int InferenceBatchSize { get; set; } = 1;

        public double SegmentOverlapPercent { get; set; } = 50;


        public string? PythonExeOverride { get; set; }

        public string? WorkerScriptOverride { get; set; }
    }
}

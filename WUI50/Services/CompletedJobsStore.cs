using System.IO;
using System.Text.Json;

namespace PianoTrans.WUI50.Services;

/// <summary>
/// Persists which input files were already transcribed, keyed by
/// input-path + file-size + last-write-time + output-folder.
/// </summary>
public sealed class CompletedJobsStore
{
    private readonly object _lock = new();
    private readonly string _path;
    private Dictionary<string, string> _entries;

    public CompletedJobsStore()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PianoTrans-WUI50");
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "completed-jobs.json");
        _entries = Load();
    }

    public bool TryGetOutput(string key, out string? outputPath)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(key, out outputPath);
        }
    }

    public void MarkCompleted(string key, string outputPath)
    {
        lock (_lock)
        {
            _entries[key] = outputPath;
            SaveUnsafe();
        }
    }

    public void Remove(string key)
    {
        lock (_lock)
        {
            if (_entries.Remove(key))
            {
                SaveUnsafe();
            }
        }
    }

    private Dictionary<string, string> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_path))
                    ?? new Dictionary<string, string>();
            }
        }
        catch
        {
            // Corrupt completed-list is not fatal.
        }

        return new Dictionary<string, string>();
    }

    private void SaveUnsafe()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_entries));
        }
        catch
        {
            // Best effort only.
        }
    }
}

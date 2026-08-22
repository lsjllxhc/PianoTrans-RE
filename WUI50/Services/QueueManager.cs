using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.UI.Dispatching;
using PianoTrans.WUI50.Models;

namespace PianoTrans.WUI50.Services;

public sealed class QueueManager
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus",
        ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi",
    };

    private readonly AppSettings _settings;
    private readonly CompletedJobsStore _completed;
    private readonly PythonEnvironment _python;
    private readonly DispatcherQueue _dispatcher;

    private Process? _process;
    private List<TranscodeJob> _activeBatch = new();
    private bool _stopRequested;
    private readonly Queue<string> _stderrTail = new();

    public QueueManager(
        AppSettings settings,
        CompletedJobsStore completed,
        PythonEnvironment python,
        DispatcherQueue dispatcher)
    {
        _settings = settings;
        _completed = completed;
        _python = python;
        _dispatcher = dispatcher;
    }

    public ObservableCollection<TranscodeJob> Jobs { get; } = new();

    public bool IsRunning { get; private set; }

    public event Action? RunningChanged;

    public event Action<string>? InfoOccurred;

    public event Action<string>? ErrorOccurred;

    public bool IsPythonReady => _python.IsValid();

    public string PythonStatusText
        => _python.IsValid()
            ? $"Python: {_python.PythonExe}"
            : $"Python 未找到: {_python.PythonExe}";

    public static bool IsSupportedMediaFile(string path)
        => SupportedExtensions.Contains(Path.GetExtension(path));

    public void AddFiles(IEnumerable<string> paths)
    {
        var skipped = 0;
        var added = 0;

        foreach (var rawPath in paths)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                continue;
            }

            var path = Path.GetFullPath(rawPath);
            if (!File.Exists(path))
            {
                skipped++;
                continue;
            }

            if (!IsSupportedMediaFile(path))
            {
                skipped++;
                continue;
            }

            var outputFolder = Path.GetFullPath(string.IsNullOrWhiteSpace(_settings.OutputFolder)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), "PianoTrans")
                : _settings.OutputFolder);
            try
            {
                Directory.CreateDirectory(outputFolder);
            }
            catch
            {
                skipped++;
                continue;
            }

            var info = new FileInfo(path);
            var key = MakeCompletedKey(path, outputFolder, info);

            // Completed earlier + the generated MIDI still exists -> never reprocess.
            if (_completed.TryGetOutput(key, out var completedOutput) &&
                !string.IsNullOrWhiteSpace(completedOutput) &&
                File.Exists(completedOutput))
            {
                skipped++;
                continue;
            }

            // Another copy of the same file is already in the queue.
            if (Jobs.Any(job => string.Equals(job.Key, key, StringComparison.Ordinal)))
            {
                skipped++;
                continue;
            }

            // An output file already exists at the default destination; treat as completed.
            var stem = Path.GetFileNameWithoutExtension(path);
            var stableOutput = Path.Combine(outputFolder, stem + ".mid");
            if (File.Exists(stableOutput))
            {
                _completed.MarkCompleted(key, stableOutput);
                skipped++;
                continue;
            }

            var output = FindFreeOutputPath(outputFolder, stem);
            var job = new TranscodeJob(path, output, key);
            Jobs.Add(job);
            added++;
        }

        if (skipped > 0)
        {
            InfoOccurred?.Invoke($"已跳过 {skipped} 个文件：不支持、已存在或已完成。");
        }
        if (added > 0)
        {
            InfoOccurred?.Invoke($"已添加 {added} 个文件到队列。");
        }
    }

    public void RemoveJob(TranscodeJob job)
    {
        if (job.Status == JobStatus.Processing)
        {
            return;
        }

        Jobs.Remove(job);
    }

    public void ClearFinished()
    {
        for (var i = Jobs.Count - 1; i >= 0; i--)
        {
            if (Jobs[i].Status is JobStatus.Completed or JobStatus.Failed)
            {
                Jobs.RemoveAt(i);
            }
        }
    }

    public async Task StartAsync()
    {
        if (IsRunning)
        {
            return;
        }

        App.Log($"StartAsync python valid={_python.IsValid()} exe={_python.PythonExe} worker={_python.WorkerScript}");
        if (!_python.IsValid())
        {
            App.Log("StartAsync python invalid");
            ErrorOccurred?.Invoke("未找到 Python 转录后端。\n\n请先运行 PianoTrans-GPU50-Install.bat，或检查 venv50 与 modern50\\PianoTrans-Worker.py 是否完整。");
            return;
        }

        var batch = Jobs.Where(job => job.Status is JobStatus.Waiting or JobStatus.Failed).ToList();
        if (batch.Count == 0)
        {
            InfoOccurred?.Invoke("队列里没有等待处理的任务。");
            return;
        }

        foreach (var job in batch)
        {
            job.Status = JobStatus.Waiting;
            job.Progress = 0;
            job.Stage = "";
            job.ErrorMessage = "";
        }

        _activeBatch = batch;
        _stopRequested = false;
        lock (_stderrTail)
        {
            _stderrTail.Clear();
        }
        IsRunning = true;
        RunningChanged?.Invoke();

        var manifestPath = WriteManifest(batch);
        var psi = _python.CreateStartInfo(manifestPath);
        App.Log($"manifest={manifestPath} jobs={batch.Count}");

        try
        {
            App.Log($"starting python: {psi.FileName} {string.Join(" ", psi.ArgumentList)}");
            _process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            _process.OutputDataReceived += OnOutputLine;
            _process.ErrorDataReceived += OnErrorLine;

            if (!_process.Start())
            {
                throw new InvalidOperationException("无法启动 Python 进程。");
            }

            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
            await _process.WaitForExitAsync();

            if (_process.ExitCode != 0 && !_stopRequested)
            {
                var current = _activeBatch.FirstOrDefault(job => job.Status == JobStatus.Processing);
                if (current is not null)
                {
                    _dispatcher.TryEnqueue(() =>
                    {
                        current.Status = JobStatus.Failed;
                        current.ErrorMessage = LastStderr();
                    });
                    ErrorOccurred?.Invoke($"转录进程异常退出（代码 {_process.ExitCode}）。\n\n{LastStderr()}");
                }
            }
        }
        catch (Exception ex)
        {
            App.Log("StartAsync exception: " + ex);
            var current = _activeBatch.FirstOrDefault(job => job.Status == JobStatus.Processing);
            if (current is not null)
            {
                _dispatcher.TryEnqueue(() =>
                {
                    current.Status = JobStatus.Failed;
                    current.ErrorMessage = ex.Message;
                });
            }

            ErrorOccurred?.Invoke("启动转录进程失败。\n\n" + ex.Message);
        }
        finally
        {
            _process?.Dispose();
            _process = null;
            IsRunning = false;
            RunningChanged?.Invoke();
        }
    }

    public void Stop()
    {
        if (_process is null || _process.HasExited)
        {
            return;
        }

        _stopRequested = true;
        try
        {
            _process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The worker may have exited between HasExited and Kill.
        }

        var current = _activeBatch.FirstOrDefault(job => job.Status == JobStatus.Processing);
        if (current is not null)
        {
            _dispatcher.TryEnqueue(() =>
            {
                current.Status = JobStatus.Waiting;
                current.Progress = 0;
                current.Stage = "已停止";
            });
        }

        InfoOccurred?.Invoke("已停止当前队列。");
    }

    private string WriteManifest(List<TranscodeJob> batch)
    {
        var runDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PianoTrans-WUI50",
            "runs");
        Directory.CreateDirectory(runDir);

        var manifestPath = Path.Combine(runDir, $"manifest-{Guid.NewGuid():N}.json");
        var manifest = new
        {
            device = _settings.DeviceMode,
            min_note_duration = _settings.MinNoteDurationSeconds,
            checkpoint = _python.CheckpointPath,
            jobs = batch.Select(job => new
            {
                input = job.InputPath,
                output = job.OutputPath,
            }).ToArray(),
        };

        File.WriteAllText(
            manifestPath,
            JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(false));

        return manifestPath;
    }

    private void OnOutputLine(object sender, DataReceivedEventArgs e)
    {
        var line = e.Data;
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        var trimmed = line.TrimStart();
        if (!trimmed.StartsWith('{'))
        {
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeElement))
            {
                return;
            }

            var type = typeElement.GetString();
            var index = root.TryGetProperty("index", out var indexElement)
                ? indexElement.GetInt32()
                : -1;
            var job = index >= 0 && index < _activeBatch.Count ? _activeBatch[index] : null;

            switch (type)
            {
                case "job_start" when job is not null:
                    _dispatcher.TryEnqueue(() =>
                    {
                        job.Status = JobStatus.Processing;
                        job.Progress = 0;
                        job.Stage = "准备中";
                    });
                    break;

                case "progress" when job is not null:
                {
                    var progress = root.TryGetProperty("progress", out var p) ? p.GetDouble() : 0;
                    var stage = root.TryGetProperty("stage", out var s) ? s.GetString() ?? "" : "";
                    progress = Math.Clamp(progress, 0, 1) * 100.0;
                    _dispatcher.TryEnqueue(() =>
                    {
                        job.Progress = progress;
                        job.Stage = StageToChinese(stage);
                    });
                    break;
                }

                case "job_done" when job is not null:
                {
                    var notes = root.TryGetProperty("notes", out var n) ? n.GetInt32() : 0;
                    var pedals = root.TryGetProperty("pedals", out var pd) ? pd.GetInt32() : 0;
                    var elapsed = root.TryGetProperty("elapsed", out var el) ? el.GetDouble() : 0;
                    var filtered = root.TryGetProperty("filtered_short_notes", out var fs) ? fs.GetInt32() : 0;
                    _dispatcher.TryEnqueue(() =>
                    {
                        job.Status = JobStatus.Completed;
                        job.Progress = 100;
                        job.Stage = "完成";
                        job.Notes = notes;
                        job.Pedals = pedals;
                        job.ElapsedSeconds = elapsed;
                        job.FilteredShortNotes = filtered;
                    });
                    _completed.MarkCompleted(job.Key, job.OutputPath);
                    break;
                }

                case "job_error" when job is not null:
                {
                    var message = root.TryGetProperty("message", out var msg) ? msg.GetString() ?? "未知错误" : "未知错误";
                    _dispatcher.TryEnqueue(() =>
                    {
                        job.Status = JobStatus.Failed;
                        job.ErrorMessage = message;
                        job.Stage = "错误";
                    });
                    ErrorOccurred?.Invoke($"「{job.FileName}」转录失败。\n\n{message}");
                    break;
                }

                case "device":
                {
                    var device = root.TryGetProperty("device", out var d) ? d.GetString() ?? "" : "";
                    if (device == "cpu")
                    {
                        _dispatcher.TryEnqueue(() => InfoOccurred?.Invoke("当前任务使用 CPU 推理。"));
                    }
                    break;
                }
            }
        }
        catch (JsonException)
        {
            // Ignore banner lines that happen to start with '{'.
        }
    }

    private void OnErrorLine(object sender, DataReceivedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Data))
        {
            return;
        }

        lock (_stderrTail)
        {
            _stderrTail.Enqueue(e.Data);
            while (_stderrTail.Count > 40)
            {
                _stderrTail.Dequeue();
            }
        }
    }

    private string LastStderr()
    {
        lock (_stderrTail)
        {
            return string.Join(Environment.NewLine, _stderrTail);
        }
    }

    private string FindFreeOutputPath(string outputFolder, string stem)
    {
        var basePath = Path.Combine(outputFolder, stem);
        var candidate = basePath + ".mid";

        var n = 1;
        while (File.Exists(candidate) || Jobs.Any(job => string.Equals(job.OutputPath, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{basePath} ({++n}).mid";
        }

        return candidate;
    }

    private static string MakeCompletedKey(string path, string outputFolder, FileInfo info)
    {
        var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var normalizedFolder = Path.GetFullPath(outputFolder).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
        var material = $"{normalizedPath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{normalizedFolder}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
    }

    private static string StageToChinese(string stage) => stage switch
    {
        "audio" => "读取音频",
        "inference" => "推理中",
        "postprocess" => "后处理",
        "write_midi" => "写入 MIDI",
        "完成" => "完成",
        _ => stage,
    };
}

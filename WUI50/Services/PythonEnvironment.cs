using System.Diagnostics;
using System.IO;
using System.Text;

namespace PianoTrans.WUI50.Services;

public sealed class PythonEnvironment
{
    public PythonEnvironment(string pythonExe, string workerScript, string checkpointPath)
    {
        PythonExe = pythonExe;
        WorkerScript = workerScript;
        CheckpointPath = checkpointPath;
    }

    public string PythonExe { get; }

    public string WorkerScript { get; }

    public string CheckpointPath { get; }

    public string WorkerDirectory => Path.GetDirectoryName(WorkerScript)!;

    public bool IsValid()
        => File.Exists(PythonExe) && File.Exists(WorkerScript) && File.Exists(CheckpointPath);

    public ProcessStartInfo CreateStartInfo(string manifestPath)
    {
        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = WorkerDirectory,
            StandardOutputEncoding = new UTF8Encoding(false),
            StandardErrorEncoding = new UTF8Encoding(false),
        };

        psi.ArgumentList.Add(WorkerScript);
        psi.ArgumentList.Add("--manifest");
        psi.ArgumentList.Add(manifestPath);

        psi.Environment["PYTHONUTF8"] = "1";
        psi.Environment["PYTHONSAFEPATH"] = "1";
        psi.Environment["PYTHONPATH"] = "";

        return psi;
    }

    public static PythonEnvironment Locate(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PythonExeOverride) &&
            !string.IsNullOrWhiteSpace(settings.WorkerScriptOverride))
        {
            var env = new PythonEnvironment(
                settings.PythonExeOverride,
                settings.WorkerScriptOverride,
                FindCheckpointNear(settings.WorkerScriptOverride));
            return env;
        }

        var start = AppContext.BaseDirectory;
        for (var dir = new DirectoryInfo(start); dir is not null; dir = dir.Parent)
        {
            var python = Path.Combine(dir.FullName, "venv50", "Scripts", "python.exe");
            var worker = Path.Combine(dir.FullName, "modern50", "PianoTrans-Worker.py");
            var checkpoint = Path.Combine(
                dir.FullName,
                "piano_transcription_inference_data",
                "note_F1=0.9677_pedal_F1=0.9186.pth");

            if (File.Exists(python) && File.Exists(worker) && File.Exists(checkpoint))
            {
                return new PythonEnvironment(python, worker, checkpoint);
            }
        }

        return new PythonEnvironment(
            Path.Combine(start, "venv50", "Scripts", "python.exe"),
            Path.Combine(start, "modern50", "PianoTrans-Worker.py"),
            Path.Combine(start, "piano_transcription_inference_data", "note_F1=0.9677_pedal_F1=0.9186.pth"));
    }

    private static string FindCheckpointNear(string workerScript)
    {
        var dir = Path.GetDirectoryName(workerScript);
        for (var d = dir is null ? null : new DirectoryInfo(dir); d is not null; d = d.Parent)
        {
            var checkpoint = Path.Combine(
                d.FullName,
                "piano_transcription_inference_data",
                "note_F1=0.9677_pedal_F1=0.9186.pth");
            if (File.Exists(checkpoint))
            {
                return checkpoint;
            }
        }

        return Path.Combine(AppContext.BaseDirectory, "note_F1=0.9677_pedal_F1=0.9186.pth");
    }
}

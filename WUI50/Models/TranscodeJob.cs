using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50.Models;

public sealed class TranscodeJob : INotifyPropertyChanged
{
    public TranscodeJob(string inputPath, string outputPath, string key)
    {
        InputPath = inputPath;
        OutputPath = outputPath;
        Key = key;
        FileName = Path.GetFileName(inputPath);
        Status = JobStatus.Waiting;
    }

    public string Key { get; }

    public string InputPath { get; }

    public string OutputPath { get; }

    public string FileName { get; }

    private JobStatus _status;
    public JobStatus Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(IsIndeterminate));
                OnPropertyChanged(nameof(IsFinished));
            }
        }
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set
        {
            if (SetProperty(ref _progress, value))
            {
                OnPropertyChanged(nameof(ProgressText));
            }
        }
    }

    private string _stage = "";
    public string Stage
    {
        get => _stage;
        set
        {
            if (SetProperty(ref _stage, value))
            {
                OnPropertyChanged(nameof(StageText));
            }
        }
    }

    private string _errorMessage = "";
    public string ErrorMessage
    {
        get => _errorMessage;
        set => SetProperty(ref _errorMessage, value);
    }

    private int _notes;
    public int Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private int _pedals;
    public int Pedals
    {
        get => _pedals;
        set => SetProperty(ref _pedals, value);
    }

    private int _filteredShortNotes;
    public int FilteredShortNotes
    {
        get => _filteredShortNotes;
        set => SetProperty(ref _filteredShortNotes, value);
    }

    private double _elapsedSeconds;
    public double ElapsedSeconds
    {
        get => _elapsedSeconds;
        set
        {
            if (SetProperty(ref _elapsedSeconds, value))
            {
                OnPropertyChanged(nameof(ElapsedText));
            }
        }
    }

    public string StatusText => LocalizationService.T(Status switch
    {
        JobStatus.Waiting => "等待中",
        JobStatus.Processing => "处理中",
        JobStatus.Completed => "已完成",
        JobStatus.Failed => "失败",
        _ => Status.ToString(),
    });

    public string StageText => LocalizationService.T(_stage);

    public bool IsIndeterminate => Status == JobStatus.Processing && Progress <= 0.5;

    public bool IsFinished => Status is JobStatus.Completed or JobStatus.Failed;

    public string ProgressText => $"{Progress:F0}%";

    public string ElapsedText => ElapsedSeconds > 0 ? $"{ElapsedSeconds:F1} s" : "";

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

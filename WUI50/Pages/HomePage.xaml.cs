using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using PianoTrans.WUI50.Models;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50.Pages;

public sealed partial class HomePage : Page
{
    private QueueManager Manager => App.Queue!;

    public HomePage()
    {
        InitializeComponent();
        JobsList.ItemsSource = Manager.Jobs;
        Manager.InfoOccurred += OnInfo;
        Manager.RunningChanged += OnRunningChanged;
        UpdateButtons();
    }

    private async void OpenFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker
        {
            ViewMode = PickerViewMode.List,
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
        };

        foreach (var ext in new[] { ".wav", ".mp3", ".flac", ".m4a", ".aac", ".ogg", ".wma", ".opus", ".mp4", ".m4v", ".mov", ".mkv", ".webm", ".avi" })
        {
            picker.FileTypeFilter.Add(ext);
        }

        InitializePicker(picker);
        var files = await picker.PickMultipleFilesAsync();
        if (files is { Count: > 0 })
        {
            Manager.AddFiles(files.Select(file => file.Path));
            UpdateButtons();
        }
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        UpdateButtons();
        _ = Manager.StartAsync();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        Manager.Stop();
        UpdateButtons();
    }

    private void ClearFinished_Click(object sender, RoutedEventArgs e)
    {
        Manager.ClearFinished();
        UpdateButtons();
    }

    private void RemoveJob_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is TranscodeJob job)
        {
            Manager.RemoveJob(job);
            UpdateButtons();
        }
    }

    private void Root_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "添加到转录队列";
            e.DragUIOverride.IsCaptionVisible = true;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private async void Root_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var files = items.OfType<StorageFile>().Select(file => file.Path).ToList();
            if (files.Count == 0)
            {
                ShowInfo("没有可添加的媒体文件。", InfoBarSeverity.Warning);
            }
            else
            {
                Manager.AddFiles(files);
                UpdateButtons();
            }
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void OnInfo(string message)
    {
        DispatcherQueue.TryEnqueue(() => ShowInfo(message, InfoBarSeverity.Informational));
    }

    private void OnRunningChanged()
    {
        DispatcherQueue.TryEnqueue(UpdateButtons);
    }

    private void UpdateButtons()
    {
        StartButton.IsEnabled = !Manager.IsRunning &&
            Manager.Jobs.Any(job => job.Status is JobStatus.Waiting or JobStatus.Failed);
        StopButton.IsEnabled = Manager.IsRunning;
        ClearButton.IsEnabled = Manager.Jobs.Any(job => job.Status is JobStatus.Completed or JobStatus.Failed);
    }

    private void ShowInfo(string message, InfoBarSeverity severity)
    {
        Info.Severity = severity;
        Info.Message = message;
        Info.IsOpen = true;
    }

    private static void InitializePicker(object picker)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
    }
}

using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage.Pickers;

namespace PianoTrans.WUI50.Pages;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();

        DeviceRadio.SelectedIndex = App.Settings.DeviceMode == "cpu" ? 1 : 0;
        OutputFolderBox.Text = App.Settings.OutputFolder;
        MinNoteBox.Value = App.Settings.MinNoteDurationSeconds;
    }

    private void DeviceRadio_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OutputFolderBox is null)
        {
            return;
        }

        App.Settings.DeviceMode = DeviceRadio.SelectedIndex == 1 ? "cpu" : "gpu";
        App.Settings.Save();
    }

    private void OutputFolderBox_LostFocus(object sender, RoutedEventArgs e)
    {
        SaveOutputFolder();
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.MusicLibrary,
        };
        picker.FileTypeFilter.Add("*");

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        _ = PickFolderAsync(picker);
    }

    private async Task PickFolderAsync(FolderPicker picker)
    {
        var folder = await picker.PickSingleFolderAsync();
        if (folder is null)
        {
            return;
        }

        OutputFolderBox.Text = folder.Path;
        SaveOutputFolder();
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        SaveOutputFolder();
        var folder = string.IsNullOrWhiteSpace(OutputFolderBox.Text)
            ? App.Settings.OutputFolder
            : OutputFolderBox.Text;

        try
        {
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{folder}\"",
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Info.Severity = InfoBarSeverity.Error;
            Info.Message = "无法打开输出文件夹：" + ex.Message;
            Info.IsOpen = true;
        }
    }

    private void MinNoteBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (OutputFolderBox is null || double.IsNaN(args.NewValue))
        {
            return;
        }

        App.Settings.MinNoteDurationSeconds = Math.Clamp(args.NewValue, 0.01, 2.0);
        App.Settings.Save();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SaveOutputFolder();
        App.Settings.MinNoteDurationSeconds = double.IsNaN(MinNoteBox.Value)
            ? 0.05
            : Math.Clamp(MinNoteBox.Value, 0.01, 2.0);
        App.Settings.Save();

        Info.Severity = InfoBarSeverity.Success;
        Info.Message = "设置已保存。";
        Info.IsOpen = true;
    }

    private void SaveOutputFolder()
    {
        var folder = OutputFolderBox.Text?.Trim() ?? "";
        if (folder.Length == 0)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(folder);
            App.Settings.OutputFolder = Path.GetFullPath(folder);
            OutputFolderBox.Text = App.Settings.OutputFolder;
            App.Settings.Save();
        }
        catch
        {
            Info.Severity = InfoBarSeverity.Error;
            Info.Message = "输出文件夹无效，请重新选择。";
            Info.IsOpen = true;
        }
    }
}

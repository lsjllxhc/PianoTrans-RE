using Microsoft.UI.Xaml.Controls;

namespace PianoTrans.WUI50.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        BackendStatus.Text = App.Queue?.PythonStatusText ?? "转录后端未初始化。";
    }
}

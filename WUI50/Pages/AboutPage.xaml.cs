using Microsoft.UI.Xaml.Controls;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        LogoLoader.Apply(LogoImage, 128);
        BackendStatus.Text = App.Queue?.PythonStatusText ?? "转录后端未初始化。";
    }
}

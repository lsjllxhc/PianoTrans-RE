using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50.Pages;

public sealed partial class AboutPage : Page
{
    public AboutPage()
    {
        InitializeComponent();
        LocalizationService.Register(this);
        LogoLoader.Apply(LogoImage, 128);
        BackendStatus.Text = App.Queue?.PythonStatusText ?? "转录后端未初始化。";
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LocalizationService.Apply(this);
    }
}

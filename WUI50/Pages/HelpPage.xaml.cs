using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PianoTrans.WUI50.Services;

namespace PianoTrans.WUI50.Pages;

public sealed partial class HelpPage : Page
{
    public HelpPage()
    {
        InitializeComponent();
        LocalizationService.Register(this);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        LocalizationService.Apply(this);
    }
}

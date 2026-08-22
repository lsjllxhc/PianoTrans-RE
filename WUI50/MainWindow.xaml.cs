using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Composition.SystemBackdrops;
using PianoTrans.WUI50.Pages;
using PianoTrans.WUI50.Services;
using Windows.Graphics;

namespace PianoTrans.WUI50;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        App.MainWindow = this;
        Title = "PianoTrans-RE";
        LogoLoader.ApplyWindowIcon(AppWindow);
        LocalizationService.Register(Root);

        SystemBackdrop = new MicaBackdrop
        {
            Kind = MicaKind.BaseAlt,
        };

        AppWindow.Resize(new SizeInt32(1180, 760));

        if (App.Queue is not null)
        {
            App.Queue.ErrorOccurred += OnQueueError;
        }

        Closed += (_, _) => App.Queue?.Stop();
        var navItems = Nav.MenuItems.OfType<NavigationViewItem>()
            .Concat(Nav.FooterMenuItems.OfType<NavigationViewItem>()).ToList();
        Nav.SelectedItem = navItems.FirstOrDefault(item => (string?)item.Tag == App.StartPage)
            ?? navItems.FirstOrDefault(item => (string?)item.Tag == "home");
    }

    private void Nav_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var item = args.SelectedItem as NavigationViewItem;
        var tag = item?.Tag as string;

        switch (tag)
        {
            case "home":
                ContentFrame.Navigate(typeof(HomePage));
                break;
            case "settings":
                ContentFrame.Navigate(typeof(SettingsPage));
                break;
            case "help":
                ContentFrame.Navigate(typeof(HelpPage));
                break;
            case "about":
                ContentFrame.Navigate(typeof(AboutPage));
                break;
        }
    }

    private void HelpFooter_Click(object sender, RoutedEventArgs e)
    {
        ContentFrame.Navigate(typeof(HelpPage));
    }

    private void OnQueueError(string message)
    {
        DispatcherQueue.TryEnqueue(() => _ = ShowErrorAsync(LocalizationService.T("转录失败"), message));
    }

    public async Task ShowErrorAsync(string title, string message)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = message,
                        TextWrapping = TextWrapping.Wrap,
                    },
                    MaxHeight = 400,
                },
                CloseButtonText = LocalizationService.T("关闭"),
                XamlRoot = Root.XamlRoot,
            };

            await dialog.ShowAsync();
        }
        catch
        {
            // Multiple dialogs can race if several jobs fail at once.
        }
    }
}

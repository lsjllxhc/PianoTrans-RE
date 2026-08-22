using System.IO;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;

namespace PianoTrans.WUI50.Services;

public static class LogoLoader
{
    /// <summary>
    /// Loads logo.png from the application folder, or from an ancestor
    /// folder when the app is started from a build output directory.
    /// Users can replace logo.png in the release root to change the logo.
    /// </summary>
    public static void Apply(Image image, int decodePixelWidth = 64)
    {
        try
        {
            var logoPath = FindLogoPath();
            if (logoPath is null)
            {
                return;
            }

            var bitmap = new BitmapImage
            {
                DecodePixelWidth = decodePixelWidth,
            };
            bitmap.UriSource = new Uri(logoPath, UriKind.Absolute);
            image.Source = bitmap;
        }
        catch
        {
            // A missing/broken logo must never break the app.
        }
    }

    public static void ApplyWindowIcon(AppWindow appWindow)
    {
        try
        {
            var iconPath = FindIconPath();
            if (iconPath is not null)
            {
                appWindow.SetIcon(iconPath);
            }
        }
        catch
        {
            // Some Windows versions do not support runtime icon changes.
        }
    }

    private static string? FindLogoPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var dir = current; dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "logo.png");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindIconPath()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var dir = current; dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "logo.ico");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}

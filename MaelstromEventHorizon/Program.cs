using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace MaelstromEventHorizon;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.Run(new GameWindow());
    }
}

internal sealed class GameWindow : Window
{
    private Rect windowedBounds;
    private bool fullScreen;

    public GameWindow()
    {
        DisplayPreferences display = DisplaySettings.Load();
        Title = "Maelstrom: Event Horizon";
        Width = 1280;
        Height = 800;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Black;
        SetWindowIcon();
        Content = new GameView(display.FullScreen, SetFullScreenFromMenu);
        ApplyFullScreen(display.FullScreen, false);

        SourceInitialized += (_, _) => ApplySystemTitleBarTheme();
        Activated += (_, _) => ApplySystemTitleBarTheme();
        Closed += (_, _) => Mouse.OverrideCursor = null;
    }

    private void SetFullScreenFromMenu(bool enabled)
    {
        ApplyFullScreen(enabled, true);
    }

    private void ApplyFullScreen(bool enabled, bool save)
    {
        fullScreen = enabled;
        if (enabled)
        {
            if (IsLoaded && WindowStyle != WindowStyle.None)
            {
                Rect bounds = RestoreBounds;
                if (bounds.Width >= MinWidth && bounds.Height >= MinHeight) windowedBounds = bounds;
            }

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            if (windowedBounds.Width >= MinWidth && windowedBounds.Height >= MinHeight)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = windowedBounds.Left;
                Top = windowedBounds.Top;
                Width = windowedBounds.Width;
                Height = windowedBounds.Height;
            }
            ApplySystemTitleBarTheme();
        }
        Cursor = enabled ? Cursors.None : Cursors.Arrow;
        Mouse.OverrideCursor = enabled ? Cursors.None : null;
        if (save) DisplaySettings.Save(enabled);
    }

    private void SetWindowIcon()
    {
        try
        {
            string path = GameAssets.PathFor("MaelstromEventHorizon.ico");
            if (!File.Exists(path)) return;
            Icon = BitmapFrame.Create(new Uri(path, UriKind.Absolute), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        }
        catch
        {
            // The executable icon remains available if the runtime asset cannot be read.
        }
    }

    private void ApplySystemTitleBarTheme()
    {
        if (fullScreen || WindowStyle == WindowStyle.None) return;
        try
        {
            nint handle = new WindowInteropHelper(this).Handle;
            if (handle == 0) return;
            int darkMode = SystemUsesDarkAppTheme() ? 1 : 0;
            if (DwmSetWindowAttribute(handle, 20, ref darkMode, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, 19, ref darkMode, sizeof(int));
        }
        catch
        {
            // Native Windows chrome still provides the correct fallback theme.
        }
    }

    private static bool SystemUsesDarkAppTheme()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        object? value = key?.GetValue("AppsUseLightTheme");
        return value is int appsUseLightTheme && appsUseLightTheme == 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}

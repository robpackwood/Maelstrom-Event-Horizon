using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Application.Services.Contracts;
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MaelstromEventHorizon.Presentation;

internal sealed class GameWindow : Window
{
    private readonly GameEngine game;
    private readonly IAssetProvider assets;
    private Rect windowedBounds;
    private bool fullScreen;

    public GameWindow(GameView view, GameEngine game, DisplayPreferences display, IAssetProvider assets)
    {
        this.game = game;
        this.assets = assets;
        Title = "Maelstrom - Event Horizon";
        Width = 1280;
        Height = 800;
        MinWidth = 900;
        MinHeight = 600;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Brushes.Black;
        SetWindowIcon();
        Content = view;
        ApplyFullScreen(display.FullScreen);

        this.game.FullScreenChanged += ApplyFullScreen;
        SourceInitialized += (_, _) => ApplySystemTitleBarTheme();
        Activated += (_, _) => ApplySystemTitleBarTheme();
        Closed += OnClosed;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        game.FullScreenChanged -= ApplyFullScreen;
        Mouse.OverrideCursor = null;
    }

    private void ApplyFullScreen(bool enabled)
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
    }

    private void SetWindowIcon()
    {
        try
        {
            string path = assets.PathFor("MaelstromEventHorizon.ico");
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
        return value is 0;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}

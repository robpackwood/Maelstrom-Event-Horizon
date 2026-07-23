using MaelstromEventHorizon.Presentation;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Media;

namespace MaelstromEventHorizon.Bootstrap;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.Default;
        var app = new System.Windows.Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        using ServiceProvider services = GameCompositionRoot.BuildServices();
        app.Run(services.GetRequiredService<GameWindow>());
    }
}

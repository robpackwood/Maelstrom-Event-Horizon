using System.Windows;
using System.Runtime;
using System.Windows.Interop;
using System.Windows.Media;
using MaelstromEventHorizon.Presentation;
using Microsoft.Extensions.DependencyInjection;

namespace MaelstromEventHorizon.Bootstrap;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        RenderOptions.ProcessRenderMode = RenderMode.Default;
        System.Windows.Application app = new() { ShutdownMode = ShutdownMode.OnMainWindowClose };
        using ServiceProvider services = GameCompositionRoot.BuildServices();
        app.Run(services.GetRequiredService<GameWindow>());
    }
}

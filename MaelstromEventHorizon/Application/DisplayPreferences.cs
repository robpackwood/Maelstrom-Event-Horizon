namespace MaelstromEventHorizon.Application;

internal sealed class DisplayPreferences
{
    public bool FullScreen { get; init; }
    public double MusicVolume { get; init; } = 1;
    public double EffectsVolume { get; init; } = .6;
    public int GraphicsQuality { get; init; } = 2;
    public bool BonusStagesEnabled { get; init; } = true;
    public bool BossFightsEnabled { get; init; } = true;
    // Zero uses every frame supplied by the display compositor.
    public int FrameRateLimit { get; init; }
}

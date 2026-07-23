namespace MaelstromEventHorizon.Application;

internal sealed class DisplayPreferences
{
    public bool FullScreen { get; init; }
    public double MusicVolume { get; init; } = 1;
    public double EffectsVolume { get; init; } = .6;
}

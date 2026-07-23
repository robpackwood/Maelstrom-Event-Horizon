using MaelstromEventHorizon.Application;
using MaelstromEventHorizon.Application.Services.Contracts;
using System.IO;
using System.Text.Json;

namespace MaelstromEventHorizon.Infrastructure.Persistence;

internal sealed class JsonDisplaySettingsStore(IAppDataPathProvider paths) : IDisplaySettingsStore
{
    public DisplayPreferences Load()
    {
        try
        {
            string path = paths.ReadPath("display.json");
            if (!File.Exists(path)) return new DisplayPreferences();
            DisplayPreferences preferences = JsonSerializer.Deserialize<DisplayPreferences>(File.ReadAllText(path))
                ?? new DisplayPreferences();
            return new DisplayPreferences
            {
                FullScreen = preferences.FullScreen,
                MusicVolume = Math.Clamp(preferences.MusicVolume, 0, 1),
                EffectsVolume = Math.Clamp(preferences.EffectsVolume, 0, 1)
            };
        }
        catch
        {
            return new DisplayPreferences();
        }
    }

    public void Save(DisplayPreferences preferences)
    {
        try
        {
            Directory.CreateDirectory(paths.DirectoryPath);
            File.WriteAllText(paths.WritePath("display.json"),
                JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Preference persistence must never prevent the game from changing modes.
        }
    }
}

using System.IO;
using System.Text.Json;

namespace MaelstromEventHorizon;

internal sealed record DisplayPreferences(bool FullScreen);

internal static class DisplaySettings
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaelstromEventHorizon");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "display.json");

    public static DisplayPreferences Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new DisplayPreferences(false);
            return JsonSerializer.Deserialize<DisplayPreferences>(File.ReadAllText(FilePath))
                ?? new DisplayPreferences(false);
        }
        catch
        {
            return new DisplayPreferences(false);
        }
    }

    public static void Save(bool fullScreen)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var preferences = new DisplayPreferences(fullScreen);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Display persistence must never prevent the game from changing modes.
        }
    }
}

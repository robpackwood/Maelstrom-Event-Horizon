using System.IO;
using System.Text.Json;

namespace MaelstromEventHorizon;

internal sealed record HighScoreEntry(string Name, int Score, int Wave, DateTime AchievedAt);

internal static class HighScoreStore
{
    private static readonly string DirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaelstromEventHorizon");
    private static readonly string FilePath = Path.Combine(DirectoryPath, "highscores.json");

    public static List<HighScoreEntry> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return (JsonSerializer.Deserialize<List<HighScoreEntry>>(File.ReadAllText(FilePath)) ?? [])
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.AchievedAt)
                .Take(10)
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    public static void Save(IEnumerable<HighScoreEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var topTen = entries.OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.AchievedAt)
                .Take(10)
                .ToList();
            File.WriteAllText(FilePath, JsonSerializer.Serialize(topTen, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // A locked profile should not prevent the game-over screen from working.
        }
    }
}

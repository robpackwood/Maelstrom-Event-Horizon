using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Scores;
using System.IO;
using System.Text.Json;

namespace MaelstromEventHorizon.Infrastructure.Persistence;

internal sealed class JsonHighScoreRepository(IAppDataPathProvider paths) : IHighScoreRepository
{
    public List<HighScoreEntry> Load()
    {
        try
        {
            string path = paths.ReadPath("highscores.json");
            if (!File.Exists(path)) return [];
            return (JsonSerializer.Deserialize<List<HighScoreEntry>>(File.ReadAllText(path)) ?? [])
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

    public void Save(IEnumerable<HighScoreEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(paths.DirectoryPath);
            var topTen = entries.OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.AchievedAt)
                .Take(10)
                .ToList();
            File.WriteAllText(paths.WritePath("highscores.json"),
                JsonSerializer.Serialize(topTen, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // A locked profile should not prevent the game-over screen from working.
        }
    }
}

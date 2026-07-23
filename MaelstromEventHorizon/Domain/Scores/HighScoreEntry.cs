namespace MaelstromEventHorizon.Domain.Scores;

internal sealed record HighScoreEntry(string Name, int Score, int Wave, DateTime AchievedAt);

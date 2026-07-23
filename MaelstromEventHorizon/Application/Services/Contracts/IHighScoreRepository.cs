using MaelstromEventHorizon.Domain.Scores;

namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IHighScoreRepository
{
    List<HighScoreEntry> Load();
    void Save(IEnumerable<HighScoreEntry> entries);
}

using MaelstromEventHorizon.Application.Services.Contracts;
using System.IO;

namespace MaelstromEventHorizon.Infrastructure.Persistence;

internal sealed class AppDataPathProvider : IAppDataPathProvider
{
    private const string CurrentFolder = "MaelstromEventHorizon";
    private const string LegacyFolder = "MaelstromDegenerateGreed";
    private readonly string localAppData =
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public string DirectoryPath => Path.Combine(localAppData, CurrentFolder);

    public string WritePath(string filename) => Path.Combine(DirectoryPath, filename);

    public string ReadPath(string filename)
    {
        string currentPath = WritePath(filename);
        if (File.Exists(currentPath)) return currentPath;

        string legacyPath = Path.Combine(localAppData, LegacyFolder, filename);
        if (!File.Exists(legacyPath)) return currentPath;
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.Copy(legacyPath, currentPath, false);
            return currentPath;
        }
        catch
        {
            return legacyPath;
        }
    }
}

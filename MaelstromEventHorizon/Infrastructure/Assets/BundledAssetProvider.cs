using System.IO;
using System.Reflection;
using MaelstromEventHorizon.Application.Services.Contracts;

namespace MaelstromEventHorizon.Infrastructure.Assets;

internal sealed class BundledAssetProvider : IAssetProvider
{
    private const string ResourcePrefix = "Assets/";

    private readonly string cacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MaelstromEventHorizon",
        "BundledAssets");

    private readonly Lock extractionGate = new();
    private bool extracted;

    public string PathFor(params string[] segments)
    {
        string loosePath = Path.Combine([AppContext.BaseDirectory, "Assets", .. segments]);

        if (File.Exists(loosePath))
        {
            return loosePath;
        }

        EnsureExtracted();
        return Path.Combine([cacheRoot, .. segments]);
    }

    private void EnsureExtracted()
    {
        lock (extractionGate)
        {
            if (extracted)
            {
                return;
            }

            Assembly assembly = typeof(BundledAssetProvider).Assembly;

            foreach (string resourceName in assembly.GetManifestResourceNames()
                         .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                string relativePath = resourceName[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(cacheRoot, relativePath));
                string root = Path.GetFullPath(cacheRoot) + Path.DirectorySeparatorChar;

                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using Stream? source = assembly.GetManifestResourceStream(resourceName);

                if (source is null)
                {
                    continue;
                }

                if (File.Exists(destination) && new FileInfo(destination).Length == source.Length)
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string temporary = destination + ".tmp";

                using (FileStream output = new(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    source.CopyTo(output);
                }

                File.Move(temporary, destination, true);
            }

            extracted = true;
        }
    }
}

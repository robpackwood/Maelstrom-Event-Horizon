using System.IO;
using System.Reflection;

namespace MaelstromEventHorizon;

internal static class GameAssets
{
    private const string ResourcePrefix = "Assets/";
    private static readonly object ExtractionGate = new();
    private static readonly string CacheRoot = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MaelstromEventHorizon", "BundledAssets");
    private static bool extracted;

    public static string PathFor(params string[] segments)
    {
        string loosePath = Path.Combine([AppContext.BaseDirectory, "Assets", .. segments]);
        if (File.Exists(loosePath)) return loosePath;

        EnsureExtracted();
        return Path.Combine([CacheRoot, .. segments]);
    }

    private static void EnsureExtracted()
    {
        if (extracted) return;
        lock (ExtractionGate)
        {
            if (extracted) return;

            Assembly assembly = typeof(GameAssets).Assembly;
            foreach (string resourceName in assembly.GetManifestResourceNames()
                         .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
            {
                string relativePath = resourceName[ResourcePrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
                string destination = Path.GetFullPath(Path.Combine(CacheRoot, relativePath));
                string root = Path.GetFullPath(CacheRoot) + Path.DirectorySeparatorChar;
                if (!destination.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

                using Stream? source = assembly.GetManifestResourceStream(resourceName);
                if (source is null) continue;
                if (File.Exists(destination) && new FileInfo(destination).Length == source.Length) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                string temporary = destination + ".tmp";
                using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                    source.CopyTo(output);
                File.Move(temporary, destination, true);
            }

            extracted = true;
        }
    }
}

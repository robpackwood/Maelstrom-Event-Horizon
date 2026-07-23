using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;
using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace MaelstromEventHorizon.Infrastructure.Persistence;

internal sealed class JsonControlBindingStore(IAppDataPathProvider paths) : IControlBindingStore
{
    public IReadOnlyDictionary<GameAction, Key> Load()
    {
        var result = new Dictionary<GameAction, Key>();
        try
        {
            string path = paths.ReadPath("controls.json");
            if (!File.Exists(path)) return result;
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (saved is null) return result;
            foreach ((string actionName, string keyName) in saved)
            {
                if (Enum.TryParse(actionName, out GameAction action) &&
                    Enum.TryParse(keyName, out Key key) && key != Key.None)
                    result[action] = key;
            }
        }
        catch
        {
            // Invalid or locked settings fall back to the built-in bindings.
        }
        return result;
    }

    public void Save(IReadOnlyDictionary<GameAction, Key> bindings)
    {
        try
        {
            string path = paths.WritePath("controls.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var data = bindings.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString());
            File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Input changes remain active for the current session if persistence fails.
        }
    }
}

using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace MaelstromEventHorizon;

internal enum GameAction
{
    TurnLeft,
    TurnRight,
    Thrust,
    Fire,
    Shield,
    Hyperspace,
    Pause,
    Quit
}

internal sealed class ControlBindings
{
    public static readonly GameAction[] Actions = Enum.GetValues<GameAction>();
    private readonly Dictionary<GameAction, Key> keys;

    public ControlBindings() => keys = Load();

    public Key this[GameAction action] => keys[action];

    public void Assign(GameAction action, Key key)
    {
        if (key == Key.None || key == Key.Escape && action != GameAction.Quit) return;
        Key oldKey = keys[action];
        GameAction? conflict = keys.FirstOrDefault(pair => pair.Value == key).Key;
        if (keys.Any(pair => pair.Value == key) && conflict is GameAction other && other != action)
            keys[other] = oldKey;
        keys[action] = key;
        Save();
    }

    public void Reset()
    {
        keys.Clear();
        foreach (var pair in Defaults()) keys[pair.Key] = pair.Value;
        Save();
    }

    public static string ActionName(GameAction action) => action switch
    {
        GameAction.TurnLeft => "TURN LEFT",
        GameAction.TurnRight => "TURN RIGHT",
        GameAction.Thrust => "THRUST",
        GameAction.Fire => "FIRE",
        GameAction.Shield => "SHIELD",
        GameAction.Hyperspace => "HYPERSPACE",
        GameAction.Pause => "PAUSE",
        GameAction.Quit => "QUIT",
        _ => action.ToString().ToUpperInvariant()
    };

    public static string KeyName(Key key) => key switch
    {
        Key.Left => "LEFT ARROW",
        Key.Right => "RIGHT ARROW",
        Key.Up => "UP ARROW",
        Key.Down => "DOWN ARROW",
        Key.Space => "SPACE",
        Key.Back => "BACKSPACE",
        _ => key.ToString().ToUpperInvariant()
    };

    private static Dictionary<GameAction, Key> Load()
    {
        var result = Defaults();
        try
        {
            string path = SettingsPath();
            if (!File.Exists(path)) return result;
            var saved = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path));
            if (saved is null) return result;
            foreach (var pair in saved)
            {
                if (Enum.TryParse(pair.Key, out GameAction action) && Enum.TryParse(pair.Value, out Key key) && key != Key.None)
                    result[action] = key;
            }
            if (result[GameAction.Quit] == Key.Q) result[GameAction.Quit] = Key.Escape;
        }
        catch { }
        return result;
    }

    private void Save()
    {
        try
        {
            string path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var data = keys.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value.ToString());
            File.WriteAllText(path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }

    private static string SettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MaelstromEventHorizon", "controls.json");

    private static Dictionary<GameAction, Key> Defaults() => new()
    {
        [GameAction.TurnLeft] = Key.Left,
        [GameAction.TurnRight] = Key.Right,
        [GameAction.Thrust] = Key.Space,
        [GameAction.Fire] = Key.Up,
        [GameAction.Shield] = Key.Down,
        [GameAction.Hyperspace] = Key.H,
        [GameAction.Pause] = Key.P,
        [GameAction.Quit] = Key.Escape
    };
}

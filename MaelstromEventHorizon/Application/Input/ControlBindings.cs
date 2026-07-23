using System.Windows.Input;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Input;

internal sealed class ControlBindings
{
    public static readonly GameAction[] Actions = Enum.GetValues<GameAction>();
    private readonly Dictionary<GameAction, Key> keys;
    private readonly IControlBindingStore store;

    public ControlBindings(IControlBindingStore store)
    {
        this.store = store;
        keys = Defaults();

        foreach ((GameAction action, Key key) in this.store.Load())
        {
            keys[action] = key;
        }

        if (keys[GameAction.Quit] == Key.Q)
        {
            keys[GameAction.Quit] = Key.Escape;
        }
    }

    public Key this[GameAction action] => keys[action];

    public void Assign(GameAction action, Key key)
    {
        if (key == Key.None || (key == Key.Escape && action != GameAction.Quit))
        {
            return;
        }

        Key oldKey = keys[action];

        GameAction? conflict = keys.Where(pair => pair.Value == key)
            .Select(pair => (GameAction?)pair.Key)
            .FirstOrDefault();

        if (conflict is { } other && other != action)
        {
            keys[other] = oldKey;
        }

        keys[action] = key;
        store.Save(keys);
    }

    public void Reset()
    {
        keys.Clear();

        foreach (KeyValuePair<GameAction, Key> pair in Defaults())
        {
            keys[pair.Key] = pair.Value;
        }

        store.Save(keys);
    }

    public static string ActionName(GameAction action)
    {
        return action switch
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
    }

    public static string KeyName(Key key)
    {
        return key switch
        {
            Key.Left => "LEFT ARROW",
            Key.Right => "RIGHT ARROW",
            Key.Up => "UP ARROW",
            Key.Down => "DOWN ARROW",
            Key.Space => "SPACE",
            Key.Back => "BACKSPACE",
            _ => key.ToString().ToUpperInvariant()
        };
    }

    private static Dictionary<GameAction, Key> Defaults()
    {
        return new Dictionary<GameAction, Key>
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
}

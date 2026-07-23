using MaelstromEventHorizon.Domain.Enums;
using System.Windows.Input;

namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IControlBindingStore
{
    IReadOnlyDictionary<GameAction, Key> Load();
    void Save(IReadOnlyDictionary<GameAction, Key> bindings);
}

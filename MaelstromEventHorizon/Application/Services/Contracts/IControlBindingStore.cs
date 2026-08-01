using System.Windows.Input;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IControlBindingStore
{
    IReadOnlyDictionary<GameAction, Key> Load();
    void Save(IReadOnlyDictionary<GameAction, Key> bindings);
}

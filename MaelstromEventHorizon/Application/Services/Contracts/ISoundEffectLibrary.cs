using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface ISoundEffectLibrary
{
    IReadOnlyDictionary<SoundCue, byte[]> Clips { get; }
}

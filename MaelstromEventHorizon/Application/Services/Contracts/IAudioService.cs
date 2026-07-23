using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Application.Services.Contracts;

internal interface IAudioService
{
    void StartTitleMusic();
    void StartWaveMusic(int wave, bool intense);
    void SetVolumes(double musicLevel, double effectsLevel);
    void StopMusic(bool stopEffects = true);
    void PauseAll();
    void ResumeAll();
    void Play(SoundCue cue, double volume = 1);
}

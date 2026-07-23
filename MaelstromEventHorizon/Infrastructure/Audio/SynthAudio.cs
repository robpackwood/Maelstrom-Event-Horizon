using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Security.Cryptography;
using System.Windows.Media;

namespace MaelstromEventHorizon.Infrastructure.Audio;

internal sealed class SynthAudio : IAudioService
{
    private sealed class LayeredEffectVoice
    {
        public readonly MediaPlayer Player = new();
        public bool Busy;
        public double RequestedVolume;
        public long StartedOrder;
    }

    private const int LayeredEffectVoiceCount = 16;
    private readonly IReadOnlyDictionary<SoundCue, byte[]> clips;
    private readonly MediaPlayer music = new();
    private readonly Lock playerGate = new();
    private readonly Dictionary<(SoundCue Cue, int Volume), (SoundPlayer Player, MemoryStream Stream)> effectPlayers = [];
    private readonly Dictionary<SoundCue, string> layeredEffectPaths = [];
    private readonly List<LayeredEffectVoice> layeredEffectVoices = [];
    private readonly string normalMusicPath;
    private readonly string bonusMusicPath;
    private readonly string[] waveMusicPaths;
    private string? openedMusicPath;
    private string? requestedMusicPath;
    private bool musicInitialized;
    private bool musicEndedHandlerAttached;
    private bool musicRequested;
    private bool audioPaused;
    private long layeredEffectOrder;
    private double requestedMusicVolume = .32;
    private double musicVolume = 1;
    private double effectsVolume = 1;
    private TimeSpan pausedMusicPosition;

    public SynthAudio(IAssetProvider assets, ISoundEffectLibrary soundEffects)
    {
        clips = soundEffects.Clips;
        normalMusicPath = assets.PathFor("through-the-universe.mp3");
        bonusMusicPath = assets.PathFor("singularity-action.mp3");
        waveMusicPaths =
        [
            assets.PathFor("Music", "wave-01-our-expanse.mp3"),
            assets.PathFor("Music", "wave-02-lift-off.mp3"),
            assets.PathFor("Music", "wave-03-building-a-colony.mp3"),
            assets.PathFor("Music", "wave-04-star-on-the-horizon.mp3"),
            assets.PathFor("Music", "wave-05-racing-through-asteroids.mp3"),
            assets.PathFor("Music", "wave-06-emergency.mp3"),
            assets.PathFor("Music", "wave-07-magic-space.mp3"),
            assets.PathFor("Music", "wave-08-the-calm-unknown.mp3"),
            assets.PathFor("Music", "wave-09-anti-entity.mp3"),
            assets.PathFor("Music", "wave-10-battle-in-outer-space.mp3")
        ];
        PrepareLayeredEffects();
    }

    private void StartMusic()
    {
        StartTrack(normalMusicPath, true);
    }

    public void StartTitleMusic()
    {
        StartTrack(normalMusicPath, true, .24);
    }

    private void StartBonusMusic() => StartTrack(File.Exists(bonusMusicPath) ? bonusMusicPath : normalMusicPath, true);

    public void StartWaveMusic(int wave, bool intense)
    {
        try
        {
            int trackIndex = (Math.Max(1, wave) - 1) % waveMusicPaths.Length;
            string path = waveMusicPaths[trackIndex];
            if (!File.Exists(path)) throw new FileNotFoundException("Wave _music asset was not extracted.", path);
            StartTrack(path, true, intense ? .36 : .30);
        }
        catch
        {
            if (intense) StartBonusMusic();
            else StartMusic();
        }
    }

    public void SetVolumes(double musicLevel, double effectsLevel)
    {
        lock (playerGate)
        {
            musicVolume = Math.Clamp(musicLevel, 0, 1);
            effectsVolume = Math.Clamp(effectsLevel, 0, 1);
            try { music.Volume = requestedMusicVolume * musicVolume; }
            catch (Exception exception) { TraceAudioFailure("set _music volume", exception); }
            RebalanceLayeredEffects();
        }
    }

    public void StopMusic(bool stopEffects = true)
    {
        musicRequested = false;
        audioPaused = false;
        pausedMusicPosition = TimeSpan.Zero;
        try { music.Stop(); }
        catch (Exception exception) { TraceAudioFailure("stop _music", exception); }
        if (stopEffects) StopActiveEffects();
    }

    public void PauseAll()
    {
        audioPaused = true;
        try
        {
            pausedMusicPosition = music.Position;
            music.Stop();
        }
        catch (Exception exception) { TraceAudioFailure("pause _music", exception); }
        StopActiveEffects();
    }

    public void ResumeAll()
    {
        audioPaused = false;
        if (!musicRequested) return;
        try
        {
            if (musicInitialized && string.Equals(openedMusicPath, requestedMusicPath, StringComparison.OrdinalIgnoreCase))
            {
                music.Position = pausedMusicPosition;
                music.Play();
            }
            else if (requestedMusicPath is not null && File.Exists(requestedMusicPath))
                OpenAndPlayMusic(requestedMusicPath, false);
        }
        catch (Exception exception) { TraceAudioFailure("resume _music", exception); }
    }

    private void StartTrack(string path, bool restart, double volume = .32)
    {
        bool wasRequested = musicRequested;
        bool trackChanged = !string.Equals(openedMusicPath, path, StringComparison.OrdinalIgnoreCase);
        requestedMusicPath = path;
        requestedMusicVolume = volume;
        musicRequested = true;
        audioPaused = false;
        pausedMusicPosition = TimeSpan.Zero;
        if (File.Exists(path)) OpenAndPlayMusic(path, restart || trackChanged || !wasRequested);
    }

    private void OpenAndPlayMusic(string path, bool restart)
    {
        try
        {
            if (!musicInitialized || !string.Equals(openedMusicPath, path, StringComparison.OrdinalIgnoreCase))
            {
                if (musicInitialized)
                {
                    music.Stop();
                    music.Close();
                }
                music.Open(new Uri(path, UriKind.Absolute));
                music.Volume = requestedMusicVolume * musicVolume;
                openedMusicPath = path;
                musicInitialized = true;
            }
            if (!musicEndedHandlerAttached)
            {
                music.MediaEnded += (_, _) =>
                {
                    if (!musicRequested || audioPaused) return;
                    music.Position = TimeSpan.Zero;
                    music.Play();
                };
                musicEndedHandlerAttached = true;
            }
            if (!audioPaused)
            {
                if (restart) music.Position = TimeSpan.Zero;
                music.Play();
            }
        }
        catch
        {
            // The game remains fully playable when a machine has no media device.
        }
    }

    public void Play(SoundCue cue, double volume = 1)
    {
        if (audioPaused || volume <= 0 || effectsVolume <= 0) return;
        if (!clips.TryGetValue(cue, out byte[]? source)) return;
        if (IsLayeredEffect(cue) && PlayLayeredEffect(cue, volume)) return;
        int volumeStep = (int)Math.Round(Math.Clamp(volume * effectsVolume, 0, 1) * 100);
        if (volumeStep <= 0) return;
        lock (playerGate)
        {
            try
            {
                if (audioPaused) return;
                var key = (cue, volumeStep);
                if (!effectPlayers.TryGetValue(key, out var cached))
                {
                    byte[] bytes = volumeStep >= 99 ? source : Scale(source, volumeStep / 100.0);
                    var stream = new MemoryStream(bytes, writable: false);
                    var player = new SoundPlayer(stream);
                    player.Load();
                    cached = (player, stream);
                    effectPlayers[key] = cached;
                }

                cached.Player.Stop();
                cached.Player.Play();
            }
            catch
            {
                // Audio must never interrupt the render loop.
            }
        }
    }

    private void StopActiveEffects()
    {
        lock (playerGate)
        {
            foreach (var cached in effectPlayers.Values)
            {
                try { cached.Player.Stop(); }
                catch (Exception exception) { TraceAudioFailure("stop sound effect", exception); }
            }
            foreach (LayeredEffectVoice voice in layeredEffectVoices)
            {
                try { voice.Player.Stop(); }
                catch (Exception exception) { TraceAudioFailure("stop layered effect", exception); }
                voice.Busy = false;
            }
        }
    }

    private void PrepareLayeredEffects()
    {
        try
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MaelstromEventHorizon", "EffectCache");
            Directory.CreateDirectory(root);
            foreach (SoundCue cue in new[]
                     { SoundCue.Explosion, SoundCue.AsteroidExplosion, SoundCue.GiantGrow, SoundCue.GiantShrink })
            {
                byte[] source = clips[cue];
                string fingerprint = Convert.ToHexString(SHA256.HashData(source).AsSpan(0, 8));
                string path = Path.Combine(root, $"{cue}-{fingerprint}.wav");
                if (!File.Exists(path)) File.WriteAllBytes(path, source);
                layeredEffectPaths[cue] = path;
            }

            for (int i = 0; i < LayeredEffectVoiceCount; i++)
            {
                var voice = new LayeredEffectVoice();
                voice.Player.MediaEnded += (_, _) => ReleaseLayeredVoice(voice);
                voice.Player.MediaFailed += (_, _) => ReleaseLayeredVoice(voice);
                layeredEffectVoices.Add(voice);
            }
        }
        catch
        {
            layeredEffectPaths.Clear();
            layeredEffectVoices.Clear();
        }
    }

    private bool PlayLayeredEffect(SoundCue cue, double volume)
    {
        if (!layeredEffectPaths.TryGetValue(cue, out string? path) || layeredEffectVoices.Count == 0)
            return false;

        lock (playerGate)
        {
            LayeredEffectVoice? voice = null;
            try
            {
                if (audioPaused) return true;
                voice = layeredEffectVoices.FirstOrDefault(candidate => !candidate.Busy)
                    ?? layeredEffectVoices.MinBy(candidate => candidate.StartedOrder)!;
                voice.Player.Stop();
                voice.Busy = true;
                voice.RequestedVolume = Math.Clamp(volume, 0, 1);
                voice.StartedOrder = ++layeredEffectOrder;
                voice.Player.Open(new Uri(path, UriKind.Absolute));
                voice.Player.Position = TimeSpan.Zero;
                RebalanceLayeredEffects();
                voice.Player.Play();
                return true;
            }
            catch
            {
                if (voice is not null) voice.Busy = false;
                return false;
            }
        }
    }

    private void ReleaseLayeredVoice(LayeredEffectVoice voice)
    {
        lock (playerGate)
        {
            voice.Busy = false;
            RebalanceLayeredEffects();
        }
    }

    private void RebalanceLayeredEffects()
    {
        int activeCount = layeredEffectVoices.Count(voice => voice.Busy);
        double headroom = activeCount <= 1
            ? 1
            : Math.Max(.4, 1 / Math.Sqrt(1 + (activeCount - 1) * .38));
        foreach (LayeredEffectVoice voice in layeredEffectVoices.Where(voice => voice.Busy))
            voice.Player.Volume = voice.RequestedVolume * effectsVolume * headroom;
    }

    private static bool IsLayeredEffect(SoundCue cue)
        => cue is SoundCue.Explosion or SoundCue.AsteroidExplosion or SoundCue.GiantGrow or SoundCue.GiantShrink;

    private static void TraceAudioFailure(string operation, Exception exception)
        => Trace.TraceWarning("Unable to {0}: {1}", operation, exception.Message);


    private static byte[] Scale(byte[] source, double volume)
    {
        var copy = (byte[])source.Clone();
        for (int i = 44; i + 1 < copy.Length; i += 2)
        {
            short value = BitConverter.ToInt16(copy, i);
            short scaled = (short)(value * Math.Clamp(volume, 0, 1));
            copy[i] = (byte)scaled;
            copy[i + 1] = (byte)(scaled >> 8);
        }
        return copy;
    }
}

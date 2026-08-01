using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;

namespace MaelstromEventHorizon.Infrastructure.Audio;

internal sealed class SynthAudio : IAudioService
{
    private sealed class LayeredEffectVoice
    {
        public readonly MediaPlayer Player = new();
        public bool Busy;
        public double RequestedVolume;
        public long StartedOrder;
        public DispatcherTimer? CutoffTimer;
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
    private readonly string bossMusicPath;
    private readonly string calmSummaryMusicPath;
    private readonly string celebrationSummaryMusicPath;
    private readonly string gameOverMusicPath;
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
        bonusMusicPath = assets.PathFor("Music", "singularity-action.mp3");
        bossMusicPath = assets.PathFor("Music", "boss-heavy-ominous.mp3");
        calmSummaryMusicPath = assets.PathFor("Music", "summary-calm-space-music.mp3");
        celebrationSummaryMusicPath = assets.PathFor("Music", "summary-celebration-our-expanse.mp3");
        gameOverMusicPath = assets.PathFor("Music", "game-over-alone.mp3");
        waveMusicPaths =
        [
            assets.PathFor("Music", "wave-12-gsf-discovery.mp3"),
            assets.PathFor("Music", "wave-02-lift-off.mp3"),
            assets.PathFor("Music", "singularity-action.mp3"),
            assets.PathFor("Music", "wave-04-star-on-the-horizon.mp3"),
            assets.PathFor("Music", "wave-05-racing-through-asteroids.mp3"),
            assets.PathFor("Music", "wave-06-emergency.mp3"),
            assets.PathFor("Music", "wave-07-magic-space.mp3"),
            assets.PathFor("Music", "wave-08-the-calm-unknown.mp3"),
            assets.PathFor("Music", "wave-09-anti-entity.mp3"),
            assets.PathFor("Music", "wave-10-battle-in-outer-space.mp3"),
            assets.PathFor("Music", "wave-11-outworld.mp3"), assets.PathFor("Music", "wave-13-joining-forces.mp3"),
            assets.PathFor("Music", "wave-18-robotic-soundtrack.mp3"),
            assets.PathFor("Music", "wave-19-anti-entity-original.mp3"), assets.PathFor("Music", "wave-20-stillness-of-space.mp3")
        ];
        PrepareLayeredEffects(assets);
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
        StopActiveEffects();
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

    public void StartSummaryMusic(bool earnedCashBonus)
    {
        StopActiveEffects();
        string preferred = earnedCashBonus ? celebrationSummaryMusicPath : calmSummaryMusicPath;
        string fallback = earnedCashBonus ? bonusMusicPath : normalMusicPath;
        StartTrack(File.Exists(preferred) ? preferred : fallback, true, earnedCashBonus ? .34 : .22);
    }

    public void StartGameOverMusic() => StartTrack(File.Exists(gameOverMusicPath) ? gameOverMusicPath : calmSummaryMusicPath, true, .26);

    public void StartBossMusic()
    {
        StartTrack(File.Exists(bossMusicPath) ? bossMusicPath : bonusMusicPath, true, .38);
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

    private void PrepareLayeredEffects(IAssetProvider assets)
    {
        try
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MaelstromEventHorizon", "EffectCache");
            Directory.CreateDirectory(root);
            foreach (SoundCue cue in new[]
                     {
                         SoundCue.Explosion, SoundCue.AsteroidExplosion, SoundCue.GiantGrow, SoundCue.GiantShrink,
                         SoundCue.EnemyWarning, SoundCue.BossAlarm, SoundCue.CashRegister, SoundCue.CashBonus,
                         SoundCue.ChaChing, SoundCue.CometCelebration, SoundCue.MultiplierWoohoo, SoundCue.RescueCelebration
                     })
            {
                byte[] source = clips[cue];
                string fingerprint = Convert.ToHexString(SHA256.HashData(source).AsSpan(0, 8));
                string path = Path.Combine(root, $"{cue}-{fingerprint}.wav");
                if (!File.Exists(path)) File.WriteAllBytes(path, source);
                layeredEffectPaths[cue] = path;
            }

            foreach (var recorded in new Dictionary<SoundCue, string>
                     {
                         [SoundCue.Fire] = "sfx_01a.wav", [SoundCue.EnemyFire] = "sfx_01b.wav",
                         [SoundCue.EnemyWarning] = "enemy-arrival-danger-alarm.wav", [SoundCue.BossAlarm] = "sfx_02d.wav",
                         [SoundCue.MenuMove] = "sfx_03a.wav", [SoundCue.Thrust] = "sfx_04a.wav",
                         [SoundCue.Explosion] = "sfx_05a.wav", [SoundCue.SteelHit] = "sfx_06.wav",
                         [SoundCue.Pickup] = "powerup-celebration.wav", [SoundCue.PowerupGotcha] = "powerup-male-victory.wav", [SoundCue.BonusVoice] = "bonus-male-voice.wav",
                         [SoundCue.Shield] = "shield-activation.wav",
                         [SoundCue.ShieldImpact] = "shield-impact.wav", [SoundCue.ReflectionBreak] = "sfx_05b.wav", [SoundCue.Nova] = "sfx_09a.wav",
                         [SoundCue.Wave] = "sfx_10a.wav", [SoundCue.Life] = "extra-ship-fanfare.wav",
                         [SoundCue.RescueCelebration] = "rescue-thank-you.wav", [SoundCue.Mine] = "sfx_12a.wav",
                         [SoundCue.Vortex] = "sfx_13c.wav", [SoundCue.CashRegister] = "sfx_14a.wav",
                         [SoundCue.Coin] = "bonus-coin-jingle.wav", [SoundCue.CashBonus] = "wave-bonus-yes-shout.wav",
                         [SoundCue.ChaChing] = "sfx_15b.wav", [SoundCue.CometCelebration] = "sfx_05c.wav",
                         [SoundCue.MultiplierWoohoo] = "sfx_16b.wav", [SoundCue.ShipCrash] = "player-death-dynamite.wav", [SoundCue.ShipBlast] = "sfx_17a.wav",
                         [SoundCue.BonusFailed] = "sfx_18a.wav", [SoundCue.GiantGrow] = "sfx_19a.wav",
                         [SoundCue.GiantShrink] = "sfx_19b.wav"
                        ,[SoundCue.VortexHit] = "sfx_13b.wav", [SoundCue.NovaHit] = "sfx_09b.wav",
                         [SoundCue.RicochetBounce] = "sfx_03b.wav"
                        ,[SoundCue.EnemyHit] = "sfx_06b.wav", [SoundCue.EnemyDestroyed] = "enemy-mechanical-explosion.wav",
                         [SoundCue.MineHit] = "sfx_12b.wav"
                        ,[SoundCue.SludgeMawHit] = "sludge-maw-gastric-hit.wav", [SoundCue.EyeTyrantHit] = "sfx_20a.wav",
                         [SoundCue.BoneBroodmotherHit] = "sfx_21a.wav", [SoundCue.VoidLeechHit] = "sfx_22a.wav",
                         [SoundCue.DreadHarvesterHit] = "sfx_20b.wav", [SoundCue.SolarWardenHit] = "sfx_21b.wav"
                        ,[SoundCue.CometSpawn] = "comet-arrival-fire-crackle.wav"
                        ,[SoundCue.SludgeMawFire] = "sfx_12c.wav", [SoundCue.EyeTyrantFire] = "sfx_01c.wav",
                         [SoundCue.BoneBroodmotherFire] = "sfx_02b.wav", [SoundCue.VoidLeechFire] = "sfx_02c.wav",
                         [SoundCue.DreadHarvesterFire] = "sfx_20c.wav", [SoundCue.SolarWardenFire] = "sfx_21b.wav"
                     })
            {
                string path = assets.PathFor("SoundEffects", recorded.Value);
                // Individually curated CC0 recordings: only the events that benefit from a real,
                // distinctive sound replace their prior synthesized cue.
                if (File.Exists(path) && recorded.Key is SoundCue.EnemyFire or
                    SoundCue.EnemyWarning or SoundCue.BossAlarm or SoundCue.Pickup or SoundCue.PowerupGotcha or SoundCue.BonusVoice or SoundCue.Shield or SoundCue.Nova or
                    SoundCue.Vortex or SoundCue.Life or SoundCue.RescueCelebration or SoundCue.CashBonus or
                    SoundCue.ChaChing or SoundCue.CometCelebration or SoundCue.CometSpawn or SoundCue.MultiplierWoohoo or
                    SoundCue.BonusFailed or SoundCue.GiantGrow or SoundCue.GiantShrink or SoundCue.Shield or
                    SoundCue.ShieldImpact or SoundCue.ReflectionBreak or SoundCue.VortexHit or SoundCue.NovaHit or SoundCue.RicochetBounce or
                    SoundCue.EnemyHit or SoundCue.EnemyDestroyed or SoundCue.MineHit or SoundCue.ShipCrash or SoundCue.ShipBlast or
                    SoundCue.Coin or SoundCue.SludgeMawHit or SoundCue.EyeTyrantHit or SoundCue.BoneBroodmotherHit or
                    SoundCue.VoidLeechHit or SoundCue.DreadHarvesterHit or SoundCue.SolarWardenHit or SoundCue.CometSpawn or
                    SoundCue.SludgeMawFire or SoundCue.EyeTyrantFire or SoundCue.BoneBroodmotherFire or
                    SoundCue.VoidLeechFire or SoundCue.DreadHarvesterFire or SoundCue.SolarWardenFire)
                    layeredEffectPaths[recorded.Key] = path;
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
                voice = layeredEffectVoices[0];
                for (int i = 0; i < layeredEffectVoices.Count; i++)
                {
                    LayeredEffectVoice candidate = layeredEffectVoices[i];
                    if (!candidate.Busy) { voice = candidate; break; }
                    if (candidate.StartedOrder < voice.StartedOrder) voice = candidate;
                }
                voice.Player.Stop();
                voice.Busy = true;
                voice.RequestedVolume = Math.Clamp(volume, 0, 1);
                voice.StartedOrder = ++layeredEffectOrder;
                voice.Player.Open(new Uri(path, UriKind.Absolute));
                voice.Player.Position = TimeSpan.Zero;
                RebalanceLayeredEffects();
                voice.Player.Play();
                ScheduleEffectCutoff(voice, cue);
                return true;
            }
            catch
            {
                if (voice is not null) voice.Busy = false;
                return false;
            }
        }
    }

    private void ScheduleEffectCutoff(LayeredEffectVoice voice, SoundCue cue)
    {
        double seconds = cue is SoundCue.Pickup ? .75 : cue is SoundCue.PowerupGotcha or SoundCue.BonusVoice ? 1.1 : cue is SoundCue.ShieldImpact ? .55 : cue is SoundCue.RescueCelebration ? .9 : cue is SoundCue.SludgeMawHit ? .19 : cue is SoundCue.EyeTyrantHit or
            SoundCue.BoneBroodmotherHit or SoundCue.VoidLeechHit or SoundCue.DreadHarvesterHit or SoundCue.SolarWardenHit ? .15 : 0;
        if (seconds <= 0) return;

        voice.CutoffTimer?.Stop();
        long order = voice.StartedOrder;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        voice.CutoffTimer = timer;
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (voice.StartedOrder != order) return;
            voice.Player.Stop();
            ReleaseLayeredVoice(voice);
        };
        timer.Start();
    }

    private void ReleaseLayeredVoice(LayeredEffectVoice voice)
    {
        lock (playerGate)
        {
            voice.Busy = false;
            voice.CutoffTimer?.Stop();
            RebalanceLayeredEffects();
        }
    }

    private void RebalanceLayeredEffects()
    {
        int activeCount = 0;
        for (int i = 0; i < layeredEffectVoices.Count; i++) if (layeredEffectVoices[i].Busy) activeCount++;
        double headroom = activeCount <= 1
            ? 1
            : Math.Max(.4, 1 / Math.Sqrt(1 + (activeCount - 1) * .38));
        for (int i = 0; i < layeredEffectVoices.Count; i++)
        {
            LayeredEffectVoice voice = layeredEffectVoices[i];
            if (voice.Busy) voice.Player.Volume = voice.RequestedVolume * effectsVolume * headroom;
        }
    }

    private bool IsLayeredEffect(SoundCue cue) => layeredEffectPaths.ContainsKey(cue);

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

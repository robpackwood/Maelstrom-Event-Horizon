using System.Diagnostics;
using System.IO;
using System.Media;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Threading;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Infrastructure.Audio;

internal sealed class SynthAudio : IAudioService
{
    private const int LayeredEffectVoiceCount = 24;
    private const int TitleMusicWave = 13;
    private readonly string bonusMusicPath;
    private readonly string bossMusicPath;
    private readonly string calmSummaryMusicPath;
    private readonly string celebrationSummaryMusicPath;
    private readonly IReadOnlyDictionary<SoundCue, byte[]> clips;

    private readonly Dictionary<(SoundCue Cue, int Volume), (SoundPlayer Player, MemoryStream Stream)> effectPlayers =
        [];

    private readonly string gameOverMusicPath;
    private readonly Dictionary<SoundCue, string> layeredEffectPaths = [];
    private readonly List<LayeredEffectVoice> layeredEffectVoices = [];
    private readonly MediaPlayer music = new();
    private readonly string normalMusicPath;
    private readonly MediaPlayer thrustLoop = new();
    private readonly MediaPlayer thrustLoopSecondary = new();
    private readonly DispatcherTimer thrustLoopTimer = new() { Interval = TimeSpan.FromSeconds(.66) };
    private readonly string titleMusicPath;
    private readonly Lock playerGate = new();
    private readonly string[] waveMusicPaths;
    private bool audioPaused;
    private double effectsVolume = 1;
    private long layeredEffectOrder;
    private bool musicEndedHandlerAttached;
    private bool musicInitialized;
    private bool musicRequested;
    private double musicVolume = 1;
    private string? openedMusicPath;
    private TimeSpan pausedMusicPosition;
    private TimeSpan requestedMusicLoopStart;
    private string? requestedMusicPath;
    private double requestedMusicVolume = .32;
    private bool thrustLoopActive;
    private bool thrustLoopInitialized;
    private bool thrustLoopUsesSecondary;
    private double thrustIntensity;

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
            assets.PathFor("Music", "wave-19-anti-entity-original.mp3"),
            assets.PathFor("Music", "wave-20-stillness-of-space.mp3")
        ];
        titleMusicPath = waveMusicPaths[TitleMusicWave - 1];

        PrepareLayeredEffects(assets);
        thrustLoopTimer.Tick += (_, _) => StartNextThrustSegment();
    }

    public void StartTitleMusic()
    {
        StartTrack(File.Exists(titleMusicPath) ? titleMusicPath : normalMusicPath, true, .30);
    }

    public void StartWaveMusic(int wave, bool intense)
    {
        StopActiveEffects();

        try
        {
            int trackIndex = (Math.Max(1, wave) - 1) % waveMusicPaths.Length;
            string path = waveMusicPaths[trackIndex];

            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Wave _music asset was not extracted.", path);
            }

            StartTrack(path, true, intense ? .36 : .30);
        }
        catch
        {
            if (intense)
            {
                StartBonusMusic();
            }
            else
            {
                StartMusic();
            }
        }
    }

    public void StartSummaryMusic(bool earnedCashBonus)
    {
        StopActiveEffects();
        string preferred = earnedCashBonus ? celebrationSummaryMusicPath : calmSummaryMusicPath;
        string fallback = earnedCashBonus ? bonusMusicPath : normalMusicPath;
        StartTrack(File.Exists(preferred) ? preferred : fallback, true, earnedCashBonus ? .34 : .22);
    }

    public void StartGameOverMusic()
    {
        StartTrack(File.Exists(gameOverMusicPath) ? gameOverMusicPath : calmSummaryMusicPath, true, .26);
    }

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

            try
            {
                music.Volume = requestedMusicVolume * musicVolume;
            }
            catch (Exception exception)
            {
                TraceAudioFailure("set _music volume", exception);
            }

            RebalanceLayeredEffects();

            if (thrustLoopActive)
            {
                thrustLoop.Volume = thrustIntensity * effectsVolume;
                thrustLoopSecondary.Volume = thrustIntensity * effectsVolume;
            }
        }
    }

    public void StopMusic(bool stopEffects = true)
    {
        musicRequested = false;
        audioPaused = false;
        pausedMusicPosition = TimeSpan.Zero;

        try
        {
            music.Stop();
        }
        catch (Exception exception)
        {
            TraceAudioFailure("stop _music", exception);
        }

        if (stopEffects)
        {
            StopActiveEffects();
        }
    }

    public void PauseAll()
    {
        audioPaused = true;

        try
        {
            pausedMusicPosition = music.Position;
            music.Stop();
        }
        catch (Exception exception)
        {
            TraceAudioFailure("pause _music", exception);
        }

        StopActiveEffects();
    }

    public void ResumeAll()
    {
        audioPaused = false;

        if (!musicRequested)
        {
            return;
        }

        try
        {
            if (musicInitialized &&
                string.Equals(openedMusicPath, requestedMusicPath, StringComparison.OrdinalIgnoreCase))
            {
                music.Position = pausedMusicPosition;
                music.Play();
            }
            else if (requestedMusicPath is not null && File.Exists(requestedMusicPath))
            {
                OpenAndPlayMusic(requestedMusicPath, false);
            }
        }
        catch (Exception exception)
        {
            TraceAudioFailure("resume _music", exception);
        }
    }

    public void Play(SoundCue cue, double volume = 1)
    {
        if (cue is SoundCue.ShipCrash or SoundCue.EnemyDestroyed)
        {
            volume *= .75;
        }

        double categoryVolume = effectsVolume;

        if (audioPaused || volume <= 0 || categoryVolume <= 0)
        {
            return;
        }

        if (!clips.TryGetValue(cue, out byte[]? source))
        {
            return;
        }

        if (IsLayeredEffect(cue) && PlayLayeredEffect(cue, volume * categoryVolume))
        {
            return;
        }

        int volumeStep = (int)Math.Round(Math.Clamp(volume * categoryVolume, 0, 1) * 100);

        if (volumeStep <= 0)
        {
            return;
        }

        lock (playerGate)
        {
            try
            {
                if (audioPaused)
                {
                    return;
                }

                (SoundCue cue, int volumeStep) key = (cue, volumeStep);

                if (!effectPlayers.TryGetValue(key, out (SoundPlayer Player, MemoryStream Stream) cached))
                {
                    byte[] bytes = volumeStep >= 99 ? source : Scale(source, volumeStep / 100.0);
                    MemoryStream stream = new(bytes, false);
                    SoundPlayer player = new(stream);
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

    public void SetThrustIntensity(double intensity)
    {
        lock (playerGate)
        {
            thrustIntensity = Math.Clamp(intensity, 0, .73);

            if (audioPaused || thrustIntensity <= 0 ||
                !layeredEffectPaths.TryGetValue(SoundCue.Thrust, out string? path))
            {
                StopThrustLoop();
                return;
            }

            try
            {
                if (!thrustLoopInitialized)
                {
                    thrustLoop.Open(new Uri(path, UriKind.Absolute));
                    thrustLoopSecondary.Open(new Uri(path, UriKind.Absolute));
                    thrustLoopInitialized = true;
                }

                thrustLoop.Volume = thrustIntensity * effectsVolume;
                thrustLoopSecondary.Volume = thrustIntensity * effectsVolume;

                if (!thrustLoopActive)
                {
                    thrustLoop.Position = TimeSpan.Zero;
                    thrustLoop.Play();
                    thrustLoopActive = true;
                    thrustLoopUsesSecondary = false;
                    thrustLoopTimer.Start();
                }
            }
            catch
            {
                StopThrustLoop();
            }
        }
    }

    private void StartMusic()
    {
        StartTrack(normalMusicPath, true);
    }

    private void StartBonusMusic()
    {
        StartTrack(File.Exists(bonusMusicPath) ? bonusMusicPath : normalMusicPath, true);
    }

    private void StartTrack(string path, bool restart, double volume = .32, TimeSpan? loopStart = null)
    {
        bool wasRequested = musicRequested;
        bool trackChanged = !string.Equals(openedMusicPath, path, StringComparison.OrdinalIgnoreCase);

        if (trackChanged)
        {
            try
            {
                music.Stop();
            }
            catch (Exception exception)
            {
                TraceAudioFailure("stop previous _music", exception);
            }
        }

        requestedMusicPath = path;
        requestedMusicVolume = volume;
        requestedMusicLoopStart = loopStart ?? TimeSpan.Zero;
        musicRequested = true;
        audioPaused = false;
        pausedMusicPosition = TimeSpan.Zero;

        if (File.Exists(path))
        {
            OpenAndPlayMusic(path, restart || trackChanged || !wasRequested);
        }
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
                    if (!musicRequested || audioPaused)
                    {
                        return;
                    }

                    music.Position = requestedMusicLoopStart;
                    music.Play();
                };
                music.MediaFailed += (_, _) =>
                {
                    if (string.Equals(requestedMusicPath, titleMusicPath, StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(normalMusicPath))
                    {
                        StartTrack(normalMusicPath, true, .30);
                    }
                };
                musicEndedHandlerAttached = true;
            }

            if (!audioPaused)
            {
                if (restart)
                {
                    music.Position = requestedMusicLoopStart;
                }

                music.Play();
            }
        }
        catch
        {
            // The game remains fully playable when a machine has no media device.
        }
    }

    private void StopActiveEffects()
    {
        lock (playerGate)
        {
            foreach ((SoundPlayer Player, MemoryStream Stream) cached in effectPlayers.Values)
            {
                try
                {
                    cached.Player.Stop();
                }
                catch (Exception exception)
                {
                    TraceAudioFailure("stop sound effect", exception);
                }
            }

            foreach (LayeredEffectVoice voice in layeredEffectVoices)
            {
                try
                {
                    voice.Player.Stop();
                }
                catch (Exception exception)
                {
                    TraceAudioFailure("stop layered effect", exception);
                }

                voice.Busy = false;
            }

            StopThrustLoop();
        }
    }

    private void StartNextThrustSegment()
    {
        lock (playerGate)
        {
            if (!thrustLoopActive || audioPaused)
            {
                return;
            }

            MediaPlayer next = thrustLoopUsesSecondary ? thrustLoop : thrustLoopSecondary;
            thrustLoopUsesSecondary = !thrustLoopUsesSecondary;
            next.Stop();
            next.Position = TimeSpan.Zero;
            next.Volume = thrustIntensity * effectsVolume;
            next.Play();
        }
    }

    private void StopThrustLoop()
    {
        if (!thrustLoopActive)
        {
            return;
        }

        try
        {
            thrustLoopTimer.Stop();
            thrustLoop.Stop();
            thrustLoopSecondary.Stop();
        }
        catch (Exception exception)
        {
            TraceAudioFailure("stop thrust loop", exception);
        }

        thrustLoopActive = false;
    }

    private void PrepareLayeredEffects(IAssetProvider assets)
    {
        try
        {
            string root = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MaelstromEventHorizon", "EffectCache");

            Directory.CreateDirectory(root);

            foreach (SoundCue cue in Enum.GetValues<SoundCue>())
            {
                if (!clips.TryGetValue(cue, out byte[]? source))
                {
                    continue;
                }

                string fingerprint = Convert.ToHexString(SHA256.HashData(source).AsSpan(0, 8));
                string path = Path.Combine(root, $"{cue}-{fingerprint}.wav");

                if (!File.Exists(path))
                {
                    File.WriteAllBytes(path, source);
                }

                layeredEffectPaths[cue] = path;
            }

            foreach (KeyValuePair<SoundCue, string> recorded in new Dictionary<SoundCue, string>
                     {
                         [SoundCue.Fire] = "sfx_01a.wav",
                         [SoundCue.EnemyFire] = "sfx_01b.wav",
                         [SoundCue.EnemyWarning] = "enemy-arrival-danger-alarm.wav",
                         [SoundCue.BossAlarm] = "sfx_02d.wav",
                         [SoundCue.MenuMove] = "sfx_03a.wav",
                         [SoundCue.Explosion] = "sfx_05a.wav",
                         [SoundCue.SteelHit] = "sfx_06.wav",
                         [SoundCue.Pickup] = "powerup-celebration.wav",
                         [SoundCue.BonusVoice] = "bonus-male-voice.wav",
                         [SoundCue.AnnouncerWarning] = "human-look-out.wav",
                         [SoundCue.AnnouncerBossDown] = "human-target-destroyed.wav",
                         [SoundCue.AnnouncerExtraShip] = "human-level-up.wav",
                         [SoundCue.AnnouncerWaveStart] = "human-ready-set-go.wav",
                         [SoundCue.AnnouncerEnemyInbound] = "human-target-engaged.wav",
                         [SoundCue.AnnouncerEnemyAssault] = "human-call-for-backup.wav",
                         [SoundCue.AnnouncerMineAlert] = "human-look-out.wav",
                         [SoundCue.AnnouncerBlackHoleAlert] = "human-look-out.wav",
                         [SoundCue.AnnouncerNovaAlert] = "human-look-out.wav",
                         [SoundCue.AnnouncerItemBox] = "human-power-up.wav",
                         [SoundCue.AnnouncerCometStorm] = "human-go-go-go.wav",
                         [SoundCue.AnnouncerBonusComet] = "human-go-go-go.wav",
                         [SoundCue.AnnouncerAirBrakes] = "human-power-up.wav",
                         [SoundCue.AnnouncerLuck] = "human-power-up.wav",
                         [SoundCue.AnnouncerTripleFire] = "human-power-up.wav",
                         [SoundCue.AnnouncerRiftVolley] = "human-power-up.wav",
                         [SoundCue.AnnouncerLongRange] = "human-power-up.wav",
                         [SoundCue.AnnouncerShields] = "human-power-up.wav",
                         [SoundCue.AnnouncerReflectionShield] = "human-power-up.wav",
                         [SoundCue.AnnouncerTimeFreeze] = "human-hold.wav",
                         [SoundCue.AnnouncerSmartBomb] = "human-fire-in-the-hole.wav",
                         [SoundCue.AnnouncerRicochetArena] = "human-suppressing-fire.wav",
                         [SoundCue.AnnouncerGiantShip] = "human-level-up.wav",
                         [SoundCue.Shield] = "shield-activation.wav",
                         [SoundCue.ShieldImpact] = "shield-impact.wav",
                         [SoundCue.ShieldSave] = "shield-save-nice.mp3",
                         [SoundCue.ReflectionBreak] = "sfx_05b.wav",
                         [SoundCue.Nova] = "sfx_09a.wav",
                         [SoundCue.Wave] = "sfx_10a.wav",
                         [SoundCue.Life] = "extra-ship-fanfare.wav",
                         [SoundCue.RescueCelebration] = "rescue-thank-you.wav",
                         [SoundCue.Mine] = "sfx_12a.wav",
                         [SoundCue.Vortex] = "sfx_13c.wav",
                         [SoundCue.CashRegister] = "sfx_14a.wav",
                         [SoundCue.Coin] = "bonus-coin-jingle.wav",
                         [SoundCue.ChaChing] = "sfx_15b.wav",
                         [SoundCue.CometCelebration] = "sfx_05c.wav",
                         [SoundCue.ShipCrash] = "player-ship-heavy-explosion.wav",
                         [SoundCue.ShipBlast] = "sfx_17a.wav",
                         [SoundCue.BonusFailed] = "sfx_18a.wav",
                         [SoundCue.GiantGrow] = "sfx_19a.wav",
                         [SoundCue.GiantShrink] = "sfx_19b.wav",
                         [SoundCue.VortexHit] = "sfx_13b.wav",
                         [SoundCue.NovaHit] = "sfx_09b.wav",
                         [SoundCue.RicochetBounce] = "sfx_03b.wav",
                         [SoundCue.EnemyHit] = "sfx_06b.wav",
                         [SoundCue.EnemyDestroyed] = "enemy-mechanical-explosion.wav",
                         [SoundCue.MineHit] = "sfx_12b.wav",
                         [SoundCue.SludgeMawHit] = "sludge-maw-gastric-hit.wav",
                         [SoundCue.EyeTyrantHit] = "sfx_20a.wav",
                         [SoundCue.BoneBroodmotherHit] = "sfx_21a.wav",
                         [SoundCue.VoidLeechHit] = "sfx_22a.wav",
                         [SoundCue.DreadHarvesterHit] = "sfx_20b.wav",
                         [SoundCue.SolarWardenHit] = "sfx_21b.wav",
                         [SoundCue.CometSpawn] = "comet-arrival-fire-crackle.wav",
                         [SoundCue.SludgeMawFire] = "sfx_12c.wav",
                         [SoundCue.EyeTyrantFire] = "sfx_01c.wav",
                         [SoundCue.BoneBroodmotherFire] = "sfx_02b.wav",
                         [SoundCue.VoidLeechFire] = "sfx_02c.wav",
                         [SoundCue.DreadHarvesterFire] = "sfx_20c.wav",
                         [SoundCue.SolarWardenFire] = "sfx_21b.wav"
                     })
            {
                string path = assets.PathFor("SoundEffects", recorded.Value);

                // Individually curated CC0 recordings: only the events that benefit from a real,
                // distinctive sound replace their prior synthesized cue.
                if (File.Exists(path) && recorded.Key is SoundCue.EnemyFire or
                        SoundCue.EnemyWarning or SoundCue.BossAlarm or SoundCue.Pickup or SoundCue.BonusVoice or
                        SoundCue.AnnouncerWarning or SoundCue.AnnouncerBossDown or SoundCue.AnnouncerExtraShip or
                        SoundCue.Shield or SoundCue.Nova or SoundCue.AnnouncerWaveStart or
                        SoundCue.AnnouncerEnemyInbound or SoundCue.AnnouncerEnemyAssault or
                        SoundCue.AnnouncerMineAlert or SoundCue.AnnouncerBlackHoleAlert or
                        SoundCue.AnnouncerNovaAlert or SoundCue.AnnouncerItemBox or SoundCue.AnnouncerCometStorm or
                        SoundCue.AnnouncerBonusComet or SoundCue.AnnouncerAirBrakes or SoundCue.AnnouncerLuck or
                        SoundCue.AnnouncerTripleFire or SoundCue.AnnouncerRiftVolley or SoundCue.AnnouncerLongRange or
                        SoundCue.AnnouncerShields or SoundCue.AnnouncerReflectionShield or
                        SoundCue.AnnouncerTimeFreeze or SoundCue.AnnouncerSmartBomb or
                        SoundCue.AnnouncerRicochetArena or SoundCue.AnnouncerGiantShip or SoundCue.Vortex or
                        SoundCue.Life or SoundCue.RescueCelebration or SoundCue.ChaChing or
                        SoundCue.CometCelebration or SoundCue.CometSpawn or SoundCue.MultiplierWoohoo or
                        SoundCue.BonusFailed or SoundCue.GiantGrow or SoundCue.GiantShrink or
                        SoundCue.ShieldImpact or SoundCue.ShieldSave or SoundCue.ReflectionBreak or SoundCue.VortexHit or
                        SoundCue.NovaHit or SoundCue.RicochetBounce or SoundCue.EnemyHit or SoundCue.EnemyDestroyed or
                        SoundCue.MineHit or SoundCue.ShipCrash or SoundCue.ShipBlast or SoundCue.Coin or
                        SoundCue.SludgeMawHit or SoundCue.EyeTyrantHit or SoundCue.BoneBroodmotherHit or
                        SoundCue.VoidLeechHit or SoundCue.DreadHarvesterHit or SoundCue.SolarWardenHit or
                        SoundCue.SludgeMawFire or SoundCue.EyeTyrantFire or SoundCue.BoneBroodmotherFire or
                        SoundCue.VoidLeechFire or SoundCue.DreadHarvesterFire or SoundCue.SolarWardenFire)
                {
                    layeredEffectPaths[recorded.Key] = path;
                }
            }

            for (int i = 0; i < LayeredEffectVoiceCount; i++)
            {
                LayeredEffectVoice voice = new();
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
        {
            return false;
        }

        lock (playerGate)
        {
            LayeredEffectVoice? voice = null;

            try
            {
                if (audioPaused)
                {
                    return true;
                }

                voice = layeredEffectVoices[0];

                for (int i = 0; i < layeredEffectVoices.Count; i++)
                {
                    LayeredEffectVoice candidate = layeredEffectVoices[i];

                    if (!candidate.Busy)
                    {
                        voice = candidate;
                        break;
                    }

                    if (candidate.StartedOrder < voice.StartedOrder)
                    {
                        voice = candidate;
                    }
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
                voice?.Busy = false;
                return false;
            }
        }
    }


    private void ScheduleEffectCutoff(LayeredEffectVoice voice, SoundCue cue)
    {
        double seconds = cue is SoundCue.Pickup ? .75 :
            cue is SoundCue.BonusVoice ? 1.1 :
            cue is SoundCue.ShieldImpact ? .55 :
            cue is SoundCue.ShieldSave ? 1.0 :
            cue is SoundCue.RescueCelebration ? .9 :
            cue is SoundCue.SludgeMawHit ? .19 :
            cue is SoundCue.EyeTyrantHit or
                SoundCue.BoneBroodmotherHit or SoundCue.VoidLeechHit or SoundCue.DreadHarvesterHit
                or SoundCue.SolarWardenHit ? .15 : 0;

        if (seconds <= 0)
        {
            return;
        }

        voice.CutoffTimer?.Stop();
        long order = voice.StartedOrder;
        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(seconds) };
        voice.CutoffTimer = timer;

        timer.Tick += (_, _) =>
        {
            timer.Stop();

            if (voice.StartedOrder != order)
            {
                return;
            }

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

        for (int i = 0; i < layeredEffectVoices.Count; i++)
        {
            if (layeredEffectVoices[i].Busy)
            {
                activeCount++;
            }
        }

        double headroom = activeCount <= 1
            ? 1
            : Math.Max(.4, 1 / Math.Sqrt(1 + (activeCount - 1) * .38));

        for (int i = 0; i < layeredEffectVoices.Count; i++)
        {
            LayeredEffectVoice voice = layeredEffectVoices[i];

            if (voice.Busy)
            {
                voice.Player.Volume = voice.RequestedVolume * headroom;
            }
        }
    }


    private bool IsLayeredEffect(SoundCue cue)
    {
        return layeredEffectPaths.ContainsKey(cue);
    }

    private static void TraceAudioFailure(string operation, Exception exception)
    {
        Trace.TraceWarning("Unable to {0}: {1}", operation, exception.Message);
    }


    private static byte[] Scale(byte[] source, double volume)
    {
        byte[] copy = (byte[])source.Clone();

        for (int i = 44; i + 1 < copy.Length; i += 2)
        {
            short value = BitConverter.ToInt16(copy, i);
            short scaled = (short)(value * Math.Clamp(volume, 0, 1));
            copy[i] = (byte)scaled;
            copy[i + 1] = (byte)(scaled >> 8);
        }

        return copy;
    }

    private sealed class LayeredEffectVoice
    {
        public readonly MediaPlayer Player = new();
        public bool Busy;
        public DispatcherTimer? CutoffTimer;
        public double RequestedVolume;
        public long StartedOrder;
    }
}

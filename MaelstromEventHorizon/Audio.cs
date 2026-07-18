using System.IO;
using System.Media;
using System.Windows.Media;

namespace MaelstromEventHorizon;

internal enum SoundCue
{
    Fire, EnemyFire, EnemyWarning, Thrust, Explosion, AsteroidExplosion, SteelHit, Pickup, Shield, ShieldImpact, Nova, Wave, Life, Mine, Vortex,
    CashRegister, Coin, CashBonus, ChaChing, CometCelebration, MultiplierWoohoo, ShipCrash, ShipBlast, BonusFailed
}

internal sealed class SynthAudio
{
    private readonly Dictionary<SoundCue, byte[]> clips = new();
    private readonly MediaPlayer music = new();
    private readonly object playerGate = new();
    private readonly Dictionary<(SoundCue Cue, int Volume), (SoundPlayer Player, MemoryStream Stream)> effectPlayers = [];
    private readonly string normalMusicPath = GameAssets.PathFor("through-the-universe.mp3");
    private readonly string bonusMusicPath = GameAssets.PathFor("singularity-action.mp3");
    private string? openedMusicPath;
    private string? requestedMusicPath;
    private bool musicInitialized;
    private bool musicEndedHandlerAttached;
    private bool musicRequested;
    private bool audioPaused;
    private TimeSpan pausedMusicPosition;
    private static readonly double[] PickupNotes = [523.25, 659.25, 783.99, 1046.5];
    private static readonly double[] CashBonusNotes = [392.0, 493.88, 587.33, 783.99, 987.77, 1174.66, 1567.98, 1975.53];
    private static readonly double[] CelebrationNotes = [523.25, 659.25, 783.99, 1046.5, 1318.51, 1567.98];

    public SynthAudio()
    {
        clips[SoundCue.Fire] = Build(.16, t =>
        {
            double e = Envelope(t, .16, .004, 1.8);
            double f = 980 - 650 * Ease(t / .16);
            return e * (.48 * Osc(f, t) + .19 * Osc(f * 2.01, t) + .11 * Noise(t, 5200, 71));
        }, .10, .28);

        clips[SoundCue.EnemyFire] = Build(.24, t =>
        {
            double e = Envelope(t, .24, .008, 1.35);
            double f = 250 + 440 * Ease(t / .24);
            return e * (.35 * Saw(f, t) + .22 * Osc(f * 1.51, t) + .12 * Noise(t, 2400, 19));
        }, .16, .44);

        clips[SoundCue.EnemyWarning] = Build(2.1, t =>
        {
            double e = Envelope(t, 2.1, .018, .24);
            double sweepPhase = t % .68 / .68;
            double triangle = 1 - Math.Abs(sweepPhase * 2 - 1);
            double frequency = 410 + 330 * Smooth(triangle);
            double pulse = .72 + .28 * Math.Sin(t * Math.PI * 2 / .68);
            return e * pulse * (.28 * Osc(frequency, t) + .13 * Saw(frequency * .5, t) + .07 * Osc(frequency * 1.99, t));
        }, .22, .84);

        clips[SoundCue.Thrust] = Build(.22, t =>
        {
            double e = Envelope(t, .22, .018, .62);
            double rumble = .22 * Noise(t, 260, 11) + .30 * Noise(t, 1900, 31);
            return e * (rumble + .12 * Saw(67 + 8 * Math.Sin(t * 29), t) + .07 * Osc(134, t));
        }, .05, .55);

        clips[SoundCue.Explosion] = Build(.92, t =>
        {
            double body = Math.Exp(-3.6 * t);
            double crack = Envelope(t, .12, .001, 2.7);
            return body * (.42 * Noise(t, 1450 - 900 * Math.Min(t, .8), 47) + .25 * Osc(82 - 31 * t, t))
                + crack * (.30 * Noise(t, 7200, 91) + .11 * Osc(190, t));
        }, .28, .65);

        clips[SoundCue.AsteroidExplosion] = Build(1.18, t =>
        {
            double fracture = Envelope(t, .15, .001, 3.1) *
                (.56 * Noise(t, 12800, 733) + .23 * Osc(Math.Max(390, 820 - t * 850), t));
            double impact = Envelope(t, .34, .002, 2.15) *
                (.26 * Noise(t, Math.Max(900, 5200 - t * 9800), 737) + .22 * Osc(Math.Max(75, 178 - t * 310), t));
            double boom = Envelope(t, 1.18, .004, 1.28) *
                (.43 * Osc(Math.Max(34, 108 - t * 52), t) + .34 * Noise(t, Math.Max(170, 2600 - t * 1950), 739));
            double secondaryTime = Math.Max(0, t - .12);
            double secondary = Envelope(secondaryTime, .78, .003, 1.55) *
                (.2 * Osc(Math.Max(38, 82 - secondaryTime * 46), secondaryTime)
                 + .18 * Noise(secondaryTime, Math.Max(220, 1900 - secondaryTime * 1500), 741));
            double debrisTime = Math.Max(0, t - .055);
            double debris = Envelope(debrisTime, .95, .002, 1.65) *
                (.21 * Noise(debrisTime, Math.Max(420, 6900 - debrisTime * 5200), 743)
                 + .1 * Osc(Math.Max(105, 390 - debrisTime * 220), debrisTime));
            double subBass = Envelope(t, 1.18, .003, 1.08) *
                (.38 * Osc(Math.Max(27, 72 - t * 34), t) + .2 * Osc(Math.Max(24, 44 - t * 17), t)
                 + .13 * Noise(t, Math.Max(90, 420 - t * 270), 747));
            return fracture + impact + boom + secondary + debris + subBass;
        }, .5, .92);

        clips[SoundCue.SteelHit] = Build(.34, t =>
        {
            double e = Envelope(t, .34, .002, 2.2);
            return e * (.29 * Osc(1120, t) + .22 * Osc(1683, t) + .14 * Osc(2317, t) + .08 * Noise(t, 6800, 7));
        }, .31, .72);

        clips[SoundCue.Pickup] = Build(.66, t =>
        {
            int step = Math.Min(3, (int)(t / .145));
            double local = t - step * .145;
            double e = Envelope(local, .16, .006, 1.35);
            return e * (.31 * Osc(PickupNotes[step], t) + .14 * Osc(PickupNotes[step] * 2, t) + .08 * Osc(PickupNotes[step] * .5, t));
        }, .35, .78);

        clips[SoundCue.Shield] = Build(.38, t =>
        {
            double e = Envelope(t, .38, .018, 1.05);
            double sweep = 290 + 1030 * Ease(t / .38);
            return e * (.25 * Osc(sweep, t) + .17 * Osc(sweep * 1.5, t) + .1 * Noise(t, 3200, 63));
        }, .30, .82);

        clips[SoundCue.ShieldImpact] = Build(.82, t =>
        {
            double crack = Envelope(t, .105, .001, 3.0) *
                (.46 * Noise(t, 11800, 67) + .2 * Osc(1460 - t * 2100, t));
            double field = Envelope(t, .82, .004, 1.35);
            double frequency = Math.Max(105, 520 - t * 430);
            double resonance = field * (.34 * Osc(frequency, t) + .22 * Osc(frequency * 1.51, t)
                + .13 * Saw(frequency * .5, t));
            double pulse = Envelope(t, .52, .002, 1.5) *
                (.28 * Osc(Math.Max(42, 112 - t * 105), t) + .12 * Noise(t, 1850, 69));
            return crack + resonance + pulse;
        }, .42, .94);

        clips[SoundCue.Nova] = Build(2.05, t =>
        {
            double e = Envelope(t, 2.05, .025, .7);
            double collapse = Math.Max(0, 1 - t / 2.05);
            return e * (.32 * Noise(t, 1100 + collapse * 2500, 101) + .28 * Osc(78 - 24 * t, t)
                + .16 * Osc(156 - 42 * t, t) + .09 * Noise(t, 180, 3));
        }, .42, .9);

        clips[SoundCue.Wave] = Build(1.05, t =>
        {
            int step = Math.Min(5, (int)(t / .16));
            double note = 196 * Math.Pow(2, step / 12.0);
            return Envelope(t % .16, .18, .008, 1.1) * (.22 * Osc(note, t) + .12 * Osc(note * 2, t));
        }, .38, .75);

        clips[SoundCue.Life] = Build(.92, t =>
        {
            int step = Math.Min(6, (int)(t / .12));
            double note = 392 * Math.Pow(2, step / 12.0);
            return Envelope(t % .12, .14, .004, .9) * (.25 * Osc(note, t) + .11 * Osc(note * 2.004, t));
        }, .4, .85);

        clips[SoundCue.Mine] = Build(.46, t =>
        {
            double e = Envelope(t, .46, .012, 1.15);
            double wobble = 118 + 34 * Math.Sin(t * 48);
            return e * (.31 * Saw(wobble, t) + .16 * Osc(wobble * 2, t) + .08 * Noise(t, 1300, 83));
        }, .18, .58);

        clips[SoundCue.Vortex] = Build(.95, t =>
        {
            double e = Envelope(t, .95, .035, .55);
            return e * (.23 * Osc(51 + 17 * Math.Sin(t * 8), t) + .14 * Osc(103 + 21 * Math.Sin(t * 5), t)
                + .12 * Noise(t, 480, 29));
        }, .46, .92);
        clips[SoundCue.CashRegister] = Build(.24, t =>
        {
            double click = Envelope(t, .055, .001, 2.8) * (.20 * Noise(t, 7600, 241) + .12 * Osc(920, t));
            double bellTime = Math.Max(0, t - .035);
            double bell = Envelope(bellTime, .205, .002, 1.45) * (.24 * Osc(1760, bellTime) + .13 * Osc(2637, bellTime));
            return click + bell;
        }, .26, .72);
        clips[SoundCue.Coin] = Build(.48, t =>
        {
            double strike = Envelope(t, .055, .001, 2.8) *
                (.13 * Noise(t, 11200, 263) + .12 * Osc(1280, t));
            double first = Envelope(t, .43, .001, 1.3) *
                (.23 * Osc(2093, t) + .16 * Osc(3139.5, t) + .08 * Osc(4186, t));
            double secondTime = Math.Max(0, t - .095);
            double second = Envelope(secondTime, .34, .001, 1.5) *
                (.16 * Osc(2349.32, secondTime) + .1 * Osc(3520, secondTime));
            return strike + first + second;
        }, .3, .82);
        clips[SoundCue.CashBonus] = Build(1.45, t =>
        {
            int step = Math.Min(7, (int)(t / .15));
            double local = t - step * .15;
            double sparkle = Envelope(local, .22, .004, 1.15) * (.19 * Osc(CashBonusNotes[step], t) + .09 * Osc(CashBonusNotes[step] * 2.003, t));
            double coins = Envelope(t, 1.35, .01, .85) * .055 * Noise(t, 6200 + 900 * Math.Sin(t * 17), 307);
            return sparkle + coins;
        }, .48, .92);
        clips[SoundCue.ChaChing] = Build(.72, t =>
        {
            double drawer = Envelope(t, .07, .001, 2.7) * (.18 * Noise(t, 7200, 419) + .11 * Osc(760, t));
            double coinTime = Math.Max(0, t - .045);
            double coin = Envelope(coinTime, .66, .002, 1.1) *
                (.24 * Osc(1760, coinTime) + .16 * Osc(2349.32, coinTime) + .09 * Osc(3520, coinTime));
            double sparkleTime = Math.Max(0, t - .22);
            double sparkle = Envelope(sparkleTime, .42, .003, 1.4) *
                (.12 * Osc(2637.02, sparkleTime) + .07 * Osc(3951.07, sparkleTime));
            return drawer + coin + sparkle;
        }, .38, .86);
        clips[SoundCue.CometCelebration] = Build(1.12, t =>
        {
            int step = Math.Min(CelebrationNotes.Length - 1, (int)(t / .13));
            double local = t - step * .13;
            double note = CelebrationNotes[step];
            double fanfare = Envelope(local, .21, .004, 1.15) *
                (.25 * Osc(note, t) + .13 * Osc(note * 2.002, t) + .07 * Osc(note * .5, t));
            double finaleTime = Math.Max(0, t - .62);
            double finale = Envelope(finaleTime, .48, .008, .9) *
                (.13 * Osc(1046.5, finaleTime) + .1 * Osc(1318.51, finaleTime) + .08 * Osc(1567.98, finaleTime));
            double confetti = Envelope(t, 1.02, .001, 1.35) *
                (.075 * Noise(t, 14800, 503) + .05 * Osc(2600 + 540 * Math.Sin(t * 31), t));
            double popA = Envelope(Math.Max(0, t - .14), .15, .001, 2.2) *
                (.11 * Noise(t, 17200, 509) + .07 * Osc(3520, t));
            double popB = Envelope(Math.Max(0, t - .43), .18, .001, 2.0) *
                (.1 * Noise(t, 15600, 521) + .065 * Osc(4186.01, t));
            return fanfare + finale + confetti + popA + popB;
        }, .46, .9);
        clips[SoundCue.MultiplierWoohoo] = Build(1.02, t =>
        {
            bool second = t >= .42;
            double local = second ? t - .42 : t;
            double duration = second ? .58 : .4;
            double progress = Math.Clamp(local / duration, 0, 1);
            double pitch = second ? 220 + 125 * Math.Sin(progress * Math.PI) : 165 + 105 * progress;
            double envelope = Envelope(local, duration, .025, second ? .72 : 1.0);
            double roundedVowel = .31 * Saw(pitch, local) + .19 * Osc(pitch * 2.01, local) + .1 * Osc(pitch * 3.02, local);
            double formants = (.10 * Osc(second ? 520 : 390, local) + .07 * Osc(second ? 1080 : 820, local)) *
                (.55 + .45 * Math.Max(0, Osc(pitch, local)));
            return envelope * (roundedVowel + formants);
        }, .34, .78);
        try
        {
            string shipDestructionPath = GameAssets.PathFor("ship-destruction.wav");
            clips[SoundCue.ShipCrash] = File.Exists(shipDestructionPath)
                ? File.ReadAllBytes(shipDestructionPath)
                : BuildShipCrashFallback();
        }
        catch
        {
            clips[SoundCue.ShipCrash] = BuildShipCrashFallback();
        }
        clips[SoundCue.ShipBlast] = Build(2.35, t =>
        {
            double hit = Envelope(t, .2, .001, 3.0) *
                (.34 * Noise(t, 9200, 631) + .24 * Osc(92, t) + .16 * Osc(46, t));
            double pressure = Envelope(t, 2.35, .003, .92) *
                (.48 * Osc(Math.Max(24, 63 - t * 18), t) + .28 * Noise(t, Math.Max(75, 620 - t * 220), 637));
            double secondaryTime = Math.Max(0, t - .34);
            double secondary = Envelope(secondaryTime, 1.75, .002, 1.28) *
                (.3 * Osc(Math.Max(22, 48 - secondaryTime * 13), secondaryTime)
                 + .2 * Noise(secondaryTime, Math.Max(80, 480 - secondaryTime * 210), 641));
            return hit + pressure + secondary;
        }, .54, .96);
        clips[SoundCue.BonusFailed] = Build(1.05, t =>
        {
            double impact = Envelope(t, .13, .001, 2.6) *
                (.23 * Noise(t, 5800, 811) + .14 * Osc(420, t));
            double fall = Envelope(t, 1.05, .012, 1.25);
            double frequency = 330 - 205 * Ease(t / 1.05);
            return impact + fall * (.29 * Saw(frequency, t) + .18 * Osc(frequency * .5, t) + .08 * Noise(t, 900, 821));
        }, .25, .72);
    }

    public void StartMusic()
    {
        StartTrack(normalMusicPath, true);
    }

    public void StartBonusMusic() => StartTrack(File.Exists(bonusMusicPath) ? bonusMusicPath : normalMusicPath, true);

    public void EnsureNormalMusic() => StartTrack(normalMusicPath, false);

    public void StopMusic(bool stopEffects = true)
    {
        musicRequested = false;
        audioPaused = false;
        pausedMusicPosition = TimeSpan.Zero;
        try { music.Stop(); }
        catch { }
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
        catch { }
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
        catch { }
    }

    private void StartTrack(string path, bool restart)
    {
        bool wasRequested = musicRequested;
        bool trackChanged = !string.Equals(openedMusicPath, path, StringComparison.OrdinalIgnoreCase);
        requestedMusicPath = path;
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
                music.Volume = string.Equals(path, bonusMusicPath, StringComparison.OrdinalIgnoreCase) ? .42 : .32;
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
        if (audioPaused || volume <= 0) return;
        if (!clips.TryGetValue(cue, out byte[]? source)) return;
        int volumeStep = (int)Math.Round(Math.Clamp(volume, 0, 1) * 100);
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
                catch { }
            }
        }
    }

    private static byte[] BuildShipCrashFallback() => Build(2.18, t =>
    {
        double impact = Envelope(t, .24, .001, 2.9) *
            (.38 * Noise(t, 11000, 601) + .2 * Osc(138, t) + .13 * Osc(1320, t));
        double hull = Envelope(t, 1.95, .004, 1.25) *
            (.34 * Noise(t, Math.Max(180, 3100 - t * 1375), 607) + .25 * Osc(Math.Max(34, 96 - t * 29), t));
        double metal = Envelope(t, 1.35, .002, 1.65) *
            (.11 * Osc(Math.Max(120, 880 - t * 310), t) + .08 * Osc(Math.Max(170, 1390 - t * 465), t));
        double secondaryTime = Math.Max(0, t - .48);
        double secondary = Envelope(secondaryTime, 1.45, .003, 1.45) *
            (.24 * Noise(secondaryTime, Math.Max(150, 1800 - secondaryTime * 980), 613)
             + .2 * Osc(Math.Max(31, 72 - secondaryTime * 24), secondaryTime));
        return impact + hull + metal + secondary;
    }, .48, .94);

    private static byte[] Build(double seconds, Func<double, double> source, double room, double stereoWidth)
    {
        const int rate = 48000;
        int count = (int)(seconds * rate);
        var mono = new double[count];
        for (int i = 0; i < count; i++) mono[i] = Math.Tanh(source(i / (double)rate) * 1.32);

        int earlyLeft = (int)(rate * .017);
        int earlyRight = (int)(rate * .023);
        int lateLeft = (int)(rate * .047);
        int lateRight = (int)(rate * .059);
        using var memory = new MemoryStream(44 + count * 4);
        using var writer = new BinaryWriter(memory);
        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + count * 4);
        writer.Write("WAVEfmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)2);
        writer.Write(rate);
        writer.Write(rate * 4);
        writer.Write((short)4);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(count * 4);

        for (int i = 0; i < count; i++)
        {
            double direct = mono[i] * (1 - room * .14);
            double left = direct + Tap(mono, i - earlyLeft) * room * .30 + Tap(mono, i - lateLeft) * room * .17;
            double right = direct + Tap(mono, i - earlyRight) * room * .30 + Tap(mono, i - lateRight) * room * .17;
            double movement = Math.Sin(i * Math.PI * 2 * .73 / rate) * stereoWidth * .075;
            left = Math.Tanh(left * (1 - movement));
            right = Math.Tanh(right * (1 + movement));
            writer.Write((short)(Math.Clamp(left, -.96, .96) * short.MaxValue));
            writer.Write((short)(Math.Clamp(right, -.96, .96) * short.MaxValue));
        }
        return memory.ToArray();
    }

    private static double Tap(double[] samples, int index) => index >= 0 ? samples[index] : 0;

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

    private static double Envelope(double t, double duration, double attack, double decayPower)
    {
        if (t < 0 || t >= duration) return 0;
        double a = Smooth(Math.Clamp(t / attack, 0, 1));
        double d = Math.Pow(Math.Max(0, 1 - t / duration), decayPower);
        return a * d;
    }

    private static double Osc(double hz, double t) => Math.Sin(2 * Math.PI * hz * t);

    private static double Saw(double hz, double t)
    {
        double phase = t * hz - Math.Floor(t * hz);
        return 2 * phase - 1;
    }

    private static double Noise(double t, double frequency, int seed)
    {
        double position = t * Math.Max(1, frequency);
        int index = (int)Math.Floor(position);
        double blend = Smooth(position - index);
        return Lerp(HashNoise(index, seed), HashNoise(index + 1, seed), blend);
    }

    private static double HashNoise(int value, int seed)
    {
        uint x = unchecked((uint)(value * 374761393 + seed * 668265263));
        x = (x ^ (x >> 13)) * 1274126177u;
        x ^= x >> 16;
        return x / (double)uint.MaxValue * 2 - 1;
    }

    private static double Smooth(double x) => x * x * (3 - 2 * x);
    private static double Ease(double x) => 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 2);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;
}

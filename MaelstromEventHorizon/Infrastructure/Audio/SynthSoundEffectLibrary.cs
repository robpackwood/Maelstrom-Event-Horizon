using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;
using System.IO;

namespace MaelstromEventHorizon.Infrastructure.Audio;

internal sealed class SynthSoundEffectLibrary : ISoundEffectLibrary
{
    private static readonly double[] PickupNotes = [523.25, 659.25, 783.99, 1046.5];
    private static readonly double[] CashBonusNotes = [392.0, 493.88, 587.33, 783.99, 987.77, 1174.66, 1567.98, 1975.53];
    private static readonly double[] CelebrationNotes = [523.25, 659.25, 783.99, 1046.5, 1318.51, 1567.98];
    private static readonly double[] GiantGrowNotes = [261.63, 329.63, 440.0, 587.33, 739.99, 987.77, 1318.51];
    private static readonly double[] GiantShrinkNotes = [987.77, 739.99, 587.33, 440.0, 329.63, 246.94];

    public IReadOnlyDictionary<SoundCue, byte[]> Clips { get; }

    public SynthSoundEffectLibrary(IAssetProvider assets)
    {
        var clips = new Dictionary<SoundCue, byte[]>
        {
            [SoundCue.Fire] = Build(.13, t =>
        {
            double envelope = Envelope(t, .13, .009, 2.05);
            double frequency = 1650 - 600 * Ease(t / .13);
            return envelope * (.26 * Osc(frequency, t) + .08 * Osc(frequency * 1.5, t)
                + .025 * Noise(t, 7200, 71));
        }, .06, .2),

            [SoundCue.EnemyFire] = Build(.24, t =>
        {
            double e = Envelope(t, .24, .008, 1.35);
            double f = 250 + 440 * Ease(t / .24);
            return e * (.35 * Saw(f, t) + .22 * Osc(f * 1.51, t) + .12 * Noise(t, 2400, 19));
        }, .16, .44),

            [SoundCue.EnemyWarning] = Build(2.1, t =>
        {
            double e = Envelope(t, 2.1, .018, .24);
            double sweepPhase = t % .68 / .68;
            double triangle = 1 - Math.Abs(sweepPhase * 2 - 1);
            double frequency = 410 + 330 * Smooth(triangle);
            double pulse = .72 + .28 * Math.Sin(t * Math.PI * 2 / .68);
            return e * pulse * (.28 * Osc(frequency, t) + .13 * Saw(frequency * .5, t) + .07 * Osc(frequency * 1.99, t));
        }, .22, .84),

            [SoundCue.BossAlarm] = Build(3.15, t =>
        {
            double envelope = Envelope(t, 3.15, .012, .18);
            double cycle = t % .78;
            double cycleProgress = cycle / .78;
            double riseAndFall = cycleProgress < .5
                ? Smooth(cycleProgress * 2)
                : Smooth((1 - cycleProgress) * 2);
            double sirenFrequency = 455 + riseAndFall * 405;
            double pulseGate = .58 + .42 * Math.Pow(Math.Max(0, Math.Sin(cycleProgress * Math.PI)), .45);
            double klaxon = pulseGate *
                (.31 * Saw(sirenFrequency, t) + .23 * Osc(sirenFrequency, t)
                 + .11 * Osc(sirenFrequency * 1.5, t));

            int warningBeat = (int)(t / .39);
            double beatTime = t - warningBeat * .39;
            double beatEnvelope = Envelope(beatTime, .31, .004, 1.25);
            double alternatingTone = warningBeat % 2 == 0 ? 126 : 94;
            double warningPulse = beatEnvelope *
                (.28 * Osc(alternatingTone, beatTime) + .17 * Osc(alternatingTone * 2, beatTime));
            double machinery = Envelope(t, 3.15, .02, .3) *
                (.08 * Noise(t, 940, 877) + .13 * Osc(48 + 4 * Math.Sin(t * 13), t));
            return envelope * (klaxon + warningPulse + machinery);
        }, .42, .94),

            [SoundCue.MenuMove] = Build(.13, t =>
        {
            double envelope = Envelope(t, .13, .002, 1.9);
            double frequency = 540 + 310 * Smooth(t / .13);
            return envelope * (.29 * Osc(frequency, t) + .14 * Osc(frequency * 1.5, t)
                + .055 * Noise(t, 4600, 883));
        }, .08, .3),

            [SoundCue.Thrust] = Build(.22, t =>
        {
            double e = Envelope(t, .22, .018, .62);
            double rumble = .22 * Noise(t, 260, 11) + .30 * Noise(t, 1900, 31);
            return e * (rumble + .12 * Saw(67 + 8 * Math.Sin(t * 29), t) + .07 * Osc(134, t));
        }, .05, .55),

            [SoundCue.Explosion] = Build(.92, t =>
        {
            double body = Math.Exp(-3.6 * t);
            double crack = Envelope(t, .12, .001, 2.7);
            return body * (.42 * Noise(t, 1450 - 900 * Math.Min(t, .8), 47) + .25 * Osc(82 - 31 * t, t))
                + crack * (.30 * Noise(t, 7200, 91) + .11 * Osc(190, t));
        }, .28, .65),

            [SoundCue.AsteroidExplosion] = Build(1.35, t =>
        {
            double fracture = Envelope(t, .24, .001, 2.7) *
                (.37 * Noise(t, 5400, 733) + .24 * Noise(t, 1650, 737)
                 + .13 * Osc(Math.Max(170, 610 - t * 1750), t));
            double chunks = RockChip(t, .025, 743) + RockChip(t, .075, 751)
                + RockChip(t, .145, 757) + RockChip(t, .235, 761);
            double explosion = Envelope(t, 1.35, .009, 1.18) *
                (.3 * Osc(Math.Max(31, 86 - t * 41), t)
                 + .2 * Osc(Math.Max(25, 47 - t * 17), t)
                 + .27 * Noise(t, Math.Max(145, 1450 - t * 920), 769));
            double dustTime = Math.Max(0, t - .1);
            double dust = Envelope(dustTime, 1.22, .025, 1.45) *
                (.18 * Noise(dustTime, Math.Max(120, 720 - dustTime * 430), 773)
                 + .08 * Noise(dustTime, Math.Max(260, 2400 - dustTime * 1550), 787));
            return fracture + chunks + explosion + dust;
        }, .46, .88),

            [SoundCue.SteelHit] = Build(.34, t =>
        {
            double e = Envelope(t, .34, .002, 2.2);
            return e * (.29 * Osc(1120, t) + .22 * Osc(1683, t) + .14 * Osc(2317, t) + .08 * Noise(t, 6800, 7));
        }, .31, .72),

            [SoundCue.Pickup] = Build(.66, t =>
        {
            int step = Math.Min(3, (int)(t / .145));
            double local = t - step * .145;
            double e = Envelope(local, .16, .006, 1.35);
            return e * (.31 * Osc(PickupNotes[step], t) + .14 * Osc(PickupNotes[step] * 2, t) + .08 * Osc(PickupNotes[step] * .5, t));
        }, .35, .78),

            [SoundCue.Shield] = Build(.38, t =>
        {
            double e = Envelope(t, .38, .018, 1.05);
            double sweep = 290 + 1030 * Ease(t / .38);
            return e * (.25 * Osc(sweep, t) + .17 * Osc(sweep * 1.5, t) + .1 * Noise(t, 3200, 63));
        }, .30, .82),

            [SoundCue.ShieldImpact] = Build(.82, t =>
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
        }, .42, .94),

            [SoundCue.Nova] = Build(2.05, t =>
        {
            double e = Envelope(t, 2.05, .025, .7);
            double collapse = Math.Max(0, 1 - t / 2.05);
            return e * (.32 * Noise(t, 1100 + collapse * 2500, 101) + .28 * Osc(78 - 24 * t, t)
                + .16 * Osc(156 - 42 * t, t) + .09 * Noise(t, 180, 3));
        }, .42, .9),

            [SoundCue.Wave] = Build(1.05, t =>
        {
            int step = Math.Min(5, (int)(t / .16));
            double note = 196 * Math.Pow(2, step / 12.0);
            return Envelope(t % .16, .18, .008, 1.1) * (.22 * Osc(note, t) + .12 * Osc(note * 2, t));
        }, .38, .75),

            [SoundCue.Life] = Build(.92, t =>
        {
            int step = Math.Min(6, (int)(t / .12));
            double note = 392 * Math.Pow(2, step / 12.0);
            return Envelope(t % .12, .14, .004, .9) * (.25 * Osc(note, t) + .11 * Osc(note * 2.004, t));
        }, .4, .85),

            [SoundCue.Mine] = Build(.46, t =>
        {
            double e = Envelope(t, .46, .012, 1.15);
            double wobble = 118 + 34 * Math.Sin(t * 48);
            return e * (.31 * Saw(wobble, t) + .16 * Osc(wobble * 2, t) + .08 * Noise(t, 1300, 83));
        }, .18, .58),

            [SoundCue.Vortex] = Build(.95, t =>
        {
            double e = Envelope(t, .95, .035, .55);
            return e * (.23 * Osc(51 + 17 * Math.Sin(t * 8), t) + .14 * Osc(103 + 21 * Math.Sin(t * 5), t)
                + .12 * Noise(t, 480, 29));
        }, .46, .92),
            [SoundCue.CashRegister] = Build(.24, t =>
        {
            double click = Envelope(t, .055, .001, 2.8) * (.20 * Noise(t, 7600, 241) + .12 * Osc(920, t));
            double bellTime = Math.Max(0, t - .035);
            double bell = Envelope(bellTime, .205, .002, 1.45) * (.24 * Osc(1760, bellTime) + .13 * Osc(2637, bellTime));
            return click + bell;
        }, .26, .72),
            [SoundCue.Coin] = Build(.48, t =>
        {
            double strike = Envelope(t, .055, .001, 2.8) *
                (.13 * Noise(t, 11200, 263) + .12 * Osc(1280, t));
            double first = Envelope(t, .43, .001, 1.3) *
                (.23 * Osc(2093, t) + .16 * Osc(3139.5, t) + .08 * Osc(4186, t));
            double secondTime = Math.Max(0, t - .095);
            double second = Envelope(secondTime, .34, .001, 1.5) *
                (.16 * Osc(2349.32, secondTime) + .1 * Osc(3520, secondTime));
            return strike + first + second;
        }, .3, .82),
            [SoundCue.CashBonus] = Build(1.45, t =>
        {
            int step = Math.Min(7, (int)(t / .15));
            double local = t - step * .15;
            double sparkle = Envelope(local, .22, .004, 1.15) * (.19 * Osc(CashBonusNotes[step], t) + .09 * Osc(CashBonusNotes[step] * 2.003, t));
            double coins = Envelope(t, 1.35, .01, .85) * .055 * Noise(t, 6200 + 900 * Math.Sin(t * 17), 307);
            return sparkle + coins;
        }, .48, .92),
            [SoundCue.ChaChing] = Build(.72, t =>
        {
            double drawer = Envelope(t, .07, .001, 2.7) * (.18 * Noise(t, 7200, 419) + .11 * Osc(760, t));
            double coinTime = Math.Max(0, t - .045);
            double coin = Envelope(coinTime, .66, .002, 1.1) *
                (.24 * Osc(1760, coinTime) + .16 * Osc(2349.32, coinTime) + .09 * Osc(3520, coinTime));
            double sparkleTime = Math.Max(0, t - .22);
            double sparkle = Envelope(sparkleTime, .42, .003, 1.4) *
                (.12 * Osc(2637.02, sparkleTime) + .07 * Osc(3951.07, sparkleTime));
            return drawer + coin + sparkle;
        }, .38, .86),
            [SoundCue.CometCelebration] = Build(1.12, t =>
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
        }, .46, .9),
            [SoundCue.MultiplierWoohoo] = Build(1.02, t =>
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
        }, .34, .78),
            [SoundCue.ShipCrash] = LoadShipCrash(assets),
            [SoundCue.ShipBlast] = Build(2.35, t =>
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
        }, .54, .96),
            [SoundCue.BonusFailed] = Build(1.05, t =>
        {
            double impact = Envelope(t, .13, .001, 2.6) *
                (.23 * Noise(t, 5800, 811) + .14 * Osc(420, t));
            double fall = Envelope(t, 1.05, .012, 1.25);
            double frequency = 330 - 205 * Ease(t / 1.05);
            return impact + fall * (.29 * Saw(frequency, t) + .18 * Osc(frequency * .5, t) + .08 * Noise(t, 900, 821));
        }, .25, .72),
            [SoundCue.GiantGrow] = Build(1.08, t =>
        {
            int step = Math.Min(GiantGrowNotes.Length - 1, (int)(t / .135));
            double local = t - step * .135;
            double note = GiantGrowNotes[step];
            double lift = Envelope(local, .2, .003, .92) *
                (.26 * Osc(note, t) + .14 * Osc(note * 2.003, t) + .08 * Osc(note * .5, t));
            double shimmer = Envelope(t, 1.03, .008, .8) *
                (.06 * Osc(1680 + 520 * Smooth(t / 1.08), t) + .035 * Noise(t, 8900, 947));
            double finishTime = Math.Max(0, t - .79);
            double finish = Envelope(finishTime, .28, .004, .75) *
                (.18 * Osc(1318.51, finishTime) + .12 * Osc(1975.53, finishTime));
            return lift + shimmer + finish;
        }, .42, .9),

            [SoundCue.GiantShrink] = Build(.92, t =>
        {
            int step = Math.Min(GiantShrinkNotes.Length - 1, (int)(t / .125));
            double local = t - step * .125;
            double note = GiantShrinkNotes[step];
            double drop = Envelope(local, .18, .002, 1.12) *
                (.25 * Osc(note, t) + .13 * Osc(note * 1.5, t) + .07 * Saw(note * .5, t));
            double squeeze = Envelope(t, .9, .003, 1.2) *
                (.085 * Osc(Math.Max(90, 410 - t * 330), t) + .045 * Noise(t, 4200, 953));
            return drop + squeeze;
        }, .34, .86),
        };
        Clips = clips;
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

    private static byte[] LoadShipCrash(IAssetProvider assets)
    {
        try
        {
            string path = assets.PathFor("ship-destruction.wav");
            return File.Exists(path) ? File.ReadAllBytes(path) : BuildShipCrashFallback();
        }
        catch
        {
            return BuildShipCrashFallback();
        }
    }

    private static double RockChip(double t, double delay, int seed)
    {
        double local = t - delay;
        double envelope = Envelope(local, .18, .001, 3.0);
        double knock = Osc(Math.Max(210, 980 - local * 3100), local);
        return envelope * (.13 * Noise(local, 4600, seed)
                           + .1 * Noise(local, 1250, seed + 17)
                           + .055 * knock);
    }

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

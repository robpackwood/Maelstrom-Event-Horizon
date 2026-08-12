using System.IO;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Enums;

namespace MaelstromEventHorizon.Infrastructure.Audio;

internal sealed class SynthSoundEffectLibrary : ISoundEffectLibrary
{
    private static readonly double[] PickupNotes = [523.25, 659.25, 783.99, 1046.5];
    private static readonly double[] GiantGrowNotes = [261.63, 329.63, 440.0, 587.33, 739.99, 987.77, 1318.51];
    private static readonly double[] GiantShrinkNotes = [987.77, 739.99, 587.33, 440.0, 329.63, 246.94];

    public SynthSoundEffectLibrary()
    {
        Dictionary<SoundCue, byte[]> clips = new()
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
                double phase = t % .52;
                double hit = phase < .18 ? Envelope(phase, .18, .004, 1.45) : 0;
                double drone = .12 * Osc(74, t) + .09 * Saw(111, t);
                return e * (drone + hit * (.31 * Saw(310, t) + .19 * Osc(465, t) + .08 * Noise(t, 2600, 715)));
            }, .3, .9),

            [SoundCue.BossAlarm] = Build(3.15, t =>
            {
                double envelope = Envelope(t, 3.15, .008, .16);
                double cycle = t % .78;
                double hornEnvelope = cycle < .58 ? Envelope(cycle, .58, .008, .72) : 0;
                double pitch = cycle < .29 ? 82 : 62;
                double horn = .48 * Saw(pitch, t) + .31 * Osc(pitch, t) + .19 * Osc(pitch * 2, t);
                double dread = .17 * Osc(31, t) + .09 * Noise(t, 210, 877);
                double alarm = .09 * Osc(520 + 95 * Math.Sin(t * Math.PI * 2 / .78), t);

                double strike = cycle < .07
                    ? Envelope(cycle, .07, .002, 2.6) * (.22 * Noise(t, 3600, 881) + .17 * Osc(48, t))
                    : 0;

                return envelope * (hornEnvelope * (horn + alarm) + dread + strike);
            }, .55, .97),

            [SoundCue.MenuMove] = Build(.13, t =>
            {
                double envelope = Envelope(t, .13, .002, 1.9);
                double frequency = 540 + 310 * Smooth(t / .13);

                return envelope * (.29 * Osc(frequency, t) + .14 * Osc(frequency * 1.5, t)
                                                           + .055 * Noise(t, 4600, 883));
            }, .08, .3),

            [SoundCue.Thrust] = Build(.72, t =>
            {
                double attack = Smooth(Math.Clamp(t / .035, 0, 1));
                double release = Smooth(Math.Clamp((.72 - t) / .055, 0, 1));
                double blow = .23 * Noise(t, 1450, 311) + .11 * Noise(t, 2800, 317);
                return attack * release * blow;
            }, .03, .22),

            [SoundCue.CanisterPulse] = Build(.42, t =>
            {
                double envelope = Envelope(t, .42, .012, 1.65);
                double wobble = Math.Sin(t * Math.PI * 2 * 4.2) * 8;
                double tone = .26 * Osc(118 + wobble, t) + .12 * Osc(236 + wobble * 2, t);
                double shimmer = .045 * Osc(710 + wobble * 4, t);
                return envelope * (tone + shimmer);
            }, .16, .55),

            [SoundCue.Explosion] = Build(.92, t =>
            {
                double body = Math.Exp(-3.6 * t);
                double crack = Envelope(t, .12, .001, 2.7);
                return body * (.42 * Noise(t, 1450 - 900 * Math.Min(t, .8), 47) + .25 * Osc(82 - 31 * t, t))
                       + crack * (.30 * Noise(t, 7200, 91) + .11 * Osc(190, t));
            }, .28, .65),

            [SoundCue.AsteroidExplosion] = Build(.78, t =>
            {
                double crack = Envelope(t, .12, .001, 3.25) *
                               (.46 * Noise(t, 4700, 733) + .26 * Noise(t, 1250, 737) + .1 * Osc(310 - t * 1150, t));

                double chunks = RockChip(t, .018, 743) + RockChip(t, .055, 751) + RockChip(t, .108, 757) +
                                RockChip(t, .173, 761) + RockChip(t, .245, 769);

                double rubble = Envelope(t, .78, .01, 1.65) *
                                (.18 * Noise(t, Math.Max(280, 1700 - t * 1300), 773) +
                                 .12 * Osc(Math.Max(46, 122 - t * 85), t));

                return crack + chunks + rubble;
            }, .32, .76),

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

                return e * (.31 * Osc(PickupNotes[step], t) + .14 * Osc(PickupNotes[step] * 2, t) +
                            .08 * Osc(PickupNotes[step] * .5, t));
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

                return e * (.32 * Noise(t, 1100 + collapse * 2500, 101) + .28 * Osc(78 - 24 * t, t) +
                            .16 * Osc(156 - 42 * t, t) + .09 * Noise(t, 180, 3));
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

            [SoundCue.RescueCelebration] = Build(1.35, t =>
            {
                double[] notes = [523.25, 659.25, 783.99, 1046.5, 1318.51];
                int step = Math.Min(notes.Length - 1, (int)(t / .16));
                double local = t - step * .16;
                double note = notes[step];

                double chime = Envelope(local, .24, .004, 1.05) *
                               (.28 * Osc(note, local) + .16 * Osc(note * 2.002, local) + .08 * Osc(note * .5, local));

                double finaleTime = Math.Max(0, t - .72);

                double finale = Envelope(finaleTime, .58, .01, .8) *
                                (.16 * Osc(1046.5, finaleTime) + .12 * Osc(1318.51, finaleTime) +
                                 .09 * Osc(1567.98, finaleTime));

                return chime + finale;
            }, .45, .9),

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
                double key = Envelope(t, .042, .001, 3.2) * (.32 * Noise(t, 6800, 241) + .16 * Osc(980, t));
                double drawerTime = Math.Max(0, t - .028);

                double drawer = Envelope(drawerTime, .14, .002, 1.65) *
                                (.23 * Noise(drawerTime, 1550, 243) + .12 * Saw(138, drawerTime));

                double bellTime = Math.Max(0, t - .095);

                double bell = Envelope(bellTime, .25, .002, 1.5) *
                              (.28 * Osc(1760, bellTime) + .16 * Osc(2637, bellTime));

                return key + drawer + bell;
            }, .3, .76),
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
                double flare = Envelope(t, .16, .003, 2.1) * (.24 * Noise(t, 11800, 503) + .14 * Osc(2050, t));

                double fizz = Envelope(t, 1.12, .004, 1.15) *
                              (.18 * Noise(t, 9000 - t * 5200, 509) + .07 * Osc(1480 - t * 820, t));

                double sparkle = Envelope(Math.Max(0, t - .3), .68, .003, 1.45) *
                                 (.09 * Noise(t, 14000, 521) + .045 * Osc(3040, t));

                return flare + fizz + sparkle;
            }, .42, .9),
            [SoundCue.MultiplierWoohoo] = Build(.68, t =>
            {
                double strike = Envelope(t, .04, .001, 2.5) * .13 * Noise(t, 8600, 529);

                double bell = Envelope(t, .68, .002, 1.35) *
                              (.25 * Osc(1760, t) + .16 * Osc(2640, t) + .09 * Osc(3520, t));

                return strike + bell;
            }, .32, .82),
            [SoundCue.PlayerShipDestruction] = Build(2.05, t =>
            {
                double impact = Envelope(t, .26, .003, 2.3) *
                                (.12 * Noise(t, 310, 591) + .52 * Osc(54, t) + .27 * Osc(32, t));
                double combustion = Envelope(t, 1.8, .015, 1.12) *
                                    (.14 * Noise(t, Math.Max(80, 320 - t * 90), 593) +
                                     .28 * Osc(Math.Max(22, 48 - t * 11), t));
                double rumble = Envelope(t, 2.05, .02, .92) *
                                (.31 * Osc(Math.Max(16, 39 - t * 9), t) + .08 * Noise(t, 95, 599));
                double glow = Envelope(t, 1.1, .012, 1.45) *
                              (.075 * Osc(147, t) + .045 * Osc(220, t) + .025 * Osc(294, t));
                return impact + combustion + rumble + glow;
            }, .52, .92),
            [SoundCue.ShipCrash] = BuildShipCrashFallback(),
            [SoundCue.ShipBlast] = Build(2.35, t =>
            {
                double hit = Envelope(t, .2, .001, 3.0) *
                             (.34 * Noise(t, 9200, 631) + .24 * Osc(92, t) + .16 * Osc(46, t));

                double pressure = Envelope(t, 2.35, .003, .92) *
                                  (.48 * Osc(Math.Max(24, 63 - t * 18), t) +
                                   .28 * Noise(t, Math.Max(75, 620 - t * 220), 637));

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

                return impact +
                       fall * (.29 * Saw(frequency, t) + .18 * Osc(frequency * .5, t) + .08 * Noise(t, 900, 821));
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
            }, .34, .86)
        };

        clips[SoundCue.VortexHit] = clips[SoundCue.Vortex];
        clips[SoundCue.BonusVoice] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerWarning] = clips[SoundCue.BossAlarm];
        clips[SoundCue.AnnouncerBossDown] = clips[SoundCue.Explosion];
        clips[SoundCue.AnnouncerExtraShip] = clips[SoundCue.Life];
        clips[SoundCue.AnnouncerWaveStart] = clips[SoundCue.Wave];
        clips[SoundCue.AnnouncerEnemyInbound] = clips[SoundCue.EnemyWarning];
        clips[SoundCue.AnnouncerEnemyAssault] = clips[SoundCue.EnemyWarning];
        clips[SoundCue.AnnouncerMineAlert] = clips[SoundCue.Mine];
        clips[SoundCue.AnnouncerBlackHoleAlert] = clips[SoundCue.Vortex];
        clips[SoundCue.AnnouncerNovaAlert] = clips[SoundCue.Nova];
        clips[SoundCue.CometSpawn] = clips[SoundCue.CometCelebration];
        clips[SoundCue.AnnouncerItemBox] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerCometStorm] = clips[SoundCue.CometSpawn];
        clips[SoundCue.AnnouncerBonusComet] = clips[SoundCue.CometSpawn];
        clips[SoundCue.AnnouncerAirBrakes] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerLuck] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerTripleFire] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerRiftVolley] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerLongRange] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerShields] = clips[SoundCue.Shield];
        clips[SoundCue.AnnouncerReflectionShield] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerTimeFreeze] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerSmartBomb] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerRicochetArena] = clips[SoundCue.Pickup];
        clips[SoundCue.AnnouncerGiantShip] = clips[SoundCue.Pickup];
        clips[SoundCue.ShieldSave] = clips[SoundCue.BonusVoice];
        clips[SoundCue.ReflectionBreak] = clips[SoundCue.AsteroidExplosion];
        clips[SoundCue.NovaHit] = clips[SoundCue.Nova];
        clips[SoundCue.RicochetBounce] = clips[SoundCue.MenuMove];
        clips[SoundCue.EnemyHit] = clips[SoundCue.SteelHit];
        clips[SoundCue.EnemyDestroyed] = clips[SoundCue.Explosion];
        clips[SoundCue.MineHit] = clips[SoundCue.Mine];
        clips[SoundCue.SludgeMawHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.EyeTyrantHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.BoneBroodmotherHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.VoidLeechHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.DreadHarvesterHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.SolarWardenHit] = clips[SoundCue.EnemyHit];
        clips[SoundCue.SludgeMawFire] = clips[SoundCue.EnemyFire];
        clips[SoundCue.EyeTyrantFire] = clips[SoundCue.EnemyFire];
        clips[SoundCue.BoneBroodmotherFire] = clips[SoundCue.EnemyFire];
        clips[SoundCue.VoidLeechFire] = clips[SoundCue.EnemyFire];
        clips[SoundCue.DreadHarvesterFire] = clips[SoundCue.EnemyFire];
        clips[SoundCue.SolarWardenFire] = clips[SoundCue.EnemyFire];
        Clips = clips;
    }

    public IReadOnlyDictionary<SoundCue, byte[]> Clips { get; }

    private static byte[] BuildShipCrashFallback()
    {
        return Build(2.18, t =>
        {
            double impact = Envelope(t, .24, .001, 2.9) *
                            (.38 * Noise(t, 11000, 601) + .2 * Osc(138, t) + .13 * Osc(1320, t));

            double hull = Envelope(t, 1.95, .004, 1.25) *
                          (.34 * Noise(t, Math.Max(180, 3100 - t * 1375), 607) +
                           .25 * Osc(Math.Max(34, 96 - t * 29), t));

            double metal = Envelope(t, 1.35, .002, 1.65) *
                           (.11 * Osc(Math.Max(120, 880 - t * 310), t) + .08 * Osc(Math.Max(170, 1390 - t * 465), t));

            double secondaryTime = Math.Max(0, t - .48);

            double secondary = Envelope(secondaryTime, 1.45, .003, 1.45) *
                               (.24 * Noise(secondaryTime, Math.Max(150, 1800 - secondaryTime * 980), 613)
                                + .2 * Osc(Math.Max(31, 72 - secondaryTime * 24), secondaryTime));

            return impact + hull + metal + secondary;
        }, .48, .94);
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
        double[] mono = new double[count];

        for (int i = 0; i < count; i++)
        {
            mono[i] = Math.Tanh(source(i / (double)rate) * 1.32);
        }

        int earlyLeft = (int)(rate * .017);
        int earlyRight = (int)(rate * .023);
        int lateLeft = (int)(rate * .047);
        int lateRight = (int)(rate * .059);
        using MemoryStream memory = new(44 + count * 4);
        using BinaryWriter writer = new(memory);
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

    private static double Tap(double[] samples, int index)
    {
        return index >= 0 ? samples[index] : 0;
    }

    private static double Envelope(double t, double duration, double attack, double decayPower)
    {
        if (t < 0 || t >= duration)
        {
            return 0;
        }

        double a = Smooth(Math.Clamp(t / attack, 0, 1));
        double d = Math.Pow(Math.Max(0, 1 - t / duration), decayPower);
        return a * d;
    }

    private static double Osc(double hz, double t)
    {
        return Math.Sin(2 * Math.PI * hz * t);
    }

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

    private static double Smooth(double x)
    {
        return x * x * (3 - 2 * x);
    }

    private static double Ease(double x)
    {
        return 1 - Math.Pow(1 - Math.Clamp(x, 0, 1), 2);
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }
}

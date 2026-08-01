using MaelstromEventHorizon.Application.Input;
using MaelstromEventHorizon.Application.Services.Composition;
using MaelstromEventHorizon.Application.Services.Contracts;
using MaelstromEventHorizon.Domain.Effects;
using MaelstromEventHorizon.Domain.Entities;
using MaelstromEventHorizon.Domain.Enums;
using MaelstromEventHorizon.Domain.Math;
using MaelstromEventHorizon.Domain.Scores;
using System.Windows.Input;

namespace MaelstromEventHorizon.Application;

internal sealed class GameEngine
{
    public const double Width = 1280;
    public const double Height = 720;
    internal const double PlayerMaxSpeed = 476;
    internal const double PlayerShotSpeed = 601.92;
    internal const double PlayerVortexGravity = 2_150_000;
    internal const double RespawnDelay = 2.25;
    internal const double ShieldReleaseDelay = .5;
    internal const double ArenaWallInset = 12;
    internal const int TitleMenuItemCount = 7;
    internal const double VolumeStep = .05;
    internal const int RapidFireBurstSize = 24;
    internal const double RapidFireShotInterval = .045;
    internal const double RapidFireReloadDuration = .42;
    internal const double RapidFireFallbackShotInterval = .15;
    private const int MaxPooledShots = 240;
    private const int MaxPooledParticles = 900;
    private const int MaxPooledShockwaves = 24;
    private const int MaxPooledFloatingTexts = 24;
    internal readonly IRandomSource Random;
    private readonly GameEngineServices services;
    internal readonly IAudioService Audio;
    internal readonly IHighScoreRepository HighScoreRepository;
    internal readonly IDisplaySettingsStore DisplaySettingsStore;
    internal double FireCooldown;
    internal double RapidFireReload;
    internal int RapidFireRoundsFired;
    internal double EventTimer;
    internal double CanisterTimer = -1;
    internal double MultiplierTimer = -1;
    internal double CometTimer = -1;
    internal double BlackHoleTimer = -1;
    internal double RescueTimer = -1;
    internal double CanisterStormSpawnTimer;
    internal double CometStormSpawnTimer;
    internal double BonusAsteroidSpawnTimer;
    internal double NextWaveTimer;
    internal double TurnVelocity;
    internal double ThrustRamp;
    internal double RespawnTimer;
    internal double ShieldReleaseTimer;
    internal double ShieldHumTimer;
    internal GameMode ModeBeforeQuitConfirmation;
    internal double SummaryElapsed;
    internal double SummaryScreenElapsed;
    internal double TransitionElapsed;
    internal double CashTickCooldown;
    internal double LevelBonusCountdown;
    internal double ScreenShakeTime;
    internal double ScreenShakeDuration;
    internal double ScreenShakeMagnitude;
    internal bool CanisterSpawned;
    internal bool MultiplierSpawned;
    internal bool CometSpawned;
    internal bool BlackHoleSpawned;
    internal bool CanisterStormWave;
    internal bool CometStormWave;
    internal bool FighterSpawnedThisWave;
    internal bool BonusSpawnsDisabled;
    internal int CanisterStormRemaining;
    internal int CometStormRemaining;
    internal int BonusAsteroidsRemaining;
    internal int BonusPatternStep;
    internal int NextLifeScore = 50_000;
    internal static readonly int[] CometValues = [1000, 2000, 3000, 4000, 5000];
    internal const double FadeToSummaryDuration = .7;
    internal const double SummaryFadeInDuration = .55;
    private const double SummaryInputDelay = 2;
    internal const double FadeToWaveDuration = .55;
    internal const double WaveFadeInDuration = .8;
    internal const double GameOverDelayDuration = 3;
    private const double GameOverFadeDuration = .7;
    private const double TitleDemoDelay = 60;
    private const double DemoDuration = 30;
    internal double TitleIdleTime;
    internal double DemoElapsed;
    internal double DemoFireCooldown;
    internal int DemoStage;
    internal bool DemoPowerupCollected;
    internal bool DemoEnemyDestroyed;
    internal bool DemoBlackHoleDestroyed;
    internal double GameOverDelayTimer;
    internal double GameOverFadeElapsed;
    internal bool PendingGameOverHighScore;
    internal string TitleSecretBuffer = "";
    public GameMode Mode { get; internal set; } = GameMode.Title;
    public Ship Player { get; internal set; } = new(new V2(Width / 2, Height / 2));
    public int Score { get; internal set; }
    public int Wave { get; internal set; }
    public int Lives { get; internal set; } = 3;
    public bool PlayerRespawning => RespawnTimer > 0;
    public int Multiplier { get; internal set; } = 1;
    public int WaveBaseCash { get; internal set; }
    public int WaveCometCash { get; internal set; }
    public int LevelBonusCash { get; internal set; }
    public bool IsBonusStage { get; internal set; }
    public bool IsBossStage { get; internal set; }
    public bool BossCountdownActive => IsBossStage && BossCountdown > 0;
    public bool BonusStageFailed { get; internal set; }
    public BonusStageKind BonusStageVariant { get; internal set; }
    public string BonusStageName => BonusStageVariant switch
    {
        BonusStageKind.DiagonalStorm => "DIAGONAL METAL STORM",
        BonusStageKind.Crossfire => "QUAD-CROSS CROSSFIRE",
        BonusStageKind.SlalomGates => "SHIFTING SLALOM",
        BonusStageKind.SpiralSwarm => "SPIRAL SWARM",
        _ => "BONUS STAGE"
    };
    public string BonusStageObjective => BonusStageVariant switch
    {
        BonusStageKind.DiagonalStorm => "CUT ACROSS THE FLOW",
        BonusStageKind.Crossfire => "READ ALL FOUR EDGES",
        BonusStageKind.SlalomGates => "THREAD EACH MOVING GAP",
        BonusStageKind.SpiralSwarm => "STAY BETWEEN THE CURVING ARMS",
        _ => "DODGE THE METAL STORM"
    };
    public int BonusAsteroidTotal { get; internal set; }
    public int BonusAsteroidsDodged { get; internal set; }
    public int SummaryBaseCash { get; internal set; }
    public int SummaryCometCash { get; internal set; }
    public int SummaryLevelBonusCash { get; internal set; }
    public int SummaryMultiplier { get; internal set; } = 1;
    public int SummaryTotalCash { get; internal set; }
    public int SummaryDeposited { get; internal set; }
    public double CashConfettiTime { get; internal set; }
    public double TransitionAlpha { get; internal set; }
    public double GameOverOverlayAlpha => Math.Clamp(GameOverFadeElapsed / GameOverFadeDuration, 0, 1);
    public bool SummaryComplete => SummaryDeposited >= SummaryTotalCash;
    public bool SummaryInputReady => Mode == GameMode.WaveSummary && SummaryScreenElapsed >= SummaryFadeInDuration + SummaryInputDelay;
    public string Banner { get; internal set; } = "EVENT HORIZON";
    public double BannerTime { get; internal set; } = 99;
    public double BossCountdown { get; internal set; }
    public double BossInvulnerability { get; internal set; }
    public double LastPowerupTime { get; internal set; }
    public double FreezeTime { get; internal set; }
    public bool RapidFireActive { get; internal set; }
    public bool ReflectionShieldActive { get; internal set; }
    public bool AirBrakesActive { get; internal set; }
    public bool LuckActive { get; internal set; }
    public bool TripleFireActive { get; internal set; }
    public bool RiftVolleyActive { get; internal set; }
    public bool LongRangeActive { get; internal set; }
    public bool RicochetArenaActive { get; internal set; }
    public double TotalTime { get; private set; }
    public double BonusTravelTime { get; internal set; }
    public double ShieldImpactTime { get; internal set; }
    public V2 ShieldImpactPoint { get; internal set; }
    internal double RicochetBounceSoundCooldown;

    public V2 ScreenShakeOffset
    {
        get
        {
            if (Mode != GameMode.Playing || ScreenShakeTime <= 0 || ScreenShakeDuration <= 0)
                return V2.Zero;
            double falloff = Math.Clamp(ScreenShakeTime / ScreenShakeDuration, 0, 1);
            double magnitude = ScreenShakeMagnitude * falloff * falloff;
            return new V2(Math.Sin(TotalTime * 103.7) * magnitude, Math.Cos(TotalTime * 127.3 + .8) * magnitude);
        }
    }

    public string PendingName { get; internal set; } = "";
    public int PendingHighScoreRank => Math.Min(10, HighScores.Count(entry => entry.Score >= Score) + 1);
    public int HighlightedHighScoreIndex => HighlightedHighScore is null ? -1 : HighScores.IndexOf(HighlightedHighScore);
    public HighScoreEntry? HighlightedHighScore { get; internal set; }
    public List<HighScoreEntry> HighScores { get; }
    public ControlBindings Bindings { get; }
    public int TitleMenuSelection { get; internal set; }
    public int ControlSelection { get; internal set; }
    public bool WaitingForBinding { get; internal set; }
    public bool FullScreenEnabled { get; internal set; }
    public double MusicVolume { get; internal set; }
    public double EffectsVolume { get; internal set; }
    public bool IsDemoMode { get; internal set; }
    public bool BonusOnlyMode { get; internal set; }
    public bool BossOnlyMode { get; internal set; }
    public List<Asteroid> Asteroids { get; } = [];
    public List<Fighter> Fighters { get; } = [];
    public List<AlienBoss> Bosses { get; } = [];
    public List<HomingMine> Mines { get; } = [];
    public List<GravityVortex> Vortices { get; } = [];
    public List<Nova> Novas { get; } = [];
    public List<Pickup> Pickups { get; } = [];
    public List<Comet> Comets { get; } = [];
    public List<Shot> Shots { get; } = [];
    public List<Particle> Particles { get; } = [];
    public List<Shockwave> Shockwaves { get; } = [];
    public List<FloatingText> FloatingTexts { get; } = [];
    public List<ShipDebris> ShipDebrisPieces { get; } = [];
    public List<Star> Stars { get; } = [];
    private readonly Stack<Shot> shotPool = new();
    private readonly Stack<Particle> particlePool = new();
    private readonly Stack<Shockwave> shockwavePool = new();
    private readonly Stack<FloatingText> floatingTextPool = new();
    private readonly Stack<ShipDebris> shipDebrisPool = new();
    internal int VisualQuality { get; private set; } = 2;
    internal string VisualQualityName => VisualQuality switch { 0 => "LOW", 1 => "BALANCED", _ => "HIGH" };

    public event Action<bool>? FullScreenChanged;
    public GameEngine(IAudioService audio, IHighScoreRepository highScoreRepository, IDisplaySettingsStore displaySettingsStore, ControlBindings bindings, DisplayPreferences preferences, IRandomSource random, GameEngineServices services)
    {
        this.Audio = audio;
        this.HighScoreRepository = highScoreRepository;
        this.DisplaySettingsStore = displaySettingsStore;
        Bindings = bindings;
        this.Random = random;
        this.services = services;
        FullScreenEnabled = preferences.FullScreen;
        MusicVolume = preferences.MusicVolume;
        EffectsVolume = preferences.EffectsVolume;
        audio.SetVolumes(MusicVolume, EffectsVolume);
        audio.StartTitleMusic();
        HighScores = highScoreRepository.Load();
        for (int i = 0; i < 115; i++)
            Stars.Add(new Star(new V2(random.NextDouble() * Width, random.NextDouble() * Height), .25 + random.NextDouble() * .75, random.NextDouble() * Math.PI * 2));
    }

    public void Update(double dt)
    {
        dt = Math.Min(dt, .04);
        TotalTime += dt;
        RicochetBounceSoundCooldown = Math.Max(0, RicochetBounceSoundCooldown - dt);
        if (Mode == GameMode.Title)
        {
            TitleIdleTime += dt;
            if (TitleIdleTime >= TitleDemoDelay)
                StartDemo();
            return;
        }

        if (Mode == GameMode.GameOverDelay)
        {
            UpdateDeathEffects(dt);
            GameOverDelayTimer = Math.Max(0, GameOverDelayTimer - dt);
            if (GameOverDelayTimer <= 0)
            {
                GameOverFadeElapsed = 0;
                Mode = PendingGameOverHighScore ? GameMode.NameEntry : GameMode.GameOver;
            }

            return;
        }

        if (Mode is GameMode.NameEntry or GameMode.GameOver)
        {
            GameOverFadeElapsed += dt;
            UpdateDeathEffects(dt);
            return;
        }

        if (Mode == GameMode.WaveOutro)
        {
            UpdateWaveOutro(dt);
            return;
        }

        if (Mode == GameMode.WaveSummary)
        {
            UpdateWaveSummary(dt);
            return;
        }

        if (Mode == GameMode.WaveSummaryExit)
        {
            UpdateWaveSummaryExit(dt);
            return;
        }

        if (Mode == GameMode.WaveIntro)
        {
            UpdateWaveIntro(dt);
            return;
        }

        if (Mode != GameMode.Playing)
            return;
        if (IsDemoMode)
        {
            DemoElapsed += dt;
            if (DemoElapsed >= DemoDuration)
            {
                ReturnToTitle();
                return;
            }
        }

        TickTimers(dt);
        if (!PlayerRespawning && !BossCountdownActive)
        {
            if (IsDemoMode)
                UpdateDemoPlayer(dt);
            else
                UpdatePlayer(dt);
        }

        UpdateWorld(dt);
        HandleCollisions();
        RemoveDead();
        if (Mode != GameMode.Playing)
            return;
        if (IsDemoMode)
        {
            UpdateDemoScript();
            return;
        }

        if (FreezeTime <= 0)
        {
            ScheduleEvents(dt);
        }
    }

    internal Shot SpawnShot(V2 position, V2 velocity, bool enemy, double lifetime)
    {
        TrimPool(Shots, shotPool, MaxPooledShots);
        Shot shot = shotPool.Count > 0 ? shotPool.Pop().Reset(position, velocity, enemy, lifetime) : new Shot(position, velocity, enemy, lifetime);
        Shots.Add(shot);
        return shot;
    }

    internal Particle SpawnParticle(V2 position, V2 velocity, double lifetime, uint color, double size)
    {
        TrimPool(Particles, particlePool, MaxPooledParticles);
        Particle particle = particlePool.Count > 0 ? particlePool.Pop().Reset(position, velocity, lifetime, color, size) : new Particle(position, velocity, lifetime, color, size);
        Particles.Add(particle);
        return particle;
    }

    internal Shockwave SpawnShockwave(V2 position, double lifetime, uint color, double maxRadius)
    {
        TrimPool(Shockwaves, shockwavePool, MaxPooledShockwaves);
        Shockwave shockwave = shockwavePool.Count > 0 ? shockwavePool.Pop().Reset(position, lifetime, color, maxRadius) : new Shockwave(position, lifetime, color, maxRadius);
        Shockwaves.Add(shockwave);
        return shockwave;
    }

    internal FloatingText SpawnFloatingText(V2 position, string text, uint color)
    {
        while (FloatingTexts.Count >= MaxPooledFloatingTexts) { floatingTextPool.Push(FloatingTexts[0]); FloatingTexts.RemoveAt(0); }
        FloatingText floatingText = floatingTextPool.Count > 0 ? floatingTextPool.Pop().Reset(position, text, color) : new FloatingText(position, text, color);
        FloatingTexts.Add(floatingText);
        return floatingText;
    }

    internal ShipDebris SpawnShipDebris(V2 position, V2 velocity, int kind, double angle, double spin)
    {
        while (ShipDebrisPieces.Count >= 32) { shipDebrisPool.Push(ShipDebrisPieces[0]); ShipDebrisPieces.RemoveAt(0); }
        ShipDebris piece = shipDebrisPool.Count > 0 ? shipDebrisPool.Pop().Reset(position, velocity, kind, angle, spin) : new ShipDebris(position, velocity, kind, angle, spin).Initialize();
        ShipDebrisPieces.Add(piece);
        return piece;
    }

    internal void RecycleShipDebris()
    {
        for (int i = ShipDebrisPieces.Count - 1; i >= 0; i--)
            if (!ShipDebrisPieces[i].Alive) { shipDebrisPool.Push(ShipDebrisPieces[i]); ShipDebrisPieces.RemoveAt(i); }
    }

    internal void CycleVisualQuality()
    {
        VisualQuality = (VisualQuality + 1) % 3;
        ShowBanner($"VISUAL QUALITY  {VisualQualityName}", 1.8);
    }

    internal bool LowerVisualQualityIfNeeded()
    {
        if (VisualQuality == 0) return false;
        VisualQuality--;
        ShowBanner($"PERFORMANCE MODE  {VisualQualityName}", 2.2);
        return true;
    }

    internal void RecycleEffects()
    {
        RecycleDead(Shots, shotPool);
        RecycleDead(Particles, particlePool);
        RecycleDead(Shockwaves, shockwavePool);
        for (int i = FloatingTexts.Count - 1; i >= 0; i--)
            if (!FloatingTexts[i].Alive) { floatingTextPool.Push(FloatingTexts[i]); FloatingTexts.RemoveAt(i); }
    }

    internal void RecycleAllEffects()
    {
        foreach (Shot item in Shots) shotPool.Push(item);
        foreach (Particle item in Particles) particlePool.Push(item);
        foreach (Shockwave item in Shockwaves) shockwavePool.Push(item);
        foreach (FloatingText item in FloatingTexts) floatingTextPool.Push(item);
        foreach (ShipDebris item in ShipDebrisPieces) shipDebrisPool.Push(item);
        Shots.Clear(); Particles.Clear(); Shockwaves.Clear(); FloatingTexts.Clear(); ShipDebrisPieces.Clear();
    }

    private static void RecycleDead<T>(List<T> active, Stack<T> pool) where T : Body
    {
        for (int i = active.Count - 1; i >= 0; i--)
            if (!active[i].Alive) { pool.Push(active[i]); active.RemoveAt(i); }
    }

    private static void TrimPool<T>(List<T> active, Stack<T> pool, int maximum)
    {
        while (active.Count >= maximum) { pool.Push(active[0]); active.RemoveAt(0); }
    }

    internal void RaiseFullScreenChanged(bool enabled) => FullScreenChanged?.Invoke(enabled);
    private void StartDemo() => services.GameInputService.StartDemo(this);
    public bool HandleCommandKey(Key key, bool isRepeat) => services.GameInputService.HandleCommandKey(this, key, isRepeat);
    private void ReturnToTitle() => services.GameInputService.ReturnToTitle(this);
    public void HandleTextInput(string text) => services.GameInputService.HandleTextInput(this, text);
    public bool HandleNameEntryKey(Key key) => services.GameInputService.HandleNameEntryKey(this, key);
    private void TickTimers(double dt) => services.PlayerSimulationService.TickTimers(this, dt);
    private void UpdatePlayer(double dt) => services.PlayerSimulationService.UpdatePlayer(this, dt);
    private void UpdateDemoPlayer(double dt) => services.PlayerSimulationService.UpdateDemoPlayer(this, dt);
    private void UpdateDemoScript() => services.PlayerSimulationService.UpdateDemoScript(this);
    internal void FirePlayer() => services.PlayerSimulationService.FirePlayer(this);
    private void UpdateWorld(double dt) => services.PlayerSimulationService.UpdateWorld(this, dt);
    internal void CompleteBonusAsteroid(Asteroid asteroid) => services.BossCombatService.CompleteBonusAsteroid(this, asteroid);
    internal void UpdateBosses(double dt) => services.BossCombatService.UpdateBosses(this, dt);
    internal void SplitSludgeGlob(Shot glob) => services.BossCombatService.SplitSludgeGlob(this, glob);
    internal void ApplyGravity(Body body, double dt) => services.BossCombatService.ApplyGravity(this, body, dt);
    internal void ApplyPlayerGravity(double dt) => services.BossCombatService.ApplyPlayerGravity(this, dt);
    private void HandleCollisions() => services.CollisionService.HandleCollisions(this);
    internal void HitAsteroid(Asteroid asteroid, int damage = 1) => services.CollisionService.HitAsteroid(this, asteroid, damage);
    internal int RollAsteroidFragmentCount() => services.CollisionService.RollAsteroidFragmentCount(this);
    internal void DestroyFighter(Fighter fighter) => services.CollisionService.DestroyFighter(this, fighter);
    internal void DamageBoss(AlienBoss boss, int damage, V2 hitPosition) => services.CollisionService.DamageBoss(this, boss, damage, hitPosition);
    internal void DestroyMine(HomingMine mine) => services.CollisionService.DestroyMine(this, mine);
    internal void DamagePlayer(bool bypassShield = false, V2? impactPosition = null) => services.CollisionService.DamagePlayer(this, bypassShield, impactPosition);
    private void UpdateDeathEffects(double dt) => services.PowerupService.UpdateDeathEffects(this, dt);
    internal void RespawnPlayer() => services.PowerupService.RespawnPlayer(this);
    internal void AwardCanister() => services.PowerupService.AwardCanister(this);
    internal void ShrinkGiantShip(V2 impactPosition) => services.PowerupService.ShrinkGiantShip(this, impactPosition);
    internal void ClearEquippedPowerups() => services.PowerupService.ClearEquippedPowerups(this);
    internal void DetonateNova(Nova nova) => services.PowerupService.DetonateNova(this, nova);
    internal void NeutralizeNova(Nova nova) => services.PowerupService.NeutralizeNova(this, nova);
    private void ScheduleEvents(double dt) => services.WaveEventService.ScheduleEvents(this, dt);
    internal void BeginNextWave() => services.WaveSpawnService.BeginNextWave(this);
    internal void UpdateBonusAsteroidStream(double dt) => services.WaveSpawnService.UpdateBonusAsteroidStream(this, dt);
    internal void SpawnFighter() => services.WaveSpawnService.SpawnFighter(this);
    internal void SpawnFighterAssault() => services.WaveSpawnService.SpawnFighterAssault(this);
    internal void SpawnMine() => services.WaveSpawnService.SpawnMine(this);
    internal void SpawnVortex() => services.WaveSpawnService.SpawnVortex(this);
    internal void SpawnVortexAssault() => services.WaveSpawnService.SpawnVortexAssault(this);
    internal void SpawnNova() => services.WaveSpawnService.SpawnNova(this);
    internal void SpawnNovaAssault() => services.WaveSpawnService.SpawnNovaAssault(this);
    internal void SpawnCanister() => services.WaveSpawnService.SpawnCanister(this);
    internal void SpawnCanisterEntity() => services.WaveSpawnService.SpawnCanisterEntity(this);
    internal void SpawnComet() => services.WaveSpawnService.SpawnComet(this);
    internal void SpawnCometEntity() => services.WaveSpawnService.SpawnCometEntity(this);
    internal void SpawnMultiplier() => services.WaveSpawnService.SpawnMultiplier(this);
    internal void SpawnBonusPickup(V2? at = null) => services.WaveSpawnService.SpawnBonusPickup(this, at);
    internal void SpawnRescueShip() => services.WaveSpawnService.SpawnRescueShip(this);
    internal void RollDrop(V2 at, double chance = .09) => services.WaveSpawnService.RollDrop(this, at, chance);
    internal void AddScore(int basePoints) => services.ScoreTransitionService.AddScore(this, basePoints);
    internal void AwardImmediateScore(int amount, V2 position) => services.ScoreTransitionService.AwardImmediateScore(this, amount, position);
    internal void AddCometCash(int amount) => services.ScoreTransitionService.AddCometCash(this, amount);
    internal void EnsureLuckyWaveEvents() => services.ScoreTransitionService.EnsureLuckyWaveEvents(this);
    private void UpdateWaveSummary(double dt) => services.ScoreTransitionService.UpdateWaveSummary(this, dt);
    internal void UpdateLevelBonus(double dt) => services.ScoreTransitionService.UpdateLevelBonus(this, dt);
    internal void CompleteWaveSummary() => services.ScoreTransitionService.CompleteWaveSummary(this);
    internal void BeginWaveOutro() => services.ScoreTransitionService.BeginWaveOutro(this);
    private void UpdateWaveOutro(double dt) => services.ScoreTransitionService.UpdateWaveOutro(this, dt);
    internal void BeginWaveSummaryExit() => services.ScoreTransitionService.BeginWaveSummaryExit(this);
    private void UpdateWaveSummaryExit(double dt) => services.ScoreTransitionService.UpdateWaveSummaryExit(this, dt);
    private void UpdateWaveIntro(double dt) => services.ScoreTransitionService.UpdateWaveIntro(this, dt);
    internal void Hyperspace() => services.ScoreTransitionService.Hyperspace(this);
    internal void CenterPlayerWithShield() => services.ScoreTransitionService.CenterPlayerWithShield(this);
    internal void SpawnShipWreck() => services.EffectsPhysicsService.SpawnShipWreck(this);
    internal void UpdateShipDebris(double dt) => services.EffectsPhysicsService.UpdateShipDebris(this, dt);
    internal void EmitThrust() => services.EffectsPhysicsService.EmitThrust(this);
    internal void Spark(V2 position, uint color, int count) => services.EffectsPhysicsService.Spark(this, position, color, count);
    internal void Explosion(V2 position, int count, uint color) => services.EffectsPhysicsService.Explosion(this, position, count, color);
    internal void AsteroidBreakup(V2 position, int count, uint color) => services.EffectsPhysicsService.AsteroidBreakup(this, position, count, color);
    private void RemoveDead() => services.EffectsPhysicsService.RemoveDead(this);
    internal void ClearWorld() => services.EffectsPhysicsService.ClearWorld(this);
    internal void TriggerScreenShake(double duration, double magnitude) => services.EffectsPhysicsService.TriggerScreenShake(this, duration, magnitude);
    internal V2 SafeEdgePosition() => services.EffectsPhysicsService.SafeEdgePosition(this);
    internal V2 SafePosition(double distance) => services.EffectsPhysicsService.SafePosition(this, distance);
    internal V2 RandomDirection() => services.EffectsPhysicsService.RandomDirection(this);
    internal bool Touching(Body a, Body b) => services.EffectsPhysicsService.Touching(this, a, b);
    internal bool TouchingComet(Shot shot, Comet comet) => services.EffectsPhysicsService.TouchingComet(this, shot, comet);
    internal V2 MoveBody(Body body, V2 nextPosition, bool wrapNormally = true) => services.EffectsPhysicsService.MoveBody(this, body, nextPosition, wrapNormally);
    internal V2 ArenaDelta(V2 from, V2 to) => services.EffectsPhysicsService.ArenaDelta(this, from, to);
    internal V2 Wrap(V2 p) => services.EffectsPhysicsService.Wrap(p);
    internal V2 PredictAim(V2 origin, V2 target, V2 targetVelocity, double projectileSpeed) => services.EffectsPhysicsService.PredictAim(this, origin, target, targetVelocity, projectileSpeed);
    internal V2 Rotate(V2 vector, double angle) => services.EffectsPhysicsService.Rotate(vector, angle);
    internal void ShowBanner(string text, double duration) => services.EffectsPhysicsService.ShowBanner(this, text, duration);
    public string BossName(AlienBossKind kind) => services.EffectsPhysicsService.BossName(kind);
    internal uint BossTint(AlienBossKind kind) => services.EffectsPhysicsService.BossTint(kind);
    public string PowerName(PowerupKind kind) => services.EffectsPhysicsService.PowerName(kind);
}

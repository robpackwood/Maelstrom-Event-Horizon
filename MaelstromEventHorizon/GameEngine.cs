using System.Windows.Input;

namespace MaelstromEventHorizon;

internal enum GameMode { Title, Controls, Playing, Paused, QuitConfirm, WaveOutro, WaveSummary, WaveSummaryExit, WaveIntro, GameOverDelay, NameEntry, GameOver }

internal sealed class GameEngine
{
    public const double Width = 1280;
    public const double Height = 720;
    private const double PlayerMaxSpeed = 476;
    private const double PlayerShotSpeed = 608;
    private const double RespawnDelay = 2.25;
    private const double ShieldReleaseDelay = .5;
    private const int RapidFireBurstSize = 15;
    private const double RapidFireReloadDuration = .5;
    private readonly Random random = new(391923);
    private readonly SynthAudio audio = new();
    private readonly Action<bool>? fullScreenChanged;
    private double fireCooldown;
    private double rapidFireReload;
    private int rapidFireRoundsFired;
    private double eventTimer;
    private double canisterTimer = -1;
    private double multiplierTimer = -1;
    private double cometTimer = -1;
    private double blackHoleTimer = -1;
    private double rescueTimer = -1;
    private double canisterStormSpawnTimer;
    private double cometStormSpawnTimer;
    private double bonusAsteroidSpawnTimer;
    private double nextWaveTimer;
    private double turnVelocity;
    private double thrustRamp;
    private double respawnTimer;
    private double shieldReleaseTimer;
    private GameMode modeBeforeQuitConfirmation;
    private bool fireWasDown;
    private double summaryElapsed;
    private double summaryScreenElapsed;
    private double transitionElapsed;
    private double cashTickCooldown;
    private double levelBonusCountdown;
    private double screenShakeTime;
    private double screenShakeDuration;
    private double screenShakeMagnitude;
    private bool canisterSpawned;
    private bool multiplierSpawned;
    private bool cometSpawned;
    private bool blackHoleSpawned;
    private bool canisterStormWave;
    private bool cometStormWave;
    private bool fighterSpawnedThisWave;
    private bool bonusSpawnsDisabled;
    private int canisterStormRemaining;
    private int cometStormRemaining;
    private int bonusAsteroidsRemaining;
    private int nextLifeScore = 50_000;
    private static readonly int[] CometValues = [500, 1000, 2000, 3000, 4000, 5000];
    private const double FadeToSummaryDuration = .7;
    private const double SummaryFadeInDuration = .55;
    private const double SummaryInputDelay = 2;
    private const double FadeToWaveDuration = .55;
    private const double WaveFadeInDuration = .8;
    private const double GameOverDelayDuration = 3;
    private const double GameOverFadeDuration = .7;
    private const double TitleDemoDelay = 60;
    private const double DemoDuration = 30;
    private double titleIdleTime;
    private double demoElapsed;
    private double demoFireCooldown;
    private int demoStage;
    private bool demoPowerupCollected;
    private bool demoEnemyDestroyed;
    private bool demoBlackHoleDestroyed;
    private double gameOverDelayTimer;
    private double gameOverFadeElapsed;
    private bool pendingGameOverHighScore;

    public GameMode Mode { get; private set; } = GameMode.Title;
    public Ship Player { get; private set; } = new(new V2(Width / 2, Height / 2));
    public int Score { get; private set; }
    public int Wave { get; private set; }
    public int Lives { get; private set; } = 3;
    public bool PlayerRespawning => respawnTimer > 0;
    public int Multiplier { get; private set; } = 1;
    public int WaveBaseCash { get; private set; }
    public int WaveCometCash { get; private set; }
    public int LevelBonusCash { get; private set; }
    public bool IsBonusStage { get; private set; }
    public bool BonusStageFailed { get; private set; }
    public int BonusAsteroidTotal { get; private set; }
    public int BonusAsteroidsDodged { get; private set; }
    public int BonusAsteroidsInPlay => bonusAsteroidsRemaining + Asteroids.Count(asteroid => asteroid.Alive && asteroid.ExitsArena);
    public int SummaryBaseCash { get; private set; }
    public int SummaryCometCash { get; private set; }
    public int SummaryLevelBonusCash { get; private set; }
    public int SummaryMultiplier { get; private set; } = 1;
    public int SummaryTotalCash { get; private set; }
    public int SummaryDeposited { get; private set; }
    public double CashConfettiTime { get; private set; }
    public double TransitionAlpha { get; private set; }
    public double GameOverOverlayAlpha => Math.Clamp(gameOverFadeElapsed / GameOverFadeDuration, 0, 1);
    public bool SummaryComplete => SummaryDeposited >= SummaryTotalCash;
    public bool SummaryInputReady => Mode == GameMode.WaveSummary &&
        summaryScreenElapsed >= SummaryFadeInDuration + SummaryInputDelay;
    public string Banner { get; private set; } = "EVENT HORIZON";
    public double BannerTime { get; private set; } = 99;
    public PowerupKind? LastPowerup { get; private set; }
    public double LastPowerupTime { get; private set; }
    public double FreezeTime { get; private set; }
    public bool RapidFireActive { get; private set; }
    public bool AirBrakesActive { get; private set; }
    public bool LuckActive { get; private set; }
    public bool TripleFireActive { get; private set; }
    public bool LongRangeActive { get; private set; }
    public double TotalTime { get; private set; }
    public double BonusTravelTime { get; private set; }
    public double ShieldImpactTime { get; private set; }
    public V2 ShieldImpactPoint { get; private set; }
    public V2 ScreenShakeOffset
    {
        get
        {
            if (Mode != GameMode.Playing || screenShakeTime <= 0 || screenShakeDuration <= 0) return V2.Zero;
            double falloff = Math.Clamp(screenShakeTime / screenShakeDuration, 0, 1);
            double magnitude = screenShakeMagnitude * falloff * falloff;
            return new V2(Math.Sin(TotalTime * 103.7) * magnitude, Math.Cos(TotalTime * 127.3 + .8) * magnitude);
        }
    }
    public string PendingName { get; private set; } = "";
    public int PendingHighScoreRank => Math.Min(10, HighScores.Count(entry => entry.Score >= Score) + 1);
    public List<HighScoreEntry> HighScores { get; }
    public ControlBindings Bindings { get; } = new();
    public int TitleMenuSelection { get; private set; }
    public int ControlSelection { get; private set; }
    public bool WaitingForBinding { get; private set; }
    public bool FullScreenEnabled { get; private set; }
    public bool IsDemoMode { get; private set; }

    public List<Asteroid> Asteroids { get; } = [];
    public List<Fighter> Fighters { get; } = [];
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

    public GameEngine(bool fullScreenEnabled = false, Action<bool>? fullScreenChanged = null)
    {
        FullScreenEnabled = fullScreenEnabled;
        this.fullScreenChanged = fullScreenChanged;
        HighScores = HighScoreStore.Load();
        for (int i = 0; i < 115; i++)
            Stars.Add(new Star(new V2(random.NextDouble() * Width, random.NextDouble() * Height),
                .25 + random.NextDouble() * .75, random.NextDouble() * Math.PI * 2));
    }

    public void Update(double dt)
    {
        dt = Math.Min(dt, .04);
        TotalTime += dt;
        if (Mode == GameMode.Title)
        {
            titleIdleTime += dt;
            if (titleIdleTime >= TitleDemoDelay) StartDemo();
            return;
        }
        if (Mode == GameMode.GameOverDelay)
        {
            UpdateDeathEffects(dt);
            gameOverDelayTimer = Math.Max(0, gameOverDelayTimer - dt);
            if (gameOverDelayTimer <= 0)
            {
                gameOverFadeElapsed = 0;
                Mode = pendingGameOverHighScore ? GameMode.NameEntry : GameMode.GameOver;
            }
            return;
        }
        if (Mode is GameMode.NameEntry or GameMode.GameOver)
        {
            gameOverFadeElapsed += dt;
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
        if (Mode != GameMode.Playing) return;

        if (IsDemoMode)
        {
            demoElapsed += dt;
            if (demoElapsed >= DemoDuration)
            {
                ReturnToTitle();
                return;
            }
        }

        TickTimers(dt);
        if (!PlayerRespawning)
        {
            if (IsDemoMode) UpdateDemoPlayer(dt);
            else UpdatePlayer(dt);
        }
        UpdateWorld(dt);
        HandleCollisions();
        RemoveDead();
        if (IsDemoMode)
        {
            UpdateDemoScript();
            return;
        }
        ScheduleEvents(dt);
    }

    private void StartGame()
    {
        IsDemoMode = false;
        titleIdleTime = 0;
        ClearWorld();
        Score = 0;
        Wave = 0;
        Lives = 3;
        Multiplier = 1;
        nextLifeScore = 50_000;
        Player = new Ship(new V2(Width / 2, Height / 2));
        turnVelocity = 0;
        thrustRamp = 0;
        Mode = GameMode.Playing;
        BeginNextWave();
    }

    private void StartDemo()
    {
        StartGame();
        IsDemoMode = true;
        demoElapsed = 0;
        demoFireCooldown = 0;
        demoStage = 0;
        demoPowerupCollected = false;
        demoEnemyDestroyed = false;
        demoBlackHoleDestroyed = false;
        eventTimer = double.MaxValue;
        canisterTimer = -1;
        multiplierTimer = -1;
        cometTimer = -1;
        blackHoleTimer = -1;
        rescueTimer = -1;
        canisterStormRemaining = 0;
        cometStormRemaining = 0;
        Pickups.Add(new Pickup(new V2(900, Height / 2), V2.Zero, PickupKind.Canister));
        ShowBanner("AUTOPILOT DEMONSTRATION", 2.2);
    }

    public bool HandleCommandKey(Key key, bool isRepeat)
    {
        if (IsDemoMode)
        {
            ReturnToTitle();
            return true;
        }
        if (Mode == GameMode.Title) titleIdleTime = 0;
        if (Mode == GameMode.GameOverDelay) return true;

        if (Mode == GameMode.Controls)
        {
            if (WaitingForBinding)
            {
                if (key == Key.Escape && ControlBindings.Actions[ControlSelection] != GameAction.Quit)
                    WaitingForBinding = false;
                else
                {
                    Bindings.Assign(ControlBindings.Actions[ControlSelection], key);
                    WaitingForBinding = false;
                }
                return true;
            }
            if (isRepeat) return true;
            if (key == Key.Up) ControlSelection = (ControlSelection + ControlBindings.Actions.Length - 1) % ControlBindings.Actions.Length;
            else if (key == Key.Down) ControlSelection = (ControlSelection + 1) % ControlBindings.Actions.Length;
            else if (key == Key.Enter) WaitingForBinding = true;
            else if (key == Key.R) Bindings.Reset();
            else if (key == Key.Escape) Mode = GameMode.Title;
            else return false;
            return true;
        }

        if (Mode == GameMode.QuitConfirm)
        {
            if (isRepeat) return true;
            if (key is Key.Enter or Key.Y) ReturnToTitle();
            else if (key is Key.Escape or Key.N || key == Bindings[GameAction.Quit]) CancelQuitConfirmation();
            return true;
        }

        if (Mode is not GameMode.NameEntry and not GameMode.Controls && key == Bindings[GameAction.Quit])
        {
            if (Mode is GameMode.Playing or GameMode.Paused or GameMode.WaveOutro or GameMode.WaveSummary or
                GameMode.WaveSummaryExit or GameMode.WaveIntro or GameMode.GameOver) BeginQuitConfirmation();
            return true;
        }
        if (isRepeat) return false;

        if (Mode == GameMode.Title)
        {
            if (key == Key.Up) TitleMenuSelection = (TitleMenuSelection + 3) % 4;
            else if (key == Key.Down) TitleMenuSelection = (TitleMenuSelection + 1) % 4;
            else if (TitleMenuSelection == 2 && key is Key.Space or Key.Left or Key.Right)
                ToggleFullScreen();
            else if (key == Key.Enter)
            {
                if (TitleMenuSelection == 0) StartGame();
                else if (TitleMenuSelection == 1) Mode = GameMode.Controls;
                else if (TitleMenuSelection == 2) ToggleFullScreen();
                else System.Windows.Application.Current.Shutdown();
            }
            else return false;
            return true;
        }

        if (Mode == GameMode.GameOver && key == Key.Enter)
        {
            StartGame();
            return true;
        }
        if (Mode == GameMode.WaveSummary)
        {
            if (!SummaryInputReady) return true;
            if (!SummaryComplete) CompleteWaveSummary();
            BeginWaveSummaryExit();
            return true;
        }
        if (Mode is GameMode.WaveOutro or GameMode.WaveSummaryExit or GameMode.WaveIntro) return true;
        if (Mode is GameMode.Playing or GameMode.Paused && key == Bindings[GameAction.Pause])
        {
            if (Mode == GameMode.Playing)
            {
                Mode = GameMode.Paused;
                audio.PauseAll();
            }
            else
            {
                Mode = GameMode.Playing;
                audio.ResumeAll();
            }
            return true;
        }
        if (Mode == GameMode.Playing && key == Bindings[GameAction.Hyperspace])
        {
            Hyperspace();
            return true;
        }
        return false;
    }

    private void ToggleFullScreen()
    {
        FullScreenEnabled = !FullScreenEnabled;
        fullScreenChanged?.Invoke(FullScreenEnabled);
    }

    private void BeginQuitConfirmation()
    {
        modeBeforeQuitConfirmation = Mode;
        Mode = GameMode.QuitConfirm;
        audio.PauseAll();
    }

    private void CancelQuitConfirmation()
    {
        Mode = modeBeforeQuitConfirmation;
        if (Mode != GameMode.Paused) audio.ResumeAll();
    }

    private void ReturnToTitle()
    {
        audio.StopMusic();
        IsDemoMode = false;
        titleIdleTime = 0;
        demoElapsed = 0;
        ClearWorld();
        Player = new Ship(new V2(Width / 2, Height / 2));
        turnVelocity = 0;
        thrustRamp = 0;
        TitleMenuSelection = 0;
        WaitingForBinding = false;
        TransitionAlpha = 0;
        Mode = GameMode.Title;
    }

    public void HandleTextInput(string text)
    {
        if (Mode != GameMode.NameEntry || PendingName.Length >= 12) return;
        foreach (char c in text.ToUpperInvariant())
        {
            if (PendingName.Length >= 12) break;
            if (char.IsLetterOrDigit(c) || c is ' ' or '-' or '_') PendingName += c;
        }
    }

    public bool HandleNameEntryKey(Key key)
    {
        if (Mode != GameMode.NameEntry) return false;
        if (key == Key.Back)
        {
            if (PendingName.Length > 0) PendingName = PendingName[..^1];
            return true;
        }
        if (key == Key.Enter)
        {
            string name = string.IsNullOrWhiteSpace(PendingName) ? "PLAYER" : PendingName.Trim();
            HighScores.Add(new HighScoreEntry(name, Score, Wave, DateTime.Now));
            var ordered = HighScores.OrderByDescending(entry => entry.Score).ThenBy(entry => entry.AchievedAt).Take(10).ToList();
            HighScores.Clear();
            HighScores.AddRange(ordered);
            HighScoreStore.Save(HighScores);
            Mode = GameMode.GameOver;
            return true;
        }
        return false;
    }

    private void TickTimers(double dt)
    {
        fireCooldown -= dt;
        if (rapidFireReload > 0)
        {
            rapidFireReload -= dt;
            if (rapidFireReload <= 0) rapidFireRoundsFired = 0;
        }
        BannerTime -= dt;
        LastPowerupTime -= dt;
        FreezeTime -= dt;
        Player.Invulnerable -= dt;
        Player.SpawnShieldTime -= dt;
        ShieldImpactTime = Math.Max(0, ShieldImpactTime - dt);
        if (respawnTimer > 0)
        {
            respawnTimer = Math.Max(0, respawnTimer - dt);
            if (respawnTimer <= 0) RespawnPlayer();
        }
        screenShakeTime = Math.Max(0, screenShakeTime - dt);
        if (IsBonusStage) BonusTravelTime += dt;
        UpdateLevelBonus(dt);
    }

    private void UpdatePlayer(double dt)
    {
        double turn = 0;
        if (Keyboard.IsKeyDown(Bindings[GameAction.TurnLeft])) turn -= 1;
        if (Keyboard.IsKeyDown(Bindings[GameAction.TurnRight])) turn += 1;
        double targetTurnVelocity = turn * 4.35;
        turnVelocity += (targetTurnVelocity - turnVelocity) * Math.Min(1, dt * 12);
        Player.Angle += turnVelocity * dt;
        bool wasThrusting = Player.Thrusting;
        bool wasShielding = Player.Shielding;
        Player.Thrusting = Keyboard.IsKeyDown(Bindings[GameAction.Thrust]);
        bool shieldHeld = Keyboard.IsKeyDown(Bindings[GameAction.Shield]);
        if (shieldHeld && Player.Shield > 0) shieldReleaseTimer = ShieldReleaseDelay;
        else shieldReleaseTimer = Math.Max(0, shieldReleaseTimer - dt);
        Player.Shielding = Player.Shield > 0 && (shieldHeld || shieldReleaseTimer > 0);

        if (Player.Thrusting)
        {
            thrustRamp = Math.Min(1, thrustRamp + dt * 2.4);
            V2 facing = V2.FromAngle(Player.Angle);
            double acceleration = 493 + thrustRamp * 195.5;
            Player.Velocity += facing * (acceleration * dt);
            EmitThrust();
            if (!wasThrusting) audio.Play(SoundCue.Thrust, .5);
        }
        else thrustRamp = Math.Max(0, thrustRamp - dt * 1.8);

        if (AirBrakesActive) Player.Velocity *= Math.Pow(.08, dt);
        else Player.Velocity *= Math.Pow(.99965, dt * 60);

        if (Player.Shielding)
        {
            Player.Shield = Math.Max(0, Player.Shield - 22 * dt);
            if (!wasShielding) audio.Play(SoundCue.Shield, .45);
        }

        bool firing = Keyboard.IsKeyDown(Bindings[GameAction.Fire]);
        bool fireRequested = RapidFireActive ? firing : firing && !fireWasDown;
        bool weaponReady = !RapidFireActive || fireCooldown <= 0 && rapidFireReload <= 0;
        if (fireRequested && weaponReady) FirePlayer();
        fireWasDown = firing;
        ApplyPlayerGravity(dt);
        if (Player.Velocity.Length > PlayerMaxSpeed)
            Player.Velocity = Player.Velocity.Normalized * PlayerMaxSpeed;
        Player.Position = Wrap(Player.Position + Player.Velocity * dt);
    }

    private void UpdateDemoPlayer(double dt)
    {
        Body? target = demoStage switch
        {
            0 => Pickups.FirstOrDefault(p => p.Alive && p.Kind == PickupKind.Canister),
            1 => Fighters.FirstOrDefault(f => f.Alive),
            2 => Vortices.FirstOrDefault(v => v.Alive),
            _ => Asteroids.Where(a => a.Alive).OrderBy(a => WrappedDelta(Player.Position, a.Position).LengthSquared).FirstOrDefault()
        };

        V2 targetDelta = target is null
            ? V2.FromAngle(Player.Angle)
            : WrappedDelta(Player.Position, target.Position);
        bool combatTarget = target is Asteroid or Fighter or GravityVortex;
        if (combatTarget && target is not null)
        {
            double lead = Math.Min(.7, targetDelta.Length / PlayerShotSpeed);
            targetDelta += target.Velocity * lead;
        }

        double desiredAngle = Math.Atan2(targetDelta.Y, targetDelta.X);
        double angleError = Math.Atan2(Math.Sin(desiredAngle - Player.Angle), Math.Cos(desiredAngle - Player.Angle));
        double targetTurnVelocity = Math.Clamp(angleError * 5.5, -3.75, 3.75);
        turnVelocity += (targetTurnVelocity - turnVelocity) * Math.Min(1, dt * 9);
        Player.Angle += turnVelocity * dt;

        bool wasThrusting = Player.Thrusting;
        bool wasShielding = Player.Shielding;
        double targetDistance = targetDelta.Length;
        Player.Thrusting = target is Pickup
            ? targetDistance > 34 && Player.Velocity.Length < 325
            : target is not null && targetDistance > 285 && Player.Velocity.Length < 290;
        if (Player.Thrusting)
        {
            thrustRamp = Math.Min(1, thrustRamp + dt * 2.4);
            double acceleration = 493 + thrustRamp * 195.5;
            Player.Velocity += V2.FromAngle(Player.Angle) * (acceleration * dt);
            EmitThrust();
            if (!wasThrusting) audio.Play(SoundCue.Thrust, .45);
        }
        else thrustRamp = Math.Max(0, thrustRamp - dt * 1.8);

        bool shieldWanted = Shots.Any(shot => shot.Alive && shot.Enemy &&
                WrappedDelta(Player.Position, shot.Position).LengthSquared < 145 * 145) ||
            Asteroids.Any(asteroid => asteroid.Alive &&
                WrappedDelta(Player.Position, asteroid.Position).LengthSquared < 92 * 92);
        if (shieldWanted && Player.Shield > 0) shieldReleaseTimer = ShieldReleaseDelay;
        else shieldReleaseTimer = Math.Max(0, shieldReleaseTimer - dt);
        Player.Shielding = Player.Shield > 0 && (shieldWanted || shieldReleaseTimer > 0);
        if (Player.Shielding)
        {
            Player.Shield = Math.Max(0, Player.Shield - 22 * dt);
            if (!wasShielding) audio.Play(SoundCue.Shield, .42);
        }

        demoFireCooldown -= dt;
        bool weaponReady = !RapidFireActive || fireCooldown <= 0 && rapidFireReload <= 0;
        if (combatTarget && Math.Abs(angleError) < .11 && targetDistance < 690 && demoFireCooldown <= 0 && weaponReady)
        {
            FirePlayer();
            demoFireCooldown = RapidFireActive ? .09 : .19;
        }

        ApplyPlayerGravity(dt);
        Player.Velocity *= Math.Pow(.9988, dt * 60);
        if (Player.Velocity.Length > 340) Player.Velocity = Player.Velocity.Normalized * 340;
        Player.Position = Wrap(Player.Position + Player.Velocity * dt);
    }

    private void UpdateDemoScript()
    {
        if (demoStage == 0)
        {
            if (!demoPowerupCollected)
            {
                if (!Pickups.Any(p => p.Alive && p.Kind == PickupKind.Canister))
                    Pickups.Add(new Pickup(Wrap(Player.Position + V2.FromAngle(Player.Angle) * 230), V2.Zero, PickupKind.Canister));
                return;
            }

            demoStage = 1;
            fighterSpawnedThisWave = true;
            V2 fighterPosition = Wrap(Player.Position + new V2(390, 115));
            Fighters.Add(new Fighter(fighterPosition, new V2(-24, 18), FighterKind.Raider) { FireDelay = 3.4 });
            ShowBanner("AUTOPILOT TARGET: RAIDER", 1.8);
            audio.Play(SoundCue.EnemyWarning, .62);
            return;
        }

        if (demoStage == 1 && demoEnemyDestroyed)
        {
            demoStage = 2;
            SpawnDemoBlackHole();
            return;
        }

        if (demoStage == 2)
        {
            if (demoBlackHoleDestroyed)
            {
                demoStage = 3;
                ShowBanner("AUTOPILOT COMBAT PATROL", 1.8);
            }
            else if (!Vortices.Any(v => v.Alive)) SpawnDemoBlackHole();
        }
    }

    private void SpawnDemoBlackHole()
    {
        blackHoleSpawned = true;
        V2 position = Wrap(Player.Position + new V2(355, -125));
        Vortices.Add(new GravityVortex(position) { Lifetime = 20 });
        ShowBanner("AUTOPILOT TARGET: BLACK HOLE", 1.8);
    }

    private void FirePlayer()
    {
        int availableRounds = RapidFireActive
            ? Math.Min(RapidFireBurstSize - rapidFireRoundsFired,
                RapidFireBurstSize - Shots.Count(shot => shot.Alive && !shot.Enemy))
            : int.MaxValue;
        if (availableRounds <= 0) return;

        V2 facing = V2.FromAngle(Player.Angle);
        double range = LongRangeActive ? 1.45 : 1;
        int roundsFired = 0;
        void Add(double offset)
        {
            if (roundsFired >= availableRounds) return;
            V2 direction = V2.FromAngle(Player.Angle + offset);
            Shots.Add(new Shot(Player.Position + direction * (22 * Ship.VisualScale),
                Player.Velocity * .28 + direction * PlayerShotSpeed, false, .82 * range));
            roundsFired++;
        }
        Add(0);
        if (TripleFireActive) { Add(-.17); Add(.17); }
        if (RapidFireActive)
        {
            rapidFireRoundsFired += roundsFired;
            fireCooldown = .085;
            if (rapidFireRoundsFired >= RapidFireBurstSize)
                rapidFireReload = RapidFireReloadDuration;
        }
        else fireCooldown = 0;
        Player.Velocity -= facing * .8;
        audio.Play(SoundCue.Fire, .72);
    }

    private void UpdateWorld(double dt)
    {
        bool frozen = FreezeTime > 0;
        foreach (var asteroid in Asteroids)
        {
            asteroid.Age += dt;
            asteroid.Angle += asteroid.Spin * dt;
            if (asteroid.ExitsArena)
            {
                asteroid.Position += asteroid.Velocity * dt;
                double margin = asteroid.Radius * 1.5;
                if (asteroid.Position.X < -margin || asteroid.Position.Y > Height + margin)
                {
                    asteroid.Alive = false;
                    BonusAsteroidsDodged++;
                    AddScore(500);
                }
            }
            else
            {
                ApplyGravity(asteroid, dt);
                asteroid.Position = Wrap(asteroid.Position + asteroid.Velocity * dt);
            }
        }

        foreach (var fighter in Fighters)
        {
            fighter.Age += dt;
            if (frozen) continue;
            V2 toShip = WrappedDelta(fighter.Position, Player.Position);
            V2 tangent = new(-toShip.Y, toShip.X);
            double weave = Math.Sin(fighter.Age * (fighter.Kind == FighterKind.Interceptor ? 2.8 : 1.5));
            V2 desired = toShip.Normalized * (fighter.Kind == FighterKind.Interceptor ? 118 : 72) + tangent.Normalized * weave * 68;
            fighter.Velocity += (desired - fighter.Velocity) * Math.Min(1, dt * 1.15);
            fighter.Angle = Math.Atan2(fighter.Velocity.Y, fighter.Velocity.X);
            fighter.Position = Wrap(fighter.Position + fighter.Velocity * dt);
            fighter.FireDelay -= dt;
            if (!PlayerRespawning && fighter.FireDelay <= 0 && toShip.Length < 720)
            {
                const double enemyShotSpeed = 335;
                V2 direction = PredictAim(fighter.Position, Player.Position, Player.Velocity, enemyShotSpeed);
                double spread = fighter.Kind == FighterKind.Interceptor ? .23 : .32;
                direction = Rotate(direction, (random.NextDouble() * 2 - 1) * spread);
                Shots.Add(new Shot(fighter.Position + direction * 22, direction * enemyShotSpeed, true, 2.35));
                fighter.FireDelay = fighter.Kind == FighterKind.Interceptor
                    ? 1.2 + random.NextDouble() * .65
                    : 1.85 + random.NextDouble() * .9;
                audio.Play(SoundCue.EnemyFire, .55);
            }
        }

        foreach (var mine in Mines)
        {
            mine.Age += dt;
            if (!frozen)
            {
                V2 delta = WrappedDelta(mine.Position, Player.Position);
                mine.Velocity += delta.Normalized * (105 * dt);
                if (mine.Velocity.Length > 220) mine.Velocity = mine.Velocity.Normalized * 220;
                mine.Angle += dt * 4.5;
                mine.Position = Wrap(mine.Position + mine.Velocity * dt);
            }
        }

        foreach (var vortex in Vortices)
        {
            vortex.Age += dt;
            vortex.Angle -= dt * 1.8;
            if (vortex.Age >= vortex.Lifetime) vortex.Alive = false;
        }

        foreach (var nova in Novas)
        {
            nova.Age += dt;
            nova.Angle += dt * (1 + nova.Age);
            if (!nova.Detonated && nova.Age >= Nova.Fuse) DetonateNova(nova);
        }

        foreach (var pickup in Pickups)
        {
            pickup.Age += dt;
            pickup.Angle += dt * (pickup.Kind == PickupKind.RescueShip ? 4.6 : 1.7);
            V2 nextPosition = pickup.Position + pickup.Velocity * dt;
            pickup.Position = pickup.Kind == PickupKind.RescueShip ? nextPosition : Wrap(nextPosition);
            if (pickup.Age >= pickup.Lifetime) pickup.Alive = false;
        }

        foreach (var comet in Comets)
        {
            comet.Age += dt;
            comet.Position = Wrap(comet.Position + comet.Velocity * dt);
            comet.Angle = Math.Atan2(comet.Velocity.Y, comet.Velocity.X);
            if (comet.Age >= comet.Lifetime) comet.Alive = false;
            if (random.NextDouble() < Math.Min(1, dt * 70))
            {
                V2 tail = -comet.Velocity.Normalized;
                Particles.Add(new Particle(comet.Position + tail * 16 + RandomDirection() * 6,
                    tail * random.Next(90, 250) + RandomDirection() * 24, .35 + random.NextDouble() * .45,
                    random.Next(3) == 0 ? 0xffffffff : comet.Tint, 2 + random.NextDouble() * 5));
            }
        }

        foreach (var shot in Shots)
        {
            shot.Age += dt;
            shot.Position = Wrap(shot.Position + shot.Velocity * dt);
            if (shot.Age >= shot.Lifetime) shot.Alive = false;
        }

        foreach (var particle in Particles)
        {
            particle.Age += dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Math.Pow(.96, dt * 60);
            if (particle.Age >= particle.Lifetime) particle.Alive = false;
        }
        foreach (var ring in Shockwaves)
        {
            ring.Age += dt;
            if (ring.Age >= ring.Lifetime) ring.Alive = false;
        }
        foreach (var text in FloatingTexts) text.Age += dt;
        UpdateShipDebris(dt);
    }

    private void ApplyGravity(Body body, double dt)
    {
        foreach (var vortex in Vortices.Where(v => v.Alive))
        {
            V2 delta = WrappedDelta(body.Position, vortex.Position);
            double d2 = Math.Max(1800, delta.LengthSquared);
            body.Velocity += delta.Normalized * (1_100_000 / d2 * dt);
        }
    }

    private void ApplyPlayerGravity(double dt)
    {
        foreach (var vortex in Vortices.Where(v => v.Alive))
        {
            V2 delta = WrappedDelta(Player.Position, vortex.Position);
            double d2 = Math.Max(3600, delta.LengthSquared);
            Player.Velocity += delta.Normalized * (780_000 / d2 * dt);
        }
    }

    private void HandleCollisions()
    {
        foreach (var shot in Shots.Where(s => s.Alive && !s.Enemy).ToArray())
        {
            Asteroid? asteroid = Asteroids.FirstOrDefault(a => a.Alive && Touching(shot, a));
            if (asteroid is not null)
            {
                V2 hitPosition = shot.Position;
                shot.Alive = false;
                AwardImmediateScore(100, hitPosition);
                HitAsteroid(asteroid);
                continue;
            }
            Fighter? fighter = Fighters.FirstOrDefault(f => f.Alive && Touching(shot, f));
            if (fighter is not null) { shot.Alive = false; if (--fighter.HitPoints <= 0) DestroyFighter(fighter); else Spark(fighter.Position, 0xfff0a060, 4); continue; }
            HomingMine? mine = Mines.FirstOrDefault(m => m.Alive && Touching(shot, m));
            if (mine is not null) { shot.Alive = false; if (--mine.HitPoints <= 0) DestroyMine(mine); continue; }
            GravityVortex? vortex = Vortices.FirstOrDefault(v => v.Alive && Touching(shot, v));
            if (vortex is not null) { shot.Alive = false; DestroyVortex(vortex); continue; }
            Nova? nova = Novas.FirstOrDefault(n => n.Alive && !n.Detonated && Touching(shot, n));
            if (nova is not null) { shot.Alive = false; NeutralizeNova(nova); continue; }
            Comet? comet = Comets.FirstOrDefault(c => c.Alive && TouchingComet(shot, c));
            if (comet is not null)
            {
                V2 hitPosition = shot.Position;
                shot.Alive = false;
                comet.Alive = false;
                AddCometCash(comet.Value);
                Explosion(hitPosition, 28, comet.Tint);
                Shockwaves.Add(new Shockwave(hitPosition, .55, comet.Tint, 125));
                FloatingTexts.Add(new FloatingText(hitPosition, $"+${comet.Value:N0}", comet.Tint));
                audio.Play(SoundCue.CometCelebration, .9);
                continue;
            }
            Pickup? prize = Pickups.FirstOrDefault(p => p.Alive && p.Kind is PickupKind.Multiplier or PickupKind.Bonus && Touching(shot, p));
            if (prize is not null)
            {
                shot.Alive = false;
                prize.Alive = false;
                if (prize.Kind == PickupKind.Multiplier)
                {
                    Multiplier = Math.Max(Multiplier, prize.Value);
                    ShowBanner($"{prize.Value}X MULTIPLIER", 1.8);
                    audio.Play(SoundCue.MultiplierWoohoo, .78);
                }
                else
                {
                    AddScore(prize.Value);
                    audio.Play(SoundCue.Coin, .78);
                }
            }
        }

        if (!PlayerRespawning)
            foreach (var shot in Shots.Where(s => s.Alive && s.Enemy).ToArray())
                if (Touching(shot, Player)) { shot.Alive = false; DamagePlayer(false, shot.Position); }

        foreach (var pickup in Pickups.Where(p => !PlayerRespawning && p.Alive &&
                     p.Kind is PickupKind.Canister or PickupKind.RescueShip).ToArray())
        {
            if (!Touching(pickup, Player)) continue;
            pickup.Alive = false;
            if (pickup.Kind == PickupKind.RescueShip)
            {
                Lives++;
                ShowBanner("RESCUE +1 SHIP", 2);
                audio.Play(SoundCue.Life);
            }
            else
            {
                AwardCanister();
                if (IsDemoMode) demoPowerupCollected = true;
            }
        }

        Asteroid? bonusImpact = !PlayerRespawning && IsBonusStage && !BonusStageFailed
            ? Asteroids.FirstOrDefault(a => a.Alive && a.ExitsArena && Touching(Player, a))
            : null;
        if (bonusImpact is not null)
        {
            FailBonusStage(bonusImpact);
            return;
        }

        if (!PlayerRespawning && Player.Invulnerable <= 0)
        {
            Body? danger = Asteroids.Cast<Body>().Concat(Fighters).Concat(Mines).Concat(Vortices)
                .FirstOrDefault(b => b.Alive && Touching(Player, b));
            if (danger is not null)
            {
                if (danger is GravityVortex blackHole)
                {
                    CollapseVortex(blackHole, false);
                    DamagePlayer(true);
                }
                else if (danger is Asteroid asteroid && Player.Shielding && Player.Shield > 0)
                {
                    RamAsteroid(asteroid);
                }
                else
                {
                    if (danger is Asteroid { ExitsArena: true } bonusAsteroid)
                        bonusAsteroid.Alive = false;
                    if (danger is HomingMine mine) DestroyMine(mine);
                    DamagePlayer();
                }
            }
        }
    }

    private void FailBonusStage(Asteroid impact)
    {
        if (Mode != GameMode.Playing || !IsBonusStage || BonusStageFailed) return;

        BonusStageFailed = true;
        bonusAsteroidsRemaining = 0;
        foreach (Asteroid asteroid in Asteroids.Where(a => a.ExitsArena)) asteroid.Alive = false;
        WaveBaseCash = 0;
        WaveCometCash = 0;
        LevelBonusCash = 0;
        Multiplier = 1;

        Explosion(impact.Position, 30, 0xffff6b64);
        Shockwaves.Add(new Shockwave(impact.Position, .65, 0xffff745f, 155));
        FloatingTexts.Add(new FloatingText(impact.Position, "BONUS FAILED  $0", 0xffff8175));
        Player.Velocity *= .35;
        ShowBanner("BONUS STAGE FAILED - $0", 2.2);
        audio.Play(SoundCue.BonusFailed, .9);
        BeginWaveOutro();
    }

    private void RamAsteroid(Asteroid asteroid)
    {
        double shieldCost = asteroid.Size switch { 3 => 24, 2 => 17, _ => 10 };
        Player.Shield = Math.Max(0, Player.Shield - shieldCost);
        V2 deflection = (asteroid.ExitsArena
            ? Player.Position - asteroid.Position
            : WrappedDelta(asteroid.Position, Player.Position)).Normalized;
        if (deflection.LengthSquared < .001) deflection = -Player.Velocity.Normalized;
        Player.Velocity += deflection * (65 + asteroid.Size * 22);
        Player.Invulnerable = .65;

        Shockwaves.Add(new Shockwave(Player.Position, .34, 0xff65e7ff, 72 + asteroid.Size * 10));
        if (asteroid.ExitsArena)
        {
            asteroid.Alive = false;
            Explosion(asteroid.Position, 20, 0xffbdefff);
            RegisterShieldImpact(asteroid.Position, 1);
            return;
        }
        if (asteroid.Steel)
        {
            asteroid.Steel = false;
            asteroid.HitPoints = 1;
            ShowBanner("STEEL CORE RAMMED", 1.2);
        }
        HitAsteroid(asteroid);
        RegisterShieldImpact(asteroid.Position, 1);
    }

    private void RegisterShieldImpact(V2 position, double strength)
    {
        ShieldImpactPoint = position;
        ShieldImpactTime = .42;
        Shockwaves.Add(new Shockwave(Player.Position, .48, 0xff73efff, 118));
        Shockwaves.Add(new Shockwave(position, .32, 0xffe8ffff, 62));
        Spark(position, 0xffd9ffff, 18);
        Spark(Player.Position, 0xff55dfff, 10);
        audio.Play(SoundCue.ShieldImpact, Math.Clamp(strength, 0, 1));
    }

    private void HitAsteroid(Asteroid asteroid)
    {
        if (asteroid.ExitsArena)
        {
            Spark(asteroid.Position, 0xffd9f7ff, 8);
            audio.Play(SoundCue.SteelHit, .55);
            return;
        }

        if (asteroid.Steel)
        {
            asteroid.HitPoints--;
            Spark(asteroid.Position, 0xffd9f7ff, 8);
            audio.Play(SoundCue.SteelHit, .55);
            if (asteroid.HitPoints <= 0)
            {
                if (random.Next(10) == 0)
                {
                    asteroid.Steel = false;
                    asteroid.HitPoints = 1;
                    ShowBanner("STEEL CORE FRACTURED", 1.4);
                }
                else if (random.Next(5) == 0)
                {
                    asteroid.Alive = false;
                    Mines.Add(new HomingMine(asteroid.Position, asteroid.Velocity));
                    audio.Play(SoundCue.Mine);
                }
                else asteroid.HitPoints = 5;
            }
            return;
        }

        asteroid.Alive = false;
        AddScore(asteroid.Size switch { 3 => 20, 2 => 50, _ => 100 });
        Explosion(asteroid.Position, asteroid.Size == 3 ? 26 : 15, 0xffff9b4a);
        if (asteroid.Size > 1)
        {
            int fragments = RollAsteroidFragmentCount();
            for (int i = 0; i < fragments; i++)
            {
                V2 velocity = asteroid.Velocity * .4 + RandomDirection() * random.Next(75, 175);
                Asteroids.Add(new Asteroid(asteroid.Position + RandomDirection() * 8, velocity,
                    asteroid.Size - 1, false, random.Next()));
            }
        }
        RollDrop(asteroid.Position);
        audio.Play(SoundCue.AsteroidExplosion, asteroid.Size == 3 ? 1 : asteroid.Size == 2 ? .9 : .82);
    }

    private int RollAsteroidFragmentCount()
    {
        if (random.Next(3) != 0) return 3;
        return random.Next(2) + 1;
    }

    private void DestroyFighter(Fighter fighter)
    {
        fighter.Alive = false;
        if (IsDemoMode) demoEnemyDestroyed = true;
        AwardImmediateScore(fighter.Kind == FighterKind.Interceptor ? 1000 : 500, fighter.Position);
        Explosion(fighter.Position, 24, fighter.Kind == FighterKind.Interceptor ? 0xff58e9ff : 0xffff4f83);
        RollDrop(fighter.Position, .24);
        audio.Play(SoundCue.Explosion, .75);
    }

    private void DestroyMine(HomingMine mine)
    {
        mine.Alive = false;
        AddScore(500);
        Explosion(mine.Position, 18, 0xffffdf4d);
        audio.Play(SoundCue.Explosion, .55);
    }

    private void DestroyVortex(GravityVortex vortex)
    {
        CollapseVortex(vortex, true);
    }

    private void CollapseVortex(GravityVortex vortex, bool awardScore)
    {
        vortex.Alive = false;
        if (IsDemoMode && awardScore) demoBlackHoleDestroyed = true;
        if (awardScore) AddScore(2000);
        Shockwaves.Add(new Shockwave(vortex.Position, .8, 0xffb069ff, 230));
        Explosion(vortex.Position, 40, 0xff6ad7ff);
        audio.Play(SoundCue.Vortex);
    }

    private void DamagePlayer(bool bypassShield = false, V2? impactPosition = null)
    {
        if (PlayerRespawning || Player.Invulnerable > 0) return;
        if (IsDemoMode)
        {
            if (!bypassShield && Player.Shielding && Player.Shield > 0)
            {
                Player.Shield = Math.Max(0, Player.Shield - 10);
                RegisterShieldImpact(impactPosition ?? Player.Position, .82);
            }
            Player.Invulnerable = .35;
            return;
        }
        if (!bypassShield && Player.Shielding && Player.Shield > 0)
        {
            Player.Shield = Math.Max(0, Player.Shield - 18);
            RegisterShieldImpact(impactPosition ?? Player.Position, .92);
            return;
        }

        SpawnShipWreck();
        Lives--;
        ClearEquippedPowerups();
        LastPowerup = null;
        LastPowerupTime = 0;
        Explosion(Player.Position, 76, 0xff62e6ff);
        Shockwaves.Add(new Shockwave(Player.Position, 1.05, 0xffff6b5e, 265));
        Shockwaves.Add(new Shockwave(Player.Position, .68, 0xffffc06a, 145));
        Spark(Player.Position, 0xffffffff, 28);
        audio.Play(SoundCue.ShipCrash, 1);
        audio.Play(SoundCue.ShipBlast, 1);
        if (Lives <= 0)
        {
            audio.StopMusic(false);
            pendingGameOverHighScore = HighScores.Count < 10 || Score > HighScores[^1].Score;
            PendingName = "";
            gameOverDelayTimer = GameOverDelayDuration;
            gameOverFadeElapsed = 0;
            Mode = GameMode.GameOverDelay;
            ShowBanner("SIGNAL LOST", 99);
            return;
        }
        Player.Thrusting = false;
        Player.Shielding = false;
        shieldReleaseTimer = 0;
        fireWasDown = false;
        respawnTimer = RespawnDelay;
        ShowBanner("SHIP DESTROYED", RespawnDelay);
    }

    private void UpdateDeathEffects(double dt)
    {
        foreach (var particle in Particles)
        {
            particle.Age += dt;
            particle.Position += particle.Velocity * dt;
            particle.Velocity *= Math.Pow(.96, dt * 60);
            if (particle.Age >= particle.Lifetime) particle.Alive = false;
        }
        foreach (var ring in Shockwaves)
        {
            ring.Age += dt;
            if (ring.Age >= ring.Lifetime) ring.Alive = false;
        }
        foreach (var text in FloatingTexts) text.Age += dt;
        UpdateShipDebris(dt);
        Particles.RemoveAll(particle => !particle.Alive);
        Shockwaves.RemoveAll(ring => !ring.Alive);
        FloatingTexts.RemoveAll(text => !text.Alive);
    }

    private void RespawnPlayer()
    {
        CenterPlayerWithShield();
        Player.Shield = 67;
        ShowBanner("SHIP RESTORED", 1.4);
    }

    private void AwardCanister()
    {
        PowerupKind power = (PowerupKind)random.Next(8);
        LastPowerup = power;
        LastPowerupTime = 4;
        switch (power)
        {
            case PowerupKind.RapidFire:
                RapidFireActive = true;
                rapidFireRoundsFired = 0;
                rapidFireReload = 0;
                break;
            case PowerupKind.AirBrakes: AirBrakesActive = true; break;
            case PowerupKind.Luck:
                LuckActive = true;
                EnsureLuckyWaveEvents();
                break;
            case PowerupKind.TripleFire: TripleFireActive = true; break;
            case PowerupKind.LongRange: LongRangeActive = true; break;
            case PowerupKind.Shields: Player.Shield = Math.Min(100, Player.Shield + 65); break;
            case PowerupKind.Freeze: FreezeTime = 8; break;
            case PowerupKind.SmartBomb: SmartBomb(); break;
        }
        ShowBanner(PowerName(power), 2.2);
        audio.Play(SoundCue.ChaChing, .9);
    }

    private void ClearEquippedPowerups()
    {
        RapidFireActive = false;
        rapidFireRoundsFired = 0;
        rapidFireReload = 0;
        AirBrakesActive = false;
        LuckActive = false;
        TripleFireActive = false;
        LongRangeActive = false;
        FreezeTime = 0;
    }

    private void SmartBomb()
    {
        var fragments = new List<Asteroid>();
        foreach (var asteroid in Asteroids.Where(a => a.Alive).ToArray())
        {
            if (asteroid.Steel)
            {
                asteroid.HitPoints = Math.Max(1, asteroid.HitPoints - 3);
                Spark(asteroid.Position, 0xffd9f7ff, 12);
                continue;
            }
            if (asteroid.Size <= 1)
            {
                asteroid.Velocity += RandomDirection() * 85;
                continue;
            }

            asteroid.Alive = false;
            AddScore(asteroid.Size switch { 3 => 20, 2 => 50, _ => 100 });
            Explosion(asteroid.Position, 12, 0xffffbd5a);
            int fragmentCount = RollAsteroidFragmentCount();
            for (int i = 0; i < fragmentCount; i++)
            {
                V2 direction = RandomDirection();
                fragments.Add(new Asteroid(asteroid.Position + direction * 8,
                    asteroid.Velocity * .45 + direction * random.Next(105, 190), asteroid.Size - 1, false, random.Next()));
            }
        }
        Asteroids.AddRange(fragments);
        foreach (var fighter in Fighters.Where(f => f.Alive).ToArray()) DestroyFighter(fighter);
        foreach (var mine in Mines.Where(m => m.Alive).ToArray()) DestroyMine(mine);
        Shockwaves.Add(new Shockwave(Player.Position, 1.1, 0xffffffff, 900));
        audio.Play(SoundCue.Nova);
    }

    private void DetonateNova(Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        foreach (var asteroid in Asteroids.Where(a => a.Alive && !a.Steel).ToArray()) HitAsteroid(asteroid);
        foreach (var fighter in Fighters.Where(f => f.Alive).ToArray()) DestroyFighter(fighter);
        foreach (var mine in Mines.Where(m => m.Alive).ToArray()) DestroyMine(mine);
        Shockwaves.Add(new Shockwave(nova.Position, 1.45, 0xffffe8a0, 1250));
        Explosion(nova.Position, 80, 0xffffffff);
        TriggerScreenShake(.9, 14);
        if (!Player.Shielding) DamagePlayer();
        audio.Play(SoundCue.Nova, 1);
        ShowBanner("SUPERNOVA", 2.2);
    }

    private void NeutralizeNova(Nova nova)
    {
        nova.Detonated = true;
        nova.Alive = false;
        AddScore(500);
        Spark(nova.Position, 0xffa7efff, 16);
        Shockwaves.Add(new Shockwave(nova.Position, .42, 0xffa7efff, 68));
        ShowBanner("NOVA NEUTRALIZED", 1.8);
        audio.Play(SoundCue.Pickup, .58);
    }

    private void ScheduleEvents(double dt)
    {
        if (IsBonusStage)
        {
            UpdateBonusAsteroidStream(dt);
            bool bonusClear = bonusAsteroidsRemaining == 0 && Asteroids.All(asteroid => !asteroid.Alive);
            if (bonusClear)
            {
                nextWaveTimer += dt;
                if (nextWaveTimer > 1.6) BeginWaveOutro();
            }
            else nextWaveTimer = 0;
            return;
        }

        if (rescueTimer > 0)
        {
            rescueTimer -= dt;
            if (rescueTimer <= 0)
            {
                SpawnRescueShip();
                rescueTimer = -1;
            }
        }

        if (canisterTimer > 0)
        {
            canisterTimer -= dt;
            if (canisterTimer <= 0) SpawnCanister();
        }

        if (multiplierTimer > 0)
        {
            multiplierTimer -= dt;
            if (multiplierTimer <= 0) SpawnMultiplier();
        }

        if (cometTimer > 0)
        {
            cometTimer -= dt;
            if (cometTimer <= 0) SpawnComet();
        }

        if (blackHoleTimer > 0)
        {
            blackHoleTimer -= dt;
            if (blackHoleTimer <= 0) SpawnVortex();
        }

        if (canisterStormRemaining > 0)
        {
            canisterStormSpawnTimer -= dt;
            if (canisterStormSpawnTimer <= 0)
            {
                SpawnCanisterEntity();
                canisterStormRemaining--;
                canisterStormSpawnTimer = .42 + random.NextDouble() * .34;
            }
        }

        if (cometStormRemaining > 0)
        {
            cometStormSpawnTimer -= dt;
            if (cometStormSpawnTimer <= 0)
            {
                SpawnCometEntity();
                cometStormRemaining--;
                cometStormSpawnTimer = .48 + random.NextDouble() * .38;
            }
        }

        eventTimer -= dt;
        if (eventTimer <= 0)
        {
            eventTimer = Math.Max(4.5, 10.5 - Wave * .28) + random.NextDouble() * 5;
            int roll = random.Next(100);
            if (roll < 31) SpawnFighter();
            else if (roll < 49 && Wave >= 2) SpawnMine();
            else if (roll < 63 && Wave >= 4) SpawnNova();
            else if (roll < 76) SpawnBonusPickup();
            else if (roll < 90) SpawnBonusPickup();
            else SpawnFighter();
        }

        bool pendingStorm = canisterStormWave && !canisterSpawned || cometStormWave && !cometSpawned ||
            canisterStormRemaining > 0 || cometStormRemaining > 0;
        bool waveClear = !PlayerRespawning && Asteroids.All(a => !a.Alive) && Fighters.All(f => !f.Alive) &&
            Vortices.All(v => !v.Alive) && !pendingStorm && blackHoleTimer <= 0;
        if (waveClear)
        {
            nextWaveTimer += dt;
            if (nextWaveTimer > 1.6) BeginWaveOutro();
        }
        else nextWaveTimer = 0;
    }

    private void BeginNextWave()
    {
        Mode = GameMode.Playing;
        CashConfettiTime = 0;
        CenterPlayerWithShield();
        Wave++;
        IsBonusStage = Wave % 5 == 0;
        BonusStageFailed = false;
        respawnTimer = 0;
        fighterSpawnedThisWave = false;
        BonusTravelTime = 0;
        BonusAsteroidsDodged = 0;
        WaveBaseCash = 0;
        WaveCometCash = 0;
        Multiplier = 1;
        canisterSpawned = false;
        multiplierSpawned = false;
        cometSpawned = false;
        blackHoleSpawned = false;
        bonusSpawnsDisabled = false;
        LevelBonusCash = 2_000;
        levelBonusCountdown = 5;
        canisterStormRemaining = 0;
        cometStormRemaining = 0;
        canisterStormWave = !IsBonusStage && random.NextDouble() < .075;
        cometStormWave = !IsBonusStage && random.NextDouble() < .075;
        bool lucky = LuckActive;
        canisterTimer = !IsBonusStage && (canisterStormWave || lucky || random.Next(3) == 0) ? 2.5 + random.NextDouble() * 7.5 : -1;
        multiplierTimer = !IsBonusStage && (lucky || random.Next(3) == 0) ? 3 + random.NextDouble() * 7.5 : -1;
        cometTimer = !IsBonusStage && (cometStormWave || lucky || random.Next(3) == 0) ? 3.5 + random.NextDouble() * 7.5 : -1;
        blackHoleTimer = !IsBonusStage && Wave > 1 && random.NextDouble() < .125 ? 3 + random.NextDouble() * 8 : -1;
        BonusAsteroidTotal = 0;
        BonusAsteroidsDodged = 0;
        bonusAsteroidsRemaining = 0;
        if (IsBonusStage)
        {
            int bonusStageNumber = Wave / 5;
            BonusAsteroidTotal = 12 + bonusStageNumber * 4;
            bonusAsteroidsRemaining = BonusAsteroidTotal;
            bonusAsteroidSpawnTimer = .9;
            eventTimer = double.MaxValue;
            rescueTimer = -1;
        }
        else
        {
            int rocks = 3 + (Wave - 1) / 2;
            for (int i = 0; i < rocks; i++)
            {
                V2 pos = SafeEdgePosition();
                bool steel = Wave >= 4 && i == rocks - 1 && Wave % 3 == 1;
                Asteroids.Add(new Asteroid(pos, RandomDirection() * random.Next(32, 78 + Wave * 3), 3, steel, random.Next()));
            }
            if (Wave >= 2 && Wave % 2 == 0) SpawnFighter();
            eventTimer = 6 + random.NextDouble() * 4;
            double rescueChance = Math.Min(.30, .12 + Wave * .008 + (LuckActive ? .10 : 0));
            rescueTimer = random.NextDouble() < rescueChance ? 5 + random.NextDouble() * 10 : -1;
        }
        nextWaveTimer = 0;
        if (IsBonusStage) audio.StartBonusMusic();
        else if (Wave == 1) audio.StartMusic();
        else audio.EnsureNormalMusic();
        ShowBanner(IsBonusStage ? "BONUS STAGE - DODGE THE METAL STORM" : $"WAVE {Wave}", IsBonusStage ? 4 : 2.2);
        audio.Play(SoundCue.Wave, .8);
    }

    private void UpdateBonusAsteroidStream(double dt)
    {
        if (bonusAsteroidsRemaining <= 0) return;
        bonusAsteroidSpawnTimer -= dt;
        if (bonusAsteroidSpawnTimer > 0) return;

        int bonusStageNumber = Wave / 5;
        int spawnIndex = BonusAsteroidTotal - bonusAsteroidsRemaining;
        bool aimedPass = spawnIndex % 3 == 1;
        double x = Width + 55 + random.NextDouble() * 105;
        double y = aimedPass ? -65 : -75 + random.NextDouble() * 170;
        V2 origin = new(x, y);

        V2 target;
        if (aimedPass)
        {
            V2 projectedPlayer = Player.Position + Player.Velocity * .42;
            target = new V2(
                Math.Clamp(projectedPlayer.X, 70, Width * .76),
                Math.Clamp(projectedPlayer.Y + random.Next(-22, 23), 24, Height - 28));
        }
        else
        {
            int laneCount = Math.Clamp(5 + bonusStageNumber, 6, 10);
            int lane = spawnIndex % laneCount;
            double laneY = 58 + lane / (double)(laneCount - 1) * (Height + 42);
            target = new V2(-120, Math.Max(y + 38, laneY));
        }

        V2 direction = (target - origin).Normalized;
        double speed = 300 + bonusStageNumber * 22 + random.NextDouble() * 75;
        Asteroids.Add(new Asteroid(origin, direction * speed, 3, true, random.Next(), true));
        bonusAsteroidsRemaining--;
        bonusAsteroidSpawnTimer = Math.Max(.27, .64 - bonusStageNumber * .045) + random.NextDouble() * .14;
    }

    private void SpawnFighter()
    {
        if (fighterSpawnedThisWave || Fighters.Any(f => f.Alive)) return;
        fighterSpawnedThisWave = true;
        FighterKind kind = Wave >= 3 && random.Next(5 + Wave) > 6 ? FighterKind.Interceptor : FighterKind.Raider;
        V2 pos = SafeEdgePosition();
        Fighters.Add(new Fighter(pos, RandomDirection() * 80, kind));
        audio.Play(SoundCue.EnemyWarning, .68);
    }

    private void SpawnMine()
    {
        Mines.Add(new HomingMine(SafeEdgePosition(), RandomDirection() * 75));
        ShowBanner("HOMING MINE", 1.4);
    }

    private void SpawnVortex()
    {
        if (blackHoleSpawned) return;
        blackHoleSpawned = true;
        blackHoleTimer = -1;
        Vortices.Add(new GravityVortex(SafePosition(250)));
        ShowBanner("BLACK HOLE", 1.6);
    }

    private void SpawnNova()
    {
        Novas.Add(new Nova(SafePosition(300)));
        ShowBanner("NOVA DETECTED", 1.8);
    }

    private void SpawnCanister()
    {
        if (bonusSpawnsDisabled) return;
        if (canisterSpawned) return;
        canisterSpawned = true;
        canisterTimer = -1;

        if (canisterStormWave)
        {
            canisterStormRemaining = random.Next(5, 11);
            ShowBanner("ITEM BOX STORM", 1.8);
            SpawnCanisterEntity();
            canisterStormRemaining--;
            canisterStormSpawnTimer = .5;
            return;
        }

        SpawnCanisterEntity();
    }

    private void SpawnCanisterEntity()
    {
        if (bonusSpawnsDisabled) return;
        V2 position = SafeEdgePosition();
        V2 destination = new(Width * (.3 + random.NextDouble() * .4), Height * (.28 + random.NextDouble() * .44));
        V2 velocity = (destination - position).Normalized * random.Next(48, 82) + RandomDirection() * 15;
        Pickups.Add(new Pickup(position, velocity, PickupKind.Canister));
    }

    private void SpawnComet()
    {
        if (bonusSpawnsDisabled) return;
        if (cometSpawned) return;
        cometSpawned = true;
        cometTimer = -1;

        if (cometStormWave)
        {
            cometStormRemaining = random.Next(5, 11);
            ShowBanner("COMET STORM", 1.8);
            SpawnCometEntity();
            cometStormRemaining--;
            cometStormSpawnTimer = .55;
            return;
        }

        SpawnCometEntity();
        ShowBanner("BONUS COMET", 1.25);
    }

    private void SpawnCometEntity()
    {
        if (bonusSpawnsDisabled) return;
        bool fromLeft = random.Next(2) == 0;
        V2 position = new(fromLeft ? 1 : Width - 1, 100 + random.NextDouble() * (Height - 200));
        V2 target = new(fromLeft ? Width + 100 : -100, 90 + random.NextDouble() * (Height - 180));
        int value = CometValues[random.Next(CometValues.Length)];
        uint tint = value switch
        {
            >= 5000 => 0xffff6e9e,
            >= 3000 => 0xffc977ff,
            >= 2000 => 0xff62dcff,
            >= 1000 => 0xff63f0ca,
            _ => 0xffffc65b
        };
        Comets.Add(new Comet(position, (target - position).Normalized * random.Next(310, 430), value, tint));
    }

    private void SpawnMultiplier()
    {
        if (bonusSpawnsDisabled) return;
        if (multiplierSpawned) return;
        multiplierSpawned = true;
        multiplierTimer = -1;
        Pickups.Add(new Pickup(SafePosition(180), V2.Zero, PickupKind.Multiplier, random.Next(2, 6)));
    }

    private void SpawnBonusPickup(V2? at = null)
    {
        if (bonusSpawnsDisabled) return;
        int value = random.Next(6) == 0 ? 500 : random.Next(1, 6) * 1000;
        Pickups.Add(new Pickup(at ?? SafeEdgePosition(), RandomDirection() * 45, PickupKind.Bonus, value));
    }

    private void SpawnRescueShip()
    {
        if (bonusSpawnsDisabled) return;
        if (Pickups.Any(p => p.Alive && p.Kind == PickupKind.RescueShip)) return;
        V2 position = random.Next(4) switch
        {
            0 => new V2(random.NextDouble() * Width, -38),
            1 => new V2(Width + 38, random.NextDouble() * Height),
            2 => new V2(random.NextDouble() * Width, Height + 38),
            _ => new V2(-38, random.NextDouble() * Height)
        };
        V2 destination = new(Width * (.3 + random.NextDouble() * .4), Height * (.28 + random.NextDouble() * .44));
        var rescue = new Pickup(position, (destination - position).Normalized * random.Next(78, 108), PickupKind.RescueShip)
        {
            Angle = random.NextDouble() * Math.PI * 2
        };
        Pickups.Add(rescue);
        ShowBanner("RESCUE SHIP", 1.7);
    }

    private void RollDrop(V2 at, double chance = .09)
    {
        if (random.NextDouble() < chance * .35) SpawnBonusPickup(at);
    }

    private void AddScore(int basePoints)
    {
        WaveBaseCash += basePoints;
    }

    private void AwardImmediateScore(int amount, V2 position)
    {
        BankScore(amount);
        FloatingTexts.Add(new FloatingText(position, $"+${amount:N0}", 0xffffd76a));
    }

    private void AddCometCash(int amount)
    {
        WaveCometCash += amount;
    }

    private void BankScore(int amount)
    {
        if (amount <= 0) return;
        Score += amount;
        while (Score >= nextLifeScore)
        {
            Lives++;
            nextLifeScore += 50_000;
            ShowBanner("EXTRA SHIP", 2);
            audio.Play(SoundCue.Life);
        }
    }

    private void EnsureLuckyWaveEvents()
    {
        if (bonusSpawnsDisabled) return;
        if (!canisterSpawned && canisterTimer <= 0) canisterTimer = .8 + random.NextDouble() * 2.8;
        if (!multiplierSpawned && multiplierTimer <= 0) multiplierTimer = 1.1 + random.NextDouble() * 3.0;
        if (!cometSpawned && cometTimer <= 0) cometTimer = 1.4 + random.NextDouble() * 3.2;
    }

    private void BeginWaveSummary()
    {
        if (Mode != GameMode.WaveOutro) return;
        Pickups.Clear();
        Comets.Clear();
        FloatingTexts.Clear();
        Shots.Clear();
        Mines.Clear();
        Vortices.Clear();
        Novas.Clear();
        SummaryBaseCash = WaveBaseCash;
        SummaryCometCash = WaveCometCash;
        SummaryLevelBonusCash = LevelBonusCash;
        SummaryMultiplier = Multiplier;
        SummaryTotalCash = SummaryBaseCash + SummaryLevelBonusCash + SummaryCometCash * SummaryMultiplier;
        SummaryDeposited = 0;
        summaryElapsed = 0;
        summaryScreenElapsed = 0;
        cashTickCooldown = 0;
        CashConfettiTime = SummaryTotalCash > 10_000 ? 2.7 : 0;
        TransitionAlpha = 1;
        Mode = GameMode.WaveSummary;
        if (CashConfettiTime > 0) audio.Play(SoundCue.CashBonus, .9);
    }

    private void UpdateWaveSummary(double dt)
    {
        summaryScreenElapsed += dt;
        TransitionAlpha = Math.Clamp(1 - summaryScreenElapsed / SummaryFadeInDuration, 0, 1);
        if (summaryScreenElapsed < SummaryFadeInDuration) return;

        if (SummaryComplete) return;

        summaryElapsed += dt;
        cashTickCooldown -= dt;
        double duration = Math.Clamp(1.6 + SummaryTotalCash / 7000.0, 1.6, 4.2);
        double progress = Math.Clamp(summaryElapsed / duration, 0, 1);
        double eased = 1 - Math.Pow(1 - progress, 3);
        int targetDeposit = progress >= 1 ? SummaryTotalCash : (int)Math.Round(SummaryTotalCash * eased);
        int delta = targetDeposit - SummaryDeposited;
        if (delta > 0)
        {
            BankScore(delta);
            SummaryDeposited += delta;
            if (cashTickCooldown <= 0)
            {
                audio.Play(SoundCue.CashRegister, .48);
                cashTickCooldown = .075;
            }
        }
    }

    private void UpdateLevelBonus(double dt)
    {
        if (LevelBonusCash <= 0) return;
        levelBonusCountdown -= dt;
        while (levelBonusCountdown <= 0 && LevelBonusCash > 0)
        {
            LevelBonusCash = Math.Max(0, LevelBonusCash - 50);
            levelBonusCountdown += 5;
            if (LevelBonusCash <= 1_000 && !bonusSpawnsDisabled) DisableBonusSpawns();
        }
    }

    private void DisableBonusSpawns()
    {
        bonusSpawnsDisabled = true;
        canisterTimer = -1;
        multiplierTimer = -1;
        cometTimer = -1;
        rescueTimer = -1;
        canisterStormRemaining = 0;
        cometStormRemaining = 0;
        canisterStormWave = false;
        cometStormWave = false;
    }

    private void CompleteWaveSummary()
    {
        int remaining = SummaryTotalCash - SummaryDeposited;
        if (remaining > 0)
        {
            BankScore(remaining);
            SummaryDeposited += remaining;
            audio.Play(SoundCue.CashRegister, .65);
        }
    }

    private void BeginWaveOutro()
    {
        if (Mode != GameMode.Playing) return;
        if (IsBonusStage) audio.StopMusic(false);
        Mode = GameMode.WaveOutro;
        transitionElapsed = 0;
        TransitionAlpha = 0;
        Player.Thrusting = false;
        Player.Shielding = false;
    }

    private void UpdateWaveOutro(double dt)
    {
        transitionElapsed += dt;
        TransitionAlpha = Math.Clamp(transitionElapsed / FadeToSummaryDuration, 0, 1);
        if (transitionElapsed >= FadeToSummaryDuration) BeginWaveSummary();
    }

    private void BeginWaveSummaryExit()
    {
        Mode = GameMode.WaveSummaryExit;
        transitionElapsed = 0;
        TransitionAlpha = 0;
    }

    private void UpdateWaveSummaryExit(double dt)
    {
        transitionElapsed += dt;
        TransitionAlpha = Math.Clamp(transitionElapsed / FadeToWaveDuration, 0, 1);
        if (transitionElapsed < FadeToWaveDuration) return;

        BeginNextWave();
        Mode = GameMode.WaveIntro;
        transitionElapsed = 0;
        TransitionAlpha = 1;
    }

    private void UpdateWaveIntro(double dt)
    {
        transitionElapsed += dt;
        TransitionAlpha = Math.Clamp(1 - transitionElapsed / WaveFadeInDuration, 0, 1);
        if (transitionElapsed < WaveFadeInDuration) return;

        TransitionAlpha = 0;
        Mode = GameMode.Playing;
    }

    private void Hyperspace()
    {
        if (PlayerRespawning || Player.Invulnerable > 1) return;
        Explosion(Player.Position, 10, 0xff68d9ff);
        Player.Position = SafePosition(0);
        Player.Velocity *= .25;
        Player.Invulnerable = 1.2;
        if (random.NextDouble() < .08 && !LuckActive) DamagePlayer();
        else Shockwaves.Add(new Shockwave(Player.Position, .35, 0xff82ddff, 75));
    }

    private void CenterPlayerWithShield()
    {
        Player.Position = new V2(Width / 2, Height / 2);
        Player.Velocity = V2.Zero;
        Player.Angle = 0;
        Player.Invulnerable = 3;
        Player.SpawnShieldTime = 3;
        Player.Thrusting = false;
        Player.Shielding = false;
        shieldReleaseTimer = 0;
        ShieldImpactTime = 0;
        turnVelocity = 0;
        thrustRamp = 0;
    }

    private void SpawnShipWreck()
    {
        V2[] offsets = [new V2(15, 0), new V2(-4, -10), new V2(-4, 10), new V2(-12, 0), new V2(3, 0)];
        for (int i = 0; i < offsets.Length; i++)
        {
            V2 outward = Rotate(offsets[i], Player.Angle);
            V2 velocity = Player.Velocity * .32 + outward.Normalized * random.Next(85, 185) + RandomDirection() * random.Next(20, 75);
            double spin = (random.NextDouble() * 2 - 1) * (2.8 + random.NextDouble() * 5.2);
            ShipDebrisPieces.Add(new ShipDebris(Wrap(Player.Position + outward * Ship.VisualScale), velocity,
                i, Player.Angle, spin).Initialize());
        }
    }

    private void UpdateShipDebris(double dt)
    {
        foreach (var piece in ShipDebrisPieces)
        {
            piece.Age += dt;
            piece.Angle += piece.Spin * dt;
            piece.Position = Wrap(piece.Position + piece.Velocity * dt);
            piece.Velocity *= Math.Pow(.994, dt * 60);
            if (piece.Age >= piece.Lifetime) piece.Alive = false;
        }
        ShipDebrisPieces.RemoveAll(piece => !piece.Alive);
    }

    private void EmitThrust()
    {
        V2 back = -V2.FromAngle(Player.Angle);
        for (int i = 0; i < 2; i++)
            Particles.Add(new Particle(Player.Position + back * (18 * Ship.VisualScale) + RandomDirection() * 2,
                Player.Velocity * .2 + back * random.Next(120, 260) + RandomDirection() * 25,
                .28 + random.NextDouble() * .25, i == 0 ? 0xff5be8ff : 0xffff7b45, random.Next(2, 6)));
    }

    private void Spark(V2 position, uint color, int count)
    {
        for (int i = 0; i < count; i++)
            Particles.Add(new Particle(position, RandomDirection() * random.Next(70, 260),
                .2 + random.NextDouble() * .45, color, 2 + random.NextDouble() * 4));
    }

    private void Explosion(V2 position, int count, uint color)
    {
        for (int i = 0; i < count; i++)
        {
            double speed = random.NextDouble() * random.Next(100, 390);
            uint c = i % 4 == 0 ? 0xffffffff : i % 3 == 0 ? 0xffff5a36 : color;
            Particles.Add(new Particle(position, RandomDirection() * speed,
                .35 + random.NextDouble() * .85, c, 2 + random.NextDouble() * 7));
        }
        Shockwaves.Add(new Shockwave(position, .38, color, 55 + count * 1.7));
    }

    private void RemoveDead()
    {
        Asteroids.RemoveAll(x => !x.Alive);
        Fighters.RemoveAll(x => !x.Alive);
        Mines.RemoveAll(x => !x.Alive);
        Vortices.RemoveAll(x => !x.Alive);
        Novas.RemoveAll(x => !x.Alive);
        Pickups.RemoveAll(x => !x.Alive);
        Comets.RemoveAll(x => !x.Alive);
        Shots.RemoveAll(x => !x.Alive);
        Particles.RemoveAll(x => !x.Alive);
        Shockwaves.RemoveAll(x => !x.Alive);
        FloatingTexts.RemoveAll(x => !x.Alive);
        ShipDebrisPieces.RemoveAll(x => !x.Alive);
    }

    private void ClearWorld()
    {
        Asteroids.Clear(); Fighters.Clear(); Mines.Clear(); Vortices.Clear(); Novas.Clear();
        Pickups.Clear(); Comets.Clear(); Shots.Clear(); Particles.Clear(); Shockwaves.Clear(); FloatingTexts.Clear(); ShipDebrisPieces.Clear();
        FreezeTime = 0;
        screenShakeTime = 0;
        screenShakeDuration = 0;
        screenShakeMagnitude = 0;
        IsBonusStage = false;
        BonusStageFailed = false;
        respawnTimer = 0;
        fighterSpawnedThisWave = false;
        BonusTravelTime = 0;
        BonusAsteroidTotal = 0;
        BonusAsteroidsDodged = 0;
        bonusAsteroidsRemaining = 0;
        fireWasDown = false;
        ClearEquippedPowerups();
    }

    private void TriggerScreenShake(double duration, double magnitude)
    {
        screenShakeDuration = Math.Max(screenShakeDuration, duration);
        screenShakeTime = Math.Max(screenShakeTime, duration);
        screenShakeMagnitude = Math.Max(screenShakeMagnitude, magnitude);
    }

    private V2 SafeEdgePosition()
    {
        return random.Next(4) switch
        {
            0 => new V2(random.NextDouble() * Width, 35),
            1 => new V2(Width - 35, random.NextDouble() * Height),
            2 => new V2(random.NextDouble() * Width, Height - 35),
            _ => new V2(35, random.NextDouble() * Height)
        };
    }

    private V2 SafePosition(double distance)
    {
        for (int i = 0; i < 50; i++)
        {
            V2 point = new(90 + random.NextDouble() * (Width - 180), 90 + random.NextDouble() * (Height - 180));
            if (V2.Distance(point, Player.Position) >= distance) return point;
        }
        return new V2(100, 100);
    }

    private V2 RandomDirection()
    {
        double a = random.NextDouble() * Math.PI * 2;
        return V2.FromAngle(a);
    }

    private static bool Touching(Body a, Body b)
    {
        bool onePassAsteroid = a is Asteroid { ExitsArena: true } || b is Asteroid { ExitsArena: true };
        V2 delta = onePassAsteroid ? b.Position - a.Position : WrappedDelta(a.Position, b.Position);
        return delta.LengthSquared < Math.Pow(a.Radius + b.Radius, 2);
    }

    private static bool TouchingComet(Shot shot, Comet comet)
    {
        V2 fromHead = WrappedDelta(comet.Position, shot.Position);
        if (fromHead.LengthSquared < Math.Pow(comet.Radius + shot.Radius, 2)) return true;

        V2 back = -comet.Velocity.Normalized;
        double along = fromHead.X * back.X + fromHead.Y * back.Y;
        if (along < 0 || along > Comet.TrailLength) return false;

        double perpendicular = Math.Abs(fromHead.X * back.Y - fromHead.Y * back.X);
        double taper = along / Comet.TrailLength;
        double trailRadius = 13 * (1 - taper) + 2.5 * taper;
        return perpendicular <= trailRadius + shot.Radius;
    }
    private static V2 Wrap(V2 p) => new((p.X % Width + Width) % Width, (p.Y % Height + Height) % Height);
    private static V2 WrappedDelta(V2 from, V2 to)
    {
        double x = to.X - from.X;
        double y = to.Y - from.Y;
        if (x > Width / 2) x -= Width; else if (x < -Width / 2) x += Width;
        if (y > Height / 2) y -= Height; else if (y < -Height / 2) y += Height;
        return new V2(x, y);
    }

    private static V2 PredictAim(V2 origin, V2 target, V2 targetVelocity, double projectileSpeed)
    {
        V2 delta = WrappedDelta(origin, target);
        double lead = Math.Min(1.1, delta.Length / projectileSpeed);
        return (delta + targetVelocity * lead).Normalized;
    }

    private static V2 Rotate(V2 vector, double angle)
    {
        double c = Math.Cos(angle);
        double s = Math.Sin(angle);
        return new V2(vector.X * c - vector.Y * s, vector.X * s + vector.Y * c);
    }

    private void ShowBanner(string text, double duration)
    {
        Banner = text;
        BannerTime = duration;
    }

    public static string PowerName(PowerupKind kind) => kind switch
    {
        PowerupKind.RapidFire => "RAPID FIRE",
        PowerupKind.AirBrakes => "AIR BRAKES",
        PowerupKind.Luck => "LUCK OF THE IRISH",
        PowerupKind.TripleFire => "TRIPLE FIRE",
        PowerupKind.LongRange => "LONG RANGE",
        PowerupKind.Shields => "SHIELD ENERGY",
        PowerupKind.Freeze => "TIME FREEZE",
        PowerupKind.SmartBomb => "SMART BOMB",
        _ => kind.ToString().ToUpperInvariant()
    };
}

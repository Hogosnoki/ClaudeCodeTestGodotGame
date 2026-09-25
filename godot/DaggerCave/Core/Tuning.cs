namespace DaggerCave;

/// <summary>
/// Every gameplay number worth nudging, in one place. Units: pixels (the player is ~28 px tall;
/// one cave cell is 16 px), seconds, and plain multipliers. Edit, save, press Play.
///
/// These are ordinary static fields rather than constants, so a future in-game debug panel
/// could change them live. Upgrade amounts live in the table in Player/Upgrades.cs, which reads
/// like a spreadsheet already.
/// </summary>
public static class Tune
{
    // =============================================================================== PLAYER
    public static class Hero
    {
        public static float StartHp = 60f;
        /// <summary>Invulnerability after being struck (the Resilience upgrade adds to this).</summary>
        public static float HurtInvuln = 0.4f;
        public static float BreathSeconds = 8f;
        /// <summary>Drowning damage per tick (every 0.5 s) = flat + fraction of max HP.</summary>
        public static float DrownDamageFlat = 5f, DrownDamageFrac = 0.02f;

        public static float RunSpeed = 170f;
        public static float GroundAccel = 1900f, AirAccel = 1200f;

        /// <summary>
        /// Jump shape. Height stays constant while you change Floatiness: gravity is multiplied by
        /// it and jump speed by its square root. Lower = floatier, slower arcs.
        /// </summary>
        public static float JumpVelocity = 470f, Gravity = 1350f, Floatiness = 0.88f;
        public static float MaxFallSpeed = 675f;
        /// <summary>Extra gravity multiplier while falling (snappier landings).</summary>
        public static float FallGravityMult = 1.2f;
        public static float CoyoteTime = 0.1f, JumpBuffer = 0.13f;
        /// <summary>Releasing jump early multiplies upward speed by this (variable jump height).</summary>
        public static float JumpCutMult = 0.5f;
        public static float DoubleJumpMult = 0.9f;

        public static float WallSlideSpeed = 110f, WallJumpPush = 250f, WallJumpMult = 0.92f;
        public static float AirDashSpeed = 540f, AirDashTime = 0.16f;

        public static float SwimSpeed = 150f, SwimAccel = 800f;
        /// <summary>How hard you leap out of the water at the surface (fraction of jump speed).</summary>
        public static float SurfaceLeapMult = 0.95f;

        public static float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCooldown = 0.95f;

        // Swing
        public static float SwingCooldown = 0.36f, SwingActiveTime = 0.11f, ComboWindow = 0.55f;
        public static float SwingReach = 30f, SwingDamage = 10f;
        public static float SwingArcDegrees = 115f, FinisherArcDegrees = 170f;
        public static float FinisherDamageMult = 2f, FinisherReachMult = 1.25f;
        /// <summary>Knockback speed by Knockback upgrade level (0 = no upgrade).</summary>
        public static float[] KnockbackByLevel = { 40f, 260f, 380f, 480f };
        public static float FinisherExtraKnockback = 120f;
        public static float PogoBounceMult = 0.95f;

        // Throw
        public static float ThrowDamage = 16f, ThrowCooldown = 2f;
        public static float ThrowSpeed = 820f, ThrowRange = 560f, ThrowSpinRadPerSec = 44f;
        public static float RicochetRange = 280f, RicochetDamageMult = 0.85f;

        /// <summary>XP needed for the next level: Base + Linear*L + Quadratic*L^2.</summary>
        public static float XpBase = 12f, XpLinear = 8f, XpQuadratic = 1.6f;
        public static float XpMagnetRange = 85f;
    }

    // =============================================================================== HIT FEEL
    public static class Feel
    {
        /// <summary>Freeze-frame lengths (seconds). The game runs at HitStopTimeScale meanwhile.</summary>
        public static float HitStopNormal = 0.08f, HitStopKill = 0.12f, HitStopFinisher = 0.15f;
        public static float HitStopThrown = 0.065f, HitStopPlayerHurt = 0.1f;
        public static float HitStopTimeScale = 0.02f;
        /// <summary>Slow motion on mini-boss / boss kills: (seconds, time scale).</summary>
        public static float EliteKillSlowMo = 0.45f, EliteKillSlowMoScale = 0.25f;
        public static float BossKillSlowMo = 1.3f, BossKillSlowMoScale = 0.15f;
        /// <summary>Camera nudge (pixels) in the direction of a blow.</summary>
        public static float KickNormal = 2.5f, KickKill = 4f, KickFinisher = 6f, KickPlayerHurt = 6f;
        public static float ShakeFinisher = 5f, ShakeKill = 3f;
        public static float CameraZoom = 2.1f, CameraFollowSharpness = 7f;
    }

    // =============================================================================== DIFFICULTY
    public static class Difficulty
    {
        /// <summary>Enemy health and damage double every this many minutes (continuous curve).</summary>
        public static float DoublingMinutes = 10f;
        /// <summary>Enemy speed/attack-rate follows the same curve but is capped here (2 = at most twice as fast).</summary>
        public static float TempoCap = 2f;
        /// <summary>Spawn intensity ("pace") reaches 1.0 after this many minutes, capped at PaceMax.</summary>
        public static float PaceMinutesPerStep = 10f, PaceMax = 3f;
    }

    // =============================================================================== SPAWNING
    public static class Spawning
    {
        /// <summary>Share of resident spawn points that hold enemies at the start, rising to 100% by FillMinutes.</summary>
        public static float ResidentFillStart = 0.4f, ResidentFillMinutes = 8f;
        /// <summary>Residents are created this far away at most, and only when off-camera.</summary>
        public static float ResidentMaxDistance = 720f;
        /// <summary>Base respawn delay for a used resident point (divided by 1 + pace).</summary>
        public static float ResidentRespawnMin = 90f, ResidentRespawnMax = 150f;

        /// <summary>Entrance waves: first after FirstWave s; the interval starts at IntervalStart and
        /// shrinks as IntervalStart / (1 + time / IntervalRampSeconds), never below IntervalMin.</summary>
        public static float FirstWave = 18f, IntervalStart = 22f, IntervalMin = 4.5f, IntervalRampSeconds = 160f;
        /// <summary>Entrance distance along the tunnels, in cells (16 px).</summary>
        public static int EntranceMinCells = 24, EntranceMaxCells = 34;
        /// <summary>Chance an above-water entrance is bats (otherwise ground walkers).</summary>
        public static float EntranceBatChance = 0.35f;

        /// <summary>Max enemies near the player = CapBase + CapPerPace * pace (entrances get +4 headroom).</summary>
        public static int CapBase = 8, CapPerPace = 10;
    }

    // =============================================================================== DROPS
    public static class Drops
    {
        public static float HeartChance = 0.02f, HeartChanceElite = 0.4f;
        public static float HeartHeal = 15f, ChestHeal = 12f;
    }

    // =============================================================================== ENEMIES
    // Hp and Damage are the values at minute 0 (the difficulty curve multiplies them).
    // Speeds are in px/s at minute 0 (the tempo curve multiplies them).

    public static class Elite
    {
        public static float SizeMult = 1.7f, HpMult = 6f, DamageMult = 1.4f, XpMult = 7f, MinKnockResist = 0.6f;
    }

    public static class Bat
    {
        public static float Hp = 12, Contact = 7, WakeRange = 240, FlySpeed = 170, WobbleSpeed = 110;
        public static int Xp = 3;
    }

    public static class Frog
    {
        public static float Hp = 20, Contact = 6, TongueDamage = 9, TongueRange = 90, TongueCooldown = 2.2f;
        public static float AggroRange = 360, HopSpeedX = 170, HopSpeedY = 365, HopCooldownMin = 1.0f, HopCooldownMax = 1.6f;
        public static int Xp = 4;
    }

    public static class Goblin
    {
        public static float Hp = 26, Contact = 4, ClubDamage = 12, RunSpeed = 125, SlingerSpeed = 95;
        public static float AggroRange = 460, WindupTime = 0.38f, RockDamage = 8, ThrowCooldown = 2.4f;
        public static int Xp = 5;
    }

    public static class Spider
    {
        public static float Hp = 18, Contact = 9, CrawlSpeed = 70, DropSpeed = 330, GroundSpeed = 150, PounceCooldown = 1.4f;
        public static int Xp = 4;
    }

    public static class Magma
    {
        public static float Hp = 40, Contact = 10, WalkSpeed = 45, GlobDamage = 9, PuddleDamage = 6, LobCooldown = 3.2f;
        public static float WaterDamagePerSec = 15;
        public static int Xp = 8;
    }

    public static class Golem
    {
        public static float Hp = 90, Contact = 12, WalkSpeed = 40, SlamWindup = 0.8f, SlamCooldown = 3.2f;
        public static float ShockwaveDamage = 14, ShockwaveSpeed = 250, SlamDamage = 18;
        public static int Xp = 14;
    }

    public static class Fish
    {
        public static float Hp = 10, Contact = 7, DartSpeed = 280, AggroRange = 320;
        public static int Xp = 2;
    }

    public static class Urchin
    {
        public static float Hp = 30, Contact = 9, SpikeDamage = 12;
        public static int Xp = 5;
    }

    public static class Eel
    {
        public static float Hp = 24, Contact = 10, LungeLength = 170, LungeSpeed = 460, Cooldown = 1.8f;
        public static int Xp = 6;
    }

    public static class Boss
    {
        public static float Hp = 650, Contact = 16, WalkSpeed = 55, ChargeSpeed = 400;
        public static float SlamDamage = 22, ShockwaveDamage = 16, RockDamage = 14;
        /// <summary>Below this share of health the boss enrages (faster, summons bats).</summary>
        public static float EnrageAt = 0.5f, EnragedSpeedMult = 1.35f;
        public static int Xp = 120;
    }

    // =============================================================================== CAVE
    public static class Cave
    {
        /// <summary>Map size in 16 px cells. Water fills the bottom half.</summary>
        public static int Width = 400, Height = 240;
        /// <summary>Total tunnel-carving steps: more = more (and longer) tunnels.</summary>
        public static int TunnelBudget = 4400;
        public static int MiniBossesMin = 3, MiniBossesMax = 4;
        /// <summary>The boss room must be at least this many cells (straight line) from the start.</summary>
        public static float BossMinDistanceCells = 150;
        public static int Moths = 80, Crabs = 60;
    }

    // =============================================================================== ENEMY BRAINS
    /// <summary>
    /// The neural-network enemy AI (Core/Brain.cs). Each creature type shares one tiny network
    /// that picks its next move every DecisionInterval seconds. Toggle training with F9 (or launch
    /// with --train); lock a type in <see cref="BrainLocks"/> once it is good enough.
    /// </summary>
    public static class Brains
    {
        /// <summary>Master switch. False = every creature uses its original scripted AI.</summary>
        public static bool Enabled = true;
        /// <summary>Outside training, a type only uses its brain once it has this many decisions of
        /// experience (until then the scripted AI plays it, so an untrained brain never ships).</summary>
        public static long MinExperienceToPlay = 3000;

        // --- when decisions happen
        /// <summary>Seconds between decisions (on the creature's own clock), +/- DecisionJitter.</summary>
        public static float DecisionInterval = 0.2f, DecisionJitter = 0.05f;
        /// <summary>Most decisions made across all creatures in one physics frame (the rest wait a frame).</summary>
        public static int MaxDecisionsPerFrame = 12;

        // --- rewards
        /// <summary>Reward per 10% of the player's max HP dealt.</summary>
        public static float DamageDealtReward = 1f;
        /// <summary>Penalty per 100% of its own max HP taken (so dying costs this much at most).</summary>
        public static float DamageTakenPenalty = 1f;
        /// <summary>Penalty per second of being alive and engaged ("punished for living too long").</summary>
        public static float TimePenaltyPerSec = 0.01f;
        /// <summary>Discount per decision (how far ahead it plans: 0.95 = about 20 decisions, 4 s).</summary>
        public static float Gamma = 0.95f;

        // --- learning
        public static float LearningRate = 0.003f;
        /// <summary>Decisions collected per creature type before each optimiser step.</summary>
        public static int BatchSize = 32;
        /// <summary>Keeps it trying new things (higher = more random).</summary>
        public static float EntropyBonus = 0.01f;
        public static float ValueLossWeight = 0.5f;
        public static float GradClip = 5f;
        /// <summary>
        /// Head start: a fresh brain is pulled toward what the scripted AI would do, and early on
        /// the scripted move is sometimes executed outright. The pull halves every TeacherHalfLife
        /// decisions, after which rewards alone shape it. TeacherStart = 0 learns from scratch.
        /// </summary>
        public static float TeacherStart = 1f, TeacherHalfLife = 2500f;
        /// <summary>Outside training the brain samples its moves at this temperature (lower = more
        /// decisive, 1 = as trained).</summary>
        public static float PlayTemperature = 0.7f;
        /// <summary>Autosave interval while training, in seconds.</summary>
        public static float AutosaveSeconds = 60f;
    }
}

/// <summary>
/// Learning locks, one per creature brain. true = frozen: the creature still uses its brain
/// (once trained) but stops changing it. Lock a type once it's as strong as you want it, then
/// keep training the others.
/// </summary>
public static class BrainLocks
{
    public static bool Bat = false;
    public static bool Frog = false;
    public static bool Goblin = false;
    public static bool Slinger = false;
    public static bool Spider = false;
    public static bool Magma = false;
    public static bool Golem = false;
    public static bool Fish = false;
    public static bool Urchin = false;
    public static bool Eel = false;
    public static bool Boss = false;
}

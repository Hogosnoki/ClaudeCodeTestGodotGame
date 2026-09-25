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
        public static float BreathSeconds = 15f;
        /// <summary>Air bubbles rising from vents on the sea floor: seconds of breath each, how
        /// many vents per cave, and seconds between bubbles from one vent.</summary>
        public static float AirBubbleBreath = 2f, AirVentIntervalMin = 4f, AirVentIntervalMax = 9f;
        public static int AirVents = 28;
        /// <summary>Drowning damage per tick (every 0.5 s) = flat + fraction of max HP.</summary>
        public static float DrownDamageFlat = 3f, DrownDamageFrac = 0.01f;

        public static float RunSpeed = 170f;
        public static float GroundAccel = 1900f, AirAccel = 1200f;

        /// <summary>
        /// Jump shape. Height stays constant while you change Floatiness: gravity is multiplied by
        /// it and jump speed by its square root. Lower = floatier, slower arcs.
        /// </summary>
        public static float JumpVelocity = 470f, Gravity = 1350f, Floatiness = 0.76f;
        public static float MaxFallSpeed = 560f;
        /// <summary>Extra gravity multiplier while falling (snappier landings).</summary>
        public static float FallGravityMult = 1.2f;
        public static float CoyoteTime = 0.1f, JumpBuffer = 0.13f;
        /// <summary>Releasing jump early multiplies upward speed by this (variable jump height).</summary>
        public static float JumpCutMult = 0.5f;
        public static float DoubleJumpMult = 0.9f;

        public static float WallSlideSpeed = 110f, WallJumpPush = 250f, WallJumpMult = 0.92f;
        public static float AirDashSpeed = 540f, AirDashTime = 0.16f;

        public static float SwimSpeed = 120f, SwimAccel = 650f;
        /// <summary>Water resistance (per second): slows swimming and sinking. Higher = thicker water.</summary>
        public static float WaterDrag = 1.75f;
        /// <summary>Speed kept when you plunge into water (0.5 = half).</summary>
        public static float WaterEntryDamp = 0.75f;
        /// <summary>How hard you leap out of the water at the surface (fraction of jump speed).</summary>
        public static float SurfaceLeapMult = 0.95f;

        public static float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCooldown = 0.95f;

        // Swing (shared by both heroes; blade length, speed and damage are per hero below)
        public static float ComboWindow = 0.55f;
        public static float SwingArcDegrees = 115f, FinisherArcDegrees = 170f;
        public static float FinisherDamageMult = 2f, FinisherReachMult = 1.25f;
        /// <summary>Extra knockback by Heavy Pommel level (0 = no upgrade), added to the hero's own.</summary>
        public static float[] KnockbackByLevel = { 0f, 220f, 340f, 440f };
        public static float FinisherExtraKnockback = 120f;
        public static float PogoBounceMult = 0.95f;

        // Throw
        public static float ThrowDamage = 16f, ThrowCooldown = 2f; // swordsman's thrown dagger
        public static float ThrowSpeed = 820f, ThrowRange = 560f, ThrowSpinRadPerSec = 44f;
        public static float RicochetRange = 280f, RicochetDamageMult = 0.85f;

        /// <summary>XP needed for the next level: Base + Linear*L + Quadratic*L^2.</summary>
        public static float XpBase = 12f, XpLinear = 8f, XpQuadratic = 1.6f;
        public static float XpMagnetRange = 85f;
    }

    // =============================================================================== HEROES
    /// <summary>The swordsman: medium sword, dodge roll, throwing daggers.</summary>
    public static class Swordsman
    {
        /// <summary>Sword reach (px), damage per strike, time between swings, and how long the
        /// blade takes to sweep its arc (longer = heavier, slower-looking swing).</summary>
        public static float Reach = 45f, Damage = 20f, SwingCooldown = 0.6f, SwingTime = 0.12f;
        /// <summary>Wind-up before the blade comes around (the first two animation frames).</summary>
        public static float SwingWindup = 0.12f;
        /// <summary>Forward burst (px/s) when swinging on the ground.</summary>
        public static float Lunge = 130f;
        /// <summary>Chest upgrades: Rending Edge bleed (share of each hit, dealt over BleedSeconds),
        /// Crescent Wave (damage share, range px, cooldown s), Executioner (bonus vs. enemies below
        /// the HP share), Fan of Knives (extra daggers' damage share).</summary>
        public static float BleedShare = 0.4f, BleedSeconds = 3f;
        public static float WaveDamage = 0.5f, WaveRange = 150f, WaveCooldown = 1.2f;
        public static float ExecuteBonus = 0.6f, ExecuteBelow = 0.35f;
        public static float FanDamage = 0.6f;
        /// <summary>Knockback (px/s) on every hit, before Heavy Pommel.</summary>
        public static float Knockback = 180f;
        public static float MoveMult = 1f, JumpMult = 1f;
    }

    /// <summary>The warden: shortsword, an aimable shield, and a barrier buff.</summary>
    public static class Warden
    {
        /// <summary>Shortsword: dagger length and damage, but the blade sweeps faster.</summary>
        public static float Reach = 30f, Damage = 10f, SwingCooldown = 0.36f, SwingTime = 0.06f, SwingWindup = 0.06f;
        public static float Lunge = 0f, Knockback = 60f;
        /// <summary>85% of the swordsman's run speed and jump height.</summary>
        public static float MoveMult = 0.85f, JumpMult = 0.85f;
        /// <summary>Sturdier: more starting HP and one more second of breath.</summary>
        public static float StartHp = 80f, ExtraBreath = 1f;

        /// <summary>Shield: damage it can block, its arc (degrees), regeneration per second, the
        /// pause after a block before it regenerates, and how long it stays at zero once broken.</summary>
        public static float ShieldHp = 40f, ShieldArcDegrees = 70f, ShieldRegen = 3f, ShieldRegenDelay = 1f, ShieldBreakTime = 6f;
        /// <summary>A block within this many seconds of raising the shield is "perfect".</summary>
        public static float PerfectWindow = 0.18f;
        /// <summary>Share of damage the shield takes on a perfect block (with the upgrade).</summary>
        public static float PerfectSoakMult = 0.3f;
        /// <summary>Move speed while the shield is raised.</summary>
        public static float ShieldMoveMult = 0.8f;

        /// <summary>Last Stand upgrade: invulnerability after surviving a killing blow (once per depth).</summary>
        public static float LastStandInvuln = 1.5f;
        /// <summary>Barrier buff: damage absorbed, duration and cooldown (seconds).</summary>
        public static float BarrierAmount = 5f, BarrierDuration = 5f, BarrierCooldown = 10f;
    }

    // =============================================================================== COMBAT
    public static class Combat
    {
        /// <summary>Touch damage from an enemy that isn't attacking, as a share of its attack
        /// damage. 0 = bumping into enemies is harmless; only their actual attacks hurt.</summary>
        public static float PassiveContactMult = 0f;
        /// <summary>Horizontal bounce (px/s) the attacker gets when it lands a melee hit, both
        /// you and enemies. Never vertical.</summary>
        public static float StrikeRecoil = 110f;
        /// <summary>Scales how far you are pushed back when struck (horizontal only).</summary>
        public static float HurtKnockbackMult = 0.8f;
        /// <summary>Free swing-cooldown resets per combo before any Flurry upgrades: hit something
        /// and you can swing again at once, this many times, then the full cooldown runs.</summary>
        public static int ComboResetsBase = 1;
    }

    // =============================================================================== HIT FEEL
    public static class Feel
    {
        /// <summary>Freeze-frame lengths (seconds). The game runs at HitStopTimeScale meanwhile.</summary>
        public static float HitStopNormal = 0.16f, HitStopKill = 0.22f, HitStopFinisher = 0.28f;
        public static float HitStopThrown = 0.12f, HitStopPlayerHurt = 0.18f;
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
        /// <summary>Scales how far enemies move: their speed, gravity and jump speed together
        /// (0.8 = 20% slower, same jump timing, 20% lower jumps). Aimed leaps compensate.</summary>
        public static float EnemyMoveScale = 0.8f;
    }

    // =============================================================================== SPAWNING
    public static class Spawning
    {
        /// <summary>Share of resident spawn points that hold enemies at the start, rising to 100% by FillMinutes.</summary>
        public static float ResidentFillStart = 0.18f, ResidentFillMinutes = 16f;
        /// <summary>Residents are created this far away at most, and only when off-camera.</summary>
        public static float ResidentMaxDistance = 720f;
        /// <summary>Base respawn delay for a used resident point (divided by 1 + pace).</summary>
        public static float ResidentRespawnMin = 120f, ResidentRespawnMax = 200f;

        /// <summary>Entrance waves: the first comes after FirstWave s. The gap between waves starts at
        /// IntervalStart and halves every RateDoublingMinutes (like the difficulty curve), never
        /// dropping below IntervalMin.</summary>
        public static float FirstWave = 25f, IntervalStart = 26f, IntervalMin = 5f, RateDoublingMinutes = 8f;
        /// <summary>Wave size: 1 + up to (pace x this) extra enemies, each from its own direction.</summary>
        public static float WaveGrowthPerPace = 1.5f;
        /// <summary>Newcomers appear in a band this many px wide just outside the screen edges.</summary>
        public static float EntranceBandPx = 200f;
        /// <summary>How far along the tunnels (in 16 px cells) to look for entry points.</summary>
        public static int EntranceMaxCells = 48;
        /// <summary>Dead-end ambush rooms (a pack springs out when you arrive). Off for now.</summary>
        public static bool AmbushRooms = false;
        /// <summary>Chance an above-water entrance is bats (otherwise ground walkers).</summary>
        public static float EntranceBatChance = 0.35f;

        /// <summary>Max enemies near the player = CapBase + CapPerPace * pace (entrances get +4 headroom).</summary>
        public static int CapBase = 6, CapPerPace = 10;
    }

    // =============================================================================== DROPS
    public static class Drops
    {
        public static float HeartChance = 0.02f, HeartChanceElite = 0.4f;
        public static float HeartHeal = 15f, ChestHeal = 12f;
        /// <summary>Chance each treasure dead end actually holds a chest (mini-bosses and the boss always drop one).</summary>
        public static float TreasureRoomChestChance = 0.8f;
        /// <summary>Chest odds by location: movement upgrades are this many times likelier in
        /// underwater chests, survival upgrades in chests above HighZoneFraction of the water line's
        /// height (0.5 = the upper half of the dry caves).</summary>
        public static float ZoneBias = 4f, HighZoneFraction = 0.5f;
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
        public static float Hp = 40, Contact = 10, WalkSpeed = 45, GlobDamage = 9, PuddleDamage = 3, LobCooldown = 3.2f;
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
        /// <summary>The blue variant's dart damage, as a share of Contact.</summary>
        public static float BlueDamageMult = 0.55f;
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
        /// <summary>Steepest walkable slope (degrees). Moss and grass grow on exactly these slopes.</summary>
        public static float WalkableSlopeDegrees = 56f;
        /// <summary>Ledge staircases through tall open spaces: the chance each next ledge up is
        /// placed, at the water line and at the roof (in between it blends). High at the bottom,
        /// sparse at the top, so the heights are harder (not impossible) to reach.</summary>
        public static float PlatformDensityBottom = 0.85f, PlatformDensityTop = 0.17f;
        /// <summary>Minimum sideways gap (cells) between ledges at similar heights, so you can
        /// jump between them without hitting your head.</summary>
        public static float PlatformGapCells = 2.5f;
        /// <summary>Roughly how far apart (in cells) staircases start.</summary>
        public static int PlatformSpacingCells = 9;
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

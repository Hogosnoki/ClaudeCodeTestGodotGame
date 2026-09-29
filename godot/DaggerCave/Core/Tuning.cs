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

        public static float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCooldown = 0.55f;

        /// <summary>A press of attack, ability or dodge is remembered this long (and through a
        /// hit-stop), so it fires the moment it's allowed: combos pressed during an impact flow on.</summary>
        public static float PressBuffer = 0.22f;
        /// <summary>Murky water (the root-choked tunnels): breath runs out this much faster, swimming is this much slower.</summary>
        public static float MurkyBreathDrain = 1.5f, MurkySwimMult = 0.8f;

        // Swing (shared by the two sword heroes; blade length, speed and damage are per hero below)
        public static float ComboWindow = 0.55f;
        public static float SwingArcDegrees = 115f, FinisherArcDegrees = 170f;
        public static float FinisherDamageMult = 2f, FinisherReachMult = 1.25f;
        /// <summary>Extra knockback by Heavy Pommel level (0 = no upgrade), added to the hero's own.</summary>
        public static float[] KnockbackByLevel = { 0f, 220f, 340f, 440f };
        public static float FinisherExtraKnockback = 120f;
        public static float PogoBounceMult = 0.95f;

        /// <summary>XP needed for the next level: Base + Linear*L + Quadratic*L^2.</summary>
        public static float XpBase = 12f, XpLinear = 8f, XpQuadratic = 1.6f;
        public static float XpMagnetRange = 85f;
    }

    // =============================================================================== HEROES
    /// <summary>The swordsman: medium sword, a quick dodge roll, and the charged strike.</summary>
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
        /// the HP share).</summary>
        public static float BleedShare = 0.4f, BleedSeconds = 3f;
        public static float WaveDamage = 0.5f, WaveRange = 150f, WaveCooldown = 1.2f;
        public static float ExecuteBonus = 0.6f, ExecuteBelow = 0.35f;
        /// <summary>Knockback (px/s) on every hit, before Heavy Pommel.</summary>
        public static float Knockback = 180f;
        /// <summary>Faster and springier than the Warden.</summary>
        public static float MoveMult = 1f, JumpMult = 1f;
        public static float StartHp = 60f;

        /// <summary>
        /// Charged Strike (the ability button): the next swing hits ChargeDamage x as hard with
        /// ChargeReach x the reach, and whatever it strikes deals WeakenMult x damage for
        /// WeakenSeconds. Recharges in ChargeCooldown s; using it never breaks a combo.
        /// </summary>
        public static float ChargeCooldown = 12f, ChargeDamage = 1.5f, ChargeReach = 1.25f, WeakenMult = 0.8f, WeakenSeconds = 5f;

        /// <summary>
        /// Heaving Swing (the second ability), on your feet only: planted for HeaveWindup s as the
        /// sword goes up, one great arc (HeaveArcDegrees, HeaveReach x the reach) for HeaveDamage x
        /// a normal swing, then planted HeaveRecover s more. A waiting Charged Strike is spent on
        /// it (multiplying it again). Knocks back at least HeaveKnockback px/s. Every HeaveCooldown s.
        /// </summary>
        public static float HeaveWindup = 0.42f, HeaveRecover = 0.3f, HeaveDamage = 2f, HeaveReach = 1.3f, HeaveArcDegrees = 190f;
        public static float HeaveKnockback = 420f, HeaveCooldown = 6f;
    }

    /// <summary>The warden: shortsword, an aimable shield, the Guarded Charge and the shield bash.</summary>
    public static class Warden
    {
        /// <summary>Shortsword: dagger length, but the blade sweeps faster. (Its damage keeps the
        /// Warden's damage a little under the Swordsman's: about 31 a second to 33.)</summary>
        public static float Reach = 30f, Damage = 11f, SwingCooldown = 0.36f, SwingTime = 0.06f, SwingWindup = 0.06f;
        public static float Lunge = 0f, Knockback = 60f;
        /// <summary>A little slower than the Swordsman, with lower jumps.</summary>
        public static float MoveMult = 0.92f, JumpMult = 0.9f;
        /// <summary>The sturdiest: much more health, some armour, and one more second of breath.</summary>
        public static float StartHp = 110f, Armor = 0.1f, ExtraBreath = 1f;

        /// <summary>Shield: its strength, arc (degrees), regeneration per second, the pause after
        /// a block before it regenerates, and how long it stays at zero once broken.</summary>
        public static float ShieldHp = 40f, ShieldArcDegrees = 135f, ShieldRegen = 3f, ShieldRegenDelay = 1f, ShieldBreakTime = 6f;
        /// <summary>
        /// Share of a blow the raised shield stops (all of it), and how much of the stopped damage
        /// the shield itself loses (half: its 40 points soak 80). Once it runs out, the rest of
        /// the blow gets through.
        /// </summary>
        public static float BlockShare = 1f, ShieldCost = 0.5f;
        /// <summary>The creature whose blow breaks the shield is stunned this long (half for
        /// mini-bosses and guardians; the great bosses shrug it off).</summary>
        public static float BreakStun = 1.6f;
        /// <summary>A block within this many seconds of raising the shield is "perfect": nothing
        /// gets through and a melee attacker's attack is broken off (it reels for PerfectStagger s).</summary>
        public static float PerfectWindow = 0.18f, PerfectStagger = 0.6f;
        /// <summary>Share of damage the shield takes on a perfect block (with the upgrade).</summary>
        public static float PerfectSoakMult = 0.3f;
        /// <summary>Move speed while the shield is raised.</summary>
        public static float ShieldMoveMult = 0.8f;
        /// <summary>Healing the Warden receives also mends the shield by this share (and a broken
        /// shield is usable again at once).</summary>
        public static float HealToShield = 0.5f;

        /// <summary>Last Stand upgrade: invulnerability after surviving a killing blow (once per depth).</summary>
        public static float LastStandInvuln = 1.5f;

        /// <summary>
        /// Guarded Charge (the ability button): a charge behind the shield (speed px/s, seconds).
        /// Projectiles and shockwaves in its way are swallowed as it keeps going; it stops at the
        /// first attacking creature it meets, breaking the attack off (the creature reels for
        /// DashStagger s, pushed DashPush px/s, and takes DashDamage). Creatures that aren't
        /// attacking are passed by.
        /// </summary>
        public static float DashSpeed = 520f, DashTime = 0.24f, DashCooldown = 3.2f, DashStagger = 0.8f, DashPush = 260f, DashDamage = 6f;

        /// <summary>
        /// Shield Bash (the second ability): a BashTime s shove behind the shield (BashLunge px/s,
        /// fading). When it meets something, every creature within BashRadius px in front (a
        /// half-circle) takes BashDamage, is stunned for BashStun s (half that for mini-bosses and
        /// guardians; the great bosses shrug it off) and whatever it was doing is broken off. The
        /// shield takes BashShieldCost, once. Every BashCooldown s.
        /// </summary>
        public static float BashDamage = 20f, BashShieldCost = 20f, BashStun = 1.6f, BashCooldown = 10f;
        public static float BashTime = 0.2f, BashRadius = 40f, BashLunge = 240f, BashPush = 160f;
    }

    /// <summary>
    /// The vitalist (a vitality manipulator): no blade. Draining life at medium range feeds
    /// alimus, which pays for heals and ruptures; a hex slows creatures and makes them take more damage.
    /// </summary>
    public static class Vitalist
    {
        public static float StartHp = 55f, MoveMult = 0.97f, JumpMult = 0.97f;
        /// <summary>
        /// Drain (the attack button): the life is torn out of the creature you aim at as the staff
        /// comes forward, with no travel time. Damage, reach (px), time between casts, and the cone
        /// (degrees either side of your aim) it looks for a creature in. The stolen life flies back
        /// to you at MoteSpeed px/s and becomes alimus when it arrives.
        /// </summary>
        public static float DrainDamage = 10f, DrainRange = 175f, DrainCooldown = 0.5f, DrainConeDegrees = 38f, MoteSpeed = 640f;
        /// <summary>A cast with nothing to drain is spent, but recharges this much sooner.</summary>
        public static float DrainWhiffCooldown = 0.25f;
        /// <summary>Seconds from the press to the strike: the cast's thrust (its clip is 6 frames at 24 fps, sped up 1.6x; the thrust is 30-45% in).</summary>
        public static float DrainStrikeDelay = 0.06f;
        /// <summary>Many Mouths: each extra creature drained (within MultiRadius px of the target)
        /// takes this share of the damage.</summary>
        public static float MultiRadius = 120f, MultiShare = 0.6f;
        /// <summary>Alimus: gained as this share of damage you deal; the most you can hold; what
        /// you start a run with.</summary>
        public static float AlimusGain = 0.1f, AlimusMax = 30f, AlimusStart = 15f;
        /// <summary>
        /// Heal: costs HealCost alimus, and shares HealAmount of health among everyone in
        /// HealRange px who is hurt, by how hurt they are (the share of their health missing).
        /// </summary>
        public static float HealCost = 15f, HealAmount = 15f, HealRange = 420f, HealCooldown = 3f;
        /// <summary>Hex: creatures within HexRadius px move and act at HexSlow speed and take
        /// HexVulnerability x damage for HexSeconds; it recharges in HexCooldown s.</summary>
        public static float HexRadius = 110f, HexSlow = 0.55f, HexVulnerability = 1.2f, HexSeconds = 5f, HexCooldown = 6f;
        /// <summary>
        /// Rupture (the second ability): costs RuptureCost alimus. The creature you aim at (within
        /// RuptureRange px) is seized where it stands and, RuptureWindup s later, bursts for
        /// RuptureDamage; every other creature within RuptureRadius px of it takes RuptureSplash.
        /// </summary>
        public static float RuptureCost = 30f, RuptureDamage = 30f, RuptureSplash = 10f, RuptureRadius = 80f, RuptureRange = 200f;
        public static float RuptureWindup = 0.24f, RuptureCooldown = 1.5f;
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
        /// <summary>The first time a creature wants to attack, it waits this long first (so nothing
        /// strikes the instant it drops into view).</summary>
        public static float FirstAttackDelay = 1f;
        /// <summary>
        /// Attack slots: of the creatures within SlotRange px that are ready to attack, this share
        /// (rounded up, at least one) may be attacking at the same time. A creature holds its slot
        /// through its attack and SlotHold s after; SlotReserve s covers the gap between deciding
        /// to attack and starting. Bosses and guardians ignore the slots.
        /// </summary>
        public static float AttackerShare = 0.33f, SlotRange = 560f, SlotHold = 0.35f, SlotReserve = 0.3f;
        /// <summary>A creature busy this long with something that isn't an attack gives its slot up.</summary>
        public static float SlotMaxBusy = 1.5f;
    }

    // =============================================================================== HIT FEEL
    public static class Feel
    {
        /// <summary>
        /// Hit-stop lengths (seconds): the hero and whatever was struck freeze on the impact pose
        /// (with a shudder) while the rest of the world carries on. Presses made meanwhile are
        /// kept (Tune.Hero.PressBuffer), so the next strike of a combo follows the instant it ends.
        /// </summary>
        public static float HitStopNormal = 0.17f, HitStopKill = 0.23f, HitStopFinisher = 0.3f, HitStopCharged = 0.32f;
        public static float HitStopWave = 0.12f, HitStopBolt = 0.07f, HitStopDash = 0.14f, HitStopPlayerHurt = 0.18f;
        /// <summary>Game speed during SlowMo (the only thing that slows the whole game: elite and boss kills).</summary>
        public static float HitStopTimeScale = 0.02f;
        /// <summary>Slow motion on mini-boss / boss kills: (seconds, time scale).</summary>
        public static float EliteKillSlowMo = 0.45f, EliteKillSlowMoScale = 0.25f;
        public static float BossKillSlowMo = 1.3f, BossKillSlowMoScale = 0.15f;
        /// <summary>Camera nudge (pixels) in the direction of a blow.</summary>
        public static float KickNormal = 2.5f, KickKill = 4f, KickFinisher = 6f, KickPlayerHurt = 6f;
        public static float ShakeFinisher = 5f, ShakeKill = 3f;
        public static float CameraZoom = 2.1f, CameraFollowSharpness = 7f;
        /// <summary>The 3D camera: vertical field of view (degrees; its distance follows from
        /// CameraZoom so it frames the same stretch of the play plane), how far above the action
        /// it sits (metres; higher shows more of each floor) and how far above the action it aims.</summary>
        public static float Camera3DFov = 50f, Camera3DLift = 4.2f, Camera3DLookLift = 0.4f;
    }

    // =============================================================================== DIFFICULTY
    public static class Difficulty
    {
        /// <summary>Enemy health and damage grow by this factor per level of depth...</summary>
        public static float DepthGrowth = 1.15f;
        /// <summary>...and double every this many minutes of play (continuous curve). (The cave gets
        /// harder more through tougher creatures than through more of them: see the pace below.)</summary>
        public static float DoublingMinutes = 27f;
        /// <summary>Enemy speed/attack rate: this share of the threat's growth, capped (1.6 = at most 60% faster).</summary>
        public static float TempoShare = 0.2f, TempoCap = 1.6f;
        /// <summary>Spawn intensity ("pace"): PacePerDepth per level of depth plus 1 per PaceMinutesPerStep minutes, capped at PaceMax.</summary>
        public static float PacePerDepth = 0.15f, PaceMinutesPerStep = 20f, PaceMax = 2.4f;
        /// <summary>Scales how far enemies move: their speed, gravity and jump speed together
        /// (0.8 = 20% slower, same jump timing, 20% lower jumps). Aimed leaps compensate.</summary>
        public static float EnemyMoveScale = 0.8f;
    }

    // =============================================================================== SPAWNING
    public static class Spawning
    {
        /// <summary>Share of resident spawn points that hold enemies at the start (plus FillPerDepth per
        /// level of depth), rising to 100% by FillMinutes.</summary>
        public static float ResidentFillStart = 0.18f, ResidentFillPerDepth = 0.05f, ResidentFillMinutes = 22f;
        /// <summary>Residents are created this far away at most, and only when off-camera.</summary>
        public static float ResidentMaxDistance = 720f;
        /// <summary>Base respawn delay for a used resident point (divided by 1 + pace).</summary>
        public static float ResidentRespawnMin = 120f, ResidentRespawnMax = 200f;

        /// <summary>Entrance waves: the first comes after FirstWave s. The gap between waves starts at
        /// IntervalStart and halves every RateDoublingMinutes (like the difficulty curve), never
        /// dropping below IntervalMin.</summary>
        public static float FirstWave = 25f, IntervalStart = 26f, IntervalMin = 7f, RateDoublingMinutes = 12f;
        /// <summary>Wave size: 1 + up to (pace x this) extra enemies, each from its own direction.</summary>
        public static float WaveGrowthPerPace = 1.2f;
        /// <summary>Newcomers appear in a band this many px wide just outside the screen edges.</summary>
        public static float EntranceBandPx = 200f;
        /// <summary>How far along the tunnels (in 16 px cells) to look for entry points.</summary>
        public static int EntranceMaxCells = 48;
        /// <summary>Dead-end ambush rooms (a pack springs out when you arrive). Off for now.</summary>
        public static bool AmbushRooms = false;
        /// <summary>Chance an above-water entrance is bats (otherwise ground walkers).</summary>
        public static float EntranceBatChance = 0.35f;
        /// <summary>Chance a school of fish also has an urchin on the floor beneath it.</summary>
        public static float UrchinWithFishChance = 0.6f;

        /// <summary>Max enemies near the player = CapBase + CapPerPace * pace (entrances get +4 headroom).</summary>
        public static int CapBase = 6, CapPerPace = 8;
    }

    // =============================================================================== DROPS
    public static class Drops
    {
        /// <summary>Heart drops (heal HeartHealFrac of max health) and potion drops.</summary>
        public static float HeartChance = 0.03f, HeartChanceElite = 0.3f, HeartHealFrac = 0.05f, ChestHeal = 12f;
        /// <summary>Embers a guardian pays (from depth 5 on, and the dragon).</summary>
        public static int GuardianEmbers = 2, DeepGuardianEmbers = 3, DragonEmbers = 6;
        public static float PotionChance = 0.01f;
        /// <summary>Chance each treasure dead end actually holds a chest (mini-bosses and the boss always drop one).</summary>
        public static float TreasureRoomChestChance = 0.8f;
        /// <summary>Extra chests scattered on the flooded floor and high in the dry caves.</summary>
        public static int WaterCaches = 6, HighCaches = 4;
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
        public static float Hp = 20, Contact = 6, TongueDamage = 9, TongueRange = 90, TongueCooldown = 2.8f;
        public static float AggroRange = 360, HopSpeedX = 170, HopSpeedY = 365, HopCooldownMin = 1.0f, HopCooldownMax = 1.6f;
        public static int Xp = 4;
    }

    public static class Goblin
    {
        public static float Hp = 26, Contact = 4, ClubDamage = 12, RunSpeed = 125, SlingerSpeed = 95;
        public static float AggroRange = 460, WindupTime = 0.38f, RockDamage = 8, ThrowCooldown = 3.0f;
        /// <summary>Pause after a club swing before the goblin acts again.</summary>
        public static float RecoverTime = 0.8f;
        public static int Xp = 5;
    }

    public static class Spider
    {
        public static float Hp = 18, Contact = 9, CrawlSpeed = 70, DropSpeed = 330, GroundSpeed = 150, PounceCooldown = 1.9f;
        /// <summary>In water it rows after you at SwimSpeed px/s, and darts at DartSpeed.</summary>
        public static float SwimSpeed = 95, DartSpeed = 250;
        public static int Xp = 4;
    }

    public static class Magma
    {
        public static float Hp = 40, Contact = 10, WalkSpeed = 45, GlobDamage = 9, PuddleDamage = 3, LobCooldown = 3.8f;
        public static float WaterDamagePerSec = 15;
        public static int Xp = 8;
    }

    public static class Golem
    {
        public static float Hp = 90, Contact = 12, WalkSpeed = 40, SlamWindup = 0.8f, SlamCooldown = 3.8f;
        public static float ShockwaveDamage = 14, ShockwaveSpeed = 250, SlamDamage = 18;
        public static int Xp = 14;
    }

    public static class Fish
    {
        public static float Hp = 10, Contact = 7, DartSpeed = 210, AggroRange = 320;
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
        public static float Hp = 24, Contact = 10, LungeLength = 170, LungeSpeed = 460, Cooldown = 2.3f;
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

    // ---- biome creatures
    public static class Rat
    {
        public static float Hp = 9, Contact = 5, BiteDamage = 5, RunSpeed = 150, Windup = 0.25f, LungeSpeed = 300, Cooldown = 1.1f;
        public static int Xp = 2;
    }

    public static class Bear
    {
        public static float Hp = 70, Contact = 12, SwipeDamage = 16, WalkSpeed = 60, ChargeSpeed = 240, SwipeWindup = 0.45f, ChargeWindup = 0.55f, ChargeCooldown = 5f;
        /// <summary>In water it treads toward you at SwimSpeed px/s and, since it can't rear up to
        /// swipe, bites: BiteWindup s with the head drawn back, then a lunge for BiteDamage.</summary>
        public static float SwimSpeed = 55, BiteDamage = 13, BiteWindup = 0.35f, BiteReach = 40, BiteCooldown = 1.4f;
        public static int Xp = 12;
    }

    public static class Scorpion
    {
        public static float Hp = 26, Contact = 6, StingDamage = 11, WalkSpeed = 72, StingWindup = 0.45f, StingReach = 40, StingCooldown = 1.6f;
        public static int Xp = 5;
    }

    public static class Hornet
    {
        public static float Hp = 10, Contact = 8, FlySpeed = 135, AimTime = 0.38f, DiveSpeed = 330, DiveCooldown = 2.2f;
        public static int Xp = 3;
    }

    public static class Skeleton
    {
        public static float Hp = 30, Contact = 5, SlashDamage = 12, WalkSpeed = 62, Windup = 0.45f, Recover = 0.7f;
        /// <summary>Chance a felled skeleton pulls itself back together once (at ReassembleHp of its health).</summary>
        public static float ReassembleChance = 0.4f, ReassembleHp = 0.5f, ReassembleTime = 2.5f;
        public static int Xp = 6;
    }

    public static class Sporeling
    {
        public static float Hp = 16, Contact = 4, WalkSpeed = 46, PuffWindup = 0.5f, PuffCooldown = 3.5f, CloudDamage = 4, CloudTime = 2.6f, CloudRadius = 36;
        public static int Xp = 4;
    }

    /// <summary>Grasping roots: how long they must hold you before the weapon is snagged, for how long, and how many cuts free them.</summary>
    public static class Roots
    {
        public static float GripToSnag = 0.75f, SnagSeconds = 0.9f, Regrip = 2.2f;
        public static int CutsToClear = 2;
    }

    /// <summary>Cave-ins: warning rumble, rocks per collapse, their damage, and the rest between collapses.</summary>
    public static class CaveIn
    {
        public static float Rumble = 1.0f, Damage = 12f, RestMin = 6f, RestMax = 10f, Reach = 44f;
        public static int RocksMin = 2, RocksMax = 4;
    }

    /// <summary>
    /// Frost wraiths are a presence more than a gunner: they haunt the player from a distance,
    /// now and then loom close with a shriek (no harm in it, only dread), and only rarely loose a
    /// single ice shard after a long, obvious wind-up (three for an elite).
    /// </summary>
    public static class Wraith
    {
        public static float Hp = 11, Contact = 5, FlySpeed = 85, CastWindup = 1.0f, CastCooldown = 7.5f, FirstCast = 4f, ShardDamage = 6, ShardSpeed = 185;
        public static float LoomCooldown = 8f, LoomTime = 1.1f, LoomDistance = 58f;
        public static int Xp = 6;
    }

    public static class Shardling
    {
        public static float Hp = 14, Contact = 5, WalkSpeed = 66, CurlTime = 0.5f, BurstCooldown = 4f, BurstDamage = 10, ShardDamage = 6;
        public static int Xp = 4;
    }

    public static class Dragon
    {
        public static float Hp = 1500, Contact = 18, WalkSpeed = 70, FireDamage = 7, DiveDamage = 26, TailDamage = 20, ShockwaveDamage = 16, RainDamage = 12;
        public static float EnrageAt = 0.5f, EnragedSpeedMult = 1.3f;
        public static int Xp = 300;
    }

    // =============================================================================== CAVE
    public static class Cave
    {
        /// <summary>Map size in 16 px cells. Water fills the bottom half.</summary>
        public static int Width = 460, Height = 240;
        /// <summary>Every biome's map is this many times as wide as its own width says (the height
        /// stays): its tunnel budget and number of rooms grow with it, so the width gets filled.</summary>
        public static float WidthScale = 1.5f;
        /// <summary>Total tunnel-carving steps: more = more (and longer) tunnels.</summary>
        public static int TunnelBudget = 4600;
        /// <summary>Radius range (cells) of dry tunnels. Narrower leaves more ground and ledges.</summary>
        public static float AirRadiusMin = 2.6f, AirRadiusMax = 3.7f;
        /// <summary>How strongly dry tunnels straighten out toward horizontal each step (0 = not at
        /// all), and how steep new branches may start (fraction of the max slope).</summary>
        public static float HorizontalBias = 0.012f, BranchPitchMult = 0.8f;
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
    public static bool Rat = false;
    public static bool Bear = false;
    public static bool Scorpion = false;
    public static bool Hornet = false;
    public static bool Skeleton = false;
    public static bool Sporeling = false;
    public static bool Wraith = false;
    public static bool Shardling = false;
    public static bool Dragon = false;
}

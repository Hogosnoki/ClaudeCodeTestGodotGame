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
        /// <summary>Every heal a hero receives, from anything, lands this many times as strong.</summary>
        public static float HealingMult = 0.85f;
        /// <summary>Invulnerability after being struck (the Resilience upgrade adds to this).</summary>
        public static float HurtInvuln = 0.4f;
        public static float BreathSeconds = 15f;
        /// <summary>Air bubbles rising from vents on the sea floor: seconds of breath each, how
        /// many vents per cave, and seconds between bubbles from one vent.</summary>
        public static float AirBubbleBreath = 4f, AirVentIntervalMin = 4f, AirVentIntervalMax = 9f;
        public static int AirVents = 28;
        /// <summary>Drowning damage per tick (every 0.5 s) = flat + fraction of max HP.</summary>
        public static float DrownDamageFlat = 3f, DrownDamageFrac = 0.01f;

        /// <summary>Run speed (px/s): 20% under the original 170. The run animation is slowed the same (see Player.RunAnimRef).</summary>
        public static float RunSpeed = 136f;
        public static float GroundAccel = 1900f, AirAccel = 1200f;
        /// <summary>Crouched, you walk at this share of your speed.</summary>
        public static float CrouchSpeed = 0.4f;
        /// <summary>A blow from more than this many px above your middle misses you while you crouch.</summary>
        public static float DuckHeight = 6f;

        /// <summary>
        /// Jump shape (JumpVelocity 336 and Gravity 1080, 0.8 x the 420 and 1350 before: a fifth less height, falling a fifth slower, the
        /// rise as long as ever). Height stays constant while you change Floatiness: gravity is multiplied by it and jump speed by its square
        /// root. Lower = floatier, slower arcs.
        /// </summary>
        public static float JumpVelocity = 336f, Gravity = 1080f, Floatiness = 0.76f;
        /// <summary>Loosing a projectile (or pouring out a spell) slows the hero to CastSlowMult of their speed while the cast plays
        /// (CastSlowSeconds for a single shot; a volley or a breath, as long as it lasts).</summary>
        public static float CastSlowMult = 0.45f, CastSlowSeconds = 0.3f;
        public static float MaxFallSpeed = 420f;
        /// <summary>The movement stick's round dead zone (a share of its throw): see Player.ReadMove.</summary>
        public static float StickDeadzone = 0.2f;
        /// <summary>Fall damage counts the time spent falling at the terminal speed (at least FallTerminalShare of MaxFallSpeed): past
        /// FallGraceSeconds of it a landing takes FallShareMin of the hero's current health, rising to FallShareMax after FallRampSeconds
        /// more. Anything that slows the fall below that speed (a second jump, an updraft, a dash, a rope, the water) resets the count.
        /// (An ordinary jump lands just short of the terminal speed; about 0.35 s of falling reaches it, then it covers MaxFallSpeed px/s.)</summary>
        public static float FallTerminalShare = 0.97f, FallGraceSeconds = 0.3f, FallRampSeconds = 1.0f, FallShareMin = 0.01f, FallShareMax = 0.5f;
        /// <summary>Extra gravity multiplier while falling (snappier landings).</summary>
        public static float FallGravityMult = 1.2f;
        public static float CoyoteTime = 0.15f, JumpBuffer = 0.19f;
        /// <summary>Releasing jump early multiplies upward speed by this (variable jump height).</summary>
        public static float JumpCutMult = 0.5f;
        public static float DoubleJumpMult = 0.9f;

        public static float WallSlideSpeed = 110f, WallJumpPush = 250f, WallJumpMult = 0.92f;
        public static float AirDashSpeed = 540f, AirDashTime = 0.16f;

        public static float SwimSpeed = 96f, SwimAccel = 650f;
        /// <summary>Water resistance (per second): slows swimming and sinking. Higher = thicker water.</summary>
        public static float WaterDrag = 1.75f;
        /// <summary>Speed kept when you plunge into water (0.5 = half).</summary>
        public static float WaterEntryDamp = 0.75f;
        /// <summary>How hard you leap out of the water at the surface (fraction of jump speed).</summary>
        public static float SurfaceLeapMult = 0.95f;
        /// <summary>Swimming up, you come to rest with your middle this far under the surface (px): the head is out, to breathe.</summary>
        public static float SurfaceRest = 5f;

        public static float DodgeSpeed = 450f, DodgeTime = 0.2f, DodgeCooldown = 0.55f;

        /// <summary>A press of attack, ability or dodge is remembered this long (and through a
        /// hit-stop), so it fires the moment it's allowed: combos pressed during an impact flow on.</summary>
        public static float PressBuffer = 0.22f;
        /// <summary>Murky water (the root-choked tunnels): breath runs out this much faster, swimming is this much slower.</summary>
        public static float MurkyBreathDrain = 1.5f, MurkySwimMult = 0.8f;
        /// <summary>Magma Skin: offered from this depth on; lava's burn with it (a share).</summary>
        public static int MagmaSkinFromDepth = 6;
        public static float MagmaSkinBurn = 0.3f;

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
    /// <summary>The swordsman: medium sword, a dash-slash, and the charged strike.</summary>
    public static class Swordsman
    {
        /// <summary>Sword reach (px), damage per strike, time between swings (from one swing's start to the
        /// next's: shortening the swing itself leaves it alone), and how long the blade takes to sweep
        /// its arc (longer = heavier, slower-looking swing).</summary>
        public static float Reach = 68f, Damage = 18f, SwingCooldown = 0.6f, SwingTime = 0.0825f;
        /// <summary>Wind-up before the blade comes around.</summary>
        public static float SwingWindup = 0.09f;
        /// <summary>A charged (empowered) strike heals the Swordsman for this share of the damage it deals.</summary>
        public static float ChargeLifesteal = 0.03f;
        /// <summary>Forward burst (px/s) when swinging on the ground.</summary>
        public static float Lunge = 130f;
        /// <summary>Chest upgrades: Rending Edge bleed (share of each hit, dealt over BleedSeconds),
        /// Crescent Wave (damage share, range px, cooldown s), Executioner (bonus vs. enemies below
        /// the HP share).</summary>
        public static float BleedShare = 0.3f, BleedSeconds = 3f;
        public static float WaveDamage = 0.5f, WaveRange = 150f, WaveCooldown = 1.2f;
        public static float ExecuteBonus = 0.6f, ExecuteBelow = 0.25f;
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

        /// <summary>Relentless Charge (an alteration): a charge empowers the whole next combo, at
        /// this share of its strength (its damage and reach bonus, and its weakening).</summary>
        public static float RelentlessShare = 0.8f;
        /// <summary>Counter Roll (an alteration): the swing back at the attacker, and how close a
        /// blow's source must be to count as a melee blow the roll can counter.</summary>
        public static float CounterReach = 70f;
        /// <summary>The dash-slash (the dodge button): a dash of DashSlashSpeed px/s for DashSlashTime s, in an orange ghost-glow, that stops at the first creature it meets for a slash of DashSlashMult times a normal strike (with everything a strike
        /// brings); with the Dash Through alteration it goes through the creature and slashes it from behind; back DashSlashCooldown s after it began (the dodge roll it replaces came
        /// back in half a second). It dodges nothing of itself: it is a way across the floor, forward or away.</summary>
        public static float DashSlashSpeed = 560f, DashSlashTime = 0.28f, DashSlashCooldown = 15f, DashSlashMult = 1.5f;
    }

    /// <summary>The warden: shortsword, an aimable shield, the Guarded Charge and the shield bash.</summary>
    /// <summary>
    /// The Aegis, the support: a ward bolt that hurts, weakens what it strikes (so they hit softer) and mends its caster; a
    /// barrier, a burden shared, and a bubble for an ally (or, with no ally, for itself). Amounts are shares of the
    /// receiver's maximum health, scaled by WardMult, so they keep up with the depths.
    /// </summary>
    public static class Aegis
    {
        /// <summary>The wind-up before the Aegis's shields (a barrier, a bubble): she holds perfectly still, animated, for these seconds.</summary>
        public static float ShieldDelay = 0.25f;
        public static float StartHp = 70f, MoveMult = 0.98f, JumpMult = 1f;
        /// <summary>Enemies see the Aegis this far off (x the distance): it takes blows for others, so it stays out of their eyes.</summary>
        public static float ThreatDist = 1.25f;
        /// <summary>Ward Bolt (the attack button): BoltDamage every BoltEvery s, flying BoltSpeed px/s up to BoltRange px, homing as bolts do; on contact a burst of BurstRadius px: the creature struck takes the whole blow, the others in it BurstShare of it; all are weakened (DebuffMult x the damage they deal, for DebuffSeconds); Lifesteal (a quarter of a percent; Mending Light and Solar Crest add more) of the damage dealt mends the Aegis.</summary>
        public static float BoltDamage = 10.8f, BoltEvery = 0.5f, BoltSpeed = 460f, BoltRange = 250f, BurstRadius = 34f, BurstShare = 0.6f;
        public static float DebuffMult = 0.9f, DebuffSeconds = 5f;
        /// <summary>Barrier (the ability button): soaks BarrierShare of the receiver's max health (x WardMult) for BarrierSeconds; Recharge BarrierCooldown s. Reaches allies within AllyRange px.</summary>
        public static float BarrierShare = 0.14f, BarrierSeconds = 15f, BarrierCooldown = 10f, AllyRange = 260f;
        /// <summary>Shared Burden (the second ability): the ally takes (1 - share) of every blow, the Aegis the rest, for BurdenSeconds. No cooldown beyond a blink.</summary>
        public static float BurdenShare = 0.2f, BurdenSeconds = 60f, BurdenBlink = 0.4f;
        /// <summary>Foe's Burden (an alteration of the second ability): laid on a creature within FoeReach px, it takes FoeShare of every blow the Aegis takes for FoeSeconds; the ability is back after FoeCooldown s.</summary>
        public static float FoeShare = 0.25f, FoeSeconds = 12f, FoeCooldown = 6f, FoeReach = 260f;
        /// <summary>Humbling Light (the second ability, when she isn't carrying anyone: alone, or without the Bulwark Oath): a flash SmiteRadius px round her; every creature it reaches deals HumbleMult as much for HumbleSeconds. SmiteCooldown s.</summary>
        public static float SmiteRadius = 120f, SmiteCooldown = 12f, HumbleMult = 0.7f, HumbleSeconds = 3f;
        /// <summary>Burden of Many: the burdened friend shares ManyShare of every blow among every hero within AllyRange of them.</summary>
        public static float ManyShare = 0.2f;
        /// <summary>Bubble (the dodge button): absorbs BubbleAbsorb of each blow until BubbleShare of the receiver's max health (x WardMult) is absorbed, when it bursts; lasts BubbleSeconds, recharges BubbleCooldown s; the receiver breathes under water meanwhile. With Soothing Burst, the burst heals allies within BurstHealRadius px for BurstHealShare of their max health.</summary>
        public static float BubbleRadiusMin = 12f, BubbleRadiusMax = 34f, BubbleCastRadius = 220f, BubbleShare = 0.1f, BubbleAbsorb = 0.1f, BubbleSeconds = 30f, BubbleCooldown = 20f, BurstHealShare = 0.045f, BurstHealRadius = 80f;
        /// <summary>Hostile Bubble (an alteration): on a creature instead, it takes (1 - WardAbsorb) of every blow while the rest builds; once WardCap has built it bursts for WardBlast, and WardSplash to the creatures round it (all x the Aegis's damage and the depth's threat).</summary>
        public static float WardAbsorb = 0.5f, WardCap = 10f, WardBlast = 30f, WardSplash = 10f, WardRadius = 64f, WardSeconds = 20f;
    }

    public static class Warden
    {
        /// <summary>Enemies see the Warden as this much closer than he is (x the distance), so he draws the fight.</summary>
        public static float ThreatDist = 0.85f;
        /// <summary>Shortsword: dagger length, but the blade sweeps faster. (Its damage keeps the
        /// Warden's damage a little under the Swordsman's: about 31 a second to 33.)</summary>
        public static float Reach = 50f, Damage = 11f, SwingCooldown = 0.36f, SwingTime = 0.045f, SwingWindup = 0.045f;
        public static float Lunge = 0f, Knockback = 60f;
        /// <summary>A little slower than the Swordsman, with lower jumps.</summary>
        public static float MoveMult = 0.92f, JumpMult = 0.9f;
        /// <summary>The sturdiest: much more health, some armour, and one more second of breath.</summary>
        public static float StartHp = 110f, Armor = 0.1f, ExtraBreath = 1f;

        /// <summary>Shield: its strength, arc (degrees), regeneration per second, the pause after
        /// a block before it regenerates, and how long it stays at zero once broken.</summary>
        public static float ShieldArcDegrees = 135f, ShieldRegenDelay = 0.8f, ShieldBreakTime = 5f;
        /// <summary>The shield's strength and regeneration scale with the Warden's health: its strength is ShieldHpShare of his
        /// maximum health (the shield soaks twice that), it regenerates ShieldRegenShare of it a second, and a bash costs
        /// BashShieldShare of it.</summary>
        public static float ShieldHpShare = 0.33f, ShieldRegenShare = 0.0135f, BashShieldShare = 0.5f;
        /// <summary>
        /// Share of a blow the raised shield stops (all of it), and how much of the stopped damage
        /// the shield itself loses (half: its 40 points soak 80). Once it runs out, the rest of
        /// the blow gets through.
        /// </summary>
        public static float BlockShare = 1f, ShieldCost = 0.5f;
        /// <summary>The creature whose blow breaks the shield is stunned this long (half for
        /// mini-bosses and guardians; the great bosses shrug it off).</summary>
        public static float BreakStun = 1.6f;
        /// <summary>Unyielding Shield (an alteration): the share of a blow it stops (it never
        /// weakens or breaks). Braced adds 5% a rank.</summary>
        public static float UnyieldingShare = 0.7f;
        /// <summary>Guardian's Charge (an alteration): the barrier it gives a friend (damage it
        /// soaks, seconds it lasts), and how far away a friend can be to be charged to (px).</summary>
        public static float BarrierAmount = 20f, BarrierSeconds = 2f, GuardianRange = 320f;
        /// <summary>Deflecting Bash (an alteration): how far in front (px) and how wide (degrees
        /// either side) it sends projectiles back.</summary>
        public static float DeflectRange = 130f, DeflectHalfArc = 70f;
        /// <summary>A block within this many seconds of raising the shield is "perfect": nothing
        /// gets through and a melee attacker's attack is broken off (it reels for PerfectStagger s).</summary>
        public static float PerfectWindow = 0.18f, PerfectStagger = 0.6f;
        /// <summary>Share of damage the shield takes on a perfect block (with the upgrade).</summary>
        public static float PerfectSoakMult = 0.3f;
        /// <summary>Move speed while the shield is raised.</summary>
        public static float ShieldMoveMult = 0.8f;
        /// <summary>Healing the Warden receives also mends the shield by this share (and a broken
        /// shield is usable again at once).</summary>
        public static float HealToShield = 0.25f;

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
        public static float BashDamage = 20f, BashStun = 1.6f, BashCooldown = 10f;
        public static float BashTime = 0.2f, BashRadius = 40f, BashLunge = 240f, BashPush = 160f;
    }

    /// <summary>
    /// The vitalist (a vitality manipulator): no blade. Draining life at medium range feeds
    /// vital force, which pays for heals and ruptures; a hex slows creatures and makes them take more damage.
    /// </summary>
    public static class Vitalist
    {
        /// <summary>The wind-up before a heal (it is cast on the one it was begun on: the hero holds perfectly still, animated, for these seconds; Rupture's is RuptureWindup).</summary>
        public static float HealDelay = 0.4f;
        public static float StartHp = 55f, MoveMult = 0.97f, JumpMult = 0.97f;
        /// <summary>
        /// Drain (the attack button): the life is torn out of the creature you aim at as the staff
        /// comes forward, with no travel time. Damage, reach (px), time between casts, and the cone
        /// (degrees either side of your aim) it looks for a creature in. The stolen life flies back
        /// to you at MoteSpeed px/s and becomes vital force when it arrives.
        /// </summary>
        public static float DrainDamage = 8f, DrainRange = 175f, DrainCooldown = 0.625f, DrainConeDegrees = 38f, MoteSpeed = 640f;
        /// <summary>A cast with nothing to drain is spent, but recharges this much sooner.</summary>
        public static float DrainWhiffCooldown = 0.25f;
        /// <summary>Seconds from the press to the strike: the cast's thrust (its clip is 6 frames at 24 fps, sped up 1.6x; the thrust is 30-45% in).</summary>
        public static float DrainStrikeDelay = 0.06f;
        /// <summary>Many Mouths: each extra creature drained (within MultiRadius px of the target)
        /// takes this share of the damage.</summary>
        public static float MultiRadius = 120f, MultiShare = 0.6f;
        /// <summary>Vital force: gained as this share of damage you deal; the most you can hold; what
        /// you start a run with.</summary>
        public static float VitalForceGain = 0.1f, VitalForceMax = 30f, VitalForceStart = 15f;
        /// <summary>
        /// Heal: costs HealCost vital force, and shares HealAmount of health among everyone in
        /// HealRange px who is hurt, by how hurt they are (the share of their health missing).
        /// </summary>
        public static float HealCost = 15f, HealAmount = 15f, HealRange = 420f, HealCooldown = 3f;
        /// <summary>Hex: creatures within HexRadius px move and act at HexSlow speed and take
        /// HexVulnerability x damage for HexSeconds; it recharges in HexCooldown s.</summary>
        public static float HexRadius = 110f, HexSlow = 0.55f, HexVulnerability = 1.2f, HexSeconds = 5f, HexCooldown = 6f;
        /// <summary>
        /// Rupture (the second ability): costs RuptureCost vital force. The creature you aim at (within
        /// RuptureRange px) is seized where it stands and, RuptureWindup s later, bursts for
        /// RuptureDamage; every other creature within RuptureRadius px of it takes RuptureSplash.
        /// </summary>
        public static float RuptureCost = 30f, RuptureDamage = 30f, RuptureSplash = 10f, RuptureRadius = 80f, RuptureRange = 200f;
        public static float RuptureWindup = 0.8f, RuptureCooldown = 1.5f;

        /// <summary>Blight Burst (an alteration): the hex's damage, and how much weaker its slow and
        /// its extra damage taken are. Endless Hex: its vital force cost (no cooldown).</summary>
        public static float BlightDamage = 8f, BlightWeaker = 0.5f, EndlessHexCost = 10f;
        /// <summary>Slow Mending (an alteration): seconds the mending part of a heal takes; Patient
        /// Mending's extra healing; Warding Mending's damage reduction while it mends you.</summary>
        public static float MendSeconds = 6f, PatientNow = 0.6f, PatientLater = 0.6f, PatientSeconds = 4f, WardingShare = 0.2f;
        /// <summary>Lifebloom (an alteration): the heal for the friend it blooms on and for everyone
        /// else in the burst, and the Healing Pool's heal a second and seconds.</summary>
        public static float BloomHeal = 30f, BloomSplash = 10f, PoolRate = 2f, PoolSeconds = 5f;
    }

    // =============================================================================== ROGUE
    public static class Rogue
    {
        /// <summary>The wind-up before a recall: the Rogue holds perfectly still, animated, for these seconds before the daggers tear home.</summary>
        public static float RecallDelay = 0.4f;
        public static float StartHp = 55f, MoveMult = 1.1f, JumpMult = 1f;
        /// <summary>Ricochet's marks: a creature a ricocheting dagger strikes is marked for MarkSeconds; a recall then flings a poison
        /// pin at every marked creature within PinRange px (PinDamage, and PinPoison more over PinSeconds).</summary>
        public static float MarkSeconds = 10f, PinRange = 320f, PinDamage = 6f, PinPoison = 14f, PinSeconds = 4f;
        /// <summary>
        /// Dagger Slash (the attack button): quick jabs, Damage each (the baseline: a thrown dagger strikes for 0.6 x that, a recall for 0.6 x), SwingCooldown s apart (five a
        /// second), Reach px, striking one creature at a time with a tiny hit-stop; no combo refunds.
        /// A strike has CritChance of landing twice as hard (CritMult). With one dagger thrown the
        /// jabs come half as fast (OneDaggerSlow).
        /// </summary>
        public static float Damage = 9f, SwingCooldown = 0.2f, SwingWindup = 0.03f, SwingTime = 0.05f, Reach = 40f, Knockback = 70f, Lunge = 0f;
        public static float CritChance = 0.05f, CritMult = 2f, HitStop = 0.02f, OneDaggerSlow = 2f;
        /// <summary>Backstab (an upgrade): a strike from behind a creature lands this many times as hard.</summary>
        public static float BackstabMult = 1.5f;
        /// <summary>
        /// Dagger Throw (the ability button): your two daggers are its uses. A thrown dagger flies at
        /// ThrowSpeed px/s up to ThrowRange px and sticks in the first creature it meets for
        /// ThrowDamage, nudging it (ThrowNudge); a miss flies back by itself, at ReturnSpeed px/s.
        /// Daggers stuck in creatures stay there until recalled (the recall button, or the attack button with none in hand); each tears back out for RecallDamage.
        /// One that meets a wall (a surface steeper than FootholdMaxSlope of the floor's, as the open side's normal.Y) goes into it and stays the same way, its
        /// hilt a one-way ledge FootholdLength px long that anyone can stand on.
        /// </summary>
        public static float FootholdLength = 14f, FootholdMaxSlope = 0.6f;
        public static float ThrowDamage = Damage * 0.6f, ThrowSpeed = 620f, ThrowRange = 280f, ThrowNudge = 90f, ReturnSpeed = 760f, ThrowConeDegrees = 12f;
        /// <summary>The wait before the next throw: ThrowCooldownOne s after a throw leaves one dagger out, ThrowCooldownBoth s after one that leaves both out (a recall doesn't shorten it).</summary>
        public static float ThreatDist = 1.15f; // the rogue seems farther off to enemies: they pick others first
        public static float ThrowCooldownOne = 0.5f, ThrowCooldownBoth = 1f;
        /// <summary>Recall (the second ability, no cooldown): each dagger stuck in a creature tears
        /// back out through it for RecallDamage, yanking it toward you (RecallYank).</summary>
        public static float RecallDamage = Damage * 0.6f, RecallYank = 1040f;
        /// <summary>Serrated Recall (an upgrade): a recall's blow makes the creature bleed, RecallBleedChance of the time, for RecallBleedMult x the blow over RecallBleedSeconds.</summary>
        public static float RecallBleedChance = 0.6f, RecallBleedMult = 2f, RecallBleedSeconds = 5f;
        /// <summary>Vanish (the dodge button): VanishSeconds of stealth (creatures lose you) at
        /// VanishSpeed times your speed; VanishCooldown s to come back. Attacking or being hurt ends
        /// it. Surprise Attack (an upgrade): the strike out of the shadows lands SurpriseMult as hard.</summary>
        public static float VanishSeconds = 6f, VanishSpeed = 1.5f, VanishCooldown = 12f, SurpriseMult = 3f;
        /// <summary>Alterations: Ricochet (a thrown dagger springs on to one more creature within
        /// RicochetRange px, then comes back), Smoke Bomb (a cloud SmokeRadius px round for
        /// SmokeSeconds: every hero in it is hidden, and creatures in it can't find anyone), Tether
        /// (Recall pulls you to a stuck dagger at TetherSpeed px/s instead).</summary>
        public static float RicochetRange = 130f, SmokeRadius = 70f, SmokeSeconds = 6f, TetherSpeed = 720f;
        /// <summary>Keen Edge (an upgrade): the crit chance each rank adds.</summary>
        public static float KeenChance = 0.08f;
    }

    // =============================================================================== ELEMENTALIST
    /// <summary>Class perks (bought with embers): how many of a hero's perks it can bring on a run.</summary>
    public static class Perks
    {
        public static int Slots = 3;
    }

    public static class Elementalist
    {
        /// <summary>The wind-ups before a spell (the hero holds perfectly still, animated, for these seconds, gravity too): a firebolt, a frostbolt, a Blizzard.</summary>
        public static float FireDelay = 0.2f, FrostDelay = 0.1f, BlizzardDelay = 0.5f;
        public static float StartHp = 55f, MoveMult = 0.97f, JumpMult = 0.97f;
        /// <summary>Alimus: what spells cost. It comes back by itself (AlimusRegen a second, its only
        /// source) up to AlimusMax; a run starts full. A bolt costs FireCost or FrostCost, a little
        /// more than the regeneration gives back at the usual rate of fire: only barely outpacing it.</summary>
        public static float AlimusMax = 40f, AlimusRegen = 1.8f;
        /// <summary>
        /// Firebolt (the attack button): a bolt of fire flying at BoltSpeed px/s up to BoltRange px,
        /// FireDamage a bolt, one coming back every FireEvery s (see FireCharges); each has IgniteChance of setting a creature alight, burning
        /// IgniteDps for IgniteSeconds. The cone (degrees either side of your aim) it looks for a
        /// creature to fly at as it leaves the staff. In flight a bolt homes: BoltTurnDegrees a
        /// second toward the creature nearest its heading, one within BoltSeekDegrees either side of it.
        /// </summary>
        public static float FireDamage = 28f, FireEvery = 1.5f, FireCost = 2.9f, FrostCost = 0.6f, BoltRange = 260f, BoltSpeed = 520f, BoltConeDegrees = 22f;
        /// <summary>The staff holds FireCharges bolts (FrostCharges with Frostbolt); a spent one comes back
        /// every FireEvery (FrostEvery) seconds, one at a time; BoltGap (FrostGap with Frostbolt) seconds must pass between throws.</summary>
        public static int FireCharges = 2, FrostCharges = 4;
        public static float BoltGap = 0.25f, FrostGap = 0.2f;
        public static float BoltTurnDegrees = 600f, BoltSeekDegrees = 60f;
        public static float IgniteChance = 0.65f, IgniteDps = 4f, IgniteSeconds = 4f;
        /// <summary>A firebolt bursts where it lands: the creatures within FireBurstRadius px of it take FireBurstShare of the blow.</summary>
        public static float FireBurstRadius = 26f, FireBurstShare = 0.35f;
        /// <summary>Frostbolt (an alteration): FrostDamage a bolt, one coming back every FrostEvery s; a creature it strikes
        /// is chilled (ChillSlow slower for ChillSeconds), and a regular creature (not a mini-boss,
        /// guardian or boss) has FreezeChance of freezing solid for FreezeSeconds.</summary>
        public static float FrostDamage = 10f, FrostEvery = 1f, ChillSlow = 0.3f, ChillSeconds = 2f, FreezeChance = 0.2f, FreezeSeconds = 3f;
        /// <summary>Updraft, the narrow draft (the dodge button): hold it to aim, let go to raise it. Costs UpdraftCost alimus; a narrow
        /// column of air UpdraftWidth px wide and UpdraftHeight px tall (18 m) for UpdraftSeconds, at the angle aimed (any direction at all).
        /// Every hero in it is launched along it (the way the wind blows) at the speed of their own full jump: as they come into it, and
        /// again whenever they land back in it (at most once every UpdraftRelaunch s). (UpdraftGravityMult, UpdraftFallMult and the
        /// downdraft's two were the old float, kept for its tests' history: no longer read.)</summary>
        public static float UpdraftCost = 15f, UpdraftWidth = 55f, UpdraftHeight = 296f, UpdraftSeconds = 4f, UpdraftRelaunch = 0.3f;
        public static float UpdraftGravityMult = 0.4f, UpdraftFallMult = 0.2f, DowndraftGravityMult = 2.2f, DowndraftFallMult = 1.7f;
        /// <summary>A leaning column also carries a hero along its lean: this speed (px/s) times how far it leans sideways.
        /// Its relief from gravity scales with how upright it is; lying flat it only blows.</summary>
        public static float UpdraftPushSpeed = 240f;
        /// <summary>
        /// Blizzard (the ability button): costs BlizzardCost alimus, BlizzardCooldown s to come
        /// back. A storm BlizzardRadius px round (about 176 px, 11 m across) at the aim point, no further
        /// than BlizzardRange px away: BlizzardTicks strikes of BlizzardDamage over BlizzardSeconds,
        /// each with BlizzardFreeze chance of freezing a regular creature. Firestorm (an alteration):
        /// FirestormDamage a strike, each with FirestormIgnite chance of setting it alight.
        /// </summary>
        public static float BlizzardCost = 20f, BlizzardCooldown = 20f, BlizzardRadius = 88f, BlizzardRange = 200f, BlizzardSeconds = 6f;
        public static int BlizzardTicks = 18;
        public static float BlizzardDamage = 2f, BlizzardFreeze = 0.06f, FirestormDamage = 3f, FirestormIgnite = 0.25f;
        /// <summary>
        /// Snap (the second ability): costs SnapCost alimus. Every frozen creature in view
        /// shatters for SnapDamage, and every other creature within SnapRadius px of it takes
        /// SnapSplash. Cinder Snap (an alteration): burning creatures burst instead, for
        /// CinderDamage and CinderSplash. SnapViewX / SnapViewY px each way is "in view" (about
        /// half the screen, either side of you).
        /// </summary>
        public static float SnapCost = 6f, SnapDamage = 15f, SnapSplash = 4f, SnapRadius = 50f, SnapCooldown = 0.6f;
        public static float CinderDamage = 20f, CinderSplash = 6f;
        /// <summary>The updraft's recharge once it has been paid for (the alimus is what holds it back).</summary>
        public static float UpdraftRecharge = 0.5f;
        /// <summary>A Snap hits this many times as hard (both the burst and the splash; the shatters of a heaving swing or a shield bash keep SnapDamage as it is).</summary>
        public static float SnapMult = 2f;
        /// <summary>The beat before a Snap bursts: the Elementalist and every creature it marks are held still for this long.</summary>
        public static float SnapFreeze = 0.35f;
        public static float SnapViewX = 500f, SnapViewY = 290f;
        /// <summary>Upgrades: Kindling's ignite chance and Deep Chill's freeze chance, per rank;
        /// Echo's alimus back for each creature a snap bursts.</summary>
        public static float KindlingChance = 0.1f, DeepChillChance = 0.04f, EchoAlimus = 3f;
        /// <summary>Cinder Snap: the snap leaves burning creatures alight, and comes back this many seconds later.</summary>
        public static float CinderSnapExtra = 2f;
    }

    // =============================================================================== COMBAT
    public static class Combat
    {
        /// <summary>Touch damage from an enemy that isn't attacking, as a share of its attack
        /// damage. 0 = bumping into enemies is harmless; only their actual attacks hurt.</summary>
        public static float PassiveContactMult = 0f;
        /// <summary>An armoured creature (golem, scorpion, shardling, the colossi) takes 40% less from physical blows
        /// until its armour has turned aside this share of its health: then its armour falls off (and blows sound as on flesh).</summary>
        public static float ArmorBreakShare = 0.1f;
        /// <summary>Horizontal bounce (px/s) the attacker gets when it lands a melee hit, both
        /// you and enemies. Never vertical.</summary>
        public static float StrikeRecoil = 110f;
        /// <summary>The same bounce for the hero when its own blow lands (a small share of the enemies').</summary>
        public static float HeroStrikeRecoil = 25f;
        /// <summary>Scales how far you are pushed back when struck (horizontal only).</summary>
        public static float HurtKnockbackMult = 0.12f;
        /// <summary>Seconds after being struck that steering is dulled (the shove itself is slight).</summary>
        public static float HurtKnockLock = 0.04f;
        /// <summary>Free swing-cooldown resets per combo before any Flurry upgrades: hit something
        /// and you can swing again at once, this many times, then the full cooldown runs.</summary>
        public static int ComboResetsBase = 1;
        /// <summary>The first time a creature wants to attack, it waits this long first (so nothing
        /// strikes the instant it drops into view).</summary>
        public static float FirstAttackDelay = 0.5f;
        /// <summary>
        /// Attack slots: of the creatures within SlotRange px that are ready to attack, this share
        /// (rounded up, at least one) may be attacking at the same time. A creature holds its slot
        /// through its attack and SlotHold s after; SlotReserve s covers the gap between deciding
        /// to attack and starting. Bosses and guardians ignore the slots.
        /// </summary>
        public static float AttackerShare = 0.33f, SlotRange = 560f, SlotHold = 0.18f, SlotReserve = 0.15f;
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
        public static float HitStopNormal = 0.15f, HitStopFinisher = 0.26f, HitStopCharged = 0.28f;
        /// <summary>A blow that kills holds this many times as long (the hero, and the dying creature's last pose).</summary>
        public static float HitStopKillMult = 1.25f;
        /// <summary>A melee blow that lands holds the hero perfectly still this long once its hit-stop is over (no delay when a swing begins).</summary>
        public static float MeleeHitPause = 0.1f;
        public static float HitStopWave = 0.1f, HitStopBolt = 0.06f, HitStopDash = 0.12f, HitStopPlayerHurt = 0.04f;
        /// <summary>Game speed during SlowMo (the only thing that slows the whole game: elite and boss kills).</summary>
        public static float HitStopTimeScale = 0.02f;
        /// <summary>Slow motion on mini-boss / boss kills: (seconds, time scale).</summary>
        public static float EliteKillSlowMo = 0.45f, EliteKillSlowMoScale = 0.25f;
        public static float BossKillSlowMo = 1.3f, BossKillSlowMoScale = 0.15f;
        /// <summary>Camera nudge (pixels) in the direction of a blow.</summary>
        public static float KickNormal = 2.5f, KickKill = 4f, KickFinisher = 6f, KickPlayerHurt = 6f;
        public static float ShakeFinisher = 5f, ShakeKill = 3f;
        public static float CameraZoom = 2.502f, CameraFollowSharpness = 7f;
        /// <summary>The 3D camera: vertical field of view (degrees; its distance follows from
        /// CameraZoom so it frames the same stretch of the play plane), how far above the action
        /// it sits (metres; higher shows more of each floor) and how far above the action it aims.</summary>
        public static float Camera3DFov = 50f, Camera3DLift = 4.2f, Camera3DLookLift = 0.4f;
    }

    // =============================================================================== DIFFICULTY
    public static class Difficulty
    {
        /// <summary>Enemy health and damage grow by this factor per level of depth...</summary>
        public static float DepthGrowth = 1.09f;
        /// <summary>Enemy health grows by only this share of that per level of depth (1.036 a level, against damage's 1.09).</summary>
        public static float HpDepthShare = 0.4f;
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
        /// <summary>How much of the difficulty strength enemy damage takes (health takes all of it).</summary>
        public static float DamageShare = 0.2f;
        /// <summary>Hard Mode: enemy health and damage multipliers on top of everything else.</summary>
        public static float HardHp = 2f, HardDamage = 1.5f;
    }

    /// <summary>Relics and the chests that hold them.</summary>
    /// <summary>Gold hidden in the dead ends: a dead end holds CoinsMin to CoinsMax single coins, or, PileChance of the time, one pile worth PileMin to PileMax.</summary>
    public static class Gold
    {
        public static int CoinsMin = 1, CoinsMax = 3, PileMin = 4, PileMax = 8;
        public static float PileChance = 0.2f;
        /// <summary>
        /// Gold too where it takes some getting to (see Main.PlaceMoreGold), never on the main floor: high platforms with nothing under them, ledges that lead nowhere,
        /// alcoves and raised crevices: about <c>SpotsPerMap</c> places on a map of the usual size, each kept <c>Spacing</c> px from the next and
        /// <c>RewardGap</c> px from any chest, cage, key or other gold.
        /// </summary>
        public static float SpotsPerMap = 10f, Spacing = 150f, RewardGap = 110f;
        /// <summary>The hardest-to-reach share of those places holds a pile instead of coins; a dead end's gold is a pile only this often.</summary>
        public static float PileShare = 0.25f, DeadEndPileChance = 0.08f;
        /// <summary>Creatures drop gold now and then: a coin (or, <c>DropPileChance</c> of the time, a small pile) <c>DropChance</c> of the time, an elite <c>EliteDropChance</c>, a guardian always (a pile of <c>GuardianPile</c>).</summary>
        public static float DropChance = 0.07f, EliteDropChance = 0.4f, DropPileChance = 0.12f;
        public static int GuardianPile = 12;
    }

    /// <summary>The wind-ups (cast delays): the hero keeps this share of its speed (walking, swimming, falling) while one gathers.</summary>
    public static class Hold
    {
        public static float MoveShare = 0.1f;
    }

    public static class Relics
    {
        /// <summary>A Hunter's Map: this share more chests in every cave.</summary>
        public static float ChestBonus = 0.2f;
        /// <summary>A Hasty Descent: the dragon is this much tougher.</summary>
        public static float ShortcutDragon = 0.1f;
        /// <summary>Share of ordinary chests that hold a relic (a silver chest), and of chests hung in a web.</summary>
        public static float RelicChestChance = 0.2f, WebChestChance = 0.12f;
        /// <summary>A relic chest that is a Relic Collection instead (a relic for each of the party, one each); a guardian's party chest always is.</summary>
        public static float CollectionChance = 0.15f;
        /// <summary>Hawk's Pursuit: a melee lunge is at least LungeMin px/s (for the heroes who never lunged), lasts LungeTimeMult times as long, and in the air a swing
        /// chases the creature nearest its aim within PursuitRange px, then hangs the hero in place for HoverTail s.</summary>
        /// <summary>Retaliation relics: a boss or mini-boss that strikes you burns for StruckIgniteDps for StruckIgniteSeconds (20% of blows); a regular creature that does is frozen solid for StruckFreezeSeconds (15%).</summary>
        public static float StruckIgniteChance = 0.2f, StruckIgniteDps = 4f, StruckIgniteSeconds = 4f, StruckFreezeChance = 0.15f, StruckFreezeSeconds = 1.5f;
        /// <summary>Co-op relics: Last Rites heals each living ally by FallHeal when you fall; Phoenix Ash and Healer's Hands add ReviveExtra (a share of max health) to a revive; a plain revive restores ReviveBase.</summary>
        public static float FallHeal = 20f, ReviveExtra = 0.2f, ReviveBase = 0.35f;
        /// <summary>Fast Res and Just Kidding: a revive goes this much faster (each).</summary>
        public static float ReviveFaster = 0.4f;
        /// <summary>Fount of Vigor and Spring Water: a level gained, or a depth advanced, heals this share of max health.</summary>
        public static float LevelHealShare = 0.2f, DepthHealShare = 0.2f;
        /// <summary>Echoing Blade: a melee blow that lands strikes again EchoDelay s later (EchoChance of the time) for EchoMult of its damage.</summary>
        public static float EchoChance = 0.2f, EchoMult = 0.5f, EchoDelay = 0.1f;
        /// <summary>Soul Lantern, Chain Lightning, Spillover, Wither, Rally, Level Surge, Bounty Hunter, Overflow (see RelicEffects).</summary>
        public static float SoulShare = 0.1f, SoulRadius = 60f;
        public static float ChainChance = 0.15f, ChainShare = 0.4f, ChainRadius = 90f; public static int ChainJumps = 2;
        public static float SpillShare = 0.3f, SpillRadius = 80f, WitherRadius = 80f;
        public static float RallyRadius = 260f, RallyShare = 0.03f, RallySeconds = 8f;
        public static float SurgeBase = 40f, SurgePerLevel = 6f, SurgeRadius = 150f, SurgeHeal = 0.1f;
        public static float BountyChance = 0.15f;
        public static float OverflowShare = 0.08f, OverflowSeconds = 10f, OverflowEvery = 3f;
        public static float LungeMin = 100f, LungeTimeMult = 1.6f, PursuitRange = 170f, HoverTail = 0.25f;
        /// <summary>Far Sight and Close Quarters: a creature this far (px) is "far"; nearer ones are in between.</summary>
        public static float FarDistance = 220f;
        /// <summary>A web chest hangs this high (px) over where it lands, under a ceiling at least WebMinThread px higher still.</summary>
        public static float WebHangHeight = 52f, WebMinThread = 90f;
        /// <summary>Blood Channeling: health paid for each alimus a spell lacks.</summary>
        public static float BloodPerAlimus = 2f;
    }

    /// <summary>Afflictions the heroes can suffer: poison (deep frogs and spiders, the Nature Elemental), burning (Fire), freezing (Frost), drowning (Water).</summary>
    public static class Status
    {
        /// <summary>Webbed (a guardian spider's silk): presses it takes to struggle free; a hero also tears free after taking WebHold of
        /// their max health in blows, or when a friend's blade cuts the web.</summary>
        public static float WebStruggle = 9f, WebHoldShare = 0.18f;
        /// <summary>After a stun, this long before another can take hold.</summary>
        public static float StunImmune = 3f;
        /// <summary>From this depth, a frog's or spider's blow poisons with CreaturePoisonChance: the damage of the blow again (x share) over CreaturePoisonSeconds.</summary>
        public static int PoisonFromDepth = 4;
        public static float CreaturePoisonChance = 0.3f, CreaturePoisonShare = 1f, CreaturePoisonSeconds = 8f;
        /// <summary>Elementals: the chance one of their blows inflicts its element's affliction.</summary>
        public static float ElementalChance = 0.35f;
        /// <summary>Burn: BurnShare x the blow over BurnSeconds (water puts it out). Nature's poison: PoisonShare x the blow over PoisonSeconds. Frost: frozen solid for FreezeSeconds (then FreezeImmune s of grace). Water: no air bubbles for DrownSeconds, and DrownBreath s less breath.</summary>
        public static float BurnShare = 1.2f, BurnSeconds = 5f, PoisonShare = 1f, PoisonSeconds = 15f, FreezeSeconds = 2f, FreezeImmune = 3f, DrownSeconds = 10f, DrownBreath = 4f;
    }

    // =============================================================================== SPAWNING
    public static class Spawning
    {
        /// <summary>Share of resident spawn points that hold enemies at the start (plus FillPerDepth per
        /// level of depth), rising to 100% by FillMinutes.</summary>
        /// <summary>How much of the old crowd there is: residents, the cap and the entrance waves (the caves are for exploring).</summary>
        public static float SpawnShare = 0.3f, WaveGapMult = 2.5f;
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
        /// <summary>Newcomers appear at least OffscreenMargin px beyond the edge of the view (any hero's) and MinSpawnDistance px from the hero.</summary>
        public static float OffscreenMargin = 140f, MinSpawnDistance = 340f;
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
        public static float HeartChance = 0.015f, HeartChanceElite = 0.15f, HeartHealFrac = 0.05f, ChestHeal = 12f;
        /// <summary>Embers a guardian pays (from depth 5 on, and the dragon).</summary>
        public static int GuardianEmbers = 2, DeepGuardianEmbers = 3, DragonEmbers = 6;
        public static float PotionChance = 0.01f;
        /// <summary>Chance each treasure dead end actually holds a chest (mini-bosses and the boss always drop one).</summary>
        public static float TreasureRoomChestChance = 0.8f;
        /// <summary>Extra chests scattered on the flooded floor and high in the dry caves.</summary>
        public static int WaterCaches = 6, HighCaches = 4;
        /// <summary>Open cells the Magma Caverns' lava lake fills, at least (while no room floor is drowned).</summary>
        public static int LavaLakeCells = 260;
        /// <summary>Chest odds by location: movement upgrades are this many times likelier in
        /// underwater chests, survival upgrades in chests above HighZoneFraction of the water line's
        /// height (0.5 = the upper half of the dry caves).</summary>
        public static float ZoneBias = 4f, HighZoneFraction = 0.5f;
    }

    // =============================================================================== KEYS AND VAULTS
    public static class Vault
    {
        /// <summary>
        /// One vault a level (not the dragon's lair): a dead end cut into the rock behind an iron
        /// gate, a passage CorridorCells long and CorridorRows tall opening into a chamber
        /// ChamberCells wide and ChamberRows tall, at least MinFromStart cells from where you
        /// start. Its chest holds risk-rewards and a rare class card.
        /// </summary>
        public static int CorridorCells = 5, CorridorRows = 3, ChamberCells = 9, ChamberRows = 5;
        public static float MinFromStart = 40f;
        /// <summary>Keys a hero can carry. Any key opens any gate. Going down, a hero keeps only as many as the cells they left locked.</summary>
        public static int MaxKeys = 3;
        /// <summary>A level holds as many keys as it has cells (vaults): one dropped by the first mini-boss slain there (hidden instead
        /// where no mini-boss lairs), the rest hidden.</summary>
        /// <summary>A hidden key lies at least this far (px) from the start, the vault and the chests.</summary>
        public static float HiddenKeyFromStart = 700f, HiddenKeySpacing = 260f;
    }

    // =============================================================================== ENEMIES
    // Hp and Damage are the values at minute 0 (the difficulty curve multiplies them).
    // Speeds are in px/s at minute 0 (the tempo curve multiplies them).

    /// <summary>Heroes found in the caves.</summary>
    public static class Heroes
    {
        /// <summary>The chance a level holds a caged hero (while any are still to be found): about one a run over
        /// the nine or so levels below the cave's mouth (which has none), a little likelier in the sunken sea.</summary>
        public static float CageChance = 0.12f, AbyssCageChance = 0.3f;
    }

    /// <summary>Every creature's health at spawn is its own Hp times this (before depth and party scaling).</summary>
    public static class Enemy
    {
        public static float BaseHpMult = 3f;
        /// <summary>Experience per kill (there are far fewer creatures about now).</summary>
        public static float XpMult = 3f;
        /// <summary>Bosses and guardians grow with depth and time by this power of the usual curve (deep ones took too long to fight).</summary>
        public static float BossGrowthPower = 0.7f;
    }

    public static class Elite
    {
        public static float SizeMult = 1.7f, HpMult = 6f, DamageMult = 1.4f, XpMult = 7f, MinKnockResist = 0.6f;
    }

    /// <summary>Switches for testing.</summary>
    public static class Testing
    {
        /// <summary>Every hero (and every side-grade) can be chosen, whatever the save says.</summary>
        public static bool UnlockEverything = true;
        /// <summary>Dual charges are open whatever the save says (see <see cref="Meta.DualCharges"/>), while they're being tested.</summary>
        public static bool DualCharges = true;
        /// <summary>No cooldowns on the elemental heroes while the elements are being tried out: their abilities and versions, the support
        /// ability, the mode's switch and the overcharge's recharge all come back at once (the swing's and the bolt's own pace stay).
        /// The elements scenario turns it off, to test the real rules.</summary>
        public static bool NoCooldowns = true;
    }

    /// <summary>The pre-run loadout: how many side-grades a hero can bring.</summary>
    public static class Loadout
    {
        public static int Slots = 3;
    }

    /// <summary>The shrines: loose shapes that sink slowly to the floor from this height (px) at this speed (px/s).</summary>
    public static class Shrine
    {
        public static float StartLift = 56f, SinkSpeed = 14f;
    }

    /// <summary>Support abilities (the support button), one per hero, each for the others' sake.</summary>
    public static class Support
    {
        /// <summary>Stalag-Might's wind-up: the Elementalist holds perfectly still, animated, for this long before the earth answers.</summary>
        public static float StalagDelay = 1.0f;
        /// <summary>Swordsman Battle Shout: allies within range deal this much more damage for some seconds.</summary>
        public static float ShoutDamage = 1.10f, ShoutSeconds = 10f, ShoutRange = 280f, ShoutCooldown = 30f;
        /// <summary>Shape Shifter Pack Howl: allies near move and strike faster.</summary>
        public static float HowlMove = 1.2f, HowlAttack = 1.15f, HowlSeconds = 8f, HowlRange = 280f, HowlCooldown = 25f;
        /// <summary>Warden Taunt: every creature counts her as next to nothing away for this long.</summary>
        public static float TauntSeconds = 3f, TauntCooldown = 20f;
        /// <summary>Vitalist Health Tap: this share of max health spent for this share of the vital force reserve.</summary>
        public static float TapCost = 0.15f, TapGain = 0.6f, TapCooldown = 15f;
        /// <summary>Elementalist Stalag-Might: damage, seconds rooted, reach, cooldown.</summary>
        public static float StalagDamage = 30f, StalagSeconds = 2f, StalagRange = 260f, StalagCooldown = 15f, StalagGroundReach = 22f;
        /// <summary>Rogue Expose: the marked creature takes this much more from everyone.</summary>
        public static float ExposeCrit = 0.10f, ExposeVuln = 1.2f, ExposeSeconds = 8f, ExposeRange = 260f, ExposeCooldown = 20f;
        /// <summary>Aegis Share Pain: the ally aimed at gives PainShare of every blow they take to the Aegis instead, until she picks
        /// another (one at a time) or picks them again; PainCooldown s between picks.</summary>
        public static float PainShare = 0.15f, PainCooldown = 4f;
        /// <summary>Automaton Pressurize: the button is only held back this long between presses (its real wait starts when the pressure goes; see Tune.AutomatonHero).</summary>
        public static float PressurePress = 0.4f;
    }

    /// <summary>
    /// The Automaton, a copper-and-brass engine of a man: slow, heavy, short in the jump and hard to knock about, with a
    /// lot of health and some armour. It doesn't breathe (no drowning, no poisoned air), and water, steam and acid do it
    /// no harm. In water it sinks like the lump of metal it is, and swims by kicking up with each press of jump. It can't
    /// truly be healed: healing it receives becomes fuel (FuelFromHeal of the amount), and Self-Repair spends that
    /// fuel to mend it, one for one.
    /// </summary>
    public static class AutomatonHero
    {
        public static float StartHp = 300f, Armor = 0.15f, MoveMult = 0.72f, JumpMult = 0.72f;
        /// <summary>How much of a blow's shove it takes (x).</summary>
        public static float KnockTaken = 0.45f;
        /// <summary>Its steam hop: a little air jump (x a full jump's speed), a puff from the joints.</summary>
        public static float HopMult = 0.6f;
        /// <summary>In water: it sinks toward SinkSpeed px/s when left alone (SinkDown with down held); each press of jump kicks it
        /// up at StrokeSpeed px/s (no more than every StrokeEvery s; 144, a fifth more than at first, so the climb is no struggle); it walks the bottom at WadeMult x the swim speed.</summary>
        public static float SinkSpeed = 190f, SinkDown = 270f, SinkAccel = 520f, StrokeSpeed = 144f, StrokeEvery = 0.28f, WadeMult = 0.8f;

        /// <summary>The cleaver: reach, damage, time between swings, the sweep and wind-up, knockback and the step into it.
        /// Its third strike is a slam: SlamShare of the strike to everything within SlamRadius px of where it lands, and the
        /// sparks set any gas there alight.</summary>
        public static float Reach = 64f, Damage = 17.5f, SwingCooldown = 0.85f, SwingTime = 0.11f, SwingWindup = 0.15f, Knockback = 240f, Lunge = 60f;
        public static float SlamRadius = 56f, SlamShare = 0.55f;

        /// <summary>Fuel: the most it holds and what it starts with; the share of any healing it becomes.</summary>
        public static float FuelMax = 80f, FuelStart = 40f, FuelFromHeal = 0.3f;
        /// <summary>Every way of gaining fuel gives this much more (healing, Scrap Reclaimer, relics, the Rally's resource).</summary>
        public static float FuelGainMult = 2f;
        /// <summary>Self-Repair (the ability button): mends RepairRate health a second, one fuel for one health, for up to
        /// RepairSeconds (or until the fuel or the damage runs out; a press stops it), moving at RepairMove x.
        /// Back after RepairCooldown s. Scrap Reclaimer: a kill gives ScrapFuel.</summary>
        public static float RepairRate = 10f, RepairSeconds = 4f, RepairMove = 0.45f, RepairCooldown = 6f, ScrapFuel = 10f;

        /// <summary>Steam Release (the second ability): a cone of steam SteamRange px long and SteamCone degrees wide: SteamDamage
        /// to every creature in it and a SteamPush shove, gas blown away and its own burning put out. The release kicks the
        /// Automaton the other way at SteamRecoil px/s (aimed down, it's a lift). Every SteamCooldown s.</summary>
        public static float SteamRange = 96f, SteamCone = 70f, SteamDamage = 9f, SteamPush = 520f, SteamRecoil = 300f, SteamCooldown = 6f;
        /// <summary>Scalding Steam: what it strikes burns (dps, seconds) and isn't shoved.</summary>
        public static float ScaldDps = 5f, ScaldSeconds = 3f;

        /// <summary>Brace (the dodge button): planted for BraceTime s, blows from in front lose BraceSoak of their damage and
        /// don't shove it; the first melee blow is answered by a piston punch (PunchDamage, PunchKnock). Every BraceCooldown s.</summary>
        public static float BraceTime = 0.75f, BraceSoak = 0.7f, BraceCooldown = 2.6f, PunchDamage = 22f, PunchKnock = 520f;

        /// <summary>Pressurize (the support button): PressureSeconds building up (glowing and shaking harder and harder, slowed to
        /// PressureMove x and unable to strike; it needs PressureMinFuel to begin), then, until it is pressed again or the fuel is gone,
        /// burning PressureFuel a second: it strikes PressureAttack x as fast and PressureDamage x as hard (back up to the cleaver's old
        /// weight), moves PressureSpeed x as fast and jumps PressureJump x as high. Back PressureCooldown s after the pressure goes.</summary>
        public static float PressureSeconds = 3f, PressureMove = 0.5f, PressureMinFuel = 5f, PressureFuel = 4f, PressureAttack = 1.35f, PressureDamage = 1.8f, PressureSpeed = 1.45f, PressureJump = 1.55f, PressureCooldown = 8f;
    }

    /// <summary>The Shape Shifter: a weak staff fighter whose strength is the creatures it copies.</summary>
    public static class Shifter
    {
        public static float StartHp = 70f, MoveMult = 1.0f, JumpMult = 1.0f;
        /// <summary>The staff: damage, reach (px: about the length of the staff ahead of the fist, 0.85 of a unit at 58 px a unit, as the sword's 68 is its blade's 1.16), seconds between swings.</summary>
        public static float StaffDamage = 5f, StaffReach = 52f, StaffCooldown = 0.55f;
        /// <summary>A form's damage (x the form's own multiplier), how far a Shift can reach, how fast a charge runs.</summary>
        public static float FormDamage = 15f, CopyRange = 110f, ChargeSpeed = 360f;
        /// <summary>Seconds Shift takes to come back once a form is dropped.</summary>
        public static float ShiftCooldown = 3f;
        /// <summary>How far away a hero in a form seems to the cave's creatures (x the real distance), and to creatures of its own kind: when they are looking (alone), and when they are choosing between several heroes (they then come last).</summary>
        public static float NoticeFactor = 2f, KinNoticeFactor = 5f, KinChoosingFactor = 1000f;
        /// <summary>Transformation: the buff a Shape Shifter has while in a form, this much more damage on every blow of the form's
        /// (its creature's own blows, and its specials), so a hero-driven creature outfights a wild one of its kind.</summary>
        public static float TransformDamage = 0.6f;
        /// <summary>Rat's Gnaw: this many bites, each this far apart (s), straight ahead.</summary>
        public static int GnawBites = 6;
        public static float GnawInterval = 0.1f;
        /// <summary>Bat's Latch: it flies at a creature (for at most LatchSeekSeconds at LatchSpeed px/s), hangs on LatchSeconds biting
        /// (the form's SpecDmg in all, in bites LatchTick apart), lets go, and mends LatchHealShare of the hero's health over LatchHealSeconds.</summary>
        public static float LatchSeekSeconds = 0.7f, LatchSpeed = 420f, LatchSeconds = 1.2f, LatchTick = 0.2f, LatchHealShare = 0.15f, LatchHealSeconds = 5f;
        /// <summary>With no creature to go for, the bat's Latch goes for a harmless cave critter (a moth, a crab) within the Latch's range instead and eats it: it mends BatEatHeal health at once, and the Latch is ready again after BatEatCooldown s.</summary>
        public static float BatEatHeal = 2f, BatEatCooldown = 1.5f;
        /// <summary>Scorpion's Venom Spray: a cone (half-angle, radians) of venom; each creature in it is poisoned for SprayVenom x the
        /// form damage over SprayVenomSeconds (topping up with each spray).</summary>
        public static float SprayHalfAngle = 0.5f, SprayVenom = 2.4f, SprayVenomSeconds = 5f;
        /// <summary>Spider's Envenomate: it pounces at one creature (at most EnvenomSeekSeconds at EnvenomSpeed px/s) and bites, leaving
        /// a powerful venom: EnvenomTotal x the form damage over EnvenomSeconds.</summary>
        public static float EnvenomSeekSeconds = 0.6f, EnvenomSpeed = 380f, EnvenomTotal = 6f, EnvenomSeconds = 6f;
        /// <summary>A Shape Shifter fish out of water loses this share of its health a second (a real fish dies of it in about eight).</summary>
        public static float FishDrownPerSec = 0.1f;
        /// <summary>The dodge button in a form commits it to memory (RememberCooldown s before it can again); the second button, or Shift with nothing to copy, takes the remembered form again.</summary>
        public static float RememberCooldown = 1.5f;
    }

    /// <summary>Blows from creatures are area blows: heroes close together split them.</summary>
    public static class Share
    {
        public static bool On = true;
        /// <summary>How close (px) a friend must stand to the one struck to take a share.</summary>
        public static float Radius = 110f;
    }

    /// <summary>The rock ledges the generator lays in: how many blows to bring one down.</summary>
    public static class Ledge
    {
        public static int Hits = 10;
        /// <summary>A broken slab grows back this many seconds later (once nobody is where it would be). A frozen ledge freezes back as soon.</summary>
        public static float RegrowSeconds = 30f;
    }

    /// <summary>The fossil graveyards' ribcages: the distance (m) between one pair of ribs and the next.</summary>
    public static class Fossils
    {
        public static float RibSpacing = 1.7f;
    }

    /// <summary>
    /// Secret wings (see <see cref="SecretWing"/>): the camera keeps to the main occupied area plus ViewPadCells of rock round it; about
    /// WingChance of levels (depth 1 on) have a hidden corridor and chamber past the right-hand edge behind a plug of rubble, cut from where
    /// the cave reaches its right-hand extreme (no further than MaxReachCells from the map's edge); the map is made PadCells wider for it.
    /// </summary>
    public static class Secrets
    {
        public static float WingChance = 0.5f;
        /// <summary>Blows a secret wing's wall takes (the plugs strewn about take Rubble.Hits); a heaving swing still brings it down in one.</summary>
        public static int WallHits = 8;
        /// <summary>How far back from the cave's end (cells) the floor a passage starts from may lie, if the ground is level under all the way out.</summary>
        public static int ApproachCells = 8;
        /// <summary>How many cells past the view's limit the wall's rock runs (the camera sees a little more of what is far from it than of what is near, at the edge).</summary>
        public static int WallPastView = 6;
        public static int ViewPadCells = 4, PadCells = 36, MaxReachCells = 90;
        /// <summary>How fast the view is let out (per second) when a wing's wall comes down.</summary>
        public static float OpenEase = 2.2f;
    }

    /// <summary>
    /// The underground river (see <see cref="CaveGenerator.GenerateRiver"/>, <see cref="CaveData.FlowAt"/>). The current runs Flow px/s from the
    /// guardian's end toward where you arrive, a little faster than an ordinary swimmer (Hero.SwimSpeed) can make way against, so a fall
    /// from the stones is carried back down it; it dies out over EddyCells cells at the beach (so you can climb out), and is Sheltered
    /// (a share of itself) in the treasure pits and the hidden chamber. TunnelCells: how far the tunnel runs in under the guardian's plateau.
    /// A fish (a Shape Shifter's, or a real one) feels only FishResist of it. The Elementalist's narrow draft is a wind in the water:
    /// whoever swims in its column is pushed along its axis at DraftPush px/s.
    /// </summary>
    public static class River
    {
        /// <summary>How far (rows) the stepping stones hang down below the water line.</summary>
        public static float StoneDraft = 2.5f;
        /// <summary>How far apart the stones stand, as a multiple of what the slowest jumper clears with room to spare (1.3: near the edge of a
        /// plain careful jump, so a pass without risk wants skill or a mobility trick: a held run-up, a double jump, a dash, a rope, a draft).</summary>
        public static float GapScale = 1.3f;
        public static float Flow = 112f, EddyCells = 30f, Sheltered = 0.12f, FishResist = 0.3f, DraftPush = 200f;
        public static int TunnelCells = 22;
        /// <summary>The secrets to the left of where you arrive (under the beach): the chance a level has one, and, if so, a second one deeper.</summary>
        public static float LeftSecretChance = 0.6f, SecondSecretChance = 0.4f;
    }

    /// <summary>
    /// The Glitch (see <see cref="DaggerCave.Glitch"/>). It comes only to a party in which everyone has slain the Elder Dragon: Chance a
    /// level (from depth 1 to the one above the dragon's), ArriveMin..ArriveMax s into it. Its health (never grown by depth), size, and XP.
    /// It blinks beside a hero every BlinkMin..BlinkMax s (BlinkNear..BlinkFar px from them) and between blinks stutters toward them in
    /// jumps of about StepPx every StepEvery s (faster than a hero runs). Its blow: Reach px, Windup s, Cooldown s. It is drawn within
    /// ShowRadius px of where it is, the place re-rolled every ShowMin..ShowMax s. A blow rolls its health anew, never to nothing until it
    /// has been struck HitsToMortal times, then ZeroChance a blow; its own blow does the same to a hero (HeroHitsToMortal,
    /// HeroZeroChance) and DebuffChance of the time leaves an affliction too. The wall it leaves wrong: WallWidth x WallHeight px.
    /// </summary>
    public static class Glitch
    {
        public static float Chance = 0.05f, ArriveMin = 25f, ArriveMax = 70f;
        public static float Hp = 999f, Radius = 20f;
        public static int Xp = 400;
        public static float BlinkMin = 1.8f, BlinkMax = 3.4f, BlinkNear = 46f, BlinkFar = 96f, StepPx = 24f, StepEvery = 0.1f;
        public static float Reach = 40f, Windup = 0.5f, Cooldown = 1.5f;
        public static float ShowRadius = 48f, ShowMin = 0.05f, ShowMax = 0.3f;
        public static int HitsToMortal = 100, HeroHitsToMortal = 8;
        public static float ZeroChance = 0.1f, HeroZeroChance = 0.15f, DebuffChance = 0.2f;
        public static float WallWidth = 56f, WallHeight = 84f;
    }

    /// <summary>The secret depth under the lakes.</summary>
    public static class Abyss
    {
        /// <summary>The chance that a level with water has a drain at the bottom of its lowest lake (about one level in three: roughly once a run).</summary>
        public static float DrainChance = 0.3f;
    }

    /// <summary>Boulders plugging narrow passages.</summary>
    public static class Rubble
    {
        public static int Hits = 4;
        /// <summary>Plugs per cave (rolled between the two) and how far from the start the nearest may be (cells).</summary>
        public static int Min = 2, Max = 5, KeepFromStart = 30;
    }

    /// <summary>The party's rope (lowered by anyone, climbed by anyone).</summary>
    /// <summary>
    /// The rope every hero carries: Length px of it (about six times a hero's height), let down while the button is held; MinLength px of
    /// drop is needed beside you to lower it at all. A tied-off rope (no holder) lasts Seconds. Segment: the chain's step (px). The coil falls
    /// at most CoilFallMax px/s and pays out up to PayOutSpeed px/s; reeled in at ReelSpeed. Gravity (px/s/s) and Damping (kept per step) for
    /// the swing; a climber weighs ClimberMass rope points and pumps at PumpAccel px/s/s. Climbing at ClimbSpeed px/s; GrabWidth px either side
    /// of the rope takes hold.
    /// </summary>
    public static class Rope
    {
        public const float Segment = 6f;
        public static float Length = 156, MinLength = 24, Seconds = 25, Cooldown = 0.8f, ClimbSpeed = 80, GrabWidth = 11;
        public static float CoilFallMax = 230, PayOutSpeed = 170, ReelSpeed = 120, Gravity = 900, Damping = 0.996f, ClimberMass = 30, PumpAccel = 240;
    }

    public static class Crab
    {
        public static float Hp = 34, Contact = 6, PinchDamage = 11, WalkSpeed = 52, SwimSpeed = 46, PinchWindup = 0.4f, PinchCooldown = 1.7f, PinchReach = 26, Size = 2.2f;
        public static int Xp = 6;
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
        /// <summary>(Goblins hit softer, 0.8x, and slower, at 0.65x the old rate: the wind-up, the pause after and the sling's wait are all longer.)</summary>
        public static float Hp = 26, Contact = 3.2f, ClubDamage = 9.6f, RunSpeed = 125, SlingerSpeed = 95;
        public static float AggroRange = 460, WindupTime = 0.58f, RockDamage = 6.4f, ThrowCooldown = 4.6f;
        /// <summary>Pause after a club swing before the goblin acts again.</summary>
        public static float RecoverTime = 1.23f;
        public static int Xp = 5;
        /// <summary>The Goblin Warchief (a guardian goblin): every CallCooldown s, while fewer than CallMax of its band live, it bellows for
        /// two more (a club and a sling) who come running from either side. Its band leave nothing when they fall.</summary>
        public static float CallCooldown = 11f, CallWindup = 0.7f;
        public static int CallMax = 3;
    }

    public static class Spider
    {
        public static float Hp = 18, Contact = 9, CrawlSpeed = 70, DropSpeed = 330, GroundSpeed = 150, PounceCooldown = 1.9f;
        /// <summary>In water it rows after you at SwimSpeed px/s, and darts at DartSpeed.</summary>
        public static float SwimSpeed = 95, DartSpeed = 250;
        public static int Xp = 4;
        /// <summary>A guardian spider's web: every WebCooldown s (from WebMin to WebMax px away, in sight) she rears back for WebWindup s
        /// and spits a glob of silk at WebSpeed px/s; a hero it strikes takes WebDamage and is wrapped for up to WebSeconds.</summary>
        public static float WebCooldown = 6.5f, WebWindup = 0.8f, WebSpeed = 330f, WebSeconds = 4.5f, WebDamage = 3f, WebMin = 50f, WebMax = 380f;
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

    /// <summary>
    /// The Elementals (Enemies/Elementals.cs): health, touch damage, walking (or swimming) speed, the
    /// damage of each thing they throw, the seconds between attacks and the XP they give. Nature also
    /// heals RegenPerSec of its health a second once it has been left alone for three seconds.
    /// </summary>
    public static class Elementals
    {
        /// <summary>How big the elementals stand (x; the water drop a little more).</summary>
        public static float Scale = 1.3f, ScaleWater = 1.4f;
        public static class Earth { public static float Hp = 80, Contact = 11, Speed = 38, Damage = 13, Cooldown = 3.4f; public static int Xp = 12; }
        public static class Frost { public static float Hp = 85, Contact = 10, Speed = 40, Damage = 7, Cooldown = 3.0f; public static int Xp = 13; }
        public static class Nature { public static float Hp = 62, Contact = 9, Speed = 52, Damage = 6, Cooldown = 2.4f, RegenPerSec = 0.04f; public static int Xp = 10; }
        public static class Fire { public static float Hp = 55, Contact = 12, Speed = 78, Damage = 7, Cooldown = 2.6f; public static int Xp = 11; }
        public static class Water { public static float Hp = 50, Contact = 7, Speed = 70, Damage = 6, Cooldown = 2.6f; public static int Xp = 9; }
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
        public static float Hp = 52.5f, Contact = 12, SwipeDamage = 16, WalkSpeed = 60, ChargeSpeed = 240, SwipeWindup = 0.45f, ChargeWindup = 0.55f, ChargeCooldown = 5f;
        /// <summary>In water it treads toward you at SwimSpeed px/s and, since it can't rear up to
        /// swipe, bites: BiteWindup s with the head drawn back, then a lunge for BiteDamage.</summary>
        public static float SwimSpeed = 55, BiteDamage = 13, BiteWindup = 0.35f, BiteReach = 40, BiteCooldown = 1.4f;
        public static int Xp = 12;
        /// <summary>A guardian bear's roar: every RoarCooldown s, with a hero within RoarRange px, it rears and fills its chest for
        /// RoarWindup s, then roars: every hero within RoarRange is stunned for StunSeconds, and it rushes the nearest at RushSpeed
        /// px/s and takes its swipe (a quicker rearing, RushSwipeWindup s).</summary>
        public static float RoarCooldown = 9f, RoarWindup = 0.85f, RoarRange = 250f, StunSeconds = 1.7f, RushSpeed = 300f, RushSwipeWindup = 0.25f;
    }

    public static class Scorpion
    {
        public static float Hp = 26, Contact = 6, StingDamage = 11, WalkSpeed = 72, StingWindup = 0.45f, StingReach = 40, StingCooldown = 1.6f;
        public static int Xp = 5;
        /// <summary>A guardian scorpion's venom spray: every SprayCooldown s, a hero within SprayRange px, it arches its tail forward
        /// for SprayWindup s and sprays a cone (SprayHalfAngle radians either side) for SpraySeconds: each hero in it takes SprayDamage
        /// and VenomTotal of poison over VenomSeconds (a second soaking adds to the first).</summary>
        public static float SprayCooldown = 7f, SprayWindup = 0.9f, SprayRange = 190f, SprayHalfAngle = 0.55f, SpraySeconds = 0.5f, SprayDamage = 2f,
            VenomTotal = 26f, VenomSeconds = 8f;
    }

    /// <summary>The merchant (<c>Enemies/Merchant.cs</c>, <c>Main.Shop.cs</c>): <c>CageShare</c> of the cages hold him while he is still to be found.</summary>
    public static class Merchant
    {
        public static float CageShare = 0.35f;
        /// <summary>His stall is offered twice a run, at depths drawn between these (the first no sooner than <c>FirstDepth</c>, the second no later than <c>LastDepth</c>).</summary>
        public static int FirstDepth = 1, LastDepth = 9, Visits = 2;
        /// <summary>His trading stand: a relic a hero carries given to a friend, <c>Trades</c> times a run (the Path adds more). With <c>NpcTrades</c>,
        /// the stand trades with him instead: a relic for one of <c>TradeOffer</c> of his (kept for later; off for now).</summary>
        public static int Trades = 1, TradeOffer = 3;
        public static bool NpcTrades = false;
        /// <summary>Prices in gold by a card's rarity (common, uncommon, rare, relic), and his supplies; each rises <c>PerDepth</c> of itself for every level down.</summary>
        public static int PriceCommon = 8, PriceUncommon = 14, PriceRare = 24, PriceRelic = 36, PriceSupplies = 12, PriceReroll = 6, PriceToken = 14;
        /// <summary>How likely a token for dealing a choice of cards again is to be among his supplies, against the others' 1.</summary>
        public static float TokenWeight = 1.2f;
        /// <summary>How much he lays out: cards (a class card first), and supplies drawn from the potion, the provisions and the token.</summary>
        public static int Cards = 2, Supplies = 2;
        /// <summary>Each reroll at a stall costs this much more of its base than the one before.</summary>
        public static float RerollStep = 0.5f;
        public static float PerDepth = 0.08f;
        /// <summary>His supplies: the belt of potions is filled, and <c>SuppliesHeal</c> of your health returns.</summary>
        public static float SuppliesHeal = 0.4f;
    }

    /// <summary>
    /// The Ember Grid's materials, found in runs in place of a drop the hero couldn't use (<c>Core/HexGrid.cs</c>). <c>Reagent</c>: a potion
    /// found with a full belt; <c>Essence</c>: a heart at full health (the hero's own essence); <c>Tumbler</c>: a key with every key slot
    /// full; <c>Seal</c>: gold picked up while carrying at least <c>SealPurse</c>; <c>Pearl</c>: of a guardian leaving an Obsidian Pearl.
    /// A relic chest can be salvaged for Relic Dust instead of its relic.
    /// </summary>
    public static class Grid
    {
        public static float Reagent = 0.5f, Essence = 0.5f, Tumbler = 0.5f, Seal = 0.5f, Pearl = 0.5f;
        public static int SealPurse = 150;
        /// <summary>A Guild Seal from gold no more than once in this many seconds (a pile is many coins).</summary>
        public static float SealGap = 8f;
        /// <summary>Of a milestone offering a "Back to..." card while the loadout has changed one of the hero's abilities.</summary>
        public static float RevertChance = 0.5f;
        /// <summary>Shared Draught: friends this close are healed this share of what a potion heals you at once. Volatile Brew: its burst.</summary>
        public static float SharedDraughtRange = 120f, SharedDraughtShare = 0.4f, VolatileRadius = 80f, VolatileDamage = 25f;
        /// <summary>Lockpick: a vault gate opens without a key for this much gold. Tumbler's Luck: of a vault opened leaving a Tumbler.</summary>
        public static int LockpickGold = 40;
        public static float TumblerLuck = 0.5f;
        /// <summary>Relic Sense / Covetous: how much likelier a relic chest is (added to its chance in place of a shrine; Covetous multiplies).</summary>
        public static float RelicSense = 0.03f, Covetous = 1.5f;
        /// <summary>Merchant's Favor: his cards cost this much more.</summary>
        public static float FavorCardMult = 1.2f;
    }

    /// <summary>
    /// The elemental heroes (<c>Core/Elements.cs</c>, <c>Enemies/Enemy.Elements.cs</c>). A creature's build-up of an element fills to
    /// <c>Full</c> (elites take <c>EliteTake</c> of it, guardians and bosses <c>BossTake</c>), starts to drain <c>DecayDelay</c> s after the
    /// last that reached it, at <c>DecayRate</c> a second. Full of fire: alight (<c>BurnDps</c> x the hero's damage for <c>BurnSeconds</c>);
    /// full again while alight: it combusts (<c>CombustDamage</c> x the hero's damage plus what the fire had left, <c>CombustSplash</c> of it
    /// to whatever is within <c>CombustRadius</c>). Full of frost: chilled (<c>ChillSlow</c> for <c>ChillSeconds</c>); full again while
    /// chilled: frozen solid for <c>FreezeSeconds</c> (a creature that can't be frozen is chilled harder). A frozen creature shattered by a
    /// heavy blow takes <c>ShatterDamage</c> x the hero's damage, <c>ShatterSplash</c> of it round about. Full of earth: stunned
    /// <c>StunSeconds</c> (one that can't be stunned is weakened instead). Sapped (earth): its attacks play <c>SapSlow</c> slower.
    /// </summary>
    public static class Elements
    {
        public static float Full = 100f, EliteTake = 0.7f, BossTake = 0.4f, DecayDelay = 2f, DecayRate = 30f;
        public static float BurnDps = 4f, BurnSeconds = 5f, CombustDamage = 22f, CombustSplash = 0.5f, CombustRadius = 56f;
        public static float ChillSlow = 0.35f, ChillSeconds = 4f, FreezeSeconds = 3f, ShatterDamage = 30f, ShatterSplash = 0.4f, ShatterRadius = 52f;
        public static float StunSeconds = 1.2f, SapSlow = 0.2f, SapSeconds = 3f;
        /// <summary>Full of nature: entangled, vines about it (it moves and acts as if mired for <c>EntangleSeconds</c>) and bleeding sap
        /// (<c>EntangleBleed</c> x the hero's damage over that time). Full of water: soaked for <c>SoakSeconds</c> (its fire put out; frost
        /// builds in it <c>SoakFrost</c> x as fast, fire <c>SoakFire</c> x).</summary>
        public static float EntangleSeconds = 3f, EntangleBleed = 14f, SoakSeconds = 6f, SoakFrost = 2f, SoakFire = 0.5f;
        /// <summary>Full of dark: blinded for <c>BlindSeconds</c> (it loses every hero, as in smoke) and exposed (it takes <c>BlindExpose</c> x
        /// the damage, and more critical blows) for <c>ExposeSeconds</c>.</summary>
        public static float BlindSeconds = 3f, BlindExpose = 1.25f, ExposeSeconds = 4f;
        /// <summary>
        /// Reactions between the statuses. Soaked (water) and entangled (nature) are opposites: soaked, frost builds <c>SoakFrost</c> x as
        /// fast and fire <c>SoakFire</c> x, and a shatter deals <c>SoakShatter</c> x; entangled, fire builds <c>TangleFire</c> x and frost
        /// <c>TangleFrost</c> x, and a fire lit in the vines burns <c>TangleBurn</c> x as long. Blinded (dark), every other element builds
        /// <c>BlindAmp</c> x. Fire taking hold on the chilled or frozen (or frost on the burning) bursts into steam instead: both go, a
        /// scald of <c>SteamDamage</c> within <c>SteamRadius</c>, and a cloud that hides heroes for <c>SteamSeconds</c>. Earth taking hold
        /// on the soaked sinks it in mud: stunned <c>MudStun</c> x as long and mired for <c>MudSeconds</c>. Wind on the burning fans the fire
        /// to every creature within <c>WildfireRadius</c>.
        /// </summary>
        public static float SoakShatter = 1.5f, TangleFire = 1.5f, TangleFrost = 0.6f, TangleBurn = 1.5f, BlindAmp = 1.5f;
        public static float SteamDamage = 14f, SteamRadius = 44f, SteamSeconds = 3f, MudStun = 1.6f, MudSeconds = 3f, WildfireRadius = 48f;
        /// <summary>Statuses a creature refuses by its nature (<see cref="Enemy.StatusTake"/>): its own element, none of it; the armoured take
        /// <c>ArmoredEarth</c> x earth; what flies or swims takes no nature.</summary>
        public static float ArmoredEarth = 0.5f;
        /// <summary>How long after one element press before the next is taken (a press inside it is kept and taken once it's over).</summary>
        public static float ChargeCooldown = 0.3f;
        /// <summary>Whether an element's version of an ability rests its button (and pays alimus). In modes every use is a version, so they do:
        /// one cooldown per button, whatever the mode.</summary>
        public static bool VersionsRest = true;
        /// <summary>Modes: the hero is always in one of their elements (their abilities are all its versions), starting in their signature
        /// (<see cref="DaggerCave.Elements.Signature"/>). One slot sits on top of the mode: the mode's own element there is an overcharge (the
        /// next ability with a greater version uses it, and that element's overcharge comes back after <c>OverchargeRecharge</c> s); another
        /// element there is an attribute (every blow and spell adds <c>AttributeBuild</c> of it, wind a shove of <c>AttributeShove</c>), kept
        /// until replaced. Pressing the attribute's element again switches the mode to it, at most once every <c>ModeCooldown</c> s.</summary>
        public static float ModeCooldown = 4.5f, OverchargeRecharge = 18f, AttributeBuild = 15f, AttributeShove = 120f;
        /// <summary>
        /// The simple kits (the Swordsman, Warden, Elementalist, Vitalist, Aegis and Rogue): three abilities each (a primary attack, a
        /// defensive, a special) and three elements, an element button switching to its element (at most once every <c>SwitchCooldown</c>
        /// s). An ability works the same in every element; what it strikes takes the element's build-up (<c>PrimaryBuild</c> a blow of the
        /// primary, <c>DefensiveBuild</c> from the defensive, <c>SpecialBuild</c> from the special), and it looks and sounds of it.
        /// </summary>
        public static float SwitchCooldown = 1f, PrimaryBuild = 40f, DefensiveBuild = 40f, SpecialBuild = 80f;
        /// <summary>Wind builds nothing: it shoves, <c>ShovePerBuild</c> px/s for each point another element would build. A defensive's pulse
        /// reaches <c>PulseRadius</c> px about the hero.</summary>
        public static float ShovePerBuild = 4f;
        /// <summary>
        /// How each simple kit lays its element on, beyond its abilities' touch:
        ///  * the Swordsman's tipper: a blow landing with the outer part of the blade (from <c>TipperFrom</c> of his reach) deals
        ///    <c>TipperDamage</c> x and builds <c>TipperBuild</c> x; for <c>RiposteWindow</c> s after a roll, every blow is a tipper;
        ///  * the Warden's counter: his cut builds nothing; a blow stopped on his shield builds <c>CounterBuild</c> in the attacker (a perfect
        ///    block, a special's <c>SpecialBuild</c>);
        ///  * the Elementalist's zone: her Burst leaves the element at the spot for <c>ZoneSeconds</c>, <c>ZoneBuild</c> to all in it every
        ///    tick (<c>ZoneTicks</c> of them; wind draws them in at <c>ZonePull</c>);
        ///  * the Vitalist's seep: her hex carries her element, <c>HexSeep</c> a second for as long as the hex lasts; her drain on a creature
        ///    still seeping tears the rest out at once, <c>DrainCash</c> x;
        ///  * the Aegis's imbue: her barrier and her field lend the heroes they wrap her element for <c>ImbueSeconds</c>, every blow of theirs
        ///    building <c>ImbueBuild</c>;
        ///  * the Rogue's stored knife: a thrown dagger builds just <c>KnifeTouch</c> as it lands, then stores the element while it stays in
        ///    the creature (<c>KnifeStore</c> a second, up to <c>KnifeStoreMax</c>), all of it released when it's recalled; a jab from
        ///    hiding builds <c>ShadowJab</c> x.
        /// </summary>
        public static float TipperFrom = 0.65f, TipperDamage = 1.3f, TipperBuild = 2f, RiposteWindow = 1f, CounterBuild = 20f;
        public static float ZoneSeconds = 4f, ZoneBuild = 12f, ZonePull = 60f, HexSeep = 15f, DrainCash = 1.5f, ImbueSeconds = 6f, ImbueBuild = 20f;
        public static float KnifeTouch = 20f, KnifeStore = 25f, KnifeStoreMax = 100f, ShadowJab = 2f;
        public static int ZoneTicks = 8;
        /// <summary>The switch's reward: an element switched to is primed for <c>PrimeSeconds</c>; the first ability used meanwhile lays it on
        /// <c>PrimeMult</c> x (its touches within <c>PrimeWindow</c> s of its use: a bolt in flight, a cleave's arc).</summary>
        public static float PrimeSeconds = 4f, PrimeMult = 2f, PrimeWindow = 1f;
    }

    /// <summary>The world answering a simple kit's element (<see cref="DaggerCave.ElementWorld"/>): frost within <c>FloeReach</c> px of the
    /// water's surface lays a floe <c>FloeHalf</c> px each way that melts after <c>FloeSeconds</c>; nature with rock within <c>VineReach</c> px
    /// above lets down a vine up to <c>VineLength</c> px long for <c>VineSeconds</c>; wind blows a gas cloud <c>GasBlow</c> px along.</summary>
    public static class ElementWorld
    {
        public static float FloeReach = 40f, FloeHalf = 30f, FloeSeconds = 8f, VineReach = 220f, VineLength = 260f, VineSeconds = 12f, GasBlow = 90f;
    }

    /// <summary>
    /// The elemental Swordsman (<c>Player/Player.Elemental.cs</c>). Swing: plain, or Earth = the Heaving Swing (<c>HeaveDamage</c>,
    /// <c>EarthBuild</c>), Fire = Searing Strike, Frost = Razor Ice (<c>MediumDamage</c>, <c>MediumBuild</c>); the element waits
    /// <c>SwingRest</c> s (by element: earth, fire, frost) before it can be lent to a swing again. Hard Swing: plain (<c>HardDamage</c>,
    /// <c>HardRest</c>), ThunderCrush (earth: <c>CrushDamage</c>, full earth, shatters the frozen), Pyroclasty (fire) and Ice Axe (frost),
    /// each its own rest and a full build-up. The jump: Explosive Lunge (fire: a blast where you leave the ground, <c>LungeDamage</c> and
    /// <c>LungeBuild</c> within <c>LungeRadius</c>, and a low leap forward at <c>LungeSpeed</c>), Glacial Vault (frost: you spring back at
    /// <c>VaultBack</c>, <c>VaultJump</c> x a jump, <c>VaultBuild</c> frost to those beside you, leaving an ice decoy where you stood), Earthfall
    /// (earth: you come down like a rock, a shockwave of <c>FallDamage</c> (more the harder you land) and <c>FallBuild</c> earth within
    /// <c>FallRadius</c>); each rests its own time. These are the dodge button's from the ground (with nothing readied, or in the air, it is the
    /// Dash Slash). Empower Hilt: <c>HiltSeconds</c> of every blow carrying it: plain +<c>HiltDamage</c>, earth saps,
    /// frost <c>HiltFrost</c>, fire <c>HiltFire</c> build-up a blow; rests by element.
    /// </summary>
    public static class ElementalSword
    {
        /// <summary>Searing Strike adds <c>SearBuild</c> fire, Razor Ice <c>MediumBuild</c> frost.</summary>
        public static float HeaveDamage = 2f, EarthBuild = 60f, MediumDamage = 1.25f, MediumBuild = 50f, SearBuild = 40f;
        public static float SwingRestEarth = 2.5f, SwingRestFire = 0f, SwingRestFrost = 0f;
        public static float HardDamage = 1.6f, CrushDamage = 2.6f, PyroDamage = 2.2f, AxeDamage = 2f;
        public static float HardRest = 5f, CrushRest = 12f, PyroRest = 10f, AxeRest = 7f, CrushWindup = 1.35f;
        /// <summary>The Ice Axe: the sword goes up over the head and behind, the axe head grows out of it through a wind-up <c>AxeWindup</c> x a
        /// Hard Swing's, and it comes down in front only (<c>AxeArcDegrees</c> of arc, not the Hard Swing's sweep from behind).</summary>
        public static float AxeWindup = 2.4f, AxeArcDegrees = 120f;
        public static float HiltSeconds = 8f, HiltDamage = 0.2f, HiltFrost = 20f, HiltFire = 25f;
        public static float HiltRest = 12f, HiltRestEarth = 14f, HiltRestFrost = 14f, HiltRestFire = 14f;
        /// <summary>Explosive Lunge: the burst behind him (<c>LungeDamage</c>, <c>LungeBuild</c> fire within <c>LungeRadius</c>) throws him forward
        /// <c>LungeSpeed</c> for <c>LungeCarry</c> s, <c>LungeLift</c> x a jump high; until he lands (at most <c>LungeStrikeSeconds</c>) he swings
        /// at every creature he comes within <c>LungeStrikeReach</c> px of, once each.</summary>
        public static float LungeDamage = 1.2f, LungeBuild = 50f, LungeRadius = 56f, LungeSpeed = 340f, LungeLift = 0.85f, LungeCarry = 0.42f, LungeRest = 4f;
        public static float LungeStrikeSeconds = 1.1f, LungeStrikeReach = 20f;
        public static float VaultJump = 0.75f, VaultBack = 300f, VaultCarry = 0.3f, VaultDamage = 0.8f, VaultBuild = 50f, VaultRadius = 46f, VaultRest = 4f;
        /// <summary>Glacial Vault's decoy: the Swordsman in ice, <c>DecoyHeight</c> x 2 <c>DecoyHalf</c> px, drawing creatures within
        /// <c>Dual.LureRange</c>; <c>DecoyHits</c> blows (or <c>DecoySeconds</c>) and it shatters, <c>DecoyBuild</c> frost within <c>DecoyRadius</c>.</summary>
        public static int DecoyHits = 2;
        public static float DecoyHeight = 26f, DecoyHalf = 8f, DecoySeconds = 6f, DecoyBuild = 40f, DecoyRadius = 44f;
        public static float FallDamage = 1.4f, FallBuild = 60f, FallRadius = 72f, FallToss = 220f, FallRest = 5f;
        /// <summary>Stalagmite Vault (frost overcharged; from the ground): he leaps back (<c>StalagBack</c>, <c>StalagJump</c> x a jump, carried
        /// <c>StalagCarry</c> s) and a stalagmite of ice <c>SpikeHeight</c> px tall bursts from the floor every <c>SpikeEvery</c> px he passes over:
        /// <c>SpikeDamage</c> and <c>SpikeBuild</c> frost to what it meets (once each), tossed up; they stand <c>SpikeStand</c> s and melt.</summary>
        public static float StalagBack = 330f, StalagJump = 0.85f, StalagCarry = 0.45f, SpikeEvery = 16f, SpikeHeight = 44f, SpikeDamage = 1.1f, SpikeBuild = 50f, SpikeStand = 1.1f, SpikeToss = 260f;
        /// <summary>Comet (earth and frost): a leap forward (<c>CometSpeed</c>, <c>CometLift</c> x a jump; in the air no jump), all blue-white; landing,
        /// a frost explosion within <c>CometRadius</c>: <c>CometDamage</c> and a full frost build-up; <c>CometCrystals</c> crystals burst out of the
        /// floor and melt after <c>CometStand</c> s.</summary>
        public static float CometSpeed = 300f, CometLift = 0.8f, CometDamage = 3f, CometRadius = 84f, CometStand = 1f;
        public static int CometCrystals = 13;
        /// <summary>Blazing Charge (earth and fire, the dash): he charges <c>BlazeSpeed</c> for <c>BlazeSeconds</c>, sword up before him in a sphere of
        /// fire, crashing into every creature within <c>BlazeReach</c> px once: <c>BlazeDamage</c>, a full fire build-up, thrown <c>BlazeToss</c>;
        /// fire trails behind him.</summary>
        public static float BlazeSpeed = 430f, BlazeSeconds = 0.42f, BlazeDamage = 1.6f, BlazeReach = 20f, BlazeToss = 300f, BlazeRest = 5f;
        /// <summary>Steam Vault (fire and frost): the steam left behind scalds creatures in it every <c>ScaldTick</c> s, <c>ScaldDamage</c> x his
        /// damage multiplier and <c>ScaldBuild</c> fire and frost.</summary>
        public static float ScaldTick = 0.4f, ScaldDamage = 1.5f, ScaldBuild = 12f;
        /// <summary>Magma Hilt (earth and fire): a blow leaves a little pool of magma under the creature, at most every <c>MagmaDripRest</c> s.</summary>
        public static float MagmaDripRest = 1.2f;
    }

    /// <summary>
    /// The elemental Elementalist (<c>Player/Player.Elemental.cs</c>). Bolt (the attack): an element lent to it lasts <c>MissileShots</c> bolts;
    /// plain bolts deal <c>PlainBolt</c> of a firebolt; Firebolt <c>FireBuild</c>, Frostbolt <c>FrostBuild</c> a bolt; Gust deals <c>GustDamage</c>
    /// of a firebolt and shoves light creatures <c>GustPush</c>. Movement (the dodge button): Lift (plain: a push up, <c>LiftJump</c> x a jump,
    /// in the air too), Fire Walk (fire: <c>WalkMove</c> x faster for <c>WalkSeconds</c>, leaving a trail of fire, <c>TrailBuild</c> a tick), Tundra
    /// (frost: the ground about you freezes for <c>TundraSeconds</c>, <c>TundraHalf</c> px each way; heroes on it go faster, creatures are chilled and
    /// filled with frost), Updraft (wind: the aimed column). Fixed range (the Burst button: a spot <c>FixedRange</c> px along the aim): Force Blast
    /// (plain, <c>BlastDamage</c>), Burst (fire: an explosion, <c>BurstDamage</c> and <c>BurstBuild</c> fire; the burning take a full build-up and
    /// combust), Snap (frost: the frozen shatter, the rest <c>SnapBuild</c> frost), Cyclone (wind: the storm that drags light creatures in,
    /// <c>CyclonePull</c>), all within <c>FixedRadius</c>. Aid (the Extended button): Ward (plain: a barrier of <c>WardBarrier</c> for you and friends
    /// within <c>AidRange</c>), Kindling (fire: <c>KindleBuild</c> fire a blow), Frost Armor (frost: melee attackers get a full frost build-up),
    /// Narrow Draft (wind: <c>TailwindMove</c> x faster, <c>TailwindJump</c> x higher, for <c>TailwindSeconds</c>), for <c>AidSeconds</c>. Each version rests its own time.
    /// </summary>
    public static class ElementalStaff
    {
        public static int MissileShots = 1;
        public static float PlainBolt = 0.85f, FireBuild = 50f, FrostBuild = 25f, GustDamage = 0.15f, GustPush = 480f;
        /// <summary>The attack's pace and weight: <c>ShotGap</c> s between casts (<c>QuickGap</c> for frost and wind), each pop or bolt
        /// <c>ShotDamage</c> x its damage.</summary>
        public static float ShotGap = 0.625f, QuickGap = 0.5f, ShotDamage = 0.85f;
        public static float StormFrostBuild = 20f, StormFireBuild = 30f, CyclonePull = 70f;
        /// <summary>With nothing readied the attack is a pop: no bolt, a small burst on the creature within <c>PopRange</c> px nearest the aim
        /// (<c>PopCone</c> degrees either way), <c>PopDamage</c>.</summary>
        public static float PopRange = 120f, PopCone = 40f, PopDamage = 9f, PopRadius = 10f;
        /// <summary>What the elemental Elementalist's spells cost in alimus (paid as each comes back): a bolt, Lift, the fixed-range spell, Aid.</summary>
        public static float BoltCost = 0.8f, LiftCost = 4f, FixedCost = 2f, AidCost = 6f;
        // movement, the dodge button: Lift, or the elemental versions with a charge readied
        public static float LiftJump = 1f, LiftRest = 2.5f;
        public static float WalkMove = 1.3f, WalkSeconds = 4f, TrailLife = 2.5f, TrailTick = 0.3f, TrailBuild = 14f, TrailDamage = 1.5f, WalkRest = 9f;
        public static float TundraSeconds = 6f, TundraHalf = 110f, TundraTick = 0.4f, TundraBuild = 12f, TundraSlow = 0.3f, TundraRest = 10f;
        public static float UpdraftRest = 3f;
        // fixed range
        public static float FixedRange = 130f, FixedRadius = 64f, BlastRadius = 100f;
        public static float BlastDamage = 7f, BurstDamage = 18f, BurstBuild = 60f, SnapBuild = 60f, SnapDamage = 8f;
        public static float BlastRest = 4f, BurstRest = 7f, SnapRest = 7f, CycloneRest = 9f;
        // aid
        public static float AidRange = 160f, AidSeconds = 8f, TailwindSeconds = 4f, TailwindMove = 1.2f, TailwindJump = 1.1f, KindleBuild = 15f, WardBarrier = 14f;
        public static float AidRestWard = 11f, AidRestWind = 10f, AidRestFrost = 12f, AidRestFire = 12f;
    }

    /// <summary>
    /// The elemental Warden (<c>Player/Player.WardenElements.cs</c>): earth, frost and wind on his three buttons, the attack (the shortsword),
    /// the block (the shield raised) and the dash (the Guarded Charge), each with a version for every element and every pair. His Shield Bash
    /// is not in the elemental kit. Damage is a multiple of his blow (<c>Warden.Damage</c> x his damage multiplier).
    /// </summary>
    public static class ElementalWarden
    {
        // ---- the attack (each mode's swing has its own pace: wind-up, swing and cooldown x <c>...Pace</c>)
        /// <summary>Stonecutter (earth): slow (<c>StonePace</c>), hard (<c>StoneDamage</c> x), <c>EdgeBuild</c> earth, a shake and grit. Rime Edge
        /// (frost): medium (<c>RimePace</c>, <c>RimeDamage</c> x), <c>EdgeBuild</c> frost. Gale Edge (wind): fast (<c>GalePace</c>), light
        /// (<c>GaleDamage</c> x), reach <c>GaleReach</c> x, what it strikes thrown back <c>GaleShove</c>.</summary>
        public static float StonePace = 2f, StoneDamage = 2f, RimePace = 1.25f, RimeDamage = 1.2f, GalePace = 0.65f, GaleDamage = 0.6f,
            EdgeBuild = 50f, GaleReach = 1.15f, GaleShove = 400f;
        /// <summary>Boulder Strike (earth+earth): <c>BoulderDamage</c> x, a full earth build-up, shatters the frozen, tosses <c>BoulderToss</c>.</summary>
        public static float BoulderDamage = 2.2f, BoulderToss = 300f;
        /// <summary>Icicle Lance (frost+frost): the cut and a lance of ice thrown on along the aim, <c>LanceDamage</c> x, a full frost build-up.</summary>
        public static float LanceDamage = 1.4f, LanceRange = 260f;
        /// <summary>Cyclone Slash (wind+wind): a whirling cut all round, <c>CycloneReach</c> x the reach, <c>CycloneDamage</c> x, shoving all
        /// it strikes away <c>CycloneShove</c>.</summary>
        public static float CycloneDamage = 1.3f, CycloneReach = 1.5f, CycloneShove = 340f;
        /// <summary>Frozen Earth (earth+frost): <c>FrozenDamage</c> x, <c>FrozenBuild</c> of earth and of frost (and it shatters the frozen).</summary>
        public static float FrozenDamage = 1.5f, FrozenBuild = 60f;
        /// <summary>Sandblast (earth+wind) and Frost Gust (frost+wind): the cut throws a cone of grit (or freezing wind) <c>ConeRange</c> px on,
        /// <c>ConeHalf</c> degrees either way: grit nicks (<c>SandDamage</c> x) and saps for <c>SandSap</c> s; the gust fills with
        /// <c>GustBuild</c> frost and shoves <c>GustShove</c>.</summary>
        public static float ConeRange = 120f, ConeHalf = 38f, SandDamage = 0.5f, SandSap = 3f, GustBuild = 60f, GustShove = 320f;

        // ---- the block (the shield raised in a mode)
        /// <summary>The elemental Warden's shield: one pool of <c>ShieldHp</c> (x his health over his starting health) whatever the mode, worn
        /// 1:1 by what it absorbs (<c>ShieldCost</c>). Raised, it absorbs <c>EarthShare</c> of a blow in earth mode (a slab of stone),
        /// <c>FrostShare</c> in frost (a pane of ice), <c>WindShare</c> in wind (a veil of air); a perfect block still stops it all. Broken, it
        /// can't be raised for <c>ShieldBreakTime</c> s, but it mends from the moment it breaks, <c>ShieldRegen</c> a second (x his health over
        /// his starting health), after <c>Tune.Warden.ShieldRegenDelay</c> once struck.</summary>
        public static float ShieldHp = 50f, ShieldRegen = 5f, ShieldBreakTime = 4f, ShieldCost = 1f;
        public static float EarthShare = 0.9f, FrostShare = 0.45f, WindShare = 0.2f;
        /// <summary>Earth Shield (earth overcharged, the block): the ground in front (<c>MoundReach</c> px on) heaves up and over him (rising in
        /// <c>MoundRise</c>) and stands while he holds his shield up, a wall to creatures (<c>MoundWallWidth</c> x <c>MoundWallHeight</c> px);
        /// let go, it sinks back in <c>MoundSink</c>. While it stands nothing reaches him and nothing moves him, nor does he move; he kneels
        /// behind it.</summary>
        public static float MoundRise = 0.3f, MoundSink = 0.45f, MoundReach = 22f, MoundWallWidth = 22f, MoundWallHeight = 46f;
        /// <summary>Ice Barrier (frost overcharged, the block): a shell of ice about him (it stays when the shield is lowered) absorbs
        /// <c>IceShare</c> of every blow; once it has absorbed <c>IceHold</c> it shatters, filling every creature within <c>FrostShellRadius</c>
        /// with <c>FrostShellBuild</c> frost and dealing <c>FrostShellDamage</c> (x his damage).</summary>
        public static float IceShare = 0.8f, IceHold = 15f, FrostShellRadius = 72f, FrostShellBuild = 90f, FrostShellDamage = 10f;
        /// <summary>Tenuous Barrier (wind overcharged, the block): a faint skin of wind absorbs <c>TenuousShare</c> of every blow and dissipates
        /// once it has absorbed <c>TenuousHold</c>.</summary>
        public static float TenuousShare = 0.2f, TenuousHold = 10f;
        /// <summary>The mixed guards (the block with an attribute) last as long as the shield stays up, <c>GuardSeconds</c> at most; wind in
        /// one throws a melee striker back <c>GuardShove</c>.</summary>
        public static float GuardSeconds = 6f, GuardShove = 380f;
        /// <summary>Permafrost Guard (earth+frost): the ground about him freezes, <c>PermafrostHalf</c> px each way, for <c>GuardSeconds</c>.</summary>
        public static float PermafrostHalf = 100f;
        /// <summary>Dust Wall (earth+wind): a cloud of dust in front, <c>DustRadius</c>, for <c>DustSeconds</c>: friends in it are hidden,
        /// creatures in it are sapped.</summary>
        public static float DustRadius = 56f, DustSeconds = 6f;

        // ---- the dash (the Guarded Charge)
        /// <summary>Rockslide Charge: ploughs through what's in its way (each struck once, <c>PloughDamage</c> x) and ends in a shockwave of earth
        /// within <c>QuakeRadius</c> (<c>QuakeDamage</c> x, a full earth build-up).</summary>
        public static float PloughDamage = 0.6f, QuakeRadius = 60f, QuakeDamage = 1.4f;
        /// <summary>Frozen Tundra: the ground it crossed (and <c>TundraPad</c> px past each end) freezes for <c>TundraSeconds</c>; what it passes
        /// takes <c>PassBuild</c> frost.</summary>
        public static float TundraSeconds = 8f, TundraPad = 40f, PassBuild = 40f;
        /// <summary>Gale Charge: <c>GaleTime</c> x as long and <c>GaleSpeed</c> x as fast, through creatures (thrown aside <c>AsideShove</c>);
        /// friends it passes get a tailwind for <c>TailwindSeconds</c>.</summary>
        public static float GaleTime = 1.6f, GaleSpeed = 1.3f, AsideShove = 300f, TailwindSeconds = 3f;
        /// <summary>Avalanche (earth+earth): a Rockslide whose quake is <c>AvalancheRadius</c> (<c>AvalancheDamage</c> x), and a fissure runs on
        /// <c>FissureRange</c> px ahead.</summary>
        public static float AvalancheRadius = 92f, AvalancheDamage = 2f, FissureRange = 220f;
        /// <summary>Tempest Charge (wind+wind): <c>TempestTime</c> x as long, carrying what it passes along in its wake, flung at the end
        /// (<c>TempestFling</c>).</summary>
        public static float TempestTime = 2f, TempestFling = 480f;
        /// <summary>Frostquake (earth+frost): the quake, and ice bursts up where the charge ends (<c>FrostquakeDamage</c> x, full frost).</summary>
        public static float FrostquakeDamage = 2.5f;
        /// <summary>Duststorm (earth+wind): clouds of dust along its path (<c>DustRadius</c>), hiding friends, sapping creatures.</summary>
        public static float StormDustSeconds = 6f;
        /// <summary>Glacier Charge (frost+frost): spikes of ice <c>GlacierSpikeHeight</c> px tall burst from the ground every <c>GlacierSpikeEvery</c>
        /// px he charges over, <c>GlacierSpikeDamage</c> x his blow to what they meet, tossed up.</summary>
        public static float GlacierSpikeEvery = 18f, GlacierSpikeHeight = 36f, GlacierSpikeDamage = 0.6f;
        /// <summary>Blizzard Charge (frost+wind): where it ends a blizzard whirls up (the Elementalist's storm): each strike
        /// <c>BlizzardStormDamage</c> (x his damage) and <c>BlizzardStormBuild</c> frost, pulling light creatures in (<c>BlizzardStormPull</c>).</summary>
        public static float BlizzardStormDamage = 2f, BlizzardStormBuild = 18f, BlizzardStormPull = 60f;
    }

    /// <summary>
    /// The elemental Vitalist (nature, wind, water; <c>Player/Player.VitalistElements.cs</c>). Her drain, the hex about her and her heal each
    /// take the mode's version, or a pair's. The drain: Thorn Drain (<c>ThornBuild</c> nature, <c>ThornForce</c> more vital force), Wisp
    /// Drain (reach <c>WispReach</c> x, cast <c>WispPace</c> x as often, <c>WispDamage</c> x), Tide Drain (<c>TideBuild</c> water, mends her
    /// <c>TideHeal</c> of what it tears out). Bramble Lash lashes up to <c>LashCount</c> creatures within <c>LashRadius</c> of the target
    /// (<c>LashShare</c> x); Whirling Siphon drains up to <c>SiphonCount</c> more in reach (<c>SiphonShare</c> x); Riptide hauls the target
    /// toward her (<c>RiptidePull</c>); Pollen Drain and Rain Siphon spread <c>CloudBuild</c> nature or water to all within
    /// <c>CloudRadius</c> of it; Life Spring mends every friend near her by <c>SpringHeal</c> of what it tears out.
    /// </summary>
    public static class ElementalVital
    {
        public static float ThornBuild = 40f, ThornForce = 0.25f, WispReach = 1.4f, WispPace = 0.75f, WispDamage = 0.8f, TideBuild = 40f, TideHeal = 0.15f;
        public static float LashRadius = 80f, LashShare = 0.6f, SiphonShare = 0.7f, RiptidePull = 320f, CloudRadius = 60f, CloudBuild = 40f, SpringHeal = 0.3f, SpringBuild = 30f;
        public static int LashCount = 3, SiphonCount = 3;
        /// <summary>The hex: Overgrowth (<c>OvergrowthBuild</c> nature to all in it), Withering Gale (<c>GaleReach</c> x the radius, everything
        /// in it thrown out <c>HexShove</c>), Drowning Hex (<c>DrownBuild</c> water). Strangling Grove: <c>GroveReach</c> x, a full meter of
        /// nature; Eye of the Tempest: <c>TempestReach</c> x, everything in it hauled in toward her (<c>TempestPull</c>); Maelstrom: a full meter
        /// of water, and a pool of healing water at her feet (<c>PoolRadius</c>, <c>PoolRate</c> a second, <c>PoolSeconds</c>); Spore Storm:
        /// <c>SporeReach</c> x, <c>MixBuild</c> nature and a shove; Mire Bloom: mired <c>MireSeconds</c> and <c>MixBuild</c> nature; Squall:
        /// <c>MixBuild</c> water and a shove.</summary>
        public static float OvergrowthBuild = 50f, GaleReach = 1.2f, HexShove = 300f, DrownBuild = 60f, GroveReach = 1.3f, TempestReach = 1.6f, TempestPull = 260f;
        public static float PoolRadius = 80f, PoolRate = 4f, PoolSeconds = 5f, SporeReach = 1.5f, MixBuild = 40f, MireSeconds = 4f;
        /// <summary>The heal: Regrowth (<c>RegrowNow</c> of it now, <c>RegrowLater</c> over <c>MendSeconds</c>), Breath of Life (reach
        /// <c>BreathReach</c> x, a tailwind <c>TailwindSeconds</c>), Cleansing Wave (every affliction washed off those in reach). Bloom of Life:
        /// <c>BloomAmp</c> x and a healing pool; Second Wind: everyone, wherever they are, and a tailwind; Wellspring: <c>WellAmp</c> x, a barrier of
        /// <c>WellBarrier</c> of each one's health for <c>WellSeconds</c>, and cleansed; Seeds on the Wind: reach <c>BreathReach</c> x and
        /// <c>SeedsMend</c> more over <c>MendSeconds</c>; Rejuvenation: <c>RejuvNow</c> now, <c>RejuvLater</c> over time, cleansed; Mist of Mending:
        /// a mist about her (<c>MistRadius</c>, <c>MistSeconds</c>) that hides her friends in it.</summary>
        public static float RegrowNow = 0.5f, RegrowLater = 0.75f, MendSeconds = 5f, BreathReach = 1.6f, TailwindSeconds = 3f, BloomAmp = 1.5f, WellAmp = 1.2f;
        public static float WellBarrier = 0.12f, WellSeconds = 8f, SeedsMend = 12f, RejuvNow = 0.6f, RejuvLater = 1f, MistRadius = 70f, MistSeconds = 5f;
    }

    /// <summary>
    /// The elemental Aegis (wind, earth, frost; <c>Player/Player.AegisElements.cs</c>). Her bubble shot (the ward bolt), her bubble field (the
    /// dodge button) and her barrier each take the mode's version, or a pair's. The shot: Gale Bubble (<c>GaleSpeed</c> x as fast,
    /// <c>GalePace</c> x as often, its burst throwing creatures back <c>BurstShove</c>), Stone Bubble (<c>StoneDamage</c> x, <c>StoneSpeed</c> x,
    /// <c>StonePace</c> x, burst <c>StoneBurst</c> x, <c>ShotBuild</c> earth), Rime Bubble (<c>ShotBuild</c> frost to all in the burst).
    /// Boulder Bubble: <c>BoulderDamage</c> x, burst <c>BoulderBurst</c> x, a full meter of earth on the one struck; Frost Orb: a full meter of
    /// frost on all in the burst; Bubble Volley: <c>VolleyCount</c> shots fanned <c>VolleySpread</c> rad apart; Hail Bubble:
    /// <c>HailDamage</c> x, earth and frost, shattering the frozen; Sand Bubble: a cloud of dust (<c>DustRadius</c>, <c>DustSeconds</c>) that
    /// saps (<c>SandSap</c> s); Snow Bubble: frost and a shove, burst <c>SnowBurst</c> x.
    /// </summary>
    public static class ElementalRogue
    {
        /// <summary>The strike (her jab): Shadow Strike (<c>ShadowDamage</c> x, <c>JabBuild</c> dark), Gale Jab (<c>GalePace</c> x the pace,
        /// <c>GaleDamage</c> x, reach <c>GaleReach</c> x, a shove of <c>JabShove</c>), Venom Jab (<c>JabBuild</c> nature and a poison of
        /// <c>Venom</c> x her damage over <c>VenomSeconds</c>). Nightfall: <c>NightfallDamage</c> x, a sure crit, a full meter of dark (blinded);
        /// Flurry: <c>FlurryPace</c> x the pace, a 180-degree sweep, <c>GaleReach</c> x; Deathcap: a full meter of nature and twice the poison;
        /// Thornwind: reach <c>ThornReach</c> x, nature and the shove; Shade Step: she steps through the creature she strikes to its far side;
        /// Nightshade: dark, nature and the poison.</summary>
        public static float ShadowDamage = 1.2f, JabBuild = 40f, GalePace = 0.7f, GaleDamage = 0.85f, GaleReach = 1.2f, JabShove = 180f, Venom = 8f, VenomSeconds = 4f;
        public static float NightfallDamage = 1.6f, FlurryPace = 0.55f, ThornReach = 1.4f;
        /// <summary>The throw: Shadow Knife (<c>KnifeBuild</c> dark), Gale Knife (<c>GaleSpeed</c> x as fast, a shove of <c>KnifeShove</c>),
        /// Barbed Knife (<c>KnifeBuild</c> nature and the poison). Umbral Knife: a full meter of dark and a sure crit; Fan of Knives: both
        /// daggers fanned, fast; Thorn Knife: a full meter of nature and a bleed (<c>ThornBleed</c> x her damage); Pollen Knife: nature to all
        /// within <c>PollenRadius</c> of the one struck; Shadow Tether: she is pulled along to the dagger, and strikes again as she lands;
        /// Nightshade Knife: dark, nature and the poison.</summary>
        public static float KnifeBuild = 50f, GaleSpeed = 1.5f, KnifeShove = 220f, ThornBleed = 12f, PollenRadius = 60f;
        /// <summary>The evade (her vanish): Shadowmeld (<c>MeldTime</c> x as long), Gale Step (a dash ahead, <c>DashSpeed</c>), Spore Cloud
        /// (<c>EvadeBuild</c> nature to creatures within <c>EvadeRadius</c>). Umbral Veil: <c>VeilTime</c> x as long, every creature within
        /// <c>EvadeRadius</c> blinded; Wind Walk: the dash and a tailwind for <c>TailwindSeconds</c>; Overgrowth Veil: a full meter of nature to
        /// creatures near; Leaf Storm: nature and a shove (<c>EvadeShove</c>); Shadow Dash: a longer dash (<c>LongDash</c> x), a cloud of smoke
        /// where she stood (<c>SmokeRadius</c>, <c>SmokeSeconds</c>); Nightbloom: the smoke, and dark and nature to creatures near.</summary>
        public static float MeldTime = 1.5f, DashSpeed = 520f, EvadeBuild = 40f, EvadeRadius = 70f, VeilTime = 2f, TailwindSeconds = 4f, EvadeShove = 260f;
        public static float LongDash = 1.5f, SmokeRadius = 50f, SmokeSeconds = 4f;
    }

    /// <summary>
    /// The elemental Shape Shifter (<c>Player/Player.ShifterElements.cs</c>): a form's blows add <c>BlowBuild</c> of its element (nature,
    /// water; a wind form's shove <c>WindShove</c>). Overcharged, the special is the greater one: for <c>GreaterSeconds</c> its blows deal
    /// <c>GreaterDamage</c> x and fill the element's meter (a wind form's shove <c>GreaterShove</c>). Out of a form her staff adds
    /// <c>StaffBuild</c> of the mode's element (wind: <c>GaleReach</c> x the reach).
    /// </summary>
    public static class ElementalShifter
    {
        public static float BlowBuild = 20f, WindShove = 160f, GreaterSeconds = 2.5f, GreaterDamage = 1.5f, GreaterShove = 360f, StaffBuild = 30f, GaleReach = 1.25f;
    }

    public static class ElementalAegis
    {
        public static float GaleSpeed = 1.5f, GalePace = 0.75f, BurstShove = 260f, StoneDamage = 1.5f, StoneSpeed = 0.75f, StonePace = 1.3f, StoneBurst = 1.3f, ShotBuild = 40f;
        public static float BoulderDamage = 2.2f, BoulderBurst = 1.5f, VolleySpread = 0.22f, HailDamage = 1.5f, DustRadius = 46f, DustSeconds = 4f, SandSap = 3f, SnowBurst = 1.5f;
        public static int VolleyCount = 3;
        /// <summary>The bubble field: Air Dome (creatures in it thrown out <c>DomeShove</c>), Stone Dome (bubbles <c>StoneCap</c> x as strong),
        /// Ice Dome (<c>DomeBuild</c> frost to creatures in it). Fortress: <c>FortressCap</c> x, lasting <c>FortressTime</c> x; Glacier Dome: a full
        /// meter of frost; Gale Dome: reaching <c>GaleReach</c> x as far, and the shove; Crystal Dome: <c>CrystalCap</c> x, and frost armour on
        /// every hero it wraps; Dust Dome: a cloud of dust and the creatures sapped; Blizzard Dome: frost and the shove.</summary>
        public static float DomeShove = 280f, StoneCap = 1.5f, DomeBuild = 40f, FortressCap = 2.2f, FortressTime = 1.5f, GaleReach = 1.6f, CrystalCap = 1.6f;
        /// <summary>The barrier: Wind Ward (a tailwind <c>TailwindSeconds</c>), Stone Skin (<c>StoneWard</c> x), Frost Ward (frost armour while it
        /// lasts). Bulwark Ward: <c>BulwarkWard</c> x; Glacial Ward: <c>GlacialWard</c> x and a Frost Barrier; Twin Gale: on the friend and her
        /// both, with a tailwind; Permafrost Ward: <c>PermafrostWard</c> x and frost armour; Sandstorm Ward: a cloud of dust about the one it wraps;
        /// Winter Ward: frost armour and a tailwind.</summary>
        public static float TailwindSeconds = 3f, StoneWard = 1.5f, BulwarkWard = 2.5f, GlacialWard = 1.3f, PermafrostWard = 1.8f, FrostWardSeconds = 30f;
    }

    /// <summary>
    /// Dual charges (<c>Player/Player.Duals.cs</c>, <c>Combat/ElementalFx.cs</c>): up to <c>MaxCharges</c> elements wait readied (the oldest
    /// goes when another is pressed past that); the Swordsman's Hard Swing and the Elementalist's attack, dodge and Burst buttons spend two at
    /// once for the pair's own ability on that button (two of one element: its great version). Damage is a multiple of the hero's blow (the Swordsman) or flat (the Elementalist),
    /// both scaled by the damage multiplier; each rests its ability for its own time.
    /// </summary>
    public static class Dual
    {
        public static int MaxCharges = 2;
        // the Swordsman (Hard Swing)
        /// <summary>Earthsplitter (earth+earth): a fissure runs along the floor <c>SplitRange</c> px at <c>SplitSpeed</c>, tossing up and stunning what it passes.</summary>
        public static float SplitRest = 14f, SplitDamage = 2.2f, SplitRange = 340f, SplitSpeed = 380f, SplitToss = 340f;
        /// <summary>It gathers <c>SplitWindup</c> x a Hard Swing's wind-up, the ground trembling; what the crack strikes is held <c>SplitHitStop</c> s.</summary>
        public static float SplitWindup = 3f, SplitHitStop = 0.16f;
        /// <summary>Inferno Cleave (fire+fire): a wall of fire stands where the blade lands for <c>WallSeconds</c>, burning creatures in it
        /// (<c>WallBuild</c> fire every <c>WallTick</c> s) and the shots that fly into it.</summary>
        public static float WallRest = 13f, WallDamage = 2f, WallSeconds = 4.5f, WallHeight = 84f, WallHalf = 14f, WallTick = 0.4f, WallBuild = 22f, WallTickDamage = 0.18f;
        /// <summary>Glacier Cleaver (frost+frost): the Ice Axe's swing, but the head that grows out of the blade is vast and ornate, grows slower and
        /// comes down later (reach, arc, wind-up multiples of a Hard Swing's), its blow <c>AxeDamage</c> x a Hard Swing and a full frost build-up
        /// twice over; the head lands and shatters what the swing froze.</summary>
        public static float AxeRest = 16f, AxeDamage = 4f, AxeReach = 1.1f, AxeArc = 1.25f, AxeWindup = 3.4f;
        /// <summary>Frost Heave (earth+frost): the blade is driven into the floor and the ground bursts up in ice under the nearest creature
        /// within <c>HeaveRange</c> px after <c>HeaveWarn</c> s: <c>HeaveDamage</c>, full frost, tossed up.</summary>
        public static float HeaveRest = 14f, HeaveDamage = 4.5f, HeaveRange = 280f, HeaveWarn = 0.35f, HeaveHalf = 30f, HeaveHeight = 150f, HeaveToss = 420f;
        /// <summary>Magma Rend (earth+fire): the floor splits into a pool of magma <c>PoolHalf</c> px either side, for <c>PoolSeconds</c>:
        /// creatures in it are mired (sapped, slowed) and burn.</summary>
        public static float PoolRest = 15f, PoolDamage = 1.6f, PoolSeconds = 5.5f, PoolHalf = 70f, PoolTick = 0.5f, PoolBuild = 20f, PoolTickDamage = 0.15f, PoolSlow = 0.45f;
        /// <summary>Thermal Shock (fire+frost): a full build-up of whichever of fire and frost the creature holds more of; then frozen solid it
        /// shatters, burning it bursts.</summary>
        public static float ShockRest = 12f, ShockDamage = 2.2f;
        /// <summary>Breaking Quake (earth+fire): gathering <c>QuakeWindup</c> x a Hard Swing's wind-up, the blade (<c>QuakeDamage</c>) is driven
        /// into the floor and cracks full of magma tear out <c>QuakeHalf</c> px either way: <c>QuakeCrackDamage</c> and a full fire build-up to
        /// what stands over them; they close after <c>QuakeSeconds</c>.</summary>
        public static float QuakeRest = 15f, QuakeDamage = 1.8f, QuakeWindup = 2f, QuakeHalf = 120f, QuakeSeconds = 1.6f, QuakeCrackDamage = 1.6f;
        // the Elementalist: two charges on the attack (bolt), the dodge (movement) or the Burst button (fixed range)
        /// <summary>Meteorite (fire+fire, bolt): falls on the thickest knot of creatures, smashing every platform it touches; <c>MeteorDamage</c> and
        /// full fire within <c>MeteorRadius</c>. Cryometeor (frost+frost, bolt): the same fall, but it bursts in frost (two full build-ups: frozen solid)
        /// and leaves the ground frozen.</summary>
        public static float MeteorRest = 15f, MeteorDamage = 46f, MeteorRadius = 92f, MeteorSpeed = 560f, MeteorHeight = 340f, CryoDamage = 30f;
        /// <summary>Fire Barrage / Frost Barrage (fire+wind / frost+wind, bolt): a volley of <c>BarrageBolts</c> bolts loosed <c>BarrageEvery</c> s apart,
        /// fanned <c>BarrageSpread</c> degrees, each <c>BarrageDamage</c> x a bolt.</summary>
        public static float BarrageRest = 10f, BarrageEvery = 0.07f, BarrageSpread = 14f, BarrageDamage = 0.6f;
        public static int BarrageBolts = 7;
        /// <summary>Strong Gust (wind+wind, bolt): a little whirlwind races along the floor at <c>GustSpeed</c> for <c>GustSeconds</c>, shoving every
        /// creature it meets back ahead of it (<c>GustShove</c>; bosses barely) and nicking them (<c>GustTick</c> damage every <c>GustEvery</c> s).</summary>
        public static float GustRest = 8f, GustSpeed = 320f, GustSeconds = 1.3f, GustRadius = 22f, GustShove = 380f, GustTick = 2f, GustEvery = 0.2f;
        /// <summary>Flamebreath (fire+fire, movement): fire pours from the staff for <c>BreathSeconds</c> (a cone <c>BreathDegrees</c> wide, <c>BreathRange</c>
        /// px), <c>BreathBuild</c> fire a tick; aimed down, it holds you up.</summary>
        public static float BreathRest = 11f, BreathSeconds = 1.5f, BreathRange = 150f, BreathDegrees = 50f, BreathTick = 0.1f, BreathBuild = 10f, BreathDamage = 1.5f;
        /// <summary>Frost Tower (frost+frost, movement): the caster leaps (<c>TowerJump</c> x a jump) and a pillar of ice rises under them
        /// (<c>TowerHeight</c> px, <c>TowerHalf</c> px each side) to stand and cast from for <c>TowerSeconds</c>. It is solid to creatures and to the
        /// caster's feet (friends pass through it), and creatures within <c>LureRange</c> px go for it instead of a hero; it has <c>TowerHp</c>
        /// health, x the caster's max health over their starting health, and shatters at none. It doesn't attack.</summary>
        public static float TowerRest = 13f, TowerJump = 1.45f, TowerHeight = 72f, TowerHalf = 14f, TowerSeconds = 10f, TowerRise = 0.3f, TowerHp = 20f, LureRange = 240f;
        /// <summary>Fire Barrier (fire+wind, movement): a ring of fire about you for <c>RingSeconds</c> (<c>RingRadius</c> px): creatures that touch it
        /// burn and are thrown back, shots that cross it burn up.</summary>
        public static float RingRest = 12f, RingSeconds = 5f, RingRadius = 48f, RingTick = 0.25f, RingBuild = 20f, RingDamage = 2f, RingPush = 260f;
        /// <summary>Frost Barrier (frost+wind, movement): frost armour on the caster: the next melee strike they take fills the attacker with
        /// <c>WardBuild</c> frost (waiting up to <c>WardSeconds</c>).</summary>
        public static float BarrierRest = 10f, WardSeconds = 30f, WardBuild = 100f;
        /// <summary>Rain of Fire / Hailstorm (fire+fire / frost+frost, fixed range): for <c>RainSeconds</c> fire (or hail) falls over the spot
        /// (<c>RainHalf</c> px each way), a drop every <c>RainEvery</c> s; each fireball bursts (<c>RainDamage</c>, <c>RainBuild</c> fire), each hailstone
        /// strikes (<c>HailDamage</c>, <c>HailBuild</c> frost) and beats flying creatures down.</summary>
        public static float RainRest = 14f, RainSeconds = 3f, RainHalf = 76f, RainEvery = 0.09f, RainDamage = 5f, RainBuild = 25f, RainBurst = 22f;
        public static float HailDamage = 4f, HailBuild = 12f, HailBeat = 160f;
        /// <summary>Fire Storm / Blizzard (fire+wind / frost+wind, fixed range): the storms, fanned by the wind: they pull light creatures in
        /// (<c>StormPull</c>) and swallow shots.</summary>
        public static float StormRest = 13f, StormPull = 45f;
        /// <summary>Tornado (wind+wind, fixed range): runs along the floor <c>TornadoSpeed</c> for <c>TornadoSeconds</c>, carrying light creatures in it up and
        /// along (<c>TornadoTick</c> damage a tick), swallowing creatures' shots, and flinging what it carries at the end.</summary>
        public static float TornadoRest = 13f, TornadoSpeed = 120f, TornadoSeconds = 4f, TornadoRadius = 40f, TornadoTick = 3f, TornadoFling = 520f;
        /// <summary>Frostfire Lance (fire+frost, fixed range): a beam through every creature in a line <c>LanceRange</c> px (<c>LanceDamage</c>, half a build-up of
        /// each); a creature already burning or chilled takes <c>LanceBonus</c> x.</summary>
        public static float LanceRest = 11f, LanceDamage = 22f, LanceRange = 440f, LanceBonus = 1.8f, LanceBuild = 50f;
    }

    /// <summary>The Nest's hornet hive (<c>Enemies/Hive.cs</c>): a tough paper nest hung from a room's ceiling. It keeps <c>Keep</c> hornets about it, and sends out a new one
    /// <c>Respawn</c> s (give or take <c>Jitter</c>) after one is lost; destroying it leaves a chest of upgrades. <c>Chance</c>: of a Nest level having one.</summary>
    public static class Hive
    {
        public static float Hp = 130, Respawn = 4f, Jitter = 0.4f, Chance = 0.85f;
        public static int Keep = 3, Xp = 14;
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
        /// <summary>A guardian skeleton (the Barrow King and its kin): every SpikeCooldown s, with a hero within SpikeRange px, it marks the
        /// ground under every hero near; SpikeWarn s later bone spikes burst up there, SpikeDamage to anyone within SpikeRadius px.</summary>
        public static float SpikeCooldown = 8f, SpikeWarn = 0.9f, SpikeRange = 380f, SpikeRadius = 26f, SpikeDamage = 14f;
    }

    public static class Sporeling
    {
        public static float Hp = 16, Contact = 4, WalkSpeed = 46, PuffWindup = 0.5f, PuffCooldown = 3.5f, CloudDamage = 4, CloudTime = 2.6f, CloudRadius = 36;
        public static int Xp = 4;
    }

    /// <summary>
    /// The Sulphur Springs: gas vents (rest, hiss, then blow a puff of gas every PuffEvery seconds for VentBlow), the clouds they leave
    /// (poisoning whoever stands in them: PoisonShare of max health, plus a depth-scaled flat part, over PoisonSeconds, every TickEvery),
    /// the blast when a fire touches gas (BlastShare of max health, then BlastBurn of burning), and the creatures of the place.
    /// </summary>
    public static class Sulphur
    {
        public static float VentIdle = 5.5f, VentWarn = 1.4f, VentBlow = 2.6f, PuffEvery = 0.5f;
        public static float CloudRadius = 38f, CloudLife = 9f, CloudRise = 4f, TickEvery = 0.6f;
        public static float PoisonShare = 0.03f, PoisonFlat = 1.5f, PoisonSeconds = 5f;
        /// <summary>The springs' water is acid: whoever is in it takes AcidShare of maximum health and AcidFlat (depth-scaled) every AcidTick seconds. The air in the lowest ToxicCells above the water is toxic: it drains breath as water does (Drowned Lungs, a bubble, or a body that doesn't breathe, are proof against it).</summary>
        public static float AcidShare = 0.05f, AcidFlat = 2.5f, AcidTick = 0.5f;
        public static float ToxicCells = 16f;
        public static float BlastShare = 0.12f, BlastFlat = 6f, BlastBurn = 0.06f, BlastRadiusMult = 1.2f, ChainDelay = 0.14f;
        /// <summary>The gasbag: drifts to hover over you and vents a cloud; bursts into one, and goes up in a ball of fire if lit.</summary>
        public static class Gasbag { public static float Hp = 22, Speed = 36, VentWindup = 0.9f, VentCooldown = 5f, CloudScale = 0.9f, PopScale = 1.4f; public static int Xp = 7; }
        /// <summary>The brimstone worm: lies hid in a vent's mouth, rises when you come near and strikes, then sinks back.</summary>
        public static class Worm { public static float Hp = 44, Bite = 15, Trigger = 110, Reach = 92, Rise = 0.5f, Strike = 0.28f, Up = 2.8f, Sink = 0.5f, Rest = 2.2f; public static int Xp = 9; }
        /// <summary>The acid newt: spits globs of acid that leave a pool that burns for PuddleLife seconds.</summary>
        public static class Newt { public static float Hp = 34, Contact = 7, Speed = 58, SpitDamage = 8, SpitCooldown = 3.2f, SpitWindup = 0.55f, PuddleDamage = 3, PuddleLife = 5.5f; public static int Xp = 7; }
        /// <summary>The vent colossus: exhales great clouds around the chamber, and when it is lit the gas goes up around it.</summary>
        public static class Colossus { public static float ExhaleCooldown = 8f, ExhaleWindup = 1.1f, Clouds = 4; }
    }

    /// <summary>
    /// The Clockwork Deep. Pistons hang from the roof of a gallery and slam to the floor on a cycle (Rest, then Warn: the housing shudders and
    /// steams, then the Fall, the Dwell on the floor, and the Rise), striking whoever is under the head (a share of maximum health, and a
    /// stun). Belts carry whatever stands on them at BeltSpeed. Saws run back and forth on rails at SawSpeed. Lifts rise and fall between the
    /// floors at LiftSpeed, resting LiftRest at each end. Steam jets (the boilers', the guardian's) scald whoever is in them.
    /// </summary>
    public static class Clockwork
    {
        public static float PistonRest = 2.8f, PistonWarn = 0.9f, PistonFall = 0.1f, PistonDwell = 0.6f, PistonRise = 1.0f, PistonShare = 0.1f, PistonFlat = 8f, PistonStun = 0.7f;
        public static float BeltSpeed = 68f;
        public static float SawSpeed = 70f, SawRadius = 13f, SawShare = 0.07f, SawFlat = 5f;
        public static float LiftSpeed = 46f, LiftRest = 1.6f, LiftHalfWidth = 20f;
        public static float SteamShare = 0.03f, SteamFlat = 2.5f, SteamTick = 0.3f, SteamPush = 70f;
        /// <summary>The automaton: a clockwork soldier with a heavy swing. Its spring winds as it works; wound tight it stops to be rewound (taking more from blows).</summary>
        public static class Automaton { public static float Hp = 70, Contact = 9, Speed = 46, Swing = 15, Windup = 0.8f, Cooldown = 2.4f, Reach = 46, Wind = 18f, Rewind = 1.7f, Rewound = 1.5f; public static int Xp = 11; }
        /// <summary>The boiler: builds pressure (Charge), then vents a jet of steam at you for Jet seconds, then cools.</summary>
        public static class Boiler { public static float Hp = 80, Charge = 3.2f, Jet = 1.4f, Cool = 2.0f, Length = 150f, Xp = 12; }
        /// <summary>The cogwheel: a toothed iron wheel that rolls at you, revs, and drives through.</summary>
        public static class Cogwheel { public static float Hp = 36, Contact = 10, Roll = 105, Rev = 0.55f, Dash = 300, DashTime = 0.45f, Cooldown = 2.2f; public static int Xp = 8; }
        /// <summary>The engine-warden: the guardian. Every few seconds steam erupts in columns across its hall, each shown by a glow a moment before.</summary>
        public static class Warden { public static float Cooldown = 7.5f, Telegraph = 1.0f, Columns = 4, ColumnLife = 1.4f; }
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
        /// <summary>Walker-carved caves (and the room caves) are this much taller than drawn, with more tunnels to fill it.</summary>
        public static float HeightScale = 1.3f;
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
        /// <summary>How many repairs (a staircase of ledges out of a pit or up to the guardian) a cave gets.</summary>
        public static int RepairBudget = 40;
        /// <summary>Ledge staircases through tall open spaces: the chance each next ledge up is
        /// placed, at the water line and at the roof (in between it blends). High at the bottom,
        /// sparse at the top, so the heights are harder (not impossible) to reach.</summary>
        public static float PlatformDensityBottom = 0.85f, PlatformDensityTop = 0.17f;
        /// <summary>Minimum sideways gap (cells) between ledges at similar heights, so you can
        /// jump between them without hitting your head.</summary>
        public static float PlatformGapCells = 2.5f;
        /// <summary>A high cavern keeps ledges only this many steps (three cells each) up from its floor; above, open air.</summary>
        public static int CavernSteps = 2;
        /// <summary>In the tall caves (130 cells and more) the chance of a ledge chain carrying on near the top is the biome's times this.</summary>
        public static float TallCaveTopPlatforms = 0.6f;
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

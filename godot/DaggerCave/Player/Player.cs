using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

public struct PlayerInput
{
    public Vector2 Move;        // -1..1 each axis; up is negative Y
    public Vector2 Aim;         // normalized aim direction (zero = use facing)
    /// <summary>With the mouse, how far away the pointer is (0: unknown, a controller's aim): where a blizzard lands.</summary>
    public float AimDist;
    public bool Jump, JumpHeld, Attack, Ability, Ability2, Dodge, Potion, Interact, Rope, Support;
    /// <summary>An element's button pressed this frame (1-3; 0: none): see <see cref="Elements"/>.</summary>
    public int Infuse;
    /// <summary>The rope button held down (a rope pays out while it is).</summary>
    public bool RopeHeld;
    /// <summary>The aim is a real one (the mouse, the right stick, or the move keys pushed), not just the way you face.</summary>
    public bool AimGiven;
    /// <summary>The attack held down (or the right stick pushed): the attack repeats as fast as it can.</summary>
    public bool AttackHeld;
    /// <summary>The attack is the right stick pushed over, not the button (for the Rogue it never recalls the daggers).</summary>
    public bool StickAttack;
    /// <summary>A deliberate push up (not while running sideways): it also takes you down an exit.</summary>
    public bool Up;
    /// <summary>The interact button held (reviving a fallen friend takes a moment).</summary>
    public bool InteractHeld;
    public bool GuardHeld;      // dodge button held (the warden's shield)
    public Vector2 GuardAim;    // right stick, else the mouse (keyboard + mouse), else zero = facing
    /// <summary>The right stick is pushed: the warden's shield goes up that way, no button needed.</summary>
    public bool StickGuard;
}

/// <summary>
/// The hero. This file is the body every hero shares: ground movement with coyote time, jump
/// buffering and variable jump height, free swimming with a breath meter, hazards, health,
/// potions and experience, and the buffering of presses. What each hero fights with lives in
/// the other Player.*.cs files: the blade (Swordsman and Warden), the Swordsman's dodge, charged
/// strike and heaving swing, the Warden's shield, Guarded Charge and shield bash, the Vitalist's
/// drain, hex, heal and rupture, and the Elementalist's bolts, updraft, blizzard and snap. Every
/// tunable that upgrades touch lives in <see cref="PlayerStats"/>.
/// </summary>
public partial class Player : CharacterBody2D
{
    // Tunables live in Core/Tuning.cs (Tune.Hero). Gravity and jump speed are both scaled by
    // Floatiness (g*k, v*sqrt(k)) so jump height is unchanged while the arc gets floatier.
    private static float Gravity => Tune.Hero.Gravity * Tune.Hero.Floatiness;
    private static float MaxFall => Tune.Hero.MaxFallSpeed;
    private static float RunSpeed => Tune.Hero.RunSpeed;
    /// <summary>The speed a full-rate run animation was made for (the run speed before it was cut by a fifth).</summary>
    private const float RunAnimRef = 170f;
    private static float BaseJumpV => Tune.Hero.JumpVelocity * MathF.Sqrt(Tune.Hero.Floatiness);
    private static float SwimSpeedBase => Tune.Hero.SwimSpeed;

    public PlayerStats Stats = new(G.Hero);
    public HeroKind Hero => Stats.Hero;
    private bool IsWarden => Stats.Hero == HeroKind.Warden;
    private bool IsSwordsman => Stats.Hero == HeroKind.Swordsman;
    private bool IsVitalist => Stats.Hero == HeroKind.Vitalist;

    public float Hp = Tune.Hero.StartHp;
    public float Breath = Tune.Hero.BreathSeconds;
    public int Level = 1;
    public int Xp;
    /// <summary>Milestone picks waiting to be offered (every few levels).</summary>
    public int PendingMilestones;
    /// <summary>Potions carried (drink with Q / Y).</summary>
    public int Potions = 1;
    /// <summary>Keys carried: any opens any vault's gate (up to Tune.Vault.MaxKeys; they go on down with you).</summary>
    public int Keys;
    /// <summary>Gold carried (found in the dead ends; spent with a merchant).</summary>
    public int Gold;
    /// <summary>Gold spent with the merchant this run.</summary>
    public int GoldSpent;
    /// <summary>Choices of cards this hero may still have dealt again this run (the Path's Second Thoughts, and tokens from the merchant).</summary>
    public int Rerolls;
    /// <summary>Relic trades at the merchant's stand left this run.</summary>
    public int Trades;
    /// <summary>Set each frame while stuck in a web.</summary>
    public float WebbedT;
    private float _indignT;
    private bool _indignOn;
    private const float IndignationBonus = 0.3f, IndignationSeconds = 4f;
    private void StartIndignation()
    {
        _indignT = IndignationSeconds;
        if (_indignOn) return;
        _indignOn = true;
        Stats.DamageMult += IndignationBonus;
        G.Fx?.Text(GlobalPosition + new Vector2(0, -28), "INDIGNANT", new Color(1f, 0.6f, 0.3f), 9, 0.7f);
    }
    private void TickIndignation(float dt)
    {
        if (!_indignOn) return;
        if ((_indignT -= dt) > 0) return;
        _indignOn = false;
        Stats.DamageMult -= IndignationBonus;
    }
    private float _hotLeft, _hotRate, _lavaTick, _xpFrac, _slideDust;
    public int Kills;
    public bool Dead;
    public bool InWater, HeadUnder;
    /// <summary>A leap out of the water is ready: the swimmer has gone back under (or stopped coming down) since the last one.</summary>
    private bool _leapReady = true;
    private float _leapT;
    public float Facing = 1;

    public Func<PlayerInput> InputOverride;
    private bool _swallow;
    /// <summary>The feelings the Delvers' pages have left in this hero this run (until the cave is left).</summary>
    public readonly System.Collections.Generic.List<RunBuff> Buffs = new();
    private CapsuleShape2D _bodyShape;
    private CollisionShape2D _bodyNode;
    private float _knockT;
    /// <summary>A blow's shove carries (the tests, laid out for the old short shove, turn it off).</summary>
    public static bool KnockLock = true;
    /// <summary>Ducking: hold down on the ground. The body is half as tall (to squeeze through low gaps), and you move slowly.</summary>
    public bool Crouching { get; private set; }

    private void UpdateCrouch(PlayerInput inp, bool onFloor)
    {
        bool want = onFloor && inp.Move.Y > 0.6f && !InWater && _rope == null && _dodgeT <= 0 && !Heaving;
        // (no standing up under a low roof)
        if (!want && Crouching && G.Cave.IsSolid(GlobalPosition + new Vector2(0, -17))) want = true;
        if (want == Crouching) return;
        Crouching = want;
        // (the feet stay where they are: the capsule shrinks from the top)
        _bodyShape.Height = want ? 14f : 26f;
        _bodyNode.Position = new Vector2(0, want ? 6f : 0f);
    }
    public SpriteAnimator Anim;
    /// <summary>Another player's hero in an online game: a puppet shown from what their machine
    /// sends (it doesn't simulate or take damage here).</summary>
    public bool IsRemote;

    // animation bookkeeping
    private bool _wallSliding, _jumpedFromGround;
    private string _lastBase = "idle";
    private float _lastAbsVx;

    // timers / state
    private float _coyote, _jumpBuffer, _invuln, _iframes, _drownTick;
    private float _airDashT, _wallJumpLock, _hurtFlash, _bubbleT, _animT;
    private Vector2 _airDashDir;
    private int _airJumps, _airDashes;
    private bool _jumpCutDone, _wasOnFloor;
    private float _pauseT, _pauseNext;
    private float _lastFallSpeed, _freeze, _stuckInRock, _terminalT;
    private int _nullDepth = -1;

    /// <summary>For the tests: what the last landing took.</summary>
    public float LastFallDamage { get; private set; }
    /// <summary>How long it has been falling at the terminal speed.</summary>
    public float TerminalT => _terminalT;

    /// <summary>A hard landing after falling at the terminal speed for long enough: a share of the hero's current health (never the
    /// last of it). Anything that brakes the fall on the way down (a second jump, an updraft, a dash, a rope, the water) starts it afresh.</summary>
    private void TakeFall(float atTerminal)
    {
        LastFallDamage = 0f;
        if (Dead || IsRemote || atTerminal <= Tune.Hero.FallGraceSeconds || Invulnerable) return;
        float k = Math.Clamp((atTerminal - Tune.Hero.FallGraceSeconds) / Tune.Hero.FallRampSeconds, 0f, 1f);
        float dmg = Hp * Mathf.Lerp(Tune.Hero.FallShareMin, Tune.Hero.FallShareMax, k);
        if (dmg < 0.5f) return;
        LastFallDamage = dmg;
        if (k >= 1f && this == G.Player) Achievements.FellHard();
        TakeRawDamage(dmg, "fall");
        G.Fx.TickText(GlobalPosition + new Vector2(0, -22), "-" + Num.Shown(dmg), new Color(1f, 0.6f, 0.45f), 10);
        G.Sfx.Play("hit", GlobalPosition, -4, 0.05f, 0.6f);
        G.Main.Rumble(0.3f + 0.4f * k, 0.15f, 0.2f);
    }

    // presses waiting to fire (see BufferPresses)
    private float _attackBuf, _abilityBuf, _ability2Buf, _dodgeBuf;
    private Vector2 _attackAim, _abilityAim, _ability2Aim;
    private float _abilityAimDist;
    private PlayerInput _dodgeInput;

    public bool Invulnerable => _invuln > 0 || _iframes > 0 || Choosing || (IsRemote && NetInvuln);
    /// <summary>Dodging, dashing, invulnerable, or behind a raised shield (an input for the enemy brains).</summary>
    public bool Guarding => IsRemote ? (_netFlags & HfGuarding) != 0 : IsDodging || IsShieldDashing || Invulnerable || ShieldRaised;
    /// <summary>The hero's ability (charged strike, Guarded Charge, heal) is ready to use.</summary>
    public bool SecondaryReady => IsRemote ? (_netFlags & HfSecondary) != 0 : Stats.Hero switch
    {
        HeroKind.Vitalist => AbilityChargeReady && VitalForce >= HealCost,
        HeroKind.Elementalist => AbilityChargeReady,
        HeroKind.Rogue => DaggersInHand > 0,
        HeroKind.Aegis => AbilityChargeReady,
        HeroKind.ShapeShifter => Shifted ? FormSpecialReady : AbilityChargeReady,
        HeroKind.Automaton => AbilityChargeReady && Fuel >= 1f,
        HeroKind.Swordsman => AbilityChargeReady || Charged > 0,
        _ => AbilityChargeReady,
    };
    public int XpToNext => (int)(Tune.Hero.XpBase + Tune.Hero.XpLinear * Level + Tune.Hero.XpQuadratic * Level * Level);

    public override void _Ready()
    {
        CollisionLayer = G.LayerPlayer;
        CollisionMask = G.LayerTerrain;
        FloorMaxAngle = Mathf.DegToRad(Tune.Cave.WalkableSlopeDegrees);
        FloorSnapLength = 7f;
        SafeMargin = 0.5f;
        _bodyShape = new CapsuleShape2D { Radius = 6.5f, Height = 26f };
        _bodyNode = new CollisionShape2D { Shape = _bodyShape };
        AddChild(_bodyNode);
        ZIndex = 1;
        Anim = SpriteAnimator.Create(SheetName(Stats.Hero));
        Anim.FootOffset = 13f;
        AddChild(Anim);
        Hp = Stats.MaxHp;
        ShieldHp = Stats.ShieldMax;
        Breath = Stats.BreathMax;
        VitalForce = Math.Min(Stats.VitalForceMax, Tune.Vitalist.VitalForceStart);
        Alimus = Stats.AlimusMax;
        // (the Automaton starts half charged, plus whatever more its Capacitor holds)
        Fuel = Math.Min(Stats.FuelMax, Tune.AutomatonHero.FuelStart + Math.Max(0f, Stats.FuelMax - Tune.AutomatonHero.FuelMax));
    }

    /// <summary>Each hero's sprite sheet and 3D design, by name.</summary>
    public static string SheetName(HeroKind h) => h switch
    {
        HeroKind.Warden => "warden",
        HeroKind.Vitalist => "vitalist",
        HeroKind.Elementalist => "elementalist",
        HeroKind.Rogue => "rogue",
        HeroKind.Aegis => "aegis",
        HeroKind.ShapeShifter => "shapeshifter",
        HeroKind.Automaton => "automaton_hero",
        _ => "swordsman",
    };

    public override void _EnterTree() { if (!G.Players.Contains(this)) G.Players.Add(this); }
    public override void _ExitTree()
    {
        G.Players.Remove(this);
        // (the stats go on to the next level: a boost under way doesn't)
        EndOverPressure();
    }

    public void SyncCharges()
    {
        if (_dodgeCd.Length != Stats.DodgeCharges) Array.Resize(ref _dodgeCd, Stats.DodgeCharges);
        if (_abilityCd.Length != Stats.AbilityCharges) Array.Resize(ref _abilityCd, Stats.AbilityCharges);
    }

    /// <summary>Heals (pickups, potions, the vitalist). The Warden's shield takes a share too.</summary>
    public void Heal(float amount)
    {
        if (Dead || amount <= 0) return;
        // online, another player's hero is healed by their own game
        if (IsRemote) { NetSync.HealRemote(this, amount); return; }
        NetSync.Scope++;
        try { HealHere(amount); }
        finally { NetSync.Scope--; }
    }

    private void HealHere(float amount)
    {
        amount *= Stats.HealingTakenMult * Tune.Hero.HealingMult;
        // (the Automaton can't truly be healed: what it receives becomes energy for Self-Repair)
        if (Stats.SelfRepair) { HealAsFuel(amount); return; }
        float before = Hp;
        Hp = Math.Min(Stats.MaxHp, Hp + amount);
        RunMetrics.Healed(NetSync.Giver, Hp - before, amount - (Hp - before));
        int healed = Num.Delta(before, Hp);
        if (healed > 0) G.Fx?.Text(GlobalPosition + new Vector2(0, -22), "+" + healed, HealColorLight, 14);
        if (Hp > before) Stats.On.RaiseHeal(this, Hp - before);
        if (IsWarden) MendShield(amount * Tune.Warden.HealToShield);
    }

    /// <summary>A gulp of air (the vents' bubbles).</summary>
    public void AddBreath(float seconds) { if (_drownT <= 0) Breath = Math.Min(Stats.BreathMax, Breath + seconds); }

    public void AddXp(int amount)
    {
        _xpFrac += amount * Meta.XpMult * Stats.XpMult;
        int whole = (int)_xpFrac;
        _xpFrac -= whole;
        Xp += whole;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            LevelUpOnce();
        }
    }

    /// <summary>When the latest milestone point was earned (the HUD flashes its notice).</summary>
    public float MilestoneAt = -100f;

    private void LevelUpOnce()
    {
        Level++;
        Progression.AutoLevel(this);
        if (Level % Meta.MilestoneEvery == 0)
        {
            PendingMilestones++;
            MilestoneAt = Time.GetTicksMsec() / 1000f;
            if (this == G.Player) G.Sfx?.Play("milestone", null, -2, 0, 1f);
        }
        if (Stats.LevelHeal) HealHere(Stats.MaxHp * Tune.Relics.LevelHealShare);
        Stats.On.RaiseLevel(this);
    }

    /// <summary>Levels gained at once (a relic's gift), each with its usual rewards.</summary>
    public void GainLevels(int n)
    {
        for (int k = 0; k < n; k++) { Xp = 0; LevelUpOnce(); }
    }

    /// <summary>Potions this hero can carry (the ember trees' and a relic's).</summary>
    public int MaxPotions => Meta.MaxPotions + Stats.ExtraPotions;

    /// <summary>Drinks a potion: part of the heal at once, the rest over the next seconds.</summary>
    public bool DrinkPotion()
    {
        if (Dead || Potions <= 0 || Hp >= Stats.MaxHp - 0.5f) return false;
        Potions--;
        float now = Stats.MaxHp * Meta.PotionHealNow * Stats.PotionPower;
        Heal(now);
        PotionHexes(now);
        _hotRate = Stats.MaxHp * Meta.PotionHealOverTime / Meta.PotionHotSeconds;
        _hotLeft = Meta.PotionHotSeconds;
        G.Sfx.Play("heal", GlobalPosition, 0, 0, 0.8f);
        G.Sfx.Play("bubble", GlobalPosition, -4, 0, 0.6f);
        G.Fx.Flash(GlobalPosition, 26, HealColor);
        G.Fx.Ring(GlobalPosition, 22, HealColorLight, 0.3f, FxLayer.RingStyle.Wave);
        for (int k = 0; k < 10; k++) G.Fx.Ember(GlobalPosition + G.RandDir() * 10, HealColor);
        if (!IsRemote) G.Fx.ScreenFlash(new Color(1f, 0.35f, 0.5f), 0.25f);
        Anim.Flash(0.6f);
        return true;
    }

    /// <summary>The Ember Grid's potion hexes as a potion is drunk: Shared Draught heals friends close by, Volatile Brew bursts.</summary>
    private void PotionHexes(float healed)
    {
        if (HexGrid.Owns("pot_shared"))
            foreach (var f in G.Players)
                if (f != this && !f.Dead && f.GlobalPosition.DistanceTo(GlobalPosition) < Tune.Grid.SharedDraughtRange)
                {
                    f.Heal(healed * Tune.Grid.SharedDraughtShare);
                    G.Fx.Ring(f.GlobalPosition, 16, HealColorLight, 0.3f, FxLayer.RingStyle.Wave);
                }
        if (HexGrid.Owns("pot_volatile"))
        {
            float total = 0;
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || !e.CanBeHit) continue;
                var to = e.GlobalPosition - GlobalPosition;
                if (to.Length() > Tune.Grid.VolatileRadius + e.HitRadius || !G.Cave.LineClear(GlobalPosition, e.GlobalPosition)) continue;
                total += Math.Max(0f, e.Hurt(Tune.Grid.VolatileDamage, to.Normalized() * 180f, e.GlobalPosition, DamageKind.Raw));
            }
            if (total > 0) OnDealtDamage(total);
            G.Fx.Ring(GlobalPosition, Tune.Grid.VolatileRadius, new Color(1f, 0.45f, 0.6f), 0.35f, FxLayer.RingStyle.Wave);
            G.Fx.AddShake(4);
        }
    }

    /// <summary>True while a potion's heal over time is still running.</summary>
    public bool Mending => _hotLeft > 0;

    private PlayerInput ReadInput()
    {
        // (frozen solid or stunned: nothing gets through)
        if (_frozenT > 0 || _stunT > 0) return default;
        var inp = InputOverride != null ? InputOverride() : ReadLocalInput(this);
        // (wrapped in web: every press is a struggle against it, and nothing else)
        if (_webT > 0) { Struggle(inp); return default; }
        // (taking aim with the narrow draft holds you still: either stick, or the move keys, only point it; no bolts, jumps or swings meanwhile)
        if (IsElementalist && _draftAiming)
        {
            inp.Move = Vector2.Zero;
            inp.Jump = inp.JumpHeld = false;
            inp.Attack = inp.AttackHeld = inp.StickAttack = false;
            inp.Ability = inp.Ability2 = inp.Support = false;
        }
        return inp;
    }

    /// <summary>
    /// The movement stick (or keys) as one vector, free in every direction. Reading each direction's action on its own gives the stick
    /// a cross-shaped dead zone (a push 20 degrees off the horizontal registers as straight along it), which, with the keys, made
    /// movement and aim fall into the eight directions 45 degrees apart. This reads the raw strengths, puts one round dead zone on the pair
    /// (<see cref="Tune.Hero.StickDeadzone"/>, rescaled so a gentle push is a gentle move), and keeps a key's full strength on each axis
    /// (so a keyboard diagonal still runs at full speed).
    /// </summary>
    public static Vector2 ReadMove()
    {
        var v = new Vector2(Input.GetActionRawStrength("move_right") - Input.GetActionRawStrength("move_left"),
                            Input.GetActionRawStrength("move_down") - Input.GetActionRawStrength("move_up"));
        return ShapeStick(v);
    }

    /// <summary>A round dead zone on a raw stick vector (see <see cref="ReadMove"/>); also what the tests use.</summary>
    public static Vector2 ShapeStick(Vector2 v)
    {
        float len = v.Length();
        float dz = Tune.Hero.StickDeadzone;
        if (len <= dz) return Vector2.Zero;
        v *= (len - dz) / (1f - dz) / len;
        return new Vector2(Math.Clamp(v.X, -1f, 1f), Math.Clamp(v.Y, -1f, 1f));
    }

    /// <summary>
    /// This machine's controls, for the hero <paramref name="p"/>: keyboard and mouse or a
    /// controller, as bound in the settings. Aim: the right stick, else (on a controller) the left
    /// stick; with keyboard and mouse, the mouse pointer while the mouse is in use, else the way
    /// you move (see <see cref="GameSettings.AimFromMouse"/>).
    /// </summary>
    public static PlayerInput ReadLocalInput(Player p)
    {
        // (a click that picked a card or pressed a button belongs to the menu: the buttons it
        // pressed stay silent until they are let go)
        if (G.Main.MenuOpen) { p._swallow = true; return default; }
        var inp = new PlayerInput
        {
            Move = ReadMove(),
            Jump = Input.IsActionJustPressed("jump"),
            JumpHeld = Input.IsActionPressed("jump"),
            Dodge = Input.IsActionJustPressed("dodge"),
            GuardHeld = Input.IsActionPressed("dodge"),
            Potion = Input.IsActionJustPressed("potion"),
            Attack = Input.IsActionJustPressed("attack"),
            AttackHeld = Input.IsActionPressed("attack"),
            Ability = Input.IsActionJustPressed("ability"),
            Ability2 = Input.IsActionJustPressed("ability2"),
            Rope = Input.IsActionJustPressed("rope"),
            RopeHeld = Input.IsActionPressed("rope"),
            Support = Input.IsActionJustPressed("support"),
            InteractHeld = Input.IsActionPressed("interact"),
        };
        inp.Interact = Input.IsActionJustPressed("interact");
        for (int k = 0; k < 3; k++) if (InputMap.HasAction(Elements.Action(k)) && Input.IsActionJustPressed(Elements.Action(k))) inp.Infuse = k + 1;
        if (p._swallow)
        {
            if (Input.IsActionPressed("attack") || Input.IsActionPressed("ability") || Input.IsActionPressed("ability2"))
            {
                inp.Attack = inp.AttackHeld = inp.Ability = inp.Ability2 = inp.Support = false;
            }
            else p._swallow = false;
        }
        // up works for exits too, but only a deliberate push (running past a door on a tilted
        // stick shouldn't take you down); chests and reviving take the interact button itself
        inp.Up = Input.IsActionJustPressed("move_up") && Math.Abs(inp.Move.X) < 0.5f;
        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.RightX), Input.GetJoyAxis(0, JoyAxis.RightY));
        bool stickOn = stick.Length() > 0.35f;
        // the right stick raises the Warden's shield by itself, pointing where it's pushed; for
        // everyone else, pushing it well over attacks that way (again and again while it's held)
        bool warden = p.Stats.Hero == HeroKind.Warden;
        inp.StickGuard = stickOn && warden;
        if (!warden && stick.Length() > 0.5f) { inp.StickAttack = !inp.AttackHeld; inp.AttackHeld = true; }
        var toMouse = (p.GetGlobalMousePosition() - p.GlobalPosition).Normalized();
        bool mouse = !G.Main.UsingPad && GameSettings.AimFromMouse;
        // the shield points wherever a swing would go: right stick, else the left stick on a
        // controller (else your facing), or the mouse
        if (stickOn) inp.GuardAim = stick.Normalized();
        else if (!mouse) inp.GuardAim = inp.Move.Length() > 0.3f ? inp.Move.Normalized() : Vector2.Zero;
        else inp.GuardAim = toMouse;
        if (stickOn) inp.Aim = stick.Normalized();
        else if (!mouse) inp.Aim = inp.Move.Length() > 0.2f ? inp.Move.Normalized() : new Vector2(p.Facing, 0);
        else inp.Aim = toMouse;
        if (mouse && !stickOn) inp.AimDist = (p.GetGlobalMousePosition() - p.GlobalPosition).Length();
        inp.AimGiven = stickOn || mouse || inp.Move.Length() > 0.2f;
        return inp;
    }

    /// <summary>
    /// Presses are remembered for a moment (Tune.Hero.PressBuffer) and fire as soon as they're
    /// allowed. They're read even during a hit-stop, so a combo pressed on the impact follows the
    /// instant the freeze ends instead of being lost.
    /// </summary>
    private void BufferPresses(in PlayerInput inp)
    {
        var aim = inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : new Vector2(Facing, 0);
        if (inp.Attack) { _attackBuf = Tune.Hero.PressBuffer; _attackAim = aim; }
        if (inp.Ability) { _abilityBuf = Tune.Hero.PressBuffer; _abilityAim = aim; _abilityAimDist = inp.AimDist; }
        if (inp.Ability2) { _ability2Buf = Tune.Hero.PressBuffer; _ability2Aim = aim; }
        if (inp.Support) { _supportBuf = Tune.Hero.PressBuffer; _supportAim = aim; }
        if (inp.Dodge) { _dodgeBuf = Tune.Hero.PressBuffer; _dodgeInput = inp; }
        if (inp.Jump) _jumpBuffer = Tune.Hero.JumpBuffer + Stats.BufferBonus;
        if (inp.Potion) DrinkPotion();
        if (inp.Infuse > 0 && Elemental) TryInfuse(inp.Infuse - 1);
        if (inp.Interact || inp.Up) TryInteract(inp.Interact);
    }

    /// <summary>
    /// Opens the chest the hero stands at (the interact button), or a vault's gate with a key, or
    /// walks into an exit (interact, or a deliberate push up). Nothing happens by just walking past.
    /// </summary>
    private void TryInteract(bool button)
    {
        if (Dead || G.Main.MenuOpen) return;
        if (button && Chest.At(GlobalPosition) is Chest chest) { chest.Interact(); return; }
        if (button && Rubble.At(GlobalPosition) is Rubble rubble) { rubble.Heave(); return; }
        if (button && HeroCage.At(GlobalPosition) is HeroCage cage) { cage.Interact(); return; }
        if (button && Merchant.At(GlobalPosition) is Merchant merchant) { merchant.Interact(); return; }
        if (button && GuildLamp.At(GlobalPosition) is GuildLamp lamp) { lamp.Read(); return; }
        if (button && VaultGate.At(GlobalPosition) is VaultGate gate) { gate.TryOpen(this); return; }
        // only standing at the door (or swimming): not mid-jump, or while an attack is under way
        if ((!IsOnFloor() && !InWater) || IsSwinging) return;
        foreach (var n in G.World.GetChildren())
            if (n is Portal portal && portal.Reaches(GlobalPosition) && (button || !portal.Outside)) { portal.Enter(); return; }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsRemote) { PuppetTick((float)delta); return; }
        // (what this hero does, the other games see: its effects and sounds are sent)
        NetSync.Scope++;
        try { Simulate((float)delta); }
        finally { NetSync.Scope--; }
    }

    private void Simulate(float dt)
    {
        _animT += dt;
        var cave = G.Cave;
        if (Dead) { Anim.TimeMult = 1; Anim.Position = Vector2.Zero; DeadPhysics(cave, dt); return; }
        var inp = ReadInput();
        BufferPresses(inp);
        TickRevive(inp, dt);
        // hit-stop: only the hero (and whoever it traded blows with) freezes, the world carries on
        if (_freeze > 0)
        {
            _freeze -= dt;
            Anim.TimeMult = 0;
            Anim.Position = new Vector2(G.Range(-1.2f, 1.2f), G.Range(-0.6f, 0.6f));
            QueueRedraw();
            return;
        }
        // melee pause: the beat after a blow that landed, once its hit-stop is over (the hero holds the pose, perfectly still)
        if (_pauseNext > 0f) { _pauseT = Math.Max(_pauseT, _pauseNext); _pauseNext = 0f; HitPausesRun++; _pausePos = GlobalPosition; PauseDrift = 0f; }
        if (_pauseT > 0f)
        {
            PauseDrift = Math.Max(PauseDrift, GlobalPosition.DistanceTo(_pausePos));
            _pauseT -= dt;
            Anim.TimeMult = 0;
            Anim.Position = Vector2.Zero;
            QueueRedraw();
            return;
        }
        Anim.TimeMult = 1;
        Anim.Position = Vector2.Zero;

        TickTimers(dt);
        TickRope(inp, dt);
        if (IsElementalist) TickDraftAim(inp);
        TickSupport(dt);
        TickHold(dt);
        Stats.On.RaiseTick(this, dt);
        TickUpgrades(dt);
        TickElemental(dt);
        switch (Stats.Hero)
        {
            case HeroKind.Warden: UpdateShield(inp, dt); break;
            case HeroKind.Vitalist: TickVitalist(dt); break;
            case HeroKind.Elementalist: TickElementalist(dt); break;
            case HeroKind.Rogue: TickRogue(dt); break;
            case HeroKind.Aegis: TickAegis(dt); break;
            case HeroKind.ShapeShifter: TickShifter(dt); break;
            case HeroKind.Automaton: TickAutomaton(dt); break;
            default: TickSwordsman(dt); break;
        }

        bool wasInWater = InWater;
        // (with Magma Skin, lava is swum in like water)
        bool Liquid(Vector2 at) => cave.IsWater(at) || (Stats.MagmaSkin && cave.IsLava(at));
        InWater = Liquid(GlobalPosition + new Vector2(0, 2));
        HeadUnder = Liquid(GlobalPosition + new Vector2(0, -9));
        if (InWater != wasInWater && Math.Abs(Velocity.Y) > 80)
        {
            bool lava = cave.Liquid == DaggerCave.Liquid.Lava;
            // in with a plunge, out with a milder sheet of water
            G.Sfx.Play(lava ? "lava" : InWater ? "splash_in" : "splash_out", GlobalPosition, InWater ? -3 : -5, 0.06f);
            G.Fx.Splash(new Vector2(GlobalPosition.X, cave.WaterY), Math.Clamp(Math.Abs(Velocity.Y) / 500f, 0.2f, 1f), lava ? new Color(1f, 0.55f, 0.15f) : new Color(0.65f, 0.88f, 1f, 0.9f));
            if (InWater && !lava) G.Fx.Bubbles(GlobalPosition, 8);
        }
        Hazards(cave, dt);
        // hitting the water soaks up most of the speed you carried in
        if (InWater && !wasInWater) Velocity *= Tune.Hero.WaterEntryDamp;
        UpdateBreath(dt);
        Unstick(cave, dt);

        if (_dashT <= 0)
        {
            if (inp.Move.X > 0.2f) Facing = 1; else if (inp.Move.X < -0.2f) Facing = -1;
        }

        var v = Velocity;
        bool onFloor = IsOnFloor();
        _wallSliding = false;
        _jumpedFromGround = false;
        UpdateCrouch(inp, onFloor);
        // (the water's surface is no floor: a leap out of the water is the swimmer's own, from in it; see Swim)
        if (InWater && _leapT <= 0f && (HeadUnder || Velocity.Y <= 0f)) _leapReady = true;
        if (onFloor) { _coyote = Tune.Hero.CoyoteTime + Stats.CoyoteBonus; _airJumps = Stats.DoubleJump ? 1 : 0; _airDashes = Stats.AirDash ? 1 : 0; _pogoSpent = false; }

        // the dodge button: the Swordsman rolls, the Vitalist hexes, the Elementalist raises an
        // updraft, the Rogue vanishes (the Warden's raises her shield)
        if (_dodgeBuf > 0 && _heldRope == null && !Holding && DodgeButton(_dodgeInput)) { _dodgeBuf = 0; RunMetrics.Used(this, 3); Stats.On.RaiseDodge(this); }

        // (a Shape Shifter in a creature's form is that creature: it goes where it goes)
        if (Possessed) v = PossessedStep(inp);
        else if (_dodgeT > 0) v = DodgeMotion(v, dt);
        else if (_dashT > 0) v = DashMotion(v, dt);
        else if (_tetherTo != null) v = TetherMotion(v, dt);
        else if (_airDashT > 0)
        {
            v = _airDashDir * Tune.Hero.AirDashSpeed;
            if (Engine.GetPhysicsFrames() % 3 == 0) Afterimage.Spawn(Anim, new Color(0.55f, 0.9f, 1f), 0.2f);
            if (_airDashT - dt <= 0) v *= 0.5f;
        }
        // (holding a rope for the others: you can't move, only stand)
        else if (_heldRope != null) v = Platform(new PlayerInput { Aim = inp.Aim }, v, dt, onFloor);
        else if (_rope != null || WantsRope(inp) && GrabRope())
        {
            v = RopeMotion(inp, v, dt);
            if (_rope != null) _airDashT = 0;
        }
        else if (InWater) v = Stats.Sinks ? SinkingSwim(inp, v, dt, cave) : Swim(inp, v, dt, cave);
        else v = Platform(inp, v, dt, onFloor);
        // (a cast being gathered: slowed to a tenth, walking, swimming and falling alike, and no jumping; the rise or fall it paused is given back when it ends, see ResumeFromHold)
        if (Holding && !Possessed)
        {
            float cap = RunSpeed * Stats.MoveSpeed * Tune.Hold.MoveShare;
            v = new Vector2(Math.Clamp(v.X, -cap, cap), Math.Clamp(v.Y, -cap, MaxFall * Tune.Hold.MoveShare));
            _jumpBuffer = 0;
        }

        if (_airDashT > 0) _airDashT -= dt;

        _lastFallSpeed = v.Y;
        bool nowFloor;
        if (Possessed)
        {
            // the hero stands where the creature does
            GlobalPosition = Ghost.GlobalPosition;
            Velocity = v;
            nowFloor = Ghost.OnGround;
        }
        else
        {
            Velocity = v;
            MoveAndSlide();
            nowFloor = IsOnFloor();
            // (a belt carries whoever stands on it)
            if (Conveyor.All.Count > 0 && nowFloor)
            {
                float belt = Conveyor.DriftAt(GlobalPosition + new Vector2(0, 14));
                if (belt != 0f) MoveAndCollide(new Vector2(belt * (float)GetPhysicsProcessDeltaTime(), 0));
            }
        }
        if (nowFloor && !_wasOnFloor && _lastFallSpeed > 260)
        {
            G.Sfx.Play("land", GlobalPosition, -6);
            G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 3 + (int)(_lastFallSpeed / 120f), 1.2f);
            if (_terminalT > Tune.Hero.FallGraceSeconds) G.Fx.Shockwave(GlobalPosition + new Vector2(0, 13), 26, new Color(1, 1, 1, 0.35f), 0.25f);
            Anim.Once("land", 1);
        }
        // fall damage: the time spent falling at the terminal speed, taken on landing; anything that slows the fall (a second jump,
        // an updraft, a dash, a rope, the water, a creature's form) starts the clock afresh
        if (nowFloor && !_wasOnFloor) TakeFall(_terminalT);
        if (nowFloor || _rope != null || InWater || Possessed || _lastFallSpeed < Tune.Hero.MaxFallSpeed * Tune.Hero.FallTerminalShare) _terminalT = 0f;
        else _terminalT += dt;
        _wasOnFloor = nowFloor;

        // the attack button, then the two ability buttons (each fires once it's allowed); held
        // down, the attack goes again as soon as it can, wherever you aim now
        if (Holding) { }
        else if (_attackBuf > 0 && Primary(_attackAim)) { _attackBuf = 0; RunMetrics.Used(this, 0); }
        else if (inp.AttackHeld && _attackBuf <= 0 && Primary(inp.Aim.LengthSquared() > 0.01f ? inp.Aim.Normalized() : new Vector2(Facing, 0), held: true, stick: inp.StickAttack)) RunMetrics.Used(this, 0);
        if (!Holding && _abilityBuf > 0 && Ability(_abilityAim)) { _abilityBuf = 0; RunMetrics.Used(this, 1); }
        if (!Holding && _ability2Buf > 0 && Ability2(_ability2Aim)) { _ability2Buf = 0; RunMetrics.Used(this, 2); }
        if (!Holding && _supportBuf > 0 && Support(_supportAim)) { _supportBuf = 0; RunMetrics.Used(this, 4); }
        if (_swingT >= 0) UpdateSwing(dt);
        TickBash(dt);

        UpdateAnimation(nowFloor);
        QueueRedraw();
    }

    private float _snagT;
    /// <summary>For the HUD and the 3D model: the weapon is caught in grasping roots.</summary>
    public bool Snagged => _snagT > 0;

    /// <summary>Grasping roots catch the weapon (or staff): no swings, spells or abilities for a moment.</summary>
    public void Snag(float seconds)
    {
        if (Dead) return;
        _snagT = Math.Max(_snagT, seconds);
        _swingT = -1;
        CancelHold();
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "SNAGGED", new Color(0.8f, 0.72f, 0.45f), 10, 0.8f);
        G.Sfx.Play("web", GlobalPosition, -2, 0.1f, 0.6f);
        Anim.Flash(0.25f);
        Anim.FlashColor = new Color(0.7f, 0.6f, 0.35f);
    }

    /// <summary>The attack button: a swing, the Vitalist's drain, or the Elementalist's bolt. True once it fires.
    /// <paramref name="held"/>: the button is being held down (a drain then waits for something to drain).
    /// <paramref name="stick"/>: it is the right stick pushed over (the Rogue's aim, not a recall).</summary>
    private bool Primary(Vector2 aim, bool held = false, bool stick = false) => _snagT <= 0 && Stats.Hero switch
    {
        HeroKind.Vitalist => CastDrain(aim, held),
        HeroKind.Elementalist => Elemental ? ElementMissile(aim, held) : CastBolt(aim, held),
        HeroKind.Rogue => DaggersInHand > 0 ? _tetherTo == null && TrySwing(aim, held) : !stick && TryRecall(fromAttack: true),
        HeroKind.Warden => _dashT <= 0 && _bashT <= 0 && TrySwing(aim, held),
        HeroKind.Aegis => CastWardBolt(aim, held),
        HeroKind.ShapeShifter => Shifted ? FormPrimary(aim, held) : TrySwing(aim, held),
        HeroKind.Automaton => !Bracing && !Pressurising && TrySwing(aim, held),
        _ => !Heaving && (Elemental ? ElementSwing(aim, held) : TrySwing(aim, held)),
    };

    /// <summary>The ability button: charged strike, Guarded Charge, heal or blizzard. True once it fires.</summary>
    private bool Ability(Vector2 aim) => _snagT <= 0 && Stats.Hero switch
    {
        HeroKind.Warden => _bashT <= 0 && TryShieldDash(aim),
        HeroKind.Vitalist => TryHeal(),
        HeroKind.Elementalist => Elemental ? TryStorm(aim, _abilityAimDist) : TryBlizzard(aim, _abilityAimDist),
        HeroKind.Rogue => TryThrow(aim),
        HeroKind.Aegis => TryBarrier(aim),
        HeroKind.ShapeShifter => TryShift(),
        HeroKind.Automaton => TryRepair(),
        _ => Elemental ? TryHilt() : TryCharge(),
    };

    /// <summary>The second ability button: heaving swing, shield bash, rupture or snap. True once it fires.</summary>
    private bool Ability2(Vector2 aim) => _snagT <= 0 && Stats.Hero switch
    {
        HeroKind.Warden => _dashT <= 0 && TryShieldBash(aim),
        HeroKind.Vitalist => TryRupture(aim),
        HeroKind.Elementalist => Elemental ? TryBurst() : TrySnap(),
        HeroKind.Rogue => TryRecall(),
        HeroKind.Aegis => TryBurden(aim),
        HeroKind.ShapeShifter => TrySpecial(aim),
        HeroKind.Automaton => TrySteam(aim),
        _ => Elemental ? TryHardSwing(aim) : TryHeave(aim),
    };

    /// <summary>The dodge button's press: a roll, a hex or an updraft (the Warden's shield reads the button itself).</summary>
    private bool DodgeButton(in PlayerInput inp) => Stats.Hero switch
    {
        HeroKind.Swordsman => TryDodge(inp),
        HeroKind.Vitalist => TryHex(),
        HeroKind.Elementalist => Elemental ? ElementAid(inp) : BeginDraftAim(inp),
        HeroKind.Rogue => TryVanish(),
        HeroKind.Aegis => TryBubble(),
        HeroKind.ShapeShifter => Possessed ? TryRemember() : TryDodge(inp), // (a creature has no dodge roll: its button commits the form to memory)
        HeroKind.Automaton => TryBrace(inp),
        _ => true,
    };

    /// <summary>Chooses the looping clip for the current movement state and triggers transitions.</summary>
    private void UpdateAnimation(bool onFloor)
    {
        var vel = Velocity;
        float avx = Math.Abs(vel.X);
        string clip;
        float speed = 1f;
        float rot = 0f;
        // (the Automaton doesn't swim: it walks the bottom and sinks upright, feet first, as on land)
        if (InWater && _dodgeT <= 0 && !Stats.Sinks)
        {
            if (vel.Length() > 40)
            {
                clip = "swim";
                speed = Math.Clamp(vel.Length() / 130f, 0.6f, 1.6f);
                float ang = MathF.Atan2(vel.Y, Math.Max(avx, 1f));
                rot = Mathf.Clamp(ang, -0.9f, 0.9f) * Facing;
            }
            else clip = "swim_idle";
        }
        else if (_wallSliding) clip = "wall_slide";
        else if (!onFloor)
        {
            if (_jumpedFromGround) Anim.Once("jump_start", 1);
            clip = vel.Y < -130 ? "jump_rise" : vel.Y < 140 ? "jump_apex" : "fall";
        }
        else if (avx > 25)
        {
            clip = "run";
            // (the run cycle is paced to the original 170 px/s, so with the 20% slower run it plays 20% slower too)
            speed = Math.Clamp(avx / (RunAnimRef * 0.95f), 0.4f, 1.2f);
            if (_lastBase == "idle") Anim.Once("run_start", 1);
        }
        else
        {
            clip = "idle";
            if (_lastBase == "run" && _lastAbsVx > 140) Anim.Once("run_stop", 1);
        }
        _lastBase = clip;
        _lastAbsVx = avx;
        Anim.Loop(clip, speed);
        Anim.Face((int)Facing);
        Anim.Motion(InWater && !Stats.Sinks ? vel * 0.3f : vel);
        // Ease the sprite's tilt toward the swim direction.
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, rot, 0.25f);

        // i-frame shimmer and post-hit blink
        float a = _invuln > 0 && (int)(_animT * 20) % 2 == 0 ? 0.35f : 1f;
        // (hidden in the shadows, or in smoke: only a faint shape)
        if (Hidden) a = 0.3f;
        Anim.Modulate = _iframes > 0 ? new Color(0.75f, 0.95f, 1f, a) : new Color(1, 1, 1, a);
    }

    /// <summary>
    /// Lava: it burns (a big share of your health) and throws you back up out of it. With Magma
    /// Skin you swim in it instead, and it burns for a third as much.
    /// </summary>
    private float _acidTick;

    private void Hazards(CaveData cave, float dt)
    {
        // acid water (the sulphur springs): it burns whoever is in it, every half second
        // (copper and brass: the Automaton takes no harm from it)
        if (!Dead && !Stats.Waterproof && cave.Biome?.AcidWater == true && cave.IsWater(GlobalPosition + new Vector2(0, 6)))
        {
            _acidTick -= dt;
            if (_acidTick <= 0)
            {
                _acidTick = Tune.Sulphur.AcidTick;
                G.Sfx.Play("lava", GlobalPosition, -10, 0.1f, 1.7f);
                G.Fx.Burst(GlobalPosition + new Vector2(G.Range(-6, 6), -4), new Color(0.78f, 0.92f, 0.2f, 0.8f), 5, 60, 1.8f, 0.4f, -40);
                TakeRawDamage(Math.Max(1f, (Stats.MaxHp * Tune.Sulphur.AcidShare + Tune.Sulphur.AcidFlat * G.DepthDmg) * (1f - Stats.DamageReduction) * Stats.DamageTakenMult), "burn");
            }
        }
        else _acidTick = 0f;
        if (!cave.IsLava(GlobalPosition + new Vector2(0, 8))) return;
        if (G.Chance(0.5f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-8, 8), 8), new Color(1f, 0.6f, 0.2f));
        if (_lavaTick > 0) return;
        _lavaTick = 0.7f;
        bool skin = Stats.MagmaSkin;
        G.Sfx.Play("lava", GlobalPosition, skin ? -8 : 0, 0.1f, 0.8f);
        G.Fx.Smoke(GlobalPosition, skin ? 2 : 4, new Color(0.25f, 0.2f, 0.2f, 0.5f));
        float burn = Math.Min(Stats.MaxHp * 0.16f + 4, 30 * G.DepthDmg) * (1f - Stats.DamageReduction) * Stats.DamageTakenMult;
        if (skin)
        {
            TakeRawDamage(burn * Tune.Hero.MagmaSkinBurn, "chip");
            return;
        }
        G.Fx.Splash(new Vector2(GlobalPosition.X, cave.WaterY), 0.8f, new Color(1f, 0.55f, 0.15f));
        TakeRawDamage(burn, "burn");
        Velocity = new Vector2(Velocity.X * 0.5f, -BaseJumpV * 1.05f);
        _coyote = 0;
        _invuln = Math.Max(_invuln, 0.3f);
    }

    /// <summary>A fallen body still obeys the world: it drops on land, and drifts down slowly in water.</summary>
    private void DeadPhysics(CaveData cave, float dt)
    {
        var v = Velocity;
        if (cave.IsWater(GlobalPosition))
        {
            v *= 1f / (1f + Tune.Hero.WaterDrag * 1.5f * dt);
            v = v.MoveToward(new Vector2(0, 28), 400f * dt);
        }
        else
        {
            v.X = Mathf.MoveToward(v.X, 0, (IsOnFloor() ? 900f : 200f) * dt);
            v.Y = Math.Min(v.Y + Gravity * dt, MaxFall);
        }
        Velocity = v;
        MoveAndSlide();
        // (a belt carries whoever stands on it)
        if (Conveyor.All.Count > 0 && IsOnFloor())
        {
            float belt = Conveyor.DriftAt(GlobalPosition + new Vector2(0, 14));
            if (belt != 0f) MoveAndCollide(new Vector2(belt * dt, 0));
        }
        Anim.Rotation = Mathf.LerpAngle(Anim.Rotation, 0, 0.2f);
        QueueRedraw();
    }

    /// <summary>Safety net: if the player ever ends up embedded in rock, nudge them out to open space.</summary>
    /// <summary>The pause menu's Unstick: moves the hero to the nearest open ground with room to stand, if they are in rock or wedged in a gap.</summary>
    public bool ForceUnstick()
    {
        var cave = G.Cave;
        if (cave == null || Dead) return false;
        bool Fits(Vector2 p) => !cave.IsSolid(p) && !cave.IsSolid(p + new Vector2(0, -14)) && !cave.IsSolid(p + new Vector2(0, 10))
            && !cave.IsSolid(p + new Vector2(-7, 0)) && !cave.IsSolid(p + new Vector2(7, 0));
        for (float r = 0; r < 500; r += 8)
            for (int k = 0; k < (r == 0 ? 1 : 24); k++)
            {
                var p = GlobalPosition + Vector2.Right.Rotated(k * Mathf.Tau / 24) * r;
                if (!Fits(p)) continue;
                GlobalPosition = p; Velocity = Vector2.Zero; _stuckInRock = 0;
                G.Fx.Ring(p, 14, new Color(0.9f, 0.95f, 1f, 0.8f));
                return true;
            }
        return false;
    }

    private void Unstick(CaveData cave, float dt)
    {
        if (!cave.IsSolid(GlobalPosition)) { _stuckInRock = 0; return; }
        _stuckInRock += dt;
        if (_stuckInRock < 0.25f) return;
        for (float r = 8; r < 400; r += 8)
            for (int k = 0; k < 16; k++)
            {
                var p = GlobalPosition + Vector2.Right.Rotated(k * Mathf.Tau / 16) * r;
                if (!cave.IsSolid(p) && !cave.IsSolid(p + new Vector2(0, -12)) && !cave.IsSolid(p + new Vector2(0, 12)))
                {
                    GlobalPosition = p; Velocity = Vector2.Zero; _stuckInRock = 0;
                    return;
                }
            }
    }

    private void TickTimers(float dt)
    {
        _coyote -= dt; _jumpBuffer -= dt; _invuln -= dt; _iframes -= dt; _swingCd -= dt; _swingSinceLast += dt;
        _wallJumpLock -= dt; _knockT -= dt; _hurtFlash -= dt; _lungeT -= dt; _hoverT -= dt;
        if (_lungeAir && _lungeT <= 0 && _hoverT <= 0) _lungeAir = false;
        _waveCd -= dt; WebbedT -= dt; _lavaTick -= dt;
        _attackBuf -= dt; _abilityBuf -= dt; _ability2Buf -= dt; _dodgeBuf -= dt;
        _drainCd -= dt; _hexCd -= dt; _healCd -= dt; _ruptureCd -= dt; _bashCd -= dt; _heaveCd -= dt; _snagT -= dt;
        TickBoons(dt);
        TickStatus(dt);
        TickIndignation(dt);
        // (the Elementalist's storm is paid for as each use comes back: see TickElementalist)
        for (int k = 0; k < _abilityCd.Length; k++) if (_abilityCd[k] > 0) _abilityCd[k] -= dt;
        if (_hotLeft > 0)
        {
            _hotLeft -= dt;
            Heal(_hotRate * dt);
            if (G.Chance(0.15f)) G.Fx.Ember(GlobalPosition + new Vector2(G.Range(-7, 7), G.Range(-10, 10)), new Color(1f, 0.55f, 0.7f));
        }
        for (int k = 0; k < _dodgeCd.Length; k++) if (_dodgeCd[k] > 0) _dodgeCd[k] -= dt;
    }

    private void UpdateBreath(float dt)
    {
        if (Stats.InfiniteBreath || Bubbled || Form?.Key == "fish")
        {
            // Drowned Lungs (or a bubble round you): the water is as good as air
            Breath = Stats.BreathMax;
            if (HeadUnder && !Stats.Breathless && (_bubbleT -= dt) <= 0) { _bubbleT = G.Range(0.8f, 1.6f); G.Fx.Bubbles(GlobalPosition + new Vector2(Facing * 3, -12), 1); }
            return;
        }
        // (the lowest air of the sulphur springs is poison: you hold your breath in it, as under water)
        bool toxic = !HeadUnder && G.Cave != null && G.Cave.IsToxic(GlobalPosition + new Vector2(0, -12));
        if (HeadUnder || toxic)
        {
            // thick, rotting water leaves you gasping sooner
            Breath -= dt * (G.Biome?.Murky == true ? Tune.Hero.MurkyBreathDrain : 1f);
            _bubbleT -= dt;
            if (_bubbleT <= 0)
            {
                _bubbleT = G.Range(0.4f, 1.0f);
                if (toxic) G.Fx.Burst(GlobalPosition + new Vector2(Facing * 3, -12), new Color(0.85f, 0.85f, 0.3f, 0.6f), 2, 24, 2f, 0.6f, -10);
                else G.Fx.Bubbles(GlobalPosition + new Vector2(Facing * 3, -12), 2);
            }
            if (Breath <= 0)
            {
                Breath = 0;
                _drownTick -= dt;
                if (_drownTick <= 0) { _drownTick = 0.5f; TakeRawDamage(Tune.Hero.DrownDamageFlat + Stats.MaxHp * Tune.Hero.DrownDamageFrac, "drown"); }
            }
        }
        else
        {
            if (Breath < Stats.BreathMax * 0.35f && Breath < Stats.BreathMax - 0.1f && _drownTick != -99) { G.Sfx.Play("gasp", GlobalPosition, -6); _drownTick = -99; }
            Breath = Math.Min(Stats.BreathMax, Breath + dt * Stats.BreathMax * 0.6f);
            if (Breath >= Stats.BreathMax) _drownTick = 0;
        }
    }

    private Vector2 Platform(PlayerInput inp, Vector2 v, float dt, bool onFloor)
    {
        float target = inp.Move.X * RunSpeed * Stats.MoveSpeed * (1f + Stats.SprintBonus * Math.Min(1f, _runT)) * (_adrT > 0f ? 1f + Stats.AdrenalineBonus : 1f) * (Shifted ? FormMove + Stats.FormSpeedAdd : 1f) * (_formAtkT >= 0 && onFloor ? 0.35f : 1f) * (onFloor ? 1f : Stats.AirSpeedMult) * (ShieldRaised && !Stats.Stalwart ? Tune.Warden.ShieldMoveMult : 1f) * (Vanished ? Tune.Rogue.VanishSpeed : 1f);
        if (WebbedT > 0) target *= 0.45f;
        if (Crouching) target *= Tune.Hero.CrouchSpeed;
        // planted for a heaving swing, or braced behind a shield bash
        bool rooted = Heaving || (_bashT > 0 && onFloor) || (Bracing && onFloor);
        if (Repairing) target *= Tune.AutomatonHero.RepairMove;
        if (Pressurising) target *= Tune.AutomatonHero.PressureMove;
        if (rooted) target = 0;
        // a column of air (the Elementalist's updraft) pushes along its own lean: sideways as much as it leans
        var draft = Updraft.All.Count > 0 ? Updraft.At(GlobalPosition) : null;
        if (draft != null && !rooted) target += draft.Up.X * Tune.Elementalist.UpdraftPushSpeed * draft.Strength;
        float accel = onFloor ? Tune.Hero.GroundAccel : (_wallJumpLock > 0 ? 350f : Tune.Hero.AirAccel * Stats.AirAccelMult);
        // (a blow's shove isn't cancelled the moment it lands)
        if (_knockT > 0) accel *= 0.1f;
        // frozen ground: slow to get going and slower to stop
        if (onFloor && G.Biome != null && G.Biome.Slippery)
        {
            accel *= Math.Abs(target) > Math.Abs(v.X) && Math.Sign(target) == Math.Sign(v.X) ? 0.35f : 0.12f;
            _slideDust -= dt;
            if (Math.Abs(v.X - target) > 60 && _slideDust <= 0) { _slideDust = 0.05f; G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 1, 0.5f, new Color(0.85f, 0.95f, 1f, 0.6f)); }
        }
        else if (onFloor && Math.Abs(v.X) > 150 && Math.Sign(target) != Math.Sign(v.X) && G.Chance(0.3f)) G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 1, 0.6f);
        v.X = Mathf.MoveToward(v.X, target, (rooted ? 4000f : accel) * dt);
        if (_lungeT > 0 && !_lungeAir) v.X = _lungeDir * Math.Max(Math.Abs(v.X) * Math.Sign(v.X) * _lungeDir, LungeNow); // sword lunge
        if (_bashT > 0) v.X = BashMotion(v.X);
        // a column of rising air (the Elementalist's updraft) slackens gravity and the speed you can fall at
        float gravMult = 1f, fallCap = MaxFall * Stats.FallCapMult;
        if (draft != null) UpdraftEase(draft, ref gravMult, ref fallCap, ref v, dt);
        // a flier's form (a bat, a hornet): light, and a held jump beats its wings
        if (FormFlier)
        {
            gravMult *= 0.3f; fallCap = Math.Min(fallCap, 150f);
            if (inp.JumpHeld) v.Y = Math.Max(v.Y - 1500f * dt, -190f);
        }
        v.Y = Math.Min(v.Y + Gravity * gravMult * dt * (v.Y > 0 ? Tune.Hero.FallGravityMult : 1f), fallCap);
        // the Pursuit relic: a swing in the air carries the hero after its creature, then hangs them in place a moment (no fall, no gravity)
        if (_lungeAir)
        {
            if (onFloor || InWater) { _lungeAir = false; _hoverT = 0; _lungeT = 0; }
            else if (_lungeT > 0) v = AerialLungeVelocity();
            else if (_hoverT > 0)
            {
                if (!_hoverStarted) { _hoverStarted = true; _hoverY0 = GlobalPosition.Y; HoverDrop = 0f; }
                HoverDrop = Math.Max(HoverDrop, GlobalPosition.Y - _hoverY0);
                v = new Vector2(v.X * 0.9f, 0f);
            }
        }

        float jumpV = BaseJumpV * MathF.Sqrt(Stats.JumpMult * FormJump) * (WebbedT > 0 ? 0.75f : 1f);
        int wallSide = WallSide();
        bool onWall = !onFloor && wallSide != 0;

        if (Stats.WallJump && onWall && v.Y > 0 && Math.Sign(inp.Move.X) == wallSide)
        {
            v.Y = Math.Min(v.Y, Tune.Hero.WallSlideSpeed * Stats.WallSlideMult);
            _wallSliding = true;
            Facing = wallSide;
            if (G.Chance(0.2f)) G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 6), new Color(0.6f, 0.55f, 0.5f, 0.6f), 1, 20, 1.5f, 0.3f, 30);
        }

        if (_jumpBuffer > 0 && !Heaving && !(Bracing && onFloor))
        {
            if (_coyote > 0)
            {
                v.Y = -jumpV; _coyote = 0; _jumpBuffer = 0; _jumpCutDone = false;
                _jumpedFromGround = true;
                G.Sfx.Play("jump", GlobalPosition, -8);
                G.Fx.Dust(GlobalPosition + new Vector2(0, 12), 3);
            }
            else if (Stats.WallJump && onWall)
            {
                v = new Vector2(-wallSide * Tune.Hero.WallJumpPush, -jumpV * Tune.Hero.WallJumpMult);
                Facing = -wallSide; _wallJumpLock = 0.16f; _jumpBuffer = 0; _jumpCutDone = false;
                G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 1.2f);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("jump_start", 2);
                G.Fx.Burst(GlobalPosition + new Vector2(wallSide * 7, 0), new Color(0.7f, 0.65f, 0.6f, 0.8f), 6, 80, 2f, 0.3f, 100);
            }
            else if (Stats.DoubleJump && _airJumps > 0)
            {
                _airJumps--; v.Y = -jumpV * Tune.Hero.DoubleJumpMult * Stats.AirJumpMult; _jumpBuffer = 0; _jumpCutDone = false;
                if (Stats.Sinks)
                {
                    // the Automaton's steam hop: a hiss and a puff from the joints, no somersault
                    G.Sfx.Play("dodge", GlobalPosition, -8, 0.05f, 0.6f);
                    for (int k = 0; k < 4; k++) G.Fx.Smoke(GlobalPosition + new Vector2(G.Range(-6, 6), 12), 1, new Color(0.94f, 0.95f, 0.97f, 0.55f), 7);
                }
                else
                {
                    G.Sfx.Play("jump", GlobalPosition, -5, 0.05f, 1.4f);
                    Anim.Once("dodge", 2, 1.6f);
                    G.Fx.Ring(GlobalPosition + new Vector2(0, 12), 10, new Color(0.7f, 0.9f, 1f, 0.8f));
                }
            }
            else if (Stats.AirDash && _airDashes > 0)
            {
                _airDashes--; _jumpBuffer = 0;
                var d = inp.Move.LengthSquared() > 0.04f ? inp.Move.Normalized() : new Vector2(Facing, 0);
                _airDashDir = d; _airDashT = Tune.Hero.AirDashTime;
                if (d.X != 0) Facing = Math.Sign(d.X);
                G.Sfx.Play("airdash", GlobalPosition, -4);
                Anim.Face((int)Facing, instant: true);
                Anim.Once("airdash", 3);
                G.Fx.Ring(GlobalPosition, 12, new Color(0.6f, 0.9f, 1f, 0.8f));
            }
        }
        if (!inp.JumpHeld && v.Y < -120 && !_jumpCutDone) { v.Y *= Tune.Hero.JumpCutMult; _jumpCutDone = true; }
        return v;
    }

    /// <summary>-1 / +1 if there is rock right beside the player (for wall slide / wall jump), else 0.</summary>
    private int WallSide()
    {
        var c = G.Cave; var p = GlobalPosition;
        for (int s = -1; s <= 1; s += 2)
            if (c.IsSolid(p + new Vector2(s * 10, -4)) && c.IsSolid(p + new Vector2(s * 10, 6))) return s;
        return 0;
    }

    private Vector2 Swim(PlayerInput inp, Vector2 v, float dt, CaveData cave)
    {
        // (a leap out carries on through the last of the water as through the air: the water doesn't drag it back)
        if (_leapT > 0f && v.Y < 0f) { _leapT -= dt; v.Y += Gravity * dt; return v; }
        var dir = inp.Move;
        if (inp.JumpHeld) dir.Y = -1;
        float spd = SwimSpeedBase * Stats.SwimSpeed * (G.Biome?.Murky == true ? Tune.Hero.MurkySwimMult : 1f);
        // a river's current carries whoever is in it (everything below is worked out in the water's own frame); the Elementalist's
        // narrow draft is a wind in the water, pushing whoever swims in its column along its axis
        var flow = Vector2.Zero;
        if (cave.Flow != 0f)
        {
            flow = cave.FlowAt(GlobalPosition);
            if (Updraft.At(GlobalPosition) is Updraft ud) flow += ud.Up * (Tune.River.DraftPush * ud.Strength);
            v -= flow;
        }
        if (dir.LengthSquared() > 0.04f)
        {
            v = v.MoveToward(dir.Normalized() * spd, Tune.Hero.SwimAccel * dt);
            if (G.Chance(0.05f)) G.Fx.Bubbles(GlobalPosition, 1);
        }
        else v = v.MoveToward(new Vector2(0, 22), 320f * dt);
        v *= 1f / (1f + Tune.Hero.WaterDrag * dt);

        // swimming up, you stop at the surface with your head out of the water, where you can breathe (no bobbing up out of it)
        float rest = cave.WaterY + Tune.Hero.SurfaceRest;
        _leapT -= dt;
        if (_leapT <= 0f && v.Y < 0f && GlobalPosition.Y + v.Y * dt < rest) v.Y = Math.Min(0f, (rest - GlobalPosition.Y) / Math.Max(dt, 0.001f));
        bool nearSurface = GlobalPosition.Y < cave.WaterY + 20;
        // the leap out: from the surface, once you've gone back under after the last one (or stopped coming down)
        if (_jumpBuffer > 0 && nearSurface && _leapReady)
        {
            v.Y = -BaseJumpV * MathF.Sqrt(Stats.JumpMult) * Tune.Hero.SurfaceLeapMult;
            _jumpBuffer = 0; _jumpCutDone = true;
            _leapReady = false;
            _leapT = 0.4f; // (the leap carries it out: the surface doesn't hold it back)
            G.Sfx.Play("jump", GlobalPosition, -6, 0.05f, 0.8f);
            if (cave.Flow != 0f) flow = Vector2.Zero; // (out of the water it is only the leap)
        }
        return v + flow;
    }

    /// <summary>
    /// Damage this hero dealt: life steal, and the Vitalist's vital force (unless the stolen life is
    /// flying home as a <see cref="LifeMote"/> that pays it on arrival).
    /// </summary>
    public void OnDealtDamage(float dealt, bool vitalForceByMote = false)
    {
        if (this == G.Player) Achievements.Dealt(dealt);
        if (Stats.LifeSteal > 0)
        {
            if (Stats.SelfRepair) HealAsFuel(dealt * Stats.LifeSteal);
            else Hp = Math.Min(Stats.MaxHp, Hp + dealt * Stats.LifeSteal);
        }
        if (Stats.ShieldSiphon > 0 && IsWarden) MendShield(dealt * Stats.ShieldSiphon);
        if (IsVitalist && !vitalForceByMote) GainVitalForce(dealt * Stats.VitalForceGain);
    }

    public void OnKill(Enemy e = null)
    {
        Kills++;
        Stats.On.RaiseKill(this, e);
        if (Stats.HealOnKill > 0) Heal(Stats.HealOnKill);
        // (Scrap Reclaimer: what it destroys, it salvages)
        if (Stats.ScrapReclaimer) GainFuel(Tune.AutomatonHero.ScrapFuel);
    }

    /// <summary>Gold picked up: it goes in the purse.</summary>
    public void GainGold(int n, bool pile = false)
    {
        if (n <= 0) return;
        if (pile) n += Stats.PileBonus;
        n = Math.Max(1, (int)MathF.Round(n * Stats.GoldMult * Meta.GoldFoundMult));
        if (!IsRemote) GridFinds.GoldGained(this, Gold);
        Gold += n;
        GoldFound += n;
        G.Sfx.Play("pickup_key", GlobalPosition, -8, 0.06f, 1.7f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -24), $"+{n}", new Color(1f, 0.84f, 0.3f), 10, 0.9f);
    }

    /// <summary>Spends gold (false when there isn't enough).</summary>
    public bool SpendGold(int n)
    {
        if (n < 0 || Gold < n) return false;
        Gold -= n;
        return true;
    }
    /// <summary>All the gold found this run (spent or not).</summary>
    public int GoldFound;

    /// <summary>True when the last Hurt was stopped by the shield (the attacker's strike is spent).</summary>
    public bool LastHitBlocked { get; private set; }

    /// <summary>Returns the damage taken. <paramref name="source"/> is credited with it (enemy learning).</summary>
    public float Hurt(float dmg, Vector2 from, float knock = 230f, Enemy source = null, bool pooled = false)
    {
        LastHitBlocked = false;
        if (Dead) return 0;
        // Counter Roll: a melee blow met mid-roll is stopped, and answered
        if (!IsRemote && TryCounter(source)) { LastHitBlocked = true; return 0; }
        if (Invulnerable) return 0;
        // (a creature can't strike through a wall of rubble or a rock slab between it and the hero)
        if (source != null && GodotObject.IsInstanceValid(source) && Breakables.Blocks(source.GlobalPosition, GlobalPosition + new Vector2(0, -4))) return 0;
        // ducking under a blow that comes from above your head
        if (Crouching && source != null && from.Y < GlobalPosition.Y - Tune.Hero.DuckHeight)
        {
            LastHitBlocked = true;
            G.Fx.Text(GlobalPosition + new Vector2(0, -22), "DUCKED", new Color(0.85f, 0.95f, 1f), 9, 0.6f);
            return 0;
        }
        // every blow from a creature is an area one: whoever is close enough shares it out evenly
        if (!pooled && source != null && Tune.Share.On) dmg = PoolBlow(dmg, from, knock, source);
        if (IsRemote)
        {
            // another player's hero: their game takes the blow (a moment's grace here, so one
            // strike isn't sent every frame while it overlaps)
            _invuln = 0.3f;
            return NetSync.HurtRemote(this, dmg, from, knock, source, pooled: true);
        }
        NetSync.Scope++;
        try { return HurtHere(dmg, from, knock, source, pooled); }
        finally { NetSync.Scope--; }
    }

    /// <summary>
    /// A creature's blow lands on this hero: every living hero within Tune.Share.Radius of them takes
    /// an equal share of it (each through their own shield, barrier and armour), so a friend's shield
    /// takes its part of the blow and everyone near gets off lighter. Returns this hero's share.
    /// </summary>
    private float PoolBlow(float dmg, Vector2 from, float knock, Enemy source)
    {
        List<Player> others = null;
        foreach (var h in G.Players)
        {
            if (h == this || !GodotObject.IsInstanceValid(h) || h.Dead) continue;
            if (h.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Share.Radius) continue;
            // (an Aegis already carrying a share of this hero's blows takes only that share, not a part of the pool besides)
            if (CarriesForMe(h)) continue;
            (others ??= new()).Add(h);
        }
        if (others == null) return dmg;
        float share = dmg / (others.Count + 1);
        foreach (var h in others) h.Hurt(share, from, knock, source, pooled: true);
        return share;
    }

    private float HurtHere(float dmg, Vector2 from, float knock, Enemy source, bool shared = false)
    {
        float raw = dmg;
        bool melee = source != null && GodotObject.IsInstanceValid(source) && source.GlobalPosition.DistanceTo(GlobalPosition) < 70;
        var block = TryBlock(from, dmg, melee ? source : null, source);
        if (block.Blocked)
        {
            // the shield took it: at most the share it lets through, with no flinch
            LastHitBlocked = true;
            float chip = ApplyChip(block.Through, source);
            RunMetrics.Prevented(raw - chip, Net.Me);
            RunMetrics.Took(chip, source);
            return chip;
        }
        dmg *= (1f - Stats.DamageReduction) * Stats.DamageTakenMult;
        if (Stats.LowHpTakenMult != 1f && Hp < Stats.MaxHp * 0.25f) dmg *= Stats.LowHpTakenMult;
        // (Hard Case: no single blow takes more than a share of your health)
        if (Stats.BlowCap > 0f) dmg = Math.Min(dmg, Stats.MaxHp * Stats.BlowCap);
        // (the Automaton braced: blows in front lose most of their force, and the first melee one is answered)
        if (Bracing) dmg = BraceBlow(dmg, from, source, melee, ref knock);
        if (Form != null) dmg *= 1f - Math.Min(0.8f, FormArmor + Stats.FormArmorAdd);
        // (Assassin's Edge: a blow at your back lands harder)
        if (Stats.BackTakenMult != 1f && Math.Abs(from.X - GlobalPosition.X) > 2f && Math.Sign(from.X - GlobalPosition.X) != Math.Sign(Facing)) dmg *= Stats.BackTakenMult;
        if (Stats.HeaveGuard && Heaving) dmg *= 0.5f; // (Braced)
        // (what the hero's own guard took off it: armour, a brace, a form's hide)
        RunMetrics.Prevented(raw - dmg, Net.Me);
        // a barrier soaks what it can first (a blow it soaks whole doesn't even make you flinch)
        float preBarrier = dmg;
        dmg = Soften(dmg);
        RunMetrics.Prevented(preBarrier - dmg, _barrierBy);
        // (Shared Burden: the Aegis takes its share of what gets through)
        float preShare = dmg;
        dmg = ShareBurden(dmg);
        RunMetrics.Prevented(preShare - dmg, _burdenBy);
        // (Foe's Burden: a quarter of what gets through is the burdened creature's)
        dmg = RedirectToFoe(dmg);
        // (Share Pain: an Aegis takes her part of every blow)
        preShare = dmg;
        dmg = SharePain(dmg);
        RunMetrics.Prevented(preShare - dmg, _painBy);
        if (dmg <= 0.01f) { LastHitBlocked = true; _invuln = Math.Max(_invuln, 0.15f); return 0; }
        RunMetrics.Took(dmg, source);
        if (source != null && GodotObject.IsInstanceValid(source))
        {
            source.CreditDamage(dmg);
            // mutual bounce: an enemy that struck you in melee rebounds a little too
            if (melee) source.Recoil(source.GlobalPosition.X - GlobalPosition.X);
        }
        TakeRawDamage(dmg, "hit");
        if (this == G.Player) { Achievements.StruckBy(source); if (Dead) Achievements.FellTo(source); }
        WebTakesHarm(dmg);
        // (what else the blow does: a poison, a burn, a freeze...)
        if (source != null && GodotObject.IsInstanceValid(source) && !Dead) source.StrikeStatus(this, dmg);
        var striker = source != null && GodotObject.IsInstanceValid(source) ? source : null;
        Stats.On.RaiseHurt(this, striker, dmg);
        FrostArmorStruck(striker, melee);
        _sinceHurt = 0f;
        if (Stats.AdrenalineBonus > 0f) _adrT = 2f;
        // (Barbed Hide: part of the blow falls back on what struck)
        if (Stats.ThornsShare > 0f && striker != null && !striker.Dead)
            striker.Hurt(dmg * Stats.ThornsShare, (striker.GlobalPosition - GlobalPosition).Normalized() * 80f, striker.GlobalPosition, DamageKind.Raw);
        if (Stats.Indignation) StartIndignation();
        // (a blow that lands brings a vanished Rogue out of the shadows)
        Reveal(true);
        _invuln = Stats.HurtInvuln;
        // a share of a friend's blow: the damage only. No flinch, no hit-stop and no shove: only the hero struck is thrown back
        if (shared) { Anim.Flash(0.5f); return dmg; }
        var away = (GlobalPosition - from).Normalized();
        // turn to face what hit you, then recoil
        if (Math.Abs(from.X - GlobalPosition.X) > 2) Facing = Math.Sign(from.X - GlobalPosition.X);
        Anim.Face((int)Facing, instant: true);
        Anim.Once("hurt", 4);
        Anim.Flash(1f);
        if (Possessed) { Ghost.Animator?.Once("hurt", 4); Ghost.Animator?.Flash(1f); }
        Freeze(Tune.Feel.HitStopPlayerHurt);
        if (source != null && GodotObject.IsInstanceValid(source) && !source.Dead) source.Freeze(Tune.Feel.HitStopPlayerHurt, hold: true);
        G.Main.Kick(away * Tune.Feel.KickPlayerHurt);
        G.Main.Rumble(0.6f, 0.8f, 0.25f);
        if (away.LengthSquared() < 0.01f) away = new Vector2(-Facing, 0);
        // horizontal only (plus a gentle push in water) so nothing can juggle you upward
        float kx = Math.Sign(away.X == 0 ? -Facing : away.X) * knock * Tune.Combat.HurtKnockbackMult * (Stats.Stalwart ? 0f : 1f) * Stats.KnockTakenMult;
        Velocity = new Vector2(kx, InWater ? Velocity.Y + away.Y * knock * 0.3f : Velocity.Y);
        if (Possessed) Ghost.Velocity = new Vector2(kx / Tune.Difficulty.EnemyMoveScale, Ghost.Velocity.Y);
        _dodgeT = 0; _airDashT = 0; _dashT = 0;
        _pauseT = 0; _pauseNext = 0;
        _knockT = KnockLock ? Tune.Combat.HurtKnockLock : 0f;
        return dmg;
    }

    /// <summary>Freezes just the hero for a hit-stop (holding the pose from this very frame).</summary>
    public void Freeze(float seconds)
    {
        _freeze = Math.Max(_freeze, seconds);
        if (Anim != null) Anim.TimeMult = 0;
        if (Possessed) Ghost.Freeze(seconds, hold: true);
    }
    /// <summary>Seconds of hit-stop left.</summary>
    public float FreezeLeft => _freeze;
    /// <summary>Seconds of post-hit stillness left, and queued behind the hit-stop (for the tests).</summary>
    public float PauseLeft => _pauseT + _pauseNext;
    /// <summary>For the tests: the last pause queued behind a landed blow, and how far the hero got from where a pause began, while it lasted.</summary>
    public float LastHitPause { get; private set; }
    public float PauseDrift { get; private set; }
    public int HitPausesRun { get; private set; }
    /// <summary>For the tests: the vertical speed just before and just after the last landed strike.</summary>
    public float HitVyBefore { get; private set; }
    public float HitVyAfter { get; private set; }
    private Vector2 _pausePos;

    private int _lastStandDepth = -1;

    private void TakeRawDamage(float dmg, string kind)
    {
        // (insulation softens a burn or poison working on you, a cushioned landing a fall)
        if (kind is "dot" or "burn") dmg *= Stats.DotMult;
        else if (kind == "fall") dmg *= Stats.FallDamageMult;
        float hp0 = Hp;
        Hp -= dmg;
        bool lethal = Hp <= 0;
        if (Hp <= 0 && Stats.LastStand && _lastStandDepth != G.Depth)
        {
            // once per depth, a killing blow leaves you standing behind a whole shield
            _lastStandDepth = G.Depth;
            Hp = 1;
            _invuln = Tune.Warden.LastStandInvuln;
            RefillShield();
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "LAST STAND", new Color(1f, 0.85f, 0.4f), 13, 1.5f);
            G.Fx.Ring(GlobalPosition, 24, new Color(1f, 0.85f, 0.4f), 0.3f, FxLayer.RingStyle.Dashed);
            G.Sfx.Play("roar", GlobalPosition, -8, 0, 1.8f);
        }
        if (Hp <= 0 && Stats.DeathWard && _wardDepth != G.Depth)
        {
            // Death Ward: once a depth, a killing blow leaves you at 1 health, untouchable for a couple of seconds
            _wardDepth = G.Depth;
            Hp = 1;
            _invuln = Math.Max(_invuln, 2f);
            Bump("deathward");
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "DEATH WARD", new Color(1f, 0.95f, 0.6f), 13, 1.5f);
            G.Fx.Ring(GlobalPosition, 26, new Color(1f, 0.95f, 0.6f), 0.5f, FxLayer.RingStyle.Wave);
            G.Sfx.Play("levelup", GlobalPosition, -2, 0, 0.8f);
        }
        if (Hp <= 0 && Stats.NullPointer && _nullDepth != G.Depth)
        {
            // Null Pointer: once a depth, a killing blow leaves you at 1, thrown clear (never into the rock)
            _nullDepth = G.Depth;
            Hp = 1;
            _invuln = Math.Max(_invuln, 1f);
            var cave = G.Cave;
            for (int k = 0; k < 12; k++)
            {
                var to = GlobalPosition + G.RandDir() * G.Range(40f, 90f);
                if (cave != null && !cave.IsSolid(to) && !cave.IsSolid(to + new Vector2(0, -12)) && cave.LineClear(GlobalPosition, to)) { GlobalPosition = to; break; }
            }
            G.Fx.Text(GlobalPosition + new Vector2(0, -30), "NULL POINTER", new Color(0f, 1f, 1f), 12, 1.4f);
            G.Fx.Debris(GlobalPosition, new Color(1f, 0f, 1f), 8, 150);
            G.Sfx.Play("glitch", GlobalPosition, -4, 0.2f, 1.3f);
        }
        // (achievements: a whole depth unhurt is lost; a blow from full that fells, or one past the whole health survived)
        if (!IsRemote && this == G.Player) Achievements.TookDamage(dmg, hp0, Stats.MaxHp, lethal, Hp <= 0, kind);
        // (damage over time, poison and burning, is quiet: no shake, flash or number of its own; its numbers are told in TickStatus)
        if (kind == "dot") { if (Hp <= 0) Die(); return; }
        bool chip = kind == "chip";
        _hurtFlash = chip ? 0.06f : 0.15f;
        // (the number floated is the change in the health shown, which counts a fraction up: see Num)
        int shown = Num.Delta(hp0, Math.Max(0f, Hp));
        if (shown > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -24), shown.ToString(), chip ? new Color(1f, 0.7f, 0.55f) : new Color(1f, 0.35f, 0.3f), chip ? 13 : 16);
        if (!chip) G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 8, 120, 2.2f, 0.45f);
        G.Fx.AddShake(kind == "drown" ? 2 : chip ? 1.5f : 6);
        if (!chip) G.Sfx.Play(kind == "drown" ? "bubble" : "hurt", GlobalPosition, -2);
        if (Hp <= 0) Die();
    }

    /// <summary>Gives up the run (the pause menu): the hero falls where they stand.</summary>
    public void GiveUp()
    {
        if (Dead) return;
        Hp = 0;
        Die();
    }

    private void Die()
    {
        if (Dead) return;
        Dead = true; Hp = 0;
        if (!IsRemote) RunMetrics.Died();
        // (a hero that falls in a creature's form falls as itself)
        if (Possessed) { Form = null; DropGhost(); ApplyFormLook(); }
        ClearStatus();
        ReviveProgress = 0;
        NetSync.HeroDown(true);
        if (!IsRemote) Stats.On.RaiseFall(this);
        Anim.Once("death", 99);
        Anim.Modulate = Colors.White;
        G.Main.Rumble(1f, 1f, 0.6f);
        G.Sfx.Play("player_die", GlobalPosition);
        G.Fx.Burst(GlobalPosition, new Color(0.8f, 0.1f, 0.1f), 30, 200, 3f, 0.9f);
        G.Main.OnPlayerDied();
    }

    /// <summary>
    /// The body is a sprite (see <see cref="Anim"/>, which drives the 3D model); this draws the
    /// 2D debug view's extras: the blade smear and the Warden's guard.
    /// </summary>
    public override void _Draw()
    {
        if (Dead) return;
        if (IsWarden) DrawGuard();
        DrawSmear();
    }
}

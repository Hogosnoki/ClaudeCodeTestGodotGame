using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// Online play, the hero's side. This game's own hero sends its state twenty times a second
/// (<see cref="WriteNet"/>); another player's hero here is a puppet (<see cref="IsRemote"/>) that
/// shows what their game sends: smoothed movement, the same animation, the shield, the staff's
/// glow and the blade's sweep. A puppet takes no damage here: a blow on it goes to its own game.
/// </summary>
public partial class Player
{
    /// <summary>Online: whose hero this is (their peer id), and their name.</summary>
    public int NetOwner;
    public string NetName = "";
    /// <summary>Online: picking an upgrade (the game doesn't stop online), and safe meanwhile.</summary>
    public bool Choosing;
    /// <summary>Online: holding the interact button over a fallen friend (0..1 of the way to getting them up).</summary>
    public float ReviveProgress { get; private set; }
    /// <summary>The fallen friend being revived, if any.</summary>
    public Player ReviveTarget { get; private set; }

    private readonly NetInterp _net = new();
    private string _netAnim = "";
    private int _netFrame;
    private float _netSpeed = 1f, _netFlash;
    private uint _netFlags;
    private float _netShieldStrength = 1f;

    private const uint HfDead = 1, HfFloor = 2, HfShield = 4, HfCharged = 8, HfHeaving = 16, HfChoosing = 32, HfInvuln = 64,
                       HfGuarding = 128, HfSecondary = 256, HfDash = 1024, HfPerfect = 2048, HfBarrier = 4096, HfMending = 8192, HfFrost = 16384,
                       HfHidden = 32768, HfDagger0Out = 65536, HfDagger1Out = 131072, HfBubble = 262144, HfPoison = 524288, HfBurn = 1048576, HfFrozen = 2097152, HfDrown = 4194304, HfTaunt = 8388608,
                       HfFire = 16777216, HfOrbWind = 33554432, HfOrbSnap = 67108864, HfOrbStorm = 134217728, HfWeb = 268435456, HfStun = 536870912;

    /// <summary>On the ground (a puppet goes by what its game says).</summary>
    public bool OnGround => IsRemote ? (_netFlags & HfFloor) != 0 : Possessed ? Ghost.OnGround : IsOnFloor();

    private bool NetInvuln => (_netFlags & HfInvuln) != 0;

    /// <summary>This game's hero, for the others.</summary>
    public void WriteNet(NetOut w)
    {
        w.Vec(GlobalPosition);
        w.HVec(Velocity);
        w.SByte((sbyte)(Facing < 0 ? -1 : 1));
        var spr = Anim?.Sprite;
        w.Str(spr != null ? (string)spr.Animation : "");
        w.Byte((byte)Math.Clamp(spr?.Frame ?? 0, 0, 255));
        w.Half(spr != null ? spr.SpeedScale * Math.Max(0f, Anim.TimeMult) : 1f);
        uint f = 0;
        if (Dead) f |= HfDead;
        if (IsOnFloor()) f |= HfFloor;
        if (ShieldRaised) f |= HfShield;
        if (Charged > 0 || SwingCharged) f |= HfCharged;
        if (Heaving) f |= HfHeaving;
        if (Choosing) f |= HfChoosing;
        if (Invulnerable) f |= HfInvuln;
        if (Guarding) f |= HfGuarding;
        if (SecondaryReady) f |= HfSecondary;
        if (IsShieldDashing) f |= HfDash;
        if (ShieldRaised && (_shieldUpT <= PerfectWindowNow || _shieldFlash > 0)) f |= HfPerfect;
        if (BarrierHp > 0.01f) f |= HfBarrier;
        if (_mendLeft > 0) f |= HfMending;
        if (BubbleHp > 0.01f) f |= HfBubble;
        if (_poisonLeft > 0) f |= HfPoison;
        if (_burnLeft > 0) f |= HfBurn;
        if (_frozenT > 0) f |= HfFrozen;
        if (_drownT > 0) f |= HfDrown;
        if (_webT > 0) f |= HfWeb;
        if (_stunT > 0) f |= HfStun;
        if (_tauntLeft > 0) f |= HfTaunt;
        if (Stats.Frostbolt) f |= HfFrost;
        if (IsElementalist)
        {
            if (Stats.Firestorm) f |= HfFire;
            if (UpdraftOrb) f |= HfOrbWind;
            if (SnapOrb) f |= HfOrbSnap;
            if (StormOrb) f |= HfOrbStorm;
        }
        if (Hidden) f |= HfHidden;
        if (_thrown[0] != null) f |= HfDagger0Out;
        if (_thrown[1] != null) f |= HfDagger1Out;
        w.UInt(f);
        w.Half(Hp);
        w.Half(Stats.MaxHp);
        w.Byte((byte)Math.Clamp(Level, 0, 255));
        w.Byte((byte)Math.Clamp(Potions, 0, 255));
        w.Byte((byte)Math.Clamp(Keys, 0, 255));
        w.HVec(ShieldDir);
        w.Byte((byte)(Math.Clamp(ShieldHp / Math.Max(1f, Stats.ShieldMax), 0, 1) * 255));
        w.Byte((byte)(Math.Clamp(_castGlow, 0, 1) * 255));
        w.Byte(LastCast switch { "drain" => 1, "hex" => 2, "heal" => 3, "rupture" => 4, "barrier" => 5, "burden" => 6, "bubble" => 7, "ward" => 8, _ => 0 });
        w.HVec(CastDir);
        w.Byte((byte)(Math.Clamp(Anim?.FlashAmount ?? 0, 0, 1) * 255));
        w.Byte((byte)(BubbleFrac * 255));
        // (a Shape Shifter: which creature it is, and which of that creature's attack clips is playing, how far along)
        w.Byte(Form?.Id ?? 0);
        w.Byte((byte)(FormClip == null ? 0 : FormClip == Form?.WindClip ? 1 : 2));
        w.Byte((byte)(Math.Clamp(FormClipT, 0f, 1f) * 255));
        // (an Elementalist's bolts: how many ready, how many it holds)
        w.Byte(IsElementalist ? (byte)(Math.Min(BoltCharges, 15) | Math.Min(BoltChargesMax, 15) << 4) : (byte)0);
    }

    /// <summary>A puppet: the latest from its game.</summary>
    public void ReadNet(NetIn r, double now)
    {
        var pos = r.Vec();
        var vel = r.HVec();
        Facing = r.SByte();
        _netAnim = r.Str();
        _netFrame = r.Byte();
        _netSpeed = r.Half();
        _netFlags = r.UInt();
        Hp = r.Half();
        Stats.MaxHp = Math.Max(1f, r.Half());
        Level = r.Byte();
        Potions = r.Byte();
        Keys = r.Byte();
        ShieldDir = r.HVec();
        _netShieldStrength = r.Byte() / 255f;
        _castGlow = Math.Max(_castGlow, r.Byte() / 255f);
        LastCast = r.Byte() switch { 1 => "drain", 2 => "hex", 3 => "heal", 4 => "rupture", 5 => "barrier", 6 => "burden", 7 => "bubble", 8 => "ward", _ => LastCast };
        CastDir = r.HVec();
        float flash = r.Byte() / 255f;
        if (flash > _netFlash + 0.3f) Anim?.Flash(flash);
        _netFlash = flash;
        if (r.More) _netBubbleFrac = r.Byte() / 255f;
        if (r.More)
        {
            byte fid = r.Byte(), phase = r.Byte(); float ft = r.Byte() / 255f;
            if (fid != (Form?.Id ?? 0)) { Form = ShiftForm.ById(fid); ApplyFormLook(); }
            FormClip = Form == null || phase == 0 ? null : phase == 1 ? Form.WindClip : Form.StrikeClip;
            FormClipT = ft;
            if (Anim != null) { Anim.FormClip = FormClip; Anim.FormClipT = FormClipT; }
        }
        if (r.More) { byte b = r.Byte(); _netBolts = b & 15; _netBoltMax = b >> 4; }
        _net.Push(now, pos, vel);
        Dead = (_netFlags & HfDead) != 0;
        ShieldRaised = (_netFlags & HfShield) != 0;
        Charged = (_netFlags & HfCharged) != 0 ? Math.Max(1, Charged) : 0;
        Choosing = (_netFlags & HfChoosing) != 0;
        ShieldHp = _netShieldStrength * Stats.ShieldMax;
    }

    /// <summary>A puppet between updates.</summary>
    private void PuppetTick(float dt)
    {
        _animT += dt;
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        if (_invuln > 0) _invuln -= dt;
        if (_net.Sample(NetSync.Now - NetSync.InterpDelay, out var pos, out var vel))
        {
            GlobalPosition = pos;
            Velocity = vel;
        }
        var cave = G.Cave;
        if (cave != null)
        {
            InWater = cave.IsWater(GlobalPosition + new Vector2(0, 2));
            HeadUnder = cave.IsWater(GlobalPosition + new Vector2(0, -9));
        }
        if (Anim != null)
        {
            if (_netAnim != "") Anim.Mirror(_netAnim, _netFrame, _netSpeed, Facing < 0 ? -1 : 1);
            Anim.Motion(InWater ? Velocity * 0.3f : Velocity);
            float a = NetInvuln && !Dead && (int)(_animT * 20) % 2 == 0 ? 0.45f : 1f;
            // (a friend hidden in the shadows shows only faintly)
            if (Hidden && !Dead) a = 0.3f;
            Anim.Modulate = new Color(1, 1, 1, a);
        }
        // the blade's sweep plays out here from its start (sent as it began)
        if (_swingT >= 0)
        {
            _swingT += dt;
            if (!_released && SweepT >= 0) _released = true;
            float follow = _heave ? Tune.Swordsman.HeaveRecover : _windup * 1.6f;
            if (SweepT > _active + follow) _swingT = -1;
        }
        QueueRedraw();
    }

    /// <summary>A puppet began a swing (for the sweep of light).</summary>
    public void NetSwing(Vector2 dir, float arc, float reach, float windup, float active, int combo, bool finisher, bool charged, bool heave)
    {
        _swingDir = dir.LengthSquared() > 0.01f ? dir.Normalized() : new Vector2(Facing, 0);
        _swingArc = arc;
        _swingReach = reach;
        _windup = Math.Max(0.01f, windup);
        _active = Math.Max(0.01f, active);
        _comboStep = combo;
        _finisher = finisher;
        _swingCharged = charged;
        _heave = heave;
        _swingT = 0;
        _released = false;
    }

    /// <summary>A puppet fell, or got back up.</summary>
    public void NetDown(bool down)
    {
        Dead = down;
        if (!down) _net.Clear();
    }

    /// <summary>A key picked up (online, handed over by the host): one more carried.</summary>
    public void GainKey()
    {
        if (Keys >= Tune.Vault.MaxKeys) return;
        Keys++;
        G.Sfx.Play("chest", GlobalPosition, -4, 0, 1.8f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -20), "+KEY", new Color(1f, 0.82f, 0.4f), 11, 1f);
    }

    /// <summary>A key spent on a vault's gate.</summary>
    public void SpendKey()
    {
        if (Keys > 0) Keys--;
    }

    /// <summary>Back on your feet (a friend held on long enough), with a share of your health.</summary>
    public void Revive()
    {
        if (!Dead || IsRemote) return;
        Dead = false;
        ClearStatus();
        Hp = Math.Max(1f, Stats.MaxHp * 0.35f);
        _invuln = 2f;
        Anim.CancelOnce();
        Anim.Modulate = Colors.White;
        Anim.Flash(0.8f);
        Anim.FlashColor = HealColorLight;
        G.Fx.Ring(GlobalPosition, 24, HealColorLight, 0.5f);
        for (int k = 0; k < 12; k++) G.Fx.Ember(GlobalPosition + G.RandDir() * 12, HealColor);
        G.Sfx.Play("levelup", GlobalPosition, -4, 0, 1.2f);
        NetSync.HeroDown(false);
        G.Main?.OnPlayerRevived();
    }

    /// <summary>A potion handed over by the host (online): one more on the belt.</summary>
    public void GainPotion()
    {
        if (Potions >= MaxPotions) return;
        Potions++;
        G.Sfx.Play("chest", GlobalPosition, -6, 0, 1.4f);
        G.Fx.Text(GlobalPosition + new Vector2(0, -16), "+POTION", new Color(1f, 0.55f, 0.7f), 11, 1f);
    }

    /// <summary>
    /// Online: holding interact over a fallen friend for two seconds gets them back up (the only
    /// way back once down; if everyone falls, the run is over).
    /// </summary>
    private void TickRevive(in PlayerInput inp, float dt)
    {
        ReviveTarget = null;
        if (!Net.Online || Dead)
        {
            ReviveProgress = 0;
            return;
        }
        Player fallen = null;
        float best = 36f;
        foreach (var p in G.Players)
        {
            if (p == this || !p.Dead || !p.IsRemote) continue;
            float d = p.GlobalPosition.DistanceTo(GlobalPosition);
            if (d < best) { best = d; fallen = p; }
        }
        ReviveTarget = fallen;
        if (fallen == null || !inp.InteractHeld) { ReviveProgress = 0; return; }
        ReviveProgress += dt / 2f;
        if (G.Chance(0.3f)) G.Fx.Ember(fallen.GlobalPosition + new Vector2(G.Range(-10, 10), G.Range(-4, 8)), HealColor);
        if (ReviveProgress < 1f) return;
        ReviveProgress = 0;
        NetSync.Revive(fallen);
        G.Fx.Ring(fallen.GlobalPosition, 24, HealColorLight, 0.5f);
    }
}

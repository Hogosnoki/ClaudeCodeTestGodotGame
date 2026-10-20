using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The elemental Warden (earth, frost, wind; <see cref="Tune.ElementalWarden"/>): his three buttons each take an element, and two charges
/// readied make the pair's own version of any of them (every pair has one on each button, <see cref="Elements.DualName"/>).
/// The attack (the shortsword): Stonecutter, Rime Edge, Gale Edge; Boulder Strike, Icicle Lance, Cyclone Slash, Frozen Earth, Sandblast,
/// Frost Gust. The block (the shield raised with charges readied; the guard lasts as long as it stays up): Stone Guard, Rime Guard, Gale
/// Guard; Bastion, Glacial Bulwark, Eye of the Storm, Permafrost Guard, Dust Wall, Hoarfrost Gale. The dash (the Guarded Charge, which in
/// every version runs through creatures instead of stopping at one): Rockslide Charge, Frozen Tundra, Gale Charge; Avalanche, Glacier
/// Charge, Tempest Charge, Frostquake, Duststorm, Blizzard Charge. His Shield Bash isn't in the elemental kit; Taunt is as it was.
/// </summary>
public partial class Player
{
    /// <summary>The version on the swing under way (for its blows), and its elements.</summary>
    private string _wSwing = "";
    private HeroElement _wA, _wB;
    /// <summary>The guard on the shield (while it stays up), its elements and how long it can last.</summary>
    private string _guard = "";
    private HeroElement _guardA, _guardB;
    private float _guardT, _guardTick;
    /// <summary>The version of the charge under way, its elements, its speed, where it began; what it carries along (the Tempest).</summary>
    private string _wDash = "";
    private HeroElement _dashA, _dashB;
    private float _dashSpeedMult = 1f, _dashCarryT, _dashDustAt;
    private Vector2 _dashFrom;
    private readonly List<Enemy> _carried = new();
    /// <summary>Clouds of dust left by a Dust Wall or a Duststorm: where, and how long each has left (creatures in them are sapped).</summary>
    private readonly List<(Vector2 at, float t)> _dusts = new();
    private float _dustTick;

    /// <summary>For the HUD and the tests: the guard on the shield now ("" none) and the version of the charge under way.</summary>
    public string GuardName => _guard;
    public float GuardLeft => _guard != "" ? _guardT : 0f;
    public HeroElement GuardElement => _guard != "" ? _guardA : HeroElement.None;
    public string DashVersion => _wDash;

    private float WardBlow => BaseDamage * Stats.DamageMult;

    /// <summary>What a Warden button spends: the pair (every pair has a version on each of his buttons), or the oldest charge.</summary>
    private (string name, HeroElement a, HeroElement b) SpendFor(ElemSlot slot)
    {
        if (DualReady)
        {
            string n = DualNameFor(slot);
            var (a, b) = SpendPair();
            return (n, a, b);
        }
        var e = SpendElement();
        return (Elements.SingleName(Stats.Hero, slot, e), e, HeroElement.None);
    }

    private static bool PairHas(HeroElement a, HeroElement b, HeroElement x) => a == x || b == x;

    private void WardUsed(string name, HeroElement a, HeroElement b)
    {
        _lastEl = a;
        ElementalUses.Add(name);
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        G.Fx.Text(GlobalPosition + new Vector2(0, -32), name.ToUpperInvariant(), col, b == HeroElement.None ? 11 : 12, 0.8f);
    }

    /// <summary>The ground under a point (or the point itself, low as the feet, where there's none near).</summary>
    private Vector2 FloorAt(Vector2 p) => Ground.Below(p, 60, out var floor) ? floor : p + new Vector2(0, 13);

    /// <summary>Every creature in a cone in front: within <paramref name="range"/>, <paramref name="halfDeg"/> degrees either side of <paramref name="dir"/>.</summary>
    private IEnumerable<Enemy> InCone(Vector2 origin, Vector2 dir, float range, float halfDeg)
    {
        float half = Mathf.DegToRad(halfDeg);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - origin;
            if (to.Length() - e.HitRadius > range) continue;
            if (to.Length() > 12 && Math.Abs(dir.AngleTo(to)) > half + MathF.Atan2(e.HitRadius, Math.Max(1f, to.Length()))) continue;
            if (!G.Cave.LineClear(origin, e.GlobalPosition)) continue;
            yield return e;
        }
    }

    private void TickWardenElements(float dt)
    {
        if (_guard != "")
        {
            if (!ShieldRaised || ShieldBroken || (_guardT -= dt) <= 0) EndGuard();
            else TickGuard(dt);
        }
        if (_dusts.Count > 0)
        {
            for (int k = _dusts.Count - 1; k >= 0; k--)
            {
                var d = _dusts[k];
                d.t -= dt;
                if (d.t <= 0) _dusts.RemoveAt(k); else _dusts[k] = d;
            }
            if ((_dustTick -= dt) <= 0)
            {
                _dustTick = 0.5f;
                foreach (var e in G.Enemies.ToArray())
                {
                    if (e.Dead || !e.CanBeHit) continue;
                    foreach (var d in _dusts)
                        if (e.GlobalPosition.DistanceTo(d.at) < Tune.ElementalWarden.DustRadius + e.HitRadius) { e.Sap(1.2f); break; }
                }
            }
        }
    }

    // ================================================================== the attack

    /// <summary>The attack button: the shortsword, or with charges readied the element's (or the pair's) cut.</summary>
    private bool WardenSwing(Vector2 aim, bool held)
    {
        if (_swingCd > 0 || (held && _dodgeT > 0)) return false;
        if (Infused == HeroElement.None) return TrySwing(aim, held);
        var (name, a, b) = SpendFor(ElemSlot.Attack);
        if (_dodgeT > 0) _dodgeT = 0;
        StartSwing(aim);
        _wSwing = name; _wA = a; _wB = b;
        var dir = _swingDir;
        var origin = GlobalPosition + new Vector2(0, -3);
        switch (name)
        {
            case "Stonecutter":
                _swingDmg *= Tune.ElementalWarden.EdgeDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                G.Fx.Debris(origin + dir * 14, Elements.ColorOf(HeroElement.Earth), 4, 120);
                break;
            case "Rime Edge":
                _swingDmg *= Tune.ElementalWarden.EdgeDamage; _swingEl = HeroElement.Frost; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                G.Fx.Burst(origin + dir * 14, new Color(0.9f, 0.97f, 1f), 6, 70, 1.8f, 0.3f, 40);
                break;
            case "Gale Edge":
                _swingReach *= Tune.ElementalWarden.GaleReach; _swingEl = HeroElement.Wind;
                G.Fx.Directional(origin + dir * 8, dir, 0.4f, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 8, 260, 1.8f, 0.25f, 0, 1);
                break;
            case "Boulder Strike":
                // a heavy blow of stone: stunned, the frozen shattered, tossed
                _swingDmg *= Tune.ElementalWarden.BoulderDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.Elements.Full; _swingShatter = true;
                G.Fx.Debris(origin + dir * 16, Elements.ColorOf(HeroElement.Earth), 8, 180);
                G.Fx.AddShake(2.5f);
                break;
            case "Icicle Lance":
                // the cut, and a lance of ice thrown on along the aim
                _swingEl = HeroElement.Frost; _swingBuild = Tune.ElementalWarden.EdgeBuild;
                var lance = new ElementBolt
                {
                    Position = origin + dir * 10f, Dir = dir, Range = Tune.ElementalWarden.LanceRange, Caster = this, Frost = true,
                    BuildMult = Tune.Elements.Full / Math.Max(1f, Tune.ElementalStaff.FrostBuild), Damage = WardBlow * Tune.ElementalWarden.LanceDamage,
                };
                G.Spawn(lance);
                NetSync.HeroVisual(lance);
                G.Sfx.Play("clink", origin, -4, 0.05f, 1.5f);
                break;
            case "Cyclone Slash":
                // a whirling cut all round
                _swingArc = Mathf.Tau; _swingReach *= Tune.ElementalWarden.CycloneReach; _swingDmg *= Tune.ElementalWarden.CycloneDamage; _swingEl = HeroElement.Wind;
                G.Fx.Ring(origin, _swingReach * 0.8f, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 0.3f, FxLayer.RingStyle.Burst);
                for (int k = 0; k < 10; k++) G.Fx.Directional(origin + Vector2.Right.Rotated(k * Mathf.Tau / 10f) * 12f, Vector2.Right.Rotated(k * Mathf.Tau / 10f + 1.4f), 0.2f, new Color(0.85f, 1f, 0.9f, 0.7f), 1, 220, 1.6f, 0.25f, 0, 1);
                G.Sfx.Play("dodge", origin, -2, 0.05f, 0.8f);
                break;
            case "Frozen Earth":
                _swingDmg *= Tune.ElementalWarden.FrozenDamage; _swingEl = HeroElement.Earth; _swingBuild = Tune.ElementalWarden.FrozenBuild; _swingShatter = true;
                G.Fx.Burst(origin + dir * 14, new Color(0.9f, 0.97f, 1f), 6, 80, 1.8f, 0.3f, 40);
                G.Fx.Debris(origin + dir * 14, Elements.ColorOf(HeroElement.Earth), 4, 120);
                break;
            case "Sandblast":
                // a cone of grit thrown off the cut: nicked and sapped
                _swingEl = HeroElement.Earth;
                foreach (var e in InCone(origin, dir, Tune.ElementalWarden.ConeRange, Tune.ElementalWarden.ConeHalf))
                {
                    float d = e.Hurt(WardBlow * Tune.ElementalWarden.SandDamage, dir * 40f, e.GlobalPosition, DamageKind.Physical);
                    if (d > 0) OnDealtDamage(d);
                    if (!e.Dead) e.Sap(Tune.ElementalWarden.SandSap);
                }
                G.Fx.Directional(origin, dir, Mathf.DegToRad(Tune.ElementalWarden.ConeHalf), new Color(0.72f, 0.6f, 0.42f, 0.85f), 22, 300, 2.4f, 0.4f, 60, 0);
                G.Fx.Dust(origin + dir * 30, 6, 2f, new Color(0.6f, 0.5f, 0.38f, 0.6f));
                G.Sfx.Play("rock", origin, -6, 0.05f, 1.3f);
                break;
            case "Frost Gust":
                // a freezing wind off the cut: filled with frost and driven back
                _swingEl = HeroElement.Frost;
                foreach (var e in InCone(origin, dir, Tune.ElementalWarden.ConeRange, Tune.ElementalWarden.ConeHalf))
                {
                    e.Build(HeroElement.Frost, Tune.ElementalWarden.GustBuild, Stats.DamageMult, this);
                    e.Shove(dir * Tune.ElementalWarden.GustShove + new Vector2(0, -50f));
                }
                G.Fx.Directional(origin, dir, Mathf.DegToRad(Tune.ElementalWarden.ConeHalf), new Color(0.88f, 0.97f, 1f, 0.85f), 22, 320, 2.2f, 0.4f, 0, 1);
                G.Sfx.Play("dodge", origin, -4, 0.05f, 0.7f);
                break;
        }
        WardUsed(name, a, b);
        return true;
    }

    /// <summary>A blow of the Warden's elemental cut lands: what the version adds beyond its build-up.</summary>
    private void WardenBladeHit(Enemy e)
    {
        if (_wSwing == "" || e.Dead) return;
        var away = (e.GlobalPosition - GlobalPosition).Normalized();
        switch (_wSwing)
        {
            case "Gale Edge": e.Shove(away * Tune.ElementalWarden.GaleShove + new Vector2(0, -60f)); break;
            case "Cyclone Slash": e.Shove(away * Tune.ElementalWarden.CycloneShove + new Vector2(0, -80f)); break;
            case "Boulder Strike": e.Shove(new Vector2(away.X * 140f, -Tune.ElementalWarden.BoulderToss)); break;
            case "Frozen Earth": e.Build(HeroElement.Frost, Tune.ElementalWarden.FrozenBuild, Stats.DamageMult, this); break;
        }
    }

    // ================================================================== the block

    /// <summary>The shield goes up with charges readied: the guard of the element (or the pair) holds while it stays up.</summary>
    private void GuardRaised()
    {
        if (!Elemental || Infused == HeroElement.None) return;
        var (name, a, b) = SpendFor(ElemSlot.Heavy);
        _guard = name; _guardA = a; _guardB = b;
        _guardT = Tune.ElementalWarden.GuardSeconds;
        _guardTick = 0f;
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        var front = GlobalPosition + new Vector2(0, -3) + ShieldDir * 16f;
        G.Fx.Ring(front, 16, new Color(col, 0.95f), 0.3f, FxLayer.RingStyle.Burst);
        switch (name)
        {
            case "Glacial Bulwark":
            {
                // a wall of ice rises in front (creatures go for it; friends walk through it)
                float side = Math.Abs(ShieldDir.X) > 0.2f ? Math.Sign(ShieldDir.X) : Facing;
                var at = FloorAt(GlobalPosition + new Vector2(side * 26f, 0));
                var wall = new FrostTower
                {
                    Position = at, Caster = this, Height = Tune.ElementalWarden.WallHeight, Half = Tune.ElementalWarden.WallHalf, Seconds = Tune.ElementalWarden.WallSeconds,
                    MaxHp = Tune.ElementalWarden.WallHp * Stats.MaxHp / Math.Max(1f, Tune.Warden.StartHp),
                };
                G.Spawn(wall);
                LastTower = wall;
                break;
            }
            case "Permafrost Guard":
                Launch(new TundraField { Position = FloorAt(GlobalPosition), Half = Tune.ElementalWarden.PermafrostHalf, Seconds = Tune.ElementalWarden.GuardSeconds });
                break;
            case "Dust Wall":
            {
                var at = GlobalPosition + ShieldDir * 30f;
                var cloud = new SmokeCloud { Position = at, Radius = Tune.ElementalWarden.DustRadius, Life = Tune.ElementalWarden.DustSeconds };
                G.Spawn(cloud);
                NetSync.HeroVisual(cloud);
                _dusts.Add((at, Tune.ElementalWarden.DustSeconds));
                G.Fx.Dust(at, 10, 2.4f, new Color(0.62f, 0.52f, 0.4f, 0.7f));
                break;
            }
            case "Eye of the Storm":
                G.Fx.Ring(GlobalPosition, Tune.ElementalWarden.StormRadius, new Color(Elements.ColorOf(HeroElement.Wind), 0.8f), 0.4f, FxLayer.RingStyle.Dashed);
                break;
        }
        G.Sfx.Play(a == HeroElement.Frost ? "clink" : a == HeroElement.Wind ? "dodge" : "rock", GlobalPosition, -6, 0.05f, 0.8f);
        WardUsed(name, a, b);
    }

    private void EndGuard() { _guard = ""; _guardA = _guardB = HeroElement.None; }

    /// <summary>The guard while it holds: its element about the shield, and the Eye of the Storm's ring of wind.</summary>
    private void TickGuard(float dt)
    {
        var col = _guardB == HeroElement.None ? Elements.ColorOf(_guardA) : Elements.DualColor(_guardA, _guardB);
        var front = GlobalPosition + new Vector2(0, -3) + ShieldDir * 16f;
        if (Engine.GetPhysicsFrames() % 4 == 0) G.Fx.Burst(front + G.RandDir() * 6f, new Color(col, 0.7f), 1, 20, 1.6f, 0.3f, 0, 0);
        if ((_guardTick -= dt) > 0) return;
        _guardTick = 0.1f;
        if (_guard != "Eye of the Storm") return;
        var me = GlobalPosition + new Vector2(0, -4);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit) continue;
            var to = e.GlobalPosition - me;
            if (to.Length() > Tune.ElementalWarden.StormRadius + e.HitRadius) continue;
            e.Shove(to.Normalized() * Tune.ElementalWarden.StormShove + new Vector2(0, -40f));
        }
        foreach (var pr in G.Main.EnemyProjectiles.ToArray())
            if (IsInstanceValid(pr) && !pr.IsQueuedForDeletion() && !pr.Reflected && pr.GlobalPosition.DistanceTo(me) < Tune.ElementalWarden.StormRadius) pr.Deflect();
        for (int k = 0; k < 3; k++)
        {
            float a = G.Range(0f, Mathf.Tau);
            G.Fx.Directional(me + Vector2.Right.Rotated(a) * Tune.ElementalWarden.StormRadius * 0.8f, Vector2.Right.Rotated(a + 1.6f), 0.2f, new Color(col, 0.6f), 1, 180, 1.5f, 0.25f, 0, 1);
        }
    }

    /// <summary>The guard covers all round (Bastion), or the shield's own arc.</summary>
    private bool GuardAllRound => _guard == "Bastion";
    /// <summary>Stone in the guard: nothing gets through, and the shield doesn't wear.</summary>
    private bool GuardStone => _guard != "" && PairHas(_guardA, _guardB, HeroElement.Earth);
    /// <summary>Wind in the guard: shots blocked go back where they came from.</summary>
    private bool GuardReflects => _guard != "" && PairHas(_guardA, _guardB, HeroElement.Wind);

    /// <summary>A melee blow met the guard: its elements answer the striker.</summary>
    private void GuardStruck(Enemy melee)
    {
        if (_guard == "" || melee == null || !IsInstanceValid(melee) || melee.Dead) return;
        var away = (melee.GlobalPosition - GlobalPosition).Normalized();
        float power = Stats.DamageMult;
        if (PairHas(_guardA, _guardB, HeroElement.Earth)) melee.Build(HeroElement.Earth, Tune.Elements.Full, power, this);
        if (PairHas(_guardA, _guardB, HeroElement.Frost))
        {
            melee.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
            // (two of frost: chilled, then frozen solid at once)
            if (_guardA == HeroElement.Frost && _guardB == HeroElement.Frost && !melee.Dead) melee.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
        }
        if (PairHas(_guardA, _guardB, HeroElement.Wind) && !melee.Dead) melee.Shove(away * Tune.ElementalWarden.GuardShove + new Vector2(0, -80f));
        AttributeHit(melee, GlobalPosition);
        var col = _guardB == HeroElement.None ? Elements.ColorOf(_guardA) : Elements.DualColor(_guardA, _guardB);
        G.Fx.Ring(melee.GlobalPosition, melee.HitRadius + 8, new Color(col, 0.9f), 0.3f, FxLayer.RingStyle.Burst);
        GuardAnswered++;
    }
    /// <summary>For the tests: melee blows a guard has answered.</summary>
    public int GuardAnswered { get; private set; }

    // ================================================================== the dash

    /// <summary>The Guarded Charge with charges readied: the element's (or the pair's) charge, which runs through creatures.</summary>
    private void WardenDashVersion()
    {
        var (name, a, b) = SpendFor(ElemSlot.Move);
        _wDash = name; _dashA = a; _dashB = b;
        _dashSpeedMult = 1f;
        _dashFrom = GlobalPosition;
        _dashCarryT = 0f;
        _dashDustAt = 0f;
        _carried.Clear();
        switch (name)
        {
            case "Gale Charge": case "Blizzard Charge": case "Duststorm":
                _dashT *= Tune.ElementalWarden.GaleTime; _dashSpeedMult = Tune.ElementalWarden.GaleSpeed; break;
            case "Tempest Charge":
                _dashT *= Tune.ElementalWarden.TempestTime; _dashSpeedMult = Tune.ElementalWarden.GaleSpeed; break;
        }
        var col = b == HeroElement.None ? Elements.ColorOf(a) : Elements.DualColor(a, b);
        G.Fx.Directional(GlobalPosition, -_dashDir, 0.6f, new Color(col, 0.85f), 12, 220, 2f, 0.3f, 0);
        WardUsed(name, a, b);
    }

    /// <summary>The elemental charge meets what's in its way: each creature once, as the version does; friends passed by a wind get a tailwind.</summary>
    private void WardenDashPass(Vector2 front, float dt)
    {
        float power = Stats.DamageMult;
        bool earth = PairHas(_dashA, _dashB, HeroElement.Earth), frost = PairHas(_dashA, _dashB, HeroElement.Frost), wind = PairHas(_dashA, _dashB, HeroElement.Wind);
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(front) > e.HitRadius + 12) continue;
            if (!_dashStruck.Add(e)) continue;
            if (earth)
            {
                e.Interrupt(_dashDir * Tune.Warden.DashPush, Tune.Warden.DashStagger);
                float d = e.Hurt(WardBlow * Tune.ElementalWarden.PloughDamage, _dashDir * 160f, e.GlobalPosition, DamageKind.Physical);
                if (d > 0) OnDealtDamage(d);
            }
            if (frost && !e.Dead)
            {
                bool glacier = _dashA == HeroElement.Frost && _dashB == HeroElement.Frost;
                e.Build(HeroElement.Frost, glacier ? Tune.Elements.Full : wind ? Tune.ElementalWarden.GustBuild : Tune.ElementalWarden.PassBuild, power, this);
                if (glacier && !e.Dead) e.Build(HeroElement.Frost, Tune.Elements.Full, power, this);
            }
            if (wind && !e.Dead)
            {
                if (_wDash == "Tempest Charge") _carried.Add(e);
                else e.Shove(new Vector2(_dashDir.X * Tune.ElementalWarden.AsideShove * 0.4f, -Tune.ElementalWarden.AsideShove));
            }
            AttributeHit(e, front - _dashDir * 10f);
            G.Fx.Spark(e.GlobalPosition, _dashDir, true, Elements.ColorOf(_dashA));
        }
        if (wind)
            foreach (var h in G.Players)
                if (h != this && h != null && IsInstanceValid(h) && !h.Dead && h.GlobalPosition.DistanceTo(front) < 22f) h.GiveBuff(BuffTailwind, Tune.ElementalWarden.TailwindSeconds);
        // the Tempest carries what it took up along in its wake
        if (_carried.Count > 0 && (_dashCarryT -= dt) <= 0)
        {
            _dashCarryT = 0.05f;
            foreach (var e in _carried)
                if (IsInstanceValid(e) && !e.Dead) e.Shove((GlobalPosition + _dashDir * 18f + new Vector2(0, -10f) - e.GlobalPosition) * 8f + _dashDir * Tune.Warden.DashSpeed * _dashSpeedMult * 0.8f);
        }
        // the Duststorm leaves clouds along its path
        if (_wDash == "Duststorm" && GlobalPosition.DistanceTo(_dashFrom) >= _dashDustAt)
        {
            _dashDustAt += Tune.ElementalWarden.DustRadius * 1.2f;
            var cloud = new SmokeCloud { Position = GlobalPosition, Radius = Tune.ElementalWarden.DustRadius * 0.8f, Life = Tune.ElementalWarden.StormDustSeconds };
            G.Spawn(cloud);
            NetSync.HeroVisual(cloud);
            _dusts.Add((GlobalPosition, Tune.ElementalWarden.StormDustSeconds));
        }
        // (its element trailing behind)
        if (Engine.GetPhysicsFrames() % 2 == 0)
            G.Fx.Burst(GlobalPosition + G.RandDir() * 6f, new Color(_dashB == HeroElement.None ? Elements.ColorOf(_dashA) : Elements.DualColor(_dashA, _dashB), 0.8f), 1, 30, 2f, 0.35f, earth ? 300 : 0, 0);
    }

    /// <summary>The elemental charge is over: the quake, the frozen ground, the ice bursting up, the Tempest's fling.</summary>
    private void WardenDashEnd()
    {
        if (_wDash == "") return;
        string name = _wDash;
        _wDash = "";
        var feet = GlobalPosition + new Vector2(0, 12);
        float power = Stats.DamageMult;
        bool earth = PairHas(_dashA, _dashB, HeroElement.Earth), frost = PairHas(_dashA, _dashB, HeroElement.Frost);
        if (earth)
        {
            bool big = name == "Avalanche";
            float r = big ? Tune.ElementalWarden.AvalancheRadius : Tune.ElementalWarden.QuakeRadius;
            float dmg = WardBlow * (big ? Tune.ElementalWarden.AvalancheDamage : Tune.ElementalWarden.QuakeDamage);
            foreach (var e in G.Enemies.ToArray())
            {
                if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(feet) > r + e.HitRadius) continue;
                var away = new Vector2(Math.Sign(e.GlobalPosition.X - feet.X), -0.8f).Normalized();
                float d = e.Hurt(dmg, away * 120f, e.GlobalPosition, DamageKind.Physical);
                if (d > 0) OnDealtDamage(d);
                if (!e.Dead) { e.Build(HeroElement.Earth, Tune.Elements.Full, power, this); e.Shove(away * 200f); }
            }
            Breakables.SpellBurst(feet, r * 0.5f);
            var col = Elements.ColorOf(HeroElement.Earth);
            G.Fx.Shockwave(feet, r, new Color(col, 0.75f), 0.35f);
            G.Fx.Debris(feet, col.Darkened(0.2f), big ? 18 : 12, 240);
            G.Fx.AddShake(big ? 5f : 3.5f);
            G.Sfx.Play("slam", feet, -2, 0.05f, 0.85f);
            if (big) Launch(new Fissure { Position = FloorAt(GlobalPosition), Dir = Math.Abs(_dashDir.X) > 0.1f ? Math.Sign(_dashDir.X) : Facing, Range = Tune.ElementalWarden.FissureRange, Damage = WardBlow * 1.2f });
        }
        if (name == "Frostquake")
            Launch(new IceEruption { Position = FloorAt(GlobalPosition + new Vector2(Facing * 24f, 0)), Damage = WardBlow * Tune.ElementalWarden.FrostquakeDamage });
        else if (frost)
        {
            // the ground it crossed freezes over
            var from = FloorAt(_dashFrom);
            var to = FloorAt(GlobalPosition);
            float half = Math.Abs(to.X - from.X) * 0.5f + Tune.ElementalWarden.TundraPad;
            Launch(new TundraField { Position = from.Lerp(to, 0.5f), Half = half, Seconds = Tune.ElementalWarden.TundraSeconds });
        }
        if (name == "Tempest Charge")
        {
            foreach (var e in _carried)
                if (IsInstanceValid(e) && !e.Dead) e.Shove(new Vector2(_dashDir.X * Tune.ElementalWarden.TempestFling, -Tune.ElementalWarden.TempestFling * 0.6f));
            G.Fx.Shockwave(GlobalPosition, 60, new Color(Elements.ColorOf(HeroElement.Wind), 0.6f), 0.35f);
            G.Sfx.Play("dodge", GlobalPosition, -2, 0.05f, 0.6f);
            _carried.Clear();
        }
    }
}

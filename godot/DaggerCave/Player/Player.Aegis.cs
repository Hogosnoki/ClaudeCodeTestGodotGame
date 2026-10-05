using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Aegis's kit. The attack button flings a ward bolt: light that homes on a creature and bursts on contact, hurting it and
/// the ones beside it a little, making them all deal less damage for a few seconds, and mending the Aegis a little. The ability
/// button puts a barrier round an ally (or, with no ally near, the Aegis); the second ability shares a burden (a part of every
/// blow an ally takes falls on the Aegis instead); the dodge button wraps an ally in a bubble that absorbs half of every blow until
/// it bursts, and lets them breathe under water. Alteration: the Bubble goes on a creature instead, softening its blows taken while
/// the rest builds, then bursting.
/// </summary>
public partial class Player
{
    private bool IsAegis => Stats.Hero == HeroKind.Aegis;

    private float _wardCd, _burdenCd, _bubbleCd;

    // ---- a bubble round this hero (from an Aegis)
    /// <summary>What the bubble can still absorb (0 = none).</summary>
    public float BubbleHp { get; private set; }
    private float _bubbleLeft;
    private bool _bubbleHeal;
    /// <summary>Wrapped in a bubble (a puppet goes by its game's flags).</summary>
    private float _bubbleCap, _netBubbleFrac;
    /// <summary>How much of its bubble is left (0..1): the bubble shrinks as it soaks.</summary>
    public float BubbleFrac => IsRemote ? ((_netFlags & HfBubble) != 0 ? Math.Max(0.05f, _netBubbleFrac) : 0f) : _bubbleCap > 0 ? Math.Clamp(BubbleHp / _bubbleCap, 0f, 1f) : 0f;
    /// <summary>The bubble's radius (px): from a snug shell when nearly spent to a roomy one when full.</summary>
    public float BubbleRadius => Tune.Aegis.BubbleRadiusMin + (Tune.Aegis.BubbleRadiusMax - Tune.Aegis.BubbleRadiusMin) * MathF.Sqrt(BubbleFrac);
    public bool Bubbled => IsRemote ? (_netFlags & HfBubble) != 0 : BubbleHp > 0.01f;

    // ---- a Shared Burden on this hero (the Aegis who took it, how much of each blow, for how long)
    private int _burdenBy;
    private float _burdenShare, _burdenLeft;
    /// <summary>A Shared Burden is on this hero.</summary>
    public bool Burdened => _burdenLeft > 0;
    // ---- the Aegis's own: who it carries for
    private readonly List<Player> _burdenTargets = new();
    private float _burdenTargetLeft;
    /// <summary>Whom the Aegis carries for (the first of them), and how long is left (for the HUD).</summary>
    public Player BurdenTarget => _burdenTargets.FirstOrDefault(p => p != null && IsInstanceValid(p) && !p.Dead) is Player t && _burdenTargetLeft > 0 ? t : null;
    public int BurdenCount => _burdenTargetLeft > 0 ? _burdenTargets.Count(p => p != null && IsInstanceValid(p) && !p.Dead) : 0;
    public float BurdenLeft => _burdenTargetLeft;
    /// <summary>How much of the bubble's recharge is left (0 = ready, 1 = just used).</summary>
    public float BubbleCooldownFrac => Math.Clamp(_bubbleCd / Math.Max(0.01f, Tune.Aegis.BubbleCooldown * Stats.BubbleCdMult), 0f, 1f);

    /// <summary>Test aid: every ability ready again.</summary>
    public void TestResetCooldowns() { _wardCd = _burdenCd = _bubbleCd = 0; }

    private void TickAegis(float dt)
    {
        _wardCd -= dt; _burdenCd -= dt; _bubbleCd -= dt;
        _castGlow = Math.Max(0f, _castGlow - dt * 2.5f);
        if (_burdenTargetLeft > 0 && (_burdenTargetLeft -= dt) <= 0) _burdenTargets.Clear();
        // (a friend who's gone, or fallen, is no longer carried)
        _burdenTargets.RemoveAll(p => p == null || !IsInstanceValid(p) || p.Dead);
        if (_burdenTargets.Count == 0) _burdenTargetLeft = 0;
    }

    // ---------------------------------------------------------------- the ward bolt

    private bool CastWardBolt(Vector2 aim, bool held = false)
    {
        if (_wardCd > 0) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        float range = Tune.Aegis.BoltRange;
        var target = FindSpellTarget(aim, range, Tune.Elementalist.BoltConeDegrees);
        var dir = target != null ? (target.GlobalPosition - CastPoint).Normalized() : aim;
        _wardCd = Tune.Aegis.BoltEvery / Math.Max(0.2f, Stats.AttackSpeed);
        AttacksStarted++;
        if (Math.Abs(dir.X) > 0.15f) Facing = Math.Sign(dir.X);
        CastDir = dir;
        Anim.Face((int)Facing, instant: true);
        Anim.Once("cast", 3, 1.6f);
        LastCast = "ward";
        _castGlow = 0.7f;
        float dmg = Tune.Aegis.BoltDamage * Stats.DamageMult * Stats.PrimaryDamageMult;
        var bolt = new ElementBolt { Position = CastPoint + dir * 6f, Dir = dir, Ward = true, Damage = dmg, Range = range, Speed = Tune.Aegis.BoltSpeed, Caster = this };
        G.Spawn(bolt);
        NetSync.HeroVisual(bolt);
        G.Fx.Flash(CastPoint, 7, bolt.Tint, 0.08f);
        G.Sfx.Play("clink", CastPoint, -10, 0.1f, 1.5f);
        return true;
    }

    /// <summary>A ward bolt met a creature: it and the ones beside it are struck and weakened, and the Aegis mended by a share of the damage.</summary>
    public void WardBoltStruck(ElementBolt bolt, Enemy e, Vector2 at)
    {
        if (!IsInstanceValid(e) || e.Dead) return;
        float reach = Tune.Aegis.BurstRadius * Stats.BurstMult;
        var struck = new List<Enemy> { e };
        foreach (var o in G.Enemies)
            if (o != e && !o.Dead && o.CanBeHit && o.GlobalPosition.DistanceTo(at) <= reach + o.HitRadius) struck.Add(o);
        float total = 0;
        float healed = 0;
        foreach (var t in struck)
        {
            float share = t == e ? 1f : Stats.BurstShare;
            if (Stats.HealingWard) healed += bolt.Damage * share;
            float dealt = t.Hurt(bolt.Damage * share * (Stats.HealingWard ? Tune.Aegis.HealingWardGlance : 1f), bolt.Dir * 40f, t == e ? at : t.GlobalPosition);
            if (dealt > 0) { total += dealt; OnDealtDamage(dealt); }
            if (!t.Dead) t.Weaken(Stats.DebuffMult, Stats.DebuffSeconds);
        }
        if (total > 0 && Stats.AegisLifesteal > 0) Heal(total * Stats.AegisLifesteal);
        if (healed > 0) HealingWardBurst(at, reach, healed);
    }

    /// <summary>Healing Ward: the burst mends the Aegis, and each friend in it, for <paramref name="amount"/>.</summary>
    private void HealingWardBurst(Vector2 at, float reach, float amount)
    {
        Heal(amount);
        foreach (var who in G.Players.ToArray())
        {
            if (who == this || who == null || !IsInstanceValid(who) || who.Dead) continue;
            if (who.GlobalPosition.DistanceTo(at) > reach + 16f) continue;
            who.Heal(amount);
            G.Fx.Beam(at, who.GlobalPosition + new Vector2(0, -6), new Color(0.6f, 1f, 0.7f, 0.9f));
        }
        G.Fx.Ring(at, reach, new Color(0.6f, 1f, 0.7f, 0.8f), 0.3f);
    }

    // ---------------------------------------------------------------- allies

    /// <summary>The friend nearest the line of aim within reach (null: none).</summary>
    private Player PickAlly(Vector2 aim)
    {
        Player best = null;
        float bestScore = float.MaxValue;
        foreach (var p in G.Players)
        {
            if (p == this || p.Dead || !IsInstanceValid(p)) continue;
            var to = p.GlobalPosition - GlobalPosition;
            float d = to.Length();
            if (d > Tune.Aegis.AllyRange) continue;
            float score = d * (1f + Math.Abs(aim.AngleTo(to)) * 1.2f);
            if (score < bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    private void CastPose(string clip, string last, float glow = 1f)
    {
        Anim.Once(clip, 3);
        LastCast = last;
        _castGlow = glow;
    }

    // ---------------------------------------------------------------- barrier

    private bool TryBarrier(Vector2 aim)
    {
        if (!IsAegis || !AbilityChargeReady) return false;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var ally = PickAlly(aim);
        SpendAbilityCharge();
        var from = CastPoint;
        void Give(Player who, float k = 1f)
        {
            // (a share of the receiver's health: as strong as the one it guards is tough)
            float amount = Tune.Aegis.BarrierShare * who.Stats.MaxHp * Stats.WardMult * Stats.BarrierMult * k;
            who.GiveBarrier(amount, Tune.Aegis.BarrierSeconds);
            if (who != this) G.Fx.Beam(from, who.GlobalPosition + new Vector2(0, -6), new Color(0.75f, 0.92f, 1f, 0.9f));
        }
        if (Stats.WideBarrier)
        {
            // (Wide Barrier: everyone near, and the Aegis, each at 60%)
            foreach (var p in G.Players.ToArray())
                if (!p.Dead && IsInstanceValid(p) && (p == this || p.GlobalPosition.DistanceTo(GlobalPosition) <= Tune.Aegis.AllyRange)) Give(p, 0.6f);
        }
        else
        {
            Give(ally ?? this);
            // (Twin Barrier: the Aegis wraps itself as well)
            if (ally != null && Stats.TwinBarrier) Give(this);
        }
        G.Sfx.Play("clink", from, -4, 0.05f, 1.2f);
        CastPose("heal", "barrier");
        return true;
    }

    // ---------------------------------------------------------------- shared burden

    /// <summary>Test aid: count as being in a party.</summary>
    public static bool TestParty;
    /// <summary>The Aegis carries friends (Shared Burden) rather than smiting: in a party, with her Bulwark Oath.</summary>
    public bool Supporting => Stats.AegisSupport && ((Net.Online && Net.Count > 1) || TestParty);
    public float SmiteCooldownFrac => Math.Clamp(_burdenCd / Tune.Aegis.SmiteCooldown, 0f, 1f);

    /// <summary>A burst of light round the Aegis: it strikes and weakens everything near.</summary>
    private bool TrySmite()
    {
        if (!IsAegis || _burdenCd > 0) return false;
        _burdenCd = Tune.Aegis.SmiteCooldown;
        float dmg = Tune.Aegis.SmiteDamage * Stats.DamageMult;
        float total = 0;
        var at = CastPoint;
        foreach (var e in G.Enemies.ToArray())
        {
            if (e.Dead || !e.CanBeHit || e.GlobalPosition.DistanceTo(at) > Tune.Aegis.SmiteRadius + e.HitRadius || !G.Cave.LineClear(at, e.GlobalPosition)) continue;
            float dealt = e.Hurt(dmg, (e.GlobalPosition - at).Normalized() * 160f, e.GlobalPosition);
            if (dealt > 0) { total += dealt; OnDealtDamage(dealt); }
            if (!e.Dead) e.Weaken(Stats.DebuffMult, Stats.DebuffSeconds);
        }
        if (total > 0 && Stats.AegisLifesteal > 0) Heal(total * Stats.AegisLifesteal);
        var col = new Color(1f, 0.9f, 0.55f);
        G.Fx.Ring(at, Tune.Aegis.SmiteRadius, col, 0.4f);
        G.Fx.Flash(at, 50, col, 0.2f);
        G.Fx.Burst(at, col, 26, 240, 2.6f, 0.5f);
        G.Fx.AddShake(3f);
        G.Sfx.Play("heal", at, -4, 0.05f, 1.5f);
        CastPose("rupture", "smite", 1f);
        return true;
    }

    private bool TryBurden(Vector2 aim)
    {
        if (!Supporting) return TrySmite();
        if (!IsAegis || _burdenCd > 0) return false;
        _burdenCd = Tune.Aegis.BurdenBlink;
        aim = aim.LengthSquared() > 0.01f ? aim.Normalized() : new Vector2(Facing, 0);
        var ally = PickAlly(aim);
        if (ally == null) { SayNo("NO ONE TO SHARE WITH"); return true; }
        // the old burdens lift (one at a time, or with Burden of Many, all round you)
        foreach (var old in _burdenTargets) if (IsInstanceValid(old)) old.GiveBurden(Net.Me, 0, 0);
        _burdenTargets.Clear();
        var who = new List<Player> { ally };
        if (Stats.BurdenAll)
            foreach (var p in G.Players)
                if (p != this && p != ally && !p.Dead && IsInstanceValid(p) && p.GlobalPosition.DistanceTo(GlobalPosition) <= Tune.Aegis.AllyRange) who.Add(p);
        float share = Stats.BurdenAll ? Stats.BurdenShare * 0.5f : Stats.BurdenShare;
        foreach (var p in who)
        {
            _burdenTargets.Add(p);
            p.GiveBurden(Net.Me, share, Stats.BurdenSeconds);
            G.Fx.Beam(CastPoint, p.GlobalPosition + new Vector2(0, -6), new Color(1f, 0.85f, 0.5f, 0.9f));
        }
        _burdenTargetLeft = Stats.BurdenSeconds;
        G.Sfx.Play("heal", CastPoint, -8, 0.05f, 0.8f);
        CastPose("rupture", "burden", 0.8f);
        return true;
    }

    /// <summary>An Aegis takes (or, with no time, lifts) a share of every blow this hero takes.</summary>
    public void GiveBurden(int aegisId, float share, float seconds)
    {
        if (Dead && seconds > 0) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Burden, share, seconds, 0, aegisId); return; }
        _burdenBy = aegisId; _burdenShare = share; _burdenLeft = seconds;
        if (seconds > 0)
        {
            G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 18, new Color(1f, 0.85f, 0.5f), 0.4f);
            G.Fx.Text(GlobalPosition + new Vector2(0, -28), "SHARED", new Color(1f, 0.88f, 0.55f), 9, 0.7f);
        }
    }

    /// <summary>The burden's share of a blow passes to the Aegis; what's left is this hero's.</summary>
    private float ShareBurden(float dmg)
    {
        if (_burdenLeft <= 0 || dmg <= 0) return dmg;
        float shared = dmg * _burdenShare;
        if (shared > 0.01f)
        {
            // (in this game: the Aegis is the local hero; in another: their game is told)
            if (_burdenBy == Net.Me) G.Player?.TakeShared(shared);
            else NetSync.SendBurden(_burdenBy, shared);
            G.Fx.Text(GlobalPosition + new Vector2(0, -24), "SHARED", new Color(1f, 0.88f, 0.55f), 8, 0.5f);
        }
        return dmg - shared;
    }

    /// <summary>The Aegis takes a share of a friend's blow (through its own softening; no flinch, no grace).</summary>
    public void TakeShared(float amount)
    {
        if (Dead || IsRemote || !IsAegis) return;
        amount = Soften(amount * (1f - Stats.BurdenSoak) * (1f - Stats.DamageReduction) * Stats.DamageTakenMult);
        if (amount <= 0.01f) return;
        TakeRawDamage(amount, "chip");
        Anim.Flash(0.4f);
    }

    // ---------------------------------------------------------------- bubble

    private bool TryBubble()
    {
        if (!IsAegis || _bubbleCd > 0) return false;
        var aim = new Vector2(Facing, 0);
        aim = CastDir.LengthSquared() > 0.01f ? CastDir : aim;
        if (Stats.HostileBubble)
        {
            // the bubble goes on a creature: softening its blows taken while the rest builds, then bursting
            var foe = FindSpellTarget(aim, Tune.Aegis.BoltRange, 40f);
            if (foe == null) { SayNo("NOTHING TO WARD"); _bubbleCd = 0.5f; return true; }
            float scale = Stats.DamageMult * G.DepthHp * Stats.WardMult;
            foe.GiveWard(Tune.Aegis.WardCap * scale, Tune.Aegis.WardBlast * scale * Stats.WardBlastMult, Tune.Aegis.WardSplash * scale * Stats.WardBlastMult, Tune.Aegis.WardSeconds);
            G.Fx.Beam(CastPoint, foe.GlobalPosition, new Color(0.6f, 0.95f, 0.9f, 0.9f));
        }
        else
        {
            // a field: every hero near the Aegis (herself too) is wrapped, each in a bubble the size of their own
            // health's share; where the bubbles touch they act as one
            foreach (var who in G.Players.ToArray())
            {
                if (who == null || !IsInstanceValid(who) || who.Dead) continue;
                if (who != this && who.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Aegis.BubbleCastRadius) continue;
                float cap = Tune.Aegis.BubbleShare * who.Stats.MaxHp * Stats.WardMult * Stats.BubbleMult;
                who.GiveBubble(cap, Tune.Aegis.BubbleSeconds, Stats.BubbleHeal);
                if (who != this) G.Fx.Beam(CastPoint, who.GlobalPosition + new Vector2(0, -6), new Color(0.6f, 0.95f, 0.9f, 0.9f));
            }
        }
        _bubbleCd = Tune.Aegis.BubbleCooldown * Stats.BubbleCdMult;
        G.Sfx.Play("bubble", CastPoint, -4, 0.05f, 1.0f);
        CastPose("hex", "bubble");
        return true;
    }

    /// <summary>A bubble that absorbs half of each blow until <paramref name="cap"/> is absorbed, lasting <paramref name="seconds"/> (and healing those near when it bursts, if asked).</summary>
    public void GiveBubble(float cap, float seconds, bool healBurst)
    {
        if (Dead) return;
        if (IsRemote) { NetSync.BoonRemote(this, NetSync.Boon.Bubble, cap, seconds, healBurst ? 1f : 0f); return; }
        BubbleHp = Math.Max(BubbleHp, cap);
        _bubbleCap = Math.Max(_bubbleCap, BubbleHp);
        _bubbleLeft = Math.Max(_bubbleLeft, seconds);
        _bubbleHeal |= healBurst;
        G.Fx.Ring(GlobalPosition + new Vector2(0, -4), 26, new Color(0.6f, 0.95f, 0.9f), 0.4f);
        G.Fx.Bubbles(GlobalPosition + new Vector2(0, -8), 6);
        G.Fx.Text(GlobalPosition + new Vector2(0, -30), "BUBBLE", new Color(0.65f, 1f, 0.92f), 9, 0.7f);
    }

    private void TickBubble(float dt)
    {
        if (BubbleHp > 0 && (_bubbleLeft -= dt) <= 0) { BubbleHp = 0; _bubbleCap = 0; _bubbleHeal = false; G.Fx.Bubbles(GlobalPosition + new Vector2(0, -8), 5); }
        if (BubbleHp > 0 && G.Chance(dt * 2f)) G.Fx.Bubbles(GlobalPosition + new Vector2(G.Range(-8, 8), -4), 1);
        if (_burdenLeft > 0 && (_burdenLeft -= dt) <= 0) _burdenLeft = 0;
    }

    /// <summary>How much two bubbles run together, 0 (apart or just kissing) to 1 (one inside the other): the lens where they overlap, over the smaller one's area.</summary>
    public static float BubbleLink(Player a, Player b)
    {
        float r1 = a.BubbleRadius, r2 = b.BubbleRadius, d = a.GlobalPosition.DistanceTo(b.GlobalPosition);
        if (d >= r1 + r2) return 0f;
        float lo = Math.Min(r1, r2);
        if (d <= Math.Abs(r1 - r2)) return 1f;
        float a1 = Mathf.Clamp((d * d + r1 * r1 - r2 * r2) / (2 * d * r1), -1f, 1f), a2 = Mathf.Clamp((d * d + r2 * r2 - r1 * r1) / (2 * d * r2), -1f, 1f);
        float lens = r1 * r1 * MathF.Acos(a1) + r2 * r2 * MathF.Acos(a2) - 0.5f * MathF.Sqrt(Math.Max(0f, (-d + r1 + r2) * (d + r1 - r2) * (d - r1 + r2) * (d + r1 + r2)));
        return Math.Clamp(lens / (MathF.PI * lo * lo), 0f, 1f);
    }

    /// <summary>
    /// The bubble takes its share of a blow, and bursts once it has taken all it can. Bubbles that
    /// touch share the load by how far they run together: alone, it all falls on this one; two
    /// barely joined, a sliver goes to the other; a clustered party splits it nearly evenly.
    /// </summary>
    private float BubbleSoak(float dmg)
    {
        if (BubbleHp <= 0.01f || dmg <= 0) return dmg;
        float want = dmg * Stats.BubbleAbsorb;
        List<(Player p, float w)> links = null;
        float sum = 1f;
        foreach (var h in G.Players)
        {
            if (h == this || h.Dead || !IsInstanceValid(h) || !h.Bubbled) continue;
            float w = BubbleLink(this, h);
            if (w < 0.03f) continue;
            (links ??= new()).Add((h, w));
            sum += w;
        }
        // (this bubble pays 1/sum of the soak: never more than it has)
        float total = Math.Min(want, BubbleHp * sum);
        float own = total / sum;
        float bubble0 = BubbleHp;
        BubbleHp -= own;
        if (links != null)
            foreach (var (h, w) in links)
            {
                float amt = total * w / sum;
                if (h.IsRemote) NetSync.BoonRemote(h, NetSync.Boon.BubbleDrain, amt, 0);
                else h.DrainBubble(amt);
            }
        if (Num.Delta(bubble0, BubbleHp) > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -26), $"{Num.Delta(bubble0, BubbleHp)} ABSORBED", new Color(0.65f, 1f, 0.92f), 9, 0.6f);
        if (BubbleHp <= 0.01f) BurstBubble();
        return dmg - total;
    }

    /// <summary>Test aid: no bubble.</summary>
    public void TestClearBubble() { BubbleHp = 0; _bubbleCap = 0; _bubbleLeft = 0; }

    /// <summary>A neighbouring bubble paid its share through this one.</summary>
    public void DrainBubble(float amount)
    {
        if (BubbleHp <= 0.01f || Dead) return;
        float b0 = BubbleHp;
        BubbleHp = Math.Max(0f, BubbleHp - amount);
        if (Num.Delta(b0, BubbleHp) > 0) G.Fx.Text(GlobalPosition + new Vector2(0, -26), $"{Num.Delta(b0, BubbleHp)} SHARED", new Color(0.65f, 1f, 0.92f), 8, 0.5f);
        if (BubbleHp <= 0.01f) BurstBubble();
    }

    private void BurstBubble()
    {
        BubbleHp = 0;
        _bubbleCap = 0;
        _bubbleLeft = 0;
        var at = GlobalPosition + new Vector2(0, -4);
        G.Fx.Burst(at, new Color(0.65f, 1f, 0.92f), 16, 160, 2f, 0.4f);
        G.Fx.Ring(at, Tune.Aegis.BurstHealRadius * (_bubbleHeal ? 0.7f : 0.35f), new Color(0.65f, 1f, 0.92f, 0.8f), 0.35f);
        G.Sfx.Play("bubble", GlobalPosition, -2, 0.1f, 1.6f);
        if (_bubbleHeal)
        {
            // Soothing Burst: everyone near it (not the one it wrapped) is mended
            foreach (var p in G.Players)
            {
                if (p == this || p.Dead || !IsInstanceValid(p) || p.GlobalPosition.DistanceTo(GlobalPosition) > Tune.Aegis.BurstHealRadius) continue;
                p.Heal(Tune.Aegis.BurstHealShare * p.Stats.MaxHp);
            }
        }
        _bubbleHeal = false;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>
    /// `--scenario=elements --hero=swordsman|warden|elementalist|vitalist|aegis|rogue`, the simple kits (<see cref="Elements.Simple"/>): an
    /// element button switches to its element (nothing goes on top; one switch a second); each of the three abilities, in each element,
    /// lands with the same damage and the same cooldown and gives what it reaches the element's touch (its build-up, by the ability's role;
    /// wind, a shove); a defensive also pulses about the hero; the second ability button does nothing; the HUD (simple_hud.png).
    /// </summary>
    private IEnumerable<object> SimpleKitRun()
    {
        var p = G.Player;
        var hero = p.Stats.Hero;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Infuse = 0; _scInput.Attack = _scInput.Ability = _scInput.Ability2 = _scInput.Dodge = false; return i; };
        foreach (var _ in ElWait(0.5f)) yield return null;
        var els = Elements.Of(hero);
        ScCheck($"the {hero} has the simple kit: {Elements.Noun(hero, KitRole.Primary)}, {Elements.Noun(hero, KitRole.Defensive)}, {Elements.Noun(hero, KitRole.Special)} in {string.Join(", ", els)}", p.SimpleKit && els.Length == 3);

        // ---- the element buttons only switch
        IEnumerable<object> Press(int slot)
        {
            while (p.InfuseCooldown > 0) yield return null;
            _scInput = new PlayerInput { Infuse = slot }; yield return null; yield return null;
            _scInput = default; yield return null;
        }
        var sig = Elements.Signature(hero);
        int si = Array.IndexOf(els, sig) + 1, o1 = si % 3 + 1, o2 = (si + 1) % 3 + 1;
        p.TestMode(HeroElement.None);
        p.TestResetModeCooldowns();
        ScCheck($"it starts in {Elements.Name(sig).ToLowerInvariant()}, its own ({p.Mode})", p.Mode == sig);
        foreach (var _ in Press(o1)) yield return null;
        ScCheck($"another element's button switches to it, nothing on top ({p.Mode}, top {p.Top}, overcharged {p.Overcharged})", p.Mode == els[o1 - 1] && p.Top == HeroElement.None && !p.Overcharged);
        foreach (var _ in Press(o2)) yield return null;
        ScCheck($"a switch straight after waits on the switch's cooldown ({p.Mode}, {p.ModeCooldown:0.00} s)", p.Mode == els[o1 - 1]);
        foreach (var _ in ElWait(Tune.Elements.SwitchCooldown + 0.1f)) yield return null;
        foreach (var _ in Press(o2)) yield return null;
        ScCheck($"once it's over, the switch goes ({p.Mode})", p.Mode == els[o2 - 1]);
        foreach (var _ in Press(o2)) yield return null;
        ScCheck($"its own button again does nothing: no overcharge ({p.Mode}, top {p.Top})", p.Mode == els[o2 - 1] && p.Top == HeroElement.None);

        // ---- every ability in every element
        var aim = new Vector2(p.Facing, 0);
        var home = p.GlobalPosition;
        Enemy foe = null;
        Enemy Fresh(float dx)
        {
            if (foe != null && IsInstanceValid(foe)) foe.QueueFree();
            p.GlobalPosition = home; p.Velocity = Vector2.Zero;
            foe = ElDummy(p, dx);
            return foe;
        }
        float BuildOf(Enemy e, HeroElement el) => e.BuiltTotal[(int)el];
        float AllBuild(Enemy e) => e.BuiltTotal.Sum();
        bool melee = hero is HeroKind.Swordsman or HeroKind.Warden or HeroKind.Rogue;
        // (how far the creature stands for each ability: the Swordsman's close in, short of his tipper)
        float Reach(KitRole r) => (hero, r) switch
        {
            (_, KitRole.Defensive) => 26f,
            (HeroKind.Elementalist, KitRole.Special) => Tune.ElementalStaff.FixedRange - 6f,
            (HeroKind.Warden, KitRole.Special) => 46f,
            (HeroKind.Vitalist or HeroKind.Aegis, KitRole.Special) => 26f,
            (HeroKind.Rogue, KitRole.Special) => 70f,
            (HeroKind.Swordsman, _) => 16f,
            _ => melee ? 20f : 80f,
        };
        // (what each ability builds at once: the Warden's cut nothing, his shield raised in time a special's worth; the other defensives
        // nothing; the Rogue's knife a little (the rest it stores); the Vitalist's hex seeps, so: some)
        float Want(KitRole r) => (hero, r) switch
        {
            (HeroKind.Warden, KitRole.Primary) => 0f,
            (HeroKind.Warden, KitRole.Defensive) => Tune.Elements.SpecialBuild,
            (_, KitRole.Defensive) => 0f,
            (HeroKind.Rogue, KitRole.Special) => Tune.Elements.KnifeTouch,
            (HeroKind.Vitalist, KitRole.Special) => 1f,
            (_, KitRole.Primary) => Tune.Elements.PrimaryBuild,
            _ => Tune.Elements.SpecialBuild,
        };
        var dmg = new Dictionary<(KitRole, HeroElement), float>();
        var rest = new Dictionary<(KitRole, HeroElement), float>();
        p.SetVitalForce(p.Stats.VitalForceMax);
        IEnumerable<object> Use(KitRole r, int hold = 1)
        {
            if (r == KitRole.Defensive && hero == HeroKind.Warden)
            {
                // the shield: held up, and a creature's blow stopped on it
                for (int k = 0; k < hold; k++) { _scInput = new PlayerInput { Dodge = true, GuardHeld = true, GuardAim = aim }; yield return null; }
                var b = p.TryBlock(foe.GlobalPosition, 10f, melee: foe);
                _scInput = default;
                ScCheck($"  the raised shield stops the blow ({b.Blocked}, perfect {b.Perfect})", b.Blocked && b.Perfect == (hold <= 1));
                yield break;
            }
            var inp = r switch
            {
                KitRole.Primary => new PlayerInput { Attack = true, Aim = aim, AimGiven = true },
                KitRole.Defensive => new PlayerInput { Dodge = true, Aim = aim, AimGiven = true, Move = new Vector2(-p.Facing, 0) },
                _ => new PlayerInput { Ability = true, Aim = aim, AimGiven = true },
            };
            // (the Guarded Charge stops only at a creature mid-attack; with Juggernaut's Charge it strikes the standing dummy too)
            bool jugg = p.Stats.Juggernaut;
            if (hero == HeroKind.Warden && r == KitRole.Special) p.Stats.Juggernaut = true;
            _scInput = inp; yield return null; yield return null; _scInput = default;
            if (hero == HeroKind.Warden && r == KitRole.Special) { foreach (var _ in ElWait(0.6f)) yield return null; p.Stats.Juggernaut = jugg; }
        }
        foreach (var el in els)
        {
            p.TestMode(el);
            foreach (var r in new[] { KitRole.Primary, KitRole.Special, KitRole.Defensive })
            {
                p.ResetAbilityCooldowns();
                p.TestCatchDaggers();
                p.TestClearBuffs();
                if (hero == HeroKind.Vitalist) p.SetVitalForce(p.Stats.VitalForceMax);
                var f = Fresh(Reach(r));
                aim = new Vector2(p.Facing, 0);
                foreach (var _ in ElWait(0.25f)) yield return null;
                int touches = p.SimpleTouches, uses = p.ElementalUses.Count;
                foreach (var _ in Use(r)) yield return null;
                yield return null;
                rest[(r, el)] = p.SimpleFrac(r);
                // (a burst's zone goes on building: its touch is read at once)
                if (hero == HeroKind.Elementalist && r == KitRole.Special) yield return null;
                float atOnce = BuildOf(f, el), hurtAtOnce = f.MaxHp - f.Hp;
                foreach (var _ in ElWait(1.4f)) yield return null;
                if (r == KitRole.Primary && hero == HeroKind.Elementalist) { foreach (var _ in ElWait(0.6f)) yield return null; atOnce = BuildOf(f, el); }
                // (the zone's build can take hold as burning; the Burst's own blow is the one read at once)
                bool zone = hero == HeroKind.Elementalist && r == KitRole.Special;
                float dealt = zone ? hurtAtOnce : f.MaxHp - f.Hp, built = zone ? atOnce : BuildOf(f, el);
                dmg[(r, el)] = dealt;
                string name = Elements.SimpleName(hero, r, el);
                bool used = r == KitRole.Defensive && hero == HeroKind.Warden || p.ElementalUses.Skip(uses).Contains(name);
                float want = Want(r);
                if (want <= 0f)
                    ScCheck($"{name}: used ({used}); it builds nothing of itself ({AllBuild(f):0}, {p.SimpleTouches - touches} touches)", used && AllBuild(f) < 0.5f && p.SimpleTouches == touches);
                else if (el == HeroElement.Wind)
                    ScCheck($"{name}: used ({used}), it reaches the creature ({p.SimpleTouches - touches} touch, seeping {f.SeepLeft:0.0} s) and builds nothing ({AllBuild(f):0}): wind shoves",
                        used && (p.SimpleTouches > touches || f.SeepLeft > 0) && AllBuild(f) < 0.5f);
                else if (want <= 1f)
                    ScCheck($"{name}: used ({used}), it seeps {Elements.Name(el).ToLowerInvariant()} in ({built:0} so far, {f.SeepLeft:0.0} s more), nothing else ({AllBuild(f) - built:0})", used && built > 5f && f.SeepLeft > 0 && AllBuild(f) - built < 0.5f);
                else
                    ScCheck($"{name}: used ({used}), it builds {Elements.Name(el).ToLowerInvariant()} ({built:0}, want {want:0}), nothing else ({AllBuild(f) - BuildOf(f, el):0})",
                        used && built > want - 1f && built < want + 1f + (zone ? Tune.Elements.ZoneBuild * 2f : 0f) && AllBuild(f) - BuildOf(f, el) < 0.5f);
                if (r == KitRole.Special && el == els[0]) ScShot("simple_special");
                ScCheck($"  the top stays empty ({p.Top})", p.Top == HeroElement.None);
                // (the Rogue, hidden by her vanish: her next jab, from hiding, builds double)
                if (hero == HeroKind.Rogue && r == KitRole.Defensive)
                {
                    var g = Fresh(Reach(KitRole.Primary));
                    aim = new Vector2(p.Facing, 0);
                    foreach (var _ in ElWait(0.2f)) yield return null;
                    bool hid = p.Hidden;
                    foreach (var _ in Use(KitRole.Primary)) yield return null;
                    foreach (var _ in ElWait(1f)) yield return null;
                    float want2 = Tune.Elements.PrimaryBuild * Tune.Elements.ShadowJab;
                    ScCheck($"  a jab from hiding ({hid}) builds {(el == HeroElement.Wind ? "a harder shove" : $"double ({BuildOf(g, el):0}, want {want2:0})")}", hid && (el == HeroElement.Wind || Math.Abs(BuildOf(g, el) - want2) < 1f));
                }
                p.TestCatchDaggers();
                foreach (var _ in ElWait(0.3f)) yield return null;
            }
        }
        if (foe != null && IsInstanceValid(foe)) foe.QueueFree();

        // ---- each hero's own way of laying the element on
        var mine = els.First(x => x != HeroElement.Wind);
        p.TestMode(mine);
        string en = Elements.Name(mine).ToLowerInvariant();
        IEnumerable<object> Ready(float dx) { p.ResetAbilityCooldowns(); p.TestCatchDaggers(); p.TestClearBuffs(); Fresh(dx); aim = new Vector2(p.Facing, 0); foreach (var _ in ElWait(0.25f)) yield return null; }
        // the switch's reward: the first ability after a switch lays the element on twice over (the next, as usual)
        {
            var role = hero == HeroKind.Warden ? KitRole.Special : KitRole.Primary;
            int slot = Array.IndexOf(els, mine) + 1;
            p.TestMode(els.First(x => x != mine));
            p.TestResetModeCooldowns();
            foreach (var _ in Press(slot)) yield return null;
            bool primed = p.Primed;
            foreach (var _ in Ready(Reach(role))) yield return null;
            foreach (var _ in Use(role)) yield return null;
            foreach (var _ in ElWait(1.4f)) yield return null;
            float first = BuildOf(foe, mine);
            foreach (var _ in Ready(Reach(role))) yield return null;
            foreach (var _ in Use(role)) yield return null;
            foreach (var _ in ElWait(1.4f)) yield return null;
            float second = BuildOf(foe, mine);
            float w = Want(role);
            ScCheck($"after a switch {en} is primed ({primed}): the first {Elements.Noun(hero, role)} builds {first:0} (want {w * Tune.Elements.PrimeMult:0}), the next {second:0} (want {w:0})",
                primed && Math.Abs(first - w * Tune.Elements.PrimeMult) < 1f && Math.Abs(second - w) < 1f);
        }
        switch (hero)
        {
            case HeroKind.Swordsman:
            {
                // the tipper: the outer part of the blade
                foreach (var _ in Ready(Tune.Swordsman.Reach * 0.85f)) yield return null;
                int t0 = p.Tippers;
                foreach (var _ in Use(KitRole.Primary)) yield return null;
                foreach (var _ in ElWait(0.8f)) yield return null;
                ScCheck($"a blow with the tip of the blade is a tipper ({p.Tippers - t0}): {BuildOf(foe, mine):0} {en} (x{Tune.Elements.TipperBuild}), {foe.MaxHp - foe.Hp:0} damage",
                    p.Tippers > t0 && Math.Abs(BuildOf(foe, mine) - Tune.Elements.PrimaryBuild * Tune.Elements.TipperBuild) < 1f);
                ScShot("simple_tipper");
                // the riposte: after a roll, every blow is a tipper
                foreach (var _ in Ready(30f)) yield return null;
                foreach (var _ in Use(KitRole.Defensive)) yield return null;
                while (p.IsDodging) yield return null;
                p.GlobalPosition = foe.GlobalPosition - new Vector2(16f * p.Facing, 0); p.Velocity = Vector2.Zero;
                aim = (foe.GlobalPosition - p.GlobalPosition).Normalized();
                bool open = p.RiposteOpen;
                t0 = p.Tippers;
                foreach (var _ in Use(KitRole.Primary)) yield return null;
                foreach (var _ in ElWait(0.8f)) yield return null;
                ScCheck($"after a roll the riposte is open ({open}): a blow from close in is a tipper too ({p.Tippers - t0}, {BuildOf(foe, mine):0} {en})", open && p.Tippers > t0);
                break;
            }
            case HeroKind.Warden:
            {
                // a blow stopped late on the shield builds a little
                foreach (var _ in Ready(26f)) yield return null;
                foreach (var _ in Use(KitRole.Defensive, 30)) yield return null;
                ScCheck($"a blow on a shield raised a while builds only {BuildOf(foe, mine):0} {en} (want {Tune.Elements.CounterBuild:0}); raised in time, {Tune.Elements.SpecialBuild:0}", Math.Abs(BuildOf(foe, mine) - Tune.Elements.CounterBuild) < 1f);
                break;
            }
            case HeroKind.Elementalist:
            {
                foreach (var _ in Ready(Tune.ElementalStaff.FixedRange - 6f)) yield return null;
                foreach (var _ in Use(KitRole.Special)) yield return null;
                yield return null;
                float now = BuildOf(foe, mine);
                ScShot("simple_zone");
                foreach (var _ in ElWait(Tune.Elements.ZoneSeconds)) yield return null;
                ScCheck($"the Burst's zone lingers: {now:0} {en} at once, {BuildOf(foe, mine):0} by the time it fades", BuildOf(foe, mine) >= now + Tune.Elements.ZoneBuild * (Tune.Elements.ZoneTicks - 1) - 1f);
                break;
            }
            case HeroKind.Vitalist:
            {
                foreach (var _ in Ready(26f)) yield return null;
                foreach (var _ in Use(KitRole.Special)) yield return null;
                foreach (var _ in ElWait(0.6f)) yield return null;
                float left = foe.SeepLeft, sofar = BuildOf(foe, mine);
                int c0 = p.CashIns;
                p.ResetAbilityCooldowns();
                foreach (var _ in Use(KitRole.Primary)) yield return null;
                foreach (var _ in ElWait(0.9f)) yield return null;
                ScCheck($"the drain on a hexed creature ({left:0.0} s of seep left, {sofar:0} {en} so far) tears the rest out at once ({p.CashIns - c0}): {BuildOf(foe, mine):0} {en}, no seep left ({foe.SeepLeft:0.0})",
                    p.CashIns > c0 && foe.SeepLeft <= 0f && BuildOf(foe, mine) > sofar + Tune.Elements.PrimaryBuild + 10f);
                break;
            }
            case HeroKind.Aegis:
            {
                foreach (var _ in Ready(80f)) yield return null;
                foreach (var _ in Use(KitRole.Defensive)) yield return null;
                foreach (var _ in ElWait(0.8f)) yield return null;
                bool imbued = p.Imbued(out var lent);
                p.ResetAbilityCooldowns();
                foreach (var _ in Use(KitRole.Primary)) yield return null;
                foreach (var _ in ElWait(1.2f)) yield return null;
                float want3 = Tune.Elements.PrimaryBuild + Tune.Elements.ImbueBuild;
                ScCheck($"her barrier lends her {en} ({imbued}, {lent}): her blows carry it, the bubble building {BuildOf(foe, mine):0} (want {want3:0})", imbued && lent == mine && Math.Abs(BuildOf(foe, mine) - want3) < 1f);
                break;
            }
            case HeroKind.Rogue:
            {
                foreach (var _ in Ready(70f)) yield return null;
                foreach (var _ in Use(KitRole.Special)) yield return null;
                foreach (var _ in ElWait(2f)) yield return null;
                float landed = BuildOf(foe, mine);
                int r0 = p.Releases;
                // (the knife out: the special again calls it home)
                p.ResetAbilityCooldowns();
                _scInput = new PlayerInput { Ability = true, Aim = aim, AimGiven = true }; yield return null; yield return null; _scInput = default;
                foreach (var _ in ElWait(0.8f)) yield return null;
                ScCheck($"the knife builds {landed:0} {en} as it lands; recalled after 2 s it releases what it stored ({p.Releases - r0}): {BuildOf(foe, mine):0} in all",
                    Math.Abs(landed - Tune.Elements.KnifeTouch) < 1f && p.Releases > r0 && BuildOf(foe, mine) >= landed + Tune.Elements.KnifeStore * 1.5f);
                break;
            }
        }
        if (foe != null && IsInstanceValid(foe)) foe.QueueFree();

        // ---- the same in every element
        foreach (var r in new[] { KitRole.Primary, KitRole.Defensive, KitRole.Special })
        {
            var ds = els.Select(el => dmg[(r, el)]).ToArray();
            var rs = els.Select(el => rest[(r, el)]).ToArray();
            ScCheck($"{Elements.Noun(hero, r)}: the same cooldown in every element ({string.Join(" / ", rs.Select(x => x.ToString("0.00")))})", rs.Max() - rs.Min() < 0.02f);
            // (a sword's blow and the drain vary a tenth either way of themselves; the Rogue's with hiding and crits; the defensives, the hex and
            // the bubble field strike nothing)
            if (r == KitRole.Defensive || r == KitRole.Special && hero is HeroKind.Vitalist or HeroKind.Aegis) continue;
            float spread = hero == HeroKind.Rogue ? 2.2f : hero is HeroKind.Swordsman or HeroKind.Warden or HeroKind.Vitalist ? 1.25f : 1.02f;
            float lo = ds.Min(), hi = ds.Max();
            ScCheck($"{Elements.Noun(hero, r)}: the same blow in every element ({string.Join(" / ", ds.Select(x => x.ToString("0.0")))})", lo > 0 && hi <= lo * spread + 0.5f);
        }

        // ---- no second ability button
        p.ResetAbilityCooldowns();
        int u0 = p.ElementalUses.Count;
        foreach (var _ in ElPress(new PlayerInput { Ability2 = true, Aim = aim, AimGiven = true }, 0.4f)) yield return null;
        ScCheck($"the second ability button does nothing ({p.ElementalUses.Count - u0} uses)", p.ElementalUses.Count == u0);
        p.GlobalPosition = home;
        foreach (var _ in ElWait(0.6f)) yield return null;
        ScShot("simple_hud");
        p.InputOverride = null;
    }
}

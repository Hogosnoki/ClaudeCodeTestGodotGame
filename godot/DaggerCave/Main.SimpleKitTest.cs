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
        float BuildOf(Enemy e, HeroElement el) => el switch
        {
            HeroElement.Fire => e.FireBuild, HeroElement.Frost => e.FrostBuild, HeroElement.Earth => e.EarthBuild, HeroElement.Nature => e.NatureBuild,
            HeroElement.Water => e.WaterBuild, HeroElement.Dark => e.DarkBuild, _ => 0f,
        };
        float AllBuild(Enemy e) => e.FireBuild + e.FrostBuild + e.EarthBuild + e.NatureBuild + e.WaterBuild + e.DarkBuild;
        bool melee = hero is HeroKind.Swordsman or HeroKind.Warden or HeroKind.Rogue;
        // (how far the creature stands for each ability)
        float Reach(KitRole r) => (hero, r) switch
        {
            (_, KitRole.Defensive) => 26f,
            (HeroKind.Elementalist, KitRole.Special) => Tune.ElementalStaff.FixedRange - 6f,
            (HeroKind.Warden, KitRole.Special) => 46f,
            (HeroKind.Vitalist or HeroKind.Aegis, KitRole.Special) => 26f,
            (HeroKind.Rogue, KitRole.Special) => 70f,
            _ => melee ? 20f : 80f,
        };
        var dmg = new Dictionary<(KitRole, HeroElement), float>();
        var rest = new Dictionary<(KitRole, HeroElement), float>();
        p.SetVitalForce(p.Stats.VitalForceMax);
        foreach (var el in els)
        {
            p.TestMode(el);
            foreach (var r in new[] { KitRole.Primary, KitRole.Special, KitRole.Defensive })
            {
                p.ResetAbilityCooldowns();
                p.TestCatchDaggers();
                if (hero == HeroKind.Vitalist) p.SetVitalForce(p.Stats.VitalForceMax);
                var f = Fresh(Reach(r));
                aim = new Vector2(p.Facing, 0);
                foreach (var _ in ElWait(0.25f)) yield return null;
                int touches = p.SimpleTouches, pulses = p.SimplePulses, uses = p.ElementalUses.Count;
                var at0 = f.GlobalPosition;
                if (r == KitRole.Defensive && hero == HeroKind.Warden)
                {
                    // the shield: held up, and a creature's blow stopped on it
                    for (int k = 0; k < 6; k++) { _scInput = new PlayerInput { Dodge = true, GuardHeld = true, GuardAim = aim }; yield return null; }
                    var b = p.TryBlock(f.GlobalPosition, 10f, melee: f);
                    _scInput = default;
                    ScCheck($"{Elements.SimpleName(hero, r, el)}: the raised shield stops the blow ({b.Blocked})", b.Blocked);
                }
                else
                {
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
                yield return null;
                rest[(r, el)] = p.SimpleFrac(r);
                foreach (var _ in ElWait(1.4f)) yield return null;
                if (r == KitRole.Primary && hero == HeroKind.Elementalist) foreach (var _ in ElWait(0.6f)) yield return null;
                float dealt = f.MaxHp - f.Hp, built = BuildOf(f, el);
                dmg[(r, el)] = dealt;
                string name = Elements.SimpleName(hero, r, el);
                bool used = r == KitRole.Defensive && hero == HeroKind.Warden || p.ElementalUses.Skip(uses).Contains(name);
                if (el == HeroElement.Wind)
                    ScCheck($"{name}: used ({used}), it reaches the creature ({p.SimpleTouches - touches} touch) and builds nothing ({AllBuild(f):0}): wind shoves", used && p.SimpleTouches > touches && AllBuild(f) < 0.5f);
                else
                {
                    float want = r switch { KitRole.Primary => Tune.Elements.PrimaryBuild, KitRole.Defensive => Tune.Elements.DefensiveBuild, _ => Tune.Elements.SpecialBuild };
                    ScCheck($"{name}: used ({used}), it builds {Elements.Name(el).ToLowerInvariant()} ({built:0}, a {r.ToString().ToLowerInvariant()}'s {want:0}), nothing else ({AllBuild(f) - built:0})",
                        used && built >= want * 0.5f && AllBuild(f) - built < 0.5f);
                }
                if (r == KitRole.Defensive && hero != HeroKind.Warden) ScCheck($"  and it pulses about the hero ({p.SimplePulses - pulses})", p.SimplePulses > pulses);
                if (r == KitRole.Special && el == els[0]) ScShot("simple_special");
                ScCheck($"  the top stays empty ({p.Top})", p.Top == HeroElement.None);
                p.TestCatchDaggers();
                foreach (var _ in ElWait(0.3f)) yield return null;
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

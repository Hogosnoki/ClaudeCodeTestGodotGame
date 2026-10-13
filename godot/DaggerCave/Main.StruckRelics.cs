using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _struckSteps;

    /// <summary>`--scenario=struck`: Cinder Brand (a boss or mini-boss that strikes you may be set alight, 20%) and Hoarfrost Mail (a regular creature that does may be frozen solid, 15%).</summary>
    private void StruckScenario()
    {
        if (_struckSteps == null) { if (_scT < 0.8f) return; _struckSteps = StruckRun().GetEnumerator(); }
        if (!_struckSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> StruckRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 9000;
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1f)) yield return null;
        var fire = Upgrades.Relics.First(r => r.Id == "relic_struck_fire");
        var frost = Upgrades.Relics.First(r => r.Id == "relic_struck_frost");
        fire.Apply(p.Stats, p); frost.Apply(p.Stats, p);
        ScCheck($"the relics' chances are 20% and 15% ({p.Stats.StruckIgnite:0.##}, {p.Stats.StruckFreeze:0.##})", Math.Abs(p.Stats.StruckIgnite - 0.2f) < 0.001f && Math.Abs(p.Stats.StruckFreeze - 0.15f) < 0.001f);
        float was1 = p.Stats.StruckIgnite, was2 = p.Stats.StruckFreeze;
        // (the chance made certain, to see what each does and to whom)
        p.Stats.StruckIgnite = p.Stats.StruckFreeze = 1f;

        Enemy Make(bool elite)
        {
            var g = new Goblin { Position = p.GlobalPosition + new Vector2(30 * p.Facing, 0), Elite = elite };
            g.SetMeta("test", true);
            _world.AddChild(g);
            g.Wake();
            return g;
        }
        void Strike(Enemy e) { p.ClearInvuln(); p.Hurt(5f, e.GlobalPosition, 100f, e); }

        var plain = Make(false);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(plain);
        yield return null;
        ScCheck($"a regular creature that strikes you is frozen solid, and not set alight (frozen {plain.FrozenSolid}, burning {plain.Ignited}, {p.StruckFroze} froze)", plain.FrozenSolid && !plain.Ignited && p.StruckFroze == 1);

        var big = Make(true);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(big);
        yield return null;
        ScCheck($"a mini-boss that strikes you is set alight, and not frozen (burning {big.Ignited}, frozen {big.FrozenSolid}, {p.StruckIgnited} lit)", big.Ignited && !big.FrozenSolid && p.StruckIgnited == 1);

        // with the chance at nothing, neither happens
        p.Stats.StruckIgnite = p.Stats.StruckFreeze = 0f;
        var again = Make(false);
        foreach (var _ in Wait(0.3f)) yield return null;
        Strike(again);
        yield return null;
        ScCheck($"without the relics nothing happens ({again.FrozenSolid})", !again.FrozenSolid);
        p.Stats.StruckIgnite = was1; p.Stats.StruckFreeze = was2;

        // ---- the co-op relics: Phoenix Ash, Healer's Hands (a revive's health) and Last Rites (an ally healed when you fall)
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        float max = p.Stats.MaxHp;
        float Revived(float giver) { p.NetDown(true); p.Revive(giver); return p.Hp / max; }
        ScCheck($"a plain revive restores 35% of max health ({Revived(0f):0.00})", Math.Abs(p.Hp / max - 0.35f) < 0.001f);
        Upgrades.Relics.First(r => r.Id == "relic_phoenix").Apply(p.Stats, p);
        ScCheck($"with Phoenix Ash you rise with 20% more ({Revived(0f):0.00})", Math.Abs(p.Hp / max - 0.55f) < 0.001f);
        ScCheck($"a friend with Healer's Hands adds 20% more again ({Revived(Tune.Relics.ReviveExtra):0.00})", Math.Abs(p.Hp / max - 0.75f) < 0.001f);
        var healer = Upgrades.Relics.First(r => r.Id == "relic_healer");
        var mate = new PlayerStats(HeroKind.Swordsman);
        healer.Apply(mate, null);
        ScCheck($"Healer's Hands gives {mate.ReviveGiveBonus:0.##} to those the bearer revives", Math.Abs(mate.ReviveGiveBonus - 0.2f) < 0.001f);
        ScCheck("the three are for a party (not offered alone)", Upgrades.Relics.Where(r => r.Id is "relic_last_rites" or "relic_phoenix" or "relic_healer").All(r => r.Multi));

        var friend = new Player { Stats = new PlayerStats(HeroKind.Swordsman), Position = p.GlobalPosition + new Vector2(40, 0) };
        friend.InputOverride = () => default;
        _world.AddChild(friend);
        foreach (var _ in Wait(0.3f)) yield return null;
        friend.Stats.MaxHp = 200; friend.Hp = 50;
        Upgrades.Relics.First(r => r.Id == "relic_last_rites").Apply(p.Stats, p);
        float hp0 = friend.Hp;
        p.GiveUp();
        yield return null;
        ScCheck($"Last Rites: when you fall a living friend is healed by 10 ({hp0:0} -> {friend.Hp:0}; {p.AlliesHealedOnFall} healed)", p.AlliesHealedOnFall == 1 && friend.Hp > hp0 + 4f && friend.Hp < hp0 + 12f);
    }
}

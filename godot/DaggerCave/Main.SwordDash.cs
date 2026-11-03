using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private IEnumerator<object> _sdSteps;

    /// <summary>
    /// `--scenario=dash --hero=swordsman` (the heroes' own kits): the Swordsman's dodge button is a Dash, not a roll: it carries him about
    /// 120 px, and a swing under way carries on through it (the blade lands where he ends up); Far Dash takes him further. And the cards
    /// dealt fit the kits: each hero's movement and defence cards are on offer, and the cards for abilities the kits no longer have are not.
    /// </summary>
    private void SwordDashScenario()
    {
        if (_sdSteps == null) { if (_scT < 0.8f) return; _sdSteps = SwordDashRun().GetEnumerator(); }
        if (!_sdSteps.MoveNext()) ScEnd();
    }

    private IEnumerable<object> SwordDashRun()
    {
        var p = G.Player;
        foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        p.Stats.MaxHp = p.Hp = 5000;
        p.InputOverride = () => { var i = _scInput; _scInput.Dodge = _scInput.Attack = false; return i; };
        IEnumerable<object> Wait(float s) { for (float t = 0; t < s; t += (float)GetProcessDeltaTime()) yield return null; }
        foreach (var _ in Wait(1f)) yield return null;
        ScCheck($"the Swordsman's dodge button is the Dash ({Elements.Noun(HeroKind.Swordsman, KitRole.Defensive)}, his own kit {p.SimpleKit})", p.SimpleKit && Elements.Noun(HeroKind.Swordsman, KitRole.Defensive) == "Dash");
        // (the way he faces is open: turn round if not)
        int dir = G.Cave.IsSolid(p.GlobalPosition + new Vector2(p.Facing * 140, -10)) ? -(int)p.Facing : (int)p.Facing;

        // ---- a plain dash
        float Dash()
        {
            var at = p.GlobalPosition;
            _scInput = new PlayerInput { Dodge = true, Move = new Vector2(dir, 0) };
            return at.X;
        }
        // (the game slowed for the checks: a slow frame here would hold several physics steps, and a dash lasts 0.15 s)
        Engine.TimeScale = 0.15;
        float x0 = Dash();
        yield return null; yield return null;
        ScCheck($"it is a dash under way ({p.IsDodging})", p.IsDodging);
        _scInput = default;
        Engine.TimeScale = 1.0;
        foreach (var _ in Wait(0.5f)) yield return null;
        float went = Math.Abs(p.GlobalPosition.X - x0);
        ScCheck($"it carries him about 120 px ({went:0} px)", went > 90f && went < 170f);

        // ---- a swing, then a dash in the middle of it: the swing carries on, and lands where he ends up
        foreach (var _ in Wait(Tune.Hero.DodgeCooldown + 0.4f)) yield return null;
        p.ResetAbilityCooldowns();
        var foe = new Spider { Position = p.GlobalPosition + new Vector2(dir * 125, 0) };
        foe.SetMeta("test", true);
        _world.AddChild(foe);
        foe.Wake(); foe.ForceGround();
        foe.Hp = foe.MaxHp = 1000000f;
        foe.Pin(60f);
        foreach (var _ in Wait(0.3f)) yield return null;
        float h0 = foe.Hp;
        Engine.TimeScale = 0.15;
        _scInput = new PlayerInput { Attack = true, Aim = new Vector2(dir, 0), AimGiven = true };
        yield return null;
        bool swung = p.IsSwinging;
        x0 = Dash();
        yield return null; yield return null;
        ScCheck($"a dash pressed mid-swing starts at once and the swing carries on (swinging {swung} -> {p.IsSwinging}, dashing {p.IsDodging})", swung && p.IsSwinging && p.IsDodging);
        _scInput = default;
        foreach (var _ in Wait(0.15f)) yield return null;
        Engine.TimeScale = 1.0;
        foreach (var _ in Wait(0.4f)) yield return null;
        ScCheck($"and the blade struck the creature where the dash took him ({h0 - foe.Hp:0} damage, {Math.Abs(p.GlobalPosition.X - x0):0} px dashed)", foe.Hp < h0);
        foe.QueueFree();

        // ---- Far Dash: further
        foreach (var _ in Wait(Tune.Hero.DodgeCooldown + 0.4f)) yield return null;
        p.ResetAbilityCooldowns();
        Upgrades.Apply(Upgrades.Get("g_swo_dfar"), p.Stats, p);
        dir = -dir;
        x0 = Dash();
        yield return null;
        _scInput = default;
        foreach (var _ in Wait(0.5f)) yield return null;
        float far = Math.Abs(p.GlobalPosition.X - x0);
        ScCheck($"Far Dash carries him further ({went:0} -> {far:0} px)", far > went * 1.12f || far > 135f);

        // ---- the cards dealt fit the kits
        bool Dealt(HeroKind h, string id) => Upgrades.Available(Upgrades.Get(id), new PlayerStats(h));
        var want = new (HeroKind, string)[] {
            (HeroKind.Swordsman, "g_swo_dfar"), (HeroKind.Swordsman, "g_swo_dghost"), (HeroKind.Elementalist, "g_ele_dfar"), (HeroKind.Warden, "g_war_ofar"),
            (HeroKind.Warden, "g_war_dhard"), (HeroKind.Vitalist, "g_vit_dhard"), (HeroKind.Aegis, "g_aeg_dhard"), (HeroKind.Rogue, "g_rog_ofar"), (HeroKind.Rogue, "g_rog_dlong") };
        var missing = want.Where(w => !Dealt(w.Item1, w.Item2)).Select(w => w.Item2).ToList();
        ScCheck($"each hero's movement and defence cards are on offer (missing: {string.Join(", ", missing)})", missing.Count == 0);
        var gone = new (HeroKind, string)[] {
            (HeroKind.Swordsman, "relic_s_fire"), (HeroKind.Warden, "relic_w_siphon"), (HeroKind.Warden, "relic_w_ram"), (HeroKind.Elementalist, "relic_e_blood"),
            (HeroKind.Elementalist, "relic_e_rime"), (HeroKind.Rogue, "relic_r_lone"), (HeroKind.Rogue, "relic_r_hemo"), (HeroKind.Swordsman, "hp"), (HeroKind.Swordsman, "longdash") };
        var still = gone.Where(w => Dealt(w.Item1, w.Item2)).Select(w => w.Item2).ToList();
        ScCheck($"the cards for what the kits no longer have, or the boons now deal, are not (still dealt: {string.Join(", ", still)})", still.Count == 0);
        ScCheck($"the Swordsman's dodge card reads Dash ({Upgrades.Get("g_swo_dsoon").Name}), the Warden's Shield Bash ({Upgrades.Get("g_war_dsoon").Name})",
            Upgrades.Get("g_swo_dsoon").Name == "Ready Dash" && Upgrades.Get("g_war_dsoon").Name == "Ready Shield Bash");
        p.InputOverride = null;
    }
}

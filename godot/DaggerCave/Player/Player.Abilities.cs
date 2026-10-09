using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// The ability button's uses (the Charged Strike, the Guarded Charge, the heal, the blizzard). Normally one use,
/// back after the ability's recharge; Twin Reserve holds a second use, each taking twice as long
/// to come back. Each use recharges on its own clock, like the Swordsman's dodge charges.
/// </summary>
public partial class Player
{
    private float[] _abilityCd = new float[1];

    /// <summary>Seconds one use of the ability button takes to come back.</summary>
    public float AbilityRecharge => Stats.AbilityCdMult * Stats.Hero switch
    {
        HeroKind.Warden => Stats.DashTime + Stats.DashCooldown,
        HeroKind.Vitalist => Tune.Vitalist.HealCooldown,
        HeroKind.Elementalist => Tune.Elementalist.BlizzardCooldown * Stats.BlizzardCdMult,
        // (the Rogue's ability button throws its daggers; its vanishing takes these uses)
        HeroKind.Rogue => Tune.Rogue.VanishCooldown * Stats.VanishCdMult,
        HeroKind.Aegis => Tune.Aegis.BarrierCooldown * Stats.BarrierCdMult,
        HeroKind.ShapeShifter => Tune.Shifter.ShiftCooldown,
        HeroKind.Automaton => Tune.AutomatonHero.RepairCooldown,
        _ => Stats.ChargeCooldown,
    };

    /// <summary>A use of the ability button is ready.</summary>
    public bool AbilityChargeReady
    {
        get
        {
            foreach (float t in _abilityCd) if (t <= 0) return true;
            return false;
        }
    }

    /// <summary>For the HUD: how many uses are ready, and each use's recharge left (seconds).</summary>
    public int AbilityUsesReady
    {
        get
        {
            int n = 0;
            foreach (float t in _abilityCd) if (t <= 0) n++;
            return n;
        }
    }
    public float[] AbilityCooldowns => _abilityCd;

    /// <summary>0 = a use is ready, 1 = every use was just spent (by the one closest to coming back).</summary>
    public float AbilityCooldownFrac
    {
        get
        {
            float soonest = float.MaxValue;
            foreach (float t in _abilityCd) { if (t <= 0) return 0; soonest = Math.Min(soonest, t); }
            return Math.Clamp(soonest / Math.Max(0.01f, AbilityRecharge), 0, 1);
        }
    }

    private void SpendAbilityCharge()
    {
        for (int k = 0; k < _abilityCd.Length; k++)
            if (_abilityCd[k] <= 0) { _abilityCd[k] = AbilityRecharge; return; }
    }
}

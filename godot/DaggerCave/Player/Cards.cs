using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// How the heroes grow (<see cref="Tune.Growth"/>). Every level gives a pick of three cards, taken when the player asks (the milestone key),
/// never in the middle of a fight. Each card changes something you can see: a hero's great special grows wider, harder, sooner or strikes
/// again; the attack bites harder or comes quicker (a caster's splash spreads); the second ability and the dodge come back sooner or hit
/// harder; and a few shared cards (health, damage, speed...) for anyone. Every <see cref="Tune.Growth.MilestoneEvery"/> levels a milestone
/// changes the hero's special into one of two forms, or gives something as big (a second stroke held, kills that hasten it).
/// The Automaton, whose kit is its own, keeps its old class cards.
/// </summary>
public static class Cards
{
    private static readonly HeroKind[] Heroes = { HeroKind.Swordsman, HeroKind.Elementalist, HeroKind.Warden, HeroKind.Vitalist, HeroKind.Aegis, HeroKind.Rogue, HeroKind.ShapeShifter };

    private static string SpecialOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "Whirlwind", HeroKind.Elementalist => "Meteor", HeroKind.Warden => "Great Bash",
        HeroKind.Vitalist => "Bloom", HeroKind.Aegis => "Judgement", HeroKind.ShapeShifter => "Apex Form", _ => "Death Blossom",
    };
    private static string PrimaryOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "Strike", HeroKind.Elementalist => "Bolt", HeroKind.Warden => "Cut",
        HeroKind.Vitalist => "Drain", HeroKind.Aegis => "Bubble", HeroKind.ShapeShifter => "Wild Strike", _ => "Jab",
    };
    private static string OtherOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "Counter", HeroKind.Elementalist => "Nova", HeroKind.Warden => "Charge",
        HeroKind.Vitalist => "Rupture", HeroKind.Aegis => "Humbling Light", HeroKind.ShapeShifter => "Wild Move", _ => "Shadowstep",
    };
    private static string DefensiveOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "Dash", HeroKind.Elementalist => "Blink", HeroKind.Warden => "Shield Bash",
        HeroKind.Vitalist => "Mending", HeroKind.Aegis => "Barrier", HeroKind.ShapeShifter => "Molt", _ => "Vanish",
    };
    private static string AgainText(HeroKind h) => h switch
    {
        HeroKind.Swordsman => $"The Whirlwind spins {Tune.Growth.WhirlExtraPulses} strokes longer.",
        HeroKind.Elementalist => "One more, smaller stone falls beside the Meteor.",
        HeroKind.Warden => "An aftershock follows the Great Bash, at half its strength.",
        HeroKind.Vitalist => "The Bloom pulses once more, at half its strength.",
        HeroKind.Aegis => "One more pillar falls on the nearest creature the Judgement missed (or on the same spot again).",
        HeroKind.ShapeShifter => $"The Apex Form lasts {Tune.Shifter.ApexExtraSeconds:0} s longer.",
        _ => "The Death Blossom throws one more volley.",
    };

    private static string Slug(HeroKind h) => h.ToString().ToLowerInvariant()[..3];

    /// <summary>The level cards for the heroes' own buttons (a chest's class card is one of these too).</summary>
    public static readonly List<Upgrade> Ability = Graded(BuildAbility(), u => u.Tier == UpgradeTier.Ability ? CardGrade.Rare : CardGrade.Uncommon);
    /// <summary>The level cards for anyone (the plain stats among them are dealt as boons now: see <see cref="Retired"/>).</summary>
    public static readonly List<Upgrade> Shared = Graded(BuildShared(), _ => CardGrade.Common);
    /// <summary>The milestone cards: each hero's two forms of their special, and the great things anyone may take.</summary>
    public static readonly List<Upgrade> Milestones = Graded(BuildMilestones(), _ => CardGrade.Epic);
    /// <summary>Shared cards no longer dealt, their place taken by the graded boons (Player/Boons.cs); kept so a hero who has them still reads right.</summary>
    private static readonly HashSet<string> Retired = new() { "g_vigor", "g_might", "g_fleet", "g_hide" };

    private static List<Upgrade> Graded(List<Upgrade> list, Func<Upgrade, CardGrade> grade)
    {
        foreach (var u in list) u.Grade = grade(u);
        return list;
    }

    public static IEnumerable<Upgrade> All => Ability.Concat(Shared).Concat(Milestones);

    /// <summary>A milestone's card (its label says so).</summary>
    public static bool IsMilestone(Upgrade u) => Milestones.Contains(u);

    private static List<Upgrade> BuildAbility()
    {
        var list = new List<Upgrade>();
        foreach (var h in Heroes)
        {
            var f = new[] { h };
            string id = Slug(h), sp = SpecialOf(h), pr = PrimaryOf(h), ot = OtherOf(h), de = DefensiveOf(h);
            // the great special
            list.Add(new() { Id = $"g_{id}_wide", Name = $"Vast {sp}", Desc = $"The {sp} reaches {Pct(Tune.Growth.WideStep)} wider.", Icon = "spell", For = f, Ability = sp, MaxStacks = 3, Tags = new[] { "special" },
                Apply = (s, p) => s.SpecialRadiusMult *= Tune.Growth.WideStep });
            list.Add(new() { Id = $"g_{id}_hard", Name = $"Crushing {sp}", Desc = $"The {sp} strikes {Pct(Tune.Growth.HardStep)} harder.", Icon = "spell", For = f, Ability = sp, MaxStacks = 3, Tags = new[] { "special" },
                Apply = (s, p) => s.SpecialDamageMult *= Tune.Growth.HardStep });
            list.Add(new() { Id = $"g_{id}_soon", Name = $"Restless {sp}", Desc = $"The {sp} rests {Pct(1f / Tune.Growth.SoonStep)} less.", Icon = "charge", For = f, Ability = sp, MaxStacks = 3, Tags = new[] { "special" },
                Apply = (s, p) => s.SpecialRestMult *= Tune.Growth.SoonStep });
            list.Add(new() { Id = $"g_{id}_again", Name = $"{sp}, Again", Desc = AgainText(h), Icon = "spell", For = f, Ability = sp, MaxStacks = 2, Tier = UpgradeTier.Ability, Tags = new[] { "special" },
                Apply = (s, p) => s.SpecialExtra++ });
            // the attack
            bool shifter = h == HeroKind.ShapeShifter;
            list.Add(new() { Id = $"g_{id}_keen", Name = $"Keen {pr}", Desc = shifter ? $"Your staff and every form's blows deal {Pct(Tune.Growth.KeenStep)} more damage." : $"Your {pr} deals {Pct(Tune.Growth.KeenStep)} more damage.", Icon = "blade", For = f, Ability = pr, MaxStacks = 3, Tags = new[] { "offence" },
                Apply = (s, p) => { s.PrimaryDamageMult *= Tune.Growth.KeenStep; if (shifter) s.FormDmgMult *= Tune.Growth.KeenStep; } });
            list.Add(new() { Id = $"g_{id}_quick", Name = $"Quick {pr}", Desc = shifter ? $"Your staff, and every creature you become, {Pct(Tune.Growth.QuickStep)} quicker." : $"Your {pr} comes {Pct(Tune.Growth.QuickStep)} quicker.", Icon = "blade", For = f, Ability = pr, MaxStacks = 3, Tags = new[] { "offence" },
                Apply = (s, p) => s.AttackSpeed *= Tune.Growth.QuickStep });
            if (h is HeroKind.Elementalist or HeroKind.Vitalist)
            {
                list.Add(new() { Id = $"g_{id}_splashw", Name = "Wide Splash", Desc = $"Your {pr}'s splash reaches {Pct(Tune.Growth.SplashWide)} further.", Icon = "spell", For = f, Ability = pr, MaxStacks = 2, Tags = new[] { "offence" },
                    Apply = (s, p) => s.SplashRadiusMult *= Tune.Growth.SplashWide });
                list.Add(new() { Id = $"g_{id}_splashh", Name = "Heavy Splash", Desc = $"Your {pr}'s splash deals {Tune.Growth.SplashShare * 100f:0}% more of the blow to those about.", Icon = "spell", For = f, Ability = pr, MaxStacks = 2, Tags = new[] { "offence" },
                    Apply = (s, p) => s.SplashShareAdd += Tune.Growth.SplashShare });
            }
            if (h is HeroKind.Swordsman or HeroKind.Warden or HeroKind.Rogue)
                list.Add(new() { Id = $"g_{id}_reach", Name = "Long Reach", Desc = $"Your {pr} reaches {Pct(Tune.Growth.ReachStep)} further.", Icon = "blade", For = f, Ability = pr, MaxStacks = 2, Tags = new[] { "offence" },
                    Apply = (s, p) => s.DaggerReach *= Tune.Growth.ReachStep });
            // the second ability
            list.Add(new() { Id = $"g_{id}_osoon", Name = $"Ready {ot}", Desc = OtherSoonText(h, ot), Icon = "charge", For = f, Ability = ot, MaxStacks = 2, Tags = new[] { "utility" }, Apply = OtherSoon(h) });
            list.Add(new() { Id = $"g_{id}_ohard", Name = $"Mighty {ot}", Desc = OtherHardText(h, ot), Icon = "spell", For = f, Ability = ot, MaxStacks = 2, Tags = new[] { "offence" }, Apply = OtherHard(h) });
            // the dodge
            list.Add(new() { Id = $"g_{id}_dsoon", Name = $"Ready {de}", Desc = DefSoonText(h, de), Icon = "dodge", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "defence" }, Apply = DefSoon(h) });
            // the ways of getting about, and the defence itself, grown
            float far = Tune.Growth.FarStep, hard = Tune.Growth.DefHard;
            switch (h)
            {
                case HeroKind.Swordsman:
                    list.Add(new() { Id = "g_swo_dfar", Name = "Far Dash", Desc = $"The Dash carries you {Pct(far)} further.", Icon = "move", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "mobility" },
                        Apply = (s, p) => s.DodgeDistMult *= far });
                    list.Add(new() { Id = "g_swo_dghost", Name = "Phantom Dash", Desc = "Nothing can strike you while you dash.", Icon = "dodge", For = f, Ability = de, MaxStacks = 1, Tier = UpgradeTier.Ability, Tags = new[] { "defence", "mobility" },
                        Apply = (s, p) => s.DodgeIFrames = true });
                    break;
                case HeroKind.Elementalist:
                    list.Add(new() { Id = "g_ele_dfar", Name = "Far Blink", Desc = $"The Blink carries you {Pct(far)} further.", Icon = "move", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "mobility" },
                        Apply = (s, p) => s.BlinkRangeMult *= far });
                    break;
                case HeroKind.Warden:
                    list.Add(new() { Id = "g_war_ofar", Name = "Long Charge", Desc = $"The Charge carries you {Pct(far)} further.", Icon = "move", For = f, Ability = ot, MaxStacks = 2, Tags = new[] { "mobility" },
                        Apply = (s, p) => s.DashTime *= far });
                    list.Add(new() { Id = "g_war_dhard", Name = "Mighty Shield Bash", Desc = $"The Shield Bash strikes {Pct(hard)} harder and stuns {Pct(hard)} longer.", Icon = "shield", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "defence", "offence" },
                        Apply = (s, p) => s.BashPowerMult *= hard });
                    break;
                case HeroKind.Vitalist:
                    list.Add(new() { Id = "g_vit_dhard", Name = "Rich Mending", Desc = $"The Mending mends {Pct(hard)} more.", Icon = "life", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "defence", "sustain" },
                        Apply = (s, p) => s.HealMult *= hard });
                    break;
                case HeroKind.Aegis:
                    list.Add(new() { Id = "g_aeg_dhard", Name = "Thick Barrier", Desc = $"The Barrier holds {Pct(hard)} more.", Icon = "shield", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "defence" },
                        Apply = (s, p) => s.BarrierMult *= hard });
                    break;
                case HeroKind.ShapeShifter:
                    list.Add(new() { Id = "g_sha_dfar", Name = "Far Molt", Desc = $"The Molt carries you {Pct(far)} further.", Icon = "move", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "mobility" },
                        Apply = (s, p) => s.DodgeDistMult *= far });
                    list.Add(new() { Id = "g_sha_hide", Name = "Tough Hide", Desc = $"Every form shrugs off {Tune.Growth.HideShare * 100f:0}% more of the blows it takes.", Icon = "shield", For = f, Ability = pr, MaxStacks = 3, Tags = new[] { "defence" },
                        Apply = (s, p) => s.FormArmorAdd += Tune.Growth.HideShare });
                    list.Add(new() { Id = "g_sha_gaze", Name = "Long Gaze", Desc = $"You copy creatures {Pct(Tune.Growth.GazeStep)} further away.", Icon = "move", For = f, Ability = pr, MaxStacks = 2, Tags = new[] { "utility" },
                        Apply = (s, p) => s.ShiftRangeMult *= Tune.Growth.GazeStep });
                    break;
                case HeroKind.Rogue:
                    list.Add(new() { Id = "g_rog_ofar", Name = "Long Shadowstep", Desc = $"The Shadowstep reaches creatures {Pct(far)} further off.", Icon = "move", For = f, Ability = ot, MaxStacks = 2, Tags = new[] { "mobility" },
                        Apply = (s, p) => s.StepRangeMult *= far });
                    list.Add(new() { Id = "g_rog_dlong", Name = "Deep Vanish", Desc = $"The Vanish lasts {Tune.Growth.VanishLonger:0.#} s longer.", Icon = "dodge", For = f, Ability = de, MaxStacks = 2, Tags = new[] { "defence" },
                        Apply = (s, p) => s.VanishBonus += Tune.Growth.VanishLonger });
                    break;
            }
        }
        return list;
    }

    private static string OtherSoonText(HeroKind h, string ot) => h switch
    {
        HeroKind.Vitalist => $"The {ot} needs {Pct(1f / Tune.Growth.OtherSoon)} less vital force.",
        _ => $"The {ot} comes back {Pct(1f / Tune.Growth.OtherSoon)} sooner.",
    };
    private static Action<PlayerStats, Player> OtherSoon(HeroKind h) => h switch
    {
        HeroKind.Warden => (s, p) => s.DashCooldown *= Tune.Growth.OtherSoon,
        HeroKind.Vitalist => (s, p) => s.RuptureCostMult *= Tune.Growth.OtherSoon,
        HeroKind.ShapeShifter => (s, p) => { s.OtherRestMult *= Tune.Growth.OtherSoon; s.FormSpecCdMult *= Tune.Growth.OtherSoon; },
        _ => (s, p) => s.OtherRestMult *= Tune.Growth.OtherSoon,
    };
    private static string OtherHardText(HeroKind h, string ot) => h switch
    {
        HeroKind.Swordsman => $"The riposte of a {ot} strikes {Pct(Tune.Growth.OtherHard)} harder.",
        HeroKind.Elementalist => $"The {ot} strikes {Pct(Tune.Growth.OtherHard)} harder and reaches a little wider.",
        HeroKind.Warden => $"The {ot} strikes {Pct(Tune.Growth.OtherHard)} harder.",
        HeroKind.Vitalist => $"The {ot} bursts {Pct(Tune.Growth.OtherHard)} wider.",
        HeroKind.Aegis => $"The creatures the {ot} humbles stay humbled {Pct(Tune.Growth.OtherHard)} longer.",
        HeroKind.ShapeShifter => $"The {ot} (the creature's own, or the form's trick) strikes {Pct(Tune.Growth.OtherHard)} harder.",
        _ => $"After a {ot}, your sure critical waits {Pct(Tune.Growth.OtherHard)} longer.",
    };
    private static Action<PlayerStats, Player> OtherHard(HeroKind h) => h switch
    {
        HeroKind.Warden => (s, p) => s.DashDamageMult *= Tune.Growth.OtherHard,
        HeroKind.Vitalist => (s, p) => { s.RuptureRadiusMult *= Tune.Growth.OtherHard; s.RuptureSplashMult *= Tune.Growth.OtherHard; },
        HeroKind.Aegis => (s, p) => s.DebuffSeconds *= Tune.Growth.OtherHard,
        HeroKind.ShapeShifter => (s, p) => s.FormSpecDmgMult *= Tune.Growth.OtherHard,
        _ => (s, p) => s.OtherPowerMult *= Tune.Growth.OtherHard,
    };
    private static string DefSoonText(HeroKind h, string de) => h switch
    {
        HeroKind.Vitalist => $"The {de} costs {Pct(1f / Tune.Growth.DefSoon)} less vital force.",
        _ => $"The {de} comes back {Pct(1f / Tune.Growth.DefSoon)} sooner.",
    };
    private static Action<PlayerStats, Player> DefSoon(HeroKind h) => h switch
    {
        HeroKind.Swordsman => (s, p) => s.DodgeCdMult *= Tune.Growth.DefSoon,
        HeroKind.Warden => (s, p) => s.BashCooldown *= Tune.Growth.DefSoon,
        HeroKind.Vitalist => (s, p) => s.HealCostMult *= Tune.Growth.DefSoon,
        HeroKind.Aegis => (s, p) => s.BarrierCdMult *= Tune.Growth.DefSoon,
        HeroKind.Rogue => (s, p) => s.VanishCdMult *= Tune.Growth.DefSoon,
        _ => (s, p) => s.DefensiveRestMult *= Tune.Growth.DefSoon,
    };

    private static List<Upgrade> BuildShared() => new()
    {
        new() { Id = "g_vigor", Name = "Vigor", Desc = $"{Pct(Tune.Growth.VigorStep)} more health (and that much mended).", Icon = "life", MaxStacks = 5, Tags = new[] { "defence" },
            Apply = (s, p) => { float more = s.MaxHp * (Tune.Growth.VigorStep - 1f); s.MaxHp += more; p?.Heal(more); } },
        new() { Id = "g_might", Name = "Might", Desc = $"{Pct(Tune.Growth.MightStep)} more damage with everything.", Icon = "blade", MaxStacks = 5, Tags = new[] { "offence" },
            Apply = (s, p) => s.DamageMult *= Tune.Growth.MightStep },
        new() { Id = "g_fleet", Name = "Fleet", Desc = $"Run {Pct(Tune.Growth.FleetStep)} faster.", Icon = "move", MaxStacks = 3, Tags = new[] { "mobility" },
            Apply = (s, p) => s.MoveSpeed *= Tune.Growth.FleetStep },
        new() { Id = "g_hide", Name = "Thick Hide", Desc = $"Take {Pct(1f / Tune.Growth.HideStep)} less damage.", Icon = "shield", MaxStacks = 3, Tags = new[] { "defence" },
            Apply = (s, p) => s.DamageTakenMult *= Tune.Growth.HideStep },
        new() { Id = "g_thirst", Name = "Blood Thirst", Desc = $"Each kill mends {Tune.Growth.ThirstHeal:0.#} health (a generator's minion, a third of that).", Icon = "life", MaxStacks = 3, Tags = new[] { "sustain" },
            Apply = (s, p) => s.HealOnKill += Tune.Growth.ThirstHeal },
        new() { Id = "g_learned", Name = "Quick Study", Desc = $"Gain {Pct(Tune.Growth.LearnStep)} more experience.", Icon = "charge", MaxStacks = 2, Tags = new[] { "utility" },
            Apply = (s, p) => s.XpMult *= Tune.Growth.LearnStep },
    };

    private static List<Upgrade> BuildMilestones()
    {
        var list = new List<Upgrade>();
        void Forms(HeroKind h, string an, string ad, string bn, string bd)
        {
            string id = Slug(h);
            list.Add(new() { Id = $"m_{id}_a", Name = an, Desc = ad, Icon = "spell", For = new[] { h }, Ability = SpecialOf(h), MaxStacks = 1, Excludes = new[] { $"m_{id}_b" }, Tier = UpgradeTier.Ability, Tags = new[] { "special", "form" },
                Apply = (s, p) => s.SpecialForm = "a" });
            list.Add(new() { Id = $"m_{id}_b", Name = bn, Desc = bd, Icon = "spell", For = new[] { h }, Ability = SpecialOf(h), MaxStacks = 1, Excludes = new[] { $"m_{id}_a" }, Tier = UpgradeTier.Ability, Tags = new[] { "special", "form" },
                Apply = (s, p) => s.SpecialForm = "b" });
        }
        Forms(HeroKind.Swordsman, "Cyclone", $"The Whirlwind draws creatures in as it spins, and reaches {Pct(Tune.Growth.CycloneRadius)} wider.",
            "Blade Storm", "Every other stroke of the Whirlwind flings a crescent of steel out to each side.");
        Forms(HeroKind.Elementalist, "Meteor Shower", $"The Meteor becomes {Tune.Growth.ShowerStones} stones falling across the spot.",
            "Cataclysm", $"The Meteor becomes one vast stone: {Pct(Tune.Growth.CataclysmRadius)} wider and {Pct(Tune.Growth.CataclysmDamage)} harder, resting {Pct(Tune.Growth.CataclysmRest)} longer.");
        Forms(HeroKind.Warden, "Shockwave", "The Great Bash sends a shockwave running on along the ground, stunning what it meets.",
            "Bulwark", $"The Great Bash wards you ({Tune.Growth.BulwarkSelf * 100f:0}% of your health) and the friends beside you ({Tune.Growth.BulwarkAllies * 100f:0}%) for {Tune.Growth.BulwarkSeconds:0} s.");
        Forms(HeroKind.Vitalist, "Overgrowth", $"The Bloom leaves a garden for {Tune.Growth.GardenSeconds:0} s: friends in it mend, creatures in it take your element.",
            "Heartbloom", $"The Bloom mends twice as much, and {Tune.Growth.HeartbloomPerFoe * 100f:0}% more for every creature it tears at.");
        Forms(HeroKind.Aegis, "Sanctum", $"The Judgement's light stays {Tune.Growth.SanctumSeconds:0} s where it fell, searing what stands in it.",
            "Chain of Light", $"{Tune.Growth.ChainPillars} more pillars fall on the creatures nearest the Judgement.");
        Forms(HeroKind.Rogue, "Shadow Marks", $"What the Death Blossom's knives find takes {Tune.Growth.MarkVulnerable * 100f:0}% more from every blow for {Tune.Growth.MarkSeconds:0} s.",
            "Whirling Steel", $"The Death Blossom throws {Tune.Growth.SteelVolleys} more volleys, {Tune.Growth.SteelKnives} more knives in each.");
        Forms(HeroKind.ShapeShifter, "Primal Fury", $"In the Apex Form every blow strikes the creatures beside what it hits too ({Tune.Growth.FuryShare * 100f:0}% of it).",
            "Lifeblood", $"In the Apex Form every blow mends you {Tune.Growth.LifebloodShare * 100f:0}% of what it deals, and each kill keeps the apex going {Tune.Growth.LifebloodKill:0} s longer.");
        // (anyone's, once each)
        list.Add(new() { Id = "m_twin", Name = "Twin Stroke", Desc = "Your special holds two strokes: use them back to back, and each comes back in its own time.", Icon = "spell", MaxStacks = 1, Tier = UpgradeTier.Ability, Tags = new[] { "special" },
            Apply = (s, p) => { s.SpecialCharges++; p?.GainSpecialCharge(); } });
        list.Add(new() { Id = "m_bloodrush", Name = "Bloodrush", Desc = $"Every kill cuts your special's rest by {Tune.Growth.KillRestCut:0.##} s (a generator's minion, a third of that).", Icon = "charge", MaxStacks = 1, Tags = new[] { "special" },
            Apply = (s, p) => s.KillRest += Tune.Growth.KillRestCut });
        list.Add(new() { Id = "m_momentum", Name = "Momentum", Desc = $"Each use of your second ability cuts your special's rest by {Tune.Growth.MomentumCut:0.#} s.", Icon = "charge", MaxStacks = 1, Tags = new[] { "special" },
            Apply = (s, p) => s.OtherFeedsSpecial += Tune.Growth.MomentumCut });
        list.Add(new() { Id = "m_overwhelm", Name = "Overwhelm", Desc = $"Your special strikes {Pct(Tune.Growth.OverwhelmDamage)} harder and reaches {Pct(Tune.Growth.OverwhelmRadius)} wider.", Icon = "spell", MaxStacks = 1, Tags = new[] { "special" },
            Apply = (s, p) => { s.SpecialDamageMult *= Tune.Growth.OverwhelmDamage; s.SpecialRadiusMult *= Tune.Growth.OverwhelmRadius; } });
        list.Add(new() { Id = "m_colossus", Name = "Colossus", Desc = $"{Pct(Tune.Growth.ColossusHp)} more health, and all of it mended now.", Icon = "life", MaxStacks = 1, Tags = new[] { "defence" },
            Apply = (s, p) => { s.MaxHp *= Tune.Growth.ColossusHp; p?.Heal(s.MaxHp); } });
        return list;
    }

    /// <summary>"25%" for a multiplier of 1.25 (or of 0.8: what it takes off, as a share of what's left).</summary>
    private static string Pct(float mult) => $"{MathF.Abs(mult - 1f) * 100f:0}%";

    /// <summary>The hero grows by these cards (the simple-kit heroes; the Shape Shifter and the Automaton keep their own).</summary>
    public static bool Grows(HeroKind h) => Array.IndexOf(Heroes, h) >= 0 && Elements.SimpleKit(h);

    /// <summary>A level brings a milestone (every <see cref="Tune.Growth.MilestoneEvery"/> levels for the heroes that grow by these cards).</summary>
    public static bool IsMilestoneLevel(HeroKind h, int level) => Grows(h) ? level % Tune.Growth.MilestoneEvery == 0 : level % Meta.MilestoneEvery == 0;

    /// <summary>
    /// A level's pick: one of the hero's own cards, and two more, each either another of the hero's own (<see cref="Tune.Boons.OwnShare"/>)
    /// or a boon at a rolled grade (Player/Boons.cs), now and then one of anyone's other cards; never two boons of one family.
    /// </summary>
    public static List<Upgrade> RollLevel(PlayerStats s, Random rng)
    {
        float W(Upgrade u) => u.Weight * Math.Max(0.05f, (u.MaxStacks - s.StackOf(u.Id)) / (float)u.MaxStacks);
        var own = Grows(s.Hero) ? Ability.Where(u => Upgrades.Available(u, s)).ToList()
                                : Upgrades.Chest.Where(u => u.Kind == UpgradeKind.Class && Upgrades.Available(u, s)).ToList();
        var shared = Shared.Where(u => !Retired.Contains(u.Id) && Upgrades.Available(u, s)).ToList();
        var picks = new List<Upgrade>();
        var families = new HashSet<string>();
        Upgrade TakeFrom(List<Upgrade> pool)
        {
            var u = Upgrades.Pick(pool.Where(x => !picks.Contains(x)).ToList(), 1, rng, W).FirstOrDefault();
            if (u != null) picks.Add(u);
            return u;
        }
        TakeFrom(own);
        for (int guard = 0; picks.Count < 3 && guard < 12; guard++)
        {
            double r = rng.NextDouble();
            if (r < Tune.Boons.OwnShare && TakeFrom(own) != null) continue;
            if (r > 0.9 && TakeFrom(shared) != null) continue;
            if (Boons.DealRolled(s, rng, families) is Upgrade b)
            {
                // (a Legendary roll may be one of the hero's infusions instead: the special itself changed)
                if (b.Grade == CardGrade.Legendary && rng.NextDouble() < Tune.Infusion.LevelChance && Infusions.Deal(s, rng, picks) is Upgrade x) b = x;
                picks.Add(b); continue;
            }
            if (TakeFrom(own) == null && TakeFrom(shared) == null) break;
        }
        // (the hero's own card isn't always on the left)
        for (int k = picks.Count - 1; k > 0; k--) { int j = rng.Next(k + 1); (picks[k], picks[j]) = (picks[j], picks[k]); }
        return picks;
    }

    /// <summary>A milestone's three: while the special has its own form, its two forms and one great thing; after, three great things
    /// (and if those run out, the hero's own cards).</summary>
    public static List<Upgrade> RollMilestone(PlayerStats s, Random rng)
    {
        var families = new HashSet<string>();
        if (!Grows(s.Hero))
        {
            // (the heroes with their own kits keep their milestones; the last card is a boon of Epic grade or better)
            var old = Upgrades.RollMilestone(s, rng);
            if (Boons.DealRolled(s, rng, families, CardGrade.Epic) is Upgrade ob) { if (old.Count >= 3) old[^1] = ob; else old.Add(ob); }
            return old;
        }
        var forms = Milestones.Where(u => u.For != null && Upgrades.Available(u, s)).ToList();
        var great = Milestones.Where(u => u.For == null && Upgrades.Available(u, s)).ToList();
        var picks = new List<Upgrade>(forms.Take(2));
        // (with the forms on offer: a great thing or an Epic-or-better boon beside them; after: two great things and such a boon)
        bool boon = picks.Count < 2 || rng.NextDouble() < Tune.Boons.MilestoneBoonChance;
        picks.AddRange(Upgrades.Pick(great, (boon ? 2 : 3) - picks.Count, rng, u => u.Weight));
        while (picks.Count < 3 && Boons.DealRolled(s, rng, families, CardGrade.Epic) is Upgrade b) picks.Add(b);
        if (picks.Count < 3) picks.AddRange(Upgrades.Pick(Ability.Where(u => Upgrades.Available(u, s) && !picks.Contains(u)).ToList(), 3 - picks.Count, rng, u => u.Weight));
        // (now and then the last card is one of the hero's infusions: the special changed, not only grown)
        if (picks.Count >= 3 && rng.NextDouble() < Tune.Infusion.MilestoneChance && Infusions.Deal(s, rng, picks) is Upgrade x) picks[^1] = x;
        return picks;
    }
}

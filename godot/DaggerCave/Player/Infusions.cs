using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// Infusions: Legendary cards that change what a hero's special does, not only how hard it strikes. Each leaves an element behind where
/// the special lands, a lingering zone that keeps on working (<see cref="Tune.Infusion"/>): the Meteor's burning crater, the Cryo Comet's
/// field of frost that keeps on building Frost in all it holds, the Whirlwind's flame cyclone... Two for each hero, and a hero may take
/// both (the special then leaves both behind); a second copy makes one Ascended: wider, longer, harder.
/// </summary>
public static class Infusions
{
    public sealed class Spec
    {
        public HeroKind Hero;
        public string Key, Name, Desc;
        public HeroElement El;
        /// <summary>The zone against the special's own reach, its life in seconds, and its strikes (as shares of <see cref="Tune.Infusion"/>'s).</summary>
        public float Radius = 1f, Seconds = 1f, Damage = 1f, Build = 1f;
        /// <summary>Chances a strike sets alight or freezes solid; the pull toward its middle.</summary>
        public float Ignite, Freeze, Pull;
    }

    private static readonly Spec[] Specs =
    {
        new() { Hero = HeroKind.Elementalist, Key = "crater", Name = "Scorched Earth", El = HeroElement.Fire, Radius = 1.25f, Seconds = 1f, Damage = 1.2f, Ignite = 0.25f,
            Desc = "The Meteor leaves a burning crater: it scorches and sets alight all in it for {s} s." },
        new() { Hero = HeroKind.Elementalist, Key = "cryo", Name = "Cryo Comet", El = HeroElement.Frost, Radius = 1.4f, Seconds = 1f, Damage = 0.6f, Build = 1.6f, Freeze = 0.08f,
            Desc = "The Meteor falls as a mega cryo comet: a field of frost that keeps building Frost in all it holds for {s} s." },
        new() { Hero = HeroKind.Swordsman, Key = "flame", Name = "Flame Cyclone", El = HeroElement.Fire, Radius = 1.2f, Seconds = 0.8f, Damage = 1.1f, Ignite = 0.2f,
            Desc = "The Whirlwind leaves a cyclone of fire spinning where it ended, burning all in it for {s} s." },
        new() { Hero = HeroKind.Swordsman, Key = "vortex", Name = "Frost Vortex", El = HeroElement.Frost, Radius = 1.3f, Seconds = 0.8f, Damage = 0.6f, Build = 1.3f, Pull = 50f,
            Desc = "The Whirlwind leaves a vortex of frost that drags creatures in and builds Frost in them for {s} s." },
        new() { Hero = HeroKind.Warden, Key = "magma", Name = "Magma Fissure", El = HeroElement.Fire, Radius = 0.8f, Seconds = 1f, Damage = 1.2f, Ignite = 0.25f,
            Desc = "The Great Bash splits the ground: a fissure of magma that burns all before you for {s} s." },
        new() { Hero = HeroKind.Warden, Key = "glacier", Name = "Glacier Crash", El = HeroElement.Frost, Radius = 0.85f, Seconds = 1f, Damage = 0.7f, Build = 1.4f, Freeze = 0.12f,
            Desc = "The Great Bash raises a glacier: ice that builds Frost, and may freeze solid, all before you for {s} s." },
        new() { Hero = HeroKind.Vitalist, Key = "thorns", Name = "Thornfield", El = HeroElement.Nature, Radius = 1f, Seconds = 1f, Damage = 1f, Build = 1.3f,
            Desc = "The Bloom leaves a field of thorns that tears at and entangles all in it for {s} s." },
        new() { Hero = HeroKind.Vitalist, Key = "tide", Name = "Tidepool", El = HeroElement.Water, Radius = 1f, Seconds = 1f, Damage = 0.8f, Build = 1.5f, Pull = 35f,
            Desc = "The Bloom wells up as a whirling tidepool that soaks and draws in all in it for {s} s." },
        new() { Hero = HeroKind.Aegis, Key = "sunfire", Name = "Sunfire", El = HeroElement.Fire, Radius = 1.1f, Seconds = 0.9f, Damage = 1.2f, Ignite = 0.25f,
            Desc = "Each pillar of the Judgement leaves holy fire burning where it fell for {s} s." },
        new() { Hero = HeroKind.Aegis, Key = "gale", Name = "Gale Sanctum", El = HeroElement.Wind, Radius = 1.4f, Seconds = 0.9f, Damage = 0.8f, Build = 0f, Pull = 70f,
            Desc = "The Judgement leaves a whirling gale that drags creatures to where it fell, and swallows their shots, for {s} s." },
        new() { Hero = HeroKind.Rogue, Key = "haze", Name = "Shadow Haze", El = HeroElement.Dark, Radius = 1f, Seconds = 0.9f, Damage = 0.8f, Build = 1.5f,
            Desc = "The Death Blossom leaves a haze of shadow that blinds and lays open all in it for {s} s." },
        new() { Hero = HeroKind.ShapeShifter, Key = "thicket", Name = "Feral Thicket", El = HeroElement.Nature, Radius = 1.1f, Seconds = 0.9f, Damage = 1.1f, Build = 1.3f,
            Desc = "The Apex Form's roar leaves a thicket of thorns about you that tears at and entangles all in it for {s} s." },
        new() { Hero = HeroKind.ShapeShifter, Key = "gale", Name = "Howling Gale", El = HeroElement.Wind, Radius = 1.4f, Seconds = 0.9f, Damage = 0.8f, Build = 0f, Pull = 60f,
            Desc = "The Apex Form's roar leaves a howling gale about you that drags creatures in and swallows their shots for {s} s." },
        new() { Hero = HeroKind.Rogue, Key = "venom", Name = "Venom Bloom", El = HeroElement.Nature, Radius = 1f, Seconds = 0.9f, Damage = 1.2f, Build = 1f,
            Desc = "The Death Blossom leaves a cloud of venom that eats at and entangles all in it for {s} s." },
    };

    private static readonly Dictionary<string, Spec> _byId = new();

    private static string SpecialOf(HeroKind h) => h switch
    {
        HeroKind.Swordsman => "Whirlwind", HeroKind.Elementalist => "Meteor", HeroKind.Warden => "Great Bash",
        HeroKind.Vitalist => "Bloom", HeroKind.Aegis => "Judgement", HeroKind.ShapeShifter => "Apex Form", _ => "Death Blossom",
    };
    private static string Slug(HeroKind h) => h.ToString().ToLowerInvariant()[..3];
    public static string IdOf(Spec s) => $"x_{Slug(s.Hero)}_{s.Key}";

    /// <summary>Every infusion card (Legendary; ids "x_ele_cryo").</summary>
    public static readonly List<Upgrade> All = Build();

    private static List<Upgrade> Build()
    {
        var list = new List<Upgrade>();
        foreach (var sp in Specs)
        {
            string id = IdOf(sp);
            _byId[id] = sp;
            float secs = Tune.Infusion.Seconds * sp.Seconds;
            list.Add(new Upgrade
            {
                Id = id, Name = sp.Name, Desc = sp.Desc.Replace("{s}", secs.ToString("0")) + " Taken again: Ascended, wider, longer and harder.",
                Icon = "spell", For = new[] { sp.Hero }, Ability = SpecialOf(sp.Hero), MaxStacks = 2, Grade = CardGrade.Legendary, Weight = 1f, Tier = UpgradeTier.Ability,
                Tags = new[] { "special", "infusion", sp.El.ToString().ToLowerInvariant() },
                When = s => Cards.Grows(s.Hero),
                Apply = (s, p) => { },
            });
        }
        return list;
    }

    public static bool IsInfusion(Upgrade u) => u != null && _byId.ContainsKey(u.Id);
    public static Spec SpecOf(Upgrade u) => u != null && _byId.TryGetValue(u.Id, out var sp) ? sp : null;

    /// <summary>The infusions this hero has, with how many times each was taken.</summary>
    public static IEnumerable<(Spec spec, int stacks)> Of(PlayerStats s)
    {
        foreach (var u in All)
        {
            int n = s.StackOf(u.Id);
            if (n > 0) yield return (_byId[u.Id], n);
        }
    }

    public static bool Any(PlayerStats s) => Of(s).Any();

    /// <summary>One of this hero's infusions they could still take (null if none).</summary>
    public static Upgrade Deal(PlayerStats s, Random rng, ICollection<Upgrade> besides)
    {
        var pool = All.Where(u => Upgrades.Available(u, s) && !besides.Contains(u)).ToList();
        return pool.Count == 0 ? null : pool[rng.Next(pool.Count)];
    }
}

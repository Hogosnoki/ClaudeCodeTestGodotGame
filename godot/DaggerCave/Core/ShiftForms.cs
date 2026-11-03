using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>What a Shape Shifter's special attack does.</summary>
public enum FormSpecial { Slam, Spin, Frenzy, Screech, Spit, Leap, Lash, Charge, Quake, Dive, Spores, Grip, Splash, Latch, Spray, Envenom }

/// <summary>
/// One creature the Shape Shifter can become: its look (the creature's own 3D model), how it moves,
/// how its ordinary attack goes (reach, damage, wind-up) and its own special attack. Damage and
/// health always scale with the Shape Shifter, never with the creature copied.
/// </summary>
public sealed class ShiftForm
{
    public byte Id;
    public string Key, Name, Set;
    /// <summary>The model's scale, how fast it runs and how high it jumps (x the hero's), and whether it flies (hold jump to flap).</summary>
    public float Size = 1f, Move = 1f, Jump = 1f;
    public bool Flier;
    /// <summary>The share of blows it shrugs off.</summary>
    public float Armor;
    /// <summary>How near the creatures count a hero in this form to be (x its distance): under 1, they come for it first (the tanks: the bear, the golem, the crab).</summary>
    public float Threat = 1f;
    /// <summary>Ordinary attack: reach (px), damage (x the base), seconds between, wind-up seconds, strike seconds, the model's wind-up and strike clips, knockback.</summary>
    public float Reach = 44f, Dmg = 1f, Cooldown = 0.7f, Wind = 0.18f, Strike = 0.2f, Knock = 160f;
    public string WindClip = "windup", StrikeClip = "strike";
    /// <summary>The special: what it is, its name, damage (x the base), range (px), seconds to come back.</summary>
    public FormSpecial Special;
    public string SpecialName = "";
    public float SpecDmg = 2f, SpecRange = 70f, SpecCd = 6f;
    /// <summary>The elemental Shape Shifter (<c>Player/Player.ShifterElements.cs</c>): the element of its kind (nature the creatures that walk, wind
    /// those that fly, water those of the water), and the name of its special greater (overcharged).</summary>
    public HeroElement El = HeroElement.Nature;
    public string GreaterName = "";
    /// <summary>The creature's own type (made fresh as the form's body).</summary>
    public System.Type Kind;
    /// <summary>A form whose creature has no driving of its own (Enemy.Driven.cs, <c>DrivenGeneric</c>): its attack is this one, a blow within
    /// <see cref="Reach"/> or, <see cref="Ranged"/>, a shot of <see cref="Shot"/> (an EnemyProjectile kind).</summary>
    public bool Ranged;
    public string Shot = "rock";
    /// <summary>It swims (moves freely in water; out of it, it walks and falls).</summary>
    public bool Swims;
    public string Icon => "form_" + Key;
    public string SpecIcon => "spec_" + Key;

    public static readonly ShiftForm[] All =
    {
        new() { Id = 1, Kind = typeof(Goblin), Key = "goblin", Name = "Goblin", Set = "goblin", Size = 1.3f, Move = 1.05f, Jump = 1f, Reach = 42f, Dmg = 1.0f, Cooldown = 0.65f, Wind = 0.2f, Strike = 0.2f, WindClip = "windup", StrikeClip = "strike",
            Special = FormSpecial.Slam, SpecialName = "Club Smash", SpecDmg = 2.2f, SpecRange = 62f, SpecCd = 5f , El = HeroElement.Nature, GreaterName = "Warlord's Smash" },
        new() { Id = 2, Kind = typeof(Skeleton), Key = "skeleton", Name = "Skeleton", Set = "skeleton", Size = 1.1f, Move = 1f, Jump = 1f, Reach = 54f, Dmg = 1.1f, Cooldown = 0.7f, Wind = 0.22f, Strike = 0.2f, WindClip = "windup", StrikeClip = "slash",
            Special = FormSpecial.Spin, SpecialName = "Bone Spin", SpecDmg = 1.8f, SpecRange = 66f, SpecCd = 6f , El = HeroElement.Nature, GreaterName = "Bone Storm" },
        new() { Id = 3, Kind = typeof(Rat), Key = "rat", Name = "Rat", Set = "rat", Size = 1.5f, Move = 1.45f, Jump = 1.15f, Reach = 26f, Dmg = 0.6f, Cooldown = 0.32f, Wind = 0.08f, Strike = 0.12f, WindClip = "windup", StrikeClip = "bite", Knock = 70f,
            Special = FormSpecial.Frenzy, SpecialName = "Gnaw", SpecDmg = 0.4f, SpecRange = 34f, SpecCd = 5f , El = HeroElement.Nature, GreaterName = "Plague Gnaw" },
        new() { Id = 4, Kind = typeof(Bat), Key = "bat", Name = "Bat", Set = "bat", Size = 1.4f, Move = 1.2f, Jump = 1f, Flier = true, Reach = 30f, Dmg = 0.6f, Cooldown = 0.45f, Wind = 0.1f, Strike = 0.14f, WindClip = "fly", StrikeClip = "dive", Knock = 80f,
            Special = FormSpecial.Latch, SpecialName = "Latch", SpecDmg = 0.72f, SpecRange = 150f, SpecCd = 8f , El = HeroElement.Wind, GreaterName = "Vampire Latch" },
        new() { Id = 5, Kind = typeof(Spider), Key = "spider", Name = "Spider", Set = "spider", Size = 0.75f, Move = 1.25f, Jump = 1.2f, Reach = 34f, Dmg = 0.8f, Cooldown = 0.5f, Wind = 0.12f, Strike = 0.16f, WindClip = "hang", StrikeClip = "pounce", Knock = 90f,
            Special = FormSpecial.Envenom, SpecialName = "Envenomate", SpecDmg = 0.6f, SpecRange = 140f, SpecCd = 7f , El = HeroElement.Nature, GreaterName = "Widow's Bite" },
        new() { Id = 6, Kind = typeof(Frog), Key = "frog", Name = "Frog", Set = "frog", Size = 1.4f, Move = 0.9f, Jump = 1.6f, Reach = 74f, Dmg = 0.8f, Cooldown = 0.8f, Wind = 0.16f, Strike = 0.22f, WindClip = "croak", StrikeClip = "tongue", Knock = 60f,
            Special = FormSpecial.Leap, SpecialName = "Pounce", SpecDmg = 2.0f, SpecRange = 60f, SpecCd = 5f , El = HeroElement.Water, GreaterName = "Monsoon Pounce" },
        new() { Id = 7, Kind = typeof(Scorpion), Key = "scorpion", Name = "Scorpion", Set = "scorpion", Size = 1.2f, Move = 1f, Jump = 0.9f, Armor = 0.15f, Reach = 50f, Dmg = 1.1f, Cooldown = 0.75f, Wind = 0.22f, Strike = 0.18f, WindClip = "sting_windup", StrikeClip = "sting",
            Special = FormSpecial.Spray, SpecialName = "Venom Spray", SpecDmg = 0.4f, SpecRange = 110f, SpecCd = 6f , El = HeroElement.Nature, GreaterName = "Deathstalker Spray" },
        new() { Id = 8, Kind = typeof(Bear), Key = "bear", Name = "Bear", Set = "bear", Size = 1.0f, Move = 0.95f, Jump = 0.9f, Armor = 0.35f, Threat = 0.5f, Reach = 52f, Dmg = 1.5f, Cooldown = 0.95f, Wind = 0.3f, Strike = 0.2f, WindClip = "rear", StrikeClip = "swipe", Knock = 280f,
            Special = FormSpecial.Charge, SpecialName = "Maul Charge", SpecDmg = 2.4f, SpecRange = 230f, SpecCd = 7f , El = HeroElement.Nature, GreaterName = "Grizzly Rampage" },
        new() { Id = 9, Kind = typeof(Golem), Key = "golem", Name = "Golem", Set = "golem", Size = 1.7f, Move = 0.7f, Jump = 0.8f, Armor = 0.4f, Threat = 0.6f, Reach = 48f, Dmg = 1.8f, Cooldown = 1.1f, Wind = 0.35f, Strike = 0.2f, WindClip = "slam_windup", StrikeClip = "slam", Knock = 320f,
            Special = FormSpecial.Quake, SpecialName = "Quake", SpecDmg = 2.6f, SpecRange = 120f, SpecCd = 8f , El = HeroElement.Nature, GreaterName = "Earthshatter" },
        new() { Id = 10, Kind = typeof(Hornet), Key = "hornet", Name = "Hornet", Set = "hornet", Size = 1.5f, Move = 1.25f, Jump = 1f, Flier = true, Reach = 40f, Dmg = 0.9f, Cooldown = 0.6f, Wind = 0.14f, Strike = 0.16f, WindClip = "aim", StrikeClip = "dive", Knock = 90f,
            Special = FormSpecial.Dive, SpecialName = "Dive Sting", SpecDmg = 2.0f, SpecRange = 200f, SpecCd = 6f , El = HeroElement.Wind, GreaterName = "Swarm Sting" },
        new() { Id = 11, Kind = typeof(Sporeling), Key = "sporeling", Name = "Sporeling", Set = "sporeling", Size = 1.5f, Move = 0.9f, Jump = 1f, Reach = 40f, Dmg = 0.7f, Cooldown = 0.7f, Wind = 0.2f, Strike = 0.2f, WindClip = "puff_windup", StrikeClip = "puff",
            Special = FormSpecial.Spores, SpecialName = "Spore Cloud", SpecDmg = 1.0f, SpecRange = 86f, SpecCd = 6f , El = HeroElement.Nature, GreaterName = "Blight Bloom" },
        new() { Id = 12, Kind = typeof(Crab), Key = "crab", Name = "Crab", Set = "crab_foe", Size = 1.6f, Move = 0.9f, Jump = 0.8f, Armor = 0.3f, Threat = 0.8f, Reach = 46f, Dmg = 1.3f, Cooldown = 0.85f, Wind = 0.25f, Strike = 0.2f, WindClip = "pinch_windup", StrikeClip = "pinch",
            Special = FormSpecial.Grip, SpecialName = "Vice Grip", SpecDmg = 2.0f, SpecRange = 54f, SpecCd = 6f , El = HeroElement.Water, GreaterName = "Crushing Tide" },
        new() { Id = 13, Kind = typeof(Fish), Key = "fish", Name = "Fish", Set = "fish", Size = 1.5f, Move = 1f, Jump = 1f, Reach = 28f, Dmg = 0.5f, Cooldown = 0.8f, Wind = 0.1f, Strike = 0.25f, WindClip = "swim", StrikeClip = "dart", Knock = 60f,
            Special = FormSpecial.Splash, SpecialName = "Splash", SpecDmg = 1.4f, SpecRange = 64f, SpecCd = 5f , El = HeroElement.Water, GreaterName = "Tidal Splash", Swims = true },
        // ---- every other creature of the cave that isn't a boss, a guardian or a structure: driven generically (their own second moves on the
        // second button; a blow or a shot of the form's on the attack button)
        new() { Id = 14, Kind = typeof(AcidNewt), Key = "newt", Name = "Acid Newt", Set = "newt", Size = 1.3f, Move = 1.05f, Jump = 1.05f, Reach = 40f, Dmg = 1.0f, Cooldown = 0.8f, Wind = 0.2f, Strike = 0.15f, Ranged = true, Shot = "acid",
            Special = FormSpecial.Spit, SpecialName = "Acid Spit", SpecRange = 220f, El = HeroElement.Nature, GreaterName = "Corrosion" },
        new() { Id = 15, Kind = typeof(Automaton), Key = "automaton", Name = "Automaton", Set = "automaton", Size = 1.2f, Move = 0.9f, Jump = 0.9f, Armor = 0.25f, Threat = 0.7f, Reach = 50f, Dmg = 1.6f, Cooldown = 0.9f, Wind = 0.3f, Strike = 0.2f, Knock = 260f,
            Special = FormSpecial.Charge, SpecialName = "Piston Charge", SpecDmg = 2.2f, SpecRange = 200f, El = HeroElement.Nature, GreaterName = "Overdrive" },
        new() { Id = 16, Kind = typeof(Boiler), Key = "boiler", Name = "Boiler", Set = "boiler", Size = 1.2f, Move = 0.8f, Jump = 0.85f, Armor = 0.3f, Threat = 0.7f, Reach = 46f, Dmg = 1.5f, Cooldown = 1.0f, Wind = 0.3f, Strike = 0.2f, Knock = 240f,
            Special = FormSpecial.Spores, SpecialName = "Steam Vent", El = HeroElement.Nature, GreaterName = "Scalding Vent" },
        new() { Id = 17, Kind = typeof(BrimstoneWorm), Key = "worm", Name = "Brimstone Worm", Set = "worm", Size = 1.3f, Move = 1.15f, Reach = 38f, Dmg = 1.2f, Cooldown = 0.6f, Wind = 0.12f, Strike = 0.15f, Swims = true,
            Special = FormSpecial.Lash, SpecialName = "Coil Lash", SpecDmg = 1.6f, SpecRange = 60f, El = HeroElement.Water, GreaterName = "Brimstone Lash" },
        new() { Id = 18, Kind = typeof(Cogwheel), Key = "cogwheel", Name = "Cogwheel", Set = "cogwheel", Size = 1.2f, Move = 1.35f, Jump = 1.1f, Reach = 40f, Dmg = 1.1f, Cooldown = 0.55f, Wind = 0.1f, Strike = 0.15f,
            Special = FormSpecial.Charge, SpecialName = "Runaway Roll", SpecDmg = 1.8f, SpecRange = 220f, El = HeroElement.Nature, GreaterName = "Runaway Gear" },
        new() { Id = 19, Kind = typeof(Eel), Key = "eel", Name = "Eel", Set = "eel", Size = 1.3f, Move = 1.2f, Reach = 36f, Dmg = 1.2f, Cooldown = 0.6f, Wind = 0.12f, Strike = 0.15f, Swims = true,
            Special = FormSpecial.Lash, SpecialName = "Coil Strike", El = HeroElement.Water, GreaterName = "Thunder Coil" },
        new() { Id = 20, Kind = typeof(EarthElemental), Key = "elem_earth", Name = "Earth Elemental", Set = "elem_earth", Size = 1.2f, Move = 0.75f, Jump = 0.85f, Armor = 0.4f, Threat = 0.6f, Reach = 54f, Dmg = 2.0f, Cooldown = 1.1f, Wind = 0.35f, Strike = 0.2f, Knock = 320f,
            Special = FormSpecial.Quake, SpecialName = "Rockfall Stomp", El = HeroElement.Nature, GreaterName = "Landslide" },
        new() { Id = 21, Kind = typeof(FireElemental), Key = "elem_fire", Name = "Fire Elemental", Set = "elem_fire", Size = 1.2f, Move = 1.1f, Reach = 46f, Dmg = 1.6f, Cooldown = 0.8f, Wind = 0.22f, Strike = 0.2f,
            Special = FormSpecial.Charge, SpecialName = "Flame Rush", El = HeroElement.Nature, GreaterName = "Wildfire Rush" },
        new() { Id = 22, Kind = typeof(FrostElemental), Key = "elem_frost", Name = "Frost Elemental", Set = "elem_frost", Size = 1.2f, Move = 0.85f, Jump = 0.9f, Armor = 0.25f, Reach = 52f, Dmg = 1.7f, Cooldown = 1.0f, Wind = 0.3f, Strike = 0.2f,
            Special = FormSpecial.Spin, SpecialName = "Blizzard Spin", El = HeroElement.Nature, GreaterName = "Whiteout Spin" },
        new() { Id = 23, Kind = typeof(NatureElemental), Key = "elem_nature", Name = "Nature Elemental", Set = "elem_nature", Size = 1.2f, Move = 0.9f, Reach = 50f, Dmg = 1.6f, Cooldown = 0.9f, Wind = 0.28f, Strike = 0.2f,
            Special = FormSpecial.Spores, SpecialName = "Thorn Ring", El = HeroElement.Nature, GreaterName = "Bramble Ring" },
        new() { Id = 24, Kind = typeof(WaterElemental), Key = "elem_water", Name = "Water Elemental", Set = "elem_water", Size = 1.2f, Move = 0.95f, Flier = true, Swims = true, Reach = 48f, Dmg = 1.5f, Cooldown = 0.9f, Wind = 0.25f, Strike = 0.2f,
            Special = FormSpecial.Splash, SpecialName = "Tidal Burst", El = HeroElement.Water, GreaterName = "Tsunami Burst" },
        new() { Id = 25, Kind = typeof(Gasbag), Key = "gasbag", Name = "Gasbag", Set = "gasbag", Size = 1.2f, Move = 0.75f, Flier = true, Reach = 40f, Dmg = 1.0f, Cooldown = 1.1f, Wind = 0.25f, Strike = 0.15f, Ranged = true, Shot = "acid",
            Special = FormSpecial.Spores, SpecialName = "Gas Glob", El = HeroElement.Wind, GreaterName = "Choking Cloud" },
        new() { Id = 26, Kind = typeof(LavaMonster), Key = "magma", Name = "Lava Monster", Set = "magma", Size = 1.2f, Move = 0.85f, Jump = 0.9f, Armor = 0.15f, Reach = 44f, Dmg = 1.4f, Cooldown = 1.0f, Wind = 0.3f, Strike = 0.15f, Ranged = true, Shot = "lava",
            Special = FormSpecial.Quake, SpecialName = "Eruption", El = HeroElement.Nature, GreaterName = "Caldera" },
        new() { Id = 27, Kind = typeof(Mite), Key = "mite", Name = "Mite", Set = "mite", Size = 1.6f, Move = 1.4f, Flier = true, Reach = 30f, Dmg = 0.55f, Cooldown = 0.3f, Wind = 0.06f, Strike = 0.1f, Knock = 50f,
            Special = FormSpecial.Frenzy, SpecialName = "Nip", SpecDmg = 0.35f, SpecRange = 30f, El = HeroElement.Wind, GreaterName = "Swarming Nip" },
        new() { Id = 28, Kind = typeof(Shardling), Key = "shardling", Name = "Shardling", Set = "shardling", Size = 1.3f, Move = 1.1f, Jump = 1.05f, Reach = 40f, Dmg = 1.1f, Cooldown = 0.7f, Wind = 0.18f, Strike = 0.15f,
            Special = FormSpecial.Spin, SpecialName = "Shard Fan", El = HeroElement.Nature, GreaterName = "Crystal Storm" },
        new() { Id = 29, Kind = typeof(Urchin), Key = "urchin", Name = "Urchin", Set = "urchin", Size = 1.3f, Move = 0.6f, Swims = true, Armor = 0.3f, Threat = 0.8f, Reach = 40f, Dmg = 1.2f, Cooldown = 0.8f, Wind = 0.2f, Strike = 0.15f,
            Special = FormSpecial.Spin, SpecialName = "Spine Volley", El = HeroElement.Water, GreaterName = "Spine Storm" },
    };

    /// <summary>The creatures that can't be copied, besides bosses and guardians: structures, the ones a form couldn't carry (the Delver
    /// burrows through rock), and the ones whose second move spends their body (the bomber, the frost wraith).</summary>
    private static bool Uncopyable(Enemy e) => e is Generator or Hive or Glitch or Dragon or CavernColossus or Delver or Bomber or FrostWraith;

    public static ShiftForm ById(int id) { foreach (var f in All) if (f.Id == id) return f; return null; }

    /// <summary>The form of a creature, or null if it can't be copied (bosses, the guardians, elementals, wraiths and the rest).</summary>
    public static ShiftForm For(Enemy e)
    {
        if (e == null || e.Dead || e.IsBoss || e.IsGuardian || e.Driven || Uncopyable(e)) return null;
        // (its own kind exactly; a kind made from another, such as the brimstone worm from the eel, has its own entry)
        var t = e.GetType();
        foreach (var f in All) if (f.Kind == t) return f;
        return null;
    }
}

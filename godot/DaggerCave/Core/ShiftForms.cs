using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>What a Shape Shifter's special attack does.</summary>
public enum FormSpecial { Slam, Spin, Frenzy, Screech, Spit, Leap, Lash, Charge, Quake, Dive, Spores, Grip }

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
    /// <summary>Ordinary attack: reach (px), damage (x the base), seconds between, wind-up seconds, strike seconds, the model's wind-up and strike clips, knockback.</summary>
    public float Reach = 44f, Dmg = 1f, Cooldown = 0.7f, Wind = 0.18f, Strike = 0.2f, Knock = 160f;
    public string WindClip = "windup", StrikeClip = "strike";
    /// <summary>The special: what it is, its name, damage (x the base), range (px), seconds to come back.</summary>
    public FormSpecial Special;
    public string SpecialName = "";
    public float SpecDmg = 2f, SpecRange = 70f, SpecCd = 6f;
    public string Icon => "form_" + Key;
    public string SpecIcon => "spec_" + Key;

    public static readonly ShiftForm[] All =
    {
        new() { Id = 1, Key = "goblin", Name = "Goblin", Set = "goblin", Size = 1.3f, Move = 1.05f, Jump = 1f, Reach = 42f, Dmg = 1.0f, Cooldown = 0.65f, Wind = 0.2f, Strike = 0.2f, WindClip = "windup", StrikeClip = "strike",
            Special = FormSpecial.Slam, SpecialName = "Club Smash", SpecDmg = 2.2f, SpecRange = 62f, SpecCd = 5f },
        new() { Id = 2, Key = "skeleton", Name = "Skeleton", Set = "skeleton", Size = 1.1f, Move = 1f, Jump = 1f, Reach = 54f, Dmg = 1.1f, Cooldown = 0.7f, Wind = 0.22f, Strike = 0.2f, WindClip = "windup", StrikeClip = "slash",
            Special = FormSpecial.Spin, SpecialName = "Bone Spin", SpecDmg = 1.8f, SpecRange = 66f, SpecCd = 6f },
        new() { Id = 3, Key = "rat", Name = "Rat", Set = "rat", Size = 1.5f, Move = 1.45f, Jump = 1.15f, Reach = 26f, Dmg = 0.6f, Cooldown = 0.32f, Wind = 0.08f, Strike = 0.12f, WindClip = "windup", StrikeClip = "bite", Knock = 70f,
            Special = FormSpecial.Frenzy, SpecialName = "Gnaw Frenzy", SpecDmg = 0.8f, SpecRange = 38f, SpecCd = 5f },
        new() { Id = 4, Key = "bat", Name = "Bat", Set = "bat", Size = 1.4f, Move = 1.2f, Jump = 1f, Flier = true, Reach = 30f, Dmg = 0.6f, Cooldown = 0.45f, Wind = 0.1f, Strike = 0.14f, WindClip = "fly", StrikeClip = "dive", Knock = 80f,
            Special = FormSpecial.Screech, SpecialName = "Screech", SpecDmg = 0.8f, SpecRange = 120f, SpecCd = 7f },
        new() { Id = 5, Key = "spider", Name = "Spider", Set = "spider", Size = 0.75f, Move = 1.25f, Jump = 1.2f, Reach = 34f, Dmg = 0.8f, Cooldown = 0.5f, Wind = 0.12f, Strike = 0.16f, WindClip = "hang", StrikeClip = "pounce", Knock = 90f,
            Special = FormSpecial.Spit, SpecialName = "Venom Spit", SpecDmg = 1.6f, SpecRange = 190f, SpecCd = 4f },
        new() { Id = 6, Key = "frog", Name = "Frog", Set = "frog", Size = 1.4f, Move = 0.9f, Jump = 1.6f, Reach = 74f, Dmg = 0.8f, Cooldown = 0.8f, Wind = 0.16f, Strike = 0.22f, WindClip = "croak", StrikeClip = "tongue", Knock = 60f,
            Special = FormSpecial.Leap, SpecialName = "Pounce", SpecDmg = 2.0f, SpecRange = 60f, SpecCd = 5f },
        new() { Id = 7, Key = "scorpion", Name = "Scorpion", Set = "scorpion", Size = 1.2f, Move = 1f, Jump = 0.9f, Armor = 0.15f, Reach = 50f, Dmg = 1.1f, Cooldown = 0.75f, Wind = 0.22f, Strike = 0.18f, WindClip = "sting_windup", StrikeClip = "sting",
            Special = FormSpecial.Lash, SpecialName = "Tail Lash", SpecDmg = 1.9f, SpecRange = 78f, SpecCd = 5f },
        new() { Id = 8, Key = "bear", Name = "Bear", Set = "bear", Size = 1.0f, Move = 0.95f, Jump = 0.9f, Armor = 0.2f, Reach = 52f, Dmg = 1.5f, Cooldown = 0.95f, Wind = 0.3f, Strike = 0.2f, WindClip = "rear", StrikeClip = "swipe", Knock = 280f,
            Special = FormSpecial.Charge, SpecialName = "Maul Charge", SpecDmg = 2.4f, SpecRange = 230f, SpecCd = 7f },
        new() { Id = 9, Key = "golem", Name = "Golem", Set = "golem", Size = 1.7f, Move = 0.7f, Jump = 0.8f, Armor = 0.35f, Reach = 48f, Dmg = 1.8f, Cooldown = 1.1f, Wind = 0.35f, Strike = 0.2f, WindClip = "slam_windup", StrikeClip = "slam", Knock = 320f,
            Special = FormSpecial.Quake, SpecialName = "Quake", SpecDmg = 2.6f, SpecRange = 120f, SpecCd = 8f },
        new() { Id = 10, Key = "hornet", Name = "Hornet", Set = "hornet", Size = 1.5f, Move = 1.25f, Jump = 1f, Flier = true, Reach = 40f, Dmg = 0.9f, Cooldown = 0.6f, Wind = 0.14f, Strike = 0.16f, WindClip = "aim", StrikeClip = "dive", Knock = 90f,
            Special = FormSpecial.Dive, SpecialName = "Dive Sting", SpecDmg = 2.0f, SpecRange = 200f, SpecCd = 6f },
        new() { Id = 11, Key = "sporeling", Name = "Sporeling", Set = "sporeling", Size = 1.5f, Move = 0.9f, Jump = 1f, Reach = 40f, Dmg = 0.7f, Cooldown = 0.7f, Wind = 0.2f, Strike = 0.2f, WindClip = "puff_windup", StrikeClip = "puff",
            Special = FormSpecial.Spores, SpecialName = "Spore Cloud", SpecDmg = 1.0f, SpecRange = 86f, SpecCd = 6f },
        new() { Id = 12, Key = "crab", Name = "Crab", Set = "crab_foe", Size = 1.6f, Move = 0.9f, Jump = 0.8f, Armor = 0.3f, Reach = 46f, Dmg = 1.3f, Cooldown = 0.85f, Wind = 0.25f, Strike = 0.2f, WindClip = "pinch_windup", StrikeClip = "pinch",
            Special = FormSpecial.Grip, SpecialName = "Vice Grip", SpecDmg = 2.0f, SpecRange = 54f, SpecCd = 6f },
    };

    public static ShiftForm ById(int id) { foreach (var f in All) if (f.Id == id) return f; return null; }

    /// <summary>The form of a creature, or null if it can't be copied (bosses, the guardians, elementals, wraiths and the rest).</summary>
    public static ShiftForm For(Enemy e)
    {
        if (e == null || e.Dead || e.IsBoss || e.IsGuardian) return null;
        string key = e switch
        {
            Goblin => "goblin", Skeleton => "skeleton", Rat => "rat", Bat => "bat", Spider => "spider", Frog => "frog",
            Scorpion => "scorpion", Bear => "bear", Golem => "golem", Hornet => "hornet", Sporeling => "sporeling", Crab => "crab",
            _ => null,
        };
        if (key == null) return null;
        foreach (var f in All) if (f.Key == key) return f;
        return null;
    }
}

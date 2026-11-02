using System;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// What every creature is made of and how it strikes, for the sound of each meeting (Audio/MaterialSounds.cs): a sword on a golem chinks, on
/// a scorpion's shell it thocks, on a rat it thwacks; a bear's claws on the Warden's plate clank, on the Rogue's leathers they tear.
/// </summary>
public partial class Enemy
{
    /// <summary>What its body is (a creature whose armour has broken sounds like the flesh beneath).</summary>
    public virtual Body BodyMaterial
    {
        get
        {
            var b = this switch
            {
                Skeleton => Body.Bone,
                EngineWarden or Automaton or Cogwheel or Boiler or Glitch => Body.Metal,
                Golem or Shardling or EarthElemental or CavernColossus or LavaMonster or Generator => Body.Stone,
                Scorpion or Spider or Hornet or Crab or Mite or Urchin or Bomber => Body.Chitin,
                NatureElemental or Sporeling or Hive => Body.Wood,
                FrostElemental or FrostWraith => Body.Ice,
                WaterElemental or Gasbag => Body.Gel,
                FireElemental => Body.Flame,
                _ => Body.Flesh,
            };
            return ArmorBroken && b is Body.Stone or Body.Metal ? Body.Flesh : b;
        }
    }

    /// <summary>How its own blows land (bites and claws cut, fists and clubs thud, stingers pierce, elementals strike with their element).</summary>
    public virtual Strike StrikeKind => this switch
    {
        Golem or EarthElemental or CavernColossus or Automaton or Cogwheel or Goblin or Frog or Crab or Delver => Strike.Blunt,
        Hornet or Scorpion or Mite or Urchin or Spider or Bat => Strike.Pierce,
        FireElemental or LavaMonster or Dragon => Strike.Fire,
        FrostElemental or FrostWraith => Strike.Frost,
        WaterElemental or Gasbag or AcidNewt => Strike.Water,
        NatureElemental or Sporeling => Strike.Nature,
        _ => Strike.Blade,
    };

    /// <summary>The blow being taken now: what struck (its element, or the hero's weapon when it's a plain blow).</summary>
    private Strike _strikeNow = Strike.Blade;

    /// <summary>What a blow of this kind, from the hero who struck it, is: a sword's edge, a dagger's point, the Warden's club, a caster's bolt, an element.</summary>
    private Strike StrikeOf(DamageKind kind)
    {
        switch (kind)
        {
            case DamageKind.Fire: return Strike.Fire;
            case DamageKind.Frost: return Strike.Frost;
            case DamageKind.Water: return Strike.Water;
            case DamageKind.Nature: return Strike.Nature;
        }
        var hero = StrikerHero();
        return hero?.Stats.Hero switch
        {
            HeroKind.Swordsman or HeroKind.ShapeShifter or HeroKind.Automaton => Strike.Blade,
            HeroKind.Rogue => Strike.Pierce,
            HeroKind.Warden => Strike.Blunt,
            HeroKind.Elementalist or HeroKind.Vitalist or HeroKind.Aegis => Strike.Bolt,
            _ => Strike.Blade,
        };
    }

    /// <summary>The cave's own stone, by biome: ice in the frozen ones, iron in the clockwork, roots and fungus where they grow, rock elsewhere.</summary>
    public static Body TerrainMaterial => G.Biome?.Id switch
    {
        BiomeId.Frost or BiomeId.Crystal => Body.Ice,
        BiomeId.Clockwork => Body.Metal,
        BiomeId.Roots or BiomeId.Fungal => Body.Wood,
        _ => Body.Stone,
    };

    /// <summary>What a creature's shot is when it lands (a thrown rock thuds, spit and acid splash, fire burns, ice and crystal cut cold, silk lashes).</summary>
    public static Strike ShotStrike(string kind) => kind switch
    {
        "fire" or "lava" => Strike.Fire,
        "ice" or "crystal" or "icicle" => Strike.Frost,
        "spit" or "acid" => Strike.Water,
        "web" => Strike.Nature,
        _ => Strike.Blunt,
    };
}

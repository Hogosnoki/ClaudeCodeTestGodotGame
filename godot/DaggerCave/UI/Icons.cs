using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The painted icons (tools/icons/make_icons.py): full-colour ability and relic icons, and the
/// white minimalist symbols for the kinds of reward. Loaded once, by name, from Art/Icons.
/// </summary>
public static class Icons
{
    private static readonly Dictionary<string, Texture2D> Cache = new();

    public static Texture2D Get(string name)
    {
        if (name == null) return null;
        if (Cache.TryGetValue(name, out var t)) return t;
        string path = $"res://DaggerCave/Art/Icons/{name}.png";
        if (ResourceLoader.Exists(path)) t = GD.Load<Texture2D>(path);
        else if (FileAccess.FileExists(path))
        {
            // (a new file the editor hasn't imported yet)
            var img = Image.LoadFromFile(ProjectSettings.GlobalizePath(path));
            if (img != null) t = ImageTexture.CreateFromImage(img);
        }
        Cache[name] = t;
        return t;
    }

    /// <summary>The icon of an ability, from the label its HUD square carries.</summary>
    public static string AbilityFor(string label)
    {
        string w = label.Split(' ')[0].ToUpperInvariant();
        return w switch
        {
            "CHARGE" or "CHARGED" => "ability_charge",
            "HEAVE" => "ability_heave",
            "DASH" => "ability_dash",
            "BASH" => "ability_bash",
            "HEAL" => "ability_heal",
            "RUPTURE" => "ability_rupture",
            "RECALL" => "ability_recall",
            "TETHER" => "ability_tether",
            "BARRIER" => "ability_barrier",
            "BURDEN" => "ability_burden",
            "SMITE" => "ability_smite",
            "STORM" => "ability_blizzard",
            "FIRE" => "ability_firestorm",
            "SNAP" => "ability_snap",
            "SHIFT" => "shift",
            "SHOUT" => "sup_shout", "TAUNT" => "sup_taunt", "TAP" => "sup_tap", "STALAG" => "sup_stalag", "EXPOSE" => "sup_expose", "MARK" => "sup_mark", "HOWL" => "sup_howl",
            "GOBLIN" => "form_goblin", "SKELETON" => "form_skeleton", "RAT" => "form_rat", "BAT" => "form_bat", "SPIDER" => "form_spider", "FROG" => "form_frog",
            "SCORPION" => "form_scorpion", "BEAR" => "form_bear", "GOLEM" => "form_golem", "HORNET" => "form_hornet", "SPORELING" => "form_sporeling", "CRAB" => "form_crab",
            "CLUB" => "spec_goblin", "BONE" => "spec_skeleton", "GNAW" => "spec_rat", "SCREECH" => "spec_bat", "VENOM" => "spec_spider", "POUNCE" => "spec_frog",
            "TAIL" => "spec_scorpion", "MAUL" => "spec_bear", "QUAKE" => "spec_golem", "DIVE" => "spec_hornet", "SPORE" => "spec_sporeling", "VICE" => "spec_crab",
            _ => null,
        };
    }

    /// <summary>The symbol of a kind of reward.</summary>
    public static string CategoryFor(Upgrade u) => u.Icon == "skip" ? "cat_skip" : u.Kind switch
    {
        UpgradeKind.Class => "cat_class",
        UpgradeKind.Alteration => "cat_alteration",
        UpgradeKind.Conditional => "cat_conditional",
        UpgradeKind.RiskReward => "cat_sidegrade",
        UpgradeKind.Relic => "cat_relic",
        _ => "cat_generic",
    };
}

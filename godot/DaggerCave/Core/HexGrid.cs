using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>What a hex is: a small step, a real feature, a build-defining trade, or a side-grade for the loadout.</summary>
public enum HexKind { Root, Minor, Notable, Keystone, SideGrade }

public sealed class HexNode
{
    public string Id, Name, Desc, Parent;
    public HexBranch Branch;
    public HexKind Kind = HexKind.Minor;
    /// <summary>Steps out from the ember fire along its branch (the root is 1).</summary>
    public int Depth;
    /// <summary>Where it sits on the grid (axial hex coordinates), set by the layout.</summary>
    public Vector2I Cell;
    public int Embers, Mats;
    /// <summary>A class perk's rank this hex holds (the perk is applied once, at the rank owned).</summary>
    public string Perk;
    /// <summary>The side-grade it unlocks for the loadout.</summary>
    public string SideGrade;
    /// <summary>What it does to the hero at the start of a run (null: read where it matters).</summary>
    public Action<PlayerStats> Apply;
}

public sealed class HexBranch
{
    public string Id, Name, Material, MatName;
    public Color Color;
    /// <summary>A hero's branch (its hexes only act for that hero; its material is that hero's essence).</summary>
    public HeroKind? Hero;
    /// <summary>The branch can be walked (its feature or hero unlocked), and why not.</summary>
    public Func<bool> Open;
    public string Locked;
    public float Angle;
    public readonly List<HexNode> Nodes = new();
    public HexNode Root => Nodes[0];
}

/// <summary>
/// The Ember Grid: every lasting advancement, on a field of hexes branching out from the ember fire at the camp. A branch for each
/// hero, the potions, the keys and vaults, the merchant, the relics and the Path; a branch opens once its hero or feature is
/// unlocked, and a hex can be bought once the one before it is owned, for embers and the branch's own material (found in runs, in
/// place of a drop the hero couldn't use: see <see cref="Tune.Grid"/>). The hexes' effects are read where they matter (Meta's
/// potion, Path and merchant numbers) or applied to a hero as a run begins (<see cref="ApplyRun"/>).
/// </summary>
public static class HexGrid
{
    public static readonly List<HexBranch> Branches = new();
    public static readonly List<HexNode> Nodes = new();
    private static readonly Dictionary<string, HexNode> ById = new();
    /// <summary>The hexes owned (kept in the meta save) and the materials held, by key.</summary>
    public static readonly HashSet<string> Owned = new();
    public static readonly Dictionary<string, int> Mats = new();
    public const string Center = "center";

    /// <summary>The grid is worth showing: some embers, a material or a hex beyond the ones every save starts with.</summary>
    public static bool Shown => Meta.Embers > 0 || Mats.Values.Any(v => v > 0) || Owned.Any(id => id != "aegis_oath");

    public static HexNode Find(string id) => ById.TryGetValue(id, out var n) ? n : null;
    public static bool Owns(string id) => Owned.Contains(id);
    /// <summary>How many of a chain are owned (ids <c>prefix1</c>, <c>prefix2</c>...).</summary>
    public static int Count(string prefix) { int n = 0; while (Owned.Contains($"{prefix}{n + 1}")) n++; return n; }
    public static int Mat(string key) => Mats.TryGetValue(key, out var n) ? n : 0;
    public static string EssenceKey(HeroKind h) => "essence_" + h.ToString().ToLowerInvariant();
    public static string MatName(string key) => key switch
    {
        "reagent" => "Reagent", "pearl" => "Obsidian Pearl", "tumbler" => "Tumbler", "seal" => "Guild Seal", "dust" => "Relic Dust",
        _ when key.StartsWith("essence_") => $"{HeroLabel(Enum.GetValues<HeroKind>().First(h => EssenceKey(h) == key))} Essence",
        _ => key,
    };
    public static Color MatColor(string key) => key switch
    {
        "reagent" => new Color(1f, 0.4f, 0.55f), "pearl" => new Color(0.7f, 0.6f, 1f), "tumbler" => new Color(1f, 0.82f, 0.4f),
        "seal" => new Color(0.95f, 0.7f, 0.25f), "dust" => new Color(0.55f, 0.95f, 0.85f), _ => new Color(0.85f, 0.9f, 1f),
    };
    public static string HeroLabel(HeroKind h) => h == HeroKind.ShapeShifter ? "Shape Shifter" : h.ToString();

    /// <summary>A material found in a run: kept at once (a run that ends badly keeps it too).</summary>
    public static void Gain(string key, int n = 1)
    {
        Mats[key] = Mat(key) + n;
        Meta.Save();
    }

    public static string MatOf(HexNode n) => n.Branch.Material;

    /// <summary>Can it be bought now (and why not)?</summary>
    public static bool CanBuy(HexNode n, out string why)
    {
        why = null;
        if (Owns(n.Id)) { why = "OWNED"; return false; }
        if (!n.Branch.Open()) { why = n.Branch.Locked; return false; }
        if (n.Parent != Center && !Owns(n.Parent)) { why = "REACH IT FROM A HEX YOU OWN"; return false; }
        if (Meta.Embers < n.Embers) { why = $"NEEDS {n.Embers - Meta.Embers} MORE EMBERS"; return false; }
        if (Mat(MatOf(n)) < n.Mats) { why = $"NEEDS {n.Mats - Mat(MatOf(n))} MORE {MatName(MatOf(n)).ToUpperInvariant()}"; return false; }
        return true;
    }

    public static bool Buy(HexNode n)
    {
        if (!CanBuy(n, out _)) return false;
        Meta.Embers -= n.Embers;
        Mats[MatOf(n)] = Mat(MatOf(n)) - n.Mats;
        Owned.Add(n.Id);
        Meta.Save();
        Achievements.CheckState();
        return true;
    }

    /// <summary>A run begins: what the grid gives this hero (the general branches' stats, and their own branch's).</summary>
    public static void ApplyRun(PlayerStats s)
    {
        var perks = new Dictionary<string, int>();
        foreach (var n in Nodes)
        {
            if (!Owns(n.Id) || (n.Branch.Hero is HeroKind h && h != s.Hero)) continue;
            n.Apply?.Invoke(s);
            if (n.Perk != null) perks[n.Perk] = perks.TryGetValue(n.Perk, out var r) ? r + 1 : 1;
        }
        foreach (var (id, rank) in perks) ClassPerks.Find(id)?.Apply(s, rank);
    }

    /// <summary>A line for the hero's preparation: how much of their branch is theirs.</summary>
    public static string Summary(HeroKind h)
    {
        var b = Branches.First(x => x.Hero == h);
        int got = b.Nodes.Count(n => Owns(n.Id));
        return $"Ember Grid: {got} of {b.Nodes.Count} hexes on the {HeroLabel(h)}'s branch";
    }

    /// <summary>The side-grades a hero may bring (unlocked on their branch).</summary>
    public static bool SideGradeOwned(string upgradeId) => Owns("alt_" + upgradeId);

    // ================================================================== the content

    private static HexBranch Br(string id, string name, string mat, Color col, HeroKind? hero, Func<bool> open, string locked)
    {
        var b = new HexBranch { Id = id, Name = name, Material = mat, Color = col, Hero = hero, Open = open, Locked = locked };
        b.MatName = MatName(mat);
        Branches.Add(b);
        return b;
    }

    private static HexNode N(HexBranch b, string id, string name, string desc, string parent, HexKind kind = HexKind.Minor, Action<PlayerStats> apply = null)
    {
        var n = new HexNode { Id = id, Name = name, Desc = desc, Parent = parent ?? Center, Branch = b, Kind = kind, Apply = apply };
        b.Nodes.Add(n); Nodes.Add(n); ById[id] = n;
        return n;
    }

    /// <summary>A chain of ranks (ids prefix1..N), each hanging from the one before; returns the last.</summary>
    private static HexNode Ranks(HexBranch b, string prefix, string name, string parent, params string[] descs)
    {
        HexNode last = null;
        for (int k = 0; k < descs.Length; k++)
            last = N(b, $"{prefix}{k + 1}", $"{name} {new[] { "I", "II", "III", "IV", "V" }[k]}", descs[k], k == 0 ? parent : $"{prefix}{k}");
        return last;
    }

    static HexGrid()
    {
        // ---------------------------------------------------------------- the potion (Reagents: a potion found with a full belt)
        var pot = Br("potion", "THE POTION", "reagent", new Color(1f, 0.4f, 0.55f), null, () => true, "");
        N(pot, "pot_root", "Apothecary", "Potions heal 5% more at once.", null, HexKind.Root);
        N(pot, "pot_flask", "Second Flask", "Carry up to 2 potions (a third only from a Deep Flask found on the way).", "pot_root", HexKind.Notable);
        Ranks(pot, "pot_quick", "Quick Draught", "pot_root", "Potions heal 10% more at once.", "Potions heal 10% more at once (+20%).", "Potions heal 10% more at once (+30%).");
        Ranks(pot, "pot_linger", "Lingering", "pot_root", "Potions heal 10% more over time.", "Potions heal 10% more over time (+20%).", "Potions heal 10% more over time (+30%).");
        Ranks(pot, "pot_brew", "Strong Brew", "pot_linger1", "The heal over time takes 2 s less (13 s).", "The heal over time takes 2 s less (11 s).", "The heal over time takes 3 s less (8 s).");
        Ranks(pot, "pot_forage", "Forager", "pot_flask", "Creatures drop potions 1% more often.", "Another +1% while you carry no potion.");
        N(pot, "pot_shared", "Shared Draught", "Drinking a potion heals the friends close by for 40% of what it heals you.", "pot_quick2", HexKind.Notable);
        N(pot, "pot_volatile", "Volatile Brew", "Drinking a potion bursts about you for 25 damage, but potions heal 15% less.", "pot_quick3", HexKind.Keystone);

        // ---------------------------------------------------------------- the Path (Obsidian Pearls: from guardians, now and then)
        var path = Br("path", "THE PATH", "pearl", new Color(0.7f, 0.6f, 1f), null, () => true, "");
        N(path, "path_root", "Wayfarer", "+3% experience.", null, HexKind.Root);
        Ranks(path, "fort_mile", "Waypoints", "path_root", "A milestone pick every 7 levels.", "A milestone pick every 6 levels.", "A milestone pick every 5 levels.");
        Ranks(path, "fort_xp", "Studious", "path_root", "+5% experience.", "+7% more experience (12%).", "+8% more experience (20%).");
        Ranks(path, "fort_elite", "Trophies", "fort_xp1", "+5% experience from elites.", "+7% more from elites (12%).", "+8% more from elites (20%).");
        Ranks(path, "fort_gold", "Prospecting", "path_root", "Gold you find is worth 10% more.", "Gold you find is worth 20% more.", "Gold you find is worth 30% more.");
        Ranks(path, "fort_reroll", "Second Thoughts", "fort_mile1", "Once a run, reroll the merchant's wares or a choice of cards.", "Twice a run.", "Three times a run.");
        N(path, "fort_pact", "Ember Pact", "Guardians give 40% more embers, but you take 15% more damage.", "fort_mile3", HexKind.Keystone, s => s.DamageTakenMult *= 1.15f);

        // ---------------------------------------------------------------- keys and vaults (Tumblers: a key found with full key slots)
        var keys = Br("keys", "KEYS AND VAULTS", "tumbler", new Color(1f, 0.82f, 0.4f), null, () => true, "");
        N(keys, "key_root", "Locksmith", "Vaults' chests deal their cards a little better: one more class card to choose from.", null, HexKind.Root);
        N(keys, "key_ring", "Key Ring", "Carry one more key.", "key_root", HexKind.Minor, s => s.KeyCapBonus += 1);
        N(keys, "key_tumbler", "Tumbler's Luck", "Opening a vault leaves a Tumbler, half the time.", "key_root");
        N(keys, "key_rich", "Rich Vaults", "A vault's chest holds one more bargain to choose from.", "key_ring", HexKind.Notable);
        N(keys, "key_pick", "Lockpick", "A vault gate you have no key for opens for 40 gold.", "key_tumbler", HexKind.Notable);
        N(keys, "key_master", "Master Key", "A vault's chest also holds a relic, but you carry one key fewer.", "key_rich", HexKind.Keystone, s => s.KeyCapBonus -= 1);

        // ---------------------------------------------------------------- the merchant (Guild Seals: gold found while your purse is full)
        var shop = Br("shop", "THE MERCHANT", "seal", new Color(0.95f, 0.7f, 0.25f), null, () => Meta.MerchantKnown, "FREE THE MERCHANT TO OPEN THIS BRANCH");
        N(shop, "shop_root", "Regular", "The merchant's first deal-again at each stall costs nothing.", null, HexKind.Root, s => s.FreeRerolls += 1);
        Ranks(shop, "shop_price", "Fair Dealing", "shop_root", "The merchant's prices are 5% lower.", "The merchant's prices are 10% lower.", "The merchant's prices are 15% lower.");
        Ranks(shop, "shop_trade", "Trader", "shop_root", "One more relic trade with a friend at the stand each run (2).", "Two more relic trades with a friend each run (3).");
        N(shop, "shop_stock", "Overstocked", "The merchant has one more thing for sale.", "shop_price1", HexKind.Notable, s => s.ShopExtra += 1);
        N(shop, "shop_route", "Regular Route", "His stall is offered three times a run instead of twice.", "shop_trade1", HexKind.Notable);
        N(shop, "shop_favor", "Merchant's Favor", "A second relic is always on his table, but his cards cost 20% more.", "shop_price3", HexKind.Keystone);

        // ---------------------------------------------------------------- relics (Relic Dust: a relic salvaged from its chest)
        var rel = Br("relics", "RELICS", "dust", new Color(0.55f, 0.95f, 0.85f), null, () => true, "");
        N(rel, "rel_root", "Antiquarian", "Salvaging a relic gives 2 Relic Dust instead of 1.", null, HexKind.Root);
        Ranks(rel, "rel_sense", "Relic Sense", "rel_root", "Relic chests are 3% likelier in place of a shrine.", "Another 3% (6%).");
        N(rel, "rel_collect", "Collector", "Relic Collections turn up twice as often.", "rel_root");
        Ranks(rel, "rel_choice", "Discerning", "rel_sense1", "A relic chest holds a choice of 2 relics.", "A relic chest holds a choice of 3 relics.");
        N(rel, "rel_heirloom", "Heirloom", "Begin every run carrying a random common relic.", "rel_collect", HexKind.Notable);
        N(rel, "rel_covet", "Covetous", "Relic chests are 50% likelier, but shrines no longer heal you.", "rel_choice2", HexKind.Keystone);

        // ---------------------------------------------------------------- the heroes (their essence: a heart found at full health, as them)
        var keystones = new Dictionary<HeroKind, (string id, string name, string desc, Action<PlayerStats> apply)>
        {
            [HeroKind.Swordsman] = ("ks_sword", "Reckless Edge", "+20% damage, but your dash-slash comes back 25% slower.", s => { s.DamageMult *= 1.2f; s.DodgeCdMult *= 1.25f; }),
            [HeroKind.Warden] = ("ks_warden", "Living Wall", "Your shield is 25% stronger, but you move 10% slower.", s => { s.ShieldMult *= 1.25f; s.MoveSpeed *= 0.9f; }),
            [HeroKind.Vitalist] = ("ks_vit", "Blood Price", "Your heals are 30% stronger, but cost 20% more vital force.", s => { s.HealMult *= 1.3f; s.HealCostMult *= 1.2f; }),
            [HeroKind.Elementalist] = ("ks_ele", "Overflowing Well", "Hold 30 more alimus, but it comes back 15% slower.", s => { s.AlimusMax += 30f; s.AlimusRegenMult *= 0.85f; }),
            [HeroKind.Rogue] = ("ks_rogue", "Shadow Pact", "Vanish lasts 2 s longer, but you have 15% less health.", s => { s.VanishBonus += 2f; s.MaxHp *= 0.85f; }),
            [HeroKind.Aegis] = ("ks_aegis", "Martyr's Vow", "Your wards are 25% stronger, but you take 10% more damage.", s => { s.WardMult *= 1.25f; s.DamageTakenMult *= 1.1f; }),
            [HeroKind.ShapeShifter] = ("ks_shift", "Feral Heart", "Every form hits 20% harder, but your staff hits 30% softer.", s => { s.FormDmgMult *= 1.2f; s.PrimaryDamageMult *= 0.7f; }),
            [HeroKind.Automaton] = ("ks_auto", "Overclocked", "You swing 15% faster, but hold 25 less fuel.", s => { s.AttackSpeed *= 1.15f; s.FuelMax -= 25f; }),
        };
        var colors = new Dictionary<HeroKind, Color>
        {
            [HeroKind.Swordsman] = new(0.95f, 0.55f, 0.3f), [HeroKind.Warden] = new(0.55f, 0.75f, 1f), [HeroKind.Vitalist] = new(0.55f, 1f, 0.55f),
            [HeroKind.Elementalist] = new(1f, 0.6f, 0.2f), [HeroKind.Rogue] = new(0.75f, 0.55f, 0.95f), [HeroKind.Aegis] = new(1f, 0.92f, 0.55f),
            [HeroKind.ShapeShifter] = new(0.6f, 0.85f, 0.45f), [HeroKind.Automaton] = new(0.8f, 0.62f, 0.4f),
        };
        foreach (var h in Enum.GetValues<HeroKind>())
        {
            var hero = h;
            var b = Br("hero_" + h.ToString().ToLowerInvariant(), HeroLabel(h).ToUpperInvariant(), EssenceKey(h), colors[h], h, () => Meta.IsUnlocked(hero), $"FREE THE {HeroLabel(h).ToUpperInvariant()} TO OPEN THIS BRANCH");
            var perks = ClassPerks.All.Where(p => p.Hero == h && p.Id != "aegis_oath").ToList();
            HexNode root;
            if (h == HeroKind.Aegis)
            {
                // (the Aegis comes with her Bulwark Oath, owned from the day she is freed)
                root = N(b, "aegis_oath", "Bulwark Oath", "In a party, your second ability is Shared Burden (alone, a Humbling Light). Yours from the start.", null, HexKind.Root, s => s.AegisSupport = true);
            }
            else root = N(b, $"root_{h.ToString().ToLowerInvariant()}", $"{HeroLabel(h)}'s Resolve", "+5% health.", null, HexKind.Root, s => s.MaxHp *= 1.05f);
            HexNode trunkEnd = root;
            var chainHeads = new List<HexNode>();
            for (int k = 0; k < perks.Count; k++)
            {
                var p = perks[k];
                // (the first perk runs on as the trunk; the others hang off it, one step further out each)
                string parent = k == 0 ? root.Id : chainHeads.Count > 0 ? chainHeads[Math.Min(chainHeads.Count - 1, k - 1)].Id : root.Id;
                HexNode last = null;
                for (int r = 1; r <= p.MaxRank; r++)
                {
                    last = N(b, $"{p.Id}_{r}", p.MaxRank > 1 ? $"{p.Name} {new[] { "I", "II", "III" }[r - 1]}" : p.Name, p.Ranks[r - 1], r == 1 ? parent : $"{p.Id}_{r - 1}");
                    last.Perk = p.Id;
                    if (r == 1) chainHeads.Add(last);
                }
                if (k == 0) trunkEnd = last;
            }
            var alts = Upgrades.Chest.Where(u => u.Alteration && u.For != null && u.For.Contains(h)).ToList();
            for (int k = 0; k < alts.Count; k++)
            {
                var u = alts[k];
                var hang = chainHeads.Count > 0 ? chainHeads[k % chainHeads.Count] : root;
                var n = N(b, "alt_" + u.Id, u.Name, $"Side-grade for your loadout ({Upgrades.AbilityTitle(u.Ability, h)}): {u.Desc}", hang.Id, HexKind.SideGrade);
                n.SideGrade = u.Id;
            }
            var ks = keystones[h];
            N(b, ks.id, ks.name, ks.desc, trunkEnd.Id, HexKind.Keystone, ks.apply);
        }

        foreach (var b in Branches)
        {
            foreach (var n in b.Nodes) n.Depth = n.Parent == Center ? 1 : Find(n.Parent).Depth + 1;
            foreach (var n in b.Nodes) Price(n);
        }
        Layout();
    }

    /// <summary>What a hex costs: the farther out, the dearer; a feature more than a step; a keystone dearest of all.</summary>
    private static void Price(HexNode n)
    {
        int d = n.Depth;
        (n.Embers, n.Mats) = n.Kind switch
        {
            HexKind.Root => (6, 1),
            HexKind.Notable => (10 + 3 * d, 2 + d / 3),
            HexKind.Keystone => (30 + 2 * d, 5),
            HexKind.SideGrade => (8 + 2 * d, 2 + d / 4),
            _ => (4 + 2 * d, 1 + d / 3),
        };
        if (n.Id == "aegis_oath") (n.Embers, n.Mats) = (0, 0);
    }

    // ================================================================== the layout: branches fan out round the fire, each hex snapped to the grid

    public static readonly Dictionary<Vector2I, HexNode> AtCell = new();
    private static readonly Vector2I[] Dirs = { new(1, 0), new(1, -1), new(0, -1), new(-1, 0), new(-1, 1), new(0, 1) };

    /// <summary>A hex's centre (pointy-top hexes of the given radius).</summary>
    public static Vector2 ToPixel(Vector2I c, float r) => new(r * MathF.Sqrt(3f) * (c.X + c.Y / 2f), r * 1.5f * c.Y);

    public static Vector2I FromPixel(Vector2 p, float r)
    {
        float q = (MathF.Sqrt(3f) / 3f * p.X - p.Y / 3f) / r, rr = (2f / 3f * p.Y) / r;
        float x = q, z = rr, y = -x - z;
        float rx = MathF.Round(x), ry = MathF.Round(y), rz = MathF.Round(z);
        float dx = MathF.Abs(rx - x), dy = MathF.Abs(ry - y), dz = MathF.Abs(rz - z);
        if (dx > dy && dx > dz) rx = -ry - rz; else if (dy <= dz) rz = -rx - ry;
        return new Vector2I((int)rx, (int)rz);
    }

    public static int HexDist(Vector2I a, Vector2I b) { int dq = a.X - b.X, dr = a.Y - b.Y; return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(dq + dr)) / 2; }

    /// <summary>
    /// A radial tree: each branch gets a wedge round the fire as wide as its leaves need, and each hex splits its share of the wedge
    /// among the hexes hanging from it; a hex sits one step farther out than its parent, at the middle of its share, snapped to the
    /// nearest free cell.
    /// </summary>
    private static void Layout()
    {
        AtCell.Clear();
        // the fire, and a ring of rock round it
        var taken = new HashSet<Vector2I> { Vector2I.Zero };
        foreach (var d in Dirs) taken.Add(d);
        const float R = 1f, Step0 = 4.6f, StepD = 1.05f;
        var kids = Nodes.Where(n => n.Parent != Center).GroupBy(n => n.Parent).ToDictionary(g => g.Key, g => g.ToList());
        var leaves = new Dictionary<string, float>();
        float Leaves(HexNode n)
        {
            if (leaves.TryGetValue(n.Id, out var v)) return v;
            v = kids.TryGetValue(n.Id, out var ks) ? Math.Max(1f, ks.Sum(Leaves)) : 1f;
            return leaves[n.Id] = v;
        }
        // (a little room between wedges, and the small branches not too thin)
        var weight = Branches.Select(b => Leaves(b.Root) + 1.5f).ToList();
        float total = weight.Sum(), at0 = -Mathf.Pi / 2 - Mathf.Tau * weight[0] / total / 2f;
        for (int bi = 0; bi < Branches.Count; bi++)
        {
            var b = Branches[bi];
            float span = Mathf.Tau * weight[bi] / total, pad = span * 0.75f / weight[bi];
            b.Angle = at0 + span / 2f;
            var share = new Dictionary<string, (float a0, float a1)> { [b.Root.Id] = (at0 + pad, at0 + span - pad) };
            at0 += span;
            // (parents before children: the branch's own order already is)
            foreach (var n in b.Nodes)
            {
                var (a0, a1) = share[n.Id];
                float ang = n == b.Root ? b.Angle : (a0 + a1) / 2f;
                float rad = (Step0 + (n.Depth - 1) * StepD) * MathF.Sqrt(3f) * R;
                var want = new Vector2(MathF.Cos(ang), MathF.Sin(ang)) * rad;
                n.Cell = Free(FromPixel(want, R), taken, want, R);
                taken.Add(n.Cell);
                AtCell[n.Cell] = n;
                if (!kids.TryGetValue(n.Id, out var ks)) continue;
                float sum = ks.Sum(Leaves), c = a0;
                foreach (var k in ks) { float w = (a1 - a0) * Leaves(k) / sum; share[k.Id] = (c, c + w); c += w; }
            }
        }
    }

    /// <summary>The neighbour of a cell nearest a point.</summary>
    private static Vector2I Step(Vector2I from, Vector2 toward, float r)
    {
        Vector2I best = from + Dirs[0]; float bd = float.MaxValue;
        foreach (var d in Dirs) { var c = from + d; float dd = ToPixel(c, r).DistanceTo(toward); if (dd < bd) { bd = dd; best = c; } }
        return best;
    }

    /// <summary>The nearest free cell to a wanted one (ring by ring), the nearest the wanted point first.</summary>
    private static Vector2I Free(Vector2I want, HashSet<Vector2I> taken, Vector2 at, float r)
    {
        if (!taken.Contains(want)) return want;
        for (int ring = 1; ring < 12; ring++)
        {
            Vector2I best = want; float bd = float.MaxValue;
            for (int dq = -ring; dq <= ring; dq++)
                for (int dr = -ring; dr <= ring; dr++)
                {
                    var c = new Vector2I(want.X + dq, want.Y + dr);
                    if (HexDist(c, want) != ring || taken.Contains(c)) continue;
                    float dd = ToPixel(c, r).DistanceTo(at);
                    if (dd < bd) { bd = dd; best = c; }
                }
            if (bd < float.MaxValue) return best;
        }
        return want;
    }

    // ================================================================== saved (with the meta save)

    public static Godot.Collections.Dictionary ToDict()
    {
        var m = new Godot.Collections.Dictionary();
        foreach (var kv in Mats) m[kv.Key] = kv.Value;
        return new Godot.Collections.Dictionary { ["owned"] = new Godot.Collections.Array(Owned.Select(x => (Variant)x)), ["mats"] = m };
    }

    public static void FromDict(Godot.Collections.Dictionary d)
    {
        Owned.Clear(); Mats.Clear();
        if (d == null) return;
        if (d.ContainsKey("owned")) foreach (var v in d["owned"].AsGodotArray()) Owned.Add((string)v);
        if (d.ContainsKey("mats")) foreach (var kv in d["mats"].AsGodotDictionary()) Mats[(string)kv.Key] = (int)kv.Value;
    }

    /// <summary>`--dumpcards`: the grid as Markdown (by branch, each hex with its cost).</summary>
    public static string Dump()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## The Ember Grid (every lasting advancement; embers plus the branch's material)\n");
        sb.AppendLine("Materials come in place of a drop the hero couldn't use: Reagents (a potion with a full belt), Tumblers (a key with full key slots),");
        sb.AppendLine("Guild Seals (gold while the purse is full), Relic Dust (a relic salvaged), a hero's Essence (a heart at full health), Obsidian Pearls (guardians, now and then).\n");
        foreach (var b in Branches)
        {
            sb.AppendLine($"**{b.Name}** ({b.MatName}{(b.Locked != "" ? $"; {b.Locked.ToLowerInvariant()}" : "")})\n");
            foreach (var n in b.Nodes)
                sb.AppendLine($"- {(n.Kind is HexKind.Keystone ? "★ " : n.Kind == HexKind.Notable ? "◆ " : n.Kind == HexKind.SideGrade ? "⇄ " : "")}**{n.Name}** ({n.Embers} embers, {n.Mats} {b.MatName}): {n.Desc}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

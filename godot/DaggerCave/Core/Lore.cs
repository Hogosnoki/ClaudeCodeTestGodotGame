using System;
using System.Collections.Generic;

namespace DaggerCave;

/// <summary>
/// The story of the game, in one place: why anyone goes down into the Dagger, why they keep going, who is caged in it and
/// what waits at the bottom. The words are shown on the story card (main menu, and the first time a run is begun), as a
/// line on each loading screen, and when a caged hero is freed.
/// </summary>
public static class Lore
{
    public const string Name = "The Dagger";

    /// <summary>The whole story so far, as paragraphs (the story card shows them one under another).</summary>
    public static readonly string[] Premise =
    {
        "Nine winters ago the river Wend ran into the earth and did not come out. It went down a long black cleft that split the meadow above Hearthmere in a single night, and the people call that cleft the Dagger. With the river went the green of the valley. The wells went bitter, the fields went to dust, and a warm wind began to blow up out of the dark, smelling of ash.",
        "The first to go down were the Delvers of the Deepsilver Guild, who had worked the upper galleries for years and knew the old works under the meadow. They went for two reasons: for the silver, which grew richer with every foot of depth, and then, when the river vanished, to find where the water had gone. Three expeditions followed them, each larger than the last: the Guild's own relief party, the Spring Order's pilgrims to the drowned shrine, and the Crown's last company of picked fighters. None came back.",
        "The creatures of the Deep do not kill everyone they catch. They cage the living and carry them down as a tithe to what sleeps on the Mother Spring at the very bottom: the Elder Dragon, drinking the rivers of the world to feed a fire older than the valley. Until it is stopped the Deep will drink every river in the land, and the people of Hearthmere with them.",
        "So you go down. Whoever dies in the Dagger wakes at the fire at its mouth, cold, thirsty and a little less themselves. The old Delvers say the fire is a Guild lamp that never went out, kept alight by what the dying leave behind: embers. Every guardian's ember feeds it, and the stronger it burns the further down it lets you go before it calls you back.",
        "Free the ones in the cages: they know the way. Find the Mother Spring. Let the river go home.",
    };


    /// <summary>
    /// The Delvers' journal: a page left by the lamp of a lost expedition in each kind of level (the lamps in the dead ends of the
    /// caves). Who wrote it, and what they saw.
    /// </summary>
    public static readonly (BiomeId biome, string author, string text)[] Journal =
    {
        (BiomeId.Den, "Marta Quill, Delver", "The bears have been gathering bones into piles. Not eating them. Arranging them. I do not like being watched by something that tidy."),
        (BiomeId.Roots, "Hob Tarrow, rope-hand", "The roots go down further than the rope does. They are drinking, same as us. They are winning."),
        (BiomeId.Nest, "Ines Vell, Delver", "Webs on the roof and a hum in the walls. If you hear the hum stop, run. Do not look up first."),
        (BiomeId.Ruins, "Master Orrin, Guild surveyor", "These halls are older than the Guild charter by a thousand years. Someone dug down here before the river ever ran. I think they were running from something too."),
        (BiomeId.Mine, "Foreman Dask", "Lamps lit on every gallery. I never lit them. The timbers are new and I cut none of them. Whoever works the Deep works it better than we did."),
        (BiomeId.Catacombs, "Sister Aveline, Spring Order", "We came to sing the dead to rest. The dead were not resting. They were queuing."),
        (BiomeId.Fungal, "Tam Greaves, Delver", "The river's last breath. You can taste it in the spores, cold and sweet. I stayed a day too long. Tell my sister the garden was beautiful."),
        (BiomeId.Tunnels, "Corporal Hale, the Crown's company", "The bore is as straight as a spear and wide enough for a cart. Nothing natural makes a road like that. Nothing natural needs one."),
        (BiomeId.Slime, "Ines Vell, Delver", "The river came this way and never left. It is down there still, only it is not water now."),
        (BiomeId.Frost, "Brother Cael, Spring Order", "The cold comes up from below. Something down there is drinking the warmth out of the water, as well as the water."),
        (BiomeId.Fossils, "Master Orrin, Guild surveyor", "Leviathans, bigger than any whale in the books, lying in rows. They died drinking. Make of that what you will."),
        (BiomeId.Crystal, "Hob Tarrow, rope-hand", "The silver's cousin. It sings when you walk past, and half the company would not stop listening. We carried them out. Some of them."),
        (BiomeId.LavaTubes, "Foreman Dask", "Bores cut by fire, then left. The walls are warm if you lay a hand on them. The chimneys go up where no one can follow. I left a lamp for whoever can."),
        (BiomeId.Magma, "Captain Brandt, the Crown's company", "The river reached the fire here. What is left is steam and ash. If you are reading this, turn back. I did not."),
        (BiomeId.Abyss, "Sister Aveline, Spring Order", "We did not know the lakes were roofs. There is a sea under them, old as the world, and the river is still running in it."),
    };

    /// <summary>The page for a kind of level (the first not yet read if that one is: a lamp always has something to say).</summary>
    public static (BiomeId biome, string author, string text) PageFor(BiomeId id, Func<BiomeId, bool> found)
    {
        foreach (var pg in Journal) if (pg.biome == id) return pg;
        return Journal[0];
    }

    /// <summary>A page's words broken into lines to fit across the screen.</summary>
    public static string Wrap(string text, int width = 92)
    {
        var lines = new List<string>();
        var cur = "";
        foreach (var w in text.Split(' '))
        {
            if (cur.Length + w.Length + 1 > width && cur.Length > 0) { lines.Add(cur); cur = w; }
            else cur = cur.Length == 0 ? w : cur + " " + w;
        }
        if (cur.Length > 0) lines.Add(cur);
        return string.Join("\n", lines);
    }

    /// <summary>One line over each biome, for its loading screen.</summary>
    public static string Line(BiomeId id) => id switch
    {
        BiomeId.Entrance => "The meadow ends here. The air below smells of ash.",
        BiomeId.Den => "Something has been gathering bones in here for a very long time.",
        BiomeId.Nest => "The webs are new. Whatever spun them is hungry.",
        BiomeId.Ruins => "The hearth-halls of a people who dug too deep, long before the Guild.",
        BiomeId.Roots => "The forest above is drinking here, from what little is left.",
        BiomeId.Mine => "The Guild's galleries. Their lamps still hang where they were left.",
        BiomeId.Catacombs => "The dead of the old kingdom lie in rows. Not all of them lie still.",
        BiomeId.Fungal => "The river's last breath feeds a garden that should not be.",
        BiomeId.Tunnels => "A bore older than the Guild, straight as a spear. It goes down.",
        BiomeId.Slime => "The river did come this way. It never left.",
        BiomeId.Frost => "The cold has no source. It rises from below, from a thing that drinks heat as well as water.",
        BiomeId.Fossils => "Whatever lived here drank deep once, too.",
        BiomeId.Crystal => "The silver's cousin. It sings in the dark.",
        BiomeId.Magma => "The river reached the fire here. What is left is steam and ash.",
        BiomeId.LavaTubes => "Old drain-ways of the fire, wide as streets. They have been quiet for an age.",
        BiomeId.Abyss => "The river's old bed, under the lakes. The sun never saw it, and neither did the Guild.",
        BiomeId.Lair => "This is where the water went.",
        _ => "",
    };

    /// <summary>A line more, for the loading screens: what the camp says to the ones who go down.</summary>
    public static readonly string[] Tips =
    {
        "A hero in a cage is a hero waiting at the camp. Free them.",
        "Every guardian's ember feeds the fire at the mouth of the Dagger.",
        "The dead wake at the fire. The fire remembers a little of what they learned.",
        "The deeper the way, the more the creatures know you are coming.",
        "Not every chest is worth the climb. Some are.",
    };

    /// <summary>Who each hero was, before: shown when they are freed.</summary>
    public static (string title, string backstory, string thanks) Hero(HeroKind h) => h switch
    {
        HeroKind.Swordsman => ("BRAN, SERGEANT OF THE HEARTHMERE WATCH", "He came back from the Crown's company, the only one who did, and he carries the sergeant's longsword and a promise to the ones he left.", "Not dead yet."),
        HeroKind.Warden => ("THE WARDEN, LAST OF THE SPRING ORDER'S SHIELD-SWORN", "She kept the drowned shrine until the water left it, and went down after it with her shield on her arm.", "The shield holds."),
        HeroKind.Vitalist => ("THE VITALIST, A HEALER OF THE SPRING ORDER", "She learned to draw life from whatever bleeds near her, and learned what that costs.", "The vein is open."),
        HeroKind.Elementalist => ("THE ELEMENTALIST, A MAGISTER OF THE DEEPSILVER GUILD", "A student of the six essences of Alima: fire, ice, water, wind, earth and nature. He draws one into his body and bends it by will alone, as heat and cold bend, and went down after the warm wind to find where it came from.", "I knew it was fire."),
        HeroKind.Rogue => ("THE ROGUE, WHO FOUND THE FIRST VEIN OF DEEPSILVER", "She was owed for it. She went back for the rest, and for the ones who followed her.", "You took your time."),
        HeroKind.Aegis => ("THE AEGIS, WARD-KEEPER OF THE SPRING ORDER", "Sent after the Warden with the Order's oldest ward-staff. The ward held for a long time.", "Stand close."),
        HeroKind.ShapeShifter => ("THE SHAPE SHIFTER, OF THE VALLEY'S OLD FOLK", "A wanderer who followed the creatures down to learn their ways, and has learned a little too well.", "I was being polite."),
        _ => ("", "", ""),
    };

    /// <summary>The banner when a caged hero is freed.</summary>
    public static string FreedBanner(HeroKind h, bool fresh)
    {
        var (title, _, thanks) = Hero(h);
        return fresh ? $"{title}  ·  \"{thanks}\"  ·  waiting at the camp" : $"{title}  ·  \"{thanks}\"";
    }
}

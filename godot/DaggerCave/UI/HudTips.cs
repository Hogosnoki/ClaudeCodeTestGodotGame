using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// What the HUD's pieces mean. Each piece says where it was drawn this frame and what it is (<see cref="Tip"/>); then:
///  * hovering the mouse over one for a moment shows its tooltip, as the setting allows (<see cref="GameSettings.HudTips"/>: off, the
///    abilities, potions and relics, or everything on the screen);
///  * the HUD guide (F1, or "HUD guide" in the pause menu) marks every piece at once with a number, and lists what each one is.
/// </summary>
public partial class Hud
{
    private readonly struct TipSpot
    {
        public readonly Rect2 R; public readonly string Title, Body; public readonly bool Basic;
        public TipSpot(Rect2 r, string title, string body, bool basic) { R = r; Title = title; Body = body; Basic = basic; }
    }

    private readonly List<TipSpot> _tips = new();
    private Vector2 _tipMouse;
    private float _tipStill;

    /// <summary>Test aid: the pieces marked last frame.</summary>
    public int TipCount => _tips.Count;
    public IEnumerable<string> TipTitles { get { foreach (var t in _tips) yield return t.Title; } }

    /// <summary>The HUD guide is up (every piece numbered and explained).</summary>
    public static bool Guide;

    /// <summary>
    /// Marks a piece of the HUD drawn this frame. <paramref name="basic"/>: an ability, a potion or a relic (shown on hover unless tooltips
    /// are off); the rest only with "everything" (and always in the guide).
    /// </summary>
    private void Tip(Rect2 r, string title, string body, bool basic = false) => _tips.Add(new TipSpot(r, title, body, basic));

    /// <summary>What an ability's square means, from its label (as the icons are found, see <see cref="Icons.AbilityFor"/>).</summary>
    public static (string title, string body) AbilityTip(string label)
    {
        string w = label.Split(' ')[0].ToUpperInvariant();
        return w switch
        {
            "CHARGE" or "CHARGED" => ("Charged Strike", "Charges your blade: the next swing hits harder and saps what it cuts. Lit while it waits."),
            "HEAVE" => ("Heaving Swing", "A great two-handed blow, on your feet. It shatters frozen creatures."),
            "DASH" => ("Guarded Charge", "A shield-first rush that breaks off the attacks it meets."),
            "BASH" => ("Shield Bash", "Stuns everything in front of you, for a share of the shield (the notch on the shield bar)."),
            "HEAL" => ("Heal", "Spends vital force (the number) to mend you, or the friend you aim at."),
            "RUPTURE" => ("Rupture", "A full reserve of vital force bursts in the creatures round you (the crimson mark on the bar)."),
            "RECALL" => ("Recall", "Calls your thrown daggers back; they cut again on the way. With Ricochet it also flings a poison pin at every marked creature."),
            "TETHER" => ("Tether", "Recall pulls you along the line to your dagger stuck in a creature, striking it as you arrive."),
            "BARRIER" => ("Barrier", "A ward of light on you or the friend you aim at, soaking up blows."),
            "BURDEN" => ("Share a Burden", "Carry part of a friend's hurt: aimed at a friend, you take a share of what they take."),
            "STORM" => ("Blizzard", "Costs alimus (the number). A storm that chills and freezes the creatures in it."),
            "FIRE" => ("Firestorm", "Costs alimus (the number). A storm of fire that sets the creatures in it alight."),
            "SNAP" => ("Snap", "Costs alimus. Shatters every frozen creature in view (the count is how many are frozen now)."),
            "SHIFT" => ("Shift", "Become the nearest creature; press again to drop the form. Your health and strength stay yours."),
            "REPAIR" or "MENDING" => ("Self-Repair", "Spends fuel to mend you, slowly. Healing you receive becomes fuel instead of health."),
            "STEAM" => ("Steam Release", "A blast of steam ahead of you; aimed down, it lifts you."),
            "SHOUT" => ("Battle Shout (support)", "You and the allies near you deal 10% more for 10 s."),
            "TAUNT" => ("Taunt (support)", "For 3 s every creature counts you as the nearest hero."),
            "TAP" => ("Health Tap (support)", "Turns 60% of your vital force into 15% of your health."),
            "STALAG" => ("Stalag-Might (support)", "Rock bursts up round a creature's feet: 30 damage, and it is held for 2 s."),
            "EXPOSE" => ("Expose (support)", "The creature you aim at takes 20% more from everyone, and is struck critically more often, for 8 s."),
            "SHARE" => ("Share Pain (support)", "Pick a friend: you take 15% of every blow they take, until you pick another (4 s between picks)."),
            "PRESSURIZE" => ("Pressurize (support)", "Builds pressure, then you move, jump and hit harder while it burns fuel. Press again to end it."),
            "HOWL" => ("Pack Howl (support)", "Allies near you run 20% and strike 15% faster for 8 s."),
            "DASHSLASH" => ("Dash Slash", "A dash in an orange ghost-glow: the first creature it touches is slashed for one and a half times a strike, with everything a strike brings. It dodges nothing of itself, but you can use it to get away. About fifteen seconds to come back."),
            "DODGE" => ("Dodge", "A quick roll out of harm's way; each ring is one dodge, refilling on its own."),
            "BRACE" or "BRACED" => ("Brace", "Plant your feet: blows push you less and hurt less while braced."),
            "HEX" => ("Hex", "Curses the creature you aim at."),
            "VANISH" or "SMOKE" => ("Vanish", "Slip out of sight: the creatures lose you for a moment."),
            "BUBBLE" or "WARD" => ("Bubble", "A bubble round you or the friend you aim at; its wearer breathes under water."),
            "UPDRAFT" => ("Updraft", "Hold to aim, let go to raise a column of air anyone can float in."),
            _ => (Capital(label), "The creature's own special trick while you wear its form."),
        };
    }

    /// <summary>What an elemental ability does, by its name (<see cref="Elements.SingleName"/>, <see cref="Elements.DualName"/>).</summary>
    public static (string title, string body) ElementTip(string name) => SimpleTip(name, out var simple) ? simple : VersionTip(name);

    /// <summary>What an elemental version does (the elemental kits before the simple ones; the Shape Shifter's).</summary>
    private static (string title, string body) VersionTip(string name) => (name, name switch
    {
        // the Swordsman's greater versions and mixes (overcharged: his mode twice; an attribute on top: the two together)
        "Fault Strike" => "Earth overcharged: a heavy blow of stone (full earth, shatters the frozen), and the floor splits on ahead of it.",
        "Flame Crescent" => "Fire overcharged: a searing blow, and a crescent of flame bursts on from its tip, setting what it catches burning.",
        "Frost Lance" => "Frost overcharged: the cut, and a lance of ice thrown on along it that freezes what it meets.",
        "Magma Strike" => "Fire and earth: a blow of molten rock, fire and earth both, leaving a little pool of magma where it lands.",
        "Permafrost Strike" => "Earth and frost: a blow of frozen stone, earth and frost both (it shatters the frozen), and spears of frozen rock burst up out of the floor before you.",
        "Steam Strike" => "Fire and frost: what it strikes takes 50 of whichever of fire and frost it holds more of, and a cloud of steam bursts where it strikes (friends in it are hidden).",
        "Meteor Fall" => "Earth overcharged: the Earthfall, but you come down like a falling star: a far greater shockwave.",
        "Blast Lunge" => "Fire overcharged: a greater blast and a longer leap, fire burning where you pass.",
        "Stalagmite Vault" => "Frost overcharged, from the ground: you leap back, and great stalagmites of ice burst up out of the ground you pass over, striking and filling with frost what they meet. They melt a moment later.",
        "Blazing Charge" => "Earth and fire, the dash (on the ground or in the air): you charge forward in a sphere of fire, sword up before you, crashing into every creature in your way: a heavy blow and 100 fire each. Fire trails behind you.",
        "Comet" => "Earth and frost (on the ground or in the air): you leap forward, all blue-white, and where you come down a great burst of frost strikes everything about (a heavy blow, 100 frost) as crystals of ice burst out of the floor and melt away.",
        "Steam Vault" => "Frost and fire: the spring back, leaving a cloud of scalding steam where you stood: friends in it are hidden, creatures in it take fire and frost.",
        "Stoneheart Hilt" => "Earth overcharged: every blow saps twice as long, for half again as long.",
        "Blazing Hilt" => "Fire overcharged: every blow carries twice the fire, for half again as long.",
        "Glacial Hilt" => "Frost overcharged: every blow carries twice the frost, for half again as long.",
        "Magma Hilt" => "Earth and fire: every blow saps and burns for a while, and molten rock drips from the blade where it strikes (a little pool, now and then).",
        "Permafrost Hilt" => "Earth and frost: every blow saps and chills for a while, and the frozen burst under it.",
        "Steam Hilt" => "Fire and frost: for a while every blow adds twice the hilt's touch of whichever of fire and frost the creature holds more of.",
        // the Elementalist's new ones
        "Frostfire Bolts" => "Fire and frost: a bolt of fire and a bolt of frost loosed together.",
        "Sky Lift" => "Wind overcharged: a great push of air carries you high, and every friend near gets a wind at their back.",
        "Steam Veil" => "Fire and frost: a burst of steam about you: friends in the cloud are hidden, creatures caught in it burn and chill.",
        "Bonfire" => "Fire overcharged: Kindling for half again as long, and a ring of fire bursts out about you.",
        "Ice Armor" => "Frost overcharged: Frost Armor, and a shell of ice that soaks blows besides.",
        "Gale Draft" => "Wind overcharged: Narrow Draft for twice as long, and a gust that throws the creatures about you back.",
        "Thermal Armor" => "Fire and frost: Frost Armor for you, Kindling for you and your friends near.",
        "Wildfire" => "Fire and wind: Kindling and a wind at the back for you and your friends near.",
        "Frostwind" => "Frost and wind: Frost Armor for you, a wind at the back for you and your friends near.",
        // the Warden: the attack
        "Shortsword" => "The shortsword. In earth mode it is Stonecutter, in frost Rime Edge, in wind Gale Edge; overcharged, the mode's greater cut.",
        "Stonecutter" => "Earth's cut: slow (half the pace) but twice as hard, 50 earth (a full meter stuns).",
        "Rime Edge" => "Frost's cut: a steady pace, a fifth harder, 50 frost (full: chilled; full again: frozen solid).",
        "Gale Edge" => "Wind's cut: quick and light (0.6x), reaching a little farther and throwing what it strikes far back.",
        "Boulder Strike" => "Earth and earth: a crushing blow, more than twice as hard: stunned, tossed up, and the frozen shattered.",
        "Icicle Lance" => "Frost and frost: the cut, and a lance of ice thrown on along your aim that freezes what it meets.",
        "Cyclone Slash" => "Wind and wind: a whirling cut all round you, reaching far, throwing everything it strikes away.",
        "Frozen Earth" => "Earth and frost: a hard blow of frozen stone, 60 earth and 60 frost; it shatters the frozen.",
        "Sandblast" => "Earth and wind: the cut throws a cone of grit ahead: everything in it is nicked and its attacks slowed.",
        "Frost Gust" => "Frost and wind: the cut looses a freezing wind ahead: everything in it is filled with frost and driven back.",
        // the Warden: the block (in a mode the shield is that element's; overcharged, the mode's barrier; with an attribute, the pair's guard)
        "Block" => "Your shield, raised. Its 50 points of strength are one pool whatever the mode, worn by what it absorbs; in earth mode it absorbs 90% of a blow, in frost 45%, in wind 20% (a perfect block stops it all). Broken, it can't be raised for 4 s, but it mends from the moment it breaks. Overcharged, raising it calls up the mode's barrier; with an attribute, the pair's guard.",
        "Stone Shield" => "Earth's shield: a slab of brown-grey stone, solid. It absorbs 90% of every blow it meets, from the one pool of 50 shared by all your shields.",
        "Frost Shield" => "Frost's shield: a pane of ice. It absorbs 45% of every blow it meets, from the one pool of 50 shared by all your shields.",
        "Wind Shield" => "Wind's shield: a faint veil of light grey air. It absorbs 20% of every blow it meets, from the one pool of 50 shared by all your shields.",
        "Earth Shield" => "Earth overcharged: the ground in front heaves up and over you and stands for as long as you hold the block: nothing reaches you, nothing moves you, and creatures can't pass it; you kneel behind it. Let go and it sinks.",
        "Ice Barrier" => "Frost overcharged: a shell of ice about you (it stays when you lower the shield) absorbs 80% of every blow; once it has absorbed 15 it shatters: 90 frost and a blow to every creature about you.",
        "Tenuous Barrier" => "Wind overcharged: a faint skin of wind about you absorbs 20% of every blow, and dissipates once it has absorbed 10.",
        "Permafrost Guard" => "Earth and frost: the ground about you freezes (creatures on it chilled, friends quicker); strikers are stunned and filled with frost, and nothing gets through.",
        "Dust Wall" => "Earth and wind: a cloud of dust in front hides your friends and slows the creatures in it; strikers are stunned and thrown back.",
        "Hoarfrost Gale" => "Frost and wind: strikers are filled with frost and thrown back, and shots go back.",
        // the Warden: the dash (every elemental charge runs through creatures instead of stopping)
        "Guarded Charge" => "A charge behind the shield. In earth mode it is a Rockslide Charge, in frost Frozen Tundra, in wind a Gale Charge; overcharged, the greater charge.",
        "Rockslide Charge" => "Earth's charge: it ploughs through everything in its way and ends in a shockwave of earth that stuns.",
        "Frozen Tundra" => "Frost's charge: it fills what it passes with frost, and the ground it crossed freezes over (creatures on it chilled, friends quicker).",
        "Gale Charge" => "Wind's charge: farther and faster, throwing creatures aside; friends it passes get a wind at their back.",
        "Avalanche" => "Earth and earth: the rockslide, with a great quake at its end and a fissure running on ahead.",
        "Glacier Charge" => "Frost and frost: everything it passes is frozen solid, and the ground it crossed freezes over.",
        "Tempest Charge" => "Wind and wind: a long charge that carries every creature it meets along in its wake, then flings them.",
        "Frostquake" => "Earth and frost: it ploughs through, ends in a quake, and ice bursts up where it stops.",
        "Duststorm" => "Earth and wind: a long charge leaving clouds of dust behind (friends in them hidden, creatures slowed), and a quake at its end.",
        "Blizzard Charge" => "Frost and wind: farther and faster, filling what it passes with frost and throwing it aside; the ground it crossed freezes over.",
        // the Vitalist: the drain (attack)
        "Drain" => "The drain: life torn out of the creature you aim at, coming home as vital force. In nature mode Thorn Drain, in wind Wisp Drain, in water Tide Drain.",
        "Thorn Drain" => "Nature's drain: thorns burst out of it (40 nature; full, it's entangled), and a quarter more vital force comes home.",
        "Wisp Drain" => "Wind's drain: reaches 40% farther and casts a quarter quicker, a little lighter.",
        "Tide Drain" => "Water's drain: 40 water (full, it's soaked: fire out, frost twice as fast), and you're mended by part of what it tears out.",
        "Bramble Lash" => "Nature overcharged: a full meter of nature, and brambles lash out to up to three creatures beside it, draining them too.",
        "Whirling Siphon" => "Wind overcharged: the wind takes more at once: up to three more creatures in reach are drained as well.",
        "Riptide" => "Water overcharged: a full meter of water (soaked), and the current hauls it toward you.",
        "Pollen Drain" => "Wind and nature: pollen carried off it on the wind fills everything about it with nature.",
        "Life Spring" => "Nature and water: what it tears out wells up as a spring, mending every friend near you by part of it.",
        "Rain Siphon" => "Wind and water: rain carried off it soaks everything about it (water).",
        // the Vitalist: the hex (dodge)
        "Hex" => "The hex: creatures about you are slowed and take more damage. In nature mode Overgrowth, in wind Withering Gale, in water Drowning Hex.",
        "Overgrowth" => "Nature's hex: vines erupt about you, filling every creature in it with nature (50).",
        "Withering Gale" => "Wind's hex: a fifth wider, and everything in it is thrown out away from you.",
        "Drowning Hex" => "Water's hex: everything in it is filled with water (60).",
        "Strangling Grove" => "Nature overcharged: a wider grove of vines; every creature in it is entangled (a full meter).",
        "Eye of the Tempest" => "Wind overcharged: far wider, and everything in it is hauled in toward you, where your friends can get at it.",
        "Maelstrom" => "Water overcharged: everything in it is soaked, and a pool of healing water wells up at your feet.",
        "Spore Storm" => "Wind and nature: much wider; spores fill everything in it with nature and the wind throws it out.",
        "Mire Bloom" => "Nature and water: the ground turns to mire: everything in it is slowed in all it does, and filled with nature.",
        "Squall" => "Wind and water: a squall soaks everything in it with water and throws it out.",
        // the Vitalist: the heal (ability)
        "Heal" => "The heal: vital force spent to mend everyone near who's hurt. In nature mode Regrowth, in wind Breath of Life, in water Cleansing Wave. In a mode it goes out even with no one hurt.",
        "Regrowth" => "Nature's heal: half of it now, and three quarters more grows back over 5 s.",
        "Breath of Life" => "Wind's heal: reaches 60% farther, and everyone near gets a wind at their back.",
        "Cleansing Wave" => "Water's heal: washes every affliction (poison, fire, frost, webs, stuns) off those it reaches.",
        "Bloom of Life" => "Nature overcharged: half again as strong, and a pool of healing blooms at your feet.",
        "Second Wind" => "Wind overcharged: reaches every friend wherever they are, and gives them all a wind at their back.",
        "Wellspring" => "Water overcharged: a fifth stronger, a barrier of water on everyone near, and every affliction washed off.",
        "Seeds on the Wind" => "Wind and nature: reaches 60% farther, and more mending grows on everyone near over 5 s.",
        "Rejuvenation" => "Nature and water: part now and all of it again over time, and every affliction washed off.",
        "Mist of Mending" => "Wind and water: the heal, and a mist about you that hides your friends in it.",
        // the Shape Shifter (elemental): the staff out of a form, and each creature's greater special (overcharged)
        "Bramble Staff" => "Your staff in nature mode: 30 nature to what it strikes.",
        "Gale Staff" => "Your staff in wind mode: reaching a quarter farther.",
        "Tide Staff" => "Your staff in water mode: 30 water to what it strikes.",
        "Recall" => "Out of a form: the form you remember for your mode's element.",
        "Warlord's Smash" => "The goblin's Club Smash, greater: for a moment its blows land half again as hard and entangle (a full meter of nature).",
        "Bone Storm" => "The skeleton's Bone Spin, greater: harder blows that entangle.",
        "Plague Gnaw" => "The rat's Gnaw, greater: every bite harder, entangling what it gnaws.",
        "Widow's Bite" => "The spider's Envenomate, greater: a harder bite that entangles.",
        "Deathstalker Spray" => "The scorpion's Venom Spray, greater: harder, and it entangles all it reaches.",
        "Grizzly Rampage" => "The bear's Maul Charge, greater: harder blows that entangle all it mauls.",
        "Earthshatter" => "The golem's Quake, greater: harder, entangling all about it.",
        "Blight Bloom" => "The sporeling's Spore Cloud, greater: harder, entangling all about it.",
        "Vampire Latch" => "The bat's Latch, greater: harder bites, and each throws its prey about on the wind.",
        "Swarm Sting" => "The hornet's Dive Sting, greater: harder, flinging what it stings.",
        "Monsoon Pounce" => "The frog's Pounce, greater: harder, soaking everything it lands on (a full meter of water).",
        "Crushing Tide" => "The crab's Vice Grip, greater: harder, soaking what it grips.",
        "Tidal Splash" => "The fish's Splash, greater: harder, soaking everything about it.",
        // the Rogue: the strike (attack)
        "Jab" => "Your daggers' jab. In dark mode Shadow Strike, in wind Gale Jab, in nature Venom Jab.",
        "Shadow Strike" => "Dark's jab: a fifth harder, 40 dark (full: blinded: it loses you, and takes more and more critical blows).",
        "Gale Jab" => "Wind's jab: far quicker, a little lighter, reaching farther, and it flicks what it strikes back.",
        "Venom Jab" => "Nature's jab: 40 nature (full: entangled), and a poison.",
        "Nightfall" => "Dark overcharged: a sure critical, 60% harder, and a full meter of dark (blinded).",
        "Flurry" => "Wind overcharged: a blur of jabs, sweeping half round you, far quicker, flicking all it strikes back.",
        "Deathcap" => "Nature overcharged: a full meter of nature (entangled) and a double poison.",
        "Thornwind" => "Wind and nature: quick, long reach, nature, and a flick back.",
        "Shade Step" => "Wind and dark: dark, and you step through the creature you strike to its far side, at its back.",
        "Nightshade" => "Nature and dark: dark and nature together, and a poison.",
        // the Rogue: the evade (dodge)
        "Vanish" => "Your vanish. In dark mode Shadowmeld, in wind Gale Step, in nature Spore Cloud.",
        "Shadowmeld" => "Dark's evade: the shadows hold you half again as long.",
        "Gale Step" => "Wind's evade: vanish with a dash ahead.",
        "Spore Cloud" => "Nature's evade: spores burst about you, filling creatures near with nature.",
        "Umbral Veil" => "Dark overcharged: twice as long in the shadows, and every creature near is blinded.",
        "Wind Walk" => "Wind overcharged: the dash, and a wind at your back for 4 s.",
        "Overgrowth Veil" => "Nature overcharged: creatures near are entangled (a full meter of nature).",
        "Leaf Storm" => "Wind and nature: a storm of leaves throws creatures near out, filling them with nature.",
        "Shadow Dash" => "Wind and dark: a long dash, leaving a cloud of smoke where you stood.",
        "Nightbloom" => "Nature and dark: a cloud of smoke about you, dark and nature to creatures near.",
        // the Rogue: the throw (ability)
        "Dagger Throw" => "A dagger thrown. In dark mode Shadow Knife, in wind Gale Knife, in nature Barbed Knife.",
        "Shadow Knife" => "Dark's throw: 50 dark to what it strikes.",
        "Gale Knife" => "Wind's throw: half again as fast, knocking what it strikes back.",
        "Barbed Knife" => "Nature's throw: 50 nature and a poison.",
        "Umbral Knife" => "Dark overcharged: a sure critical and a full meter of dark (blinded).",
        "Fan of Knives" => "Wind overcharged: both daggers at once, fanned, fast, knocking back.",
        "Thorn Knife" => "Nature overcharged: a full meter of nature (entangled) and a bleed.",
        "Pollen Knife" => "Wind and nature: pollen bursts off it, nature to everything about the one it strikes.",
        "Shadow Tether" => "Wind and dark: dark, and you're drawn along to the dagger, striking again as you land.",
        "Nightshade Knife" => "Nature and dark: dark, nature and a poison.",
        // the Aegis: the bubble shot (attack)
        "Bubble Shot" => "The bubble shot (the ward bolt): it homes and bursts, hurting and weakening what's about it. In wind mode Gale Bubble, in earth Stone Bubble, in frost Rime Bubble.",
        "Gale Bubble" => "Wind's shot: half again as fast, cast more often, and its burst throws creatures back.",
        "Stone Bubble" => "Earth's shot: slower and heavier: half again as hard, a wider burst, 40 earth to all in it.",
        "Rime Bubble" => "Frost's shot: 40 frost to everything in the burst.",
        "Boulder Bubble" => "Earth overcharged: more than twice as hard, a wide burst; the one it hits is stunned (a full meter of earth).",
        "Frost Orb" => "Frost overcharged: a full meter of frost to everything in the burst.",
        "Bubble Volley" => "Wind overcharged: three fast shots, fanned.",
        "Hail Bubble" => "Earth and frost: half again as hard, earth and frost to all in the burst; the frozen shatter.",
        "Sand Bubble" => "Earth and wind: the burst leaves a cloud of dust that hides your friends and slows what's caught in it.",
        "Snow Bubble" => "Frost and wind: a wider burst of snow: frost, and everything in it thrown back.",
        // the Aegis: the bubble field (dodge)
        "Bubble Field" => "The bubble field: you and your friends near are each wrapped in a bubble. In wind mode Air Dome, in earth Stone Dome, in frost Ice Dome.",
        "Air Dome" => "Wind's field: creatures near are thrown out as it goes up.",
        "Stone Dome" => "Earth's field: the bubbles are half again as strong.",
        "Ice Dome" => "Frost's field: creatures near are filled with frost (40).",
        "Fortress" => "Earth overcharged: bubbles more than twice as strong, lasting half again as long.",
        "Glacier Dome" => "Frost overcharged: a full meter of frost to every creature near.",
        "Gale Dome" => "Wind overcharged: reaches friends far farther off, and throws creatures out.",
        "Crystal Dome" => "Earth and frost: stronger bubbles, and frost armour on everyone it wraps.",
        "Dust Dome" => "Earth and wind: a cloud of dust about you hides your friends; creatures near are slowed.",
        "Blizzard Dome" => "Frost and wind: frost to every creature near, and the wind throws them out.",
        // the Aegis: the barrier (ability)
        "Barrier" => "The barrier: a ward on you or the friend you aim at. In wind mode Wind Ward, in earth Stone Skin, in frost Frost Ward.",
        "Wind Ward" => "Wind's barrier: and a wind at their back.",
        "Stone Skin" => "Earth's barrier: half again as strong.",
        "Frost Ward" => "Frost's barrier: and frost armour while it lasts (melee strikers are filled with frost).",
        "Bulwark Ward" => "Earth overcharged: two and a half times as strong.",
        "Glacial Ward" => "Frost overcharged: a little stronger, and the next creature to strike them in melee is frozen full.",
        "Twin Gale" => "Wind overcharged: on your friend and you both, each with a wind at their back.",
        "Permafrost Ward" => "Earth and frost: much stronger, and frost armour.",
        "Sandstorm Ward" => "Earth and wind: a cloud of dust about the one it wraps hides them.",
        "Winter Ward" => "Frost and wind: frost armour and a wind at their back.",
        // the Swordsman
        "Empower Hilt" => "For 8 s every blow lands 20% harder; in a mode, every blow carries its element instead: earth saps a creature's attacks, frost and fire fill its build-up.",
        "Earth Hilt" or "Fire Hilt" or "Frost Hilt" => "For 8 s every blow carries the element: earth saps a creature's attacks, frost and fire fill its build-up.",
        "Swing" => "The sword combo. In earth mode the Heaving Swing comes every few seconds; in fire mode every swing is a Searing Strike, in frost Razor Ice.",
        "Heaving Swing" => "Earth's swing: rooted, twice as hard, 60 earth (a full meter stuns).",
        "Searing Strike" => "Fire's swing: a quick blow, 40 fire (full: alight; full again: it combusts).",
        "Razor Ice" => "Frost's swing: a quick blow, 50 frost (full: chilled; full again: frozen solid).",
        "Dash Slash" => "The dodge: a slashing dash. In the elemental kit there is no Dash Slash: the dodge is your mode's leap (fire an Explosive Lunge, frost a Glacial Vault, earth Earthfall), and nothing while it rests.",
        "Explosive Lunge" => "Fire's dodge, from the ground: the ground bursts behind you (fire to everything beside you) and throws you forward and up; on the way you swing at every creature you come to.",
        "Glacial Vault" => "Frost's dodge, from the ground: you spring back, leaving a likeness of yourself in ice where you stood (only one at a time). Creatures go for it; it takes a couple of blows before it shatters, filling those beside it with frost.",
        "Earthfall" => "Earth's dodge: a leap (in the air, none), and you come down like a rock, in a shockwave of earth (the harder the fall, the harder it hits).",
        "Hard Swing" => "A slow, rooted heavy blow: in earth mode ThunderCrush, in fire Pyroclasty, in frost the Ice Axe; overcharged, the mode's great cleave.",
        "ThunderCrush" => "Earth's Hard Swing: slow and crushing, a full earth build-up (a stun), and it shatters the frozen.",
        "Pyroclasty" => "Fire's Hard Swing: a full fire build-up (alight; alight already, it combusts).",
        "Ice Axe" => "Frost's Hard Swing: the sword goes up over your head, an axe head of ice grows out of it, and it comes down in front of you: a full frost build-up (chilled; chilled already, frozen solid).",
        "Earthsplitter" => "Earth and earth: a long wind-up as the ground trembles and rock lifts about you, then the blade comes down and a fissure tears along the floor, tossing up and stunning everything it passes under. Rubble in its way comes down.",
        "Inferno Cleave" => "Fire and fire: a wall of fire stands where the blade lands. Creatures in it burn; shots flying into it burn up.",
        "Glacier Cleaver" => "Frost and frost: held up behind you, the blade slowly grows a vast, ornate axe head of ice, then comes down in front of you. Its blow freezes what it cuts, and the head shatters it as it lands.",
        "Frost Heave" => "Earth and frost: the ground bursts up in ice under the nearest creature: a great blow of frost, and it's thrown into the air.",
        "Breaking Quake" => "Earth and fire: you gather, then drive the blade into the floor; great cracks full of magma tear out either way, and everything over them takes 100 fire and is thrown up. They close a moment later.",
        "Thermal Shock" => "Fire and frost: what it strikes takes 100 of whichever of fire and frost it holds more of; then frozen solid it shatters, burning it bursts.",
        // the Elementalist
        "Pop" => "With nothing readied: a small burst right on the creature nearest your aim, a short way off. No bolt; weaker than the elements. Each charge of fire, frost or wind readied makes the next one a Firebolt, Frostbolt or Gust.",
        "Meteorite" => "Fire and fire: a meteorite falls on the thickest knot of creatures, smashing every platform it passes through (they grow back), and bursts in fire.",
        "Cryometeor" => "Frost and frost: a rock of ice falls on the thickest knot of creatures; everything about it is frozen solid, and the ground freezes over.",
        "Strong Gust" => "Wind and wind: a little whirlwind races along the floor from the staff, shoving every creature it meets back and nicking them as it goes.",
        "Fire Barrage" or "Frost Barrage" => "Fire (or frost) and wind: a volley of seven bolts loosed one after another, fanned along the aim.",
        "Lift" => "The dodge: a push of air straight up, on the ground or in the air. In a mode the dodge is the mode's movement whenever it is ready (fire Fire Walk, frost Tundra, wind the Updraft), Lift meanwhile; overcharged, Flamebreath or Frost Tower.",
        "Fire Walk" => "Fire's dodge: for 4 s you move 30% faster and fire burns where you walk.",
        "Tundra" => "Frost's dodge: the ground about you freezes over: heroes on it go faster, creatures on it are chilled and filled with frost.",
        "Updraft" => "Wind's dodge: a column of rising air along your aim (straight up, aimed nowhere) to ride up. It carries heroes along it.",
        "Flamebreath" => "Fire and fire: fire pours from the staff in a cone, burning creatures and the shots in it. Aimed down, it holds you up.",
        "Frost Tower" => "Frost and frost: you leap, and a pillar of ice rises under you to stand and cast from. Creatures near go for it instead of you (it has a little health, more as you grow); your friends walk through it.",
        "Fire Barrier" => "Fire and wind: a ring of fire about you, going where you go: creatures that touch it burn and are thrown back; shots burn up.",
        "Frost Barrier" => "Frost and wind: frost armour: the next creature to strike you in melee is filled with frost (a full meter).",
        "Force Blast" => "A wide, weak burst of force at a spot ahead, throwing creatures out of it. In fire mode it is Burst, in frost Snap, in wind a Cyclone.",
        "Burst" => "Fire's spell: an explosion at a spot ahead, 60 fire; and every creature in view already burning, wherever it is, takes a full build-up (and combusts).",
        "Snap" => "Frost's spell: every creature in view frozen solid shatters, wherever it is; and the cold closes on a spot ahead, filling what's there with frost.",
        "Cyclone" => "Wind's spell: a storm at a spot ahead that drags light creatures into its middle and swallows the shots that fly into it.",
        "Rain of Fire" => "Fire and fire: fire rains over a spot ahead for 3 s, each fireball bursting where it lands.",
        "Hailstorm" => "Frost and frost: hail pours over a spot ahead for 3 s, filling creatures with frost and beating flying ones down.",
        "Fire Storm" => "Fire and wind: a storm of fire, fanned by the wind: it burns, drags light creatures in and swallows shots.",
        "Blizzard" => "Frost and wind: a storm of snow, fanned by the wind: it chills, drags light creatures in and swallows shots.",
        "Tornado" => "Wind and wind: a tornado runs along the floor, carrying light creatures along inside it and swallowing their shots, then flings them away.",
        "Frostfire Lance" => "Fire and frost: one beam through every creature in a line, fire and frost together; one already burning or chilled takes far more.",
        "Ward" => "Aid: a barrier for you and your friends near. In fire mode it is Kindling, in frost Frost Armor, in wind Narrow Draft.",
        "Kindling" => "Fire's Aid: for 8 s you and your friends near add a little fire to every blow.",
        "Frost Armor" => "Frost's Aid: for 8 s a creature that strikes you in melee is filled with frost.",
        "Narrow Draft" => "Wind's Aid: for 4 s you and your friends near move 20% faster and jump higher.",
        _ => "",
    });

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();

    /// <summary>Every frame: the hovered piece's tooltip, or the guide.</summary>
    private void DrawTips(Font font, Vector2 vs)
    {
        if (Guide) { DrawGuide(font, vs); return; }
        if (GameSettings.HudTips == 0) return;
        var m = GetLocalMousePosition();
        // (only for a mouse resting there: a tooltip doesn't jump up under a cursor passing through)
        if (m.DistanceTo(_tipMouse) > 3f) { _tipMouse = m; _tipStill = 0; return; }
        _tipStill += (float)GetProcessDeltaTime();
        if (_tipStill < 0.35f || (Time.GetTicksMsec() - GameSettings.MouseSeenMs) > 15000) return;
        for (int k = _tips.Count - 1; k >= 0; k--)
        {
            var t = _tips[k];
            if (!t.Basic && GameSettings.HudTips < 2) continue;
            if (!t.R.HasPoint(m)) continue;
            TipBox(font, vs, m + new Vector2(16, 18), t.Title, t.Body, Mathf.Clamp((_tipStill - 0.35f) * 6f, 0f, 1f));
            return;
        }
    }

    private void TipBox(Font font, Vector2 vs, Vector2 at, string title, string body, float a)
    {
        const float w = 280;
        var lines = WrapTip(font, body, w - 16, 12);
        float h = 28 + lines.Count * 15;
        if (at.X + w > vs.X - 8) at.X = vs.X - 8 - w;
        if (at.Y + h > vs.Y - 8) at.Y = vs.Y - 8 - h;
        var r = new Rect2(at, new Vector2(w, h));
        UiArt.Shadow(this, r, 10f, 0.4f * a);
        UiArt.FillRounded(this, r, 10f, UiArt.Surface.Panel, new Color(1, 1, 1, 0.96f * a), 0.6f);
        UiArt.Outline(this, r, 10f, new Color(0.85f, 0.7f, 0.35f, 0.8f * a), 1.5f);
        DrawString(font, at + new Vector2(8, 18), title, HorizontalAlignment.Left, -1, 13, new Color(1f, 0.85f, 0.45f, a));
        for (int k = 0; k < lines.Count; k++)
            DrawString(font, at + new Vector2(8, 35 + k * 15), lines[k], HorizontalAlignment.Left, -1, 12, new Color(0.9f, 0.9f, 0.92f, a));
    }

    private static List<string> WrapTip(Font font, string text, float width, int size)
    {
        var lines = new List<string>();
        string cur = "";
        foreach (var word in text.Split(' '))
        {
            string next = cur == "" ? word : cur + " " + word;
            if (cur != "" && font.GetStringSize(next, HorizontalAlignment.Left, -1, size).X > width) { lines.Add(cur); cur = word; }
            else cur = next;
        }
        if (cur != "") lines.Add(cur);
        return lines;
    }

    /// <summary>The HUD guide: every piece outlined and numbered, and a list of what each number is.</summary>
    private void DrawGuide(Font font, Vector2 vs)
    {
        DrawRect(new Rect2(Vector2.Zero, vs), new Color(0, 0, 0, 0.35f));
        // (one entry per kind of piece: five dodge rings are one "Dodge")
        var seen = new HashSet<string>();
        var list = new List<TipSpot>();
        foreach (var t in _tips) if (seen.Add(t.Title)) list.Add(t);
        var gold = new Color(1f, 0.82f, 0.4f);
        for (int k = 0; k < list.Count; k++)
        {
            var r = list[k].R;
            UiArt.Outline(this, r.Grow(2f), 6f, new Color(gold, 0.85f), 1.5f);
            var badge = r.Position + new Vector2(-4, -4);
            DrawCircle(badge, 8, new Color(0.08f, 0.07f, 0.09f, 0.95f));
            DrawArc(badge, 8, 0, Mathf.Tau, 16, gold, 1.2f);
            DrawString(font, badge + new Vector2(-8, 4), (k + 1).ToString(), HorizontalAlignment.Center, 16, 10, gold);
        }
        // the legend, in the middle of the screen
        const float w = 560;
        var rows = new List<(int n, string title, List<string> lines)>();
        float h = 50;
        for (int k = 0; k < list.Count; k++)
        {
            var lines = WrapTip(font, list[k].Body, w - 190, 11);
            rows.Add((k + 1, list[k].Title, lines));
            h += Math.Max(1, lines.Count) * 14 + 4;
        }
        var at = new Vector2(vs.X / 2 - w / 2, Math.Max(60, vs.Y / 2 - h / 2));
        UiArt.Shadow(this, new Rect2(at, new Vector2(w, h)), 16f, 0.5f);
        UiArt.FillRounded(this, new Rect2(at, new Vector2(w, h)), 16f, UiArt.Surface.Panel, Colors.White, 0.8f);
        UiArt.Frame(this, new Rect2(at, new Vector2(w, h)), 16f, UiArt.Rarity.Rare, (float)Time.GetTicksMsec() / 1000f);
        DrawString(font, at + new Vector2(14, 24), "THE HUD", HorizontalAlignment.Left, -1, 15, gold);
        DrawString(font, at + new Vector2(14, 24), "F1 or any button closes it", HorizontalAlignment.Right, w - 28, 10, new Color(1, 1, 1, 0.5f));
        float y = at.Y + 46;
        foreach (var (n, title, lines) in rows)
        {
            DrawString(font, new Vector2(at.X + 14, y), $"{n}.", HorizontalAlignment.Left, -1, 11, gold);
            DrawString(font, new Vector2(at.X + 36, y), title, HorizontalAlignment.Left, 150, 11, new Color(1f, 0.92f, 0.75f));
            for (int k = 0; k < lines.Count; k++)
                DrawString(font, new Vector2(at.X + 186, y + k * 14), lines[k], HorizontalAlignment.Left, -1, 11, new Color(0.88f, 0.88f, 0.9f));
            y += Math.Max(1, lines.Count) * 14 + 4;
        }
    }

    public override void _Input(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode == Key.F1 && G.Player != null) { Guide = !Guide; GetViewport().SetInputAsHandled(); return; }
        // (any other press closes the guide)
        if (Guide && e.IsPressed() && !e.IsEcho() && e is InputEventKey or InputEventJoypadButton or InputEventMouseButton)
        {
            Guide = false;
            GetViewport().SetInputAsHandled();
        }
    }
}

# Ability archive

The elemental heroes now use the **simple kits** (`Elements.Simple` in `Core/Elements.cs`; README, "Simple kits"): three abilities each
(a primary attack, a defensive, a special), three elements, and an element changes only what an ability does to what it reaches,
never how the ability works. Everything the simple kits replaced is kept here, and kept in the code, for reuse.

**Nothing below was deleted.** It sits behind the switch:

* `Elements.Simple = false` brings the whole mode system back as it was (the mode, the slot on top for an overcharge or an attribute,
  every version below, the cooldown rules, the HUD panels, the tips, and its tests: `--scenario=elements` runs the old ones when the
  switch is off).
* To reuse one ability, start from the functions named under its button. Each version is a `case` on its name in those functions, and
  its numbers are in `Core/Tuning.cs` (`Tune.ElementalSword`, `Tune.ElementalStaff`, `Tune.ElementalWarden`, `Tune.ElementalVital`,
  `Tune.ElementalAegis`, `Tune.ElementalRogue`). Its tooltip is in `UI/HudTips.cs` (`VersionTip`) and its name in `Elements.SingleName`
  and `Elements.DualName`.
* The classic kits (Settings, "Classic kits (no elements)") are unchanged. They still use the Rupture, the Recall button, Shared
  Burden and Humbling Light, the Shield Bash, the Charged Strike, the heave, the Blizzard and the Snap.

The Shape Shifter (whose element picks the creature she becomes) and the Automaton (no elements) kept their kits. Nothing of theirs is
archived.

## Retired systems

| System | What it did | Where |
|---|---|---|
| The slot on top of the mode | The mode's own element pressed again was an **overcharge**: the next ability with a greater version used it, and it came back after `OverchargeRecharge` s. Another element pressed went on top as the **attribute**: every blow and spell added `AttributeBuild` of it (wind: a shove of `AttributeShove`). Pressing the attribute again switched the mode to it, at most once every `ModeCooldown` s. | `TryInfuse` (the branch after the simple kits' early return), `SpendPair`, `SpendFor`, `DualNameFor`, `AttributeHit` (Player/Player.Elemental.cs); `Tune.Elements` |
| Versions that change the mechanics | Each element made its own version of every button: its own damage, reach, speed, shape and cooldown (`ElemRest`, `Tune.Elements.VersionsRest`). | The tables below |
| Mixes and greater versions | Two elements made a third ability on each button (an overcharge: the mode's greater version; an attribute: the two mixed). | The tables below ("overcharge", "mix") |
| Elemental damage types | Fire and frost blows dealt fire and frost damage, so creatures that resist or are weak to them took less or more. Simple kits deal plain blows: the numbers never change with the element. | `OnSwingHit` (Player/Player.Blade.cs), `ElementBoltStruck`, `TryFixed` (Player/Player.Elemental.cs) |
| The Warden's mode shield | One shield pool, absorbing its mode's share (stone most, ice half, air little), mending while broken. The simple Warden has the classic shield (the slab still takes the element's look). | `ModeShieldShare` (Player/Player.WardenElements.cs); `ElementShield` (Player/Player.Warden.cs, Player/Upgrades.cs); `Tune.ElementalWarden.ShieldHp`, `ShieldRegen`, `ShieldCost`, `EarthShare`, `FrostShare`, `WindShare` |
| The stone club | The Warden in earth swung a club with no combo, every blow at its own slow pace. | `OnSwingHit` (`club`, Player/Player.Blade.cs) |
| The Firebolt's burst | A fire bolt burst in a small blast where it struck. | `FireBurst` (Player/Player.Elementalist.cs) |

## Retired buttons

The simple kits use three buttons: attack, dodge and ability. The second ability button does nothing for them. These were on it, or
on a button whose role changed:

| Hero | Ability | Was on | Now |
|---|---|---|---|
| Swordsman | Hard Swing (and its versions) | Second ability | The special, Cleave, on the ability button (the plain Hard Swing) |
| Swordsman | Empower Hilt (and its versions) | Ability | Retired |
| Swordsman | The element leaps (Earthfall, Explosive Lunge, Glacial Vault and the rest) | Dodge | The defensive, Roll (a plain dodge roll) |
| Warden | The elemental guards and barriers (Earth Shield, Ice Barrier, Tenuous Barrier, the guards) | The shield, overcharged or with an attribute | The defensive, Shield (the classic shield) |
| Elementalist | The fixed-range spell (Force Blast, Burst, Snap, Cyclone and the rest) | Second ability | The special, Burst, on the ability button |
| Elementalist | Lift, Fire Walk, Tundra, Updraft and their pairs | Dodge | Retired: the dodge button is now the Ward |
| Elementalist | Aid buffs (Kindling, Frost Armor, Narrow Draft and their pairs) | Ability | Retired: the plain Ward is the defensive |
| Vitalist | Rupture | Second ability | Retired from the simple kit |
| Vitalist | The hex | Dodge | The special, on the ability button |
| Vitalist | The heal | Ability | The defensive, Mending, on the dodge button |
| Aegis | Humbling Light and Shared Burden | Second ability | Retired from the simple kit |
| Aegis | The bubble field | Dodge | The special, Dome, on the ability button |
| Aegis | The barrier | Ability | The defensive, on the dodge button |
| Rogue | Recall (and Tether) | Second ability | The attack calls the daggers home while none is in hand |
| Rogue | The throw | Ability | The special, Knife (unchanged button) |

## Retired versions


## Swordsman (Earth, Fire, Frost)


### Attack (Swing)

Code: `ElementSwing`, `DualSwing` (Player/Player.Elemental.cs). Plain (no element): Swing.

| Ability | Element(s) | What it did |
|---|---|---|
| Heaving Swing | Earth | Earth's swing: rooted, twice as hard, 60 earth (a full meter stuns). |
| Searing Strike | Fire | Fire's swing: a quick blow, 40 fire (full: alight; full again: it combusts). |
| Razor Ice | Frost | Frost's swing: a quick blow, 50 frost (full: chilled; full again: frozen solid). |
| Fault Strike | Earth + Earth (overcharge) | Earth overcharged: a heavy blow of stone (full earth, shatters the frozen), and the floor splits on ahead of it. |
| Flame Crescent | Fire + Fire (overcharge) | Fire overcharged: a searing blow, and a crescent of flame bursts on from its tip, setting what it catches burning. |
| Frost Lance | Frost + Frost (overcharge) | Frost overcharged: the cut, and a lance of ice thrown on along it that freezes what it meets. |
| Magma Strike | Earth + Fire (mix) | Fire and earth: a blow of molten rock, fire and earth both, leaving a little pool of magma where it lands. |
| Permafrost Strike | Earth + Frost (mix) | Earth and frost: a blow of frozen stone, earth and frost both (it shatters the frozen), and spears of frozen rock burst up out of the floor before you. |
| Steam Strike | Fire + Frost (mix) | Fire and frost: what it strikes takes 50 of whichever of fire and frost it holds more of, and a cloud of steam bursts where it strikes (friends in it are hidden). |

### Dodge (movement)

Code: `ElementDodge`, `SwordLeap`, `DualLeap`, `TickSwordLeaps` (Player/Player.Elemental.cs); Combat/SwordSpells.cs. Plain (no element): Dash Slash.

| Ability | Element(s) | What it did |
|---|---|---|
| Earthfall | Earth | Earth's dodge: a leap (in the air, none), and you come down like a rock, in a shockwave of earth (the harder the fall, the harder it hits). |
| Explosive Lunge | Fire | Fire's dodge, from the ground: the ground bursts behind you (fire to everything beside you) and throws you forward and up; on the way you swing at every creature you come to. |
| Glacial Vault | Frost | Frost's dodge, from the ground: you spring back, leaving a likeness of yourself in ice where you stood (only one at a time). Creatures go for it; it takes a couple of blows before it shatters, filling those beside it with frost. |
| Meteor Fall | Earth + Earth (overcharge) | Earth overcharged: the Earthfall, but you come down like a falling star: a far greater shockwave. |
| Blast Lunge | Fire + Fire (overcharge) | Fire overcharged: a greater blast and a longer leap, fire burning where you pass. |
| Stalagmite Vault | Frost + Frost (overcharge) | Frost overcharged, from the ground: you leap back, and great stalagmites of ice burst up out of the ground you pass over, striking and filling with frost what they meet. They melt a moment later. |
| Blazing Charge | Earth + Fire (mix) | Earth and fire, the dash (on the ground or in the air): you charge forward in a sphere of fire, sword up before you, crashing into every creature in your way: a heavy blow and 100 fire each. Fire trails behind you. |
| Comet | Earth + Frost (mix) | Earth and frost (on the ground or in the air): you leap forward, all blue-white, and where you come down a great burst of frost strikes everything about (a heavy blow, 100 frost) as crystals of ice burst out of the floor and melt away. |
| Steam Vault | Fire + Frost (mix) | Frost and fire: the spring back, leaving a cloud of scalding steam where you stood: friends in it are hidden, creatures in it take fire and frost. |

### Second ability (Hard Swing)

Code: `TryHardSwing`, the Ice Axe (Player/Player.Elemental.cs); `DualSword` (Player/Player.Duals.cs). Plain (no element): Hard Swing.

| Ability | Element(s) | What it did |
|---|---|---|
| ThunderCrush | Earth | Earth's Hard Swing: slow and crushing, a full earth build-up (a stun), and it shatters the frozen. |
| Pyroclasty | Fire | Fire's Hard Swing: a full fire build-up (alight; alight already, it combusts). |
| Ice Axe | Frost | Frost's Hard Swing: the sword goes up over your head, an axe head of ice grows out of it, and it comes down in front of you: a full frost build-up (chilled; chilled already, frozen solid). |
| Earthsplitter | Earth + Earth (overcharge) | Earth and earth: a long wind-up as the ground trembles and rock lifts about you, then the blade comes down and a fissure tears along the floor, tossing up and stunning everything it passes under. Rubble in its way comes down. |
| Inferno Cleave | Fire + Fire (overcharge) | Fire and fire: a wall of fire stands where the blade lands. Creatures in it burn; shots flying into it burn up. |
| Glacier Cleaver | Frost + Frost (overcharge) | Frost and frost: held up behind you, the blade slowly grows a vast, ornate axe head of ice, then comes down in front of you. Its blow freezes what it cuts, and the head shatters it as it lands. |
| Frost Heave | Earth + Frost (mix) | Earth and frost: the ground bursts up in ice under the nearest creature: a great blow of frost, and it's thrown into the air. |
| Breaking Quake | Earth + Fire (mix) | Earth and fire: you gather, then drive the blade into the floor; great cracks full of magma tear out either way, and everything over them takes 100 fire and is thrown up. They close a moment later. |
| Thermal Shock | Fire + Frost (mix) | Fire and frost: what it strikes takes 100 of whichever of fire and frost it holds more of; then frozen solid it shatters, burning it bursts. |

### Ability (Empower Hilt)

Code: `TryHilt`, `DualHilt`, `HiltTouch` (Player/Player.Elemental.cs). Plain (no element): Empower Hilt.

| Ability | Element(s) | What it did |
|---|---|---|
| Earth Hilt | Earth | For 8 s every blow carries the element: earth saps a creature's attacks, frost and fire fill its build-up. |
| Fire Hilt | Fire | For 8 s every blow carries the element: earth saps a creature's attacks, frost and fire fill its build-up. |
| Frost Hilt | Frost | For 8 s every blow carries the element: earth saps a creature's attacks, frost and fire fill its build-up. |
| Stoneheart Hilt | Earth + Earth (overcharge) | Earth overcharged: every blow saps twice as long, for half again as long. |
| Blazing Hilt | Fire + Fire (overcharge) | Fire overcharged: every blow carries twice the fire, for half again as long. |
| Glacial Hilt | Frost + Frost (overcharge) | Frost overcharged: every blow carries twice the frost, for half again as long. |
| Magma Hilt | Earth + Fire (mix) | Earth and fire: every blow saps and burns for a while, and molten rock drips from the blade where it strikes (a little pool, now and then). |
| Permafrost Hilt | Earth + Frost (mix) | Earth and frost: every blow saps and chills for a while, and the frozen burst under it. |
| Steam Hilt | Fire + Frost (mix) | Fire and frost: for a while every blow adds twice the hilt's touch of whichever of fire and frost the creature holds more of. |

## Warden (Earth, Frost, Wind)


### Attack

Code: `WardenSwing`, `WardenBladeHit` (Player/Player.WardenElements.cs). Plain (no element): Shortsword.

| Ability | Element(s) | What it did |
|---|---|---|
| Stonecutter | Earth | Earth's cut: slow (half the pace) but twice as hard, 50 earth (a full meter stuns). |
| Rime Edge | Frost | Frost's cut: a steady pace, a fifth harder, 50 frost (full: chilled; full again: frozen solid). |
| Gale Edge | Wind | Wind's cut: quick and light (0.6x), reaching a little farther and throwing what it strikes far back. |
| Boulder Strike | Earth + Earth (overcharge) | Earth and earth: a crushing blow, more than twice as hard: stunned, tossed up, and the frozen shattered. |
| Icicle Lance | Frost + Frost (overcharge) | Frost and frost: the cut, and a lance of ice thrown on along your aim that freezes what it meets. |
| Cyclone Slash | Wind + Wind (overcharge) | Wind and wind: a whirling cut all round you, reaching far, throwing everything it strikes away. |
| Frozen Earth | Earth + Frost (mix) | Earth and frost: a hard blow of frozen stone, 60 earth and 60 frost; it shatters the frozen. |
| Sandblast | Earth + Wind (mix) | Earth and wind: the cut throws a cone of grit ahead: everything in it is nicked and its attacks slowed. |
| Frost Gust | Frost + Wind (mix) | Frost and wind: the cut looses a freezing wind ahead: everything in it is filled with frost and driven back. |

### Ability (Guarded Charge)

Code: `WardenDashVersion`, `WardenDashPass`, `WardenDashEnd` (Player/Player.WardenElements.cs). Plain (no element): Guarded Charge.

| Ability | Element(s) | What it did |
|---|---|---|
| Rockslide Charge | Earth | Earth's charge: it ploughs through everything in its way and ends in a shockwave of earth that stuns. |
| Frozen Tundra | Frost | Frost's charge: it fills what it passes with frost, and the ground it crossed freezes over (creatures on it chilled, friends quicker). |
| Gale Charge | Wind | Wind's charge: farther and faster, throwing creatures aside; friends it passes get a wind at their back. |
| Avalanche | Earth + Earth (overcharge) | Earth and earth: the rockslide, with a great quake at its end and a fissure running on ahead. |
| Glacier Charge | Frost + Frost (overcharge) | Frost and frost: everything it passes is frozen solid, and the ground it crossed freezes over. |
| Tempest Charge | Wind + Wind (overcharge) | Wind and wind: a long charge that carries every creature it meets along in its wake, then flings them. |
| Frostquake | Earth + Frost (mix) | Earth and frost: it ploughs through, ends in a quake, and ice bursts up where it stops. |
| Duststorm | Earth + Wind (mix) | Earth and wind: a long charge leaving clouds of dust behind (friends in them hidden, creatures slowed), and a quake at its end. |
| Blizzard Charge | Frost + Wind (mix) | Frost and wind: farther and faster, filling what it passes with frost and throwing it aside; the ground it crossed freezes over. |

### Block (the shield)

Code: `GuardRaised`, `RaiseBarrier`, `GuardStruck`, `ModeShieldShare` (Player/Player.WardenElements.cs); `ElementShield` (Player/Player.Warden.cs, Player/Upgrades.cs). Plain (no element): Block.

| Ability | Element(s) | What it did |
|---|---|---|
| Stone Shield | Earth | Earth's shield: a slab of brown-grey stone, solid. It absorbs 90% of every blow it meets, from the one pool of 50 shared by all your shields. |
| Frost Shield | Frost | Frost's shield: a pane of ice. It absorbs 45% of every blow it meets, from the one pool of 50 shared by all your shields. |
| Wind Shield | Wind | Wind's shield: a faint veil of light grey air. It absorbs 20% of every blow it meets, from the one pool of 50 shared by all your shields. |
| Earth Shield | Earth + Earth (overcharge) | Earth overcharged: the ground in front heaves up and over you and stands for as long as you hold the block: nothing reaches you, nothing moves you, and creatures can't pass it; you kneel behind it. Let go and it sinks. |
| Ice Barrier | Frost + Frost (overcharge) | Frost overcharged: a shell of ice about you (it stays when you lower the shield) absorbs 80% of every blow; once it has absorbed 15 it shatters: 90 frost and a blow to every creature about you. |
| Tenuous Barrier | Wind + Wind (overcharge) | Wind overcharged: a faint skin of wind about you absorbs 20% of every blow, and dissipates once it has absorbed 10. |
| Permafrost Guard | Earth + Frost (mix) | Earth and frost: the ground about you freezes (creatures on it chilled, friends quicker); strikers are stunned and filled with frost, and nothing gets through. |
| Dust Wall | Earth + Wind (mix) | Earth and wind: a cloud of dust in front hides your friends and slows the creatures in it; strikers are stunned and thrown back. |
| Hoarfrost Gale | Frost + Wind (mix) | Frost and wind: strikers are filled with frost and thrown back, and shots go back. |

## Elementalist (Fire, Frost, Wind)


### Attack (the bolt)

Code: `StaffBolt`, `Pop`, `LooseBolt`, `ElementBoltStruck` (Player/Player.Elemental.cs); `DualBolt` (Player/Player.Duals.cs). Plain (no element): Pop.

| Ability | Element(s) | What it did |
|---|---|---|
| Firebolt | Fire | A bolt of fire (the slower gap between casts): a small burst of flame where it strikes, and its fire build-up. |
| Frostbolt | Frost | A bolt of frost (the quicker gap): weaker, its frost build-up. |
| Gust | Wind | A quick gust of air (the quicker gap): a light blow that shoves what it strikes away. |
| Meteorite | Fire + Fire (overcharge) | Fire and fire: a meteorite falls on the thickest knot of creatures, smashing every platform it passes through (they grow back), and bursts in fire. |
| Cryometeor | Frost + Frost (overcharge) | Frost and frost: a rock of ice falls on the thickest knot of creatures; everything about it is frozen solid, and the ground freezes over. |
| Fire Barrage | Fire + Wind (mix) | Fire (or frost) and wind: a volley of seven bolts loosed one after another, fanned along the aim. |
| Frost Barrage | Frost + Wind (mix) | Fire (or frost) and wind: a volley of seven bolts loosed one after another, fanned along the aim. |
| Strong Gust | Wind + Wind (overcharge) | Wind and wind: a little whirlwind races along the floor from the staff, shoving every creature it meets back and nicking them as it goes. |
| Frostfire Bolts | Fire + Frost (mix) | Fire and frost: a bolt of fire and a bolt of frost loosed together. |

### Dodge (movement)

Code: `ElementMove`, `StaffJump` (Player/Player.Elemental.cs); `DualMove` (Player/Player.Duals.cs). Plain (no element): Lift.

| Ability | Element(s) | What it did |
|---|---|---|
| Fire Walk | Fire | Fire's dodge: for 4 s you move 30% faster and fire burns where you walk. |
| Tundra | Frost | Frost's dodge: the ground about you freezes over: heroes on it go faster, creatures on it are chilled and filled with frost. |
| Updraft | Wind | Wind's dodge: a column of rising air along your aim (straight up, aimed nowhere) to ride up. It carries heroes along it. |
| Flamebreath | Fire + Fire (overcharge) | Fire and fire: fire pours from the staff in a cone, burning creatures and the shots in it. Aimed down, it holds you up. |
| Frost Tower | Frost + Frost (overcharge) | Frost and frost: you leap, and a pillar of ice rises under you to stand and cast from. Creatures near go for it instead of you (it has a little health, more as you grow); your friends walk through it. |
| Fire Barrier | Fire + Wind (mix) | Fire and wind: a ring of fire about you, going where you go: creatures that touch it burn and are thrown back; shots burn up. |
| Frost Barrier | Frost + Wind (mix) | Frost and wind: frost armour: the next creature to strike you in melee is filled with frost (a full meter). |
| Sky Lift | Wind + Wind (overcharge) | Wind overcharged: a great push of air carries you high, and every friend near gets a wind at their back. |
| Steam Veil | Fire + Frost (mix) | Fire and frost: a burst of steam about you: friends in the cloud are hidden, creatures caught in it burn and chill. |

### Second ability (fixed-range spell)

Code: `TryFixed` (Player/Player.Elemental.cs); `DualFixed` (Player/Player.Duals.cs). Plain (no element): Force Blast.

| Ability | Element(s) | What it did |
|---|---|---|
| Burst | Fire | Fire's spell: an explosion at a spot ahead, 60 fire; and every creature in view already burning, wherever it is, takes a full build-up (and combusts). |
| Snap | Frost | Frost's spell: every creature in view frozen solid shatters, wherever it is; and the cold closes on a spot ahead, filling what's there with frost. |
| Cyclone | Wind | Wind's spell: a storm at a spot ahead that drags light creatures into its middle and swallows the shots that fly into it. |
| Rain of Fire | Fire + Fire (overcharge) | Fire and fire: fire rains over a spot ahead for 3 s, each fireball bursting where it lands. |
| Hailstorm | Frost + Frost (overcharge) | Frost and frost: hail pours over a spot ahead for 3 s, filling creatures with frost and beating flying ones down. |
| Fire Storm | Fire + Wind (mix) | Fire and wind: a storm of fire, fanned by the wind: it burns, drags light creatures in and swallows shots. |
| Blizzard | Frost + Wind (mix) | Frost and wind: a storm of snow, fanned by the wind: it chills, drags light creatures in and swallows shots. |
| Tornado | Wind + Wind (overcharge) | Wind and wind: a tornado runs along the floor, carrying light creatures along inside it and swallowing their shots, then flings them away. |
| Frostfire Lance | Fire + Frost (mix) | Fire and frost: one beam through every creature in a line, fire and frost together; one already burning or chilled takes far more. |

### Ability (Aid)

Code: `TryAid`, `DualAid` (Player/Player.Elemental.cs). Plain (no element): Ward.

| Ability | Element(s) | What it did |
|---|---|---|
| Kindling | Fire | Fire's Aid: for 8 s you and your friends near add a little fire to every blow. |
| Frost Armor | Frost | Frost's Aid: for 8 s a creature that strikes you in melee is filled with frost. |
| Narrow Draft | Wind | Wind's Aid: for 4 s you and your friends near move 20% faster and jump higher. |
| Bonfire | Fire + Fire (overcharge) | Fire overcharged: Kindling for half again as long, and a ring of fire bursts out about you. |
| Ice Armor | Frost + Frost (overcharge) | Frost overcharged: Frost Armor, and a shell of ice that soaks blows besides. |
| Gale Draft | Wind + Wind (overcharge) | Wind overcharged: Narrow Draft for twice as long, and a gust that throws the creatures about you back. |
| Thermal Armor | Fire + Frost (mix) | Fire and frost: Frost Armor for you, Kindling for you and your friends near. |
| Wildfire | Fire + Wind (mix) | Fire and wind: Kindling and a wind at the back for you and your friends near. |
| Frostwind | Frost + Wind (mix) | Frost and wind: Frost Armor for you, a wind at the back for you and your friends near. |

## Vitalist (Nature, Wind, Water)


### Attack (the drain)

Code: `VitalDrain`, `VitalDrainStruck` (Player/Player.VitalistElements.cs). Plain (no element): Drain.

| Ability | Element(s) | What it did |
|---|---|---|
| Thorn Drain | Nature | Nature's drain: thorns burst out of it (40 nature; full, it's entangled), and a quarter more vital force comes home. |
| Wisp Drain | Wind | Wind's drain: reaches 40% farther and casts a quarter quicker, a little lighter. |
| Tide Drain | Water | Water's drain: 40 water (full, it's soaked: fire out, frost twice as fast), and you're mended by part of what it tears out. |
| Bramble Lash | Nature + Nature (overcharge) | Nature overcharged: a full meter of nature, and brambles lash out to up to three creatures beside it, draining them too. |
| Whirling Siphon | Wind + Wind (overcharge) | Wind overcharged: the wind takes more at once: up to three more creatures in reach are drained as well. |
| Riptide | Water + Water (overcharge) | Water overcharged: a full meter of water (soaked), and the current hauls it toward you. |
| Pollen Drain | Wind + Nature (mix) | Wind and nature: pollen carried off it on the wind fills everything about it with nature. |
| Life Spring | Nature + Water (mix) | Nature and water: what it tears out wells up as a spring, mending every friend near you by part of it. |
| Rain Siphon | Wind + Water (mix) | Wind and water: rain carried off it soaks everything about it (water). |

### Dodge (the hex)

Code: `VitalHex`, `VitalHexStruck` (Player/Player.VitalistElements.cs). Plain (no element): Hex.

| Ability | Element(s) | What it did |
|---|---|---|
| Overgrowth | Nature | Nature's hex: vines erupt about you, filling every creature in it with nature (50). |
| Withering Gale | Wind | Wind's hex: a fifth wider, and everything in it is thrown out away from you. |
| Drowning Hex | Water | Water's hex: everything in it is filled with water (60). |
| Strangling Grove | Nature + Nature (overcharge) | Nature overcharged: a wider grove of vines; every creature in it is entangled (a full meter). |
| Eye of the Tempest | Wind + Wind (overcharge) | Wind overcharged: far wider, and everything in it is hauled in toward you, where your friends can get at it. |
| Maelstrom | Water + Water (overcharge) | Water overcharged: everything in it is soaked, and a pool of healing water wells up at your feet. |
| Spore Storm | Wind + Nature (mix) | Wind and nature: much wider; spores fill everything in it with nature and the wind throws it out. |
| Mire Bloom | Nature + Water (mix) | Nature and water: the ground turns to mire: everything in it is slowed in all it does, and filled with nature. |
| Squall | Wind + Water (mix) | Wind and water: a squall soaks everything in it with water and throws it out. |

### Ability (the heal)

Code: `VitalHeal`, `VitalHealStruck`, `HealReach`/`HealAmp`/`HealSpread` (Player/Player.VitalistElements.cs). Plain (no element): Heal.

| Ability | Element(s) | What it did |
|---|---|---|
| Regrowth | Nature | Nature's heal: half of it now, and three quarters more grows back over 5 s. |
| Breath of Life | Wind | Wind's heal: reaches 60% farther, and everyone near gets a wind at their back. |
| Cleansing Wave | Water | Water's heal: washes every affliction (poison, fire, frost, webs, stuns) off those it reaches. |
| Bloom of Life | Nature + Nature (overcharge) | Nature overcharged: half again as strong, and a pool of healing blooms at your feet. |
| Second Wind | Wind + Wind (overcharge) | Wind overcharged: reaches every friend wherever they are, and gives them all a wind at their back. |
| Wellspring | Water + Water (overcharge) | Water overcharged: a fifth stronger, a barrier of water on everyone near, and every affliction washed off. |
| Seeds on the Wind | Wind + Nature (mix) | Wind and nature: reaches 60% farther, and more mending grows on everyone near over 5 s. |
| Rejuvenation | Nature + Water (mix) | Nature and water: part now and all of it again over time, and every affliction washed off. |
| Mist of Mending | Wind + Water (mix) | Wind and water: the heal, and a mist about you that hides your friends in it. |

## Aegis (Wind, Earth, Frost)


### Attack (the bubble shot)

Code: `AegisShot`, `AegisShotBurst` (Player/Player.AegisElements.cs). Plain (no element): Bubble Shot.

| Ability | Element(s) | What it did |
|---|---|---|
| Gale Bubble | Wind | Wind's shot: half again as fast, cast more often, and its burst throws creatures back. |
| Stone Bubble | Earth | Earth's shot: slower and heavier: half again as hard, a wider burst, 40 earth to all in it. |
| Rime Bubble | Frost | Frost's shot: 40 frost to everything in the burst. |
| Boulder Bubble | Earth + Earth (overcharge) | Earth overcharged: more than twice as hard, a wide burst; the one it hits is stunned (a full meter of earth). |
| Frost Orb | Frost + Frost (overcharge) | Frost overcharged: a full meter of frost to everything in the burst. |
| Bubble Volley | Wind + Wind (overcharge) | Wind overcharged: three fast shots, fanned. |
| Hail Bubble | Earth + Frost (mix) | Earth and frost: half again as hard, earth and frost to all in the burst; the frozen shatter. |
| Sand Bubble | Earth + Wind (mix) | Earth and wind: the burst leaves a cloud of dust that hides your friends and slows what's caught in it. |
| Snow Bubble | Frost + Wind (mix) | Frost and wind: a wider burst of snow: frost, and everything in it thrown back. |

### Dodge (the bubble field)

Code: `AegisDome`, `AegisDomeUp`, `DomeReach`/`DomeCap`/`DomeTime` (Player/Player.AegisElements.cs). Plain (no element): Bubble Field.

| Ability | Element(s) | What it did |
|---|---|---|
| Air Dome | Wind | Wind's field: creatures near are thrown out as it goes up. |
| Stone Dome | Earth | Earth's field: the bubbles are half again as strong. |
| Ice Dome | Frost | Frost's field: creatures near are filled with frost (40). |
| Fortress | Earth + Earth (overcharge) | Earth overcharged: bubbles more than twice as strong, lasting half again as long. |
| Glacier Dome | Frost + Frost (overcharge) | Frost overcharged: a full meter of frost to every creature near. |
| Gale Dome | Wind + Wind (overcharge) | Wind overcharged: reaches friends far farther off, and throws creatures out. |
| Crystal Dome | Earth + Frost (mix) | Earth and frost: stronger bubbles, and frost armour on everyone it wraps. |
| Dust Dome | Earth + Wind (mix) | Earth and wind: a cloud of dust about you hides your friends; creatures near are slowed. |
| Blizzard Dome | Frost + Wind (mix) | Frost and wind: frost to every creature near, and the wind throws them out. |

### Ability (the barrier)

Code: `AegisBarrier`, `AegisWardGiven`, `WardAmp` (Player/Player.AegisElements.cs). Plain (no element): Barrier.

| Ability | Element(s) | What it did |
|---|---|---|
| Wind Ward | Wind | Wind's barrier: and a wind at their back. |
| Stone Skin | Earth | Earth's barrier: half again as strong. |
| Frost Ward | Frost | Frost's barrier: and frost armour while it lasts (melee strikers are filled with frost). |
| Bulwark Ward | Earth + Earth (overcharge) | Earth overcharged: two and a half times as strong. |
| Glacial Ward | Frost + Frost (overcharge) | Frost overcharged: a little stronger, and the next creature to strike them in melee is frozen full. |
| Twin Gale | Wind + Wind (overcharge) | Wind overcharged: on your friend and you both, each with a wind at their back. |
| Permafrost Ward | Earth + Frost (mix) | Earth and frost: much stronger, and frost armour. |
| Sandstorm Ward | Earth + Wind (mix) | Earth and wind: a cloud of dust about the one it wraps hides them. |
| Winter Ward | Frost + Wind (mix) | Frost and wind: frost armour and a wind at their back. |

## Rogue (Dark, Wind, Nature)


### Attack (the jab)

Code: `RogueStrike`, `RogueBladeHit` (Player/Player.RogueElements.cs). Plain (no element): Jab.

| Ability | Element(s) | What it did |
|---|---|---|
| Shadow Strike | Dark | Dark's jab: a fifth harder, 40 dark (full: blinded: it loses you, and takes more and more critical blows). |
| Gale Jab | Wind | Wind's jab: far quicker, a little lighter, reaching farther, and it flicks what it strikes back. |
| Venom Jab | Nature | Nature's jab: 40 nature (full: entangled), and a poison. |
| Nightfall | Dark + Dark (overcharge) | Dark overcharged: a sure critical, 60% harder, and a full meter of dark (blinded). |
| Flurry | Wind + Wind (overcharge) | Wind overcharged: a blur of jabs, sweeping half round you, far quicker, flicking all it strikes back. |
| Deathcap | Nature + Nature (overcharge) | Nature overcharged: a full meter of nature (entangled) and a double poison. |
| Thornwind | Wind + Nature (mix) | Wind and nature: quick, long reach, nature, and a flick back. |
| Shade Step | Wind + Dark (mix) | Wind and dark: dark, and you step through the creature you strike to its far side, at its back. |
| Nightshade | Nature + Dark (mix) | Nature and dark: dark and nature together, and a poison. |

### Dodge (the evade)

Code: `RogueEvade` (Player/Player.RogueElements.cs). Plain (no element): Vanish.

| Ability | Element(s) | What it did |
|---|---|---|
| Shadowmeld | Dark | Dark's evade: the shadows hold you half again as long. |
| Gale Step | Wind | Wind's evade: vanish with a dash ahead. |
| Spore Cloud | Nature | Nature's evade: spores burst about you, filling creatures near with nature. |
| Umbral Veil | Dark + Dark (overcharge) | Dark overcharged: twice as long in the shadows, and every creature near is blinded. |
| Wind Walk | Wind + Wind (overcharge) | Wind overcharged: the dash, and a wind at your back for 4 s. |
| Overgrowth Veil | Nature + Nature (overcharge) | Nature overcharged: creatures near are entangled (a full meter of nature). |
| Leaf Storm | Wind + Nature (mix) | Wind and nature: a storm of leaves throws creatures near out, filling them with nature. |
| Shadow Dash | Wind + Dark (mix) | Wind and dark: a long dash, leaving a cloud of smoke where you stood. |
| Nightbloom | Nature + Dark (mix) | Nature and dark: a cloud of smoke about you, dark and nature to creatures near. |

### Ability (the throw)

Code: `RogueThrow`, `RogueKnifeStruck` (Player/Player.RogueElements.cs). Plain (no element): Dagger Throw.

| Ability | Element(s) | What it did |
|---|---|---|
| Shadow Knife | Dark | Dark's throw: 50 dark to what it strikes. |
| Gale Knife | Wind | Wind's throw: half again as fast, knocking what it strikes back. |
| Barbed Knife | Nature | Nature's throw: 50 nature and a poison. |
| Umbral Knife | Dark + Dark (overcharge) | Dark overcharged: a sure critical and a full meter of dark (blinded). |
| Fan of Knives | Wind + Wind (overcharge) | Wind overcharged: both daggers at once, fanned, fast, knocking back. |
| Thorn Knife | Nature + Nature (overcharge) | Nature overcharged: a full meter of nature (entangled) and a bleed. |
| Pollen Knife | Wind + Nature (mix) | Wind and nature: pollen bursts off it, nature to everything about the one it strikes. |
| Shadow Tether | Wind + Dark (mix) | Wind and dark: dark, and you're drawn along to the dagger, striking again as you land. |
| Nightshade Knife | Nature + Dark (mix) | Nature and dark: dark, nature and a poison. |

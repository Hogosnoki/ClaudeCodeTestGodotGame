# Dagger Deep: catalogue

Every hero ability, reward, upgrade, side-grade, technique, bargain, relic, class perk and meta rank, for balance review.
The card sections (from "Hero upgrades" on) are generated from the game's own data: `--dumpcards=PATH` rewrites them.
"×N" = can be taken N times; *(rare)* = an ability-tier card (likelier in chests, dealt by vaults); *(party)* = only offered online with friends.

## Heroes and their abilities

Each hero has an attack, a dodge-button ability, two ability buttons and a support ability (the fourth button). Numbers are base values.

| Hero | HP · move | Attack | Dodge button | Ability | Second ability | Support |
| --- | --- | --- | --- | --- | --- | --- |
| **Swordsman** | 60 · 100% | Sword: 20 dmg, 45 px, every 0.6 s, combo, heavy knockback | **Dash-slash**: 0.28 s dash, slashes the first creature for 1.5× a strike; 15 s cooldown | **Charged Strike**: next swing +50% dmg, +25% reach, target deals 20% less for 5 s; 12 s | **Heaving Swing**: planted, 190° arc, 2× dmg, shatters frozen; 6 s | **Battle Shout**: allies near deal +10% for 10 s; 30 s |
| **Warden** | 110, 10% armour · 92% | Shortsword: 11 dmg, 30 px, every 0.36 s | **Shield** (hold): 135° arc stops whole blows; 40 points, soaks 2× (80), regen 3/s | **Guarded Charge**: shield rush that eats projectiles and breaks the first attack met; 3.2 s | **Shield Bash**: 20 dmg + 1.6 s stun in a half-circle; 10 s | **Taunt**: every creature targets you for 3 s; 20 s |
| **Vitalist** | 55 · 97% | Drain: 10 dmg at 175 px every 0.5 s; a tenth returns as vital force | **Hex**: 110 px, slow to 55% and +20% damage taken for 5 s; 6 s | **Heal** (0.4 s wind-up): 15 vital force → 15 HP shared among the hurt nearby; 3 s | **Rupture** (0.8 s wind-up): 30 vital force, target bursts for 30, 10 to others within 80 px | **Health Tap**: 60% of vital force → 15% of health; 15 s |
| **Elementalist** | 55 · 97% | Firebolt: 28 dmg, homing, bursts for 35% on creatures within 26 px, 2 charges (1 per 1.5 s), 65% ignite (4/s, 4 s). Frostbolt side-grade | **Updraft**: 10 s column, gravity 0.4 inside; recharge costs 15 alimus | **Blizzard** (0.5 s): 9 strikes of 2 over 3 s, 6% freeze each; recharge costs 20 alimus; 20 s | **Snap**: everything frozen shatters for 30 (8 splash); recharge costs 6 alimus | **Stalag-Might** (1 s): 30 dmg + 2 s root; 15 s |
| **Rogue** | 55 · 110% | Dagger Slash: 9 dmg, 22 px, 5 jabs/s (half with a dagger out); 5% crit ×2 | **Vanish**: 6 s hidden, +50% speed, first blow ×4 (surprise); 12 s | **Dagger Throw**: 18 dmg, sticks in its target | **Recall** (0.4 s): daggers return, tearing out for 9 and yanking the creature | **Expose**: target takes +20% and +10% crit chance for 8 s; 20 s |
| **Aegis** | 70 · 98% | Ward Bolt: 10.8 dmg every 0.5 s, 60% burst on neighbours, weakens (−10% dmg, 5 s); no healing | **Bubble**: on a friend or you, absorbs 10% of each blow up to 10% of their HP; 20 s | **Barrier** (0.25 s): 14% of the target's max HP as a shield for 15 s; 10 s | **Shared Burden**: take 20% of what gets through a friend's own armour, for 60 s (alone: **Humbling Light**, creatures within 120 px deal 30% less for 3 s; 12 s) | **Share Pain**: take 15% of a chosen friend's blows; 4 s between picks |
| **Shape Shifter** | 70 · 100% | Staff: 5 dmg, 52 px, every 0.55 s; in a form, the form's attack (15 dmg, +60% Transformation buff) | Dodge roll | **Shift**: become the nearest creature (110 px); press again to drop it; 3 s. **Remember/Recall**: take a remembered form again (1.5 s) | **Form Special**: each form's own trick (bat latch, rat gnaw, spider spray…) | **Pack Howl**: allies +20% move, +15% attack speed for 8 s; 25 s |
| **Automaton** | 300, 15% armour · 72% | Cleaver: 17.5 dmg, 64 px, every 0.85 s, slam splash 55% | **Brace**: 0.75 s, soaks 70%, punch 22; 2.6 s | **Self-Repair**: burns fuel to mend 10/s for 4 s (healing received becomes fuel); 6 s | **Steam Release**: 96 px cone, 9 dmg + scald 5/s 3 s, big push; aimed down it lifts you; 6 s | **Pressurize** (toggle): ×1.8 dmg, ×1.35 attack, ×1.45 speed, burns 4 fuel/s; 8 s |

## Rewards (where the power comes from)

- **Level-ups**: every level gives a small hero-specific stat gain (e.g. Warden +6 HP, +2.25% dmg, +1.5% guard). Every 8 levels (7/6/5 with the Path) a **milestone point**: a pick of 3 for your hero, always including a side-grade (and Magma Skin from depth 6) while any remain (and a party card online).
- **Upgrade shrines / wood chests** (treasure rooms, caches high up and under water, mini-bosses): 3 cards: one class card for your hero, two generic. Heals you a little. "Leave it" keeps them for later or a friend.
- **Relic chests** (silver; hidden chambers, ~relic-chest chance in place of a wood chest): one relic, no choice: take it or leave it.
- **Relic Collection chests** (teal, three gems; the guardian's party chest, and 15% of relic chests): one relic for each of the party; each hero takes one.
- **Guardian's hoard** (gold chest, one per player) + a Relic Collection for the party, embers (2, 3 from depth 5, 6 for the dragon) and one meta resource (a Reagent or an Obsidian Pearl).
- **Vaults** (behind a key-locked gate; a key is hidden in the level or dropped by the first mini-boss): two bargains and one rare class card.
- **Corrupted chests** (the Null): corrupted cards.
- **Hornet hive** (Nest): destroying it leaves a wood chest.
- **Hearts** (creature drops; elites likelier, guardians always): heal. **Potions** (rare drops, and the merchant): 15% at once + 15% over 15 s (improved by the Potion tree); belt of 1 (2 with the tree; 3 only with a Deep Flask found on the way).
- **Gold**: never on the main floor: dead ends (coins), high platforms, ledges to nowhere, alcoves and raised crevices; piles in the hardest quarter to reach; creatures drop it (7%, elites 40%, guardians a pile of 12). Spent with the merchant.
- **The merchant's stall** (twice a run, between depths 1 and 9, once he is freed from a cage): 2 cards, a relic, 2 supplies (potion, provisions = 40% heal, a Second Thoughts token), "deal again" for gold, and a **trading stand** where a hero gives a relic they carry to a friend (1 trade a run, +1/+2 from the Path's Trading ranks; it comes back if the friend can't carry it). Prices by rarity: 8 / 14 / 24 / relic 36, +8% per depth.
- **Second Thoughts** (card rerolls): 1–3 a run from the Path, or a merchant token; deals a chest's or milestone's choice, or the merchant's wares, again.
- **Hero cages**: free a locked hero (or the merchant). **Journal pages** at guild lamps: lore, a run-long "felt" buff, 1 ember the first time.
- **Between runs**: embers buy **class perks** (permanent, a few slots per hero) and **meta ranks** (activated with that tree's resource).

## Hero upgrades and side-grades (by ability)

Class upgrades come in chests (one per chest, for your hero) and milestones; side-grades (alterations) change how an ability works, one per ability, from milestones or the loadout.

### Swordsman

**Sword**
- **Longer Blade** ×3: Blade reach +15%.
- **Flurry** ×3 *(rare)*: Your combo chains one more strike: landing it refunds the swing cooldown again.
- **Finisher** *(rare)*: The last strike of a full combo hits much harder (x2 damage, wider arc).
- **Downward Thrust** *(rare)*: Aerial down-slashes bounce you off enemies and refresh air jumps (once: touch the ground before the next bounce).
- **Thirsty Blade** ×3: Heal 1% of damage dealt.
- **Rending Edge** ×2 *(rare)*: Sword hits make enemies bleed for 30% more damage over 3 s.
- **Executioner** *(rare)*: +60% damage to enemies below 25% health.

**Charged Strike**
- **Focus** ×2: Charged Strike recharges 20% faster.
- **Twin Charge** *(rare)*: Charged Strike empowers your next two swings (with Relentless Charge, two combos).
- **Crippling Strike** *(rare)*: Enemies struck by a charged swing deal 35% less damage instead of 20%.
- ◆ side-grade **Relentless Charge**: Your Charged Strike empowers your whole next combo, at 80% strength: +40% damage and +20% reach on every strike.

**Heaving Swing**
- **Broad Shoulders** ×2: Heaving swing recharges 20% faster.
- **Relentless Heave** *(rare)*: Your heaving swing has a second use, but each comes back 60% slower.
- ◆ side-grade **Swift Heave**: With a Charged Strike waiting, your heave spends it on speed instead of force: no wind-up, and you can heave in the air.

**Dash Slash**
- **Phantom Step** *(rare)*: Dashing makes you briefly invulnerable.
- **Nimble** ×2: Dodge cooldown -20%.
- **Second Wind** *(rare)*: Gain a second dash-slash, but each comes back 60% slower.
- **Flowing Counter**: A counter that lands can start a combo.
- ◆ side-grade **Counter Dash**: Dashing into a melee blow ends the dash, stops all of that blow and swings back at the attacker.
- ◆ side-grade **Dash Through**: Your dash-slash goes through the first creature it meets instead of stopping at it, and you turn, once clear of it, to slash it from behind (1.5 times a strike).

### Warden

**Shortsword**
- **Longer Blade** ×3: Blade reach +22%.
- **Flurry** ×3 *(rare)*: Your combo chains one more strike: landing it refunds the swing cooldown again.
- **Finisher** *(rare)*: The last strike of a full combo hits much harder (x2 damage, wider arc).
- **Downward Thrust** *(rare)*: Aerial down-slashes bounce you off enemies and refresh air jumps (once: touch the ground before the next bounce).
- **Thirsty Blade** ×3: Heal 1% of damage dealt.

**Shield**
- **Riposte Guard** *(rare)*: A perfect block (raise the shield just before the hit) reflects projectiles back at enemies.
- **Iron Timing**: Perfect blocks cost your shield 70% less.
- **Stalwart**: Full speed with the shield raised, and hits never knock you back.
- **Quick Mend** *(rare)*: Your shield starts regenerating almost at once after a block, and 50% faster.
- **Spiked Shield** *(rare)*: Melee attackers that strike your shield take 40% of the blow back.
- **Last Stand** *(rare)*: Once per depth, a killing blow leaves you at 1 HP, briefly invulnerable, with a whole shield.
- **Bastion** ×2: +25% shield durability.
- **Braced** ×2: Your unyielding shield stops 5% more of each blow.
- ◆ side-grade **Unyielding Shield**: Your shield never weakens or breaks, but stops only 70% of each blow (a perfect block still stops it all).

**Guarded Charge**
- **Ready Charge** ×2: Guarded Charge cooldown -20%.
- **Long Charge** ×2: Your Guarded Charge carries you 30% farther.
- **Battering Charge** *(rare)*: Your Guarded Charge hits what it stops four times as hard.
- **Rallying Charge** *(rare)*: Breaking an attack with your Guarded Charge mends your shield by 12 and heals 4.
- **Vanguard** ×2: Guarded Charge recharges 20% faster.
- **Thick Barrier**: Your barrier soaks 50% more.
- **Lasting Barrier**: Your barrier lasts 33% longer.
- ◆ side-grade **Guardian's Charge**: Your Guarded Charge rushes to the friend nearest your aim and wraps them in a barrier that soaks 20 damage for 2 s (alone, it wraps you).

**Shield Bash**
- **Hard Shoulder** ×2: Shield bash recharges 20% faster.
- **Dense Metal** ×2: Your shield bash costs your shield 30% less.
- **Return to Sender**: Projectiles your bash sends back deal double damage.
- ◆ side-grade **Deflecting Bash**: Your shield bash no longer stuns: instead it sends every projectile in a wide arc in front back where it came from.

### Vitalist

**Drain**
- **Many Mouths** ×2 *(rare)*: Your drain also tears the life out of one more creature near its target (60% damage).
- **Far Reach** ×2: Your drain and rupture reach 25% farther.
- **Hungering Spirit** ×2: Gain 1.5x as much vital force from the damage you deal.
- **Deep Well** ×2: Hold 15 more vital force.

**Hex**
- **Spreading Blight** ×2: Your hex reaches 20% farther.
- **Lingering Hex** ×2: Your hex lasts 2 s longer.
- **Withering Hex** *(rare)*: Hexed creatures rot, losing 3 health a second.
- ◆ side-grade **Blight Burst**: Your hex also deals 8 damage to everything it reaches, but slows and weakens half as much.

**Heal**
- **Deep Mending** ×2: Your heal restores 20% more.
- **Frugal Rites** ×2: Your heal costs 25% less vital force.
- **Wellspring** ×4: +15% vital force gained and +10% heal strength.
- **Patient Mending**: Your heals heal for 60% at once, and 60% more over 4 s.
- **Warding Mending**: Anyone your heal is mending takes 20% less damage.

**Rupture**
- **Burst Veins** *(rare)*: Your rupture's burst reaches 30% farther and splashes 1.5x as hard.
- **Thin Blood** ×2: Your rupture costs 20% less vital force.
- **Healing Pool**: Your bloom leaves a pool that heals everyone in it 2 a second for 5 s.
- ◆ side-grade **Lifebloom**: Your rupture blooms on a friend instead of a creature (alone, on you): it heals them 30, and everyone else in the burst 10.

### Elementalist

**Firebolt**
- **Kindling** ×2: Your fire is 10% likelier to set a creature alight (firebolts, and a Firestorm).
- **Deep Reservoir** ×2: Hold 20 more alimus.
- **Spare Bolt**: Your staff holds one more firebolt (two more frostbolts, with Frostbolt).
- **Flashpoint** ×2: Your firebolt's burst reaches 40% farther and splashes 15% more of the blow.
- ◆ side-grade **Frostbolt**: Your bolts are frost instead of fire: 10 damage a bolt, the staff holds four and gets one back every second, each slowing what it strikes by 30% for 2 s, with a 20% chance of freezing a creature solid for 3 s (never a mini-boss, guardian or boss).

**Updraft**
- **Attunement** ×3: Your alimus comes back 15% faster.

**Blizzard**
- **Deep Chill** ×2: Your frost is 4% likelier to freeze a creature solid (frostbolts, and the blizzard).
- **Long Winter** *(rare)*: Your blizzard lasts 50% longer.
- **Whiteout**: Your blizzard is 30% wider.
- **Gathering Storm**: Your blizzard comes back 25% sooner.
- ◆ side-grade **Firestorm**: Your blizzard is a storm of fire: 3 damage a strike, each with a 10% chance of setting a creature alight (but it freezes nothing).

**Snap**
- **Shrapnel** *(rare)*: Your snap's bursts reach 30% farther.
- **Echo** *(rare)*: Every creature your snap bursts gives you 3 alimus back.
- ◆ side-grade **Cinder Snap**: Your snap no longer puts out the burning creatures it bursts (they keep burning), but comes back 2 s later.

### Rogue

**Dagger Slash**
- **Keen Edge** ×2: Your strikes are 8% likelier to be critical (twice as hard).
- **Backstab** *(rare)*: Your strikes from behind a creature land 50% harder.
- **Cruel Edge** *(rare)*: Your critical strikes land 2.5 times as hard instead of twice.

**Dagger Throw**
- **Twin Throw** *(rare)*: With both daggers in hand, you throw both at once.
- **Weighted Daggers** ×2: Your thrown daggers strike 30% harder.
- **Chain Ricochet**: Your ricochet springs on to one more creature.
- ◆ side-grade **Ricochet**: A thrown dagger springs on from the creature it strikes to one more nearby, marking each (it still sticks in walls, never in creatures). Recall flings a poison pin at every marked creature.

**Vanish**
- **Surprise Attack** *(rare)*: The strike that ends your vanishing (a slash, a throw or a recall) lands three times as hard.
- **Quick Fade** ×2: Vanish comes back 20% sooner.
- **Deep Shadows** ×2: Your vanishing (or your smoke) lasts 3 s longer.
- **Thick Smoke**: Your cloud of smoke is 40% wider.
- ◆ side-grade **Smoke Bomb**: Instead of vanishing alone, you throw down a cloud of smoke for 6 s: every hero in it is hidden, and creatures in it can't find anyone.

**Recall**
- **Rending Recall** *(rare)*: Your recall tears out of creatures twice as hard.
- **Serrated Recall**: A recall has a 60% chance to make the creature bleed for double the blow's damage over 5 s.
- **Pounce**: Arriving by tether is always a critical strike.
- ◆ side-grade **Tether**: Recall pulls you to your dagger stuck in a creature (striking it as you arrive) instead of pulling the dagger to you.

### Aegis

**Ward Bolt**
- **Brighter Bolt** ×3: Your ward bolt hits 18% harder.
- **Wider Burst** ×2: Your bolt's burst reaches 20% farther.
- **Scatter Light** ×2: The creatures beside the one your bolt strikes take 10% more of the blow.
- **Sapping Light** ×2: Creatures your bolt bursts on deal 5% less still (15%, then 20% less).
- **Lingering Weakness** *(rare)*: What your bolt weakens stays weakened 3 s longer.

**Barrier**
- **Reinforced Barrier** ×2: Your barrier absorbs 20% more.
- **Quick Ward** ×2: Your barrier comes back 15% sooner.
- **Twin Barrier** *(rare)*: A barrier for a friend wraps you in one as well.
- ◆ side-grade **Wide Barrier**: Your barrier falls on every friend near you, and on you, each at 60% of its strength.

**Shared Burden**
- **Heavier Burden** *(party)*: You carry 10% more of the blows your friend takes, and another 5% of each blow is simply turned aside.
- **Steeled Heart** ×2 *(party)*: What you carry for a friend weighs 20% less on you.
- ◆ side-grade **Foe's Burden**: Your second ability lays its burden on a creature instead: for 12 s a quarter of every blow you take falls on it. It comes back in 6 s.
- ◆ side-grade **Burden of Many** *(party)*: The friend you burden shares 20% of what they take with everyone near them (you among them).

**Bubble**
- **Tougher Bubble** ×3: Your bubble bursts only after absorbing 10% more.
- **Quick Bubble** ×2: Your bubble comes back 20% sooner.
- **Soothing Burst** *(rare)*: When your bubble bursts, it heals the friends around it.

### ShapeShifter

**Staff**
- **Weighted Staff** ×3: Your staff hits 30% harder.
- **Long Grip** ×2: Your staff reaches 18% farther.

**Shift**
- **Quick Change** ×2: Shift comes back 20% sooner.
- **Keen Eye** ×2: You can copy creatures 40% farther away.
- **Mending Change** *(rare)*: Taking a form heals you for 7% of your health.

**Forms**
- **Honed Fangs** ×3: Every form's attacks hit 15% harder.
- **Thick Hide** ×3: Every form shrugs off 8% more of the blows it takes.
- **Fleet Form** ×2: Every form moves 10% faster.
- ◆ side-grade **Rending Forms**: What a form's attacks hit bleeds for half the damage over 3 s.

**Form Specials**
- **Practiced Tricks** ×3: Form specials come back 15% sooner.
- **Savage Tricks** ×3: Form specials hit 15% harder.

### Automaton

**Cleaver**
- **Honed Cleaver** ×2: Your cleaver hits 15% harder.
- **Oiled Joints** ×3: 12% less time between your swings.
- **Heavy Piston** *(rare)*: The slam that ends your combo reaches 25% farther and hits 25% harder.

**Self-Repair**
- **Fine Tools** ×2: Self-Repair mends you 30% faster.
- **Bigger Accumulator** ×3: Hold 25 more fuel.
- **Efficient Gearing** ×2: Healing you receive gives 5% more of itself as fuel (35%, then 40%).
- ◆ side-grade **Scrap Reclaimer**: Every creature you destroy gives you 10 fuel, but healing gives you none.

**Steam Release**
- **Pressure Valve** ×2: Steam Release comes back 20% sooner.
- **High-Pressure Steam** ×2: Steam Release hits 30% harder, shoves 30% harder and lifts you 30% higher.
- ◆ side-grade **Scalding Steam**: Your steam no longer shoves creatures; it scalds them, and they burn for 3 s.

**Brace**
- **Locked Joints** ×2: Brace holds 0.25 s longer.
- **Piston Fist** ×2: Brace's counter-punch hits 60% harder.
- ◆ side-grade **Full Plate**: Braced, you soften blows from behind as well as in front, but no longer counter-punch.

## Generic upgrades (34)

Anyone's; the two other cards in a chest.

- **Quick Hands** ×5: 20% less time between your attacks.
- **Whetstone** ×6: +15% damage.
- **Light Boots** ×4: +10% movement speed.
- **Spring Step** ×2: +16% jump height.
- **Webbed Gloves** ×4: +25% swim speed.
- **Deep Lungs** ×3: +50% breath underwater.
- **Vitality** ×5: +10 max HP and heal 10.
- **Resilience** ×3: Stay invulnerable 0.2 s longer after being struck.
- **Toughened Hide** ×4: Take 10% less damage.
- **Deep Flask**: Carry one more potion, and creatures are 2% likelier to drop one.
- **Keen Eye** ×5: Your hits are 5% likelier to be critical.
- **Opportunist** ×2: Hit creatures that are burning, chilled, frozen, poisoned or stunned 16% harder.
- **Giant Slayer** ×4: Hit mini-bosses, guardians and bosses 15% harder.
- **First Blood** ×3: Hit creatures at full health 15% harder.
- **Skyborne** ×3: Hit 15% harder while in the air.
- **Tidal Fury** ×2: Hit 20% harder while in water.
- **Barbed Hide** ×4: A creature that strikes you takes 15% of the blow back.
- **Recuperation** ×3: Healing you receive is 20% stronger.
- **Steady Footing** ×3: Blows shove you 25% less.
- **Cushioned Landing** ×2: Take 30% less from falls.
- **Insulation** ×2: Take 30% less from burning, poison and the like, working on you over time.
- **Potion Mastery** ×2: Potions heal 25% more.
- **Adrenaline** ×3: Run 15% faster for two seconds after a blow gets through.
- **Sprinter** ×3: After a second of running, go up to 12% faster.
- **Long Dash** ×3: Dashes and dodges are 15% faster, and so go further.
- **Scholar** ×5: Gain 15% more experience.
- **Forager** ×3: Creatures are 2% likelier to drop a heart.
- **Alchemist** ×2: Creatures are 1% likelier to drop a potion.
- **Key Ring** ×2: Carry one more key.
- **Windstep** ×3: Move 16% faster in the air.
- **Hardy** ×5: +8% max health.
- **Relentless** ×3: Attack 12% faster.
- **Prospector** ×2: Gold you find is worth 20% more.
- **Coin Magnet** ×3: Gold is picked up from 32 px further away.

## Conditional upgrades (4)

Generic, offered once something else is true.

- **Double Jump** *(rare)*: Jump once more in mid-air.
- **Air Dash** *(rare)*: Jumping in mid-air dashes in any direction you hold.
- **Deadly Aim** ×3: Your critical hits land 25% harder.
- **Sure Grip** ×2: Slide down a wall 35% slower.

## Techniques (1)

A way of getting about, unlocked once (milestones); their upgrades wait for them.

- **Magma Skin** *(rare)*: Swim in lava, and it burns you for only 30% as much.

## Bargains (risk-rewards) (32)

Something given, something taken; each once (vaults, chests).

- **Heavy Hand**: Your attacks come 34% slower, but hit 50% harder.
- **Hollow Bones**: Jump 20% higher, but swim 30% slower.
- **Gill-Touched**: Swim 50% faster, but run 20% slower.
- **Glass Edge**: Deal 20% more damage, but take 25% more.
- **Stoneskin**: Take 25% less damage, but deal 20% less.
- **Overcharged Staff**: Your staff holds one more firebolt (two more frostbolts), but you cast 15% slower. *[Elementalist]*
- **Drowned Lungs**: You never run out of breath, but all healing you receive is 40% weaker.
- **Long Stride**: Jump 20% lower, but move 40% faster through the air.
- **Anvil Grip**: Attack 20% slower, but hit 20% harder.
- **Quicksilver Grip**: Attack 20% faster, but hit 20% softer.
- **Amphibian Charm**: Move 35% faster in water, but 15% slower on land.
- **Assassin's Edge**: Hit creatures' backs 25% harder, but take 15% more from blows at yours.
- **Far Sight**: Hit far-off creatures up to 20% harder, and near ones as much softer.
- **Close Quarters**: Hit near creatures up to 20% harder, and far ones as much softer.
- **Ember Heart**: Your fire is 25% likelier to set creatures alight, but your alimus comes back 20% slower. *[Elementalist]*
- **Overflowing Vessel**: 50% more alimus to hold, but it comes back 25% slower. *[Elementalist]*
- **Bloodlust**: Hit 25% harder, but gather 30% less vital force. *[Vitalist]*
- **Siphon Crystal**: Gather 30% more vital force, but hit 25% softer. *[Vitalist]*
- **Deep Well**: Hold 50% more vital force, but heals cost 25% more of it. *[Vitalist]*
- **Earthshaker**: Your heaving swing hits 50% harder, but takes 40% longer to come back. *[Swordsman]*
- **Featherfoot Sash**: Your dash-slash comes back 30% sooner, but slashes 20% softer. *[Swordsman]*
- **Tower Plate**: Your shield is 30% stronger, but you move 10% slower. *[Warden]*
- **Second Skin**: Every form shrugs off 20% more of the blows it takes, but you take 15% more damage. *[ShapeShifter]*
- **Borrowed Fangs**: Every form's attacks hit 20% harder, but your staff hits 20% softer. *[ShapeShifter]*
- **Echoing Voice**: Form specials come back 20% sooner, but you take 10% more damage. *[ShapeShifter]*
- **Solar Crest**: What your ward bolt weakens is weakened 10% more, but your bolt hits 15% softer. *[Aegis]*
- **Bottled Sea**: Your bubble absorbs half of every blow, but bursts after absorbing 20% less. *[Aegis]*
- **Overfired Boiler**: Steam Release comes back twice as fast, but you take 10% more damage. *[Automaton]*
- **Dynamo**: Healing gives you 15% more of itself as fuel, but your cleaver hits 10% softer. *[Automaton]*
- **Ballast Tanks**: You take no shove at all, but you move 10% slower. *[Automaton]*
- **Loose Governor**: You swing 20% faster, but Self-Repair mends 30% slower. *[Automaton]*
- **Quickfingers**: Daggers are ready again in 60% of the time, but thrown ones hit 15% softer. *[Rogue]*

## Relics: Common (19)

Relic chests (three to choose from), hoards, the merchant. Rarity sets how often each is dealt (common 1.2, uncommon 1, rare 0.6, legendary 0.3).

- **Gaudy Charm** *(party)*: Creatures see you 20% closer than you are: you draw their eyes.
- **Dim Cloak** *(party)*: Creatures see you 20% farther than you are: they look to others first.
- **Trophy Hunter**: Every kill heals you 2 HP.
- **Hunger**: Every kill gives your reserve a little back (vital force, alimus, fuel, shield; a little health for the rest).
- **Bounty Hunter**: A kill has a 15% chance to drop two or three gold.
- **Wither**: A creature that dies burning or poisoned passes it on to those about it, at its full length again.
- **Tracking Mark**: A creature you hit is marked for 4 s: it takes 10% more from you.
- **Spillover**: A blow that kills spills 30% of its damage onto the nearest other creature.
- **Lucky Coin**: Gold you find is worth 30% more, and creatures are 1% likelier to drop a potion.
- **Fat Purse**: Every pile of gold holds 4 more coins.
- **Fence's Ledger**: Having the merchant's wares rerolled costs 40% less.
- **Guardian's Purse**: A fallen guardian leaves 20 gold.
- **Gorge**: Every kill gives you 4 vital force. *[Vitalist]*
- **Scrap Heap**: Every kill gives you 4 fuel. *[Automaton]*
- **Rime Heart**: Your frost is 12% likelier to freeze a creature solid, but your fire sets nothing alight. *[Elementalist]*
- **Burning Heart**: Your fire burns for double the damage, but your frost can't freeze creatures solid. *[Elementalist]*
- **Siphoning Bulwark**: A quarter of the damage you deal recharges your shield. *[Warden]*
- **Warbanner** *(party)*: Creatures see you 20% closer than you are, and you hit 10% harder. *[Warden]*
- **Hemorrhage**: A recall always makes its creature bleed. *[Rogue]*

## Relics: Uncommon (25)

- **Hunter's Map**: 20% more chests in every cave, but no chest of your own when a guardian falls.
- **Spring Water**: Each depth you advance heals you for 20% of your health.
- **Cinder Brand**: When a boss or mini-boss strikes you, there is a 20% chance it is set alight.
- **Hoarfrost Mail**: When a regular creature strikes you, there is a 15% chance it is frozen solid.
- **Riposte**: After you use the dodge button, your next blow within 3 s hits 50% harder.
- **Retribution**: When struck, your next blow within 4 s hits 30% harder.
- **Scavenger**: Opening a chest mends 10% of your health and gives 5 gold.
- **High Ground**: You hit creatures below you 20% harder.
- **Frostfire**: Fire hits frozen or chilled creatures, and frost hits burning ones, 30% harder.
- **Overstock**: The merchant has one additional item for sale.
- **Haggler's Tongue**: Everything the merchant sells costs 20% less.
- **Loaded Dice**: At every shop you may reroll once for free.
- **Adaptive Hide**: Returning to your own shape heals you for 15% of your health over 6 s. *[ShapeShifter]*
- **Overload**: While your alimus is nearly full, your fire and frost hit 15% harder. *[Elementalist]*
- **Last Rites** *(party)*: When you fall, every living ally is healed by 20.
- **Phoenix Ash** *(party)*: When a friend gets you back on your feet, you rise with 20% more health.
- **Healer's Hands** *(party)*: When you get a fallen friend back on their feet, they rise with 20% more health.
- **Fast Res** *(party)*: You raise a fallen friend 40% faster.
- **Just Kidding** *(party)*: When you have fallen, friends raise you 40% faster.
- **Blood Channeling**: A spell's recharge that lacks the alimus to begin is paid for with your own health instead. *[Elementalist]*
- **Lingering Fire**: Charged Strike burns for 5 s (every swing is charged) instead of one swing, but takes 60% longer to come back. *[Swordsman]*
- **Juggernaut's Charge**: Your Guarded Charge no longer stops for what it meets: it runs its whole length and strikes everything in its way. *[Warden]*
- **Martyr's Mantle** *(party)*: You carry 20% more of the blows of a friend under your Shared Burden. *[Aegis]*
- **Prism**: Your bolt's burst reaches 20% farther, and the creatures in it take 20% more of the blow. *[Aegis]*
- **Lone Blade**: You can only throw one dagger, but it hits twice as hard, and its recall 1.5x as hard. *[Rogue]*

## Relics: Rare (12)

- **Fount of Vigor**: Every level you gain heals you for 20% of your health.
- **Locksmith's Ring**: Every cave holds a second vault, and one more key to open it.
- **Deeper Dark**: One more level lies between here and the dragon.
- **Hasty Descent**: One level fewer before the dragon, which is 10% tougher for it.
- **Hawk's Pursuit**: Melee lunges are twice as strong and last longer (even for those who never lunged), and in the air a swing hangs you in place and pulls you after the creature in its way: you hover and hunt on the wing. But you hit 10% softer. *[Swordsman/Warden/Rogue/Automaton]*
- **Echoing Blade**: When your blade lands, there is a 20% chance it strikes again for half the damage, with everything a strike brings. *[Swordsman/Warden/Rogue/Automaton]*
- **Soul Lantern**: Creatures you kill explode for 10% of their health on those about them.
- **Hard Case**: No single blow can take more than 30% of your health.
- **Berserker's Pulse**: Below a quarter of your health you hit 35% harder and take 20% less.
- **Compound Interest**: The gold you carry grows by 10% (at least 1) each time you advance a depth.
- **Midas Mark**: You hit 1% harder for every 25 gold you carry (at most 100% more).
- **Undying Wound**: A recall's bleed never stops until the creature is dead. *[Rogue]*

## Relics: Legendary (1)

- **Death Ward**: Once a depth, a blow that would kill you leaves you at 1 health, untouchable for two seconds.

## Corrupted cards (4)

The Null's corrupted chests.

- **Overflow**: Deal three times the damage. Take three times the damage.
- **Bit Flip**: One blow in eight you land deals six times as much; one in eight deals nothing at all.
- **Duplication**: Every potion and key you pick up comes twice. Your maximum health is a third lower.
- **Null Pointer**: Once each depth, a blow that would kill you leaves you at 1 health and throws you clear. All healing you receive is halved.

## Class perks (bought with embers between runs; a few slots per hero)

**Swordsman**
- **Honed Edge** (2/3/4 embers): +5% damage. → +10% damage. → +15% damage.
- **Quick Wrists** (2/3/4 embers): Swings come 15% sooner. → Swings come 30% sooner. → Swings come 45% sooner.
- **Shake it Off** (3 embers): A heaving swing that strikes nothing is ready again in half the time.
- **Braced** (3 embers): While you heave, you take half damage.
- **Flow** (3 embers): Your combo chains one more strike from the start.

**Warden**
- **Steady Nerve** (3 embers): Perfect blocks are twice as forgiving.
- **Coyote Shield** (3 embers): Near misses still block: a wider arc, and a grace moment after you lower it.
- **Iron Skin** (2/3/4 embers): 10% less damage taken. → 20% less damage taken. → 30% less damage taken.
- **Stout** (2/3/4 embers): +10% health. → +20% health. → +30% health.

**Vitalist**
- **Sip of Life** (2/3/4 embers): 10% chance for your drain to heal you 1 HP. → 20% chance for your drain to heal you 1 HP. → 30% chance for your drain to heal you 1 HP.
- **Deep Reserves** (2/3/4 embers): +5 vital force. → +10 vital force. → +15 vital force.
- **Mending Ward** (2/3/4 embers): Those you've healed take 3% less damage for a while. → Those you've healed take 6% less damage for a while. → Those you've healed take 10% less damage for a while.
- **Gentle Hands** (2/3/4 embers): Your heal restores 10% more. → Your heal restores 20% more. → Your heal restores 30% more.
- **Ruinous** (2/3/4 embers): Rupture's burst hurts what's around it 25% more. → Rupture's burst hurts what's around it 50% more. → Rupture's burst hurts what's around it 75% more.

**Elementalist**
- **Deep Well** (2/3/4 embers): +5 alimus. → +10 alimus. → +15 alimus.
- **Attunement** (2/3/4 embers): Alimus comes back 10% faster. → Alimus comes back 20% faster. → Alimus comes back 30% faster.
- **Quickened** (2/3/4 embers): Bolts fly 5% faster in succession. → Bolts fly 10% faster in succession. → Bolts fly 15% faster in succession.
- **Kindred Elements** (2/3/4 embers): +5% chance to ignite or freeze. → +10% chance to ignite or freeze. → +15% chance to ignite or freeze.
- **Wide Weather** (2/3/4 embers): Your storms are 10% wider. → Your storms are 20% wider. → Your storms are 30% wider.

**Rogue**
- **Sharpened Steel** (2/3/4 embers): +5% damage. → +10% damage. → +15% damage.
- **Killer's Eye** (2/3/4 embers): +3% critical chance. → +6% critical chance. → +9% critical chance.
- **Quick Draw** (2/3/4 embers): Daggers can be thrown again 10% sooner. → Daggers can be thrown again 20% sooner. → Daggers can be thrown again 30% sooner.
- **Serrated Edges** (3 embers): A recall has a chance to make what it cuts bleed.
- **Long Shadows** (2/3/4 embers): Vanish lasts 1 s longer. → Vanish lasts 2 s longer. → Vanish lasts 3 s longer.

**Aegis**
- **Bulwark Oath** (3 embers): In a party, your second ability is Shared Burden (alone, a Humbling Light). It comes with her, equipped.
- **Brilliant Bolt** (2/3/4 embers): +5% damage. → +10% damage. → +15% damage.
- **Stronger Wards** (2/3/4 embers): Barriers and bubbles are 8% stronger. → Barriers and bubbles are 16% stronger. → Barriers and bubbles are 24% stronger.
- **Ready Hands** (2/3/4 embers): Barrier and bubble come back 10% sooner. → Barrier and bubble come back 20% sooner. → Barrier and bubble come back 30% sooner.
- **Stout Heart** (2/3/4 embers): What you carry for a friend weighs 15% less on you. → What you carry for a friend weighs 30% less on you. → What you carry for a friend weighs 45% less on you.
- **Steadfast** (2/3/4 embers): +10% health. → +20% health. → +30% health.

**ShapeShifter**
- **Borrowed Strength** (2/3/4 embers): +5% damage. → +10% damage. → +15% damage.
- **Adaptable** (2/3/4 embers): +10% health. → +20% health. → +30% health.
- **Quick Study** (2/3/4 embers): Shift comes back 10% sooner. → Shift comes back 20% sooner. → Shift comes back 30% sooner.
- **Layered Hide** (2/3/4 embers): Forms shrug off 4% more. → Forms shrug off 8% more. → Forms shrug off 12% more.
- **Long Sight** (2/3/4 embers): Copy creatures 15% farther away. → Copy creatures 30% farther away. → Copy creatures 45% farther away.

**Automaton**
- **Thick Plating** (2/3/4 embers): +10% health. → +20% health. → +30% health.
- **Ground Edge** (2/3/4 embers): +5% damage. → +10% damage. → +15% damage.
- **Capacitor** (2/3/4 embers): Hold 10 more fuel, and start with it. → Hold 20 more fuel, and start with it. → Hold 30 more fuel, and start with it.
- **Induction Coil** (2/3/4 embers): Healing gives 3% more of itself as fuel. → Healing gives 6% more of itself as fuel. → Healing gives 9% more of itself as fuel.
- **Quick Valve** (2/3/4 embers): Steam Release comes back 10% sooner. → Steam Release comes back 20% sooner. → Steam Release comes back 30% sooner.

## Meta trees (embers buy a rank; a resource found at a guardian activates it)

**THE POTION** (Reagents)
- **Immediate heal**: Quick Draught I (1): Potions heal 10% more at once (+10%). → Quick Draught II (2): Potions heal 10% more at once (+20%). → Quick Draught III (3): Potions heal 10% more at once (+30%).
- **Heal over time**: Lingering I (1): Potions heal 10% more over time (+10%). → Lingering II (2): Potions heal 10% more over time (+20%). → Lingering III (3): Potions heal 10% more over time (+30%).
- **Heal speed**: Strong Brew I (1): The heal over time takes 2 s less (13 s). → Strong Brew II (2): The heal over time takes 2 s less (11 s). → Strong Brew III (3): The heal over time takes 3 s less (8 s).
- **Drop rate**: Forager (1): Enemies drop potions 1% more often (2%). → Desperate Forager (2): Another +1% while you carry no potion.
- **Potion belt**: Second Flask (1): Carry up to 2 potions (a third only from a lucky find on the way: a Deep Flask).

**THE PATH** (Obsidian Pearls)
- **Milestones**: Waypoints I (1): A milestone pick every 7 levels. → Waypoints II (2): A milestone pick every 6 levels. → Waypoints III (3): A milestone pick every 5 levels.
- **Experience**: Studious I (1): +5% experience. → Studious II (2): +7% more experience (12%). → Studious III (3): +8% more experience (20%).
- **Elite experience**: Trophies I (1): +5% experience from elites. → Trophies II (2): +7% more from elites (12%). → Trophies III (3): +8% more from elites (20%).
- **Fair dealing**: Fair Dealing I (1): The merchant's prices are 5% lower. → Fair Dealing II (2): The merchant's prices are 10% lower. → Fair Dealing III (3): The merchant's prices are 15% lower.
- **Prospecting**: Prospecting I (1): Gold you find is worth 10% more. → Prospecting II (2): Gold you find is worth 20% more. → Prospecting III (3): Gold you find is worth 30% more.
- **Second thoughts**: Second Thoughts I (1): Once a run, reroll the merchant's wares or a choice of cards. → Second Thoughts II (2): Twice a run, reroll the merchant's wares or a choice of cards. → Second Thoughts III (3): Three times a run, reroll the merchant's wares or a choice of cards.
- **Trading**: Trader I (1): One more relic trade with a friend at the merchant's stand each run (2). → Trader II (2): Two more relic trades with a friend at the merchant's stand each run (3).


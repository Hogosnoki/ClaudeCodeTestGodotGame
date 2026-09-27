# Dagger Deep

A side-view rogue-lite in 3D: descend from a cave mouth to a dragon at the bottom of the world, as a
sword-swinging, dodging Swordsman, a shield-bearing Warden, or a Vitalist who drains life from
creatures and gives it to the living. You move on a 2D plane (left, right,
up, down), but everything you see is 3D: sculpted rock, lit water and lava, skinned and animated
creatures, and effects with real light (see **3D presentation** below). Each level is a biome with its own
cave generator, creatures and hazards. Slay the guardian of each level's exit, then choose one of
two ways down: a gentle one (one depth deeper) or a steep one (two deeper), each into a
different biome. Depth 10 is the dragon's lair. Between runs, embers and rare resources buy
permanent ranks in the upgrade trees.

Open `godot/project.godot` in Godot 4.4 (.NET build) and press Play. `Scenes/DaggerDeep.tscn` is
the project's main scene; the older map-editor and demo scenes are still in `Scenes/`.

## Running it at home

1. **Install the .NET edition of Godot 4.4.x.** On godotengine.org's download page, pick "Godot
   Engine - .NET". The standard edition can't run C# and will refuse to build the project.
2. **Install the .NET 8 SDK** (dotnet.microsoft.com, ".NET 8.0", SDK). Restart your computer if
   Godot later can't find it.
3. **Get the code.** The 3D version lives on the branch `claude/magical-wright-xux3l1` (the 2D
   placeholder it grew from is `claude/dagger-rogue-lite-cave-7snpg9`), not on `main`. Either:
   - On GitHub, switch the branch dropdown to it, then choose Code, then Download ZIP, and unzip it.
   - Or run `git clone <repo url>`, then `git checkout claude/magical-wright-xux3l1`.
4. **Import the project.** In Godot's Project Manager, click Import and select the file
   **`godot/project.godot`** (the `godot` folder, not the repository root). The first open takes
   a minute while Godot imports the sprite sheets.
5. **Press Play** (F5, or the triangle at the top right). Godot builds the C# code the first time.
   A controller plugged in before or during play works straight away.

The game uses Godot's Forward+ renderer, so it needs a graphics card with Vulkan support (any
desktop GPU from the last several years). The first level takes a few seconds longer to load
while its creatures are sculpted; later levels reuse them.

## Tuning

Almost every gameplay number is in **`Core/Tuning.cs`**, grouped by topic (`Tune.Hero`,
`Tune.Feel`, `Tune.Difficulty`, `Tune.Spawning`, `Tune.Drops`, one class per enemy,
`Tune.Boss`, `Tune.Cave`). Each has a short comment. To try a change: edit a value, save,
press Play; Godot rebuilds automatically. Upgrade amounts are in the table in
`Player/Upgrades.cs`.

Handy starting points:

| To change | Edit |
| --- | --- |
| Movement feel | `Hero.RunSpeed`, `Hero.Floatiness` (jump arc, same height), `Hero.JumpVelocity`, `Hero.CoyoteTime` |
| Survivability | `Hero.StartHp`, `Hero.HurtInvuln`, `Drops.HeartChance`, `Combat.*` (touch damage, recoil, combo) |
| Hit weight | `Feel.HitStop*`, `Feel.Kick*` |
| The three heroes | `Swordsman.*` (sword, dodge, Charged Strike), `Warden.*` (shortsword, shield, shield dash), `Vitalist.*` (drain bolt, alimus, heal, hex) |
| How many enemies attack at once | `Combat.AttackerShare` (a third of those ready, rounded up), `Combat.SlotRange` |
| Biome hazards | `Roots.*` (grasping roots), `CaveIn.*` (fossil graveyard ceilings), `Hero.Murky*` (rotting water), `Wraith.*` |
| Chests vs level-ups | `Drops.TreasureRoomChestChance`, `Drops.ZoneBias`; the stat list is `Upgrades.LevelUp` |
| How fast it gets hard | `Difficulty.DoublingMinutes`, `Difficulty.TempoCap` |
| How busy it is | `Spawning.IntervalStart`, `Spawning.RateDoublingMinutes`, `Spawning.IntervalMin`, `Spawning.ResidentFillStart`, `Spawning.CapBase` |
| Map size | `Cave.Width`, `Cave.Height`, `Cave.TunnelBudget` (keep these roughly in proportion) |
| Enemy brains | `Brains.*` (rewards, learning rate, when brains take over) and the `BrainLocks` flags |

## Training the enemy brains

Every creature has a tiny neural network that can drive it in place of its hand-written AI. Each
creature type shares one network. You train them by fighting them.

### Training loop

1. **Press F9 in game** (or launch with `-- --train`) to start training. A panel on the right
   lists each creature type's brain, and each creature shows its current move above its head.
   F8 hides those labels.
2. **Fight.** Every creature on screen learns from how the fight goes. Die, restart, repeat.
3. **Saving is automatic.** Brains are saved every 60 s, and also when you die, restart, go down a
   depth, turn training off (F9), or quit. F10 saves on the spot.
4. **Lock a creature type** once it's as strong as you want. Open `Core/Tuning.cs` and set its
   flag in `BrainLocks` to `true`, for example `public static bool Goblin = true;`. A locked
   brain still plays, but stops changing. Keep training the others.

### Where the brains are saved

When you run from the Godot editor, brains are saved into the project at
`DaggerCave/Brains/<type>.json`, so you can commit them to git. An exported build saves to
Godot's user data folder instead.

To ship trained brains inside an exported build, add `DaggerCave/Brains/*.json` to the export
preset's "Filters to export non-resource files".

To start a creature over, delete its `.json` file.

### Normal play (training off)

A type plays with its brain once the brain has at least `Tune.Brains.MinExperienceToPlay`
decisions of training (3000 by default). Until then, the original scripted AI plays that type,
so a half-trained brain never reaches normal play by accident. `Tune.Brains.Enabled = false`
switches every creature back to the scripted AI.

### What the network sees

There are 32 inputs, plus which move the creature made last:

| Category | Inputs |
| --- | --- |
| Where the player is | direction as x/distance and y/distance (instead of an angle), distance, a "closeness" value that is sharp at close range, raw x/y offsets |
| What the player is doing | velocity, HP share, in water, on the ground, swinging, guarding (dodging, invulnerable or shield raised), facing this creature, ability (Charged Strike, shield dash or heal) ready |
| Itself | velocity, HP share, on the ground, in water, line of sight to the player, how long it has been fighting, "recently hurt" and "recently landed a hit" traces, main attack readiness |
| Terrain toward the player | wall, gap, low ceiling |
| Allies | nearest ally's offset, how many allies are within 200 px |
| Difficulty | the difficulty tempo |

### What it chooses

The network picks a move about every 0.2 s. It doesn't pick while the creature is mid-attack,
airborne, and so on. Moves the creature can't make right now (an attack on cooldown, for
example) are masked out.

| Creature | Moves |
| --- | --- |
| Goblin | idle, approach, retreat, jump, club |
| Slinger | idle, approach, retreat, jump, throw |
| Frog | sit, hop toward, hop away, tongue |
| Spider | wait, toward, away, strike (drop on its thread from the ceiling, or pounce on the ground) |
| Magma Brute | stand, advance, retreat, lob |
| Golem | stand, advance, retreat, slam |
| Bat | hover, swoop, retreat, circle (roosting and waking stay scripted) |
| Fish | drift, approach, flee, dart, leap |
| Urchin | rest (hold its spikes in), bristle |
| Eel | lurk, lunge |
| Colossus | advance, back off, leap slam, roar, charge |

### How it learns

It learns by advantage actor-critic:
- The network has two hidden layers of 24 tanh units.
- It has one output per move, plus a "critic" output that estimates how well things are going.

Rewards:
- **Damage dealt to the player:** +1 per 10% of the player's max HP. This includes damage from
  its projectiles, puddles and shockwaves.
- **Damage taken:** -1 per 100% of its own max HP.
- **Time alive and fighting:** -0.01 per second.

All three amounts are in `Tune.Brains`. Because being killed costs at most the damage taken, a
bigger time penalty makes creatures more reckless. They learn that ending the fight early is
worth it.

Features that make it learn fast:
- **Teacher head start.** A fresh brain copies the scripted AI, and early on the scripted move is
  often executed outright. The teacher's share fades by half every 2500 decisions, so after a few
  sessions the rewards alone shape it.
- **Standard optimizer settings.** It uses the Adam optimizer with a fairly high learning rate
  (0.003), advantage normalization, gradient clipping and an entropy bonus, so it keeps trying new
  things.
- **Shared experience.** Every goblin on screen feeds the same goblin brain.

A decision costs about 5 microseconds. At most 12 decisions run per physics frame, and a creature
that doesn't get a slot waits a frame, so big crowds can't stall the game.

The panel's columns:

| Column | Shows |
| --- | --- |
| decisions | the brain's training experience |
| teacher | how often the scripted move is still being forced |
| reward | the average reward per decision (rising = getting better) |
| dealt | the average damage dealt to the player per decision |

## Controls

| Action | Keyboard / mouse | Controller |
| --- | --- | --- |
| Move | A / D (or arrows) | Left stick or D-pad |
| Swim up / down | W / S (Space also swims up) | Left stick up / down (A also swims up) |
| Jump | Space | A |
| Attack: swing (swordsman, warden) / drain bolt (vitalist) | Left click, toward the mouse (J attacks toward your held direction) | X, toward the right stick if held, otherwise the left stick |
| Ability: Charged Strike / shield dash / heal | Right click (or K) | RB or RT |
| Dodge roll / hold to raise the shield / hex | Shift (or L); the shield points at the mouse | B, LB or LT; the shield points along the right stick, or the way you face |
| Raise the Warden's shield without a button | - | Push the right stick: the shield rises by itself and points along it, even behind you while you run the other way |
| Go down an exit | E, or W / up, at the doorway | Left stick or D-pad up at the doorway |
| Drink a potion | Q | Y |
| Choose hero | Left / right on the title or death screen, or click a card | D-pad or stick left / right |
| Pause | Esc | Start |
| Pick upgrade | Click, 1 / 2 / 3, or arrows + Enter | D-pad or stick left / right, then A |
| Start / restart | Enter or click / R | A / Y |
| Enemy training (debug) | F9 on/off, F10 save now, F8 move labels | - |

Resizing or maximizing the window scales the whole picture up, keeping its 16:9 shape
(letterboxed if the window's shape differs).

You can switch between the two at any time. The game follows whichever device you used last: it
hides the mouse cursor, changes the on-screen button prompts, and turns rumble on for hits, kills
and damage taken while a controller is active.

## What's in it

**The run**. You start at the Cave Entrance (depth 0). Every level ends in a guardian's chamber
(marked EXIT on the minimap); killing the guardian drops a chest and opens the exits: stone
doorways onto passages that go down into the dark. Each shows which biome it leads to (a faint
breath of that biome's colour comes up from far below) and how deep: the gentle way goes one depth
down (one chevron on the keystone), the steep way two (two chevrons; harder, but fewer levels to
the dragon). Nobody is taken down until they choose to go: stand at the doorway and press E or
up, so an exit that opens under your feet can't whisk you away from the guardian's chest. The last levels lead into the Dragon's Lair at
depth 10; slaying the Elder Dragon wins the run. Dying (or winning) returns you to the camp screen.

**Biomes** (`Core/Biomes.cs`, generators in `Cave/CaveGenerator.cs` and `Cave/BiomeGen.cs`).
Each biome sets its generator and its parameters, palette, darkness, liquid, hazards, spawn tables,
mini-bosses and guardian:

| Depth | Biome | Cave | Creatures | Hazards / features | Guardian |
| --- | --- | --- | --- | --- | --- |
| 0 | Cave Entrance | one long winding corridor with side pockets; no darkness; few enemies | spiders, bats, rats | - | The Web-Mother (a weak ground spider that calls its brood) |
| 1-2 | Den | big round rooms in a line, almost no climbing; lots of elites; picked less often | rats, bears, goblins, bats | - | The Den Mother (bear) |
| 1-3 | Root-Choked Tunnels (the deep canopy) | branching tunnels through rock riven by massive tree roots coming down from the surface world; pools of murky water low down | rats, spiders, rot frogs, bats, mire fish, root eels | grasping roots that slow you and, if they hold you long enough, snag your weapon for a moment (two cuts clear them); thick, rotting water that leaves you gasping 50% sooner and swimming 20% slower | The Rotback (bear) |
| 1-3 | Nest | round rooms scattered up and down, joined by zig-zag tunnels that are always walkable; enemies and chests are in the rooms | scorpions, hornets, spiders | webs that slow you (cut them) | The Brood Queen (scorpion) |
| 2-3 | Ruins | right-angled halls and corridors on three floors, brickwork, shafts with stone slabs to climb, tall halls with floating slabs | skeletons, goblins, slingers, rats, scorpions | - | The Bone Knight (skeleton) |
| 3-4 | Fungal Cavern | wide, low-lying tunnels, little vertical variation | sporelings, frogs, mossback golems, spore hornets | spore pods that burst into choking clouds | The Mossback Hulk |
| 3-5 | Tunnels | slightly smaller map, tight flat or diagonal passages, water low down | rats, bears, bats, fish, eels, urchins | - | The Tunnel Brute (bear) |
| 4-6 | Slime Cavern | the original half-flooded cave | goblins, frogs, magma brutes, golems, bats, spiders, fish, eels, urchins | air vents | The Cavern Colossus |
| 5-7 | Frost Caverns | mostly horizontal, ice everywhere | frost bears, rime skeletons, frost wraiths, ice bats | slippery ground; a frozen water surface (break it to swim, and break it again from below to get out); ice ledges that shatter after 3 landings or 2 blows and refreeze | The Rime Colossus |
| 5-7 | Fossil Graveyards | a few vast, echoing chambers of layered sediment; in each, the ribcage of a leviathan arches from deep in the back to just in front of you, and a great skull is sunk in the wall | fossil skeletons, bone scorpions, ossuary golems, marrow rats, bats | unstable ceilings: walk beneath one and dust sifts down and the rock groans, then a few stones break loose (each one shows where it will land) | The Ossuary Colossus |
| 6-8 | Crystal Caves | ledges in every tall space, so no long falls | shardlings, crystal golems, skeletons | crystal spikes | The Prism Golem |
| 8-9 | Magma Caverns | more ledges, less climbing, lava instead of water | magma brutes, obsidian golems, ember scorpions, fire bats | lava (burns hard and throws you out), fire vents | The Molten Colossus |
| 10 | Dragon's Lair | an antechamber and one domed arena over a lava lake, with pits and tiers of ledges | - | lava | The Elder Dragon |

Every generator ends with the same pass. A movement-aware reachability check (walk, jump, fall and
swim over cells) looks for any spot you could fall into but not climb out of, fixes it with
stepping-stone ledges, and retries the seed if needed. The exit chamber must be reachable.

**The walker caves** (`CaveGenerator`) carve tunnels with branching "walkers" into a scalar
field. `CaveView` then runs marching squares over that field, which makes every wall an angled
slope rather than a block. The same iso-lines become the collision segments. Above the water,
walkers never climb or drop more steeply than about 34°, so every dry tunnel can be walked in
both directions. Any slope up to 56° is walkable (`Cave.WalkableSlopeDegrees`), and moss and
grass grow on exactly those slopes, so green ground is always ground you can walk on. Steep shafts
drop only into the flooded bottom, and every flooded system a shaft feeds gets a gentle beach plus
a connector tunnel back to the main network.

Tall open spaces get ledge staircases that zig-zag upward one jump at a time. They are dense low
down and thin out toward the roof (per biome: the crystal caves put ledges almost everywhere).
Dead ends become treasure rooms, a few become elite mini-boss lairs, and the dead end farthest from
the start becomes the exit chamber.

**The heroes** (`Player/`: `Player.cs` for what they share, `Player.Blade.cs` for the sword and
shortsword, and one file per hero). Pick one on the title screen, or switch on the death screen,
with left / right. They're meant to complement each other in a party (the game is single-player
for now, but the heal and the shield dash already work on every hero present): the Swordsman
deals the damage, the Warden takes it for others, the Vitalist keeps everyone alive.

| | Swordsman | Warden | Vitalist |
| --- | --- | --- | --- |
| Attack | Medium sword: 45 px reach, 20 damage per strike, swings 0.6 s apart with a short forward lunge, heavy knockback | Shortsword: 30 px reach, 13 damage, swings every 0.36 s with a fast sweep | Drain bolt: a single-target spell at medium range (175 px) that seeks the creature you aim at, 15 damage, every 0.5 s; a tenth of the damage comes back as alimus |
| Dodge button | Dodge roll on a short cooldown (0.55 s); a swing started mid-roll turns the roll into the strike | Hold to raise the shield (see below) | Hex: creatures around you (110 px) slow to 55% and take 20% more damage for 5 s; 6 s cooldown |
| Ability button | Charged Strike: the next swing does 50% more damage with 25% more reach, and whatever it cuts deals 20% less damage for 5 s; 12 s cooldown; instant, so it never breaks a combo | Shield dash: a guarded charge that stops at the first projectile or attacking creature it meets, swallowing the projectile and breaking the attack off; it passes by creatures that aren't attacking; 3.2 s cooldown | Heal: spends 12 alimus to restore 22 health, shared among everyone in range (420 px) who is hurt, by how hurt each is (each gets 22 x their share of missing health / the sum of those shares); 3 s cooldown |
| Movement | Full speed and jump | 92% speed, 90% jump height (80% speed while shielding) | 97% speed and jump |
| Toughness | 60 HP, 15 s of breath | 75 HP, 10% armour, 16 s of breath | 55 HP, 15 s of breath |

Warden's shield:
- **Blocking**: it's a broad arc of blue light (135°) that stops 70% of each blow arriving within
  it, melee or projectile; the other 30% gets through as chip damage (no flinch, no knockback).
  The shield loses half of what it stops. An ordinary block doesn't end the attack.
- **Aiming**: like a swing, it aims at the right stick, else the left stick on a controller, else
  the mouse. Pushing the right stick raises it by itself, so you can run one way and guard the
  other without holding a button. You can swing while it's up, but those swings don't combo.
- **Strength**: it holds 40 and regenerates 3 per second, starting 1 s after its last block.
- **Breaking**: once drained it breaks, stays at zero for 6 s, then regenerates from zero again.
- **Healing**: whenever the Warden is healed (a heart, a potion, the Vitalist's heal), the shield
  mends by half as much, and a broken shield is usable again at once.
- **Perfect block**: raising it at most 0.18 s before a hit stops all of the blow and breaks the
  attack off (the attacker reels). The shield dash does the same to whatever it meets.

The Vitalist's alimus is like mana: earned by dealing damage (a tenth of it), held up to 100, and
spent on heals. You start each level with at least 40.

All of these numbers are in `Tune.Swordsman`, `Tune.Warden` and `Tune.Vitalist`.

Movement has coyote time, jump buffering and variable jump height. Gravity is fairly floaty
(`Hero.Floatiness`, which keeps jump height the same) and fall speed is capped at 560 px/s.
Swimming has a breath meter; you take drowning damage when it runs out and the audio is muffled
while your head is underwater. Sparse air vents on the flooded cave floor release a big bubble
every 4-9 s. Swim into one for 2 s of air (`Hero.AirBubbleBreath`, `Hero.AirVents`). Water soaks up a quarter of your speed when you plunge in and
drags a little on swimming and sinking (`Hero.WaterDrag`, `Hero.WaterEntryDamp`).

After being struck you are invulnerable for 0.4 s. Drowning costs 3 HP plus 1% of max HP every
half second once your breath runs out. Healing is scarce:
hearts drop from 2% of regular kills and 40% of mini-bosses.

Enemies only hurt you with actual attacks, never by just touching you. The attacks are clubs,
tongues, rocks, slams and spikes, plus body attacks like a bat's swoop, a fish's dart, an eel's
lunge, a spider's drop or pounce, and the Colossus's charge and leap. Each body attack can hit you
at most once (`Combat.PassiveContactMult` brings back touch damage).

Landing a hit bounces the attacker back a little, horizontally only. That applies both to you
and to enemies (`Combat.StrikeRecoil`). Getting struck also pushes you sideways without
launching you upward.

**Combo**:
1. When a swing lands, your swing cooldown is refunded once, so you can strike again at once.
2. The next swing runs the full cooldown, and then a new combo can start.
3. Each Flurry upgrade adds one more refund to the chain, up to 3.
4. With Finisher, the last strike of a chain of three or more hits much harder.

**Growth during a run** (`Player/Upgrades.cs`):
- **Level-ups** are automatic. The Swordsman gains +2 max HP, +3% damage and +1.5% swing speed per
  level; the Warden +4 max HP, +1.5% damage, 1% damage reduction and +2 shield; the Vitalist +3
  max HP, +2% damage and +3 alimus capacity.
- **Milestones**: every 8 levels (fewer with the Path tree) you pick one of three +15% boosts:
  damage, attack speed, max health, damage reduction, run and swim speed, reach, and per hero
  faster dodges and Charged Strike (Wind Runner), a stronger shield (Aegis) and faster dash
  (Vanguard), or more alimus and stronger heals (Wellspring).
- **Leave it**: every chest and milestone screen also offers to take nothing. Each reward left
  behind pays 1 ember if you then kill that level's guardian.
- **Potions**: you start with one and can carry one (more with the Potion tree). Q / Y drinks it:
  15% of your health at once and 15% more over 20 s. Enemies drop one 1% of the time.
- **Hearts** heal 5% of your health and drop from 3% of kills (30% of elites).
- **Chests** hold the real upgrades and abilities.
  - 80% of treasure dead ends hold a chest (`Drops.TreasureRoomChestChance`). Mini-bosses and
    the boss always drop one.
  - Extra caches are scattered away from the dead ends: 6 on the flooded floor and 4 high in the
    dry caves (`Drops.WaterCaches`, `Drops.HighCaches`).
  - Where a chest is tilts its odds (`Drops.ZoneBias`). Movement upgrades are 4x likelier in
    underwater chests, which is how you gear up for the heights. Survival upgrades are 4x
    likelier in chests high in the cave.

| Area | Chest upgrades |
| --- | --- |
| Blade (swordsman, warden) | reach, Flurry (+1 strike to your combo, up to 3), Finisher (the last strike of a full combo hits much harder, requires Flurry), aerial down-slash pogo, knockback, stronger knockback, life steal |
| Sword techniques (swordsman) | Rending Edge (hits bleed for 40% more over 3 s, stacks twice), Crescent Wave (swings loose a flying slash, half damage, every 1.2 s), Executioner (+60% damage to enemies under 35% health) |
| Charged Strike (swordsman) | Focus (recharges 20% faster), Twin Charge (empowers two swings), Crippling Strike (what it cuts deals 35% less instead of 20%), Storm Edge (a charged swing looses a full-strength crescent wave) |
| Dodge (swordsman) | invulnerability while dodging, shorter cooldown, a second dodge charge |
| Shield (warden) | Tempered Shield (stops 10% more), Stalwart (full speed while shielding, no knockback), Quick Mend (regenerates almost at once after a block, 50% faster), Spiked Shield (melee attackers take 40% of the blow back), Last Stand (once per depth, survive a killing blow at 1 HP with a whole shield), Riposte Guard (perfect blocks, and the dash, reflect projectiles), Iron Timing (perfect blocks cost the shield 70% less), Tower Shield (wider arc) |
| Shield dash (warden) | Ready Charge (shorter cooldown), Long Charge (30% farther), Shield Bash (hits what it stops four times as hard), Rallying Charge (breaking an attack mends the shield by 12 and heals 4) |
| Spells (vitalist) | Splitting Bolt (bolts leap to a second creature), Far Reach (+25% bolt range), Hungering Spirit (+50% alimus from damage), Deep Well (+40 alimus capacity), Deep Mending (+30% heal), Frugal Rites (heals cost 25% less), Spreading Blight (+30% hex radius), Lingering Hex (+2 s), Withering Hex (hexed creatures rot for 6 health a second) |
| Movement (everyone) | wall jump, double jump *or* air dash in any direction (you can only have one), move speed, jump height, swim speed, breath |
| Everyone | attack speed, damage; max HP, longer invulnerability after being struck (Resilience), damage reduction, heal on kill, XP magnet |

**Enemies** (`Enemies/`). They follow some etiquette (`Tune.Combat`):
- The first time a creature wants to attack, it waits 1 s (`FirstAttackDelay`), so nothing
  strikes the instant it drops into view.
- Creatures near you share out attack slots: of those within 560 px that are ready to attack, a
  third (rounded up, at least one) may be attacking at once (`AttackerShare`). A crowd you've
  gathered doesn't queue politely behind one attacker any more. Bosses and guardians are exempt.
- Attack cooldowns are longer, frogs only use their tongue with their feet planted, and fish
  dart more slowly.

Each one moves differently:

| Enemy | Behavior |
| --- | --- |
| Bat | Roosts on the ceiling, then swoops at you on a wobbling path and retreats. |
| Frog | Hops in arcs and lashes its tongue from range. It can also swim. |
| Goblin | Runs at you, hops gaps and clubs you. The slinger variant keeps its distance and lobs rocks. |
| Spider | Crawls along the ceiling and drops on a silk thread when you pass underneath. If you cut the thread, it pounces along the ground. |
| Magma Brute | Leaves burning puddles and lobs lava globs. Water boils it away. |
| Golem | Slow and almost impossible to knock back. It telegraphs a ground slam that sends shockwaves along the floor, which you can jump over. |
| Cave Fish | Darts at you in water. It also leaps out at you on shore, then flops around on land. |
| Urchin | Stationary on the seabed and pulses its spikes outward. Common: they often sit beneath schools of fish. |
| Eel | Hides in a wall burrow and can't be hurt there. It lunges along a line at swimmers. |
| Rat | Runs in packs, crouches for a quarter second and lunges with a bite. |
| Bear | Rears up for a heavy swipe, or roars and charges, stunning itself if it hits a wall. |
| Scorpion | Scuttles close, arches its tail and stings forward and up. |
| Hornet | Hovers above you, takes aim with a buzz, then dives in a straight line. |
| Skeleton | Plods forward and slashes. Sometimes a felled skeleton pulls itself back together at half health. |
| Sporeling | Waddles close and puffs a choking spore cloud; bursts into one when killed. |
| Frost Wraith | A haunting more than a hunter: it drifts at the edge of the light, now and then looms in over you with a shriek (arms flung wide, jaw open, eyes blazing, but doing no harm), and only rarely, after a long and obvious wind-up, looses a single ice shard (an elite, a fan of three). Frail once you reach it. |
| Shardling | Curls up and bursts in a spray of crystal shards; shatters into more when killed. |
| Cavern Colossus | Leap slams, a roar that brings stalactites down, and a wall charge that leaves it stunned. It enrages at half health and summons bats. |
| Elder Dragon | Stalks the arena floor, sweeps a cone of fire, flies up and dives with a ground-shaking landing, lashes its tail, and roars down a rain of fire. It enrages at half health and calls fire bats. |

Biomes also field tinted variants (frost bears, rime skeletons, ember scorpions, obsidian golems,
fire bats...). Mini-bosses are elite versions and drop a chest; guardians are elites with extra
health and a title.

**Between runs: embers, resources and the upgrade trees** (`Core/Meta.cs`, `UI/MetaMenu.cs`).
Progress saved in `user://meta.json`:
- **Embers** come from guardians (1, 2 from depth 5, 5 for the dragon) plus 1 per reward you left
  behind on that level. Embers buy the ranks of the trees.
- **Resources**: each guardian yields one, drawn evenly from every resource not yet found (13
  Reagents and 9 Obsidian Pearls in all, so each is a 1-in-22 chance at first). A bought rank only
  takes effect once you spend a resource of its tree on it. A tree stays hidden until you find its
  first resource.
- **The Potion tree** (Reagents): immediate heal +5% x3, heal over time +5% x3, heal-over-time
  3 s / 3 s / 4 s faster, drop rate +1% (and +1% more while you carry none), and two more flasks.
  Back at camp after your first Reagent, a short introduction gives one first rank free and asks
  you to spend the Reagent on it (both skippable).
- **The Path** (Obsidian Pearls): milestones every 7, 6 then 5 levels; +5 / +7 / +8% experience;
  the same for elites.

Open the trees with U (controller: BACK) on the title or camp screen.

**Spawning and difficulty**. Enemies come from two sources:
- **Residents** sit at points scattered through the tunnels. They are only ever created while out
  of view, so they are already there when you arrive. Early in a run most points stay empty
  (18% filled). They fill up over 16 minutes and respawn slowly, so exploring stirs up fewer
  enemies than time does.
- **Entrances** arrive in waves on a timer. The first wave comes at 25 s with one enemy. The gap
  between waves halves every 8 minutes (`Spawning.RateDoublingMinutes`), down to 5 s, and waves
  grow with time.
  - Each newcomer gets its own entry point from a different direction, in a band just outside
    the screen edges, and every entry point is reachable along the tunnels.
  - What arrives suits the spot: fish in water, bats where there's no floor, walkers on the
    ground. Each comes to you its own way: bats fly in, frogs hop, goblins run, fish swim.

Enemies move 20% less far than their raw numbers say (`Difficulty.EnemyMoveScale`). That
scales their speed, gravity and jump height together, while aimed leaps (the fish's shore
ambush, the Colossus's slam) still land where they aim. Magma puddles burn for 3, and blue fish
bite softer than orange ones.

Difficulty rises with depth and, more slowly, with play time: enemy health and damage are
1.13^depth x 2^(minutes/30) (`Tune.Difficulty`). Enemy speed, attack rate and animation speed rise
by a fifth of that, capped at 1.6x so fights stay readable. Spawn intensity grows with both too.

**Sprites and the animation clock** (`Art/`, generated by `tools/sprites/`). The 2D game this grew
from drew every character as a sprite-sheet animation played at 24 fps. The sheets are still
loaded and played, invisibly: their clips, frames and timing are the clock the 3D models animate
to, so every attack's wind-up, strike and hitbox lines up exactly as before. The sets:

| Group | Characters |
| --- | --- |
| Heroes | the Swordsman, the Warden and the Vitalist |
| Enemies | every enemy type |
| Boss | the Cavern Colossus |
| Passive critters | glow moths and cave crabs (new ambient wildlife) |

The game has no allies yet. Each character has separate right-facing and left-facing clips, plus
turn-around clips that rotate it through a front-facing view.

The left-facing sprites are real renders, not mirrored copies. That means the blade stays in the
hero's right hand (the far hand when facing right, the near hand when facing left), and the light
always comes from the upper left.

All three heroes are drawn by the same rig (`tools/sprites/player.py`) with a style switch:
- The Swordsman: a teal cloak, a red scarf and a medium sword.
- The Warden: a blue tabard, a gold sash, a shortsword and a buckler.
- The Vitalist: a green robe, a bone mask with glowing eyes and a crystal-headed staff.

They share every clip:
- idle
- run, run start and skid stop
- turn
- jump start, rise, apex, fall and land
- wall slide
- swim and tread water
- dodge roll
- air dash
- 15 slashes (the blade-bearers): three combo strikes aimed in five directions (up, up-forward,
  forward, down-forward and down)
- the Warden's shield bash (the dash); the Vitalist's cast, hex and heal
- hurt
- death

Enemies have the equivalents: wake-ups, hop wind-ups, attack wind-up / strike / recover, hurt and
death. A death clip keeps playing after the creature is removed.

The sheets come from a small 2.5D vector rig renderer (Python + cairo). To regenerate them, run
`pip install pycairo`, then `python3 tools/sprites/build.py [names] [--contact DIR]`. The
`--contact DIR` option also writes a preview sheet per character.

**Swings** have three beats, drawn into the sprites and matched by the hitbox:
1. **Wind-up** (2 frames, 0.12 s for the sword and 0.06 s for the shortsword): the blade pulls back.
2. **Woosh** (2 frames, when the blade actually hits): the blade becomes a translucent motion arc,
   and the smear drawn in-game follows your real reach and aim.
3. **Follow-through** (3 frames): the blade overshoots and holds for a beat, then recovers.

The finisher gets one more frame of wind-up and follow-through. The speeds are
`Swordsman.SwingWindup` / `SwingTime` and the `Warden` equivalents.

**Squash and stretch**. Every character sits on a springy scale that overshoots a little:
- wind-ups coil (squash) for as long as they play;
- strikes, leaps and jumps pop into a stretch;
- landings and hurts squash;
- fast movement stretches along the motion.

**Game feel**. Hits have several layers:
- Freeze-frames scaled to the hit: 0.17 s, 0.23 s on kills, 0.3 s on finishers, 0.32 s for a
  Charged Strike. Only the hero and the creature trading the blow freeze, on the very pose of the
  impact (with a small shudder); the rest of the cave carries on. A press made during the freeze
  isn't lost: the next swing of a combo starts the instant it ends. Mini-boss and boss kills still
  get a moment of whole-game slow motion.
- A quick flash (brief enough that the frozen pose stays readable), an elastic squash on the
  creature that was hit, and an impact spark.
- A camera kick in the direction of the blow, plus controller rumble.
- Damage numbers that pop in.
- A swing smear that is brightest at the blade's leading edge, with a golden echo on finishers.
- Afterimage trails on dodges and air dashes.

**Presentation**: see **3D presentation** below.

All sound effects and both music loops (ambient exploration and boss) are synthesized at startup
(`Audio/`). Besides the sprite sheets in `Art/`, the only asset files are the rock and ground
textures in `Assets/` (public domain, from Poly Haven; see `Assets/CREDITS.md`). Every 3D model
is built by code when the game runs.

## 3D presentation

The simulation is unchanged from the 2D game: every creature, projectile and hazard is still a 2D
node in pixel units, colliding with the marching-squares cave. That 2D world is hidden, and
`Render3D/` draws it in 3D. 16 px are one metre; the play plane is z = 0, and the camera looks at it
from the front, framing exactly what the gameplay camera frames (so "off screen" still means off
screen for spawning). F7 shows the 2D world on top, for checking collisions against the art.

- **Cave** (`Render3D/Terrain/`): a signed distance field built from the cave's own collision
  outline becomes rock with depth: a back wall, rounded ledges, and a front cut that bulges toward
  you, with noise relief away from the play plane so the walkable edge is exactly where the
  collision is. It is meshed with surface nets in parallel chunks and shaded with a triplanar PBR
  rock shader: textures recoloured per biome, moss on walkable floors, masonry in the Ruins, snow
  and ice in the Frost Caverns, magma veins, crystal flecks, glowing fungus, wet rock and caustics
  under water, root mats, and sediment layers with bones caught in them. Stalactites, boulders,
  crystals, mushrooms, grass, roots, icicles and glow threads decorate it (`Render3D/Decor/`),
  and their glows light the cave through a pool of real lights. The root-choked tunnels get massive
  bark-fluted roots coming down through the ceilings; the fossil graveyards a leviathan's ribcage
  across each great chamber, skulls sunk in the walls and old bones underfoot.
- **Water and lava** (`Render3D/Liquid3D.cs`): a refracting, depth-absorbing water surface with
  an animated waterline and volumetric fog under water; emissive lava that lights the cave, with a
  heat haze above it.
- **Lighting**: AgX tonemapping with a contrast curve on top, SSAO, glow, thin volumetric fog, a
  light per biome, the hero's lantern, back walls kept darker than the play layer, and a key and
  rim light of their own for every creature, so they read against the rock however dark the cave
  gets.
- **Ink outlines**: every creature and hero is drawn a second time as a slightly swollen hull
  whose pixels run a Sobel filter over the scene's depth and normals (`creature_ink.gdshader`),
  inking wherever either jumps: its silhouette and its strongest creases, about two pixels wide at
  any distance.
- **Creatures** (`Render3D/Creatures/`): each creature type is sculpted once from code
  (`Designs/`): signed-distance primitives (limbs, eggs, blocks, carved sockets) are
  smooth-unioned on a skeleton and meshed, then teeth, claws, horns, eyes, membranes and weapons
  are added as explicit parts. Every vertex is skinned to the nearby bones. The designs animate
  procedurally from the sprite clip, its progress and the creature's state (gait from speed,
  springs on capes and tails, aim for the frog's tongue and the dragon's fire). Types a level can
  spawn are sculpted in parallel while it loads, everything else in the background.
- **Effects** (`Render3D/Fx/`): the effect calls are the same as before (`G.Fx.Spark`,
  `Explosion`, `Smoke`...), simulated by `FxLayer`, but each particle now has a depth of its own and
  is drawn as 3D: additive HDR light for sparks, flashes, embers, rings and swooshes (they bloom),
  lit smoke and dust, tumbling rock debris, glassy bubbles, and brief real lights at every flash.
  The blade smear is a glowing ribbon with a light at its edge; dodges leave glowing afterimages of
  the pose. Damage numbers, elite health bars and screen washes are drawn over the 3D view.
- **Props** (`Render3D/Props/`): every projectile, pickup, chest, exit and hazard gets a 3D body
  that follows its gameplay node and reads its state (the chest's lid, the vent's eruption, the
  ice's cracks, grasping roots writhing as they close, loose stones shaking before a cave-in). An
  exit is a dressed-stone doorway in a knuckle of rock; its opening is one quad whose shader traces
  each view ray into a passage behind it (`fx_stairwell.gdshader`): stone ribs, each set lower than
  the last, stepping down into the dark, and past them a faint glow of the biome below.
- **Menus**: the three hero cards on the title and death screens show the heroes' 3D models idling
  under studio lights, each in a small viewport of its own (`UI/HeroPortrait.cs`).

Quitting (closing the window, or a test finishing) goes through `Core/SafeQuit.cs`, which first
lets the renderer's queued background shader compiles drain: Godot 4.4 can otherwise hang at exit
while any are still waiting.

To look at the models without playing, `--modelsheet=goblin,bear --shots=DIR` lines each type
up in its clips under studio lights and saves `DIR/sheet_NAME.png` (`--sheetclips=idle:0.3,run:0.6`
picks the clips and moments, `--sheetyaw=DEG` the angle).

## Test harness

The harness takes command-line user args after `--`:

```
# 12 seeds per biome: generation time, retries, trap cells (must be 0), a reachable exit;
# saves a map image of each biome (add --biome=NAME for just one)
godot --headless --path godot -- --gentest

# The autopilot bot plays for 60 s and saves a screenshot every 3 s
xvfb-run godot --path godot --rendering-driver vulkan -- --autotest --seed=1013 --duration=60 --shots=/tmp/shots

# Spawns one of every creature and screenshots it (on land, underwater, then the boss)
xvfb-run godot --path godot --rendering-driver vulkan -- --seed=1013 --bestiary --shots=/tmp/shots

# One screenshot after N frames (with --fixed-fps for a steady clock); --fxtest=K lays out one of
# every effect K frames before the shot, --proptest one of every prop, --exittest the two exits
# a guardian leaves (one right where you stand)
xvfb-run godot --path godot --rendering-driver vulkan --fixed-fps 12 -- --biome=entrance --lookshot=/tmp/look.png --frames=60 --proptest
```

The 3D view needs the Vulkan driver (on a machine without a GPU, Mesa's lavapipe works, slowly).
Debug views: `--cam3d=YAW,PITCH,DIST` orbits the camera around the action, `--bright` floods the
cave with light, `--nofog` turns the volumetric fog off, `--terraindebug` colours the rock by what
the shader thinks it is, and `--terraindebug=weights` by its texture projections (red side-on,
green top-down, blue front-on).

`--start=boss` and `--start=water` change where the player spawns, `--seed=N` fixes the cave, and
`--biome=NAME` (entrance, den, roots, nest, ruins, fungal, tunnels, slime, frost, fossils, crystal,
magma, lair)
starts in that biome at its depth. `--fullrun` with `--autotest` gives the bot a very sturdy hero
and has it head for each guardian and then the steep exit, all the way to the dragon; it prints
`[fullrun] VICTORY` and quits when the dragon dies.

To record gameplay, `--showcase` runs a directed demo (a land fight, then underwater, then the
boss). Combine it with Godot's movie writer, which renders frame by frame at a fixed rate and
captures the audio:

```
xvfb-run godot --path godot --rendering-driver vulkan --write-movie /tmp/v/f.png --fixed-fps 30 -- --showcase --seed=1013 --duration=31
```

To check the neural network's maths (a gradient check, learning a simple task from rewards, and
a save/load round trip), run:

```
godot --headless --path godot -- --nntest
```

To have the autopilot bot train the brains, add `--train` to an `--autotest` run. Add
`--braindir=DIR` to keep those brains out of the project.

`--herotest` (add `--hero=warden` or `--hero=vitalist`) scripts checks of each hero's mechanics
and prints ok / FAIL for each:
- Swordsman: the sword's reach and lunge; a combo pressed during a hit-stop starting as it ends;
  the Charged Strike (harder, and weakening what it cuts); a swing out of a dodge; the crescent
  wave; air bubbles.
- Warden: the shield stopping 70% from the front (and losing half of that) but nothing from
  behind; breaking, staying down, and mending at once when healed; perfect blocks reflecting a
  shot and breaking off a melee attack while ordinary blocks don't; swinging behind the shield;
  the right stick raising it by itself; the shield dash swallowing a projectile, breaking off an
  attack it meets, and passing a creature that isn't attacking.
- Vitalist: the drain bolt landing and paying back a tenth as alimus; the hex, and hexed
  creatures taking more damage; the heal's cost and amount; no heal when no one is hurt.
- For all: the game clock never slowing for a hit-stop.

`--hitstoptest --shots=DIR` has the hero strike a golem twice and logs every frame of it (who is
frozen, for how long, the clip and frame), saving a screenshot of each.

The checks are timed in game seconds, so on a slow software renderer run them with
`--fixed-fps 60` (and a small window, `--resolution 640x360`, to save time): otherwise frames of a
third of a second let the game's timers run ahead of the script.

`--hero=warden` and `--hero=vitalist` also work with `--autotest` and the other modes.

Two more test modes:
- `--animtest --shots=DIR` scripts the player through every movement and attack transition (run,
  turn, stop, jump, land, slashes, dodge, ability, hurt) and saves a frame every 1/20 s.
- `--padtest` drives the game with synthetic controller events: start, move, swing, Charged
  Strike, dodge, pick an upgrade from the level-up cards, pause and unpause. Its presses are a
  twentieth of a second long, so on a slow software renderer run it with `--fixed-fps 60` too. It prints what happened at each
  step.

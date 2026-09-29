# Dagger Deep

A side-view rogue-lite in 3D: set off from a camp in a sunny meadow, down through the cave mouth to
a dragon at the bottom of the world, as a sword-swinging, dodging Swordsman, a shield-bearing
Warden, or a Vitalist who drains life from creatures and gives it to the living. You move on a 2D plane (left, right,
up, down), but everything you see is 3D: sculpted rock, lit water and lava, skinned and animated
creatures, and effects with real light (see **3D presentation** below). Each level is a biome with its own
cave generator, creatures and hazards. Slay the guardian of each level's exit, then choose one of
two ways down: a gentle one (one depth deeper) or a steep one (two deeper), each into a
different biome. Depth 10 is the dragon's lair. Every run begins and ends at the camp outside the cave,
where the heroes sit round a cooking fire; between runs, embers and rare resources buy permanent
ranks in the upgrade trees. Up to three friends can play together online, one of each
hero (see **Playing with friends online**).

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

## Getting a copy to play (and to send a friend)

You don't need Godot to play: every push to GitHub builds the game for Windows and Linux
(`.github/workflows/build-game.yml`).

1. On the repository's GitHub page, open the **Actions** tab and click the latest
   **Build the game** run (a green tick means it worked).
2. At the bottom, under **Artifacts**, download **DaggerDeep-Windows** (or **DaggerDeep-Linux**).
   It's a zip of the whole game folder.
3. Unzip it anywhere and run `DaggerDeep.exe` (Windows may warn that it "protected your PC",
   because the game isn't signed: click More info, then Run anyway).
4. To play together, send your friend the same zip. `HOW-TO-PLAY.txt` inside it explains
   hosting and joining.

Artifacts are kept for 30 days. For a permanent download link, push a version tag
(`git tag v1.0 && git push origin v1.0`): the same build then appears as a GitHub release with
both zips attached. Both players need the same build: the game refuses to connect two
different versions of its network protocol (`Net.Version`).

To build it yourself instead: in Godot, install the export templates (Editor, Manage Export
Templates, Download and Install), then Project, Export, pick **Windows** or **Linux**, and
Export Project. From a command line:
`godot --headless --path godot --export-release "Windows" ../build/DaggerDeep-Windows/DaggerDeep.exe`.
Keep the exported files together: the executable, `DaggerDeep.pck` and the `data_` folder.

## Playing with friends online

Up to three players, one of each hero. One player hosts; the others join with the host's code.

1. On the main menu, choose **Multiplayer (online)** (or press O, or Y on a controller).
2. The host clicks **Host a game**. The lobby shows a join code like `7K3QD-M2XP9`, with a
   Copy button: send it to your friend. (If your computer is on a home network, a second code
   for players on the same Wi-Fi shows too.)
3. The friend pastes (or types) the code and clicks **Join**. A plain address works too
   (`192.168.1.20`, `100.101.102.103`, `my.host.name`, with `:port` if needed).
4. In the lobby, each player takes a different hero (the one you last chose at the camp fire,
   if it's free). The host clicks **Start the descent** and everyone begins together.

In the cave:
- **Going down**: when a guardian falls, everyone still standing has to walk into the same exit
  (interact, E or LT, or up at the doorway). The first to arrive waits there ("Waiting at the exit: 1 of 2
  here"); walking away cancels.
- **Falling and reviving**: a fallen hero stays down. A friend brings them back by standing
  beside them and holding interact (E, or LT on a controller; not up, so it works while you
  tread water beside them) for two seconds; they get up with a
  third of their health. If everyone is down at once, the run is over for the whole party. A
  fallen hero who is still down when the others go down an exit comes along, back on their feet.
- **Sharing**: experience is shared (everyone gains what anyone collects), each player levels
  up and picks their own upgrades. A chest's cards are dealt for the whole party, and everyone
  sees the same ones: look in (interact at the chest), and take a card or leave it closed for
  someone else (tell them what's in it). A card for another hero is shown but only they can take
  it, one player looks in a chest at a time, and taking a card spends it for everyone. Hearts
  and potions go to whoever touches them. Kills count for whoever landed the blow.
- **Harder caves**: creatures have 50% more health for each extra player, and the cave holds
  35% more of them.
- **Pausing** opens the menu but doesn't stop the game (the others are still playing), and
  picking an upgrade keeps you safe while you choose. Leaving from the pause menu takes you
  back to the main menu; if the host leaves, the game ends for everyone.
- **Walking out**: at depth 0, the tunnel you came in by leads back out to the daylight. When
  everyone still standing waits there (interact at the bright end), you all walk out together:
  the run ends on the spot, keeping nothing from it, and you're back in the lobby.
- **Guardians**: one woken by a friend on the far side of the cave doesn't pull your view away
  from your own hero; a notice says so, and its chamber is marked EXIT on your map.
- After a run the camp screen shows how it went, and Enter (A) goes back to the lobby, still
  together, for the next descent.

The HUD shows your friends' names and health under your own bars, their names over their heroes,
and their dots on the minimap.

**If the friend can't connect.** The join code is the host's internet address and port
(24890, UDP) in ten letters. The host's game asks the router to let players in (UPnP), and the
lobby says whether it worked. If it didn't:
- Players on the same network can use the second (local network) code.
- Otherwise, forward UDP port 24890 to the host's computer in the router's settings, or have
  everyone install a free virtual LAN such as Tailscale or ZeroTier and type the host's address
  from it in place of the code.
- Windows asks the host, the first time, whether to let the game through the firewall: allow it.

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
| The three heroes | `Swordsman.*` (sword, dodge, Charged Strike, heaving swing), `Warden.*` (shortsword, shield, Guarded Charge, shield bash), `Vitalist.*` (drain, alimus, heal, hex, rupture) |
| How many enemies attack at once | `Combat.AttackerShare` (a third of those ready, rounded up), `Combat.SlotRange` |
| Biome hazards | `Roots.*` (grasping roots), `CaveIn.*` (fossil graveyard ceilings), `Hero.Murky*` (rotting water), `Wraith.*` |
| Chests vs level-ups | `Drops.TreasureRoomChestChance`, `Drops.ZoneBias`; the cards are `Upgrades.Chest`, the level-up stats `Progression.AutoLevel` |
| How fast it gets hard | `Difficulty.DepthGrowth`, `Difficulty.DoublingMinutes` (tougher creatures), `Difficulty.PacePerDepth`, `Difficulty.PaceMinutesPerStep` (more of them), `Difficulty.TempoCap` |
| How busy it is | `Spawning.IntervalStart`, `Spawning.RateDoublingMinutes`, `Spawning.IntervalMin`, `Spawning.ResidentFillStart`, `Spawning.CapBase` |
| Map size | each biome's `W` and `H` in `Core/Biomes.cs`, then `Cave.WidthScale` (1.5: every level is half again as wide, the same height; the dragon's arena keeps its size) |
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
| What the player is doing | velocity, HP share, in water, on the ground, swinging, guarding (dodging, invulnerable or shield raised), facing this creature, ability (Charged Strike, Guarded Charge or heal) ready |
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
| Attack: swing (swordsman, warden) / drain (vitalist); hold it down to keep attacking | Left click, toward the mouse (J attacks toward your held direction) | X, toward the right stick if held, otherwise the left stick |
| Attack where you point (swordsman, vitalist) | - | Push the right stick well over: it attacks that way, again and again while it's held |
| Ability: Charged Strike / Guarded Charge / heal | Right click (or K) | RB |
| Second ability: heaving swing / shield bash / rupture | F (or middle click, or I) | RT |
| Dodge roll / hold to raise the shield / hex | Shift (or L); the shield points at the mouse | B or LB; the shield points along the right stick, or the way you face |
| Raise the Warden's shield without a button | - | Push the right stick: the shield rises by itself and points along it, even behind you while you run the other way |
| Interact: open a chest, go down an exit, bring a friend back | E | LT |
| Open a chest | E beside it (walking into it doesn't open it) | LT |
| Go down an exit | E, or W / up, at the doorway | LT, or the stick / D-pad up, at the doorway |
| Bring a fallen friend back (online) | Hold E beside them | Hold LT beside them (both thumbs stay free to swim) |
| Leave the cave (depth 0: ends the run, keeping nothing) | E at the daylight at the far left | LT |
| Drink a potion | Q | Y |
| Main menu | Up / down and Enter, or click | D-pad or stick up / down, then A |
| Choose hero (at the camp fire) | Left / right, or click the arrows | D-pad or stick left / right |
| Descend into the cave | Enter (or R), or click Descend into the cave | A |
| Back to the main menu (from the fire) | Esc | B |
| Play online | Multiplayer on the main menu (or O) | Y |
| Pause (settings, your build) | Esc | Start |
| Your build, while picking a card | Tab | Back |
| Pick upgrade (a card marked for another hero, or one you can't take, stays put) | Click, 1 / 2 / 3, or arrows + Enter | D-pad or stick left / right, then A |
| Enemy training (debug) | F9 on/off, F10 save now, F8 move labels | - |

Every action except the debug keys can be rebound, for keyboard and mouse and for the controller
separately (up to three inputs each): pause, then **Settings**, **Controls**.

Resizing or maximizing the window scales the whole picture up, keeping its 16:9 shape
(letterboxed if the window's shape differs).

**Settings** (pause, then Settings; or Settings on the main menu). Everything takes
effect at once and is kept in `user://settings.cfg`:
- **Graphics**: window mode (windowed, borderless, fullscreen), vsync, a frame-rate cap, render
  scale (with FSR upscaling), shadows, volumetric fog, bloom, ambient occlusion, ink outlines,
  anti-aliasing, brightness and screen shake.
- **Sound**: master, music and effects volumes.
- **Controls**: how keyboard presses aim (at the mouse, where you move, or automatically),
  vibration, and rebinding every action.

You can switch between the two at any time. The game follows whichever device you used last: it
hides the mouse cursor, changes the on-screen button prompts, and turns rumble on for hits, kills
and damage taken while a controller is active.

## What's in it

**The camp**. The game opens on the main menu, over the camp outside the cave: a green meadow
under a blue sky, a cooking pot over the fire, and the cave's great dark mouth in a mossy cliff.
**Single player** brings you in to the fire, where the three heroes sit on logs; choose one (the
chosen hero stands up) and descend, and the view drifts into the cave mouth as the run begins.
The main menu also has multiplayer, settings, a button to support the game on Ko-fi, and quit.

**The run**. You start at the Cave Entrance (depth 0), with the tunnel you came in by behind you:
at its far left end is the daylight, so bright it washes out to white. Interact there to walk
back out (only the interact button takes you, never a stray up): the run ends on the spot and
nothing from it is kept, not its embers, finds or even that it happened. Every level ends in a guardian's chamber
(marked EXIT on the minimap); killing the guardian drops a chest and opens the exits: stone
doorways onto passages that go down into the dark. Each shows which biome it leads to (a faint
breath of that biome's colour comes up from far below) and how deep: the gentle way goes one depth
down (one chevron on the keystone), the steep way two (two chevrons; harder, but fewer levels to
the dragon). Nobody is taken down until they choose to go: stand at the doorway and press E or
up, so an exit that opens under your feet can't whisk you away from the guardian's chest. The last levels lead into the Dragon's Lair at
depth 10; slaying the Elder Dragon wins the run. Dying (or winning) returns you to the camp fire, with
how the run went.

**Biomes** (`Core/Biomes.cs`, generators in `Cave/CaveGenerator.cs` and `Cave/BiomeGen.cs`).
Each biome sets its generator and its parameters, palette, darkness, liquid, hazards, spawn tables,
mini-bosses and guardian:

| Depth | Biome | Cave | Creatures | Hazards / features | Guardian |
| --- | --- | --- | --- | --- | --- |
| 0 | Cave Entrance | one long winding corridor with side pockets, and the way back out to the daylight behind you; no darkness; few enemies | spiders, bats, rats | - | The Web-Mother (a weak ground spider that calls its brood) |
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
| 8-9 | Magma Caverns | more ledges, less climbing, lava instead of water | magma brutes, obsidian golems, ember scorpions, fire bats | lava (burns hard and throws you out; with Magma Skin you swim in it, and chests lie at its bottom), fire vents | The Molten Colossus |
| 10 | Dragon's Lair | an antechamber and one domed arena over a lava lake, with pits and tiers of ledges | - | lava | The Elder Dragon |

Every level is half again as wide as its biome's base size and just as tall (`Cave.WidthScale`;
the dragon's arena keeps its shape), with more rooms and tunnels to fill it.

Every generator ends with the same pass. A movement-aware reachability check (walk, jump, fall and
swim over cells) looks for any spot you could fall into but not climb out of and fixes it with
stepping-stone ledges; if the guardian's chamber can't be reached at all, it builds a stair of
ledges up to it; any repair that would wall off somewhere you could already go is undone; and the
seed is retried if needed. The exit chamber must be reachable. (A lone cell or two the coarse
model can slip into doesn't count as a trap.) Zig-zag passages between rooms start out away from
the room they leave and turn back at other rooms' walls, so their climbs are never left hanging
over a room's air.

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
shortsword, `Player.Abilities.cs` for ability charges, `Player.Net.cs` for online play, and one
file per hero). Pick one at the camp fire with left / right (the chosen hero stands up).
They complement each other in a party (online, one of each): the Swordsman deals the damage, the
Warden takes it for others, the Vitalist keeps everyone going.

| | Swordsman | Warden | Vitalist |
| --- | --- | --- | --- |
| Attack | Medium sword: 45 px reach, 20 damage per strike, swings 0.6 s apart with a short forward lunge, heavy knockback | Shortsword: 30 px reach, 11 damage, swings every 0.36 s with a fast sweep (about 31 damage a second to the sword's 33) | Drain: tears the life out of the creature you aim at (within 175 px), 10 damage as the staff comes forward (0.06 s after the press: the creature flashes and bursts in crimson, a tether of life snapping out to it), every 0.5 s; the stolen life flies back to you as a crimson mote and becomes alimus (a tenth of the damage) when it arrives |
| Dodge button | Dodge roll on a short cooldown (0.55 s); a swing started mid-roll turns the roll into the strike | Hold to raise the shield (see below) | Hex: creatures around you (110 px) slow to 55% and take 20% more damage for 5 s; 6 s cooldown |
| Ability button | Charged Strike: the next swing does 50% more damage with 25% more reach, and whatever it cuts deals 20% less damage for 5 s; 12 s cooldown; instant, so it never breaks a combo | Guarded Charge: a charge behind the shield that swallows the projectiles and shockwaves in its way and keeps going, and stops at the first attacking creature it meets, breaking the attack off; it passes by creatures that aren't attacking; 3.2 s cooldown | Heal: spends 15 alimus to restore 15 health, shared among everyone in range (420 px) who is hurt, by how hurt each is (each gets 15 x their share of missing health / the sum of those shares); 3 s cooldown. All healing is pink |
| Second ability | Heaving swing, on your feet only: you're planted for 0.42 s as the sword goes up, then one great 190° arc with 30% more reach for twice a normal swing's damage and a heavy knockback, then planted 0.3 s more; a waiting Charged Strike is spent on it for more still; 6 s cooldown | Shield bash: a short shove behind the shield; when it meets something, every creature within 40 px in front (a half-circle) takes 20 damage, is stunned for 1.6 s (half that for mini-bosses and guardians; the great bosses shrug it off) and whatever it was doing is broken off; the shield takes 20, once; 10 s cooldown | Rupture: spends 30 alimus (a full reserve). The creature you aim at (within 200 px) is seized where it stands and a quarter second later bursts from within for 30 damage, and every other creature within 80 px of it takes 10; the effect starts at the creature, not at you; 1.5 s cooldown |
| Movement | Full speed and jump | 92% speed, 90% jump height (80% speed while shielding) | 97% speed and jump |
| Toughness | 60 HP, 15 s of breath | 110 HP, 10% armour, 16 s of breath | 55 HP, 15 s of breath |

Warden's shield:
- **Blocking**: it's a broad arc of blue light (135°) that stops all of each blow arriving within
  it, melee or projectile. The shield loses half of what it stops (so its 40 points soak 80). An
  ordinary block doesn't end the attack.
- **Aiming**: like a swing, it aims at the right stick, else the left stick on a controller, else
  the mouse. Pushing the right stick raises it by itself, so you can run one way and guard the
  other without holding a button. You can swing while it's up, but those swings don't combo.
- **Strength**: it holds 40 and regenerates 3 per second, starting 1 s after its last block.
- **Breaking**: once drained it breaks, the rest of that blow gets through (as chip damage: no
  flinch, no knockback), and it stays at zero for 6 s, then regenerates from zero again. Whatever
  struck the breaking blow, attacker or shooter, is stunned for 1.6 s (half for mini-bosses and
  guardians; the great bosses only rock back).
- **Healing**: whenever the Warden is healed (a heart, a potion, the Vitalist's heal), the shield
  mends by half as much, and a broken shield is usable again at once.
- **Perfect block**: raising it at most 0.18 s before a hit stops all of the blow and breaks the
  attack off (the attacker reels). The Guarded Charge does the same to whatever it meets.

The Vitalist's alimus is like mana: earned by draining (a tenth of the damage), held up to 30
(more with upgrades and levels), and spent on heals (15) and ruptures (30). You start a run with 15
and each level with at least 7.5. Healing is deliberately scarce: every hero is meant to pull
their weight and dodge, not lean on the Vitalist.

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

**Readable wind-ups**. A creature winding up or in the middle of an attack keeps its pose and its
timing when struck: the blow flashes it, squashes it and shows the damage, but it doesn't flinch,
freeze for the hit-stop or reel from the knockback. So an attack always lands exactly as its
wind-up promised, and can be read and blocked. Only the shield breaks an attack off: a perfect
block, the Guarded Charge, the shield bash, or a shield broken by the blow.

**Holding the attack** down (or, on a controller, pushing the right stick well over, for everyone
but the Warden, whose right stick raises the shield) attacks again and again as fast as the
attack allows, wherever you aim. A held attack never cuts a dodge roll short (a press still does),
and the Vitalist's held drain waits for something within reach rather than casting at nothing.

**Combo**:
1. When a swing lands, your swing cooldown is refunded once, so you can strike again at once.
2. The next swing runs the full cooldown, and then a new combo can start.
3. Each Flurry upgrade adds one more refund to the chain, up to 3.
4. With Finisher, the last strike of a chain of three or more hits much harder.

**Growth during a run** (`Player/Upgrades.cs`). Every card belongs to one kind, shown by its
frame colour and the label at its top:

| Kind | Colour | What it does | Where it comes from |
| --- | --- | --- | --- |
| Stats | | a little of everything, per hero | on their own at every level-up |
| Class | gold | grows one of your hero's four abilities (its tree) | one in every chest; milestones |
| Alteration | teal, glowing | changes how one ability works; an ability takes one at most, and each has upgrades of its own | milestones only (at least one on every milestone while any are left) |
| Generic | pale blue | anyone's: damage, health, speed... | the other two cards in a chest |
| Conditional | green | offered once something else is true (a double jump once you've a jump upgrade; Magma Skin from depth 6) | chests |
| Risk and reward | violet | something given, something taken; each taken once | chests (vaults, later) |

- **Level-ups** are automatic. The Swordsman gains +2 max HP, +3% damage and +1.5% swing speed per
  level; the Warden +4 max HP, +1.5% damage, 1% damage reduction and +2 shield; the Vitalist +3
  max HP, +2% damage and +1 alimus capacity.
- **Milestones**: every 8 levels (fewer with the Path tree) you pick one of three of your hero's
  own cards (class upgrades and alterations), or leave it and take nothing.
- **Chests**: interact at one (E / LT) to look inside. It holds one class card and two others
  (generic, conditional or risk and reward). The cards are dealt the first time anyone looks, and
  stay the same until someone takes one: leave it, and it closes again with the same cards, for
  later or for a friend. Taking a card spends the chest (and heals you a little). Online, a
  chest's class card may be for any hero in the party, everyone sees the same cards, a card for
  another hero is shown but only they can take it, and only one player looks in a chest at a
  time.
- **Your build**: TAB (controller: BACK) while picking a card, or "Your build" in the pause menu,
  shows your four ability trees side by side (every upgrade, taken or not, with its ranks; the
  alterations beneath, the one you took and its upgrades, and cards it rules out crossed through),
  then the generic, conditional and risk-reward cards you've taken.
- **Potions**: you start with one and can carry one (more with the Potion tree). Q / Y drinks it:
  15% of your health at once and 15% more over 20 s. Enemies drop one 1% of the time.
- **Hearts** heal 5% of your health and drop from 3% of kills (30% of elites).
- **Chests** hold the real upgrades and abilities.
  - 80% of treasure dead ends hold a chest (`Drops.TreasureRoomChestChance`). Mini-bosses and
    the boss always drop one.
  - Extra caches are scattered away from the dead ends: 6 on the flooded floor and 4 high in the
    dry caves (`Drops.WaterCaches`, `Drops.HighCaches`), and in the Magma Caverns 4 down in the
    lava (`LavaCaches`), for a hero with Magma Skin (the lava there rises into a lake, never over
    a room's floor, or fills wells sunk from the lowest tunnels; where the guardian's chamber lies
    lowest it can only be a thin film, and fewer fit).
  - Where a chest is tilts its odds (`Drops.ZoneBias`). Movement upgrades are 4x likelier in
    underwater chests, which is how you gear up for the heights. Survival upgrades are 4x
    likelier in chests high in the cave.

The trees (◆ an alteration; its upgrades are indented beneath it):

| Hero · ability | Class upgrades | Alterations |
| --- | --- | --- |
| Swordsman · Sword | Longer Blade (reach), Flurry (+1 strike to your combo, up to 3), Finisher (the last strike of a full combo hits much harder), Downward Thrust (pogo), Heavy Pommel and Crushing Blows (knockback), Thirsty Blade (life steal), Rending Edge (bleed for 40% more over 3 s), Crescent Wave (a flying slash every 1.2 s), Executioner (+60% under 35% health) | |
| Swordsman · Charged Strike | Focus (recharges 20% faster), Twin Charge (two swings, or with Relentless, two combos), Crippling Strike (35% weaker instead of 20%), Storm Edge (a full-strength wave) | ◆ Relentless Charge: the charge carries through your whole next combo at 80% strength · Unbroken (+1 combo strike) |
| Swordsman · Heaving Swing | Broad Shoulders (recharges 20% faster) | ◆ Swift Heave: with a Charged Strike waiting, the heave spends it on speed: no wind-up, in the air too, no extra damage · Second Heave (3 s sooner) |
| Swordsman · Dodge Roll | Phantom Step (invulnerable while rolling), Nimble (-20% cooldown), Second Wind (a second roll), Wind Runner (rolls and Charged Strike recharge 15% faster, four times) | ◆ Counter Roll: rolling into a melee blow stops the roll dead, stops the blow whole and swings back at the attacker · Flowing Counter (a counter can start a combo) |
| Warden · Shortsword | as the Swordsman's blade (reach, Flurry, Finisher, pogo, knockback, life steal) | |
| Warden · Shield | Riposte Guard (perfect blocks reflect), Iron Timing (perfect blocks cost 70% less), Tower Shield (wider), Stalwart (full speed, no knockback), Quick Mend, Spiked Shield (40% back), Last Stand, Aegis (+15% strength and regeneration, four times) | ◆ Unyielding Shield: stops 70% of each blow but never weakens or breaks (rules out Quick Mend and Iron Timing) · Braced (+5%, twice) |
| Warden · Guarded Charge | Ready Charge (-20% cooldown), Long Charge (30% farther), Battering Charge (x4 on what it stops), Rallying Charge (breaking an attack mends 12 and heals 4), Vanguard (15% faster, four times) | ◆ Guardian's Charge: rushes to the friend nearest your aim and wraps them in a barrier that soaks 20 damage for 2 s (alone, it wraps you) · Thick Barrier (+50%), Lasting Barrier (+33% longer) |
| Warden · Shield Bash | Hard Shoulder (recharges 20% faster) | ◆ Deflecting Bash: no stun, but every projectile in a wide arc in front goes back where it came from · Return to Sender (double damage) |
| Vitalist · Drain | Many Mouths (one more creature at 60%, twice), Far Reach (+25%), Hungering Spirit (+50% alimus), Deep Well (+15 alimus) | |
| Vitalist · Hex | Spreading Blight (+30% radius), Lingering Hex (+2 s), Withering Hex (rot for 6 a second) | ◆ Blight Burst: the hex also deals 12 to everything it reaches, but slows and weakens half as much · ◆ Endless Hex: no cooldown, 10 alimus a cast |
| Vitalist · Heal | Deep Mending (+30%), Frugal Rites (25% cheaper), Wellspring (+15% alimus gained and heal strength, four times) | ◆ Slow Mending: half the heal at once, half over 6 s · Patient Mending (all of it over time, 20% more), Warding Mending (-20% damage taken while mending) |
| Vitalist · Rupture | Burst Veins (40% farther, 50% more splash), Thin Blood (20% cheaper) | ◆ Lifebloom: the rupture blooms on the friend nearest your aim (alone, on you), healing them 30 and everyone else in the burst 10 · Healing Pool (it leaves a pool healing 3 a second for 5 s) |

| Everyone | Cards |
| --- | --- |
| Generic | Quick Hands (attack speed), Whetstone (damage), Vitality (max HP), Resilience, Toughened Hide (damage reduction), Trophy Hunter (heal 1 per kill), Lodestone (XP magnet), Light Boots, Spring Step, Webbed Gloves, Deep Lungs (+50% breath) |
| Conditional | Double Jump *or* Air Dash (rare; once you've a jump upgrade or Hollow Bones, or a speed upgrade), Magma Skin (from depth 6: swim in lava, and it burns you for only 30%) |
| Risk and reward (each taken once, never offered again) | Heavy Hand (attacks 34% slower but 66% harder), Hollow Bones (jump 20% higher, swim 40% slower), Gill-Touched (swim 50% faster, run 20% slower), Twin Reserve (your ability holds a second use, each takes twice as long to come back), Glass Edge (deal 20% more, take 25% more), Stoneskin (take 25% less, deal 20% less), Drowned Lungs (never run out of breath, but all healing you receive is 30% weaker; it and Deep Lungs rule each other out) |

Boons: a barrier (Guardian's Charge) soaks blows before they reach your health, and shows as a
pale shell around the hero; a mending (Slow Mending) heals over its seconds, and with Warding
Mending softens every blow meanwhile. Online, a boon given to a friend's hero is held by their
game (everyone else sees the barrier by that hero's flags).

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
| Spider | Crawls along the ceiling and drops on a silk thread when you pass underneath. If you cut the thread, it pounces along the ground. In water it swims after you, legs rowing, and darts in to bite. |
| Magma Brute | Leaves burning puddles and lobs lava globs. Water boils it away. |
| Golem | Slow and almost impossible to knock back. It telegraphs a ground slam that sends shockwaves along the floor, which you can jump over. |
| Cave Fish | Darts at you in water. It also leaps out at you on shore, then flops around on land. |
| Urchin | Stationary on the seabed and pulses its spikes outward. Common: they often sit beneath schools of fish. |
| Eel | Hides in a wall burrow and can't be hurt there. It lunges along a line at swimmers. |
| Rat | Runs in packs, crouches for a quarter second and lunges with a bite. |
| Bear | Rears up for a heavy swipe, or roars and charges, stunning itself if it hits a wall. Treading water it can't swipe or charge: it paddles after you and lunges in to bite. |
| Scorpion | Scuttles close, arches its tail and stings forward and up. |
| Hornet | Hovers above you with a droning buzz, takes aim, then dives in a straight line. |
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
- **Embers** come from guardians (2, 3 from depth 5, 6 for the dragon). Embers buy the ranks of
  the trees.
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

Open the trees at the camp fire: U (controller: BACK), or the Upgrade trees button.

**Spawning and difficulty**. Enemies come from two sources:
- **Residents** sit at points scattered through the tunnels. They are only ever created while out
  of view, so they are already there when you arrive. Early in a run most points stay empty
  (18% filled, 5% more per level of depth). They fill up over 22 minutes and respawn slowly, so exploring stirs up fewer
  enemies than time does.
- **Entrances** arrive in waves on a timer. The first wave comes at 25 s with one enemy. The gap
  between waves starts at 26 s and halves every 12 minutes (`Spawning.RateDoublingMinutes`), down
  to 7 s, and waves grow slowly with the pace (below).
  - Each newcomer gets its own entry point from a different direction, in a band just outside
    the screen edges, and every entry point is reachable along the tunnels.
  - What arrives suits the spot: fish in water, bats where there's no floor, walkers on the
    ground. Each comes to you its own way: bats fly in, frogs hop, goblins run, fish swim.

Enemies move 20% less far than their raw numbers say (`Difficulty.EnemyMoveScale`). That
scales their speed, gravity and jump height together, while aimed leaps (the fish's shore
ambush, the Colossus's slam) still land where they aim. Magma puddles burn for 3, and blue fish
bite softer than orange ones.

Difficulty rises with depth and, more slowly, with play time, and mostly through tougher creatures
rather than more of them: enemy health and damage are 1.15^depth x 2^(minutes/27)
(`Tune.Difficulty`). Enemy speed, attack rate and animation speed rise by a fifth of that, capped at
1.6x so fights stay readable. How many come (the pace) grows gently: 0.15 per level of depth plus 1
per 20 minutes, capped at 2.4.

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

Each character has separate right-facing and left-facing clips, plus turn-around clips that
rotate it through a front-facing view.

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
- the Warden's shield dash and shield bash (a shove); the Swordsman's heaving swing; the
  Vitalist's cast, hex, heal and rupture
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

All sound effects and the three music loops (a sunny, major-key one for the camp, with birdsong and
the fire's crackle; ambient exploration; and the boss) are synthesized at startup
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
- **The camp** (`Render3D/Camp/CampScene.cs`): a world of its own, drawn in a viewport behind the
  menus (so the cave's lights and fog stay out of it) and switched off while you're below: a
  procedural sky with a warm afternoon sun and shadows, a meadow of rolling hills with thousands
  of swaying grass tufts and drifts of wild flowers, trees and faceted boulders, fair-weather
  clouds, blue mountains on the horizon, a fire of billboard flames with sparks, smoke and a
  steaming pot, and the cave mouth: an arch in the cliff whose tunnel fades to black. The heroes
  are their in-game models on logs round the fire (a sitting pose in `HeroDesign`), their
  lanterns unlit in daylight.
- **Menus**: the hero cards in the online lobby show the heroes' 3D models idling under studio
  lights, each in a small viewport of its own (`UI/HeroPortrait.cs`).

Quitting (closing the window, or a test finishing) goes through `Core/SafeQuit.cs`, which first
lets the renderer's queued background shader compiles drain: Godot 4.4 can otherwise hang at exit
while any are still waiting.

To look at the models without playing, `--modelsheet=goblin,bear --shots=DIR` lines each type
up in its clips under studio lights and saves `DIR/sheet_NAME.png` (`--sheetclips=idle:0.3,run:0.6`
picks the clips and moments, `--sheetyaw=DEG` the angle).

## How online play works

`Net/Net.cs` is the session (hosting, joining, the lobby, moving messages); `Net/NetSync.cs` is
what the messages carry and do; `Main.Online.cs` is the flow of a run together;
`UI/OnlineMenu.cs` is the menu and lobby. Messages travel as byte arrays through two RPCs on one
node (reliable and unreliable), over Godot's ENet peer.

- **The host's game owns the cave.** Its spawner, rooms, guardian and every creature run there.
  The other games build the same level from the host's seed (the chests placed while building
  come from the seed too, so they match) and show copies (puppets) of the host's creatures,
  updated twenty times a second and shown a tenth of a second behind, smoothly interpolated,
  with the host's animation clip and frame.
- **Each game runs its own hero**, so it handles exactly as it does alone; the others see a
  puppet of it (its movement, animation, shield, staff glow and blade sweep).
- **Blows cross over.** A blow on a copy of a creature is sent to the host, which applies it
  (its hex, freeze and other effects too). A creature's blow on another player's hero is sent
  to that player's game, which takes it with its own dodge, shield and invulnerability deciding
  what gets through.
- **Things the host's creatures make** (thrown rocks, shockwaves, falling stones, lava, spore
  clouds, dropped hearts, potions and experience, chests and exits) are sent as they appear, and
  every game runs its own copy; a copy only ever hurts its own game's hero.
- **Effects and sounds** made by the host's creatures and by each hero are recorded as they're
  made (`FxLayer`, `SoundBank`) and replayed in the other games, so everyone sees and hears the
  same fight.
- **The host decides** who opens a chest, when everyone is at the same exit (and then sends the
  next level's seed), and when the run is over. Experience is shared.

## Test harness

The harness takes command-line user args after `--`:

```
# 12 seeds per biome: generation time, retries, trap cells (at most 6), a reachable exit;
# saves a map image of each biome (add --biome=NAME for just one; --genverbose prints every
# attempt, and --genimage=SEED saves that seed's map, or that attempt's with --genverbose)
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
  the Charged Strike (harder, and weakening what it cuts); a swing out of a dodge; the heaving
  swing (planting you, taking the waiting charge, landing for about three swings); the crescent
  wave; holding the attack swinging again and again, without cutting a roll short; air bubbles.
- Warden: the shield stopping all of a shot from the front (and losing half of that) but nothing
  from behind; breaking, staying down, and mending at once when healed; a blow that breaks it
  leaving its striker stunned; perfect blocks reflecting a shot and breaking off a melee attack
  while ordinary blocks don't; swinging behind the shield; the right stick raising it by itself;
  the Guarded Charge swallowing a projectile and carrying on, breaking off an attack it meets, and
  passing a creature that isn't attacking; the shield bash striking two creatures at once, its
  single cost to the shield, the stuns, and its cooldown; a golem struck as it winds up a slam
  keeping its pose and slamming on time; holding the attack.
- Vitalist: the drain striking as the staff comes forward and its mote paying back a tenth as alimus
  on arrival; the hex, and hexed creatures taking more damage; the reserve's size; the heal's
  cost and amount; no heal when no one is hurt; the rupture seizing its target, then bursting on
  it and splashing a creature beside it but not one far off, and refused without the alimus;
  Many Mouths draining a second creature; Twin Reserve's two heals in a row; holding the attack
  draining again and again.
- For all: the game clock never slowing for a hit-stop.

`--hitstoptest --shots=DIR` has the hero strike a golem twice and logs every frame of it (who is
frozen, for how long, the clip and frame), saving a screenshot of each.

The checks are timed in game seconds, so run them with `--fixed-fps 60`. They need no window:
`godot --headless --path godot --fixed-fps 60 -- --herotest --hero=warden` runs in well under a
minute, where a software renderer would take many (with a window, use a small one,
`--resolution 480x270`).

`--alttest` (with `--hero=...`) runs the same way through the hero's alterations: Relentless Charge
carrying a whole combo, Swift Heave in the air, Counter Roll stopping a club and answering it (but
not a blow from afar); the Unyielding Shield stopping 70% and never breaking, Braced, Guardian's
Charge wrapping you in a barrier that soaks a blow whole and the rest of a bigger one before
wearing off, Deflecting Bash sending a shot back without stunning; Blight Burst's damage, Endless
Hex spending alimus instead of a cooldown, Slow Mending's half now and half later, Warding
Mending, Lifebloom healing you and its Healing Pool.

`--upgradetest` (headless, a few seconds) checks the rules of the cards over thousands of rolls
for every hero: a chest holds one class card and two others, never an alteration; a milestone
holds only the hero's own cards, with an alteration while any are left; an ability takes one
alteration, and an alteration's upgrades wait for it; the primary attacks have none and every
other ability has one; Wall Kick is gone; Unyielding Shield keeps out Quick Mend and Iron Timing;
Deep Lungs and Drowned Lungs rule each other out; Magma Skin waits for depth 6; and a party's
chest may hold any party hero's class card, locked to them.

`--hero=warden` and `--hero=vitalist` also work with `--autotest` and the other modes.

**Online play**: `tools/nettest.sh` runs two copies of the game without a window on one machine,
one hosting (`--nettest=host`) and one joining it (`--nettest=join`, `--netaddr=IP` for another
machine), and has them check what travels between them: the lobby and each player's own hero,
the same cave in both games, a friend's swing landing on the host's creature (and credited to
them), a creature's blow taken in the friend's game, shared experience, a chest the friend
looks in and leaves (closed again in both games, with the same cards), which the host then looks
in (offered the very same cards) and takes from (spent in both games), a barrier and a warding
mending given to the friend's hero (held in their game, the barrier seen in the host's), a fallen
friend brought back,
going down an exit together into the same next level (a Fossil Graveyard, where each game's
camera must stay on its own hero), the run ending for everyone, and then, back in the lobby, a
second run that everyone walks straight out of through the cave mouth, back to the lobby. It exits
0 only if both pass; the CI build runs it against the exported Linux build. Add
`--ntshots=DIR` to the host (run with a window) for screenshots of the lobby, the fight, a
revive, the exit and the camp; `--onlineshot=DIR` saves the main menu, the online menu and a lobby.

`--menushot=DIR` saves the pause menu and each settings tab, a chest holding a friend's cards, a
milestone's cards and the build page (per `--hero`). `--bosstest` (headless) checks, for
every biome and 24 seeds, that the guardian's chamber has reachable floor that wakes it and that
the guardian stands where the hero can reach it.

Two more test modes:
- `--animtest --shots=DIR` scripts the player through every movement and attack transition (run,
  turn, stop, jump, land, slashes, dodge, ability, hurt) and saves a frame every 1/20 s.
- `--fronttest=DIR` drives the way in with the same actions keys and controllers send: the main
  menu, choosing a hero at the fire, descending to depth 0, a death back at the camp, and back to
  the menu, with a screenshot of each stage. `--campshot=DIR` renders the camp from the menu's
  view, from the fire with each hero chosen, and on the way to the cave.
- `--scenario=NAME` stages one situation and checks it: `water` (a spider and a bear swim after
  the hero and attack in the water), `mouth` (the way out at depth 0: up doesn't take you,
  interact does, and nothing from the run is kept), `fossilcam` (the camera keeps the hero in view
  in the Fossil Graveyards), and `drain` with `--hero=vitalist` (a frame-by-frame picture of the
  drain's burst), and `magma` (the Magma Caverns hide their chests in the lava; lava burns a hero
  and throws them out, but with Magma Skin they swim in it for 30% of the burn, open a chest down
  there and swim back up; seed 2 unless `--seed` says otherwise). Add `--shots=DIR` for
  screenshots.
- `--musicdump=DIR` saves the three music loops as `.wav` files.
- `--padtest` drives the game with synthetic controller events: the main menu and the fire, move, swing, Charged
  Strike, dodge, the heaving swing, pick an upgrade from the level-up cards, pause, move through
  the pause menu into the settings and back out, unpause, swing with the right stick, dodge with
  LB, and look in a chest with LT, leave it, find the same cards again, and take one. Its presses
  are a twentieth of a second long, so run it with `--fixed-fps 60` too (it needs no window). It
  prints ok / FAIL for each step, then PASS or FAIL.

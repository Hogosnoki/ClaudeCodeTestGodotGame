# Dagger Deep

A side-view rogue-lite in 3D: set off from a camp in a sunny meadow, down through the cave mouth to
a dragon at the bottom of the world, as a sword-swinging, dodging Swordsman, a shield-bearing
Warden, a Vitalist who drains life from creatures and gives it to the living, an Elementalist
who burns, freezes and shatters them, or a Rogue who strikes out of the shadows with two daggers.
You move on a 2D plane (left, right,
up, down), but everything you see is 3D: sculpted rock, lit water and lava, skinned and animated
creatures, and effects with real light (see **3D presentation** below). Each level is a biome with its own
cave generator, creatures and hazards. Slay the guardian of each level's exit, then choose one of
two ways down: a gentle one (one depth deeper) or a steep one (two deeper), each into a
different biome. Each level also has a vault behind an iron gate, and two keys to find (see
**Keys and vaults**). Depth 10 is the dragon's lair. Every run begins and ends at the camp outside the cave,
where the heroes sit round a cooking fire; between runs, embers and rare materials buy permanent
advancements on the Ember Grid (see **The Ember Grid**). Up to five friends can play together online, one of each
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

Up to five players, one of each hero. One player hosts; the others join with the host's code.

1. On the main menu, choose **Multiplayer (online)** (or press O, or Y on a controller).
2. The host clicks **Host a game**. The lobby shows a join code like `7K3QD-M2XP9`, with a
   Copy button: send it to your friend. (If your computer is on a home network, a second code
   for players on the same Wi-Fi shows too.)
3. The friend pastes (or types) the code and clicks **Join**. A plain address works too
   (`192.168.1.20`, `100.101.102.103`, `my.host.name`, with `:port` if needed).
4. In the lobby, each player takes a hero (the one you last chose at the camp fire): the heroes are in two rows of four, and
   the panel on the right shows the hero under the cursor (a moving portrait, how they play, and what each ability and their
   support ability does). Taking one
   moves you on to a second stage, as in single player: **The Ember Grid** (at your hero's branch) and
   your **loadout** (side-grades); "Done: back to the lobby" (or Esc / B) returns to the
   lobby, and the **Grid & loadout** button there brings it back. What you set is what you start the
   run with. The host clicks **Start the descent** and everyone begins together.

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
  it, one player looks in a chest at a time, and taking a card spends it for everyone. Hearts,
  potions and keys go to whoever touches them, and a vault's gate opened with anyone's key is
  open for everyone. Kills count for whoever landed the blow.
- **Harder caves**: creatures have 50% more health for each extra player, and the cave holds
  35% more of them.
- **Pausing** opens the menu but doesn't stop the game (the others are still playing), and
  picking an upgrade keeps you safe while you choose. Leaving from the pause menu takes you
  back to the main menu; if the host leaves, the game ends for everyone.
- **Walking out**: at depth 0, the tunnel you came in by leads back out to the daylight. When The tunnel now runs right out through the map's left edge (`BiomeGen.OpenMouth` opens the solid border there; an invisible wall at the edge keeps everyone in), and the daylight floods in from that edge and thins into the tunnel, so it reads as a passage to the outside rather than a glow in front of a blank wall. The way out is at the edge (1.6 cells in, `cave.Mouth`).
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
| Hit weight (a landed blow freezes both the striker and the struck for a moment (0.15 s for a normal sword hit, 0.26 s for a finisher, 0.28 s for a charged strike), and a killing blow holds 1.25 times as long (the hero and the dying creature's last pose; `Feel.HitStopKillMult`); being struck shoves you very little: `Combat.HurtKnockbackMult`, `HurtKnockLock`, `HeroStrikeRecoil`) | `Feel.HitStop*`, `Feel.Kick*` |
| The five heroes | `Swordsman.*` (sword, dodge, Charged Strike, heaving swing), `Warden.*` (shortsword, shield, Guarded Charge, shield bash), `Vitalist.*` (drain, vital force, heal, hex, rupture), `Elementalist.*` (bolts, alimus, updraft, blizzard, snap, burning and freezing), `Rogue.*` (jabs, critical strikes, thrown daggers, recall, vanish, smoke) |
| How many enemies attack at once | `Combat.AttackerShare` (a third of those ready, rounded up), `Combat.SlotRange` |
| Biome hazards | `Roots.*` (grasping roots), `CaveIn.*` (fossil graveyard ceilings), `Hero.Murky*` (rotting water), `Wraith.*` |
| Chests vs level-ups | `Drops.TreasureRoomChestChance`, `Drops.ZoneBias`; the cards are `Upgrades.Chest`, the level-up stats `Progression.AutoLevel` |
| Keys and vaults | `Vault.*` (the vault's passage and chamber, how far from the start, keys per level and how many a hero carries, where hidden keys may lie); a vault chest's cards are `Upgrades.RollVaultCards` |
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
| Interact: open a chest or a vault's gate, go down an exit, bring a friend back | E | LT |
| Open a chest | E beside it (walking into it doesn't open it) | LT |
| Open a vault's gate (it takes a key) | E at the gate | LT |
| Go down an exit | E, or W / up, at the doorway | LT, or the stick / D-pad up, at the doorway |
| Bring a fallen friend back (online) | Hold E beside them | Hold LT beside them (both thumbs stay free to swim) |
| Leave the cave (depth 0: ends the run, keeping nothing) | E at the daylight at the far left | LT |
| Drink a potion | Q | Y |
| Main menu | Up / down and Enter, or click | D-pad or stick up / down, then A |
| Choose hero (stage 1 at the camp fire) | Left / right, or click the arrows | D-pad or stick left / right |
| On to the next stage (hero, then grid & loadout, then difficulty) | Enter (or R), or click the button | A |
| Grid & loadout stage: the Ember Grid, the loadout | U or P, L, or the buttons | The buttons (D-pad to move, A to open); BACK opens the grid |
| Descend into the cave (the last stage, difficulty) | Enter (or R), or click Descend into the cave | A |
| Back a stage (from the first: to the main menu) | Esc | B |
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
- **Sound**: master, music and effects volumes. Each slider follows the ear: its square is the amplitude, so half way is about half as loud
  (-12 dB) and a tenth all but silent (-40 dB). Every sound effect plays on the SFX bus (`--scenario=sfxvol`, run under `--write-movie` so
  the audio is really mixed, measures it).
- **Controls**: how keyboard presses aim (at the mouse, where you move, or automatically),
  vibration, and rebinding every action.

You can switch between the two at any time. The game follows whichever device you used last: it
hides the mouse cursor, changes the on-screen button prompts, and turns rumble on for hits, kills
and damage taken while a controller is active.

## What's in it

**The camp**. The game opens on the main menu, over the camp outside the cave: a green meadow
under a blue sky, a cooking pot over the fire, and the cave's great dark mouth in a mossy cliff (the ground and the rock
fade into its darkness: the camp's ground and cliff shaders shade everything in and just before the mouth down to black, and the haze thins there too).
**Single player** brings you in to the fire, where the heroes sit on logs, and getting ready goes in
three stages (Enter / A on to the next, Esc / B back): **1 Character** (choose one; the chosen hero
stands up), **2 Grid & loadout** (the Ember Grid and the side-grade loadout) and **3
Difficulty** (the two difficulty sliders and Hard Mode, and the Descend button), and the view drifts
into the cave mouth as the run begins. In the online lobby the cursor (arrow keys, D-pad, stick) walks
the controls in the order they are laid out: left and right along the hero cards, up and down between rows.
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
depth 10; slaying the Elder Dragon wins the run. Dying (or winning) returns you to the camp fire: when the death screen comes up, every creature and projectile of the run is sent away (nothing growls on behind the camp), with
how the run went.

**Keys and vaults** (`Combat/Vault.cs`, `Cave/VaultGen.cs`). Every level but the dragon's lair
has one vault: a dead end cut square into the rock, a short passage shut by an iron gate with a
padlock and a lamp over it, and a chamber behind it holding a dark chest. It's marked VAULT on the
minimap (VAULT (OPEN) once it is). The gate keeps everything out, heroes and creatures alike;
interact at it with a key (E / LT) and the lock drops and the gate grinds up into the rock for
good. The key is spent, and any key opens any vault's gate. Without one, the gate only rattles.
- **Keys**: a level holds two. The first mini-boss slain there drops one beside its chest; the
  other lies hidden somewhere you'd have to go looking (a treasure dead end, the flooded floor, a
  ledge up high, at least 700 px from the start and well clear of the guardian, the vault and the
  chests), glinting faintly now and then. On a level with no mini-boss, both are hidden.
- A hero carries three at most (the HUD shows them beside the potions, and a fourth stays where it
  lies, "KEYS FULL"). Keys you don't use come down with you to the next level. Online, a key goes
  to whoever touches it first, and a gate opened with it opens in every game.
- **A vault's chest** holds two risk-and-reward cards (only vaults deal them) and one rare class
  card: one of the ability tier, such as Flurry, Phantom Step, Riposte Guard, Many Mouths, Long
  Winter or Twin Throw (online, for any hero in the party, as a chest's class card may be).

**Biomes** (`Core/Biomes.cs`, generators in `Cave/CaveGenerator.cs` and `Cave/BiomeGen.cs`).
Each biome sets its generator and its parameters, palette, darkness, liquid, hazards, spawn tables,
mini-bosses and guardian:

| Depth | Biome | Cave | Creatures | Hazards / features | Guardian |
| --- | --- | --- | --- | --- | --- |
| 0 | Cave Entrance | one long winding corridor with side pockets, and the way back out to the daylight behind you; no darkness; few enemies | spiders, bats, rats | - | The Web-Mother (a weak ground spider that calls its brood) |
| 1-2 | Den | big round rooms in a line, almost no climbing; lots of elites; picked less often | rats, bears, goblins, bats | - | The Goblin Warchief (60%) or the Den Mother (bear) |
| 1-3 | Root-Choked Tunnels (the deep canopy) | branching tunnels through rock riven by massive tree roots coming down from the surface world; pools of murky water low down | rats, spiders, rot frogs, bats, mire fish, root eels | grasping roots that slow you and, if they hold you long enough, snag your weapon for a moment (two cuts clear them); thick, rotting water that leaves you gasping 50% sooner and swimming 20% slower | The Rotback (bear) |
| 1-3 | Nest | round rooms scattered up and down, joined by zig-zag tunnels that are always walkable; enemies and chests are in the rooms | scorpions, spiders (on the ceiling and the floor: the commonest residents), hornets, and now and then a **hornet hive** | webs that slow you (cut them) | The Brood Queen (scorpion) |
| 2-3 | Ruins | right-angled halls and corridors on three floors, brickwork, shafts with stone slabs to climb, tall halls with floating slabs; over a hall or two with solid rock above it, a ladder of slabs climbs up through the roof into a hidden chamber with a silver (relic) chest | skeletons, goblins, slingers, rats, scorpions | - | The Bone Knight (skeleton) |
| 3-5 | Catacombs | a stepped pyramid of vaulted halls: a great nave under a scalloped vault in the middle of the map (stepped tombs of stacked sarcophagi in its tallest bays), and out of each end a long stair down into a landing hall, another into a long gallery, and so on, outward and downward; one side ends in the Crypt Lord's rotunda (rock behind it for the treasury), the other in a sealed tomb. Cold blue stone and brickwork; lit only by a dim blue glow and the odd wall torch. Skulls and bones strewn underfoot, heaps of skulls, burial urns, ossuary walls of bone heaped against the back wall (skulls every way up, long bones jutting across them), and the barriers across the passages are banks of skulls, not boulders | crypt skeletons, grave rats, grave-robber slingers, crypt bats, spiders, restless wraiths | - | The Crypt Lord (skeleton) |
| 3-4 | Fungal Cavern | wide, low-lying tunnels, little vertical variation | sporelings, frogs, mossback golems, spore hornets | spore pods that burst into choking clouds | The Mossback Hulk |
| 3-5 | Tunnels | slightly smaller map, tight flat or diagonal passages, water low down | rats, bears, bats, fish, eels, urchins | - | The Barrow King (60%) or the Tunnel Brute (bear) |
| 4-6 | Slime Cavern | the original half-flooded cave | goblins, frogs, magma brutes, golems, bats, spiders, fish, eels, urchins | air vents | The Cavern Colossus |
| 5-7 | Frost Caverns | mostly horizontal, ice everywhere | frost bears, rime skeletons, frost wraiths, ice bats | slippery ground; a frozen water surface (break it to swim, and break it again from below to get out); ice ledges that shatter after 3 landings or 2 blows and refreeze | The Rime Colossus |
| 5-7 | Fossil Graveyards | a few vast, echoing chambers of layered sediment; in each, the ribcage of a leviathan arches from deep in the back to just in front of you, and a great skull is sunk in the wall | fossil skeletons, bone scorpions, ossuary golems, marrow rats, bats | unstable ceilings: walk beneath one and dust sifts down and the rock groans, then a few stones break loose (each one shows where it will land) | The Ossuary Colossus |
| 6-8 | Crystal Caves | ledges in every tall space, so no long falls | shardlings, crystal golems, skeletons | crystal spikes | The Prism Golem |
| 6-8 | Old Lava Tubes | long, wide, round bores the fire cut and left (twice the usual width, a fifth of the usual branches, smooth walls) running down in great sweeps; the odd chimney carved up out of a tube's roof, too high for any jump, ends in a hidden chamber with a silver (relic) chest (the guardian is never up one); faint embers in the seams of the rock | basalt scorpions, basalt golems, magma brutes, cinder bats, ember hornets, fire elementals | - | The Basalt Warden (golem) |
| 4-7 | The Underground River | a high, long canyon with a river flowing back down it along the floor, from the guardian's end toward where you arrive, too fast to swim against; boulders hang in the river, flat on top, a jump apart: the dry way across | bats only: the roof is full of them (river, pale and blind ones), elite bats on the broad banks | the Echo Queen, a huge bat, on a high plateau at the head of the river | the river's bed holds the upgrades; the river comes from under the plateau: a tunnel to a hidden chamber with a silver chest |
| 5-8 | The Clockwork Deep | a forgotten works: four stacked galleries with dead-level floors, great machine halls cut off them (a stair of catwalks up one wall and the other), and shafts between the floors with catwalks and, in about half, a lift; riveted iron and brass plate on every wall, gears turning behind them | automata (clockwork soldiers that run on a spring), boilers (squat tanks that vent steam), cogwheels (iron wheels that roll, rev and drive through), soot bats, rusted skeletons, iron golems | pistons that hang from the roof and slam on a cycle, belts that carry you, saws on rails, lifts, steam | The Engine-Warden (columns of steam erupt across its hall) |
| 6-9 | The Sulphur Springs | the ordinary branching caves, in crusted yellow rock, with a pool of acid in the lowest quarter and toxic air lying on it | acid newts (they keep their distance and spit arcs of acid), gasbags (drifting bladders of gas that hang over your head), brimstone worms (hid in holes in the floor), brimstone scorpions, sulphur bats, fire elementals | gas vents that hiss, then blow clouds of poisonous gas that hang about; the gas is fuel: a fire bolt, a fireball, a firestorm or a burning creature lights it and it goes up in a ball of fire that hurts everything in it and lights the clouds beside it | The Vent Colossus (it breathes out a ring of clouds across its chamber: light them) |
| secret | The Sunken Sea | one vast, deep, wide-open underground lake: a beach where you arrive, a shelf at the far side with the way on, and between them black water over trenches and shoals, rock islands, pillars rising from the deep and treasure pits sunk into the lake bed | fish, eels, urchins, crabs, water elementals; frogs, drowned skeletons and bats on the shores | - | none: the two exits (gentle and steep, as ever) are open from the start |
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

**The vault** is cut last, after the traversal check (it only adds room you can walk into and back
out of) and after the spawn points (nothing starts inside it). `Cave/VaultGen.cs` looks for a
floor you can walk to, dry, well away from the start and clear of the guardian's chamber, beside a
tunnel wall that is a thick slab of rock, and runs a straight passage (5 cells long, 3 tall) into
the rock, opening into a chamber (9 by 5), both floored level with the tunnel and with a skin of
rock all round, so it opens only into the tunnel it starts from. The farthest such spot from the
start wins (a little shuffled). A curving wall gets a doorstep of up to three cells before the gate.
Where there's no such spot clear of the guardian (and always in the Cave Entrance, whose winding
tunnel has no wall of its own to cut into), the search allows the guardian's chamber too, and the
vault is usually a treasury beyond its far wall: the Entrance's corridor and the Den's line of
rooms stop 22 cells short of the level's end to leave it room. An attempt that can't fit a vault anywhere
counts as worse, so the generator tries another seed, but a cave you could get stuck in always
counts as worse still.

**The heroes** (`Player/`: `Player.cs` for what they share, `Player.Blade.cs` for the sword and
shortsword, `Player.Abilities.cs` for ability charges, `Player.Net.cs` for online play, and one
file per hero). Pick one at the camp fire with left / right (the chosen hero stands up).
They complement each other in a party (online, one of each): the Swordsman deals the damage, the
Warden takes it for others, the Vitalist keeps everyone going, the Elementalist controls the
field (burning, freezing and shattering from range, and lifting everyone up out of reach), and
the Rogue picks off what matters (quick jabs, daggers thrown and torn back out, and slipping out
of sight).

| | Swordsman | Warden | Vitalist | Elementalist | Rogue |
| --- | --- | --- | --- | --- | --- |
| Attack | Medium sword: 45 px reach, 20 damage per strike, swings 0.6 s apart with a short forward lunge, heavy knockback | Shortsword: 30 px reach, 11 damage, swings every 0.36 s with a fast sweep (about 31 damage a second to the sword's 33) | Drain: tears the life out of the creature you aim at (within 175 px), 10 damage as the staff comes forward (0.06 s after the press: the creature flashes and bursts in crimson, a tether of life snapping out to it), every 0.5 s; the stolen life flies back to you as a crimson mote and becomes vital force (a tenth of the damage) when it arrives | Firebolt: a bolt of fire flying 520 px/s up to 260 px (a creature near your aim draws it), 28 damage; the staff holds two bolts, gets them back one at a time (one every 1.5 s), and throws them at least 0.25 s apart; each has a 65% chance of setting what it strikes alight (4 a second for 4 s) | Dagger Slash: 22 px reach, 9 damage, five jabs a second, one creature at a time (the nearest), with barely a hit-stop and no combo; half as fast with a dagger thrown. Each of the Rogue's blows (a jab, a throw, a recall) has a 5% chance of being a critical strike, twice as hard |
| Dodge button | **Dash-slash**: a dash (560 px/s for 0.28 s) in an orange ghost-glow that ends in a slash of 1.5x a strike, with everything a strike brings, on the first creature it touches; about 15 s to come back (`Tune.Swordsman.DashSlash*`); it dodges nothing of itself (Phantom Step still makes it invulnerable), but can be used to get away | Hold to raise the shield (see below) | Hex: creatures around you (110 px) slow to 55% and take 20% more damage for 5 s; 6 s cooldown | Updraft: 15 alimus for a wide (4.5 m), tall (10 m) column of faint air at your feet for 10 s; it lifts nobody, but every hero in it (a friend too) has gravity at 0.4 and terminal velocity at 0.2, so jumps go far higher and falls are a slow drift | Vanish: 6 s in the shadows: creatures lose you, and you move half again as fast; attacking or being struck ends it; 12 s cooldown |
| Ability button | Charged Strike: the next swing does 50% more damage with 25% more reach, and whatever it cuts deals 20% less damage for 5 s; 12 s cooldown; instant, so it never breaks a combo | Guarded Charge: a charge behind the shield that swallows the projectiles and shockwaves in its way and keeps going, and stops at the first attacking creature it meets, breaking the attack off; it passes by creatures that aren't attacking; 3.2 s cooldown | Heal: spends 15 vital force to restore 15 health, shared among everyone in range (420 px) who is hurt, by how hurt each is (each gets 15 x their share of missing health / the sum of those shares); 3 s cooldown. All healing is pink | Blizzard: 20 alimus; a storm about 50 px across on the creature nearest your aim (or where the mouse points, or ahead of you), up to 200 px away: 9 strikes of 2 over 3 s, each with a 6% chance of freezing a regular creature solid for 1.5 s; 20 s cooldown | Dagger Throw: one of your two daggers, at the creature nearest your aim (up to 280 px), for 18 damage; it sticks in the creature it meets and rides in it, nudging it; a miss comes back by itself, and with both stuck in creatures, both come home |
| Second ability | Heaving swing, on your feet only: you're planted for 0.42 s as the sword goes up, then one great 190° arc with 30% more reach for twice a normal swing's damage and a heavy knockback, then planted 0.3 s more; a waiting Charged Strike is spent on it for more still; it breaks a rubble pile in one blow and shatters every frozen creature it strikes; 6 s cooldown | Shield bash: a short shove behind the shield; when it meets something, every creature within 40 px in front (a half-circle) takes 20 damage, is stunned for 1.6 s (half that for mini-bosses and guardians; the great bosses shrug it off) and whatever it was doing is broken off; the shield takes 20, once; it shatters frozen creatures it meets; 10 s cooldown | Rupture: spends 30 vital force (a full reserve). The creature you aim at (within 200 px) is seized where it stands and a quarter second later bursts from within for 30 damage, and every other creature within 80 px of it takes 10; the effect starts at the creature, not at you; 1.5 s cooldown | Snap: 6 alimus; the Elementalist (slowed to a tenth) and every frozen creature in view (held still) have 0.35 s, then each shatters for 30 (twice the old 15), and every other creature within 50 px of it takes 8 (twice the old 4) | Recall: every dagger out comes home, and one stuck in a creature tears back out through it for 9, yanking it toward you; one stuck in a wall leaves it, and its foothold; no cooldown |
| Movement | Full speed and jump | 92% speed, 90% jump height (80% speed while shielding) | 97% speed and jump | 97% speed and jump | 110% speed, full jump; slides down walls and kicks off them from the start |
| Toughness | 60 HP, 15 s of breath | 110 HP, 10% armour, 16 s of breath | 55 HP, 15 s of breath | 55 HP, 15 s of breath | 55 HP, 15 s of breath |

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

The Vitalist's vital force is like mana: earned by draining (a tenth of the damage), held up to 30
(more with upgrades and levels), and spent on heals (15) and ruptures (30). You start a run with 15
and each level with at least 7.5. Healing is deliberately scarce: every hero is meant to pull
their weight and dodge, not lean on the Vitalist.

The Elementalist's alimus pays for its spells' *recharge*, not their casting: a spell is **free to cast** (the bolt, the updraft, the storm, the snap), and nothing is taken when it goes off. Its recharge runs as usual,
and **when it is done the alimus is taken to bring it back** (a spent bolt's next charge, the updraft, the snap, each use of the storm; the dearest first, so the cheap ones don't keep it from the great ones). With too little alimus a spell **does not return**: it
waits, its orb dark and a hair short of ready, and comes back the moment there is enough. Blood Channeling lets a return that lacks the alimus go ahead anyway, paid for in health. `Player.TickElementalist`, `Tune.Elementalist.UpdraftRecharge`. It holds 40 (more with
upgrades and levels), comes back by itself at 2.5 a second (its only source), and a run starts
full. Its fire and frost work on creatures in three ways, each shown by a wash of colour over the
creature (online, the host keeps them and every game shows them):
- **Burning**: it loses health every second until the fire goes out.
- **Chilled**: it moves and acts slower (a frostbolt's chill is 30% for 2 s).
- **Frozen solid**: it stands in a block of ice for 1.5 s, doing nothing, until it thaws or a
  snap shatters it. Only a regular creature freezes: never a mini-boss, a guardian or a boss, and
  never one winding up or in the middle of an attack (like every blow, frost never cuts a
  telegraphed attack short).

The Rogue's two daggers are its attacks, and what isn't in its hands is out in the cave:
- **Thrown**: a dagger rides in the creature it struck until you recall it, or until the
  creature dies and it drops out and flies home. With one out, your jabs come half as fast; with
  both stuck in creatures, both come home by themselves. The HUD shows which are in hand.
- **In the shadows** (Vanish, or inside a Smoke Bomb's cloud): creatures lose you. Whatever they
  were already swinging plays out; then they stand looking about ("?") until you show yourself.
  Guardians and bosses aren't fooled. Any attack brings you out of the shadows as it begins, and
  so does being struck.
- Online, a friend's daggers and smoke show in every game (their game deals their blows), and a
  friend in the shadows shows faintly.

All of these numbers are in `Tune.Swordsman`, `Tune.Warden`, `Tune.Vitalist`, `Tune.Elementalist`
and `Tune.Rogue`.

Movement and aim are free over the whole circle. The movement stick is read as one vector with a single round dead zone (`Player.ReadMove`,
`Hero.StickDeadzone` 0.2: a gentle push is a gentle move, rest drift is nothing), not each direction on its own: reading the four actions
separately gave the stick a cross-shaped dead zone that bent every push toward the eight directions 45 degrees apart, and that carried into
swimming, the Elementalist's narrow draft and every aimed throw or bolt when aiming with the left stick. The right stick and the mouse were
always free. (Only the keyboard's move keys are eight-way, as keys must be: aim with the mouse there.) `--scenario=stick` checks it.

Movement has coyote time, jump buffering and variable jump height. Gravity is fairly floaty
(`Hero.Floatiness`, which keeps jump height the same) and fall speed is capped at 420 px/s (`Hero.MaxFallSpeed`). Gravity and jump
height are both 0.8x what they were (`Hero.Gravity` 1080, `Hero.JumpVelocity` 336: a full jump rises about 52 px, the rise lasting as long
as ever, so a jump carries as far sideways). The cave generator's climbs follow it: the coarse reach check counts a jump as three cells
(was four), the ledge stairs and slab ladders step three cells (floating stairs were four), the arena tiers stand three apart, and the
repairs on the way to the guardian lay a ledge every two cells of climb (were three).
**Fall damage** counts the time spent falling at that terminal speed (at least 97% of it): past 0.3 s of it (about a 180 px drop) a
landing takes 1% of the hero's *current* health, rising to 50% after another second (`Hero.FallTerminalShare`, `FallGraceSeconds`,
`FallRampSeconds`, `FallShareMin`, `FallShareMax`). An ordinary jump lands just short of the terminal speed and costs nothing, and anything
that brakes the fall on the way down (a second jump, the Elementalist's updraft, a dash, grabbing a rope, water, a creature's form) starts
the count afresh, so a long fall is easily softened. It never takes the last of anyone's health. `--scenario=fall` checks it.
Swimming has a breath meter; you take drowning damage when it runs out and the audio is muffled
while your head is underwater. Sparse air vents on the flooded cave floor release a big bubble
every 4-9 s. Swim into one for 4 s of air (`Hero.AirBubbleBreath`, `Hero.AirVents`). Water soaks up a quarter of your speed when you plunge in and
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
timing when struck: the blow flashes it, squashes it and shows the damage, but it doesn't flinch or
reel from the knockback. It does freeze for the hit-stop, the wind-up holding for a beat (a moment
to get ready) before carrying on. So an attack always lands as its wind-up promised, and can be
read and blocked. Only the shield breaks an attack off: a perfect
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
| Class | gold | grows one of your hero's four abilities (its tree) | one in every chest; milestones; a rare one in every vault |
| Alteration (side-grade) | teal, glowing | changes how one ability works; an ability takes one at most, and each has upgrades of its own | never found in a run: unlocked on the Ember Grid and brought in the loadout; a milestone may offer its **Back to...** card instead (half the time while one is brought), which puts the ability back as it was |
| Generic | pale blue | anyone's: damage, health, speed... | the other two cards in a chest |
| Conditional | green | offered once something else is true (a double jump once you've a jump upgrade; Magma Skin from depth 6) | chests |
| Risk and reward | violet | something given, something taken; each taken once | vaults only (two in each) |

- **Level-ups** are automatic. The Swordsman gains +2 max HP, +3% damage and +1.5% swing speed per
  level; the Warden +4 max HP, +1.5% damage, 1% damage reduction and +2 shield; the Vitalist +3
  max HP, +2% damage and +1 vital force capacity; the Elementalist +3 max HP, +2% damage and +1 alimus
  capacity; the Rogue +2 max HP, +2.5% damage and +0.2% critical chance.
- **Milestones**: every 8 levels (fewer with the Path's Waypoints) you pick one of three of your hero's
  own cards (class upgrades, a technique, something for the party, or a **Back to...** card for a side-grade you brought), or
  leave it and take nothing.
- **Chests**: interact at one (E / LT) to look inside. It holds one class card and two others
  (generic or conditional). The cards are dealt the first time anyone looks, and
  stay the same until someone takes one: leave it, and it closes again with the same cards, for
  later or for a friend. Taking a card spends the chest (and heals you a little). Online, a
  chest's class card may be for any hero in the party, everyone sees the same cards, a card for
  another hero is shown but only they can take it, and only one player looks in a chest at a
  time.
- **Your build**: TAB (controller: BACK) while picking a card, or "Your build" in the pause menu,
  shows your four ability trees side by side (every upgrade, taken or not, with its ranks; the
  alterations beneath, the one you took and its upgrades, and cards it rules out crossed through),
  then the generic, conditional and risk-reward cards you've taken.
- **Potions**: you start with one and can carry one (two with the grid's Second Flask). Q / Y drinks it:
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
- **Vaults** (one a level, behind a gate that takes a key: see **Keys and vaults**) hold the
  risk-and-reward cards: a vault's chest deals two of them and one rare class card.

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
| Vitalist · Drain | Many Mouths (one more creature at 60%, twice), Far Reach (+25%), Hungering Spirit (+50% vital force), Deep Well (+15 vital force) | |
| Vitalist · Hex | Spreading Blight (+30% radius), Lingering Hex (+2 s), Withering Hex (rot for 6 a second) | ◆ Blight Burst: the hex also deals 12 to everything it reaches, but slows and weakens half as much · ◆ Endless Hex: no cooldown, 10 vital force a cast |
| Vitalist · Heal | Deep Mending (+30%), Frugal Rites (25% cheaper), Wellspring (+15% vital force gained and heal strength, four times) | ◆ Slow Mending: half the heal at once, half over 6 s · Patient Mending (all of it over time, 20% more), Warding Mending (-20% damage taken while mending) |
| Vitalist · Rupture | Burst Veins (40% farther, 50% more splash), Thin Blood (20% cheaper) | ◆ Lifebloom: the rupture blooms on the friend nearest your aim (alone, on you), healing them 30 and everyone else in the burst 10 · Healing Pool (it leaves a pool healing 3 a second for 5 s) |
| Elementalist · Firebolt | Kindling (fire 10% likelier to set creatures alight, bolts and a Firestorm, twice), Deep Reservoir (+15 alimus, twice), Spare Bolt (the staff holds one more firebolt, twice; not with Frostbolt), Frozen Quiver (two more frostbolts, twice; only with Frostbolt) | ◆ Frostbolt: bolts of frost, 12 damage; the staff holds four, one back every second; chilling (30% slower for 2 s) with a chance of freezing a regular creature solid |
| Elementalist · Updraft | Attunement (alimus back 25% faster, three times) | (none: the narrow, aimed column below is the updraft itself) |
| Elementalist · Blizzard | Deep Chill (frost 4% likelier to freeze, frostbolts and the blizzard, twice), Long Winter (50% longer), Whiteout (30% wider), Gathering Storm (back 25% sooner) | ◆ Firestorm: a storm of fire, 3 a strike, each with a 25% chance of setting a creature alight (it freezes nothing) |
| Elementalist · Snap | Shrapnel (bursts 40% wider), Echo (5 alimus back for each creature it bursts) | ◆ Cinder Snap: burning creatures burst instead, for 40 and 12 around (also a 0.35 s beat first) |
| Rogue · Dagger Slash | Keen Edge (+5% critical chance, twice), Backstab (+50% from behind a creature), Cruel Edge (critical strikes 2.5 times as hard) | |
| Rogue · Dagger Throw | Twin Throw (both daggers at once), Weighted Daggers (+30%, twice) | ◆ Ricochet: the dagger springs on from its creature to one more nearby (130 px), marking each, then flies on (into a wall, where it sticks, or back); never sticking in a creature (so it rules out Tether). A recall flings a poison pin at every marked creature (6, and 14 more over 4 s) · Chain Ricochet (one creature more) |
| Rogue · Vanish | Surprise Attack (the attack that ends your vanishing, a jab, a throw or a recall, lands four times as hard), Quick Fade (back 20% sooner, twice), Deep Shadows (3 s longer, the smoke too) | ◆ Smoke Bomb: instead of vanishing alone, a cloud of smoke 70 px round for 6 s: every hero in it is hidden, and creatures in it can't find anyone · Thick Smoke (40% wider) |
| Rogue · Recall | Rending Recall (twice as hard) | ◆ Tether: recall pulls you along the line to your dagger stuck in a creature, striking it as you arrive (rules out Ricochet) · Pounce (arriving is always a critical strike) |

The rare class upgrades (a vault's chest holds one while any are left; chests and milestones deal
them too):
Flurry, Finisher, Downward Thrust and Heavy Pommel (both blades); Rending Edge, Crescent Wave,
Executioner, Twin Charge, Crippling Strike, Storm Edge, Phantom Step and Second Wind (Swordsman);
Riposte Guard, Iron Timing, Stalwart, Spiked Shield, Last Stand, Battering Charge and Rallying
Charge (Warden); Many Mouths, Withering Hex and Burst Veins (Vitalist); Long Winter, Shrapnel and
Echo (Elementalist); Backstab, Cruel Edge, Twin Throw, Surprise Attack and Rending Recall (Rogue).

| Everyone | Cards |
| --- | --- |
| Generic | Quick Hands (attack speed), Whetstone (damage), Vitality (max HP), Resilience, Toughened Hide (damage reduction), Trophy Hunter (heal 1 per kill), Lodestone (XP magnet), Light Boots, Spring Step, Webbed Gloves, Deep Lungs (+50% breath) |
| Conditional | Double Jump *or* Air Dash (rare; once you've a jump upgrade or Hollow Bones, or a speed upgrade), Magma Skin (from depth 6: swim in lava, and it burns you for only 30%) |
| Risk and reward (vaults only; each taken once, never offered again) | Heavy Hand (attacks 34% slower but 66% harder), Hollow Bones (jump 20% higher, swim 40% slower), Gill-Touched (swim 50% faster, run 20% slower), Twin Reserve (your ability holds a second use, each takes twice as long to come back), Glass Edge (deal 20% more, take 25% more), Stoneskin (take 25% less, deal 20% less), Drowned Lungs (never run out of breath, but all healing you receive is 30% weaker; it and Deep Lungs rule each other out) |

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
| Reef Crab | Lives on shore slabs (ground that runs down into the water at a walkable angle) in the watery caves: scuttles along the shore and walks the bottom after you, swims up for a hero overhead, and snaps both claws shut at close range. |
| Hornet | Hovers above you with a droning buzz, takes aim, then dives in a straight line. |
| Skeleton | Plods forward and slashes. Sometimes a felled skeleton pulls itself back together at half health. |
| Sporeling | Waddles close and puffs a choking spore cloud; bursts into one when killed. |
| Frost Wraith | A haunting more than a hunter: it drifts at the edge of the light, now and then looms in over you with a shriek (arms flung wide, jaw open, eyes blazing, but doing no harm), and only rarely, after a long and obvious wind-up, looses a single ice shard (an elite, a fan of three). Frail once you reach it. |
| Shardling | Curls up and bursts in a spray of crystal shards; shatters into more when killed. |
| Cavern Colossus | Leap slams, a roar that brings stalactites down, and a wall charge that leaves it stunned. It enrages at half health and summons bats. |
| Elder Dragon | Stalks the arena floor, sweeps a cone of fire, flies up and dives with a ground-shaking landing, lashes its tail, and roars down a rain of fire. It enrages at half health and calls fire bats. |

Biomes also field tinted variants (frost bears, rime skeletons, ember scorpions, obsidian golems,
fire bats...). Mini-bosses are elite versions and drop a chest (the first slain on a level, a key too); guardians are elites with extra
health and a title.

**Between runs: embers, materials and the Ember Grid** (`Core/Meta.cs`, `Core/HexGrid.cs`, `UI/HexGridMenu.cs`; see **The Ember
Grid** below). Progress saved in `user://meta.json`: embers come from guardians (2, 3 from depth 5, 6 for the dragon) and, with the
branches' materials found in runs, buy hexes on the grid. Open it at the camp fire: U (controller: BACK), or The Ember Grid button.

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
rather than more of them: every creature's own health is doubled at spawn (Tune.Enemy.BaseHpMult), then enemy damage is 1.15^depth x 2^(minutes/27)
and enemy health grows at half that rate per depth, 1.075^depth x 2^(minutes/27) (`Tune.Difficulty`, `HpDepthShare`). Enemy speed, attack rate and animation speed rise by a fifth of that, capped at
1.6x so fights stay readable. How many come (the pace) grows gently: 0.15 per level of depth plus 1
per 20 minutes, capped at 2.4.

**Sprites and the animation clock** (`Art/`, generated by `tools/sprites/`). The 2D game this grew
from drew every character as a sprite-sheet animation played at 24 fps. The sheets are still
loaded and played, invisibly: their clips, frames and timing are the clock the 3D models animate
to, so every attack's wind-up, strike and hitbox lines up exactly as before. The sets:

| Group | Characters |
| --- | --- |
| Heroes | the Swordsman, the Warden, the Vitalist, the Elementalist and the Rogue |
| Enemies | every enemy type |
| Boss | the Cavern Colossus |
| Passive critters | glow moths and cave crabs (new ambient wildlife) |

Each character has separate right-facing and left-facing clips, plus turn-around clips that
rotate it through a front-facing view.

The left-facing sprites are real renders, not mirrored copies. That means the blade stays in the
hero's right hand (the far hand when facing right, the near hand when facing left), and the light
always comes from the upper left.

All five heroes are drawn by the same rig (`tools/sprites/player.py`) with a style switch:
- The Swordsman (Bran): a broad, weathered warrior from the concept art, sculpted from the same signed-distance primitives as the others (`Designs/HeroDesign.Bran.cs`) at a finer grid (about 80k triangles): a heavy-browed, stubbled face with the brows, lids, lips and nostrils laid on the skin as explicit strands (they are finer than a voxel; `Sculptor.SurfaceX` finds where the skin stands under each), dark tousled hair, a slate cowl, a shaggy fur mantle over his left shoulder, bare arms in leather bracers and fingerless gloves, straps, a belt and buckles over a dark tunic, a split navy tabard following his thighs, baggy trousers into fur-cuffed boots, a ragged cloak and his longsword. Texture is painted over the finished skin (`Sculptor.Paints`: stubble, ruddy cheeks, hairline, scuffed leather, worn cloth, dusty hems), and each skin vertex carries how much of each detail kind it is (`CUSTOM1/2`), so a seam between two materials blends their textures smoothly in `creature.gdshader`. `--modelsheet=swordsman --sheetfocus=Y,H --sheetsoft` frames close-ups.
- The Warden: built broader than the rest. His shoulder joints and hip sockets sit further out on the rig (`ArmZ`, `LegZ`), with a barrel chest, a broad back, great layered pauldrons, thick mailed arms in steel vambraces and gauntlets, and heavy legs. He wears a blue tabard and gold sash, and carries a shortsword and a kite shield. His helm is an open-faced bascinet with a nasal bar, a mail aventail and a short red crest; in it is a broad, square-jawed, stern face under a heavy brow, a once-broken nose and a short auburn beard.
- The Vitalist: a green robe, a crystal-headed staff, and a bone mask carved like a skull: a heavy brow over deep sockets lit green, high cheekbones, the nose a hollow, the teeth set in a grim line, a crack down from the brow.
- The Elementalist: a violet robe, an ember sash and a staff crowned with a burning orb.
  Its cooldowns are shown on the hero as small glowing orbs: two orange ones in its hands for the
  bolts it holds (white with Frostbolt, which holds a third), and three that circle it for the
  updraft (gray), the snap (yellow) and the storm (white, orange with Firestorm). An orb shrinks
  away while its spell recovers and swells back when it's ready; the bolts return one at a time,
  the last spent coming back first, and the HUD shows the bolts as pips under the alimus bar. Other
  players in an online game see the same orbs (the state rides the hero's net flags).
  Under the tall hood is an old, weathered face: gaunt under high cheekbones, with deep-set eyes catching the orb's light under bushy grey brows, a hooked nose, the lines of age and a long grey beard.
- The Aegis: an ivory robe and teal mantle, and a pale hood thrown back from a calm, soft-featured face. The face has a smooth brow, a straight nose, full lips at ease and a gentle near-smile, with dark hair parted under a gold circlet set with a teal stone.
- The Shape Shifter: a young woman in a grey robe, with a wildwood staff with antler tines. She has a lean, wild face: high sharp cheekbones over a fine jaw, pale shining eyes and a sly half-smile. Long silver-grey hair falls from her hood to her collarbones and is swept across her brow. A band of charcoal crosses her eyes, with claw marks down one cheek and a red collar at her throat. (The red point that used to stick out behind her head was an old scarf tail, removed.)
- The Automaton: a copper head under a brass dome, and no face at all: a blank, smooth faceplate (the one hero without facial features).

**Faces.** Every hero's face is built on the Swordsman's carefully placed anatomy (`Designs/HeroDesign.Faces.cs`), moved, scaled and reshaped by a `Face` of the hero's own:
- **Bone structure:** the jaw (square to soft) and chin; the cheekbones' prominence and height; the brow ridge; the nose's length, bridge bump (a break or a hook), tip and width.
- **Features:** the lips' fullness and the mouth's width and set (a smile or stern); the brows' tilt and weight; how deep the eyes sit.
- **Lines of age:** folds from the nostrils to the mouth, and forehead creases.
- **Beard:** none, stubble, short or long, with a moustache.
- **Fine strands on the skin:** brows, the lids' lash lines, the lips, the mouth's line, nostrils and philtrum.
- **Paint:** a flush on the cheeks, shadow in the sockets, warmth about the mouth, a beard's shadow, war paint.

Each eye is a white, an iris and a pupil, turned in a touch, with the upper lid over its top.

**Proportions.** Every hero has a shorter torso and longer legs than they were sculpted with, at about the same height. `HeroDesign.Proportion` reshapes the finished figure through `Sculptor.Reshape`:
- the trunk is squeezed 12% between the hips and the neck, and the legs are stretched 8% from hip to ankle and made 10% thicker (`LegGirth`);
- the head and neck, the feet and soles, and each arm (with whatever it holds) move without stretching;
- the leg solver takes the new thigh and shin lengths and hip height, and the painters see the skin in the sculpt's measures (`Sculptor.PaintSpace`).

**Crisp edges.** Beards, hair, bandanas, collars, helms and masks are marked `Prim.Crisp`. Their colour and material stop hard where their surface meets another's, instead of blending across the smooth union's width (which left a beard half skin, or a red scarf bleeding onto a jaw); the bones still share those vertices smoothly, so they bend as before. They are also joined with tight blends, so they read as cloth or hair with an edge. Every hero is now meshed at the Swordsman's 1 cm grid (about 55-75k triangles; a hero takes about half a second to bake). `--sheetclips=rest:0` poses the rig at rest for close-ups.
- The Rogue: a plum hood, dark leather, a short cape and a dagger in each hand. A dark bandana is tied over his face to just under the eyes, hiding his nose, mouth and jaw; its hem is pulled taut across the bridge of the nose from cheek to cheek, with folds drawn down under the cheekbones. Above it are intent brows, red eyes, and a lock of pale hair fallen across one eye.

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
  casters' cast, hex, heal and rupture (the Elementalist casts its bolts, updraft, blizzard and
  snap with them); the Rogue's throw (its recall plays it too, and its jabs are its slashes)
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

The effects are built from noise and struck-object models rather than notes (`Audio/Synth.cs`:
`BandNoise` for whooshes and splashes, `Modes` for stone, wood, metal and glass being hit, `Creak`
for stressed wood), so nothing rings like a chime. What a blow on an enemy sounds like depends on
what it is made of (`Enemy.HitSound`): a stone or armoured one *chinks*, wood, bone and roots give a
crackling thud, ice cracks, fire hisses and spits, water slaps, and flesh thumps. Chests creak and
knock, the hero plunges in (`splash_in`) and climbs out more gently (`splash_out`), and swords whoosh.
`--sfxdump=DIR` writes every effect as a .wav for a listen (the elements' too: each element's five events,
plain and as two sample attacks, and a pair).

How they are finished, so they sound made of real things in a real place rather than a synthesizer:
- **44.1 kHz** for the effects (the music stays at 22.05 kHz). Filter settings and noise levels are converted (`Synth.K`,
  `Synth.NoiseGain`), so each sound keeps its character at the higher rate, only cleaner in the highs.
- **The finishing pass** every effect goes through (`Synth.Finish`): rumble and offset filtered out, a 1.5 ms fade-in and a short
  fade at the end (no clicks), the peaks rounded by a gentle saturation (warmth, not a hard digital edge), and a small stone room
  round it (`Synth.Room`: early reflections off near walls and a short damped tail, the highest air taken out). The menus' own sounds
  stay dry.
- **Three takes** of each effect: the plain one, one a shade darker and one a shade brighter, each in a slightly different room,
  played at random (with the usual small pitch wander), so a run of blows or footsteps never sounds machine-gunned.
- New detail in the commonest sounds: a hit on flesh has the crack of contact, a wet slap and the body's weight behind it;
  landing and jumping scatter grit (`Synth.Grains`); the hero's grunt when struck is a voice (`Synth.Voice`: a breathy glottal pulse
  through "uh" formants, its pitch falling) rather than a filtered tone.

**Nothing tonal.** No effect plays a note or holds a sine: every thump is a **rumble** (noise held low, `Synth.Rumble`), every droplet,
bubble and blurp a blip of filtered noise, every voice breath through a throat (a goblin's snarl, a beast's roar, a frog's croak, a
gasp, `Synth.Whisper`). The textures that make real sounds are their own tools: **crackle** (`Synth.Crackle`: a random scatter of tiny
damped clicks, mostly small, the odd loud one: fire, ice forming, grit, twigs, gravel, droplets), **turbulent air** (`Synth.Whoosh`:
band noise whose centre and loudness wander like eddies: gusts, a flame's roar, whooshes, never a whistle), **crystallization**
(`Synth.Tinkles`: glassy tinks a few milliseconds long at scattered colours, so they sparkle rather than sing), **water** (`Synth.Gurgle`)
and **crunch** (`Synth.Crunch`: a rough asymmetric drive with a breath of grit, so blows bite). Struck stone, wood and metal ring only for
an instant. The game's own sounds, with nothing real to copy, are textures too: a level gained is air rushing in, a deep bloom of weight
and a shower of crystal and sparks; a milestone a rising flurry of crystal; the cards a paper riffle and a glitter; a relic a struck plate
of old metal (a gong's inharmonic wash); experience a soft sparkle. Each blade has its own air: the sword's sweep, the Warden's short cut,
the Rogue's jab, the Automaton's cleaver; daggers bite into flesh, chip into rock and skitter off it.

**The elements' sounds** (`Audio/ElementSounds.cs`): every element has its own sound for **readying** it (`Ev.Charge`: fire a small flare
catching with a crackle, wind the whoosh of air drawn in, frost the twinkling of crystallization over a breath of frost, earth stone
grinding with grit in it, nature a rustle of leaves, water a slosh, shadow a whisper), for an attack **loosed** (`Ev.Cast`), **striking a
creature** (`Ev.Hit`), **meeting rock** (`Ev.Wall`) and **dissipating** at the end of its range or life (`Ev.Fizzle`: a flame guttering,
ice subliming, a breeze dying, pebbles trickling, drips slowing). A readying sound is smaller for an attribute, bigger for a mode switch
and biggest for an overcharge. **Every attack sounds like itself**: its own name seeds its own variant of its element's sound (brighter
or darker, longer or shorter, denser or sparser texture), a pair layers its second element under the first and is weightier, and the
same seed makes its strikes, splashes on rock and dissipation, so a Firebolt and a Meteorite, or a Rime Edge and an Icicle Lance, are
told apart by ear. Where they come from: every ability used (`Player.CastSound`, at the three places that name it), bolts and bubble shots
(`ElementBolt`: their caster's ability), elemental throws, elemental blade blows (the swing's, the Warden's cut's or the hilt's element),
and the lasting spells (`HeroSpell`: each kind of spell its own variant, its strikes a few times a second at most, and its own
dissipation when it ends of itself). They are made on first hearing and kept (the elements' readying sounds at start); a sound's name
carries everything it is made from (`@event|attack|element|element|weight`), so online the other games make the very same one, and the
same attack's sound struck twice within a breath is played once.

## The Elementals

Five creatures that live where their element is (`Enemies/Elementals.cs`, looks in `Render3D/Creatures/Designs/ElementalDesigns.cs`, numbers in `Tune.Elementals`, placement in `Biomes.AddElementals`): the **Nature Elemental** (roots and fungal caves; a man of roots and leaves that looses leaf-blades and heals if left alone), the **Water Elemental** (every biome with water; drifting drops with droplets circling it, never leaves the water, spits droplets), the **Fire Elemental** (magma caverns; many small flames standing as one, quick, flings fireballs), the **Frost Elemental** (frost caverns; a crystalline golem that fans out ice shards) and the **Earth Elemental** (dens, nests, tunnels, roots, fossil graveyards; packed earth and stone with orbiting rocks, hard to knock back, hurls boulders). `--scenario=elementals` checks each winds up an attack.

Armoured creatures (golems, scorpions, shardlings and the colossi) take 40% less from physical blows, but only until the armour has turned aside a tenth of their health (`Combat.ArmorBreakShare`: what it soaks counts, not what gets through): then it falls off ("ARMOUR BROKEN"), every blow lands in full, and they sound like flesh when struck instead of stone. Earthen creatures keep their resistance.

## Motion

Heroes' legs are solved, not keyframed (`HeroDesign.Motion.cs`): each foot stays planted while it carries the body and swings through an arc to land ahead, so runs never skate; each hero has a style of stride, lean and arm carriage, and their own way of cutting or casting. Swings play the gameplay swing's own timers (wind-up, sweep, follow-through), blows land as the blade crosses the aim, and the sword's light trail is read off the blade's bone each frame. `--scenario=motion --shots=DIR` records cropped frames of runs and cuts; `--modelsheet=NAME --sheetclips=run:0.25,...` lays out poses (`run:PHASE` fixes the gait phase, `air:T` poses a jump from rising (0) through the apex (0.5) to falling fast (1)).

### Heroic carriage

Every hero moves like the main character: confident, committed, never stiff, but within what a body can really do.

- **Standing.** No hero stands at attention. Each holds a stance of their own: feet set wide and staggered, knees soft, chest up and chin lifted, the weapon held where it can strike from. The feet are solved onto the floor (`Stance`), so the weight can drift between them without a foot floating or sliding. The breath lifts the chest and squares the shoulders; the Automaton only hums.
  - The Swordsman moves after Marth (Super Smash Bros. Melee); see below.
  - The Warden stands squared behind his shield, sword cocked over his shoulder.
  - The Rogue moves after Sheik (Super Smash Bros. Melee); see below.
  - The casters stand tall, staff planted. The Elementalist holds power ready in the free hand; the Vitalist and the Aegis open a palm.
  - The Automaton stands broad, its cleaver low and forward.
  - The Shape Shifter is loose and clawed, staff held like a blade.
- **Cuts.** Every beat is committed:
  - Wind-up: the whole body coils and rocks back onto the rear foot, the blade drawn on past its mark.
  - Cut: the leading foot steps in and the body drives forward over it into a deep lunge, rear heel lifting. The pelvis leads the turn and the free arm flings back to balance.
  - Follow-through: the blade carries on past its mark and is held there a beat before the body recovers.
  - Throughout, the head holds steady with the eyes on the mark, and the arm keeps the blade's line against the chest's pitch so it ends low over the floor, not through it.
  - A finisher goes further in everything.
  - The heaving swing rises onto the toes as the sword goes up and back, then drops the whole weight into the blow and holds the finish.
- **Jumps and landings.**
  - Take-off: a deep coil with the arms swung back, then everything thrown upward onto the toes.
  - Rising: one knee driven high, the other leg trailing long.
  - Falling: legs reaching for the ground, arms spread wide, eyes on the landing.
  - Landing: the fall soaked up in a deep crouch, the free hand reaching for the floor, the weapon trailed wide behind, head up. The hero then rises tall, the chest lifting a touch past upright.
- **The Swordsman, after Marth.** The Swordsman's reference is Marth from Super Smash Bros. Melee: an aristocratic fencer, tall, light and poised, with fast, wide crescent cuts.
  - Standing, he is side-on with chest proud and chin up. The sword is held low and forward, its point toward the floor ahead, and the off hand is drawn back by the hip. He bobs lightly on his feet.
  - Running, he is tall in long gliding strides. The free arm pumps and the sword arm is swept back nearly straight, the blade trailing low behind him.
  - Cuts are wide crescents thrown with a long, nearly straight arm, the body turning through them upright. The off hand reaches toward the mark in the wind-up, then sweeps back and out behind, long and open, as the blade comes through. The finisher rises overhead and comes down into a long lunge like his forward smash.
  - Jumping, both knees are drawn up neatly with the blade trailing back. His double jump is a tight forward somersault, the sword arm held out so the blade wheels round with him. (It used to show the dash-slash pose by mistake, since both play the same clip.)
  - He lands light, like a fencer.
- **The Rogue, after Sheik.** The Rogue's reference is Sheik from Super Smash Bros. Melee: a ninja, low, springy and quick.
  - Standing, the Rogue holds a very low, wide crouch, front foot forward, pitched toward the prey with the eyes up. The weight rides on the balls of the feet in a constant springy bounce. The front hand is raised open by the face with its dagger up; the rear hand is low behind the hip, its dagger trailing.
  - Running, the body is pitched hard forward in long strides, both arms swept back behind with the daggers trailing.
  - Strikes are short and snapping: they shoot out from the crouch to full reach and spring straight back, while the other hand snaps up to guard the face.
  - Jumping, the Rogue balls up tight with both arms swept back.
  - The dagger throw is Sheik's needles: both arms crossed over the chest in a crouch, then flung out together, fanned, the body springing forward behind them.
- **Everything else.**
  - Running, every hero leans forward from the feet, the way a runner balances while driving against the ground. The whole body tips forward: the pelvis and everything above it pitch with it (`Body.Pitch`). The legs are turned back against that pitch, so the solved feet stay planted, and the spine bends on top of it.
    - The lean is about 5° at a jog and about 15-19° at a sprint (plus the spine's own bend), steeper still in the first strides out of a stand, while the body is accelerating.
    - Braking tips the body back behind the leading foot.
    - The head stays up with the eyes ahead, however far the body pitches into it.
  - A start drives off the back foot.
  - Throws and casts step the weight back on the draw and forward behind the release.
  - A blow rocks a hero hard, but the weapon stays up (the Warden's shield stays between him and it) and the body comes straight back.

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
  lying along each great chamber's length, short and stout (`Tune.Fossils.RibSpacing`; `--scenario=ribs`): modelled on real whale skeletons (`DecorMeshes.Ribcage`): the spine
  runs overhead with a tall, back-leaning spine on each vertebra, each rib standing on a flat plate out from its vertebra, going out, bowing
  into a barrel and curling in, swept back toward the tail; ribs are flat tapering blades with a knobbed head, longest and most bowed a third
  of the way along and shorter, straighter and thinner toward the tail, so a hero walks through the cage between them, skulls sunk in the walls and old bones underfoot.
- **Water and lava** (`Render3D/Liquid3D.cs`): a refracting, depth-absorbing water surface with
  an animated waterline and volumetric fog under water; emissive lava that lights the cave, with a
  heat haze above it.
- **Lighting**: AgX tonemapping with a contrast curve on top, SSAO, glow, thin volumetric fog, a
  light per biome, the hero's lantern, back walls kept darker than the play layer, and a key and
  rim light of their own for every creature, so they read against the rock however dark the cave
  gets.
- **Ink outlines** (`Render3D/InkOutline.cs`, `Shaders/ink_edge.gdshader`): a screen-space edge pass, with no
  second body or hull. A small viewport beside each 3D view has a camera that follows the view's camera and
  sees only the creatures' bodies (their own visual layer); the creature shader answers that camera with a
  flat mask (how far off the creature is there, and the colour of its line) instead of its usual look.
  One full-screen pass in the view finds the pixels just outside that mask and inks them, two pixels wide by default at any distance
  (the character borders setting, the shader global `ink_actor_px`), in the afflicted colour (poison, fire, frost) when there is one. Nothing is drawn over
  a creature itself, so there are no lines along its own limbs and folds; a creature behind rock gets
  none where it is hidden, and the line never lies over rock in front of it. (The camp and the title
  portraits have their own.) The mask is made at the size of the view's own picture, **not the window's**: when the window isn't 16:9 the picture
  is kept to the content's shape with bars beside it, and a mask at the window's size was a different shape, which put every outline off the creature
  (checked with `--resolution 1600x700 --lookshot=...`).
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
  The other games build the same level from the host's seed (the chests, the vault's gate and
  the hidden keys placed while building come from the seed too, so they match) and show copies
  (puppets) of the host's creatures, updated twenty times a second and shown a tenth of a second
  behind, smoothly interpolated, with the host's animation clip and frame.
- **Each game runs its own hero**, so it handles exactly as it does alone; the others see a
  puppet of it (its movement, animation, shield, staff glow and blade sweep).
- **Blows cross over.** A blow on a copy of a creature is sent to the host, which applies it
  (its hex, freeze and other effects too). A creature's blow on another player's hero is sent
  to that player's game, which takes it with its own dodge, shield and invulnerability deciding
  what gets through.
- **Things the host's creatures make** (thrown rocks, shockwaves, falling stones, lava, spore
  clouds, dropped hearts, potions, keys and experience, chests and exits) are sent as they
  appear, and every game runs its own copy; a copy only ever hurts its own game's hero.
- **Effects and sounds** made by the host's creatures and by each hero are recorded as they're
  made (`FxLayer`, `SoundBank`) and replayed in the other games, so everyone sees and hears the
  same fight.
- **The host decides** who opens a chest, who picks up a key (it goes to that player's game),
  whose key opens a vault's gate (it's spent in that player's game, and the gate opens in every
  game), when everyone is at the same exit (and then sends the next level's seed), and when the
  run is over. Experience is shared.

## Test harness

The harness takes command-line user args after `--`:

```
# 12 seeds per biome: generation time, retries, trap cells (at most 6), a reachable exit, and a
# sound vault (sealed but for its gate, with a walkable way up to the gate; it exits 1 if any
# level but the dragon's lair lacks one); saves a map image of each biome (add --biome=NAME for
# just one; --genverbose prints every attempt, and --genimage=SEED saves that seed's map, or that
# attempt's with --genverbose)
godot --headless --path godot -- --gentest
# (--gencount=N makes N seeds per biome; --genprof prints where the generator's time went; set
# GEN_LANES=1 to make caves one attempt at a time instead of on every core)

# The autopilot bot plays for 60 s and saves a screenshot every 3 s
xvfb-run godot --path godot --rendering-driver vulkan -- --autotest --seed=1013 --duration=60 --shots=/tmp/shots

# Spawns one of every creature and screenshots it (on land, underwater, then the boss)
xvfb-run godot --path godot --rendering-driver vulkan -- --seed=1013 --bestiary --shots=/tmp/shots

# One screenshot after N frames (with --fixed-fps for a steady clock); --fxtest=K lays out one of
# every effect K frames before the shot, --proptest one of every prop (a key and a vault's chest
# among them), --elementtest the
# Elementalist's spells (a blizzard, a firestorm, an updraft, bolts, a frozen and a burning goblin),
# --roguetest the Rogue's (a dagger in flight, one stuck in a golem, a smoke cloud with a goblin
# lost in it), --exittest the two exits
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
magma, lair, catacombs, mine, lavatubes)
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

`--herotest` (add `--hero=warden`, `--hero=vitalist`, `--hero=elementalist` or `--hero=rogue`) scripts checks of each hero's mechanics
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
- Vitalist: the drain striking as the staff comes forward and its mote paying back a tenth as vital force
  on arrival; the hex, and hexed creatures taking more damage; the reserve's size; the heal's
  cost and amount; no heal when no one is hurt; the rupture seizing its target, then bursting on
  it and splashing a creature beside it but not one far off, and refused without the vital force;
  Many Mouths draining a second creature; Twin Reserve's two heals in a row; holding the attack
  draining again and again.
- Elementalist: a firebolt's damage and its cost (none); one that catches setting a creature
  alight, and the burn a second; holding the attack casting again and again; alimus coming back
  by itself; the updraft rising at your feet for its alimus, lifting nothing but letting a jump float far higher and a fall drift down slowly; an updraft cast with too little alimus going up for nothing and then waiting, paying and recharging once there is enough; the blizzard landing on
  the creature you aim at, striking nine times for its damage, waiting out its cooldown, and its
  frost freezing a regular creature solid; a snap shattering a frozen creature and splashing the
  one beside it, for its alimus; a snap with nothing frozen costing nothing; a mini-boss never
  freezing.
- Rogue: sliding down walls and kicking off them from the start; a jab striking the nearest
  creature only, five a second when held; a critical jab twice as hard; Backstab from behind but
  not to the face; a thrown dagger's damage, sticking in a golem, the jabs slowing with one out,
  both coming home by themselves once both are stuck, and a throw at nothing coming back; recall
  tearing a dagger out of a goblin for its damage and yanking it toward you; vanishing (a goblin
  losing you, half again the speed) and coming out of it on attacking, even at nothing, or on
  being struck; Surprise Attack's jab and throw four times as hard, and only the first; Cruel
  Edge, Weighted Daggers, Rending Recall, Quick Fade and Deep Shadows.
- For all: the game clock never slowing for a hit-stop.

`--hitstoptest --shots=DIR` has the hero strike a golem twice and logs every frame of it (who is
frozen, for how long, the clip and frame), saving a screenshot of each.

They stand in one known cave (seed 1013 unless `--seed` says otherwise): the lie of the land
round a random start could tip a check. The checks are timed in game seconds, so run them with `--fixed-fps 60`. They need no window:
`godot --headless --path godot --fixed-fps 60 -- --herotest --hero=warden` runs in well under a
minute, where a software renderer would take many (with a window, use a small one,
`--resolution 480x270`).

`--alttest` (with `--hero=...`) runs the same way through the hero's alterations: Relentless Charge
carrying a whole combo, Swift Heave in the air, Counter Roll stopping a club and answering it (but
not a blow from afar); the Unyielding Shield stopping 70% and never breaking, Braced, Guardian's
Charge wrapping you in a barrier that soaks a blow whole and the rest of a bigger one before
wearing off, Deflecting Bash sending a shot back without stunning; Blight Burst's damage, Endless
Hex spending vital force instead of a cooldown, Slow Mending's half now and half later, Warding
Mending, Lifebloom healing you and its Healing Pool; Frostbolt's damage, chill, quicker casting
and freezing, the aimed updraft, Firestorm setting creatures alight
as it strikes, and Cinder Snap bursting a burning creature and splashing the one beside it;
Ricochet striking two golems and marking them, never sticking in one, the recall's poison pins, and Chain Ricochet a third; Smoke Bomb hiding you
in its cloud (and not once you're out of it) and blinding a goblin in it, and Thick Smoke's wider
cloud; Tether pulling you to your dagger in a golem and striking it as you arrive, and Pounce's
sure critical.

`--upgradetest` (headless, a few seconds) checks the rules of the cards over thousands of rolls
for every hero: a chest holds one class card and two others, never an alteration or a
risk-and-reward card; a vault's chest holds two risk-and-reward cards (while any are left) and a
rare class card, never an alteration, and a party's vault may hold any of its heroes' rare
cards; a milestone holds only the hero's own cards, never a side-grade (only a Back to... card for one brought); an ability takes one
alteration, and an alteration's upgrades wait for it; the primary attacks have none and every
other ability has one; Wall Kick is gone; Unyielding Shield keeps out Quick Mend and Iron Timing;
Deep Lungs and Drowned Lungs rule each other out, as do Ricochet and Tether; Magma Skin waits
for depth 6; and a party's chest may hold any party hero's class card, locked to them. A hero
who has every card of their own already is offered none at a milestone (it heals 30 instead).

`--hero=warden`, `--hero=vitalist`, `--hero=elementalist` and `--hero=rogue` also work with `--autotest` and the other modes.

**Online play**: `tools/nettest.sh` runs two copies of the game without a window on one machine,
one hosting (`--nettest=host`) and one joining it (`--nettest=join`, `--netaddr=IP` for another
machine; `JOIN_HERO=elementalist` makes the joiner that hero, swordsman by default; a Rogue
joiner also throws a dagger into the host's golem and vanishes, and the host checks it sees both), and has
them check what travels between them: the lobby and each player's own hero,
the same cave in both games, a friend's swing landing on the host's creature (and credited to
them), a creature's blow taken in the friend's game, shared experience, a chest the friend
looks in and leaves (closed again in both games, with the same cards), which the host then looks
in (offered the very same cards) and takes from (spent in both games), a barrier and a warding
mending given to the friend's hero (held in their game, the barrier seen in the host's), a fallen
friend brought back,
going down an exit together into the same next level (a Fossil Graveyard, where each game's
camera must stay on its own hero), a key the host drops at the friend's feet going to the friend,
who opens the level's vault with it (open in both games, and the key spent), the run ending for
everyone, and then, back in the lobby, a
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
  menu, the three stages of the choice at the fire (hero, loadout & perks, difficulty), descending to
  depth 0, a death back at the camp (no creature left behind), and back to the menu, with a
  screenshot of each stage. `--campshot=DIR` renders the camp from the menu's
  view, from the fire with each hero chosen, and on the way to the cave.
- `--scenario=NAME` stages one situation and checks it: `water` (a spider and a bear swim after
  the hero and attack in the water; seed 1013 unless `--seed` says otherwise), `mouth` (the way out at depth 0: up doesn't take you,
  interact does, and nothing from the run is kept), `fossilcam` (the camera keeps the hero in view
  in the Fossil Graveyards), and `drain` with `--hero=vitalist` (a frame-by-frame picture of the
  drain's burst), and `magma` (the Magma Caverns hide their chests in the lava; lava burns a hero
  and throws them out, but with Magma Skin they swim in it for 30% of the burn, open a chest down
  there and swim back up; seed 2 unless `--seed` says otherwise), and `vault` (a Den level: its
  vault, a shut gate with a chest behind it, and its hidden keys well away from the start; the
  gate stops a hero, and interacting without a key leaves it shut; a key walked over is picked up,
  opens the gate and is spent; the vault's chest deals two risk-and-reward cards and a rare class
  card; a fourth key stays where it lies; keys go down to the next level; and the first mini-boss
  slain there drops a key). Add `--shots=DIR` for screenshots.
- `--musicdump=DIR` saves the three music loops as `.wav` files.
- `--padtest` drives the game with synthetic controller events: the main menu and the fire, move, swing, Charged
  Strike, dodge, the heaving swing, pick an upgrade from the level-up cards, pause, move through
  the pause menu into the settings and back out, unpause, swing with the right stick, dodge with
  LB, and look in a chest with LT, leave it, find the same cards again, and take one. Its presses
  are a twentieth of a second long, so run it with `--fixed-fps 60` too (it needs no window). It
  prints ok / FAIL for each step, then PASS or FAIL.


## The story, and the loading screens

`Core/Lore.cs` holds the story in one place: why anyone goes down into the Dagger (the river Wend ran into
the earth nine winters ago, and the valley above Hearthmere is dying of thirst), who went first (the
Deepsilver Guild's delvers, then the Guild's relief party, the Spring Order's pilgrims and the Crown's last
company, none of whom came back), why the heroes behind the cages are there (the creatures of the Deep cage the
living as a tithe for the Elder Dragon drinking at the Mother Spring) and why you keep coming back (whoever
dies wakes at the fire at the cave's mouth, kept alight by the embers of guardians). It is shown on **The
story** card (the main menu's third button, and automatically the first time a run is begun), as one line over
each biome's loading screen, and, for each caged hero, as a backstory when they are freed at the camp.

Going down shows the new level's loading screen at once (`UI/LoadingScreen.cs`: a drawn picture, a line of lore
and a tip for each biome; the old level stays frozen behind it) and builds the cave behind it on other threads:
the generator, then the terrain mesh, are made off the main thread and only the scene's nodes and the upload are
done on it. `--loadscreen` (with `--biome=`) holds the screen up, `--loadshots=DIR` saves one for every biome,
and `--scenario=loading` checks the order of events (screen up first, game paused underneath, level built, screen
gone).

Making a cave used to take two to five seconds; it takes under a second on average (a cap of about four): the
reachability checks run on flat arrays with reused buffers, the several attempts a cave may need are made side
by side on the machine's cores (judged in order, so the result is the same as one at a time) and stragglers are
told to stop as soon as an earlier attempt has made a sound cave.

## Can the guardian be reached? (generation)

After carving, a cave is checked twice. The first check works in 16 px cells and is kind to narrow
necks and tall climbs. The second, `Cave/FineReach.cs`, is strict: a 13 x 26 px body (the grid's
outline, plus the body's true width and head tested point by point in the world, so a neck between two
grid points can't pass for open), the slowest hero's jump (no double jump, wall jump or updraft), walking up
slopes, falling, and swimming, flooded over a 6 px grid. Where it can't get from the start to the
guardian it repairs the first break in the way (opening out a neck, putting stepping stones across
a gap or up a climb; ice ledges in frozen caverns) and tries again, then starts over with another
seed if a few repairs don't do it. `--gentest` reports how many caves pass (`fine n/12`).

## Difficulty

Two sliders, set where heroes are chosen (the host's rule online): **Difficulty** (0.5 to 2.0) and
**Per-player difficulty** (1.0 to 2.0). Enemy strength is `difficulty x (players x per-player)`: 1 on
your own at the defaults, and 2 x (6 x 2) = 24 at the top of both with six players. Health takes all of
it; damage a fifth of the way (`Tune.Difficulty.DamageShare`). Bosses and guardians grow with depth by
the 0.7 power of the usual curve, so deep ones don't drag. The caves hold about 30% of the old crowd
(`Tune.Spawning.SpawnShare`), each creature has +50% health on top of the doubling, and kills give
3x experience. Upgrades and relics marked PARTY (threat, a friend's burden) are only ever offered in a
party. A party is at most six heroes.

## HUD tooltips and the HUD guide

Every piece of the HUD says what it is as it is drawn (`Hud.Tip`, `UI/HudTips.cs`): health, experience and level, the milestone notice,
breath, every ability square and cooldown ring (the support too), the class's resource bar (shield, vital force, alimus, fuel, daggers), the
effects on you, potions and keys, relics (each with its own name and description), the depth line, the guardian's bar, the map and what the
journal pages have left you with.
- **Tooltips**: rest the mouse on a piece for a moment and a box explains it. Settings > Controls > **HUD tooltips**: *Off*, *Abilities,
  potions and relics* (the default) or *Everything on the HUD* (`GameSettings.HudTips`).
- **The HUD guide** (F1, or **HUD guide** in the pause menu): every piece outlined and numbered, with a list of what each number is in the
  middle of the screen; any key or button closes it (back to the pause menu, if it was opened from there). Works with a controller too.
- Test: `--scenario=hudguide --hero=vitalist` (checks the pieces are marked and shoots the guide).

## The menus' look

No menu is a flat box of one colour any more, and no corner is square (`UI/UiArt.cs`, all made by code):
- **Surfaces**: tileable textures generated at start: dark linen (panels), slate, wooden planks, crimson velvet with a damask lattice,
  hammered gold, veined vault ashlar, a night sky with stars, leather, crystal facets, green vellum, violet marble, brushed iron.
- **What the choice is** shows in the mat the cards lie on: wood for a chest's treasure, velvet for a relic, a night sky (with slow rays)
  for a milestone, hammered gold for a guardian's hoard, veined stone for a vault. Each card is printed on its kind's surface: leather
  (a hero's class card), crystal (an alteration), vellum (conditional), stone (generic), marble (risk-reward), velvet (a relic).
- **Rarity** is the frame: a plain iron band (common), bronze with studs at the corners (uncommon: a hero's ability cards), a double gold
  band with filigree curls and a diamond crest (rare: alterations, rare cards), rose-gold with a gem in each corner, a gem crest and a
  breathing glow (relic), violet with thorns (a bargain). The name of the rarity is printed under the symbol.
- **Whose card** it is: a hero's emblem in their colour at the top left (a sword, a kite shield, a drop of life, a flame, a dagger, a
  ward, a paw, a cog), or a compass rose for a card any hero can take. **What it's for**: a pill at the top right, with a glyph and a
  colour (OFFENSE crossed blades, SURVIVAL a shield, MOBILITY a wing, BARGAIN scales). **Mutually exclusive** cards wear a red chain
  ribbon naming what they can't be had with (either way round). Ranks are pips at the foot.
- The theme's panels and buttons are nine-patch textures (`UiArt.Box`): linen in a rounded gilt frame with studs, buttons with a soft
  sheen in a bronze rim (gilded when pressed). The build page, perk and meta trees, the hero cards at the fire, the camp's strip (framed
  in the chosen hero's colour), tooltips and the HUD guide use the same frames; the HUD's bars are rounded, shaded gauges (health keeps
  a pale ghost of what was just lost), its ability squares, icons, prompts and map are rounded.
- Look at them: `--menushot=DIR` (pause, settings, a chest's cards, a milestone's cards, the build page) and `--fronttest=DIR`.

## Stylised shading

Settings > Graphics > **Stylised shading** (on by default; the shader global `cel_shading`, `GameSettings.CelShading`, `--nocel` to
look at the old way) gives the creatures, heroes, cave rock and the camp a cel-shaded look:
- **Light in tones**: the creature, rock and camp shaders have their own `light()` (flattened light, a crisp small highlight), and the
  lit picture is divided into a few soft-edged tones by a full-screen pass (lighting tones, below).
- **Simpler colour** on the creatures: the grain and mottling of skin, scales and cloth turned down to a hint, the bump detail softened,
  and each colour drawn a little toward its tone band, so a hero reads as a few clear blocks (hair, scarf, leather, steel).
- **Painted rock** (with minimalist ground off): the photographed rock textures are sampled a few mip levels softer (no grain), their brightness eased toward a
  few soft tone bands, and their relief softened, so the cave reads as shapes and colour rather than photographs.

### Tuning the stylised look

Under **Stylised shading** in Settings > Graphics are six dials, each live (they are shader globals, `Render3D/Shaders/cel.gdshaderinc`,
so a change shows at once everywhere with nothing reloaded), and a preset that sets all six (Subtle, Painted, Cartoon, Bold, Poster,
Flat; moving a dial by hand makes it "Custom"). Two pairs of the same two dials, blur then tones, one pair for the textures and one for
the lit picture:
- **Texture blur** (0 to 4, default 0): how soft textures are sampled, in mip levels down (on creatures, which have no image textures,
  how much of their skin, scale and cloth detail is smoothed away, and their bump with it).
- **Texture tones** (not divided, or 2 to 12): a texture's brightness divided into that many soft-edged tones, in a perceptual scale,
  the hue kept (so even three tones leave a goblin green and a bone pale).
- **Lighting blur** (0 to 4, default 0) and **Lighting tones** (not divided, or 2 to 12): the same, after the lighting, on the whole lit
  3D picture (`Render3D/CelPost.cs`, `Shaders/cel_post.gdshader`: a full-screen pass under every 2D layer, hidden when neither dial does
  anything). The picture is blurred by that many mip levels, then its brightness divided into tones the way a texture's is: hue kept,
  each tone the middle of its range (so it is no brighter or darker on average), the darkest shades left alone (black stays black).
  With tones, highlights are small hard spots instead of a gloss. These replace the old per-material "light bands".
- **Band softness** (0 to 100%): how sharp the edge between two tones is (texture and lighting tones both).
- **Light flatness** (0 to 100%): the light's contrast squeezed toward its own average. Toward 100% a surface takes a light at one
  level on the side facing it (0.65) and none on the side away, with a soft edge (`cel_angle`), instead of shading smoothly with the
  angle; distance falloff and shadows stay, highlights dull. The flat level is set from what the game's scenes actually show, so the
  picture is never brighter or darker for it, only flatter (measured at 0% and 100%: the den -7%, the fungal caverns +1%, the
  Swordsman -5%, a goblin -1%). With lighting tones it reads as a cartoon (the Cartoon preset); without them, as plainer, cheaper
  lighting (the Flat preset).
`--cel=blur,tones,lightblur,lighttones,softness[,flatness]` sets them from the command line (for screenshots).

### Minimalist ground and borders

The look aims at Castle Crashers and Cuphead: flat fills, simple forms, ink borders round things.
- **Minimalist ground** (Settings > Graphics, on by default; the shader global `terrain_solid`, `GameSettings.SolidTerrain`, `--photo`
  for the photographed textures): the cave rock is two flat colours, grass on every face you could stand on (`ground_up`: its up
  component over 0.6) and the biome's wall colour (its `Edge`, brightened by `wall_gain`) on every other; snow instead of grass in the
  frozen caves, a darker wall under water, the lava's glow over it. The polygons are not smoothed: each face is lit by its own direction
  (from the screen-space derivatives of its position, not the mesh's smoothed normals), so every corner of the rock shows, and each face
  takes a slight shade of its own (`facet_shade`) so two neighbours never quite match. The grass's edge follows the polygons. The camp
  does the same: all-grass meadow, grey cliff stone with grass on its upward faces, every face flat; the rubble and ledge stone drop their
  photographed grain (`PropViews.RestyleRock`).
- **Dirt**: the wall colour is darker than it was (`wall_gain` 0.8), and the dirt behind the play plane is darker still (`back_dirt`: half the
  foreground's brightness, fading in from z = 0 back to z = -3). It has texture again, foreground and background alike (only the brightness differs), still flat colour on flat faces: grain and grit,
  and lumpy stones (a Voronoi cell each, `stone_size` 0.9 m, its own shade, darker crevices between them, and small stones within), drawn on whichever way the
  face looks; the grass gets its own mottle and a few pebbles.
- **Grass and roots**: the cave's tufts are bigger (taller, thicker blades, fewer per tuft) and about a fifth as many; the camp's
  meadow likewise (1,400 big tufts instead of 9,000). Dangling roots are 2.6x thicker and longer, a quarter as many.
- **Camera** 10% closer (`Tune.Feel.CameraZoom`; since tuned to 2.502).
- **No borders inside a texture**: grass meets wall with no line between them.
- **Object borders** (Settings > Graphics, 0 to 4 px, default 1; the shader global `ink_terrain_px`, `--ink=terrain,characters`): a thin
  ink line round the outline of every 3D thing (`Render3D/EdgeInk.cs`, `Shaders/edge_depth.gdshader`): a full-screen pass that finds
  where the scene steps away behind something (the depth on one side farther than here by more than 6% of the distance, more than a slope
  would explain, the other side not nearer) and inks the nearer thing's side. Widths are in pixels at 720 lines, scaled with the
  resolution.
- **Ambient light only** (Settings > Graphics, on by default while the look is being settled; `GameSettings.AmbientOnly`, `--lights` for
  the old lighting): every light in the cave and the camp (the directional key, fill and rim lights, the sun, torches, spell, fire and
  pickup lights) is muted, so nothing is shaded by direction and shapes read by colour, facets and borders alone (`Render3D/AmbientOnly.cs`:
  each light's cull mask goes to 0, its shadow and fog glow off, checked every frame since the game turns lights up and down; switching
  the setting off gives each back what it had). The ambient makes up for it: near white (the biome's light tints it 20%) and equally
  strong in every biome, and the same at the camp instead of the sky's blue. Metal, with nothing lit to mirror, is drawn mostly as its own
  colour (metallic down to 15%: the shader global `light_ambient_only` in the creature shader, `AmbientOnly.Metal` for the steel, gold
  and iron materials). The menu portraits keep their lights.
- **Character borders** (0.5 to 6 px, default 2; `ink_actor_px`): the heroes' and creatures' line (`InkOutline`), a little bolder, drawn
  over the object border.

### Debug mode

Typing `debug` at the character select screen (the Character stage) turns debug mode on, and typing it again turns it off (it isn't saved: type
it each launch). While the letters are typed they don't do what their keys do (D would step the hero, E carry on): the first D is held back
and, if the word turns out not to be typed (another key comes, or a second passes), its step is made after all (`Main.Debug.cs`). A badge
at the top left shows while debug mode is on, with a **Look settings** button; the same tab is in Settings (main menu or pause menu).

Settings gets a **Debug** tab (`UI/SettingsMenu.cs`, `Core/DebugTuning.cs`) with, all live:
- the look's switches (stylised shading, ambient light only, minimalist ground, object and character borders) and the stylised dials (preset,
  texture blur and tones, lighting blur and tones, band softness, light flatness). In normal settings only the switches and borders are
  shown;
- **Foreground** brightness and contrast (the ground at and in front of the play plane, and the camp's meadow and cliff), **Background**
  brightness and contrast (the ground behind it, fading in from the play plane to 3 m back), the **background dirt's darkness**, and a
  **background blur** (a depth blur of everything behind the play plane, with how many metres behind the plane it starts);
- **Debug window opacity** (default 40%): on the Debug tab the settings window is see-through and the screen is not dimmed, so every change
  can be watched behind it (at the character select the camp shows, with the hero strip stepped aside; opened from the pause menu, the
  paused cave);
- **Characters and creatures** brightness and contrast;
- **Red, green and blue** gains (0 to 2, 1 = unchanged) for **every distinct material**: characters and creatures (with their brightness and
  contrast), dirt, grass, stone, snow and ice, wood, metal, roots and bark, bone, the camp's trees, and for the **whole picture** (a full-screen
  pass over everything lit in 3D, under the HUD: `CelPost`, which also shows whenever one of these is off 1). The shader-drawn materials (the
  cave's dirt, grass and snow, the camp's meadow and cliff, the grass blades) read them as shader globals (`dbg_m_*`); standard materials are
  registered with `MaterialGains.Tint` (wood: the chests' planks and the camp's logs; metal: steel, gold and iron; stone: the rubble, ledge
  and boulder rock and the camp's far mountains) and have their albedo colour multiplied live. The photographed-texture ground (minimalist
  ground off) isn't covered;
- **3D shape and transitions**: **polygon smoothing** (0: every polygon flat; 1: smooth shading across them, and the grass test follows the
  smoothed normal, so its edge stops following the polygons), **grass/dirt transition softness** (0: a hard edge; up to a wide gradual blend),
  the **foreground-to-background fade** distance (0.3 to 12 m: how far behind the play plane the foreground look gives way to the
  background look, for both the back dirt's darkness and the background adjustments) and **crevice softness** (the cracks between stones);
- **Scene effects**: cave haze, ambient occlusion and bloom strength (times the usual), exposure, camera **field of view** (the framing stays
  the same, only the perspective changes), **outline sensitivity** (how big a depth step makes a border; lower: more) and **border opacity**;
- the dirt and grass: dirt colour gain, grain, stone shade variation, crevices, stone size, facet shade, how much the grass takes the biome's
  growth colour, and the steepest face that still counts as ground;
- the cave's **ambient light** strength and biome tint, and the **camera zoom**.

Brightness and contrast are made in the display's perceptual scale (`dbg_adjust` in `cel.gdshaderinc`). Contrast works about the **average
colour** of what is drawn, not about grey: at 0 every colour becomes that average (a flat wash of its own colour), at 1 nothing changes, above
1 colours spread away from it. The ground's average is its dirt and grass in the shares they take on screen (25% grass), separately for the
foreground and the darker background dirt; a creature's is the mean of its mesh's vertex colours (`body_avg`, set in `CreatureModel`);
the camp's is its grass, and its cliff's its stone with a little grass. Brightness multiplies on top, then the red, green and blue gains. Every tuning value is a shader global
(`dbg_*`, in project.godot) except the ambient light, blur and zoom, which the code reads. They are saved with the settings (the `[debug]` section), so a look found in debug mode stays across launches.

**Two looks.** The title screen (the camp behind the main menu and the character select) has a look of its own, swapped in whenever the camp
is on screen (`GameSettings.SetLookProfile`, from `Main.ShowCampScene`) and out when a run starts. Its defaults are more like the original:
no stylised shading, no tones, the sun and sky lighting (ambient light only off), the photographed cliff rock, the soft two-green meadow, no
object borders, the characters' line at 3.5 px, and every colour and brightness gain neutral. The cave keeps the tuned look. Each look
keeps its own settings and debug tuning values (saved under `graphics` / `debug` for the cave and `title_look` / `debug_title` for the
title screen): the Debug tab edits the one on show (its first line says which; open Settings from the main menu for the title screen's, from
the pause menu in a run for the cave's), and the values list shows both. Reset puts the look on show back to its defaults
(`LookSnap.TitleDefaults` / `CaveDefaults`, and `TitleDefault` / `CaveDefault` on each tuning value).

The camp's meadow has more grass texture (a tufted mottle, fine blade strokes and pale flecks, in `camp_ground.gdshader`) and far fewer tall
clumps (380 and 55 dry ones, from 1,400 and 260).

The box at the bottom of the tab lists **every value** (a `*` marks one changed from its default); **Copy all values** puts it on the
clipboard (and in the log), and **Reset everything to defaults** goes back. To make a tuned look the default: change `Default` in
`DebugTuning.All`, the `LookDef` constants in `GameSettings`, and the matching `dbg_*` / `cel_*` entries in project.godot's shader globals.
The cave's defaults now in the code are the look tuned in debug mode (the second round: the lighting without tones at 95% softness and no
flatness, texture blur 0.2, brightness 140%, dirt gain 1.69, grass fully taking the biome colour, polygon smoothing 84%, outline sensitivity
0.019, a warm whole-picture tint, field of view 61, camera zoom 2.502), in `DebugTuning.All`, `GameSettings.LookDef` and project.godot's
shader globals. The title screen's defaults are its own (see Two looks).
Tests: `--debug` starts with debug mode on, `--dbg=key=value,...` sets tuning values, `--scenario=debugmode` types the word and checks it.

## Wind-ups (casting delays)

Some casts gather for a moment before they go off, to give them weight (`Player/Player.Hold.cs`; the lengths are in `Tune`): the Vitalist's
**heal** 0.4 s (`Vitalist.HealDelay`) and **Rupture** 0.8 s (`RuptureWindup`, its seizing), the Aegis's **shields** (barrier and bubble) 0.25 s
(`Aegis.ShieldDelay`), the Elementalist's **firebolt** 0.2 s, **frostbolt** 0.1 s, **Blizzard** 0.5 s (`Elementalist.FireDelay` / `FrostDelay` /
`BlizzardDelay`) and **Stalag-Might** 1 s (`Support.StalagDelay`), and the Rogue's **recall** 0.4 s (`Rogue.RecallDelay`).
- The cost is paid and the cooldown started at the press. Then, for the wind-up, the hero is **slowed to a tenth** of its speed (`Tune.Hold.MoveShare`: walking,
  swimming and falling alike, so the weight is felt without being frozen): no jumping, no dodging, no new attack or ability. The hero is animated in the cast
  (`SpriteAnimator.OnceFor` stretches the clip to the wind-up's length) with a gathering glow; then the cast goes off, aimed where the aim
  and the nearest creature are by then.
- A stun, freeze, web, snag or death cuts the wind-up short and the cast is lost.
- The Rogue's dagger throw and slash have none. The **throw** now does 60% of a dagger slash (`ThrowDamage = Damage * 0.6`, was 120%) and the
  **recall** 60% as well (was 150%), so he is no longer best at range.
- `--scenario=holds --hero=NAME` (vitalist, elementalist, aegis, rogue) checks each wind-up's length, that walking was pushed the whole time
  and the hero crept at no more than a tenth of its speed, and that the cast went off after.

The Automaton's clinking now sounds only while it is **building** pressure; once pressurized it stays quiet but glows as brightly as it did at
the end of the build-up (shaking a little, with steam and sparks).

In menus the left stick steps **once per push**, then (held) pauses 0.4 s and repeats every 0.12 s, like a held key (`UI/MenuStick.cs`). The
engine's own stick bindings on `ui_*` stepped on every frame, and so did the hero choice at the fire, which read the stick through
`move_left` / `move_right`: it now takes only key and button presses from those (`Main.StepKey`), and the stick's one step comes from
MenuStick. `--fronttest` pushes a stick that reports six times and checks the hero changes by one.

## Secrets left of the entrance (the Underground River)

Under the beach where you arrive, to the left of the entrance, where nobody thinks to look, a level of the river now and then (60%,
`Tune.River.LeftSecretChance`) hides a **slit in the face of the sunken beach**, marked by escaping bubbles: a swim in, with the current,
along a tunnel under the beach to a chamber with a **silver chest**; and, 40% of those (`SecondSecretChance`), a **second chamber** deeper, down a
narrow shaft from the first's floor, with another (`Cave/RiverGen.cs`, from a generator of its own so the rest of the level is unchanged).
The nooks' pass no longer wipes the river's own slit marks (`CaveData.OwnHints`). `--leftsecrets` makes every level have two;
`--scenario=river --leftsecrets` checks the chambers, chests, marks, the swim in and the shaft.

## The Shape Shifter's staff

The staff is held like a **staff**: a crooked wildwood shaft about 1.5 long, **gripped about its middle**, the foot showing behind the fist as far as the antler-tined head stands
ahead of it (it used to be held like a sword, by its end). Its reach follows: `Tune.Shifter.StaffReach` is **52 px**, down from 60, which is the staff's length ahead of the fist
at the sword's scale (the sword's 68 px is its blade's length). The cuts are still a sword's. (`HeroDesign.cs`.)

## The Shape Shifter remembers a form

A Shape Shifter can **remember** the form it is in and take it again **without a creature to copy** (`Player.Shifter.cs`: `TryRemember`, `TryRecall`):
- **Remember**: in a form, the **dodge button** (a creature has no roll) commits the form to memory (`PlayerStats.RememberedForm`, kept for the run; one form at a time, 1.5 s between). The HUD under Shift says which.
- **Recall**: out of a form, the **second button** takes the remembered form at once (it spends the Shift ability's use, as a Shift does), and so does **Shift itself when nothing can be copied near**
  (nothing about, or only what can't be copied: a guardian or a boss). With a creature that can be copied near, Shift copies it as before. So a **bear can be had against the last boss**.
- **Tanking**: the heavy forms now draw the creatures' eyes (`ShiftForm.Threat`: they count a hero in the form that much nearer; the bear 0.5x, the golem 0.6x, the crab 0.8x), and the bear shrugs off
  35% of blows (was 20%; the golem 40%). More for the party is yet to come. `--scenario=shifterspecials --hero=shifter` checks remembering, recalling, and Shift still copying.

## The meters under the health

The thin meters under the health bar (`Hud._Draw`) each get their own row with room between, and show only while they mean something: a **Warden's shield** while it is raised, worn, broken or mending; a **barrier or bubble** while one is up; and the **Elementalist's alimus** while it is short of full or a spell waits on it (with marks at the updraft's and the storm's costs). Each row's amount stands just past its end, and the experience bar sits under the last of them. The alimus bar that stood among the Elementalist's ability squares is gone (its bolt pips stay there).

**Fire takes more readily**: a firebolt sets a creature alight 65% of the time (was 40%), each Firestorm strike 25% (was 10%).

**Fixed**: the Rogue's Recall had stopped working (the Shape Shifter's new "recall a remembered form" shared its name, so the Rogue's button called the Shifter's); the merchant and his stall were drawn at a third of a hero's size (now 1.6x).

## More gold, and gold from creatures

**Gold where something ought to be** (`Main.PlaceMoreGold`, `Tune.Gold`): beside the dead ends, coins and piles are laid, from the seed alone and after every other reward is down, on **high platforms with nothing under them**,
on **ledges, slabs and shelves that lead nowhere**, in **alcoves** (rock close on both sides, a low roof) and in **crevices raised out of the passage**, and never on the main floor; about ten places on a map of the usual size (more on a bigger one), 150 px apart, 110 px from any chest, cage, key, door or other gold,
and only where a hero can walk. Each place is scored by how hard it is to get to (the climb, how tucked away, how far from the start); the kinds take turns, hardest first, and the **hardest quarter hold the piles** (`Tune.Gold.PileShare`); a dead end's gold is coins (a pile only 8% of the time). **Creatures drop gold now and then** (7% a kill, 40% from an elite, a pile of 12 from a guardian; 12% of drops are piles, `Enemy.DieHere`): a coin that flies out and falls to the floor (`CoinPickup.Dropped`, sent to the party like any prop);
a hive's hornets and everything in the merchant's stall leave none.

**Wind-ups pause your rise and fall, they don't reset it.** A wind-up slows you to a tenth, in the air too (you hang there while it gathers), and when it ends the rise or fall you were in carries on as it was (`Player.ResumeFromHold`). A blow that lands still stops any rise or fall, as before.

## The merchant and his stall

**Finding him.** The merchant is another character to unlock, but not a playable one. While anyone in the party hasn't met him (`Meta.MerchantUnlocked`, a bit of `PeerInfo.Locked` online), some of the hero cages hold **him**
(`Tune.Merchant.CageShare`, 35% of cages, all of them once every hero is found): free him (interact) and he is yours for good (`HeroCage.Merchant`, `Lore.MerchantFreedBanner`). While everything is unlocked for testing he is always known.

**His stall is a third exit, twice a run** (at two depths drawn as the run begins, the first no sooner than the 1st, the second no later than the 9th, `Main.RollStalls`, `Tune.Merchant.FirstDepth/LastDepth/Visits`; a steep way down that skips one's depth brings it at the next guardian, never past the 9th). (a door in the gold of his lamp, marked THE MERCHANT, with a coin on its keystone and "stays at depth N" under it, rather than the biome's name). Once he is known, a fallen guardian leaves three exits, left to right: *the gentle way* (a level down), **the merchant's stall** and *the steep way* (two down) (`Main.PlanExits`). The stall does
**not** descend: it is the *same biome at the same depth*, in a **tiny new map** (`BiomeDef.ForShop`, `Biomes.ShopOf`; the biome's palette and dressing, in a map of two chambers about 105 cells wide) in which
nothing lives, nothing is hidden (no chests, gold, vault, rubble, secret ways) and the way on is open from the start: two exits, one level down and two (the way a guardian-less level opens them), and no second stall. In the exit chamber
stands the merchant at his table (`Combat/Merchant.cs`, `MerchantDesign`, `MerchantView`; a lantern, a pack, a plank table with wares); arriving heals nothing (the level below does). Online, the stall rides on the same message and portal sync as any level
(a shop flag).

**Trading.** Stand at him and press interact: the cards he has are dealt for you (`Main.Shop.cs`), each with a **price in gold** on a tag under it; a card you can't pay says how much more you need. What he has (`Tune.Merchant.Cards/Supplies`, so the table stays small): **two cards** (a class card and one other), **a relic**, and **two supplies** drawn from a potion for the belt, provisions (mends 40% of your health) and a **token of Second Thoughts** (one more time a choice of cards may be dealt again; a little likelier than the others). Buying takes the card at once and lays the table out again without it;
**Deal Again** shuffles the cards he hasn't sold (6 gold, a half more each time, the supplies stay); **Walk Away** closes the stall. Prices are by rarity (8, 14, 24 and 36 for a relic, rising 8% a level down).

**Dealing a choice of cards again.** A chest's cards or a milestone's can be **dealt again** (R, a controller's Y, or the button under the cards) as many times as the hero has charges (`Player.Rerolls`): the Path's **Second Thoughts** I-III give 1, 2 or 3 a run,
and the merchant sells tokens. A chest's new cards are the chest's from then on, for any friend who looks in after.

**Gold relics.** Fat Purse (piles hold 3 more coins), Overstock (one more thing on his table), Haggler's Tongue (20% cheaper), Loaded Dice (the first deal-again at each stall is free), Fence's Ledger (dealing again 40% cheaper),
Regular Customer (+15 gold on reaching a stall), Peddler's Friend (his provisions are free), Guardian's Purse (+20 gold per guardian) and Compound Interest (+10% of your gold each level you go down).
**Unlocks** (now on the Ember Grid: the merchant's branch has **Fair Dealing** I-III, his prices 5/10/15% lower; the Path's has **Prospecting** I-III, gold found worth 10/20/30% more, and **Second Thoughts** I-III). `--scenario=shop --hero=swordsman` checks all of it.

## The Nest's hornet hive

Most Nests (`Tune.Hive.Chance`, 85%) hold a **hornet hive** (`Enemies/Hive.cs`, `HiveDesign.cs`, `Tune.Hive`, `Main.PlaceHive/RunHive`): a paper nest the size of a boulder, hung from the ceiling of one
of the level's treasure rooms (the place is from the seed alone; the host's game hangs it up when a hero first comes within 900 px, so online it reaches every game like any creature).
It strikes at nothing itself, but it is **tough** (130 health before the usual scaling, 13 hornets' worth) and it **keeps three hornets about it**: all three come out when it wakes, and **about four
seconds after one is lost** (4 s give or take 0.4, each lost hornet counting its own time) a new one crawls out of its mouth. A hornet of the hive drops no experience, hearts or potions
(`Enemy.Brood`), or it would be a spring of them. **Destroying the hive leaves a chest of upgrades** where it fell (and the hornets already out stay, but are not replaced). The Nest's residents also
lean harder on **spiders**: on the ceiling they are five parts in six of the table, and on the floor they share it equally with the scorpions. `--scenario=hive --hero=swordsman`.

## The Swordsman's dash-slash

The dodge roll is gone: the Swordsman's dodge button is a **dash-slash** (`Player.Swordsman.cs`). A dash of 560 px/s for 0.28 s, in any direction in the air or water and along the floor on the ground,
in an **orange ghost-glow** (a trail of orange afterimages and embers, where the roll's was blue). The first creature it touches stops it and is **slashed for 1.5 times a normal strike**,
with everything a strike brings (the same swing logic runs: bleed, life, a charged strike's weakening, the combo refund and hit-stop). Touching nothing, it just carries him a good way: forward or
away, so it can be used defensively; it makes no one invulnerable of itself (Phantom Step, "Dashing makes you briefly invulnerable", still does, and Counter Roll is now Counter Dash). It takes about
**15 s** to come back (the roll took 0.55 s; Nimble, Second Wind's second charge, Wind Runner and Featherfoot Sash still apply). The Shape Shifter keeps the plain roll. `--scenario=dashslash --hero=swordsman`.

**Dash Through** (Swordsman side-grade for the dodge button; `Stats.DashThrough`): the dash no longer stops at the first creature it touches: it carries him **through** it, and when he is past it (or the dash ends) he turns and
slashes it **from behind**, for the same 1.5x strike (the swing's lunge is held back so it doesn't draw him back through). The scenario checks both forms.

## Gold

Gold lies hidden in the **dead ends** of every cave, which used to hold nothing: `CaveGenerator.AddDeadEnds` finds the tips of the level's branches (a flood of the open
cells from the start, and the cells nothing nearby is farther along than; kept 14 cells apart, 26 cells from where you arrive, and out of the guardian's room, the vaults and
the hidden chambers, which hold a reward already) and `Main.PlaceGold` puts **one to three coins** along the floor of each, and one time in five **a pile of four to eight**
instead (`Tune.Gold`). A coin is worth 1; touching it puts it in the purse (`Player.Gold`, shown by a coin and a count beside the keys; it goes on down from level to level
with the hero, and `GoldFound` counts the run). From the seed alone, so every game makes the same; online, the host decides who gets one (`NetSync.GivePickup` kind 5), as with
a key. `--scenario=gold` checks it (every dead end holds gold, the worths, picking up). Gold is for a merchant's wares (to come).

## Shields on the HUD

Beneath the health bar, in a thin meter like it, **light grey (almost white)**: a **Warden's shield** (its amount of what it can still stop; the Warden's own gauge at the bottom
left stays too) and any **barrier or bubble** over a hero (the Aegis's, or a friend's). One row when there is one of them, two thinner rows when both; the amounts sit just
past the end of the health bar, and the tip says what each is. (`UI/Hud.cs`)

## Retaliation relics (for anyone)

Two relics that answer a blow (`Player.StruckBack`, after a creature's strike gets through your shield and armour; numbers in `Tune.Relics`):
- **Cinder Brand**: when a **boss, guardian or mini-boss** strikes you, **20%** chance it is set alight (4 a second for 4 s, times your damage).
- **Hoarfrost Mail**: when a **regular creature** strikes you, **15%** chance it is **frozen solid** for 1.5 s (a snap or a heaving swing shatters it).
  It catches the very creature that just struck, even mid-attack (`Enemy.FreezeSolid(..., force: true)`), where other freezes never do.
Each blow rolls for itself (a blow shared with friends rolls for each who takes a share). `--scenario=struck` checks both, and neither without them.

## Echoing Blade (a relic for the melee heroes)

When a melee blow **lands**, there is a **20%** chance (`Tune.Relics.EchoChance`) it **strikes again 0.1 s later for half the damage** (`EchoMult`), with everything a strike
brings: the same hit path runs again (`Player.OnSwingHit(..., echo)`), so bleed, life steal, a charged strike's weakening, a Rogue's critical, a heaving swing's shatter and
the kill all happen again. What belongs to the swing, not the strike (the combo refund, the hit-stop and the pauses), is not repeated. Weight 0.8.
`--scenario=melee --hero=swordsman` checks it (certain echo: x1.42 of a plain blow's damage, the damage's own spread aside).

## Co-op relics (uncommon; only offered to a party)

Three relics for a party, at the ordinary relic weight (the two retaliation relics above are *rare*: weight 0.6, likelier to be passed over):
- **Last Rites**: when you fall, **every living ally is healed by 10** (`Player.FallHealing`, through the usual healing multipliers, so a little under 10 with the base healing multiplier; the HUD ring shows who).
- **Phoenix Ash**: when a friend gets you back up, **you rise with 20% more health** (35% of max health becomes 55%).
- **Healer's Hands**: when you get a fallen friend up, **they rise with 20% more** (sent with the revive message online: `NetSync.Revive(target, bonus)`), and the two stack (75%).
`Tune.Relics.FallHeal`, `ReviveExtra`, `ReviveBase`. `--scenario=struck` checks them (a revive can't happen alone in the real game, so the test revives by hand).

## Hawk's Pursuit (a relic for the melee heroes)

A relic for the Swordsman, Warden, Rogue and Automaton (`relic_lunge` in `Player/Relics.cs`, "Hawk's Pursuit"): melee **lunges are twice as strong** and
last 1.6 times as long, and a hero who never lunged (the Rogue, the Automaton) lunges at least 100 px/s; **in the air, a swing pulls the hero after the
creature nearest its aim** (within 170 px and a wide cone, in the open) in any direction, and then **hangs it in place for 0.25 s** (no gravity), so
chained swings let a hero hover and hunt a creature through the air. The bargain: **hits are 10% softer**. A hero with a creature in the air still
lunges along its aim when none is in the way. Numbers in `Tune.Relics` (`LungeMin`, `LungeTimeMult`, `PursuitRange`, `HoverTail`),
`PlayerStats.LungeMult` / `AerialLunge`; `--scenario=melee --hero=swordsman` checks the swing without it, with it, the pursuit and the hover.

## Melee blows that land

A melee swing (the Swordsman's, Warden's, Rogue's and Automaton's) has **no delay as it begins**. When a blow **lands**, the hero's vertical
speed is **reset to zero** (a rise or a fall stops dead; a Pogo strike then bounces up as before) and, once the hit-stop is over, the hero
holds still for **0.1 s** more (`Tune.Feel.MeleeHitPause`; no walking, jumping or gravity; being hurt cuts it short).
`--scenario=melee --hero=NAME` checks there is no opening delay, the pause length, that the hero held still, and the vertical reset.

## Reporting a problem

- **The seed** is on the HUD (small, under the depth line) and in the pause menu, so any cave can be named and made again.
- **Report a problem** (the pause menu's button, or **F6** anywhere in a run; `UI/BugReportMenu.cs`, `Core/BugReports.cs`): it asks one
  thing, **what happened**, and the player writes as much or as little as they like. Everything else is collected behind the scenes, so the
  session is always known (`Core/SessionLog.cs` keeps it as the game goes):
  - **the session**: an id, when it started, how long the game has been open and how long was actually spent playing, runs and deaths so
    far, and every cave of the session (seed, depth, cave kind, hero, number of players, when, for how long);
  - **the party now**: how many players (online or not, host or guest), which heroes, their levels, HP and kills;
  - **the trail**: the last 90 things that happened (a run started, a cave entered, a pause, an unstick, a death, a win, the report opened);
  - **where**: the seed, depth and cave kind, the cell the player stands on, a **shut-in check** (a flood fill of the open air from the
    player: how many of the cave's open cells it reaches, and whether the start and the guardian's room are among them) and the **map** (a
    crop round the player in the issue; the whole map in the file: `X` rock, `.` air they can reach, `,` air they can't, `~` water, `^`
    lava, `P` player, `S` start);
  - **the machine**: build, Godot, OS, GPU, window size, frame rate; the cave generator's own diagnostics; every look setting (both looks);
    and the last 70 lines of the game's log.
- **Where it goes**: the whole report is saved in `user://bug_reports/` (on Linux `~/.local/share/godot/app_userdata/ProcGenGame/bug_reports/`,
  on Windows `%APPDATA%\Godot\app_userdata\ProcGenGame\bug_reports\`) and copied to the clipboard, and the browser opens a **new GitHub issue
  on `hogosnoki/claudecodetestgodotgame`** with its title (`[Report] <first line>`), the start of the body (the words; a table of the seed, cave, hero, place, build, session and party) and
  the label `bug-report` already filled in; the player pastes the whole report from the clipboard (Ctrl+V) and posts it. The address is kept under
  1900 characters (`BugReports.MaxUrl`): Windows hands a longer one to Edge instead of the player's default browser. Posting needs a (free) GitHub
  account; without one, "Show the saved file" opens the reports folder, to send the file any other way. Reports are then issues, so they can be listed, read and closed from
  GitHub (or by a session with GitHub access) while the game is changed. The game carries **no GitHub token**: one inside a game anyone can
  download is one anyone can take, so the player's own account posts it. (Posting with no click would need a small server of the owner's
  holding a token, which this repository doesn't have.)
- **A shut-in start warns by itself**: when a cave begins with the start's air joined to under 400 open cells, or to nowhere near the
  guardian's room, the log gets a `[seal]` line with the seed and a banner offers F6 (none of the ten biomes tried gave a false alarm).
- Tests: `--scenario=report` makes a report (not sent, no browser: `BugReports.OpenBrowser`) and checks the seed, map, shut-in check, saved file and
  address length.

## Run metrics

**Your Build** has a second page, **Run metrics** (left / right, or click the tabs, switches between them), and the same page comes up once
when the party falls (Tab / Back brings it back at the camp). It sets the party's heroes side by side, the best of each row in gold:
attacks and hits (hit rate), damage dealt, overkill, kills (elites and bosses), damage to elites and bosses, damage taken (and from elites
and bosses), damage prevented, healing given, overheal, deaths and revives given; then each hero's damage by attack (the sword, an ability, a
dagger throw, "over time" for burns, poison and hexes), the creatures they hurt most (with kills) and what hurt them most.
- Each game keeps its own hero's record (`Core/RunMetrics.cs`) and sends it to the others every few seconds, on a death and when the run
  ends (`Net.Msg.Metrics`). What one hero does for another is kept by the one it was done for, under the giver's name, and added up for the
  giver: a barrier's soaking, a share of a blow taken by an Aegis (Shared Burden, Share Pain), a heal (and what ran past full), a revive.
  Damage a hero's own guard takes off (a shield, armour, a brace) counts as their own.
- A blow is put down to the last button pressed, for 1.6 s (so a bolt or a thrown dagger still counts for its button); an attack counts as
  a hit when its damage lands in that time. On a friend's game the kill is reckoned from the health the copy of the creature shows.
- Test: `--scenario=metrics` (attacks, hits and a miss, damage by attack and creature, kills and overkill, a blow with a barrier, a heal past
  full, the page), and `tools/nettest.sh` checks the host's record reaches the other game.

## Effects: kinds of ring

The expanding rings were all one ring; now there are five (`FxLayer.RingStyle`, drawn by the glow shader's shapes 7 to 10): **plain** (hits),
**burst** (a ragged blast front that slows as it spreads, sparks thrown off it: blows that strike all round, ruptures, smites, shatters,
bashes, explosions), **ping** (a diamond closing in on its point with a glint: marks, targets, warnings, a taunt, a caged hero),
**dashed** (a broken ring turning as it spreads: blessings and auras, shouts, howls, barriers, wards, shifting) and **wave** (a soft swell of
light with motes rising from it: healing, revives, hearts). Online they travel as one effect (op 23; `Net.Version` 8). Test:
`--scenario=ringfx`.

## The water's surface

Swimming up, a hero comes to rest at the surface with their middle 5 px under (`Tune.Hero.SurfaceRest`) and their head out, breathing,
with no bobbing up out of it. A press of jump there leaps out of the water (the last of the water doesn't drag the leap back). The
surface is no floor: falling back in, a press of jump does nothing until the hero has gone back under, or stopped coming down. The
Automaton, who sinks, leaps out the same way from the surface. `--scenario=surface` checks it.

## Shared blows, ropes, rubble, crouching

A creature's blow is an area one: every living hero within `Tune.Share.Radius` (110 px) of the one struck
takes an equal share of it, each through their own shield, barrier, bubble and armour (`Player.PoolBlow`;
online the host's game does the sharing, and tells the others' games to take theirs). The Aegis's Bubble
wraps every hero near her; each bubble shrinks as it soaks, the Fx layer draws them as one metaball blob
(`Shaders/fx_shieldfield.gdshader`) and bubbles that touch share a blow by how far they overlap
(`Player.BubbleLink`). **Rope** (C): every hero carries one (`Combat/Rope.cs`). Hold the button
at an edge and it is let down over it: the hero can't move meanwhile, and the coil falls and unrolls as it goes, so the
wound part is always at the bottom. Let go of the button (or run out: 130 px, five heights) and it stops, the coil opening
into the rope's end with a little swing; press again to reel it in. It is a chain of points (verlet) that hangs from the
hands, lies against the rock it falls on and swings; a climber (up to take hold, up and down to climb, left and right to
pump, jump to let go with the swing's speed, over the top onto the ledge) weighs 30 times a stretch of rope, so taking hold
sets it swinging with the climber's own momentum. Online, each game keeps a copy that carries its own hero; the holder's
game sends when it stops, reels in or is dropped. `--scenario=rope` checks it. **Rubble** plugs narrow passages (`Cave/RubbleGen.cs`): blades or the interact button clear it, a
few rocks at a time. **Crouch**: hold down on the ground; the body is half as tall, and a blow from above
your head misses you. Caged heroes are chosen by what the party still has locked and unlock for everyone.

## The Shape Shifter

A feeble staff fighter whose strength is borrowed. The ability button (Shift) copies the nearest creature
in range (`Tune.Shifter.CopyRange`): the hero's body becomes that creature's own 3D model, outlined in white.
Thirteen forms (`Core/ShiftForms.cs`): goblin, skeleton, rat, bat, spider, frog, scorpion, bear, golem,
hornet, sporeling, crab, fish.

The hero **is** that creature, not an imitation of it. Shifting spawns a real instance of the enemy class
(`Player.Shifter.cs`, `MakeCreature`) with `Enemy.Master` set to the player (`Enemies/Enemy.Driven.cs`); the
hero's body is hidden and the player simply rides the creature's position. The creature runs its own action
engine (`Think`, wind-ups, strikes, recoveries, clips, gait, jump, flight, swim, speed, armour) with one change:
instead of a brain or script choosing its `Intent`, the controller does. The stick makes the creature *advance
toward a point* 320 px in the stick's direction (its target is that point instead of the hero), the attack
button sets its attack intent and the creature attacks the moment it can, through its own wind-up, and the jump
button is the creature's own jump (the frog hops, the bat flaps up, the spider lunges). Enemies never read
these, so they behave as before. Blows land on creatures and breakables through the creature's own hit tests
(`StrikeFoes`), never on the hero or allies; damage is the creature's own base number x the hero's damage
multipliers. The bear's second button is its real Charge; the other forms keep a Shape Shifter trick on it (Club
Smash, Bone Spin, Gnaw, Latch, Envenomate, Pounce, Venom Spray, Quake, Dive Sting, Spore Cloud, Vice
Grip, Splash), each with its own icon (`tools/icons/make_icons.py`). In a form the hero has the **Transformation** (shown beside the health
bar): every blow of the form's, its creature's own and its special, deals 60% more (`Tune.Shifter.TransformDamage`), so a hero-driven
creature outfights a wild one of its kind while the character sheet still shows the Shape Shifter's own numbers. Some specials:
- **Gnaw** (rat): six quick bites straight ahead (or the way you aim), darting in a little with each, a row of teeth snapping shut on
  whatever it bites; nothing behind or beside it is touched (`GnawBites`, `GnawInterval`; 0.4x the form damage a bite).
- **Latch** (bat): it flies at the creature nearest your aim (within 150 px), hangs on to it for 1.2 s biting (0.72x the form damage in
  all, a fifth of what it was, the creature held still unless it is a guardian), lets go, flutters back, and mends 15% of the hero's health over 5 s (`Latch*`). **With no creature in range it goes for a harmless cave critter instead** (a moth or
  a crab, within the same 150 px, the way you aim first), flies at it and **eats** it: 2 health at once (`BatEatHeal`, through the usual healing
  multipliers), and the Latch is ready again after 1.5 s (`BatEatCooldown`) so a bat can feed on several; `--scenario=shifterspecials` checks it.
- **Venom Spray** (scorpion): a cone of venom ahead; every creature in it takes a little and is poisoned (2.4x the form damage over 5 s,
  topping up with each spray) (`Spray*`).
- **Envenomate** (spider): it pounces on one creature and bites, leaving a powerful venom (6x the form damage over 6 s) (`Envenom*`).
  Poisoned creatures drip green and wear a green aura (`Enemy.Envenom`). `--scenario=shifterspecials --hero=shifter` checks all of these.
Each form keeps what makes the creature itself:
- **Fish**: swims where it is pushed (it cannot drown in water), the jump button leaps it out of the water, on land it
  flops about goofily instead of walking, and it drowns in the air (`Tune.Shifter.FishDrownPerSec` of the hero's health a
  second) until it flops back in.
- **Spider** (`Enemy`'s driven mode in `LandEnemies.cs`): pushed into a wall, or up into a ceiling, it takes hold and walks
  along it (the model lies along the wall); pushed straight away from what it holds, it steps off on its web and drops
  (the attack makes the drop a strike), up climbs the thread back and takes hold again where it left, the jump button cuts
  the thread, and on a ceiling the attack is its drop, on a wall a pounce.
- **Bat**: has the air to itself and can't land; idle (or pushed up) at a ceiling it hangs there as a roosting bat does,
  and lets go for any other push or the attack. The hornet likewise flies free, and never hangs.
- To the cave's creatures a shifted hero seems **twice as far** away (`Tune.Shifter.NoticeFactor`), and to creatures of its
  own kind far further (`KinNoticeFactor`; among several heroes `KinChoosingFactor` puts the kin last of all, though still a
  valid target when alone).

Health stays the Shape Shifter's own. Shift again
drops the form, then Shift recharges. `--scenario=shifter --hero=shifter` checks the staff and the shift,
`--scenario=shifterforms --hero=shifter` checks that each walks with its own gait and winds up before its blow
lands, and `--scenario=shifterbehaviors --hero=shifter` the fish, spider, bat and noticing above (add `--shots=DIR` under
xvfb for pictures; it looks for a clean wall and ceiling, so try another `--seed` if it notes none).

## The Automaton

The last work of Ottoline Brass, found in the Clockwork Deep (`Player/Player.Automaton.cs`, `Tune.AutomatonHero`; its model is
`HeroDesign.Automaton.cs`: a barrel chest banded in brass with a furnace glowing behind a barred window, two stacks behind the head,
a blank, faceless head, pistons on the forearms, a long-hafted cleaver held edge first).
- **Finding him**: he is never caged. While anyone in the party still lacks him, a Clockwork Deep level (which always tries to cut
  a secret wing) has him lying disabled in a heap at the far end of the wing, past the cracked wall, and a **power cell** hidden
  somewhere in the level (`Combat/PowerCell.cs`; whoever finds it finds it for the party). With the cell, interact at the heap to
  revive him: he gets up, and he's unlocked for everyone (`Main.PlaceAutomatonHeap`, `Meta.AutomatonWanted`).
- **His make**: 300 health, 15% armour, 0.72x run speed, 0.72x jump height, and he takes only 45% of a blow's shove. His second
  jump is a little steam hop from the joints (0.6x a jump).
- **No lungs**: he never drowns, and the toxic air and the gas clouds do nothing to him. **Waterproof**: water, steam and acid do
  him no harm.
- **He sinks**: in water he falls toward 190 px/s left alone (270 with down held; anyone else drifts down at 22), upright and feet
  first, and walks the bottom as on land. Each press of jump kicks him up a little (120 px/s, no more than every 0.28 s), so
  climbing up through the water is slow; at the surface a press leaps out as anyone's does.
- **Fuel, not healing**: every heal he receives (potions, pickups, a Vitalist, life steal, the rest between depths) becomes fuel,
  30% as much (`FuelFromHeal`; 80 fuel at most, starting at 40). **Self-Repair** (the ability button) spends fuel to mend him,
  one for one, at 10 health a second for up to 4 s, slowed to 45% while he tinkers (a second press stops it; 6 s to recharge).
- **Cleaver** (attack): slow, heavy chops (17.5 damage, 0.85 s apart, 64 px reach), three to a combo; the third is a **slam**
  that sends a shock through the floor (55% of the strike to everything within 56 px of where it lands) and sets gas alight.
- **Steam Release** (second ability): a cone of steam ahead (9 damage and a hard shove), which blows gas away, puts out his own
  burning, and kicks him the other way at 300 px/s. Aimed at the floor it is a lift; in water, a thrust.
- **Brace** (dodge button): planted for 0.75 s, blows from in front lose 70% of their damage and can't move him, and the first
  melee blow is answered with a counter-chop.
- **Pressurize** (support button): three seconds of building pressure, glowing hotter and shaking harder and harder, slowed and
  unable to strike (a blow doesn't break it; it needs 5 fuel to begin). Then, still shaking and glowing, he strikes 35% faster and
  1.8x as hard (the cleaver's old weight), moves 45% faster and jumps 55% higher, burning 4 fuel a second, until the fuel is gone
  or the button is pressed again. It's back 8 s after the pressure goes.
- Cards for each ability (alterations: **Scrap Reclaimer**, fuel from kills instead of from healing; **Scalding Steam**, the
  steam burns instead of shoving; **Full Plate**, Brace softens blows from behind too), four relics and five class perks.
- `--scenario=automaton` (in the Sulphur Springs) checks the kit; `--scenario=clockwork` checks the heap, the cell and the revival.
  `--hero=automaton` plays as him.

## Support abilities (the support button, V)

Every hero has a fourth ability that does something for the others (`Player.Support.cs`, `Tune.Support`):
Swordsman **Battle Shout** (allies near and you deal 10% more for 10 s, 30 s to recharge), Warden **Taunt**
(every creature counts her as next to nothing away for 3 s), Vitalist **Health Tap** (15% of max health for
60% of the vital force reserve), Elementalist **Stalag-Might** (30 damage and the creature is rooted for
2 s, 15 s), Rogue **Expose** (the creature takes 20% more from everyone, and everyone crits it 10% more often, for 8 s), Aegis **Share Pain**
(the friend she aims at gives her 15% of every blow they take, until she picks another or them again; 4 s between picks), Shape Shifter **Pack Howl** (allies near run
20% and strike 15% faster for 8 s), Automaton **Pressurize** (a 3 s build-up, then faster, harder strikes, quicker feet and
higher jumps, burning fuel until pressed again; see above). `--scenario=support --hero=vitalist` checks all seven.

### Loadout, Prodigy's Brand, rubble

- **Loadout** (button on the loadout & perks stage of the hero select, or `L` there): pick up to three of the hero's **side-grades** (the alterations: alternate abilities such as Frostbolt for Firebolt, one to an ability) to start the run with. While everything is unlocked for testing (`Tune.Testing.UnlockEverything`) every side-grade is listed; they will be locked away with the heroes. The give-and-take upgrades are called **risk-rewards**.
- **Radiant Ward** (Aegis side-grade, replacing the old Healing Ward): the ward bolt's burst strikes at full strength and reaches 40% wider. **The Aegis's auto-attack no longer heals anyone** (the ward bolt used to mend her and her friends; that made her too strong): its burst only damages and weakens.
- **Foe's Burden** (Aegis side-grade for the second ability, `Player.Aegis.cs`, `Tune.Aegis.Foe*`): instead of carrying a friend (or smiting) it is laid on **a creature** within 260 px, best the one you aim at, for 12 s: that creature takes **25%** of every blow the Aegis takes (as raw damage, so it can't be multiplied further), and she takes the other 75%. A beam of violet light joins them, the HUD label reads `FOE n s`, and it comes back after **6 s** (the Smite takes 9 s, Share a Burden 0.4 s). It works alone or in a party. `--scenario=aegis --hero=aegis` checks it.
- **Prodigy's Brand** levels you up thirty-five times at once; you can never open a chest again (shrines, relic chests, guardians' hoards and vaults alike), and guardians leave you no hoard.
- A cleared rock blockage collapses top-first into a flat heap that lies there a few seconds before sinking away, so the way through is obvious.

- **Breaking ice with spells:** an ice sheet over water breaks on the third spell touch (a bolt, a Blizzard/Firestorm strike, or a Vitalist drain wisp); blades still break it in two blows. A bolt that meets ice stops there.

### Livelier creatures

`Render3D/Creatures/CreatureLife.cs` is a layer over every creature's own animation (heroes excepted; heavy creatures show less, `CreatureDesign.LifeScale`): it breathes, shifts and glances about while idle, with a shrug and a hop now and then; leans into its stride; rocks back before it sets off and forward as it stops (all on springs, so changes overshoot like weight); rears up with a flare of its glow when it notices a hero; coils back and quivers before an attack, with its glow building; lunges forward and stretches as the blow lands; and flinches when struck. Enemies also wait only half as long before their first attack (`Tune.Combat.FirstAttackDelay` 0.5 s) and hold their attack slot half as long (`SlotHold` 0.18, `SlotReserve` 0.15).

Creature-specific telegraphs (on top of that layer; `--scenario=telegraph` checks the frog and spider): the **frog**'s throat balloons and shudders for 0.3 s (mouth cracking, body sat back) before its tongue lashes at where you are then, and it pants faster once it has noticed you; the hunting **spider** crouches flat, fangs spread and quivering, for 0.3 s before it pounces; the **bat** hangs in the air with its wings thrown up and its mouth open for 0.3 s before each swoop, beats deep to climb and shallow to glide, and shivers as it roosts; the **eel** draws its head back in an S with its mouth wide before it bites.

- **Stalag-Might** now bursts a bulge of rock up out of the ground round the creature's feet (`StalagGrip`), which holds it for the 2 s and sinks away when the hold ends (or the creature is freed or dies). Only a creature near the ground can be seized (`Tune.Support.StalagGroundReach`); aimed at a flier or a leaper high in the air it says "NOT NEAR THE GROUND" and costs nothing.
- The **Warden's shield** is 1.5 times as strong (`ShieldHpShare` 0.33) and regenerates 1.5 times as fast (`ShieldRegenShare` 0.0135), with a shorter pause before regeneration (0.8 s) and a shorter break (5 s).
- Rats are 30% bigger and paler; the ink outline's line is a pixel wider (`edge_px` 2.5, now in `ink_edge.gdshader`).

### Where the plugs go, and how they stand

Plugs (boulders; banks of skulls in the Catacombs) never appear in the first level. They go preferentially at the
mouth of a dead end (a side passage that goes nowhere but to what is hidden in it): `Cave/RubbleGen.cs` walls off a
candidate plug's cells, counts what can be reached on each side, and a side that runs out without the start or the
guardian in it is a pocket (the bigger it is, the nearer the plug to its mouth); plugs go to those first and to any
level passage after that (`--gentest` prints `rubble n (dead ends m)`). A plug is barely narrower at its foot than at
its top, and its pieces are scattered at all angles and depths, so it stands, but only just.

### The secret depth: the Sunken Sea

About one level in three that has water (not the first level, and not so deep that it would pass the dragon's, `Tune.Abyss.DrainChance`)
has a **drain**: the lowest swimmable point of its lakes has a drain, with a shaft sunk from it to the bottom of the map
(`CaveGenerator.AddDrain`, from the seed alone, so online every game makes the same one). Swim into it, with no button to press, and
you go down at once to the Sunken Sea, one depth deeper (so its exits lead to depth + 1 and depth + 2: a shortcut). It is only reachable this
way: it never comes up among the ordinary exits (`Weight` 0, left out of `Biomes.PickFor`). It has no guardian and its two exits stand
open from the start; it holds more chests (relic-tier ones as ever now and then), underwater treasure pits and water creatures. Test it with
`--scenario=abyss --forcedrain` (a drain in every watery level; the scenario swims into it and checks the sea), `--start=drain` to look at the
drain, and `--biome=abyss --start=water` for the lake.

The drain looks like nothing you would swim into: no light, only a curtain of black that thickens from nothing at the lowest point of the water to solid black, running down past the bottom of the map (to the bottom of the screen, however low the view looks) so that nothing below can be seen, with a negative light that
drains the colour from the water round it. Where you arrive in the Sunken Sea a waterfall pours from a crack in the roof to the beach beside you (the
lake above, coming down), with a continuous low roar (a seamless looped drone, `SoundBank.WaterfallDrone`) heard from a good way off. Rubble piles are built from angular, flat-shaded chunks of broken rock with a real stone grain (projected from three
sides), not smooth balls.

### The camera's limits, and secret wings

The camera keeps to the **main occupied area**: the bounds of all the cave's open space plus four cells of rock round it (`CaveData.ViewRect`,
`Tune.Secrets.ViewPadCells`; the 3D view obeys the same limits), so nothing out past the cave's own edge can ever be seen.

That makes room for **secret wings**. About half the levels try for one (`Tune.Secrets.WingChance`; a wing is cut only where the cave
reaches its right-hand side: a level floor with four rows of headroom within `Tune.Secrets.ApproachCells` (8) cells of the right-hand extreme and
no more than `MaxReachCells` (90) cells from the start's side of it, clear of breakable ledges and vaults; most levels that try find one, and
none is cut in the first level, the abyss, the dragon's lair or the underground river): a four-row passage runs out past the cave's right-hand edge to a domed chamber with a
silver chest (and a caged hero now and then, as any hidden chamber has), shut off by a special wall. It is the cave's own rock: the terrain's
mesher, field and material run on the field with the passage's mouth put back (`SecretWing.Wall`, meshed by `LedgeMesh` as the breakable ledges
are; `SecretWallView`), carried along the passage as far as the camera will ever see, with a ring of the rock round it so it overlaps the
terrain's own and leaves no seam, so it lines up with the wall round it, fills the passage from the front to the back wall, and takes
more than a glance to notice (dust sifts from it now and then, the draught of the passage behind it). It is a rubble plug underneath,
but a special one: it takes eight blows (`Tune.Secrets.WallHits`; the plugs strewn about the level take four), and a heaving swing (the
Swordsman's) still brings it down in one. Until it has been struck at all there is no prompt and nothing to heave: it is only a wall.
When it falls, the wall crumbles in pieces along its length.
The wall is crackly, which is how it shows it can be broken: a web of fine cracks (a Voronoi of broken stone, `secret_cracks.gdshader`, laid
over the wall's mesh as a material overlay) in black lines, kept to the wall's own rectangle so the rock round
it is unmarked; each blow it takes widens and brightens them (`hurt`). Its rock runs `Tune.Secrets.WallPastView` (6) cells past the view's
limit, since the camera sees a little more of what lies far from it than of what lies near, at the edge, so the wall reaches the end of the
viewport whatever the angle. The generator makes the map `Tune.Secrets.PadCells` wider for the wing (the new rock on the right). The wing
lies outside the view's limits, so it can't be seen from the cave; when its wall is down, in this game or a friend's, the limits are let
out to take it in (`Main.OpenWing`: they ease out, and the camera slides out with them rather than jumping), a notice reads A SECRET LIES
BEYOND, and the passage and the chamber are there to walk into. `--scenario=wing --wing` checks all of it (`--wing` gives every level a
wing); `--gentest --wing` checks that the passage runs through to the chamber. (Widening the map changes the field's stride, so the breakable
ledges' cell indices are remapped as it is done.)

### The Clockwork Deep

`BiomeId.Clockwork`, depths 5 to 8 (`Biomes.cs`, `Tune.Clockwork`, `GenStyle.Works`, `Cave/WorksGen.cs`). A works: four stacked galleries with perfectly level floors and a roof that steps now and then, great machine halls cut off them (11-15 cells tall, with a stair of catwalks up one wall and the other), and 8-wide shafts between the floors with catwalks too; about half of the shafts also hold a lift (`CaveData.LiftShafts`). The floors are dead level so belts and pistons have somewhere to be. The rock shader's special mode 9 lays staggered, riveted iron and brass plate on every wall near the open space, rust bleeding from the seams, and the decor turns gears on the back walls, most of them in meshing pairs (`GearSpinner`). The machinery is in `Combat/Machines.cs` (placed by `Main.PlaceMachinery`; views in `Render3D/Props/MachineViews.cs`):
- **Piston** (`Piston`): hangs from the roof of a gallery where it is 70-120 px to the floor. A cycle of rest (2.8 s), warning (its housing shudders, steams and its lamps blink, 0.9 s), the fall (0.1 s), the dwell on the floor (0.6 s) and the rise (1 s). A hero under the head as it comes down is struck once a cycle (10% of maximum health and a flat part) and **stunned** for 0.7 s.
- **Belt** (`Conveyor`): along a stretch of level floor, carries whoever stands on it (hero or creature) at 68 px/s, either way (`Conveyor.DriftAt`; applied after each body's own movement, so a wall still stops them).
- **Saw** (`SawBlade`): a blade on a rail along the floor, back and forth for ever, whirring; cuts a hero it touches.
- **Lift** (`Lift : AnimatableBody2D`): a one-way platform that runs between the two floors of its shaft, resting 1.6 s at each end, carrying what stands on it; the catwalks are still there, so no route depends on one.
- **Steam jet** (`SteamJet`): a jet of scalding steam along a direction for a length and a time (a thin share of health every third of a second, and a push along it), stopped short by rock; the boilers' and the guardian's.
- **Automaton** (`Automaton`, armoured): a heavy overhead chop after a long, readable wind-up. It runs on a spring (`Spring`, 1 fresh .. 0 run down; `Tune.Clockwork.Automaton.Wind` seconds of work): freshly wound it is quick, then slows, swings later and shudders; at the end it stops for 1.7 s to be rewound, the key whirling in its back, and **takes half again as much from blows**.
- **Boiler** (`Boiler`, armoured, never moves): its pressure builds (gauge climbing, shuddering, glowing), then it turns on you and vents a jet of steam for 1.4 s, then cools.
- **Cogwheel** (`Cogwheel`, armoured): rolls at you, stops short and revs in place (sparks), then drives through you at 300 px/s with nearly twice the contact damage, then is spent for a moment.
- **The Engine-Warden** (`EngineWarden : CavernColossus`): fights as the Colossus does, and every few seconds columns of steam erupt across its hall, each shown a moment before by a hissing and a gout of white at the floor (one is always under the hero).
- `--scenario=clockwork` checks all of it (the generator's machinery, a piston's warning, strike and stun and its harmlessness beside it, a belt both ways, a saw, a lift carrying a hero up, steam, an automaton winding down and stopping and taking more, a boiler's charge and jet, a cogwheel's rev and dash, the guardian's columns); clockwork_*.png.

### The Sulphur Springs

`BiomeId.Sulphur`, depths 6 to 9 (`Biomes.cs`, `Tune.Sulphur`). An ordinary branching cave (`GenStyle.Walkers`) in yellow, crusted rock (the rock shader's special mode 8 lays patches of sulphur bloom over it) round hot, murky yellow pools. Everything that is its own is in `Combat/Gas.cs` and `Enemies/SulphurEnemies.cs`:
- **Gas vents** (`GasVent`, `BiomeDef.GasVents`): fissures in the floor on a cycle of rest (5.5 s), hiss and yellow bubbling (the warning, 1.4 s), then blowing (2.6 s), a puff every half second. Each puff is a **`GasCloud`**, which hangs where it lands, drifting up a little and lying along the roof, for about nine seconds, and **poisons** a hero standing in it (`Player.GivePoison` every 0.6 s: 3% of maximum health plus a depth-scaled part over five seconds, topped up while they stay, so it builds). Vents only blow near a hero.
- **The gas is fuel.** `GasCloud.IgniteAt(point, radius)` lights every cloud it touches (and sets off every gasbag); it is called by the Elementalist's fire bolts (in flight and where they burst), a firestorm, and the fireballs and lava globs of creatures (`EnemyProjectile`). A lit cloud goes up a heartbeat later in a ball of fire (12% of a hero's health and then burning, and a third of a creature's health, igniting it), and lights the clouds beside it in turn, so one spark can run through a whole room's worth. Frost and ward bolts do nothing to it.
- **Gasbag** (`Gasbag`, `Element.Gas`: it takes half again from fire): a bladder of glowing gas that drifts down off the roof to hang just over a hero's head, swells, and lets a cloud go under itself. It does no harm of its own. Struck down it bursts into a cloud; lit, it goes up in a ball of fire. `Pop(fire)`.
- **Brimstone worm** (`BrimstoneWorm : Eel`): lies hid in a hole in the floor (no one can strike it), and when a hero comes near it strikes out along a line, bites, breathes a puff of gas where it struck, and draws back (vulnerable while out). It shares the eel's burrowing body (`EelDesign`, `WormDesign`) and brain.
- **Acid newt** (`AcidNewt`, `EnemyProjectile` kind `acid`): keeps 100-190 px off and spits an arc of acid (a fan of three for an elite); where it lands the floor burns for a few seconds (`AcidPuddle`).
- **The Vent Colossus** (`VentColossus : CavernColossus`): fights as the Cavern Colossus does, and every eight seconds or so breathes out a ring of great clouds across its chamber (more once enraged); lit, the chamber goes up around it.
- **The water is acid, and the lowest air is toxic** (`BiomeDef.AcidWater`, `BiomeDef.ToxicAir`, `CaveData.ToxicY`/`IsToxic`): the water lies in the bottom quarter of the map (half the usual share), burns whoever is in it (`Tune.Sulphur.AcidShare` of maximum health and a depth-scaled part, every half second, in `Player.Hazards`), and the 16 cells of open air above it (a yellow haze, `Liquid3D`) are toxic: a hero holds their breath in them as under water (the breath drains, and runs out into drowning damage), and gets it back out of them. Drowned Lungs, a bubble, or a body that doesn't breathe are proof against the air.
- Online, the creatures are replicated by type as the others are, and the clouds and pools their host makes are sent as props. The new creatures' sprite sets (`Art/newt.json`, `gasbag.json`, `worm.json`) hold only clip timings (the body is the 3D model; `SpriteSet` makes a blank texture when there is no picture).
- `--scenario=sulphur` checks it all (the acid, the toxic air, the vent's warning and its clouds, the poison, a fire lighting a cloud and the blast running through its neighbours, the gasbag's hover, burst and detonation, the worm's strike, the newt's acid and its pool, the guardian's breath); sulphur_*.png.

### The Underground River

`BiomeId.River`, depths 4 to 7 (`GenStyle.River`, `CaveGenerator.GenerateRiver`, `Tune.River`). A long, high canyon, 300 x 1.5 cells wide:
the beach where you arrive, then the river, then a high rock plateau that holds the guardian's chamber. The river runs *the wrong way*: its
current (`CaveData.Flow`, 112 px/s, `FlowAt`) carries everything in it from the guardian's end back toward the beach, a little faster than an
ordinary swimmer (`Hero.SwimSpeed` 96) can make way against, and it slackens only in the eddy at the beach (so you can climb out), and in the
treasure pits and the hidden chamber (12% of itself).
- **The way on is dry, and sparse.** Boulders hang in the river, flat on top and rounded beneath, their undersides just under the water so the
  current runs on beneath them. They are few and small, and stand far apart (`Tune.River.GapScale` 1.3: near the edge of what a careful
  jump clears; a climb a shorter gap, a drop a longer), so crossing without a wetting takes skill or a trick of mobility: a held run-up,
  a double jump, a dash, a wall, the rope, a friend's draft. Two broad banks at most, where the elite bats lair.
  The generator proves the dry route with the strict pixel-true jumper (`FineReach` in its dry mode, never touching water) and tries again
  if it fails (`CaveData.DryOk`). Fall in and the river takes you back down it toward the start, and you have the stones to climb again.
- **Slabs over the river** (`AddRiverLedges`), at two heights: a low one over the middle of a broad enough stone (a jump up from either
  end of it), and a high one over the gap between two stones, above the reach of a stone-to-stone jump so it never gets in the way. They
  make a second, higher way across, and like every ledge they can be broken. If they would cost the dry route anything, they all go again.
- **The upgrades are at the bottom of the river**: treasure pits sunk in the river bed (and chests scattered along it), out of the current.
- **The secret under the guardian's ground.** The river comes from beneath the plateau: its mouth is a tunnel into the rock under the
  guardian's floor (marked by bubbles escaping), 22 cells long, to a hidden chamber with a silver chest. The current is full strength
  in it: it can be reached only by swimming faster than the water runs. A swim-speed upgrade or relic (Gill-Touched x1.5; two together
  more), the Elementalist's narrow draft aimed along the water, a Shape Shifter's fish. Out-swimming it takes about 7 s at twice the speed
  (the breath lasts 15).
- **Wind and fish.** The Elementalist's narrow draft is a wind in the water here: whoever swims in its column is pushed along its
  axis (`River.DraftPush` 200 px/s, so aimed up the river it carries a swimmer, and a friend, upstream; aimed with the stick in any direction).
  A fish (a Shape Shifter's form, or a real one) feels only 30% of the current (`FishResist`); any other creature in the water is carried
  along with it, and the guardian isn't.
- **Only bats live here** (the water elementals are left out of this biome): river, pale and blind bats roosting in the roof and wheeling in
  in waves, elite bats in the lairs, and the Echo Queen, a bat half again the size of an elite, three and a half times its health.
- The water shows its current: surface ripples and long streaks of foam race along it (`water_surface` and `water_face` take a `flow`).
  `--scenario=river` (also with `--hero=shifter`, for the fish) checks the whole of it, `--gentest --biome=river` the generator (dry way,
  hidden chamber, vault).

### The Glitch, and the Null behind the wall

**The Glitch** (`Enemies/Glitch.cs`, `Tune.Glitch`) comes only to a party in which *everyone* has slain the Elder Dragon at least once
(`Meta.Victories`; online each player's game says so on a spare bit of the lobby handshake, `Net.SlayerBit`, and `Net.PartySlayers` asks
them all), and rarely even then: 5% of levels from depth 1 to the one above the dragon's, 25 to 70 s in. When it comes the music stops
(`SoundBank.Hush`) and the banner reads SØMETHING IS WRØNG.
- It is a heap of broken rectangles bigger than a hero (`GlitchView`): flat unlit boxes in the colours of a broken screen and the black and
  magenta checker of a missing texture, each jumping, changing colour and tearing into a scanline on its own beat. Each game draws it
  somewhere within 48 px of where it really is, re-rolled several times a second (`ShowRadius`), so you never quite know where to swing.
- It doesn't move as things move: no gliding, no jumping. It stutters toward a hero in jumps (about 24 px every 0.1 s, faster than a hero
  runs), through rock as through air, and every 1.8 to 3.4 s it is simply somewhere else, beside another hero (`Blink*`). It screeches and
  chirps in lo-fi, bit-crushed, sample-held tones with dropouts (`glitch`, `glitch_chirp`, `glitch_hum`).
- Its blow (0.5 s wind-up, the heap swelling and tearing) rolls a hero's health anew, anywhere from 1 to all of it, and can't roll nothing
  until it has struck that hero 8 times (then 15% of its blows do); one blow in five also leaves poison, a burn, a web or a stun. A dodge
  slips it. Online, the blow is a boon sent to the struck hero's own game (`NetSync.Boon.Glitch`).
- A blow on it rolls *its* health anew the same way (its numbers are never numbers: ERR, NaN, 0x3F2A...), and nothing brings it down until
  it has been struck 100 times (`HitsToMortal`); after that each blow has a 10% chance of rolling nothing. Bleeding, poison and fire can't.
- Beaten, the music comes back, and the far wall of the guardian's chamber (the end away from where the level begins) goes wrong: a patch
  of it turns to broken boxes and missing textures (`GlitchWallView`) and isn't really there. Walk into it, no button, and you go through
  (online, everyone gathers there first, as at any exit) to **the Null** (`Main.OpenGlitchWall`, `Portal.Glitch`), one depth deeper.

**The Null** (`BiomeId.Null`) is built rooms in black, plum and magenta with cyan light, reached no other way. It has no guardian and its
exits are open from the start (to depth + 1 and depth + 2, like the Sunken Sea). It holds:
- **A corrupted chest for each hero** (`Main.PlaceNullFinds`; theirs to open first), dealing three **corrupted cards** (`Upgrades.Corrupted`),
  each a strong thing with a strange cost, taken once: **Overflow** (deal three times the damage, take three times the damage), **Bit Flip**
  (one blow in eight lands six-fold, one in eight does nothing), **Duplication** (every potion and key you pick up comes twice; a third less
  maximum health) and **Null Pointer** (once a depth, a killing blow leaves you at 1 health and throws you clear; healing received halved).
- Corrupted creatures (skeletons, goblins, golems, scorpions, bats and spiders in its colours) and more chests than most levels.
- Nothing holds still: now and then a hero on the move skips a frame, a step ahead the way they're going (never into rock), with a chirp
  and a spray of broken pixels (`Main.TickNull`).

`--scenario=glitch` runs the whole of it (the silence, the stutter and blinks, both health rolls, the hundred blows, the wall, the Null and
its chest); `--glitch` makes the Glitch come to any level a moment after it starts, and `--slayer` counts you as a dragon-slayer.

### The Guild's lamps, and ways in only a creature can use

- **A Guild lamp** (`GuildLamp`): in about seven levels in ten (never the entrance or the lair) a lantern is still burning at the side of a treasure room
  out of the way, over what is left of whoever kept it. Press interact to read the page left there: a line from a lost Delver, Spring Order pilgrim
  or Crown soldier about this kind of level (`Lore.Journal`, fifteen pages, one per biome), and, the first time, an ember. Pages found are kept
  (`Meta.JournalFound`) and listed under the story on **The story** card. Test: `--scenario=lamp`. What a page leaves with its reader (the
  feeling, a small buff for the rest of that cave, `RunBuffs.ForPage`) is shown apart from the Delver's words, under a short rule and in its own
  colour (gold for inspiration, blue for sorrow, `RunBuffs.Colour`), both when the page is read and in the journal.
- **A fish's slit** (water levels): out of the side of the deep water, a crack under a cell tall (14 px: a fish form is 11 across, a spider 14.4, a
  hero's capsule 13 by 26) running into the rock to a small underwater chamber with a silver relic chest. Only the Shape Shifter's fish can
  swim it. A thread of bubbles escapes from its mouth.
- **A spider's crack** (any dry roof): a vertical crack 22 px wide up out of a high roof, too high to jump to from anything you can walk to,
  with a small chamber and a chest at its top. A spider form crawls along the roof to it and up its walls; a little dust sifts out of its mouth.
- Both are carved last (`Cave/Nooks.cs`), from the seed alone, in about two levels in five. Test: `--scenario=nooks --forcenooks` (the fish in
  a water level, add `--biome=mine` for the spider), `--gentest --forcenooks` for how often a biome can hold them.

### Where you arrive, and the ledges that can be broken

- **The way in.** Below the first level you arrive in front of the stair you came down by: a dim stone doorway, bordered with rock like the exits but
  going up, barely lit, with no name or prompt (`Portal.Entry`). The first level has none (you walked in from outside). Walker caves now put the
  way in a quarter of the way down the map (`BiomeDef.StartRow`), the mine and the crypt lower than before.
- **Breakable ledges.** The rock ledges and stepping stones the generator lays in (`StampLedge`: in tall spaces, as repairs and as the stairs to
  the guardian) are checked for traversal as rock, then lifted out of the terrain (`CaveGenerator.LiftLedges`) and put back as slabs
  (`RockLedge`) that stand and carry weight until they are broken: ten blows (`Tune.Ledge.Hits`) to bring one down, a shudder and a spray of dirt
  with each, then it collapses in blocks. Thirty seconds later (`Tune.Ledge.RegrowSeconds`) it grows back, swelling up out of the rock, once
  nobody (hero or creature) is where it would be; it breaks again just as easily (spells too), so it can never trap anyone for good. Ice ledges
  (frost caverns) freeze back after the same thirty seconds; the dragon's tiers are as before. Online, each blow is sent to the other games. `--scenario=ledges` checks it.

### Ledges in the cave's own rock, textured doorways, and the Delvers' feelings

- A breakable ledge is meshed by the terrain's own mesher over a tiny field holding just that ledge (`Render3D/Terrain/LedgeMesh.cs`), with the
  terrain's own material, so it is the same stone as the cave and looks like part of it (the same size and shape as before). Struck, it shudders;
  broken, pieces of the same stone fall and it is gone.
- The rocks round the entry and exit doorways are broken, angular chunks with a stone grain (the same grain as the rubble and ledges), not smooth
  lumps.
- **The Delvers' feelings** (`Core/Buffs.cs`): reading a lamp's page now also leaves a feeling in the reader, an *inspiration* or a *sorrow that
  steadies*: a little higher jump, further jumps (speed), swim speed, more breath, more damage, ten per cent shorter ability and dodge cooldowns,
  less damage taken, more healing given or received (each page its own). It lasts until the hero leaves the cave, over every level between,
  is listed under "FELT" at the top left, and a page's feeling is taken once. `--scenario=lamp` checks it.
- **A slower pace** (`Tune.Hero`): run speed 136 px/s (was 170), swim speed 96 (was 120) and jump velocity 420 (was 470: jumps 20% lower), for every
  hero and shape. The run animation plays 20% slower to match (it is paced to the old 170 px/s). The generator's reachability check follows
  these numbers (`FineReach`, with the slowest hero's jump at 0.95 of the base).
- **Caged heroes** (`Main.PlaceHeroCage`): never at depth 0 (or the lair), only somewhere hard to reach: most of the time in a level's hidden
  chamber (a chimney, a fish slit or a spider crack) when it has one, else the dry spot farthest and highest from the start. About one a run
  (`Tune.Heroes.CageChance` 12% a level over the nine or so below the mouth; 30% in the Sunken Sea).
- **Falling rocks** (cave-ins, the guardians' stalactites) are painted in dirt brown, no longer the terrain shader's green inputs; the generated
  ledges are shaded as a rock face (darker, more occluded) so they sit in the cave's rock instead of glowing against it.
- **Elementalist upgrades** (`Player/Upgrades.cs`): *Spare Bolt* (+1 firebolt held, twice) and *Frozen Quiver* (+2 frostbolts held, twice).
- **Co-op roles** (`--scenario=coop --hero=rogue|swordsman|warden` checks them; `--alttest --hero=elementalist` the updraft):
  - **The Elementalist's updraft is the narrow, aimed one** (the Narrow Draft alteration is gone, its numbers are the updraft's: 55 px wide, 296 px
    tall, 15 s). The dodge button *aims* it while held (a faint column with chevrons shows where it would stand, following the mouse, the right
    stick or the move keys; aimed nowhere it stands straight up) and *raises* it when let go, at the final angle, **any angle at all**: up, along
    (a sideways wind), or down (a downdraft, which speeds a fall instead of easing it). It costs 15 alimus, paid on release.
  - **The Rogue's daggers stick in walls** (a surface steeper than `Tune.Rogue.FootholdMaxSlope`; floors and ceilings still turn them back). The
    hilt is a one-way ledge (`FootholdLength` 14 px) anyone can stand on or jump up through; a dagger in a wall is drawn larger and glints.
    It stays until recalled. With Tether the Rogue can pull herself to one. Online, every game flies the same dagger into the same wall, so each
    has the foothold.
  - **The Swordsman's heaving swing breaks a rubble pile in one blow** (`Rubble.Smash`, sent to the other games) and **shatters frozen creatures**; the
    **Warden's shield bash** shatters them too. A shatter is a snap's burst (`Player.BurstCreature`), without the Snap's doubling: the creature takes `Tune.Elementalist.SnapDamage` on top of
    the blow, and what stands near it takes the splash.
  - **The right stick aims the Rogue's daggers** like the mouse: pushing it over to swing never recalls the daggers (only the attack *button* with none in
    hand does; `PlayerInput.StickAttack`). **The Elementalist aims the narrow draft with either stick** (or the move keys): taking aim holds him still,
    with no moving, jumping, bolts or swings until the button is let go (`Player.ReadInput`).
- **More breakable platforms** (`CaveGenerator.ConvertSmallPlatforms`): any small platform the cave grew by itself (a free-floating island of
  rock, thin and wider than deep, up to 64 corners and 10 cells wide) is lifted out of the field and made a ledge slab like the generated ones (ten
  blows to break, dirt and shudder), with its own thickness for the collider. `cave.NaturalPlatforms` counts them (the gentest line shows
  `platforms n/total`); `--scenario=ledges` checks one. Frost's breakable ice platforms are lumps of ice now: a wandering outline, thick in the
  middle and ragged at the ends, a keeled faceted underside, clumps of icicles of every length and shards of rime.
- **Ledge slabs** (`LedgeMesh`) are cut out of the terrain itself: the cave's field is built once more with every ledge in it (on the loading
  thread), the terrain mesher runs over a window round each ledge, and the ledge keeps its own triangles of that terrain, clipped cleanly just
  in front of where it would bend into the back wall. Same field, seed, mesher and material, so every vertex has the ground's colour,
  occlusion, depth and grain. And **the platforms** (ledge slabs, ice platforms) are no longer put on the creatures' light layer
  (`PropView.ActorLit`): the creatures' warm key and cool rim lights had made them bright with glowing edges. The doorways and rubble
  keep those lights (without them they went nearly black; `--lookshot=PATH --lookexit` shows a doorway).
  `--scenario=ledgeab --seed=N [--ledgesasground]` renders the same views with the ledges as slabs or left in the rock as plain ground; the
  two now measure within about 1/255 of each other on the ledges' tops and faces. `LEDGE_DEBUG=1` prints each slab's triangle count.
- **Rubble barriers** are made with the terrain's own material, so each takes its biome's rock and tint (they were one brown photo everywhere).
- **The doorways** (the stair you came in by, and the exits) stand 0.85 m back from where the heroes walk, so no one walks through their stones.
- **The Aegis's Barrier** soaks half what it did (`Tune.Aegis.BarrierShare` 14% of the receiver's max health). **Her ward bolt** is a
  golden bubble that wobbles as it flies (no fire, no ember trail) and pops on impact.
- **Chests** carry a soft white fill light in front (on the creatures' light layer only), so their faces, bands and locks read in the dark.
- **Prodigy's Brand** no longer stops the upgrade shrines (the three prongs) from opening: only relic chests, guardians' hoards and vaults.
- **Guardians' own tricks** (`--scenario=guardians` checks each, with a picture of every wind-up), each with a wind-up clip of its own:
  - **The Web-Mother** rears back, abdomen swung up under her (`web_windup`), and spits a ball of silk (`EnemyProjectile` kind `web`). A hero
    it strikes is **webbed** (`Player.GiveWeb`): wrapped in white strands, unable to move or act, until they struggle free (every press
    counts, `Tune.Status.WebStruggle`; the HUD shows how far), take enough harm (`WebHoldShare` of their health), a friend's blade passes
    through the web (`Player.CutFree`), or it gives way (`Tune.Spider.WebSeconds`).
  - **Guardian bears** (the Den Mother, the Rotback, the Tunnel Brute) rise and draw breath (`roar_windup`), then roar: every hero within
    `Tune.Bear.RoarRange` is **stunned** (`Player.GiveStun`, stars round the head, `StunSeconds`, then a spell of immunity), and the bear
    rushes the nearest for a quick swipe.
  - **The Brood Queen** arches her tail high over her head, stinger aimed (`spray_windup`), and sprays a cone of venom (`spray`): a little
    harm and a heavy poison (`Tune.Scorpion.VenomTotal` over `VenomSeconds`; a second soaking adds to it).
  - **The Cavern Colossus**'s charge runs to its arena's end, where it digs in, skids and wheels round (`charge_turn`) and charges back the
    other way; the second run ends against the wall, stunned, as before. It no longer runs out of its chamber to be put back in the middle.
  - Online, the web, the stun and the venom reach each hero in their own game (`NetSync.Boon` Web, Stun, Venom, Unweb; flags `HfWeb`,
    `HfStun` show them on the others' screens).
- **Spells break platforms**: a bolt (fire, frost, the Aegis's ward), the Vitalist's drain and a storm strike the breakable slabs and ice
  platforms as a blow does (`Breakables.Spell`, `SpellBurst`), so a caster is never shut in by one. `--scenario=ledges` checks a bolt.
- **The rope** is six heights long now (`Tune.Rope.Length` 156). The coil hangs from its near side as it unwinds (the rope leaves it on the
  side toward whoever lets it down); when it stops, its last turn swings in toward them, a small push on the end. A coil lands on a breakable
  slab as on rock (and rests there), and the rope hangs past slabs rather than through them.
- **A struck slab trembles gently** about its own middle. Its mesh's origin is the cave's corner, so the tremor's slight turn had swung it
  up and down about that far point; it now shakes inside a pivot at the ledge's centre, smaller and settling quickly.
- **Fewer keys**: a level holds exactly as many keys as it has cells (vaults): one, or two with a Locksmith's Ring. The first mini-boss
  slain drops one where a mini-boss lairs; the rest are hidden. Going down, a hero keeps only as many keys as the cells they left locked
  (`Main.LockedCells`): open every cell and you leave with none. `--scenario=vault` checks both.
- **High caverns** (`CaveGenerator`, the tunnel-walker caves 130 cells tall or more, after the map's height scale: one hall, two in the very
  tallest, i.e. Slime and Crystal): a broad hall carved up from a main tunnel's floor into the upper cave, 14 to 30 cells tall (far above
  any jump), with a lobe or two so it isn't a plain arch. Ledge chains keep only their first two steps inside it (`Tune.Cave.CavernSteps`);
  above is open air for a bat's wings, a spider's legs, an updraft or a friend's footholds. High in its walls are an *aerie* or two (a
  pocket with a silver chest), and over its crown a crack up into a hidden *grotto* (a chamber with a silver chest; caged heroes may be
  kept in either). Each is closed in its own shell of rock, so it holds even where the heights were a maze of old tunnels. The tall caves
  also carry fewer ledges near their tops (`Tune.Cave.TallCaveTopPlatforms`). `cave.HighCaverns` lists the halls; the gentest line
  counts them; `--nocaverns` leaves them out; `--lookshot=PATH --lookcavern[=N]` stands a hero in one.
- **The Shape Shifter carries a real staff**: a crooked wildwood shaft with a leather wrap and bead charm, its head a knot of burl with two
  antler tines curling back (it was a sword mesh in staff colours). She still swings it with the sword's cuts.
- **The Swordsman's scarf tail is gone**: it stood stiffly out behind his shoulder like a spike; the cowl is knotted at the nape.
- **Frostbolt**: 0.2 s between throws while charged (`Tune.Elementalist.FrostGap`); firebolts 0.25 s.
- **The Elementalist's orbs and charges** (`Player/Player.Elementalist.cs`, `HeroDesign.AttachOrbs/SyncOrbs`): lore from the world of Alima: an
  Elementalist absorbs elemental essence (fire, ice, water, wind, earth, nature) and moves it about his body by will alone. His staff now holds
  **two bolts** (`Tune.Elementalist.FireCharges`), thrown at least 0.25 s apart (`BoltGap`) and regained one at a time, a bolt every 1.5 s
  (`FireEvery`); **Frostbolt** holds **four** (`FrostCharges`), one back every second (`FrostEvery`), and now hits for 12 (it no longer rapid-fires). Fire bolts cost
  a little more alimus (2.9) since they come less often. Each spell shows as a glowing orb: two orange in his hands (the bolts; white under
  Frostbolt, with a third circling his left hand), and three circling his chest: gray (updraft), yellow (snap), white (storm; orange under
  Firestorm). An orb shrinks out while its spell is spent or recharging and swells back when ready. The bolt charges also show as pips under
  the HUD's alimus bar. Online, the orbs' state travels in the hero's net flags. `--scenario=orbs --hero=elementalist` checks it.

## Catalogue review (balance pass from the edited CATALOGUE.md)

Every value, stack count and rarity in the reviewed catalogue is in the cards now; `CATALOGUE.md` is regenerated (`--dumpcards`).
- **Removed** (overpowered or ill-fitting): Crescent Wave, Storm Edge, Unbroken, Wind Runner, Heavy Pommel, Crushing Blows, Tower Shield,
  Endless Hex, Slow Mending, Radiant Ward, Hostile Bubble, Fluid Form, Echoing Trick, Lodestone, Steady Hand, Air Control, Featherfall,
  Twin Reserve; the relics Rally, Overflow, Level Surge, Deep Breath, Regular Customer, Prodigy's Brand, Chain Lightning, Peddler's Friend;
  the Warden's Indignation perk. (Their code stays, unused.) **Coyote time and the jump buffer** are what one card of each gave, by default.
- **New**: Relentless Heave (a second heave, each 60% slower), Dense Metal (a bash costs the shield 30% less), Flashpoint (the firebolt's
  burst), Burning Heart, Fast Res, Just Kidding. Patient Mending is now a plain heal card (60% at once, 60% over 4 s); Spare Bolt holds a
  firebolt or two frostbolts (Frozen Quiver is folded in); Double Jump and Air Dash are conditional chest cards after a Spring Step / a
  Windstep (only Magma Skin is still a technique). Pairs that pull against each other exclude one another (Glass Edge/Stoneskin,
  Anvil/Quicksilver Grip, Far Sight/Close Quarters, Bloodlust/Siphon Crystal, Gaudy Charm/Dim Cloak, Rime Heart/Burning Heart,
  Scatter/Sapping Light).
- **Levels**: level-up gains are half as big again (`Progression.AutoLevel`); a milestone every 8 levels (7/6/5 with the Path's Waypoints).
- **Potions**: one belt rank (two potions; a third only from a Deep Flask, now taken once); Quick Draught / Lingering add 10% each, Strong
  Brew takes the heal over time from 15 s to 13/11/8.
- **Second Thoughts** reroll the merchant's wares too (a free deal that spends one).
- **The Elementalist's firebolt bursts** where it lands: 35% of the blow to creatures within 26 px (`Tune.Elementalist.FireBurst*`).
- **Shared Burden** takes its share of what got through the friend's own armour, guard and barrier; and the Aegis carrying it is left out
  of that friend's area pool (heroes close together split creatures' blows, so before she was taking half of every blow and her share of
  the rest). Burden of Many: the friend shares 20% among everyone near them. Heavier Burden: +10% carried, and 5% turned aside.
- **Humbling Light** replaces the Smite: creatures within 120 px deal 30% less for 3 s; 12 s.
- **Bears** have 75% of their health. The den and the tunnels are guarded by a bear only 40% of the time: otherwise by the **Goblin
  Warchief** (a big goblin that bellows for a club and a sling, up to three of its band at once, who leave nothing) or the **Barrow King**
  (a skeleton that marks the floor under every hero near and bursts bone spikes there 0.9 s later; every guardian skeleton does it).
  **Goblins** hit 0.8x as hard and at 0.65x the rate. `--scenario=guardians` checks both.
- **Relic chests** hold one relic: take it or leave it. A **Relic Collection** (teal, three gems: the guardian's party chest and 15% of relic
  chests) holds a relic for each of the party; each hero takes one, and the rest stay for the others (`Chest.Collection`, `TookHere`).
- **The trading stand** at the merchant's: a hero gives a relic they carry to a friend (a hero's own relic only to a friend of that hero),
  1 trade a run, +1/+2 from the Path's Trading ranks (`Player.Trades`, `Meta.Trades`). The friend's game carries it at once, or hands it
  back if they already carry it, and the trade is refunded (`Net.Msg.RelicGift/RelicReturned`). Relics now record what they changed when
  taken (`Player/RelicUndo.cs`), so a relic given away comes off cleanly; the merchant's own relic-for-relic trade is kept for later
  behind `Tune.Merchant.NpcTrades`. `--scenario=shop` checks the stand.
- **The merchant's first stall** can come from the 1st depth (the second still no later than the 9th).
- **Seed 804912** (the Nest) had a neck the coarse check let through and the fine check missed between its grid points; the fine check now
  tests the body's true width, so that layout is repaired or rejected. `--gentest --biome=nest --genseed=804912` makes that one cave.


## Elements (every hero but the Automaton)

The Swordsman, the Elementalist, the Warden, the Vitalist, the Aegis, the Rogue and the Shape Shifter fight with **elements** (`Core/Elements.cs`, `Player/Player.Elemental.cs`,
`Enemies/Enemy.Elements.cs`, `Tune.Elements`, `Tune.ElementalSword`, `Tune.ElementalStaff`, `Tune.ElementalWarden`), on **1, 2, 3** on the
keyboard, **d-pad left, up, right** on a controller (the d-pad no longer moves; the left stick does), rebindable as *Element 1-3*.

### Simple kits (the elemental heroes now)

The Swordsman, the Warden, the Elementalist, the Vitalist, the Aegis and the Rogue each have **three abilities and three elements**
(`Elements.Simple`, `Player/Player.Simple.cs`). The element changes what an ability does to what it reaches, never how the ability
works:

| | Primary attack (attack) | Defensive (dodge) | Special (ability) | Elements |
|---|---|---|---|---|
| Swordsman | Strike: a sword swing | Roll: a dodge roll | Cleave: the Hard Swing | earth, fire, frost |
| Warden | Cut: the shortsword | Shield: the block (held) | Charge: the Guarded Charge | earth, frost, wind |
| Elementalist | Bolt | Ward: a barrier on you and friends near | Burst: a blast at a spot along the aim | fire, frost, wind |
| Vitalist | Drain | Mending: the heal | Hex: the hex about her | nature, wind, water |
| Aegis | Bubble: the bubble shot | Barrier: on the friend aimed at (or her) | Dome: the bubble field | wind, earth, frost |
| Rogue | Jab (no dagger in hand: recall) | Vanish | Knife: the dagger throw | dark, wind, nature |

* **An element button switches to its element**, and does nothing else: no overcharge, no attribute, no mixes. A switch takes
  effect at once, at most once a second (`Tune.Elements.SwitchCooldown`). Pressing the element you're in does nothing.
* **The same numbers in every element.** Damage, reach, speed, cooldowns, barriers and shield absorption don't change with the
  element. The blows are plain, so no creature resists or is weak to them by element.
* **The element's touch.** What an ability strikes takes its element's build-up: a primary's blow 40 (`PrimaryBuild`), a defensive
  40 (`DefensiveBuild`), a special 80 (`SpecialBuild`). Fire burns, frost chills then freezes, earth stuns, nature entangles, water
  soaks, dark blinds. Wind builds nothing: it shoves the creature away, harder the bigger the ability (`ShovePerBuild`).
* **Every defensive pulses.** Using it sends a ring of the element out to 60 px about the hero (`PulseRadius`), touching each creature
  in it (no blow). The Warden's shield instead touches each creature whose blow it stops.
* **The look and sound are the element's.** Each use pops its name in the element's colour: Searing Strike, Rime Shield, Gale Hex,
  Thorn Drain, Shadow Knife (an adjective per element, `Elements.Adjective`, before the ability's noun, `Elements.Noun`). Bolts,
  bubbles, bursts, pulses and the sparks of each touch take the element's colour; blows land with its sound; the weapon glows with it.
* **The second ability button does nothing** for these heroes.
* **HUD.** The three elements on the d-pad's half circle (the one you're in big and lit, the switch's cooldown round the middle),
  then three squares in the element's colour (primary, defensive, special) with their cooldowns. The Vitalist's vital force bar
  follows. Hovering a square gives the ability's full name and what its element does (`UI/Hud.Simple.cs`).

The Shape Shifter keeps her own rules (her element picks the creature she becomes; see below) and the Automaton has no elements. The
classic kits (Settings) are unchanged.

**The archive.** Everything the simple kits replaced is still in the code, behind `Elements.Simple`, and catalogued with what each
ability did and where its code is in **[ABILITY_ARCHIVE.md](ABILITY_ARCHIVE.md)**. That covers the mode with its overcharge and
attribute, all 180 elemental versions, greater versions and mixes, Empower Hilt, the element leaps, Lift, Fire Walk, Tundra, the
Updraft, the Aid buffs, the Rupture, the Recall button and Shared Burden. Turning the switch off brings the mode kits back whole, and
the rest of this section describes them.

**Tests.** `--scenario=elements --hero=swordsman|warden|elementalist|vitalist|aegis|rogue` (`Main.SimpleKitTest.cs`) checks:

* the element buttons only switch, with a switch cooldown and nothing on top;
* every ability, in every element, gives the right build-up (wind: a shove, no build-up);
* each defensive pulses;
* each ability's cooldown and blow are the same in every element;
* the second ability button does nothing.

All six pass, and `--hero=shapeshifter` still runs her own test.

### The mode kits (archived: `Elements.Simple = false`)

**Modes.** The hero is always **in one of their elements** (its mode): every ability is that element's version, so in fire mode the
Swordsman's swings are Searing Strikes, his Hard Swing Pyroclasty, his dodge (from the ground) an Explosive Lunge. Each hero starts in their
own element (`Elements.Signature`: the Swordsman and the Elementalist fire, the Warden earth). **One slot sits on top of the mode**, and the
element buttons fill it:

| You press... | It does |
|---|---|
| your **mode's own** element | **Overcharge**: it goes on top, and the next ability that has a greater version (the same-element pairs below: Glacier Cleaver, Meteorite, Rain of Fire, the Earth Shield, Avalanche...) becomes it; that element's overcharge then comes back after 18 s (`OverchargeRecharge`) |
| **another** element | it goes on top as the **attribute** (replacing whatever was there, an overcharge too): every button that has a **mix** of the two becomes it (fire mode with wind on top: the fire-wind versions), and every blow and spell adds a little of it (15 build-up; wind, a shove), until a mix spends it or it's replaced |
| the **attribute's** element again | the **mode switches** to it (the slot empties), at most once every 4.5 s (`ModeCooldown`) |

So fire, fire, wind leaves you in fire mode with wind riding on top (the overcharge is gone); wind again puts you in wind mode. Presses are
taken 0.3 s apart (`ChargeCooldown`; one inside it is kept). **Using a mix or an overcharged ability spends what's on top**: the hero
is back in the plain ground state of their mode (`SpendPair` empties the slot either way), and must press the element again for another. In modes **every button keeps its cooldown** (`VersionsRest`, one cooldown per
button whatever the mode, so switching never refreshes one): the rests in the tables below apply. A few versions rest on their own while the
button stays usable (the Heaving Swing, the elements' movement), and the button is plain meanwhile (a swing, Lift; the elemental
Swordsman's dodge does nothing while his leap rests).
Overcharges are locked on a new save (`Meta.DualCharges`; `Tune.Testing.DualCharges` opens them for testing), so a new player starts with
just modes.

**No cooldowns while testing.** `Tune.Testing.NoCooldowns` (on for now) takes every cooldown off the elemental heroes while the elements are
tried out: their abilities and every version, the support ability, the Warden's dash charge, the mode's switch and the overcharge's recharge
all come back at once (the swing's and the bolt's own pace stay, so a button still can't fire every frame). The elements scenario turns it
off to test the real rules, and checks that with it on a switch back goes at once and a spent overcharge is there again.

The mode shows as a steady **glow on the hero's weapon** in its colour (an aura over the sword blade; a halo about the staff's orb, the
orb swollen and burning hot; the Warden's shield too), in every game online; an attribute burns along the sword's edge (and in the staff's
halo) in its colour, and an overcharge makes the glow flare and pulse. As an element goes on, it runs across the weapon: a puff of flame
along it, a sheen of ice up it from hand to tip, a crackle of grit, a twist of wind. The HUD shows the hero's three elements on a half
circle over the ability squares, one at each point of a triangle as they sit on the d-pad (left, up, right; keys 1, 2, 3): the mode big and
lit, what's on top as a small diamond at its corner, each element's overcharge coming back as a ring round its point, the mode's switch
coming back round the middle, and the mode written beside it (FIRE MODE, FIRE + WIND, FIRE OVERCHARGED); every button has its square with
its cooldown and the seconds left, named for what it does now. Every elemental use pops its name up over the hero, the basic attacks included (a Searing
Strike, a Stonecutter, the Elementalist's Firebolts, Frostbolts, Gusts and Pops); the name pops in place and fades there, it doesn't float
up (`FxLayer.AbilityName`).

**Barrier meters.** While a hero has a barrier, a small meter over their head shows how much of it is left (`FxOverlay`): a light blue bar
for a barrier (`Player.BarrierFrac`, against the most it held since it was last empty), teal for the Aegis's bubble, and the Warden's Frost
Shield or Wind Barrier in its element's colour; two at once stack upward. Every game draws them from the heroes' state, so they show online.

**Build-up, not chances.** An elemental blow fills a creature's meter of that element (thin coloured bars over it); full, it takes hold and
empties: **fire** sets it alight, and full again while alight it **combusts** (a burst on it and those pressed close, with what the fire had
left); **frost** chills it, and full again while chilled it is **frozen solid** (a creature that can't be frozen is chilled harder); a heavy
blow **shatters** the frozen; **earth** stuns it (one that can't be stunned is weakened). Earth's lighter touch **saps** (its attacks play 20%
slower); wind **shoves** (light creatures far, heavy ones hardly). Elites take 70% of a build-up, guardians and bosses 40%; meters drain a
while after the last that reached them. Online, the host keeps the meters (only the host's screen shows them, for now).

Each line below is one button, by the mode the hero is in (names in `Elements.SingleName`).

| Swordsman (earth, fire, frost) | Plain | Earth | Fire | Frost |
|---|---|---|---|---|
| **Swing** (attack) | the sword combo | **Heaving Swing**: rooted, x2, 60 earth (rests 2.5 s) | **Searing Strike**: x1.25, 40 fire (`SearBuild`) | **Razor Ice**: x1.25, 50 frost |
| **Dodge** (movement; no Dash Slash in the elemental kit) | (none) | **Earthfall**: a leap (in the air, none: you come straight down), and you come down like a rock: a shockwave of earth, harder the harder the fall (5 s) | **Explosive Lunge**, from the ground: the ground bursts behind you (fire to what's beside you) and throws you forward and up (340 px/s, 0.85x a jump); until you land (1.1 s at most) you **swing at every creature you come to**, once each, with everything a strike brings and 25 fire (4 s) | **Glacial Vault**, from the ground: you spring back off a burst of ice (frost to what's beside you), leaving your likeness in ice where you stood: an **ice decoy** creatures go for (as for a Frost Tower; anyone walks through it) that breaks after two blows or 6 s, filling those beside it with 40 frost; while one still stands, a vault leaves no other (4 s) |
| **Hard Swing** (second ability) | heavy blow x1.6 (5 s) | **ThunderCrush**: x2.6, slower, full earth, shatters the frozen (12 s) | **Pyroclasty**: x2.2, full fire (10 s) | **Ice Axe**: the sword goes up over the head and behind, and through a long wind-up (2.4x a Hard Swing's) an axe head of ice grows out of each side of the blade near its point (the sword is the shaft), with motes of frost drawn to it and a crack of ice at each step; then it comes down **in front only** (a 120-degree arc, not the Hard Swing's sweep from behind): x2, full frost (7 s) |
| **Empower Hilt** (ability, 8 s) | blows +20% (12 s) | blows sap (14 s) | blows +25 fire (14 s) | blows +20 frost (14 s) |

The jump is always a plain jump. The elemental Swordsman has **no Dash Slash**: his dodge is his mode's leap, and does nothing while it
rests. The leaps that come down on what's below (Earthfall, Meteor Fall, Comet) and the Blazing Charge go in the air too (without their
jump); the others (the lunges and vaults) only from the ground. (The classic Swordsman keeps his Dash Slash.)

**The axes of ice** (`Render3D/Creatures/Designs/HeroDesign.Axe.cs`, `Render3D/Shaders/fx_frostgrow.gdshader`): each part of a head is
drawn by a frost-grow shader. Its ice carries a gradient out from where it grows (the blade for the bits, a crystal's foot for the spikes),
roughened with noise; whatever lies past the growth front isn't drawn and the front itself burns white, so as the swing's growth rises the
head builds itself out of frost from the sword outward. The Ice Axe grows a crescent bit on each side and a spike from the point; the
Glacier Cleaver's blade first runs on in a long haft of ice, then vast horned bits flare out of its end, then a crown and studs of crystal.
The pose (`AxeHeave`) holds the sword up behind the head, arching back and trembling as the head grows, a last haul back, then one fast
arc down in front. `Player.AxeKind`, `AxeStage` (0-1 the wind-up, 1-2 the blow, 2-3 after) and `AxeGrow` drive it, and reach the other
games in the hero's state.
Battle Shout is as it was.
The cards for the Charged Strike and Swift Heave aren't dealt to the elemental Swordsman.

| Elementalist (fire, frost, wind) | Plain | Fire | Frost | Wind |
|---|---|---|---|---|
| **Attack** (each charge makes one bolt; nothing counted, it goes as often as the gap between casts allows: 0.625 s, 0.5 s for frost and wind, 0.4x the old pace, and every pop or bolt deals 0.85x) | **Pop**: no bolt; a small burst (9) right on the creature nearest the aim within 120 px | **Firebolt**: its burst, 50 fire | **Frostbolt**: quicker, 25 frost | **Gust**: very weak (0.15x a firebolt) but shoves hard (480) |
| **Dodge** (movement; the element rests its version a while, the dodge is Lift meanwhile) | **Lift**: a push straight up, in the air too (2.5 s) | **Fire Walk**: 30% faster for 4 s, fire burning where you walk (9 s) | **Tundra**: the ground freezes 110 px each way for 6 s: heroes on it go faster, creatures are chilled and filled with frost (10 s) | **Updraft**: a column along the aim (straight up, aimed nowhere) to ride up (3 s) |
| **Fixed range** (Burst button: a spot 130 px along the aim) | **Force Blast**: a wide (100 px) weak burst (7), throws creatures out (4 s) | **Burst**: an explosion at the spot, 60 fire; and every creature in view already burning, anywhere, takes a full build-up (combusts) (7 s) | **Snap**: every creature in view frozen solid shatters, anywhere; what's at the spot takes 60 frost (7 s) | **Cyclone**: drags light creatures in, swallows shots (9 s) |
| **Aid** (Extended button, 8 s) | **Ward**: a barrier for you and friends near (11 s) | **Kindling**: you and friends near add 15 fire a blow (12 s) | **Frost Armor**: melee attackers are filled with frost (12 s) | **Narrow Draft**: you and friends near move 20% faster, jump higher, for 4 s (10 s) |

The plain names (Pop, Lift, Force Blast, Ward) and Aid on the fourth button are my fill-ins, as are the Swordsman's frost and earth jumps;
rename or swap them freely. The elemental Elementalist's spells cost far less alimus than the classic kit's (Lift 4, the fixed-range
spell 2, Aid 6, paid as each comes back; his attack costs nothing), his alimus is always shown, and he has no orbs about him. The cards for Frostbolt, Firestorm, Cinder Snap, Kindling (the old chance card), Deep Chill and Shrapnel aren't
dealt to the elemental Elementalist; the storm cards (wider, longer) work on the Cyclone, the Fire Storm and the Blizzard.

**Casting slows the caster.** A cast that throws a projectile (a bolt, a dagger, a ward bolt, a wave, a barrage, a breath, a meteorite)
slows the hero to 45% of their speed while it plays (0.3 s for a single shot; a volley or a breath, as long as it lasts). A wind-up (a cast
gathering, `Player.Hold.cs`) is slow motion now, not a pause: a jump keeps rising and a fall keeps falling, at a tenth of the speed and with
every change to the motion at a tenth of the rate, and when it ends the hero carries on at full speed (it used to stop the rise, drop the
hero, then hand the rise back).

### Greater versions (overcharge) and mixes (attribute)

An **overcharge** (the mode's own element pressed again) makes the next use of a button the mode's **greater version**: the
same-element pairs in the tables below (`Player/Player.Duals.cs`, the lasting spells in `Combat/ElementalFx.cs`, `Tune.Dual`, names in
`Elements.DualName`). An **attribute** (another element on top) makes every button its **mix** with the mode, the two-element pairs, for as
long as it stays on top. Every button of all three heroes has all six pairs (three greater, three mixes), so nothing on top ever goes
unused; the order doesn't matter (fire mode with wind on top and wind mode with fire on top both give the fire-wind versions).

| Swordsman (Swing) | | What it does |
|---|---|---|
| Earth + Earth | **Fault Strike** | x1.5, full earth, shatters the frozen, and the floor splits on 150 px ahead of the blow |
| Fire + Fire | **Flame Crescent** | x1.25 and a crescent of flame bursting from the tip: 0.6x the blow and 50 fire to all within 40 px |
| Frost + Frost | **Frost Lance** | the cut, and a lance of ice thrown on along it (220 px, 1.2x, a full frost build-up) |
| Earth + Fire | **Magma Strike** | x1.4, 50 fire and 50 earth, and a little pool of magma where it lands (2.5 s) |
| Earth + Frost | **Permafrost Strike** | x1.4, 50 earth and 50 frost, shatters the frozen, and spears of frozen rock burst out of the floor before the blade (they melt away) |
| Fire + Frost | **Steam Strike** | x1.3, 50 of **whichever of fire and frost the creature holds more of** (burning counts as a full meter of fire, chilled as one of frost, frozen as two; even, your mode decides), and a cloud of steam where it strikes (heroes in it are hidden) |

| Swordsman (Dodge) | | What it does |
|---|---|---|
| Earth + Earth | **Meteor Fall** | the Earthfall, coming down like a falling star: a 1.6x shockwave |
| Fire + Fire | **Blast Lunge** | the Explosive Lunge, 1.3x as far, leaving fire burning where you pass (2.5 s) |
| Frost + Frost | **Stalagmite Vault** | from the ground: a leap back (330 px/s), and a great stalagmite of ice (44 px) bursts up out of the ground every 16 px you pass over: x1.1 and 50 frost to what it meets (once each), tossed up; they stand 1.1 s and melt away. No likeness is left |
| Earth + Fire | **Blazing Charge** | the dash, on the ground or in the air: you charge forward (430 px/s for 0.42 s, not falling) sword up before you, wrapped in a nearly opaque sphere of fire, crashing into every creature in your way once: x1.6, **100 fire**, thrown on; a great trail of fire behind you, and fire burning on the ground you pass (5 s) |
| Earth + Frost | **Comet** | on the ground or in the air: a leap forward, all blue-white; where you come down, a great burst of frost: x3 and **100 frost** to everything within 84 px, and 13 crystals of ice burst out of the floor and outward, melting away after a second |
| Fire + Frost | **Steam Vault** | the spring back, and a cloud of scalding steam where you stood: friends in it are hidden, creatures in it take 12 fire and 12 frost every 0.4 s |

| Swordsman (Empower Hilt) | | What it does |
|---|---|---|
| Earth + Earth / Fire + Fire / Frost + Frost | **Stoneheart / Blazing / Glacial Hilt** | the element's hilt, twice as strong, for half again as long |
| Earth + Fire | **Magma Hilt** | earth's and fire's hilts on every blow, and molten rock drips from the blade where it strikes (a little pool of magma, at most every 1.2 s) |
| Earth + Frost | **Permafrost Hilt** | earth's and frost's hilts on every blow, and the frozen burst under it |
| Fire + Frost | **Steam Hilt** | twice the hilt's touch of whichever of fire and frost the creature holds more of |

| Swordsman (Hard Swing) | | What it does | Rest |
|---|---|---|---|
| Earth + Earth | **Earthsplitter** | a long wind-up (3x a Hard Swing's, like the axe's) as the ground trembles harder and harder and grit and stones lift off it about you; then the blade comes down, the floor jolts (a heavy screen shake, rock flying, the creatures near held a beat) and a fissure tears 340 px along it (over steps, stopping at walls and drops), rock flying high behind it and the screen quaking as it runs; whatever it passes under is held in hit-stop, tossed up and fully stunned, and a rubble wall in its way comes down | 14 s |
| Fire + Fire | **Inferno Cleave** | a wall of fire stands where the blade lands for 4.5 s: creatures in it burn (22 fire every 0.4 s), creatures' shots flying into it burn up, gas in it goes off | 13 s |
| Frost + Frost | **Glacier Cleaver** | the Ice Axe's swing, but the head that grows out of the blade is vast and ornate (1.1x the reach): it grows slower and comes down later (3.4x a Hard Swing's wind-up), in front only (150 degrees): its blow is 4x a Hard Swing and two full frost build-ups (chilled, then frozen solid), and the head shatters what it froze as it lands | 16 s |
| Earth + Frost | **Frost Heave** | the blade is driven into the ground; frost cracks the floor under the nearest creature within 280 px and a column of ice bursts up there: a great frost blow, full frost, thrown into the air | 14 s |
| Earth + Fire | **Breaking Quake** | gathering (2x a Hard Swing's wind-up, the ground trembling), you drive the blade into the floor; great gaping cracks full of magma tear out 120 px either way along it (`MagmaCracks`), and everything over them takes x1.6 and **100 fire** at once and is thrown up; they close 1.6 s later | 15 s |
| Fire + Frost | **Thermal Shock** | a full build-up (100) of **whichever of fire and frost the creature holds more of**; then, frozen solid, it shatters at once, and burning, it bursts (combusts) at once | 12 s |

| Elementalist | Attack | Dodge (movement) | Fixed range (Burst button) |
|---|---|---|---|
| Fire + Fire | **Meteorite**: falls on the thickest knot of creatures, smashing every platform it passes through (they grow back); cast standing, it falls through any rock above your feet and bursts only on ground as low as you stand or lower (cast in the air, on any ground); 46 fire and full fire within 92 px (15 s) | **Flamebreath**: fire pours from the staff for 1.5 s in a 50° cone, burning creatures and shots; aimed down, it holds you up (11 s) | **Rain of Fire**: fire falls over the spot for 3 s, each fireball bursting where it lands (14 s) |
| Frost + Frost | **Cryometeor**: the same fall in ice: everything about it is frozen solid, and the ground freezes over (a Tundra) (15 s) | **Frost Tower**: you leap and a pillar of ice (72 px) rises under you to stand and cast from, for 10 s. It's solid to creatures and to your feet; your friends walk through it. Creatures within 240 px go for it instead of a hero (their blows from beside it and their shots that meet it wear it down); it has 20 health x your max health over your starting health, and doesn't attack (13 s) | **Hailstorm**: hail over the spot for 3 s: frost, and it beats flying creatures down (14 s) |
| Fire + Wind | **Fire Barrage**: a volley of seven firebolts, fanned along the aim (10 s) | **Fire Barrier**: a ring of fire about you for 5 s, going where you go: creatures that touch it burn and are thrown back, shots burn up (12 s) | **Fire Storm**: the storm of fire, fanned: it pulls light creatures in and swallows shots (13 s) |
| Frost + Wind | **Frost Barrage**: a volley of seven frostbolts (10 s) | **Frost Barrier**: frost armour: the next creature to strike you in melee is filled with 100 frost (a full meter), waiting up to 30 s (10 s) | **Blizzard**: the storm of snow, fanned: it pulls light creatures in and swallows shots (13 s) |
| Wind + Wind | **Strong Gust**: a little whirlwind races along the floor (320 px/s, 1.3 s), shoving every creature it meets back ahead of it and nicking them (2 every 0.2 s) (8 s) | **Sky Lift**: a great push of air carrying you high (1.7x a jump), and a wind at the back of every friend near (Narrow Draft) | **Tornado**: runs along the floor 4 s, carrying light creatures along inside it and swallowing shots, then flings them (13 s) |
| Fire + Frost | **Frostfire Bolts**: a firebolt and a frostbolt loosed together, fanned a little apart (10 s) | **Steam Veil**: a burst of steam about you (56 px, 5 s): friends in it are hidden, creatures in the burst take 30 fire and 30 frost (12 s) | **Frostfire Lance**: one twin beam through a line 440 px long, half a build-up of each; the burning or chilled take 1.8x (11 s) |

| Elementalist (Aid) | | What it does |
|---|---|---|
| Fire + Fire | **Bonfire** | Kindling for half again as long, and a ring of fire bursting out about you (50 fire) |
| Frost + Frost | **Ice Armor** | Frost Armor for half again as long, and a shell of ice (a barrier 1.5x a Ward's) |
| Wind + Wind | **Gale Draft** | Narrow Draft for twice as long, and a gust that throws the creatures about you back |
| Fire + Frost | **Thermal Armor** | Frost Armor on you and Kindling for you and friends near |
| Fire + Wind | **Wildfire** | Kindling and Narrow Draft for you and friends near |
| Frost + Wind | **Frostwind** | Frost Armor on you and Narrow Draft for you and friends near |

A pair's bolt rests on its own (meanwhile the bolts are plain and the charges wait); movement and fixed-range duals rest their button. The earlier Frozen Orb, Wildfire and Whiteout are gone (their pairs' slots are now
Hailstorm/Cryometeor, Fire Barrage/Barrier/Storm and Frost Barrage/Barrier/Blizzard).

Who has which is by who would: Frost Heave is the Swordsman's (the Elementalist has no earth). The Elementalist's frost movement is now
**Tundra** (ground that chills and fills with frost whoever walks on it); a heavier Frozen Tundra can still be the Warden's, the tank's,
when it gets its elements.
A **third charge** for the Elementalist (and the tri-charge abilities it would open) comes later. Online, the lasting spells live in the
caster's game, which deals their blows; their look reaches the other games as the caster's effects (the Glacier Cleaver's axe shows on
everyone's copy of the Swordsman). The Swordsman's newer ones are in `Combat/SwordSpells.cs` (`IceCrystals` for the Comet's burst, the
Stalagmite Vault's trail and the Permafrost Strike's spears; `MagmaCracks` for Breaking Quake; `BlazeShell`, the Blazing Charge's sphere;
`SteamScald`), drawn by `Render3D/Props/SwordSpellViews.cs` (clear glowing crystals bursting up and shrinking as they melt, jagged
glowing rifts with heaved-up lips, a fire sphere from `fx_wardshell.gdshader`'s third kind); the crystals and cracks reach the other games
as hero events, the charge and the Comet's blue-white glow as two bits beside his mode. `--scenario=elements` checks every single and dual version for both heroes (`elements_hud.png`, `elements_tundra.png`,
`elements_iceaxe.png`, `duals_hud.png`, `duals_glacier.png`, `duals_inferno.png`, `duals_magma.png`, `duals_meteor.png`, `duals_breath.png`, `duals_tower.png`,
`duals_rain.png`).

### The elemental Warden

The Warden fights with **earth, frost and wind** (`Player/Player.WardenElements.cs`, `Tune.ElementalWarden`). His three buttons are the
**attack** (the shortsword), the **block** (the shield, raised) and the **dash** (the Guarded Charge), each with a version for each mode
a greater version when overcharged and a mix with an attribute on top (the pairs below). He starts in earth
mode. His **Shield Bash is not in the elemental kit** (its cards aren't dealt); Taunt, on the support button, is as it was. His dash keeps
its own cooldown; his shortsword and shield glow with his mode. The guards are in `Combat/WardenGuards.cs` (`EarthMound`, `WardShell`; their
3D views in `PropViews.cs`, the shells drawn by `fx_wardshell.gdshader`); online, every game raises its own from the Warden's state (two
bits beside his mode), while his own game keeps what they stop. The mixed guards hold while the shield stays up, 6 s at most.

**His shield.** The elemental Warden's shield is **one pool of 50** (`Tune.ElementalWarden.ShieldHp`, at his starting health; it grows with
it) whatever the mode, and it carries from one mode to the next. Raised, it absorbs the mode's share of every blow it meets and is worn 1:1
by what it absorbs (a perfect block still stops it all): **90%** in earth mode, **45%** in frost, **20%** in wind. Broken, it can't be
raised for **4 s**, but it starts mending at once (5 a second at his starting health, `ShieldRegen`). It looks its mode, in every game: a
slab of **brown-grey stone, solid** in earth; a pane of **pale blue ice**, half clear, in frost; a veil of **light grey air at 20%** in
wind (the steel shield hangs from a bone of its own, `shield`, shrunk away while the elemental one is held in its place), and raised, the
guard before him is a curved slab of the same stuff, blended rather than glowing (`Fx3D.WardSlab`: the stone grained and solid, the ice
and air thinning as the shield wears); it flashes when struck and is gone while broken. Only **overcharged** does the block call up the mode's **barrier**.

| Warden (earth, frost, wind) | Plain | Earth | Frost | Wind |
|---|---|---|---|---|
| **Attack** (each mode swings at its own pace: wind-up, swing and cooldown x the pace) | the shortsword | **Stonecutter**: slow (2x the pace), x2, 50 earth; the ground jolts and grit flies off the blade. In earth mode his sword is a club of packed dirt and stone, and **no combos**: a blow that lands never refunds the swing | **Rime Edge**: steady (1.25x), x1.2, 50 frost, a sheet of rime along the arc | **Gale Edge**: quick (0.65x), x0.6, a little more reach, throws what it strikes far back (400), a gust and an afterimage |
| **Block** (the shield, the mode's: one pool of 50 for all three) | the shield | **Stone Shield**: absorbs 90% of every blow it meets; solid brown-grey stone | **Frost Shield**: absorbs 45%; a pane of ice | **Wind Shield**: absorbs 20%; a veil of light grey air at 20% |
| **Dash** (every elemental charge runs through creatures, each struck once, instead of stopping at one) | the Guarded Charge | **Rockslide Charge**: ploughs through (x0.6 each) and ends in a shockwave of earth, 60 px (x1.4, full earth) | **Frozen Tundra**: 40 frost to what it passes; the ground it crossed (and 40 px past each end) freezes for 8 s | **Gale Charge**: 1.6x as long and 1.3x as fast, throwing creatures aside; friends it passes get a tailwind for 3 s |

| Pair | Attack | Block | Dash |
|---|---|---|---|
| Earth + Earth | **Boulder Strike**: x2.2, full earth (stunned), tossed up, shatters the frozen | **Earth Shield**: the ground in front heaves up into a great lump of rock that curls over him and stands **for as long as the block is held**: nothing reaches him (blows or shots, from any side, without limit), nothing moves him, he can't move or leap, and he **kneels** behind it with his shield up; it is a **wall** to creatures too (a body on the tower layer they can't pass). Let go and it sinks back at once (from the ground; while it can't rise, the overcharge waits) | **Avalanche**: the rockslide with a 92 px quake (x2), and a fissure running on 220 px ahead |
| Frost + Frost | **Icicle Lance**: the cut, and a lance of ice thrown on 260 px along the aim (x1.4, full frost) | **Ice Barrier**: a faceted shell of ice about him (it stays when the shield is lowered) absorbs 80% of every blow; once it has absorbed 15 it shatters, filling every creature within 72 px with 90 frost and dealing 10 | **Glacier Charge**: everything it passes is frozen solid, spikes of ice burst up out of the ground it charges over (36 px, every 18 px, tossing what they meet), and the ground it crossed freezes |
| Wind + Wind | **Cyclone Slash**: a whirling cut all round, 1.5x the reach, x1.3, everything struck thrown away | **Tenuous Barrier**: a faint skin of wind about him absorbs 20% of every blow, and dissipates once it has absorbed 10 | **Tempest Charge**: twice as long, carrying every creature it meets along in its wake, flung at the end |
| Earth + Frost | **Frozen Earth**: x1.5, 60 earth and 60 frost, shatters the frozen | **Permafrost Guard**: the ground about him freezes (100 px each way); stone and frost together (nothing through, strikers stunned and filled with frost) | **Frostquake**: ploughs through, the quake, and ice bursts up where it ends (x2.5, full frost) |
| Earth + Wind | **Sandblast**: a cone of grit 120 px ahead nicks (x0.5) and saps everything in it for 3 s | **Dust Wall**: a cloud of dust in front (56 px, 6 s) hides friends and saps creatures; stone and wind together | **Duststorm**: a long charge leaving clouds of dust behind (friends hidden, creatures sapped), and the quake at its end |
| Frost + Wind | **Frost Gust**: a freezing wind 120 px ahead: 60 frost, driven back | **Hoarfrost Gale**: strikers filled with frost and thrown back, shots go back | **Blizzard Charge**: farther and faster, 60 frost to what it passes, thrown aside; the ground it crossed freezes, and a blizzard whirls up where it ends (the Elementalist's storm: frost on every strike, pulling light creatures in) |

The names are my fill-ins; rename or swap them freely. The HUD shows his three buttons, each named for what it does in his mode (the
block's square shows the guard while it holds, the shield's strength beside them). `--scenario=elements --hero=warden`
checks every mode and greater version, and (for all three heroes) the mode controls (`modes_overcharged.png`, `modes_switched.png`, `warden_guard.png`, `warden_bulwark.png`, `warden_hud.png`).

### Two new elements: nature and water

The Vitalist and the Aegis brought two elements of their own to creatures (`Enemies/Enemy.Elements.cs`, `Tune.Elements`), each with its
thin meter over them: **nature** full entangles a creature (vines about it: everything it does slowed as if mired, 3 s, and it bleeds sap,
14 x the hero's damage over that time); **water** full soaks it for 6 s (its fire put out, frost building in it twice as fast, fire half).
A water heal's **cleanse** washes a hero's afflictions off (poison, fire, frost, webs, stuns), reaching friends' games as a boon.

### The elemental Vitalist (nature, wind, water)

`Player/Player.VitalistElements.cs`, `Tune.ElementalVital`. Her **drain** (the attack), the **hex** about her (the dodge button) and her
**heal** (the ability) each take her mode's version; **Rupture** is as it was. She starts in nature mode. Nature roots and bleeds, wind
reaches and moves, water soaks and mends; the drain's motes and the attribute's touch ride on every drain. In a mode the heal goes out
even with no one hurt (for what else it does), still costing its vital force.

| Vitalist | Plain | Nature | Wind | Water |
|---|---|---|---|---|
| **Drain** | the drain | **Thorn Drain**: 40 nature; a quarter more vital force comes home | **Wisp Drain**: reaches 1.4x as far, casts a quarter quicker, 0.8x | **Tide Drain**: 40 water; mends her by 15% of what it tears out |
| **Hex** (dodge) | the hex | **Overgrowth**: vines: 50 nature to all in it | **Withering Gale**: 1.2x wider, everything in it thrown out | **Drowning Hex**: 60 water to all in it |
| **Heal** (ability) | the heal | **Regrowth**: half now, three quarters more over 5 s | **Breath of Life**: 1.6x the reach, a tailwind for all it reaches | **Cleansing Wave**: every affliction washed off those it reaches |

| Vitalist pairs | Drain | Hex | Heal |
|---|---|---|---|
| Nature + Nature | **Bramble Lash**: a full meter of nature; brambles lash up to 3 creatures beside it, draining them (0.6x) | **Strangling Grove**: 1.3x wider, a full meter of nature to all (entangled) | **Bloom of Life**: 1.5x, and a healing pool blooms at her feet (5 s) |
| Wind + Wind | **Whirling Siphon**: up to 3 more creatures in reach drained too (0.7x) | **Eye of the Tempest**: 1.6x wider, everything in it hauled in toward her | **Second Wind**: every friend wherever they are, and a tailwind |
| Water + Water | **Riptide**: a full meter of water, and the current hauls it toward her | **Maelstrom**: a full meter of water to all, and a healing pool at her feet | **Wellspring**: 1.2x, a barrier of water (12% of health, 8 s) and a cleanse for all near |
| Wind + Nature | **Pollen Drain**: 40 nature to it and everything within 60 px | **Spore Storm**: 1.5x wider, 40 nature and a shove | **Seeds on the Wind**: 1.6x the reach, more mending over 5 s |
| Nature + Water | **Life Spring**: 30 nature; every friend near is mended by 30% of what it tears out | **Mire Bloom**: mired 4 s, 40 nature | **Rejuvenation**: 60% now, as much again over 5 s, and cleansed |
| Wind + Water | **Rain Siphon**: 40 water to it and everything within 60 px | **Squall**: 40 water and a shove | **Mist of Mending**: a mist about her (70 px, 5 s) hides her friends |

### The elemental Aegis (wind, earth, frost)

`Player/Player.AegisElements.cs`, `Tune.ElementalAegis`. Her **bubble shot** (the attack: the ward bolt, which still homes, bursts, weakens and
mends her a little), her **bubble field** (the dodge button) and her **barrier** (the ability) each take her mode's version; **Shared Burden**
(and Smite) is as it was. She starts in wind mode. Wind keeps creatures off and speeds her friends, earth makes her wards heavier and her
shots hit harder, frost fills what comes near with cold. Her shots take the colour of their element.

| Aegis | Plain | Wind | Earth | Frost |
|---|---|---|---|---|
| **Bubble Shot** | the ward bolt | **Gale Bubble**: 1.5x as fast, cast 0.75x as often, its burst throws creatures back | **Stone Bubble**: 1.5x as hard, slower, 1.3x the burst, 40 earth | **Rime Bubble**: 40 frost to all in the burst |
| **Bubble Field** (dodge) | the bubbles | **Air Dome**: creatures near thrown out | **Stone Dome**: bubbles 1.5x as strong | **Ice Dome**: 40 frost to creatures near |
| **Barrier** (ability) | the barrier | **Wind Ward**: and a tailwind | **Stone Skin**: 1.5x as strong | **Frost Ward**: and frost armour while it lasts |

| Aegis pairs | Bubble Shot | Bubble Field | Barrier |
|---|---|---|---|
| Earth + Earth | **Boulder Bubble**: 2.2x, 1.5x the burst, the one it hits stunned (full earth) | **Fortress**: bubbles 2.2x as strong, lasting 1.5x | **Bulwark Ward**: 2.5x as strong |
| Frost + Frost | **Frost Orb**: a full meter of frost to all in the burst | **Glacier Dome**: a full meter of frost to creatures near | **Glacial Ward**: 1.3x, and a Frost Barrier (the next melee striker frozen full) |
| Wind + Wind | **Bubble Volley**: three fast shots, fanned | **Gale Dome**: reaches friends 1.6x as far, and throws creatures out | **Twin Gale**: on the friend and her both, with a tailwind |
| Earth + Frost | **Hail Bubble**: 1.5x, earth and frost to all in it, the frozen shattered | **Crystal Dome**: bubbles 1.6x, and frost armour on all it wraps | **Permafrost Ward**: 1.8x, and frost armour |
| Earth + Wind | **Sand Bubble**: a cloud of dust where it bursts (hides friends), creatures in it sapped | **Dust Dome**: a cloud of dust about her, creatures near sapped | **Sandstorm Ward**: a cloud of dust about the one it wraps |
| Frost + Wind | **Snow Bubble**: 1.5x the burst, frost and a shove | **Blizzard Dome**: frost to creatures near and the wind throws them out | **Winter Ward**: frost armour and a tailwind |

Every name is a fill-in, and nothing is balanced yet. The HUD names each button for what it does in her mode (the attack's version is
written beside the element row). Their focus (the Vitalist's crystal, the Aegis's disc) burns in the mode's colour, an attribute pulsing
through it, an overcharge flaring it. `--scenario=elements --hero=vitalist` (or `aegis`) checks the mode controls, nature and water on a
creature, the versions on every button, and every pair (`vitalist_riptide.png`, `vitalist_overgrowth.png`, `vitalist_maelstrom.png`,
`vitalist_hud.png`, `aegis_volley.png`, `aegis_ward.png`, `aegis_airdome.png`, `aegis_hud.png`).

### The elemental Rogue (dark, wind, nature)

`Player/Player.RogueElements.cs`, `Tune.ElementalRogue`. Her **strike** (the jab), her **evade** (the dodge button: the vanish) and her
**throw** (the ability) each take her mode's version; the **recall** is as it was. She starts in dark mode. Dark blinds and lays creatures
open, wind makes her quick and flings, nature poisons and roots. **Dark** is a new element on creatures: full, a creature is **blinded** for
3 s (it loses every hero, as in smoke; bosses and guardians for less) and **exposed** for 4 s (1.25x the damage from everyone, and more
critical blows).

| Rogue | Plain | Dark | Wind | Nature |
|---|---|---|---|---|
| **Strike** | the jab | **Shadow Strike**: x1.2, 40 dark | **Gale Jab**: 0.7x the pace, x0.85, 1.2x the reach, a flick back | **Venom Jab**: 40 nature and a poison |
| **Evade** (dodge) | the vanish | **Shadowmeld**: half again as long | **Gale Step**: vanish with a dash ahead | **Spore Cloud**: 40 nature to creatures within 70 px |
| **Throw** (ability) | a dagger | **Shadow Knife**: 50 dark | **Gale Knife**: 1.5x as fast, knocks back | **Barbed Knife**: 50 nature and a poison |

| Rogue pairs | Strike | Evade | Throw |
|---|---|---|---|
| Dark + Dark | **Nightfall**: x1.6, a sure critical, blinded | **Umbral Veil**: twice as long, every creature near blinded | **Umbral Knife**: a sure critical, blinded |
| Wind + Wind | **Flurry**: 0.55x the pace, a half-round sweep, flicks all back | **Wind Walk**: the dash and a tailwind 4 s | **Fan of Knives**: both daggers, fanned, fast |
| Nature + Nature | **Deathcap**: entangled, a double poison | **Overgrowth Veil**: creatures near entangled | **Thorn Knife**: entangled and a bleed |
| Wind + Nature | **Thornwind**: quick, 1.4x the reach, nature and a flick | **Leaf Storm**: nature, and creatures near thrown out | **Pollen Knife**: nature to all within 60 px of the one struck |
| Wind + Dark | **Shade Step**: dark, and she steps through what she strikes to its far side, at its back | **Shadow Dash**: a long dash, smoke where she stood | **Shadow Tether**: dark, and she's drawn along to her dagger, striking again as she lands |
| Nature + Dark | **Nightshade**: dark, nature and a poison | **Nightbloom**: a cloud of smoke, dark and nature to creatures near | **Nightshade Knife**: dark, nature and a poison |

`--scenario=elements --hero=rogue` checks the mode controls, a blinded creature, the versions on her buttons and every pair
(`rogue_shadestep.png`, `rogue_tether.png`, `rogue_hud.png`).

### The elemental Shape Shifter (nature, wind, water)

`Player/Player.ShifterElements.cs`, `Tune.ElementalShifter`. For her the **element is the kind of creature she becomes** (`ShiftForm.El`):
**nature** the creatures that walk (goblin, skeleton, rat, spider, scorpion, bear, golem, sporeling), **wind** those that fly (bat, hornet),
**water** those of the water (fish, crab, frog). Rather than every form taking every element (nine versions times thirteen forms), each
creature keeps its own attack and special, and the element shapes the rest:

- **Shift** (the ability) copies the nearest creature of her mode's kind; with none near, she takes the form of that kind she remembers;
  with none of those either but another kind near, she copies it and her mode follows its element. Shift again drops the form.
- **Remember** (the dodge button, in a form) keeps it for its element: one remembered form for each (the HUD lists all three).
- **Switching the mode while shifted** takes her into the remembered form of the new element (a rat becomes her remembered bat), or back
  to herself if she has none. **The second button out of a form** takes the remembered form of her mode.
- **A form's blows carry its element**: 20 nature or water build-up a blow (a venom rat entangles, a tide crab soaks), a wind form's
  blows a shove; the attribute on top rides on them too, as for every elemental hero. Her name for the shape says its element (Venom
  Rat, Storm Bat, Tide Crab).
- **Overcharged, the creature's special is its greater one**: for 2.5 s its blows deal 1.5x and fill the element's meter (wind: a big
  shove). Each creature has its own: Warlord's Smash (goblin), Bone Storm (skeleton), Plague Gnaw (rat), Widow's Bite (spider),
  Deathstalker Spray (scorpion), Grizzly Rampage (bear), Earthshatter (golem), Blight Bloom (sporeling), Vampire Latch (bat), Swarm Sting
  (hornet), Monsoon Pounce (frog), Crushing Tide (crab), Tidal Splash (fish).
- **Out of a form** her staff carries the mode's element: the Bramble Staff (30 nature), the Gale Staff (a quarter more reach), the Tide
  Staff (30 water).

`--scenario=elements --hero=shifter` checks the mode controls, the kinds, Shift taking the mode's kind, remembering one form for each
element, a mode switch changing the form, Shift taking another kind (the mode following), a form's blows carrying its element, a greater
special, the recall, and the staff (`shifter_bat.png`, `shifter_greater.png`, `shifter_hud.png`). The Automaton keeps its own kit.

**Classic kits**: *Settings > Controls > Classic kits (no elements)* brings back the elemental heroes' old kits (from the next run). The tests of the classic kits keep them; `--scenario=elements --hero=swordsman` (or `elementalist`, `warden`,
`vitalist`, `aegis`, `rogue`, `shifter`) tests the elemental ones (`elements_hud.png`), and `--elements` runs any other test with them.

## Save files

**Save files** on the main menu (`Core/Profiles.cs`, `UI/SaveFilesMenu.cs`): each save keeps its own progress (embers, the Ember Grid,
heroes and the merchant freed, loadouts, the journal) and its **own in-game achievements**, in `user://saves/<id>/`; the list and the one
in use are in `user://saves/index.json`. Each row shows what the save holds (embers, runs, the deepest depth, heroes, achievements) and can
be **played** (switched to), **renamed** (type in its name and press Enter), **reset** (started over from nothing) or **deleted**; Reset and
Delete ask twice (the button turns red; press it again within a few seconds). A new save starts with the three starting heroes. A save from
before save files becomes the first one. Settings and controls are shared by every save. Once the game is on Steam, Steam's achievements
are the account's, whichever save earns them. `--scenario=saves` checks it all (in `user://test_saves`; `saves_menu.png`).

**The merchant's table is the same for the whole party**: his cards, relic and supplies are dealt from the level's seed and the party's
heroes (in the same order in every game), from what a hero fresh to the run could take, so every game shows the same wares (a card a hero
already has is shown locked, as at a chest); what one hero buys is gone from their own table only.

## Achievements (progress and feats)

Two kinds (`Core/Achievements.cs`; the full list, in Steamworks order, is `ACHIEVEMENTS.md`, rewritten by `--dumpachievements=PATH`):
- **Progress achievements** (67) mark the way through the game and are **party-wide**: every game in the party sees the same event (a depth
  reached, a guardian slain, the dragon) and earns them together. They are visible, and the game's own completion counts only these.
  Advance to depth 1–11, Defeat each of the 21 guardians, Conqueror (all 21 of the launch's: new guardians get their own achievement, so
  the goalposts never move), Explorer (every cave a run can take you through), Deep Sea Explorer, Into the Null, Freed each caged hero and
  the merchant, All Accounted For, Victory, Victory as each hero, The Long Way Down (the dragon at depth 11), Competent each hero (every
  side-grade hex of theirs on the Ember Grid), Vault Breaker, Generous (a relic traded), Fair Shares (a Relic Collection emptied), Lore Keeper, Master of the Path / Potion.
- **Feats** (22) are deeds, **hidden** until earned and **each player's own** unless they suit the party (marked *(party)*: Power Hungry,
  Looter, Ransacked, Indescribable Horrors, Undefeatable, Brawler). Obliterated (felled from full health by one blow; a Death Ward, Last
  Stand or Null Pointer saving you doesn't count), Too Stubborn to Die (survive a blow past your whole health), Savior / Burden (10
  revives given / taken in a run), Fish-Kin (2 minutes under water on the bubbles' air: not as a creature, not in an Aegis bubble, not
  without lungs), Not Fair, Untouchable (no damage at all for a depth; chip through a shield counts), Devastating Blow (500 in one strike),
  Fleet of Foot (double speed), Hard Landing (the most a fall can take), Chain Reaction, Glass Cannon, Hoarder, Minimalist, Tight Squeeze,
  Spike-Dancer, and the rest.
- **Earned** ones and the lifetime record they are judged from (guardians slain, caves entered, the deepest depth, side-grades taken) are
  kept in `user://achievements.json`; what can be judged from what is kept is checked again at start-up (quietly), so an achievement added
  later is granted to whoever already qualifies.
- **In the game**: a plaque slides in at the top right when one is earned (steel-blue with a laurel for progress, gold with a star and
  FEAT! for a feat; a crowd of them goes by quicker), and **Achievements** on the title and pause menus opens a page with two tabs, Progress
  (with the completion) and Feats (unearned ones shown as ???).
- **Steam** comes later: `Achievements.ToSteam` is where an unlock will be passed on (SetAchievement and StoreStats). Steam has no
  categories: progress is listed first and visible, feats after and hidden, with their own icon frame; Steam's own completion count will
  include the feats (it can't leave any out).
- Tests: `--scenario=achievements` checks the list (every guardian the caves make is on it), the hooks, both notices, both tabs and the save.


## The Ember Grid

Every lasting advancement is bought on one field of hexes round the camp's ember fire (`Core/HexGrid.cs`, the screen `UI/HexGridMenu.cs`),
replacing the old Potion and Path trees and the class-perk screen.
- **Branches** fan out from the fire, each in a wedge of its own: **The Potion**, **The Path**, **Keys and Vaults**, **The Merchant**,
  **Relics**, and one for **each hero**. A branch opens once its feature or hero is unlocked (the merchant's once he is freed, a hero's
  once they are). A hex can be bought once the one before it is owned, for embers and the branch's **material**; the farther out, the
  dearer (a root 6 embers and 1 material; a step 4 + 2 per ring; a notable feature 10 + 3 per ring and 2+; a side-grade 8 + 2 per ring;
  a keystone 30+ and 5). Hexes come as steps (a plain dot), notable features (a diamond), side-grades (a swirl) and **keystones** (a
  star: a build-defining trade, good with bad).
- **Materials** turn up in runs in place of something the hero couldn't use (`Core/GridFinds.cs`, `Tune.Grid`): a potion touched with a
  full belt is a **Reagent** half the time (otherwise it waits for someone with room); a heart at full health is the hero's own
  **Essence** half the time; a key with every slot full a **Tumbler** half the time; gold picked up while carrying 150 or more is also
  a **Guild Seal** half the time (once the merchant is known, at most once in 8 s); a relic chest can be **salvaged** for **Relic
  Dust** instead of taking its relic; and each guardian leaves an **Obsidian Pearl** half the time. They're kept at once (a run that
  ends badly keeps them), online each game keeps its own player's, and the run's summary lists them.
- **The heroes' branches** hold their class perks (each rank a hex; the first perk is the trunk), their **side-grades** (each one's hex
  unlocks it for the loadout: picking a side-grade in a run no longer unlocks anything), a root (+5% health; the Aegis's is her Bulwark
  Oath, owned from the start) and a keystone (Reckless Edge, Living Wall, Blood Price, Overflowing Well, Shadow Pact, Martyr's Vow,
  Feral Heart, Overclocked). A hero's hexes act only for that hero.
- **Side-grades in a run** go the other way now: brought in the loadout, a milestone may offer a **Back to...** card (Back to Dash
  Slash, Back to Firebolt...) that takes the side-grade off again, and the upgrades grown on it with it, as if never taken (the
  side-grade and its upgrades are applied traced, like a relic, so they can be taken back: `Player/RelicUndo.cs`).
- **The general branches**: the Potion (stronger, quicker potions, a second flask, Forager, **Shared Draught** heals friends close by
  for 40% of what a potion heals you, keystone **Volatile Brew** bursts for 25 when you drink but heals 15% less); the Path (Waypoints
  for milestones every 7/6/5 levels, experience, Trophies, Prospecting, Second Thoughts, keystone **Ember Pact**: guardians give 40% more
  embers, you take 15% more damage); Keys and Vaults (**Locksmith** a vault's chest has one more class card, **Key Ring** one more key,
  **Tumbler's Luck** a vault opened leaves a Tumbler half the time, **Rich Vaults** one more bargain, **Lockpick** a gate opens for 40
  gold without a key, keystone **Master Key** a relic in every vault but one key fewer); the Merchant (**Regular** a free deal-again at
  each stall, Fair Dealing, **Trader** more relic trades with friends, **Overstocked**, **Regular Route** a third stall each run,
  keystone **Merchant's Favor** a second relic on his table but his cards cost 20% more); Relics (**Antiquarian** salvage gives 2 dust,
  **Relic Sense** relic chests 3%/6% likelier, **Collector** Relic Collections twice as often, **Discerning** a relic chest holds a
  choice of 2, then 3, **Heirloom** begin each run with a common relic, keystone **Covetous** relic chests 50% likelier but shrines no
  longer heal).
- **The screen**: drag (or the right stick) to look about, the wheel (or shoulders) to zoom, click a hex (or step with the arrows /
  stick) to see it, the Buy button (double-click, Enter / A) to buy it. Owned hexes are lit in their branch's colour, ones you could buy
  now pulse, locked branches are grey. The first time it opens (after your first material) it explains itself.
- **Old saves** are given back what they spent: every bought rank and class-perk rank comes back as embers, and the Reagents and
  Pearls found as materials, to be laid out on the grid afresh.
- Tests: `--metatest` (the grid's shape, buying, reach, locked branches, effects, a hero's branch acting only for them, the old save's
  refund, the save's round trip); `--scenario=grid` (materials in a run, a Back to... card taking a side-grade and its upgrades off
  exactly, milestones never dealing side-grades, Volatile Brew, the vault's extra cards, Discerning, salvaging, Merchant's Favor;
  `grid_revert.png`); `--gridshot=DIR` (the screen: `grid_1.png`, `grid_2.png`). `--dumpcards=PATH` writes the whole grid into
  `CATALOGUE.md`.

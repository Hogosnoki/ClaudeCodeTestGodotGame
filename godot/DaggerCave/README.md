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
where the heroes sit round a cooking fire; between runs, embers and rare resources buy permanent
ranks in the upgrade trees. Up to five friends can play together online, one of each
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
4. In the lobby, each player takes a hero (the one you last chose at the camp fire). Taking one
   moves you on to a second stage, as in single player: your hero's **class perks**, **loadout**
   (side-grades) and the **upgrade trees**; "Done: back to the lobby" (or Esc / B) returns to the
   lobby, and the **Loadout & perks** button there brings it back. What you set is what you start the
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
| On to the next stage (hero, then loadout & perks, then difficulty) | Enter (or R), or click the button | A |
| Loadout & perks stage: class perks, loadout, upgrade trees | P, L, U, or the buttons | The buttons (D-pad to move, A to open); BACK opens the trees |
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
under a blue sky, a cooking pot over the fire, and the cave's great dark mouth in a mossy cliff.
**Single player** brings you in to the fire, where the heroes sit on logs, and getting ready goes in
three stages (Enter / A on to the next, Esc / B back): **1 Character** (choose one; the chosen hero
stands up), **2 Loadout & perks** (class perks, side-grade loadout and upgrade trees) and **3
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
| 1-2 | Den | big round rooms in a line, almost no climbing; lots of elites; picked less often | rats, bears, goblins, bats | - | The Den Mother (bear) |
| 1-3 | Root-Choked Tunnels (the deep canopy) | branching tunnels through rock riven by massive tree roots coming down from the surface world; pools of murky water low down | rats, spiders, rot frogs, bats, mire fish, root eels | grasping roots that slow you and, if they hold you long enough, snag your weapon for a moment (two cuts clear them); thick, rotting water that leaves you gasping 50% sooner and swimming 20% slower | The Rotback (bear) |
| 1-3 | Nest | round rooms scattered up and down, joined by zig-zag tunnels that are always walkable; enemies and chests are in the rooms | scorpions, hornets, spiders | webs that slow you (cut them) | The Brood Queen (scorpion) |
| 2-3 | Ruins | right-angled halls and corridors on three floors, brickwork, shafts with stone slabs to climb, tall halls with floating slabs | skeletons, goblins, slingers, rats, scorpions | - | The Bone Knight (skeleton) |
| 3-5 | Catacombs | a stepped pyramid of vaulted halls: a great nave under a scalloped vault in the middle of the map (stepped tombs of stacked sarcophagi in its tallest bays), and out of each end a long stair down into a landing hall, another into a long gallery, and so on, outward and downward; one side ends in the Crypt Lord's rotunda (rock behind it for the treasury), the other in a sealed tomb. Cold blue stone and brickwork; lit only by a dim blue glow and the odd wall torch. Skulls and bones strewn underfoot, heaps of skulls, burial urns, ossuary walls of bone heaped against the back wall (skulls every way up, long bones jutting across them), and the barriers across the passages are banks of skulls, not boulders | crypt skeletons, grave rats, grave-robber slingers, crypt bats, spiders, restless wraiths | - | The Crypt Lord (skeleton) |
| 3-4 | Fungal Cavern | wide, low-lying tunnels, little vertical variation | sporelings, frogs, mossback golems, spore hornets | spore pods that burst into choking clouds | The Mossback Hulk |
| 3-5 | Tunnels | slightly smaller map, tight flat or diagonal passages, water low down | rats, bears, bats, fish, eels, urchins | - | The Tunnel Brute (bear) |
| 4-6 | Slime Cavern | the original half-flooded cave | goblins, frogs, magma brutes, golems, bats, spiders, fish, eels, urchins | air vents | The Cavern Colossus |
| 5-7 | Frost Caverns | mostly horizontal, ice everywhere | frost bears, rime skeletons, frost wraiths, ice bats | slippery ground; a frozen water surface (break it to swim, and break it again from below to get out); ice ledges that shatter after 3 landings or 2 blows and refreeze | The Rime Colossus |
| 5-7 | Fossil Graveyards | a few vast, echoing chambers of layered sediment; in each, the ribcage of a leviathan arches from deep in the back to just in front of you, and a great skull is sunk in the wall | fossil skeletons, bone scorpions, ossuary golems, marrow rats, bats | unstable ceilings: walk beneath one and dust sifts down and the rock groans, then a few stones break loose (each one shows where it will land) | The Ossuary Colossus |
| 6-8 | Crystal Caves | ledges in every tall space, so no long falls | shardlings, crystal golems, skeletons | crystal spikes | The Prism Golem |
| 6-8 | Old Lava Tubes | long, wide, round bores the fire cut and left (twice the usual width, a fifth of the usual branches, smooth walls) running down in great sweeps; the odd chimney carved up out of a tube's roof, too high for any jump, ends in a hidden chamber with a silver (relic) chest (the guardian is never up one); faint embers in the seams of the rock | basalt scorpions, basalt golems, magma brutes, cinder bats, ember hornets, fire elementals | - | The Basalt Warden (golem) |
| 4-7 | The Underground River | a high, long canyon with a river flowing back down it along the floor, from the guardian's end toward where you arrive, too fast to swim against; boulders hang in the river, flat on top, a jump apart: the dry way across | bats only: the roof is full of them (river, pale and blind ones), elite bats on the broad banks | the Echo Queen, a huge bat, on a high plateau at the head of the river | the river's bed holds the upgrades; the river comes from under the plateau: a tunnel to a hidden chamber with a silver chest |
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
| Attack | Medium sword: 45 px reach, 20 damage per strike, swings 0.6 s apart with a short forward lunge, heavy knockback | Shortsword: 30 px reach, 11 damage, swings every 0.36 s with a fast sweep (about 31 damage a second to the sword's 33) | Drain: tears the life out of the creature you aim at (within 175 px), 10 damage as the staff comes forward (0.06 s after the press: the creature flashes and bursts in crimson, a tether of life snapping out to it), every 0.5 s; the stolen life flies back to you as a crimson mote and becomes vital force (a tenth of the damage) when it arrives | Firebolt: a bolt of fire flying 520 px/s up to 260 px (a creature near your aim draws it), 28 damage; the staff holds two bolts, gets them back one at a time (one every 1.5 s), and throws them at least 0.25 s apart; each has a 40% chance of setting what it strikes alight (4 a second for 4 s) | Dagger Slash: 22 px reach, 9 damage, five jabs a second, one creature at a time (the nearest), with barely a hit-stop and no combo; half as fast with a dagger thrown. Each of the Rogue's blows (a jab, a throw, a recall) has a 5% chance of being a critical strike, twice as hard |
| Dodge button | Dodge roll on a short cooldown (0.55 s); a swing started mid-roll turns the roll into the strike | Hold to raise the shield (see below) | Hex: creatures around you (110 px) slow to 55% and take 20% more damage for 5 s; 6 s cooldown | Updraft: 15 alimus for a wide (4.5 m), tall (10 m) column of faint air at your feet for 10 s; it lifts nobody, but every hero in it (a friend too) has gravity at 0.4 and terminal velocity at 0.2, so jumps go far higher and falls are a slow drift | Vanish: 6 s in the shadows: creatures lose you, and you move half again as fast; attacking or being struck ends it; 12 s cooldown |
| Ability button | Charged Strike: the next swing does 50% more damage with 25% more reach, and whatever it cuts deals 20% less damage for 5 s; 12 s cooldown; instant, so it never breaks a combo | Guarded Charge: a charge behind the shield that swallows the projectiles and shockwaves in its way and keeps going, and stops at the first attacking creature it meets, breaking the attack off; it passes by creatures that aren't attacking; 3.2 s cooldown | Heal: spends 15 vital force to restore 15 health, shared among everyone in range (420 px) who is hurt, by how hurt each is (each gets 15 x their share of missing health / the sum of those shares); 3 s cooldown. All healing is pink | Blizzard: 20 alimus; a storm about 50 px across on the creature nearest your aim (or where the mouse points, or ahead of you), up to 200 px away: 9 strikes of 2 over 3 s, each with a 6% chance of freezing a regular creature solid for 1.5 s; 20 s cooldown | Dagger Throw: one of your two daggers, at the creature nearest your aim (up to 280 px), for 18 damage; it sticks in the creature it meets and rides in it, nudging it; a miss comes back by itself, and with both stuck in creatures, both come home |
| Second ability | Heaving swing, on your feet only: you're planted for 0.42 s as the sword goes up, then one great 190° arc with 30% more reach for twice a normal swing's damage and a heavy knockback, then planted 0.3 s more; a waiting Charged Strike is spent on it for more still; it breaks a rubble pile in one blow and shatters every frozen creature it strikes; 6 s cooldown | Shield bash: a short shove behind the shield; when it meets something, every creature within 40 px in front (a half-circle) takes 20 damage, is stunned for 1.6 s (half that for mini-bosses and guardians; the great bosses shrug it off) and whatever it was doing is broken off; the shield takes 20, once; it shatters frozen creatures it meets; 10 s cooldown | Rupture: spends 30 vital force (a full reserve). The creature you aim at (within 200 px) is seized where it stands and a quarter second later bursts from within for 30 damage, and every other creature within 80 px of it takes 10; the effect starts at the creature, not at you; 1.5 s cooldown | Snap: 15 alimus; every frozen creature in view shatters for 15, and every other creature within 50 px of it takes 4 | Recall: every dagger out comes home, and one stuck in a creature tears back out through it for 9, yanking it toward you; one stuck in a wall leaves it, and its foothold; no cooldown |
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

The Elementalist's alimus pays for its spells (the bolts are free): it holds 40 (more with
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
(`Hero.Floatiness`, which keeps jump height the same) and fall speed is capped at 420 px/s (`Hero.MaxFallSpeed`).
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
| Alteration | teal, glowing | changes how one ability works; an ability takes one at most, and each has upgrades of its own | milestones only (at least one on every milestone while any are left) |
| Generic | pale blue | anyone's: damage, health, speed... | the other two cards in a chest |
| Conditional | green | offered once something else is true (a double jump once you've a jump upgrade; Magma Skin from depth 6) | chests |
| Risk and reward | violet | something given, something taken; each taken once | vaults only (two in each) |

- **Level-ups** are automatic. The Swordsman gains +2 max HP, +3% damage and +1.5% swing speed per
  level; the Warden +4 max HP, +1.5% damage, 1% damage reduction and +2 shield; the Vitalist +3
  max HP, +2% damage and +1 vital force capacity; the Elementalist +3 max HP, +2% damage and +1 alimus
  capacity; the Rogue +2 max HP, +2.5% damage and +0.2% critical chance.
- **Milestones**: every 8 levels (fewer with the Path tree) you pick one of three of your hero's
  own cards (class upgrades and alterations), or leave it and take nothing.
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
| Elementalist · Blizzard | Deep Chill (frost 4% likelier to freeze, frostbolts and the blizzard, twice), Long Winter (50% longer), Whiteout (30% wider), Gathering Storm (back 25% sooner) | ◆ Firestorm: a storm of fire, 3 a strike, each with a 10% chance of setting a creature alight (it freezes nothing) |
| Elementalist · Snap | Shrapnel (bursts 40% wider), Echo (5 alimus back for each creature it bursts) | ◆ Cinder Snap: burning creatures burst instead, for 20 and 6 around |
| Rogue · Dagger Slash | Keen Edge (+5% critical chance, twice), Backstab (+50% from behind a creature), Cruel Edge (critical strikes 2.5 times as hard) | |
| Rogue · Dagger Throw | Twin Throw (both daggers at once), Weighted Daggers (+30%, twice) | ◆ Ricochet: the dagger springs on from its creature to one more nearby (130 px), then flies back, never sticking (so it rules out Tether) · Chain Ricochet (one creature more) |
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
- The Warden: a blue tabard, a gold sash, a shortsword and a buckler.
- The Vitalist: a green robe, a bone mask with glowing eyes and a crystal-headed staff.
- The Elementalist: a violet robe, an ember sash and a staff crowned with a burning orb.
  Its cooldowns are shown on the hero as small glowing orbs: two orange ones in its hands for the
  bolts it holds (white with Frostbolt, which holds a third), and three that circle it for the
  updraft (gray), the snap (yellow) and the storm (white, orange with Firestorm). An orb shrinks
  away while its spell recovers and swells back when it's ready; the bolts return one at a time,
  the last spent coming back first, and the HUD shows the bolts as pips under the alimus bar. Other
  players in an online game see the same orbs (the state rides the hero's net flags).
- The Rogue: a plum hood, a dark mask, dark leather, a short cape and a dagger in each hand.

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
knock, the hero plunges in (`splash_in`) and climbs out more gently (`splash_out`), a level-up is a
gathering swell and a deep impact, and swords whoosh. `--sfxdump=DIR` writes every effect as a
.wav for a listen.

## The Elementals

Five creatures that live where their element is (`Enemies/Elementals.cs`, looks in `Render3D/Creatures/Designs/ElementalDesigns.cs`, numbers in `Tune.Elementals`, placement in `Biomes.AddElementals`): the **Nature Elemental** (roots and fungal caves; a man of roots and leaves that looses leaf-blades and heals if left alone), the **Water Elemental** (every biome with water; drifting drops with droplets circling it, never leaves the water, spits droplets), the **Fire Elemental** (magma caverns; many small flames standing as one, quick, flings fireballs), the **Frost Elemental** (frost caverns; a crystalline golem that fans out ice shards) and the **Earth Elemental** (dens, nests, tunnels, roots, fossil graveyards; packed earth and stone with orbiting rocks, hard to knock back, hurls boulders). `--scenario=elementals` checks each winds up an attack.

Armoured creatures (golems, scorpions, shardlings and the colossi) take 40% less from physical blows, but only until the armour has turned aside a tenth of their health (`Combat.ArmorBreakShare`: what it soaks counts, not what gets through): then it falls off ("ARMOUR BROKEN"), every blow lands in full, and they sound like flesh when struck instead of stone. Earthen creatures keep their resistance.

## Motion

Heroes' legs are solved, not keyframed (`HeroDesign.Motion.cs`): each foot stays planted while it carries the body and swings through an arc to land ahead, so runs never skate; each hero has a style of stride, lean and arm carriage, and their own way of cutting or casting. Swings play the gameplay swing's own timers (wind-up, sweep, follow-through), blows land as the blade crosses the aim, and the sword's light trail is read off the blade's bone each frame. `--scenario=motion --shots=DIR` records cropped frames of runs and cuts; `--modelsheet=NAME --sheetclips=run:0.25,...` lays out poses (`run:PHASE` fixes the gait phase).

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
  One full-screen pass in the view finds the pixels just outside that mask and inks them, three and a half pixels (`edge_px`) wide
  at any distance, in the afflicted colour (poison, fire, frost) when there is one. Nothing is drawn over
  a creature itself, so there are no lines along its own limbs and folds; a creature behind rock gets
  none where it is hidden, and the line never lies over rock in front of it. (The camp and the title
  portraits have their own.)
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
  by itself; the updraft rising at your feet for its alimus, lifting nothing but letting a jump float far higher and a fall drift down slowly; no updraft without the alimus; the blizzard landing on
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
Ricochet striking two golems and never sticking, and Chain Ricochet a third; Smoke Bomb hiding you
in its cloud (and not once you're out of it) and blinding a goblin in it, and Thick Smoke's wider
cloud; Tether pulling you to your dagger in a golem and striking it as you arrive, and Pounce's
sure critical.

`--upgradetest` (headless, a few seconds) checks the rules of the cards over thousands of rolls
for every hero: a chest holds one class card and two others, never an alteration or a
risk-and-reward card; a vault's chest holds two risk-and-reward cards (while any are left) and a
rare class card, never an alteration, and a party's vault may hold any of its heroes' rare
cards; a milestone holds only the hero's own cards, with an alteration while any are left; an ability takes one
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
necks and tall climbs. The second, `Cave/FineReach.cs`, is strict: a 13 x 26 px body (a little
over, to be safe), the slowest hero's jump (no double jump, wall jump or updraft), walking up
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
- **Latch** (bat): it flies at the creature nearest your aim (within 150 px), hangs on to it for 1.2 s biting (3.6x the form damage in
  all, the creature held still unless it is a guardian), lets go, flutters back, and mends 15% of the hero's health over 5 s (`Latch*`).
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

## Support abilities (the support button, V)

Every hero has a fourth ability that does something for the others (`Player.Support.cs`, `Tune.Support`):
Swordsman **Battle Shout** (allies near and you deal 10% more for 10 s, 30 s to recharge), Warden **Taunt**
(every creature counts her as next to nothing away for 3 s), Vitalist **Health Tap** (15% of max health for
60% of the vital force reserve), Elementalist **Stalag-Might** (30 damage and the creature is rooted for
2 s, 15 s), Rogue **Expose** (the creature takes 20% more from everyone, and everyone crits it 10% more often, for 8 s), Aegis **Mending Mark**
(whoever next strikes the marked creature is healed 10, 30 s), Shape Shifter **Pack Howl** (allies near run
20% and strike 15% faster for 8 s). `--scenario=support --hero=vitalist` checks all seven.

### Loadout, Prodigy's Brand, rubble

- **Loadout** (button on the loadout & perks stage of the hero select, or `L` there): pick up to three of the hero's **side-grades** (the alterations: alternate abilities such as Frostbolt for Firebolt, one to an ability) to start the run with. While everything is unlocked for testing (`Tune.Testing.UnlockEverything`) every side-grade is listed; they will be locked away with the heroes. The give-and-take upgrades are called **risk-rewards**.
- **Healing Ward** (Aegis side-grade): the ward bolt hits for only 25% but its burst mends the Aegis and every friend in it for the full blow, which is what makes the Aegis playable alone.
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

The drain looks like nothing you would swim into: no light, only a curtain of black that thickens from nothing at the lowest point of the water to solid black, running down to the bottom of the map so that nothing below can be seen, with a negative light that
drains the colour from the water round it. Where you arrive in the Sunken Sea a waterfall pours from a crack in the roof to the beach beside you (the
lake above, coming down). Rubble piles are built from angular, flat-shaded chunks of broken rock with a real stone grain (projected from three
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
  (`Meta.JournalFound`) and listed under the story on **The story** card. Test: `--scenario=lamp`.
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
    **Warden's shield bash** shatters them too. A shatter is a snap's burst (`Player.BurstCreature`): the creature takes `Tune.Elementalist.SnapDamage` on top of
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


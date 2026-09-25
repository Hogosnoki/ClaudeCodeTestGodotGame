# Dagger Deep

A 2D side-view rogue-lite: descend through procedurally generated, half-flooded caverns as a
sword-swinging, dodging Swordsman or a shield-bearing Warden. Level up by picking stat boosts,
hunt down chests for the real upgrades, and slay the Cavern Colossus at the far end of each cave
to open a portal one depth deeper.

Open `godot/project.godot` in Godot 4.4 (.NET build) and press Play. `Scenes/DaggerDeep.tscn` is
the project's main scene; the older map-editor and demo scenes are still in `Scenes/`.

## Running it at home

1. **Install the .NET edition of Godot 4.4.x.** On godotengine.org's download page, pick "Godot
   Engine - .NET". The standard edition can't run C# and will refuse to build the project.
2. **Install the .NET 8 SDK** (dotnet.microsoft.com, ".NET 8.0", SDK). Restart your computer if
   Godot later can't find it.
3. **Get the code.** This work lives on the branch `claude/dagger-rogue-lite-cave-7snpg9`, not on
   `main`. Either:
   - On GitHub, switch the branch dropdown to it, then choose Code, then Download ZIP, and unzip it.
   - Or run `git clone <repo url>`, then `git checkout claude/dagger-rogue-lite-cave-7snpg9`.
4. **Import the project.** In Godot's Project Manager, click Import and select the file
   **`godot/project.godot`** (the `godot` folder, not the repository root). The first open takes
   a minute while Godot imports the sprite sheets.
5. **Press Play** (F5, or the triangle at the top right). Godot builds the C# code the first time.
   A controller plugged in before or during play works straight away.

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
| The two heroes | `Swordsman.*` (sword reach, damage, speed, lunge), `Warden.*` (shortsword, shield, barrier) |
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
| What the player is doing | velocity, HP share, in water, on the ground, swinging, guarding (dodging, invulnerable or shield raised), facing this creature, secondary (throw or barrier) ready |
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
| Swing | Left click, toward the mouse (J swings toward your held direction) | X, toward the right stick if held, otherwise the left stick |
| Throw dagger (swordsman) / barrier (warden) | Right click (or K) | RB or RT |
| Dodge (swordsman) / hold to raise the shield (warden) | Shift (or L); the shield points at the mouse | B, LB or LT; the shield points along the right stick, or the way you face |
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

**The cave** (`Cave/`). `CaveGenerator` carves tunnels with branching "walkers" into a scalar
field. `CaveView` then runs marching squares over that field, which makes every wall an angled
slope rather than a block. The same iso-lines become the collision segments. Above the water,
walkers never climb or drop more steeply than about 34°, so every dry tunnel can be walked in
both directions. Any slope up to 56° is walkable (`Cave.WalkableSlopeDegrees`), and moss and
grass grow on exactly those slopes, so green ground is always ground you can walk on. Steep shafts drop only into the flooded bottom half, and every flooded system a
shaft feeds is given a gentle beach plus a connector tunnel back to the main network.

After carving, a movement-aware reachability check (walk, jump, fall and swim over cells) looks
for any spot you could fall into but not climb out of. Each such pit is fixed by adding
stepping-stone ledges, and a seed is retried if that isn't enough. The same pass removes noise
specks and makes sure the boss room is reachable and far from the start.

Tall open spaces get ledge staircases that zig-zag upward one jump at a time, starting from the
water surface and from the floors of tall caverns:
- They're dense at the water line and thin out toward the roof (`Cave.PlatformDensityBottom` /
  `PlatformDensityTop`). The low caves are easy to climb around. The heights take luck or better
  movement upgrades, but are never out of reach.
- In a narrow shaft a ledge becomes a shelf on one wall, leaving a gap to drop through.
- Ledges at similar heights keep a clear sideways gap (`Cave.PlatformGapCells`) and never stack
  directly overhead, so jumping between them doesn't bump your head.

Dry tunnels are fairly narrow (`Cave.AirRadiusMin` / `AirRadiusMax`) and lean toward long
horizontal sweeps (`Cave.HorizontalBias`), so more rock is left standing to walk and climb on.
The start room has a solid floor.

The cave is about 460 x 240 cells (7360 x 3840 px), sized for roughly 15-20 minutes of exploring. Dead ends
become treasure rooms (ambush rooms exist too but are off: `Spawning.AmbushRooms`), and 3-4 of them (spread out, favoring the far reaches) are
elite mini-boss lairs. The dead end farthest from the start (measured
along the tunnels) becomes the boss arena, which gets a dome and a solid floor.

**The heroes** (`Player/`). Pick one on the title screen, or switch on the death screen, with
left / right.

| | Swordsman | Warden |
| --- | --- | --- |
| Weapon | Medium sword: 45 px reach, 20 damage per strike, slower swings (0.6 s apart) with a short forward lunge, heavy knockback | Shortsword: 30 px reach, 13 damage, swings every 0.36 s with a fast sweep |
| Defense (Shift / B / LB) | Dodge roll | Hold to raise the shield (see below) |
| Secondary (right click / RB) | Thrown dagger (2 s recharge, doesn't lock the sword) | Barrier: absorbs 5 damage for 5 s, 10 s cooldown |
| Movement | Full speed and jump | 85% speed and jump height (80% speed while shielding) |
| Toughness | 60 HP, 15 s of breath | 80 HP, 16 s of breath |

Warden's shield:
- **Blocking**: it's a broad arc of blue light (135°). It absorbs melee attacks (each blow costs
  the shield its damage once and ends that attack) and projectiles that arrive within
  its arc. Like a swing, it aims at the right stick, else the left stick on a controller, else
  the mouse. You can swing while it's up, but those swings don't combo.
- **Strength**: it holds 40 damage and regenerates 3 per second, starting 1 s after its last block.
- **Breaking**: once drained it breaks, stays at zero for 6 s, then regenerates from zero again.
- **Perfect block**: raising it at most 0.18 s before a hit counts as a perfect block, which the
  Riposte Guard and Iron Timing upgrades build on.

All of these numbers are in `Tune.Swordsman` and `Tune.Warden`.

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

**Level-ups and chests** (`Player/Upgrades.cs`). Each time, you choose one of three:
- **Level-ups** offer small stat nudges: +8 max HP, +6% damage, +6% swing speed, +5% reach, +4%
  speed, +5% jump, +10% swim, +1.5 s breath, 3% less damage taken, or longer post-hit
  invulnerability. Each hero also gets their own:
  - Swordsman: faster dodge and throw recharge.
  - Warden: shield strength, shield regeneration, and faster recovery from a break.
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
| Blade (both) | attack speed, reach, damage, Flurry (+1 strike to your combo, up to 3), Finisher (the last strike of a full combo hits much harder, requires Flurry), aerial down-slash pogo, knockback, stronger knockback |
| Sword techniques (swordsman) | Rending Edge (hits bleed for 40% more over 3 s, stacks twice), Crescent Wave (swings loose a flying slash, half damage, every 1.2 s), Executioner (+60% damage to enemies under 35% health) |
| Thrown dagger (swordsman) | ricochet *or* pierce (you can only have one), a second throw charge, faster recall, Fan of Knives (two extra daggers per throw) |
| Dodge (swordsman) | invulnerability while dodging, shorter cooldown, a second dodge charge |
| Shield and barrier (warden) | Stalwart (full speed while shielding, no knockback), Quick Mend (shield regenerates almost at once after a block, 50% faster), Restoring Ward (a barrier that takes a hit but outlasts it heals what it has left when it fades), Last Stand (once per depth, survive a killing blow at 1 HP with a fresh barrier), Riposte Guard (perfect blocks reflect projectiles), Iron Timing (perfect blocks cost the shield 70% less), Tower Shield (wider arc), barrier: shorter cooldown, longer duration, more absorption, Thorned Ward (melee attackers take back what they deal) |
| Movement (both) | wall jump, double jump *or* air dash in any direction (you can only have one), move speed, jump height, swim speed, breath |
| Survival (both) | max HP, longer invulnerability after being struck (Resilience), damage reduction, life steal, heal on kill, XP magnet |

**Enemies** (`Enemies/`). They follow some etiquette (`Tune.Combat`):
- The first time a creature wants to attack, it waits 1 s (`FirstAttackDelay`), so nothing
  strikes the instant it drops into view.
- Creatures near you take turns: an attack can't start within 0.45 s of another one
  (`AttackStagger`; the boss is exempt).
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
| Cavern Colossus (boss) | Leap slams, a roar that brings stalactites down, and a wall charge that leaves it stunned. It enrages at half health and summons bats. |

Mini-bosses are elite versions of these enemies and drop a chest.

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

Difficulty rises continuously with play time. Enemy health and damage follow 2^(minutes/10), so
they double every 10 minutes. Enemy speed, attack rate and animation speed follow the same curve,
capped at 2x so fights stay readable. They are sped up by running each enemy on a faster clock.

**Sprites** (`Art/`, generated by `tools/sprites/`). Every character is a sprite-sheet animation
played at 24 fps:

| Group | Characters |
| --- | --- |
| Heroes | the Swordsman and the Warden |
| Enemies | every enemy type |
| Boss | the Cavern Colossus |
| Passive critters | glow moths and cave crabs (new ambient wildlife) |

The game has no allies yet. Each character has separate right-facing and left-facing clips, plus
turn-around clips that rotate it through a front-facing view.

The left-facing sprites are real renders, not mirrored copies. That means the blade stays in the
hero's right hand (the far hand when facing right, the near hand when facing left), and the light
always comes from the upper left.

Both heroes are drawn by the same rig (`tools/sprites/player.py`) with a style switch:
- The Swordsman: a teal cloak, a red scarf and a medium sword.
- The Warden: a blue tabard, a gold sash, a shortsword and a buckler.

They share every clip:
- idle
- run, run start and skid stop
- turn
- jump start, rise, apex, fall and land
- wall slide
- swim and tread water
- dodge roll
- air dash
- 15 slashes: three combo strikes aimed in five directions (up, up-forward, forward, down-forward and down)
- throw
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
- Freeze-frames scaled to the hit: 0.16 s, 0.22 s on kills, 0.28 s on finishers. Only the hero and the
  creature trading the blow freeze (with a small shudder); the rest of the cave carries on.
  Mini-boss and boss kills still get a moment of whole-game slow motion.
- A white flash, an elastic squash on the creature that was hit, and an impact spark.
- A camera kick in the direction of the blow, plus controller rumble.
- Damage numbers that pop in.
- A swing smear that is brightest at the blade's leading edge, with a golden echo on finishers.
- Afterimage trails on dodges and air dashes.

**Presentation**. The cave, water, lighting and effects are drawn procedurally. The rock is
vertex-colored and has moss, stalactites, glowing mushrooms, crystals and glow worms. There's
also animated water, a torchlight vignette, particles and a minimap.

All sound effects and both music loops (ambient exploration and boss) are synthesized at startup
(`Audio/`). The sprite sheets in `Art/` are the game's only asset files.

## Test harness

The harness takes command-line user args after `--`:

```
# 30 seeds: generation time, retries, trap cells (must be 0), rooms, boss distance
godot --headless --path godot -- --gentest

# The autopilot bot plays for 60 s and saves a screenshot every 3 s
xvfb-run godot --path godot --rendering-driver opengl3 -- --autotest --seed=1013 --duration=60 --shots=/tmp/shots

# Spawns one of every creature and screenshots it (on land, underwater, then the boss)
xvfb-run godot --path godot --rendering-driver opengl3 -- --seed=1013 --bestiary --shots=/tmp/shots
```

`--start=boss` and `--start=water` change where the player spawns, and `--seed=N` fixes the cave.

To record gameplay, `--showcase` runs a directed demo (a land fight, then underwater, then the
boss). Combine it with Godot's movie writer, which renders frame by frame at a fixed rate and
captures the audio:

```
xvfb-run godot --path godot --rendering-driver opengl3 --write-movie /tmp/v/f.png --fixed-fps 30 -- --showcase --seed=1013 --duration=31
```

To check the neural network's maths (a gradient check, learning a simple task from rewards, and
a save/load round trip), run:

```
godot --headless --path godot -- --nntest
```

To have the autopilot bot train the brains, add `--train` to an `--autotest` run. Add
`--braindir=DIR` to keep those brains out of the project.

`--herotest` (add `--hero=warden` for the Warden) scripts checks of each hero's mechanics and
prints ok / FAIL for each:
- the sword's reach and lunge;
- the shield blocking from the front but not from behind;
- the shield breaking, staying down and recovering;
- a perfect block reflecting a shot;
- the barrier absorbing a hit;
- the game clock never slowing for a hit-stop.

`--hero=warden` also works with `--autotest` and the other modes.

Two more test modes:
- `--animtest --shots=DIR` scripts the player through every movement and attack transition (run,
  turn, stop, jump, land, slashes, dodge, throw, hurt) and saves a frame every 1/20 s.
- `--padtest` drives the game with synthetic controller events: start, move, swing, throw, dodge,
  pick an upgrade from the level-up cards, pause and unpause. It prints what happened at each
  step.

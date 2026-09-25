# Dagger Deep

A 2D side-view rogue-lite: descend through procedurally generated, half-flooded caverns as a
dagger wielder, level up by picking one of three upgrades, and slay the Cavern Colossus at the
far end of each cave to open a portal one depth deeper.

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
| Survivability | `Hero.StartHp`, `Hero.HurtInvuln`, `Drops.HeartChance` |
| Hit weight | `Feel.HitStop*`, `Feel.Kick*` |
| How fast it gets hard | `Difficulty.DoublingMinutes`, `Difficulty.TempoCap` |
| How busy it is | `Spawning.IntervalStart`, `Spawning.IntervalMin`, `Spawning.ResidentFillStart`, `Spawning.CapBase` |
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
| What the player is doing | velocity, HP share, in water, on the ground, swinging the dagger, dodging or invulnerable, facing this creature, holding a dagger to throw |
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
| Swing dagger | Left click, toward the mouse (J swings toward your held direction) | X, toward the right stick if held, otherwise the left stick |
| Throw dagger | Right click (or K) | RB or RT |
| Dodge | Shift (or L) | B, LB or LT |
| Pause | Esc | Start |
| Pick upgrade | Click, 1 / 2 / 3, or arrows + Enter | D-pad or stick left / right, then A |
| Start / restart | Enter or click / R | A / Y |
| Enemy training (debug) | F9 on/off, F10 save now, F8 move labels | - |

You can switch between the two at any time. The game follows whichever device you used last: it
hides the mouse cursor, changes the on-screen button prompts, and turns rumble on for hits, kills
and damage taken while a controller is active.

## What's in it

**The cave** (`Cave/`). `CaveGenerator` carves tunnels with branching "walkers" into a scalar
field. `CaveView` then runs marching squares over that field, which makes every wall an angled
slope rather than a block. The same iso-lines become the collision segments. Above the water,
walkers never climb or drop more steeply than about 34°, so every dry tunnel can be walked in
both directions. Steep shafts drop only into the flooded bottom half, and every flooded system a
shaft feeds is given a gentle beach plus a connector tunnel back to the main network.

After carving, a movement-aware reachability check (walk, jump, fall and swim over cells) looks
for any spot you could fall into but not climb out of. Each such pit is fixed by adding
stepping-stone ledges, and a seed is retried if that isn't enough. The same pass removes noise
specks and makes sure the boss room is reachable and far from the start. The cave is about
400 x 240 cells (6400 x 3840 px), sized for roughly 15-20 minutes of exploring. Dead ends
become treasure rooms or ambushes, and 3-4 of them (spread out, favoring the far reaches) are
elite mini-boss lairs. The dead end farthest from the start (measured
along the tunnels) becomes the boss arena, which gets a dome and a solid floor.

**The dagger wielder** (`Player/`). Movement has coyote time, jump buffering and variable jump
height. Swimming has a breath meter; you take drowning damage when it runs out and the audio is
muffled while your head is underwater. You can swing the dagger in any direction and dodge. The
thrown dagger locks your weapon for 2 s per charge, and you can keep swinging while any dagger is
still in hand, and it flies as a whirling blade.

You start with 60 HP. After being struck you are invulnerable for 0.4 s, which the Resilience
upgrade extends. Healing is scarce: hearts drop from 2% of regular kills and 40% of mini-bosses.

**Upgrades** (`Player/Upgrades.cs`). These come from XP level-ups and treasure chests, and each
time you choose one of three:

| Area | Upgrades |
| --- | --- |
| Dagger | attack speed, reach, damage, combo (a hit resets your swing cooldown), finisher (a harder third strike, requires combo), aerial down-slash pogo, knockback, stronger knockback |
| Throw | ricochet *or* pierce (you can only have one), a second throw charge, faster recall |
| Movement | wall jump, double jump *or* air dash in any direction (you can only have one), move speed, jump height, swim speed, breath |
| Dodge | invulnerability while dodging, shorter cooldown, a second dodge charge |
| Survival | max HP, longer invulnerability after being struck (Resilience), damage reduction, life steal, heal on kill, XP magnet |

**Enemies** (`Enemies/`). Each one moves differently:

| Enemy | Behavior |
| --- | --- |
| Bat | Roosts on the ceiling, then swoops at you on a wobbling path and retreats. |
| Frog | Hops in arcs and lashes its tongue from range. It can also swim. |
| Goblin | Runs at you, hops gaps and clubs you. The slinger variant keeps its distance and lobs rocks. |
| Spider | Crawls along the ceiling and drops on a silk thread when you pass underneath. If you cut the thread, it pounces along the ground. |
| Magma Brute | Leaves burning puddles and lobs lava globs. Water boils it away. |
| Golem | Slow and almost impossible to knock back. It telegraphs a ground slam that sends shockwaves along the floor, which you can jump over. |
| Cave Fish | Darts at you in water. It also leaps out at you on shore, then flops around on land. |
| Urchin | Stationary on the seabed and pulses its spikes outward. |
| Eel | Hides in a wall burrow and can't be hurt there. It lunges along a line at swimmers. |
| Cavern Colossus (boss) | Leap slams, a roar that brings stalactites down, and a wall charge that leaves it stunned. It enrages at half health and summons bats. |

Mini-bosses are elite versions of these enemies and drop a chest.

**Spawning and difficulty**. Enemies come from two sources:
- **Residents** sit at points scattered through the tunnels. They are only ever created while out
  of view, so they are already there when you arrive. Early in a run many points stay empty;
  after about 8 minutes all of them fill.
- **Entrances** arrive in waves on a timer: about every 22 s at first, shortening to about every
  5 s. Each wave picks a point along the tunnels just out of view (so it has a route to you), and
  the enemies come to you their own way: bats fly in, frogs hop, goblins run, fish swim.

Difficulty rises continuously with play time. Enemy health and damage follow 2^(minutes/10), so
they double every 10 minutes. Enemy speed, attack rate and animation speed follow the same curve,
capped at 2x so fights stay readable. They are sped up by running each enemy on a faster clock.

**Sprites** (`Art/`, generated by `tools/sprites/`). Every character is a sprite-sheet animation
played at 24 fps:

| Group | Characters |
| --- | --- |
| Player | the player |
| Enemies | every enemy type |
| Boss | the Cavern Colossus |
| Passive critters | glow moths and cave crabs (new ambient wildlife) |

The game has no allies yet. Each character has separate right-facing and left-facing clips, plus
turn-around clips that rotate it through a front-facing view.

The left-facing sprites are real renders, not mirrored copies. That means the dagger stays in the
rogue's right hand (the far hand when facing right, the near hand when facing left), and the light
always comes from the upper left.

The player's clips cover:
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

**Game feel**. Hits have several layers:
- Freeze-frames scaled to the hit: longer for finishers and kills, with slow motion for mini-boss
  and boss kills.
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

Two more test modes:
- `--animtest --shots=DIR` scripts the player through every movement and attack transition (run,
  turn, stop, jump, land, slashes, dodge, throw, hurt) and saves a frame every 1/20 s.
- `--padtest` drives the game with synthetic controller events: start, move, swing, throw, dodge,
  pick an upgrade from the level-up cards, pause and unpause. It prints what happened at each
  step.

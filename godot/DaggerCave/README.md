# Dagger Deep

A 2D side-view rogue-lite: descend through procedurally generated, half-flooded caverns as a
dagger wielder, level up by picking one of three upgrades, and slay the Cavern Colossus at the
far end of each cave to open a portal one depth deeper.

Open `godot/project.godot` in Godot 4.4 (.NET build) and press Play. `Scenes/DaggerDeep.tscn` is
the project's main scene; the older map-editor and demo scenes are still in `Scenes/`.

## Controls

| Action | Keyboard / mouse | Gamepad |
| --- | --- | --- |
| Move | A / D (or arrows) | Left stick |
| Swim up / down | W / S (Space also swims up) | Left stick |
| Jump | Space | A |
| Swing dagger (toward the mouse) | Left click (J swings toward your held direction) | X, aim with right stick |
| Throw dagger | Right click (or K) | RB |
| Dodge | Shift (or L) | B |
| Pause | Esc | Start |
| Pick upgrade | Click or 1 / 2 / 3 | |

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
specks and makes sure the boss room is reachable and far from the start. Dead ends become
treasure rooms, elite mini-boss rooms or ambushes. The dead end farthest from the start (measured
along the tunnels) becomes the boss arena, which gets a dome and a solid floor.

**The dagger wielder** (`Player/`). Movement has coyote time, jump buffering and variable jump
height. Swimming has a breath meter; you take drowning damage when it runs out and the audio is
muffled while your head is underwater. You can swing the dagger in any direction and dodge. The
thrown dagger locks your weapon for 2 s per charge, and you can keep swinging while any dagger is
still in hand.

**Upgrades** (`Player/Upgrades.cs`). These come from XP level-ups and treasure chests, and each
time you choose one of three:

| Area | Upgrades |
| --- | --- |
| Dagger | attack speed, reach, damage, combo (a hit resets your swing cooldown), finisher (a harder third strike, requires combo), aerial down-slash pogo, knockback, stronger knockback |
| Throw | ricochet *or* pierce (you can only have one), a second throw charge, faster recall |
| Movement | wall jump, double jump *or* air dash in any direction (you can only have one), move speed, jump height, swim speed, breath |
| Dodge | invulnerability while dodging, shorter cooldown, a second dodge charge |
| Survival | max HP, damage reduction, life steal, heal on kill, XP magnet |

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

Mini-bosses are elite versions of these enemies and drop a chest. Enemies spawn from
pre-computed points just outside your view as you explore, and those points re-arm after a while
once you're far away.

**Presentation**. Everything is drawn procedurally with Godot draw calls, and every enemy has its
own animation. The rock is vertex-colored and has moss, stalactites, glowing mushrooms, crystals
and glow worms. There's also animated water, a torchlight vignette, particles, hit-stop, screen
shake and a minimap. All sound effects and both music loops (ambient exploration and boss) are
synthesized at startup (`Audio/`), so the game has no asset files.

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

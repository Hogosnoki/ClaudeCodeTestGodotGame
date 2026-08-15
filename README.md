# Procedural Map Generation — Milestone 1

Core mechanics for the procedural map generation system, proven end-to-end. See "Confirmed
decisions" below for the architecture questions this raised before implementation started.

## Layout

```
engine/ProcGen.Engine/    The generation engine. Plain C# class library, net8.0, zero
                           dependency on Godot or any UI/editor code. This is the single
                           source of truth both the tool and the game call into.
tests/ProcGen.Engine.Tests/  xUnit tests proving the milestone-1 acceptance criteria against
                           the engine directly (no Godot runtime needed to run these).
godot/                    Minimal Godot 4.4 C# project. One test scene
                           (Scripts/Milestone1TestScene.cs) that references the engine
                           project and calls into it exactly like the game/tool will.
```

`ProceduralMapGen.sln` at the repo root ties all three projects together.

## Confirmed decisions

Three architectural questions were flagged in the spec as needing confirmation before writing
code. They were resolved as follows:

1. **Engine language: pure C#, no GDExtension.** The engine (`engine/ProcGen.Engine`) is a plain
   .NET class library with no reference to `GodotSharp` at all — it doesn't know Godot exists.
   Both the tool and the game reference it as an ordinary project/assembly reference. The "one
   engine, two thin consumers, can't drift apart" requirement is enforced by that assembly
   boundary rather than by a native/managed boundary: nothing UI- or editor-specific can leak into
   `ProcGen.Engine` because it can't call into `GodotSharp` even if someone tried.
2. **Noise construction: Perlin-style gradient noise with a quintic fade curve** ("improved
   noise"), extended to three axes (X, Y, Transformation) by treating Transformation as an
   ordinary third lattice axis interpolated exactly like X/Y. See
   `engine/ProcGen.Engine/Noise/LatticeNoise3D.cs`.
3. **Save/export format: Godot Resource (`.tres`/`.res`).** Not yet implemented (see "Deferred by
   design" below) — the engine's plain C# data model is what will get mirrored by a thin Godot
   `Resource` adapter layer living in `godot/`, not folded into the engine itself.

## Determinism

Same seeds + parameters + overrides → bit-identical output, verified twice: once by
`DeterminismTests` running the engine directly, and once live inside the actual Godot runtime by
the test scene (see "Running the milestone-1 test scene" below).

The cross-platform risk called out in the spec — native floating-point/library differences
between platforms — is addressed by construction:
- Gradient selection uses a pure-integer avalanche hash (unchecked 32-bit arithmetic, which .NET
  guarantees wraps identically on every platform/architecture it targets) into a small fixed table
  of gradient vectors, not a float-based hash.
- No trigonometric/transcendental functions (`Sin`, `Cos`, etc. — whose `libm` implementations can
  differ subtly across platforms) appear anywhere in the noise path.
- No fused-multiply-add is used (.NET does not auto-contract `+`/`*` into FMA; only an explicit
  `Math.FusedMultiplyAdd` call would introduce that risk).
- The only floating-point operations are `+ - * /` on `double`, which IEEE 754 defines exactly.

This reasoning is documented in `LatticeNoise3D`'s doc comment so it stays next to the code it
governs.

## Weighted-range tile selection

Implemented as a precomputed cumulative-bounds table (`Selection/CompiledLayer.cs`) plus a binary
search (`Selection/TileSelector.cs`) — mathematically equivalent to the spec's "sum ranges, walk
the list subtracting" description, just O(log n) instead of O(n) per cell. The selection result
exposes the continuous normalized value and the selected tile's bounds (not just the discrete tile
id), so a renderer can compute proximity to a range boundary and blend — the hook the spec asks
for, though the milestone-1 scene doesn't render a blended view yet.

Note: the spec's worked example states `0.7 + 0.3 + 0.2 + 2.0 = 2.2`; that sum is actually 3.2.
The engine implements the described *mechanism* correctly; tests use the correct sum. Worth
double-checking against your intended tile weights when you move past the example values.

## Layers and `writes_over`

`Generation/MapGenerator.cs` resolves a region cell-by-cell, layer by layer, bottom to top. Each
layer's `writes_over` rules are checked against a per-cell dictionary of every lower layer's
already-resolved (and already-override-applied) output — not just the immediately preceding
layer — so a layer can reference any lower layer by id. A layer with an empty `writes_over` list
is a base layer and is always eligible. Manual overrides are applied per layer as the final step,
regardless of whether the procedural filter matched, and the *post-override* value is what higher
layers see when evaluating their own filters — matching the composition order the spec asks for.

`writes_over` with multiple rules matches on **any** rule (logical OR) — the spec's examples only
show one rule per layer, so this is a documented design choice, not something pinned down by the
spec text.

## Running it

```bash
# Engine unit tests (32 tests, no Godot needed):
dotnet test tests/ProcGen.Engine.Tests/ProcGen.Engine.Tests.csproj

# Milestone-1 scene, live inside real Godot (requires the Godot 4.4 mono/.NET editor binary):
cd godot
godot --headless --path .
```

The scene prints an ASCII render of the generated region, the Transformation-axis agreement
percentages between T=0.0/0.1/0.2, the override + `writes_over` re-evaluation check, and the
determinism check — then exits with code 0 on success. This was run against the actual
Godot 4.4.1 mono editor binary during development; sample output:

```
Agreement T0.0 vs T0.1: 98.2 % (same style, gradually different arrangement)
Agreement T0.1 vs T0.2: 98.4 %
Cell (0,14) procedurally: ground=sand, ground_cover=(none)
After hand-painting ground=(0,14) -> land: ground_cover=grass (re-evaluated writes_over against the override)
PASS: regeneration is bit-identical.
```

## Deferred by design (per "stop there" in the spec)

Not built yet, on purpose — milestone 1 is only meant to prove the core mechanics:

- More than 2 layers, and the `no_override` pseudo-tile.
- **Zones** (spatially-varying parameter overrides). Integration point to keep in mind: the
  resolution loop in `MapGenerator` currently compiles one `CompiledLayer` per `LayerDef` for the
  whole region. Zones will need the per-cell noise-parameters/tile-list lookup to become
  position-dependent (a zone's override merged over the layer's base parameters at that specific
  cell) rather than one fixed `CompiledLayer` per layer per region. The cell loop already resolves
  everything per-cell, so this should be a matter of swapping "look up this layer's compiled tile
  list" for "look up this layer's compiled tile list *at this position*" — not a restructure.
- The Godot `Resource` save/export adapter (layer definitions + seeds + override diffs, mirroring
  `MapDefinition`/`OverrideStore` 1:1, living in `godot/` so the engine stays Godot-free) and the
  full editor tool UI.

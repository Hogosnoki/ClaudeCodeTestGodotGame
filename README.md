# Procedural Map Generation — Milestone 1 + integration package

Core mechanics for the procedural map generation system, proven end-to-end, plus a copy-paste
integration package for dropping the engine into any Godot project. See "Confirmed decisions"
below for the architecture questions this raised before implementation started, and
**[`INTEGRATION.md`](INTEGRATION.md)** for how to bring this into your own project, render it with
real tile art, and visually debug it.

## Layout

```
engine/ProcGen.Engine/    The generation engine. Plain C# class library, net8.0, zero
                           dependency on Godot or any UI/editor code. This is the single
                           source of truth both the tool and the game call into. This is
                           also the folder you copy-paste into another Godot project --
                           see INTEGRATION.md.
tests/ProcGen.Engine.Tests/  xUnit tests proving the milestone-1 acceptance criteria against
                           the engine directly (no Godot runtime needed to run these).
godot/                    Minimal Godot 4.4 C# project demonstrating both consumers:
  Integration/               Godot-dependent rendering/debug helpers (ProcGenTileMapView,
                              ProcGenDebugOverlay) -- the other copy-paste folder. Thin
                              consumers of the engine, not part of it.
  Scripts/Milestone1TestScene.cs   Headless console proof of the milestone-1 scenario.
  Scripts/VisualDebugDemoScene.cs  Interactive, on-screen demo: colored-cell rendering,
                                    click-to-paint overrides, Transformation scrubbing.
  Scripts/MapEditorToolScene.cs    The map-making tool: a fixed side property panel
                                    (region, Transformation, per-layer seed, per-layer tile
                                    ranges with manual entry + nudge buttons) driving live
                                    regeneration, plus click-to-paint on top.
docs/                     Reference screenshots for INTEGRATION.md / this README.
INTEGRATION.md            How to copy this into your own Godot project, render with real
                           tile art, and visually debug generation.
```

`ProceduralMapGen.sln` at the repo root ties all the C# projects together.

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

## The map editor tool

`godot/Scenes/MapEditorTool.tscn` is the actual "map-making tool" from the spec: a fixed property
panel docked to the right, map view on the left, no hand-painting-every-tile required. Every field
writes straight into the live `MapDefinition`/`OverrideStore` the engine consumes and triggers
`MapGenerator.GenerateRegion` again — there's no separate "tool state" that could drift from what
actually gets generated.

![Map editor tool: region/Transformation/layer/seed/tile-range panel on the right, live-generated map on the left, one hand-painted override outlined](docs/map_editor_tool_reference.png)

Panel sections, top to bottom:
- **Region** — origin X/Y and width/height of the generated region, so you can generate/view any
  arbitrary area of X/Y space, not just a fixed one.
- **Transformation** — `-0.1`/`+0.1` buttons plus a manually-editable field; both paths go through
  `TransformationAxis.ClampStep`, so typing an oversized jump gets clamped exactly like an
  oversized nudge would.
- **Layer** — click to choose which layer you're working on; also switches the map view to that
  layer's raw resolution (or check "Show final composite" to see the composited result instead).
- **Seed (selected layer)** — that layer's X/Y/T position in the generation lattice, freely
  editable.
- **Tiles (selected layer)** — one row per tile: id, a `-` button, a manually-editable range field,
  and a `+` button, each nudge moving the range by 0.1 exactly as asked for. Changing a range
  regenerates immediately, so the effect on the map is visible right away.

Left-click on the map still cycles a manual override on the selected layer at that cell (same
mechanism as the visual debug demo); right-click clears it. Painting is the exception path for
when a setting alone can't express what you want — tuning ranges/seeds/region is meant to be how
you shape most of a map.

This was built and verified the same way as the rest of the tool consumers: run in the real Godot
4.4.1 editor under Xvfb + software OpenGL, driven with actual `xdotool` clicks and keystrokes —
selecting layers, nudging tile ranges and watching the map reshape live, typing a seed value
directly into a field, and painting/clearing an override — not just built and assumed to work.

## Running it

```bash
# Engine unit tests (33 tests, no Godot needed):
dotnet test tests/ProcGen.Engine.Tests/ProcGen.Engine.Tests.csproj

# Milestone-1 console proof, live inside real Godot (requires the Godot 4.4 mono/.NET editor binary):
cd godot
godot --headless --path .

# Interactive visual debugger (open in the editor and press F6, or run directly):
godot --path . res://Scenes/VisualDebugDemo.tscn

# The map editor tool -- property panel + live regeneration:
godot --path . res://Scenes/MapEditorTool.tscn
```

The headless scene prints an ASCII render of the generated region, the Transformation-axis
agreement percentages between T=0.0/0.1/0.2, the override + `writes_over` re-evaluation check, and
the determinism check — then exits with code 0 on success. This was run against the actual
Godot 4.4.1 mono editor binary during development; sample output:

```
Agreement T0.0 vs T0.1: 98.2 % (same style, gradually different arrangement)
Agreement T0.1 vs T0.2: 98.4 %
Cell (0,14) procedurally: ground=sand, ground_cover=(none)
After hand-painting ground=(0,14) -> land: ground_cover=grass (re-evaluated writes_over against the override)
PASS: regeneration is bit-identical.
```

The interactive scene was similarly run and driven with real synthetic input (via `xdotool`,
under Xvfb + software OpenGL) during development — see [`INTEGRATION.md`](INTEGRATION.md) for a
screenshot and the full walkthrough of what it demonstrates (layer switching, click-to-paint
overrides with a visible override marker, and the Transformation step clamp visibly capping an
oversized jump).

## Copy-paste integration package

`engine/ProcGen.Engine/` (minus its `.csproj`/`bin`/`obj`) and `godot/Integration/` are the two
folders meant to be copied into any other Godot project as-is — no project-reference plumbing, no
NuGet package. This was verified for real: those exact files were copied into a brand-new,
unrelated Godot project with nothing but the default C# template, and it built and ran with zero
warnings. Full walkthrough, including rendering with real tile art and the no-art-required visual
debugger, is in [`INTEGRATION.md`](INTEGRATION.md).

## Deferred by design (per "stop there" in the spec)

Not built yet, on purpose:

- More than 2 layers, and the `no_override` pseudo-tile.
- **Zones** (spatially-varying parameter overrides). Integration point to keep in mind: the
  resolution loop in `MapGenerator` currently compiles one `CompiledLayer` per `LayerDef` for the
  whole region. Zones will need the per-cell noise-parameters/tile-list lookup to become
  position-dependent (a zone's override merged over the layer's base parameters at that specific
  cell) rather than one fixed `CompiledLayer` per layer per region. The cell loop already resolves
  everything per-cell, so this should be a matter of swapping "look up this layer's compiled tile
  list" for "look up this layer's compiled tile list *at this position*" — not a restructure.
- The Godot `Resource` save/export adapter (layer definitions + seeds + override diffs, mirroring
  `MapDefinition`/`OverrideStore` 1:1, living in `godot/` so the engine stays Godot-free).
- In the editor tool specifically: add/remove/reorder layers and tiles, `writes_over` filter
  editing, noise parameter editing (octaves/frequency/persistence/lacunarity), and a pan/zoom
  camera for the map view (the region panel's Origin X/Y fields cover "any arbitrary region," just
  not by dragging).

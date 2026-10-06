using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>--loadshots=DIR: every biome's loading screen, saved as DIR/load_NAME.png (a look at the screens, not a game).</summary>
    private async void RunLoadShots(string dir)
    {
        _hud.Visible = false;
        foreach (var b in Biomes.All)
        {
            _loadScreen.Open(b, Math.Max(b.MinDepth, 1));
            await ToSignal(GetTree().CreateTimer(0.9), SceneTreeTimer.SignalName.Timeout);
            GetViewport().GetTexture().GetImage()?.SavePng($"{dir}/load_{b.Id}.png");
        }
        GD.Print("[loadshots] done");
        SafeQuit.Request(this);
    }

    // ---------------------------------------------------------------- --scenario=loading

    private int _ldPhase;
    private float _ldT;
    private ulong _ldStart;

    /// <summary>--scenario=loading: taking an exit puts the loading screen up at once, the next cave is made behind it, and the game goes on in it.</summary>
    private void LoadingScenario()
    {
        var p = G.Player;
        _ldT += (float)GetProcessDeltaTime();
        switch (_ldPhase)
        {
            case 0:
                if (_ldT < 0.6f) return;
                _forceLoadScreen = true;
                _ldStart = Time.GetTicksMsec();
                EnterExit(Biomes.Get(BiomeId.Catacombs), 3);
                _ldPhase = 1; _ldT = 0;
                break;
            case 1:
                if (_ldT < 0.05f) return;
                ScCheck($"the loading screen is up at once ({_loadScreen.IsOpen}, loading {_loading})", _loadScreen.IsOpen && _loading);
                ScCheck($"and the game holds still behind it (paused {GetTree().Paused})", GetTree().Paused);
                _ldPhase = 2; _ldT = 0;
                break;
            case 2:
                if (_loading && _ldT < 20f) return;
                ScCheck($"the cave was made: {G.Biome.Name} at depth {G.Depth} ({Time.GetTicksMsec() - _ldStart} ms, attempts {G.Cave?.Attempts})", G.Cave != null && G.Cave.Biome.Id == BiomeId.Catacombs && G.Depth == 3 && !_loading);
                ScCheck($"the game runs on in it (paused {GetTree().Paused}, hero {G.Player != null && IsInstanceValid(G.Player)})", !GetTree().Paused && G.Player != null && IsInstanceValid(G.Player));
                _ldPhase = 3; _ldT = 0;
                break;
            case 3:
                if (_ldT < 1.5f) return;
                ScCheck($"and the screen is gone ({_loadScreen.Visible})", !_loadScreen.Visible);
                ScCheck($"the hero stands in the new cave's start ({G.Player.GlobalPosition.DistanceTo(G.Cave.StartPos):0} px away)", G.Player.GlobalPosition.DistanceTo(G.Cave.StartPos) < 400);
                ScEnd();
                break;
        }
    }

    private int _abPhase, _abDepth;
    private float _abT;

    /// <summary>
    /// `--scenario=abyss --forcedrain`: a lake level with its drain; the hero swims down into it and comes up in the sunken sea (the
    /// next depth, no guardian, two exits open from the start), whose exits lead on from there.
    /// </summary>
    private void AbyssScenario()
    {
        var p = G.Player;
        _abT += (float)GetProcessDeltaTime();
        switch (_abPhase)
        {
            case 0:
                if (_abT < 0.6f) return;
                var drain = G.Cave.Drain;
                ScCheck($"this lake has a drain ({drain != null})", drain != null);
                var portal = _world.GetChildren().OfType<Portal>().FirstOrDefault(x => x.Drain);
                ScCheck("and it is there to be swum into", portal != null && portal.Depth == G.Depth + 1 && portal.To.Id == BiomeId.Abyss);
                if (drain == null || portal == null) { ScEnd(); return; }
                _abDepth = G.Depth;
                p.GlobalPosition = drain.Value + new Vector2(0, -90);
                p.Velocity = Vector2.Zero;
                ScShot("drain");
                _abPhase = 1; _abT = 0;
                break;
            case 1:
                // (sinking: nothing to do but wait for the loading screen)
                if (_loading || G.Biome.Id == BiomeId.Abyss) { _abPhase = 2; _abT = 0; }
                else if (_abT > 20f) { ScCheck("swimming into the drain takes you down", false); ScEnd(); }
                else if (G.Cave.Drain is Vector2 d0) p.GlobalPosition = p.GlobalPosition.MoveToward(d0, 3f);
                break;
            case 2:
                if (_loading && _abT < 90f) return;
                ScCheck($"the sunken sea: {G.Biome.Name} at depth {G.Depth} (from {_abDepth})", G.Biome.Id == BiomeId.Abyss && G.Depth == _abDepth + 1 && !_loading);
                ScCheck($"it has no drain of its own ({G.Cave.Drain == null})", G.Cave.Drain == null);
                var exits = _world.GetChildren().OfType<Portal>().Where(x => !x.Drain && !x.Outside).ToList();
                ScCheck($"and its way on is open from the start: {exits.Count} exits ({string.Join(", ", exits.Select(x => x.Depth))})", exits.Count == 2 && exits.Any(x => x.Depth == G.Depth + 1) && exits.Any(x => x.Depth == G.Depth + 2));
                ScCheck($"no guardian ({ActiveBoss == null})", ActiveBoss == null);
                ScCheck($"water-dwellers about ({G.Cave.Spawns.Count(s => s.Kind is SpawnKind.Water or SpawnKind.WaterFloor or SpawnKind.WaterWall)} spawn points)", G.Cave.Spawns.Count(s => s.Kind is SpawnKind.Water or SpawnKind.WaterFloor or SpawnKind.WaterWall) > 10);
                ScCheck($"chests: {Chest.All.Count}", Chest.All.Count >= 8);
                _abPhase = 3; _abT = 0;
                break;
            case 3:
                if (_abT < 1.5f) return;
                ScShot("sea_start");
                var way = _world.GetChildren().OfType<Portal>().First(x => !x.Drain && !x.Outside);
                p.GlobalPosition = way.GlobalPosition + new Vector2(0, 17);
                _abPhase = 4; _abT = 0;
                break;
            case 4:
                if (_abT < 1.5f) return;
                ScShot("sea_exit");
                ScEnd();
                break;
        }
    }
}

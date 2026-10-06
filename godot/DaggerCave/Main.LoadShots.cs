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
}

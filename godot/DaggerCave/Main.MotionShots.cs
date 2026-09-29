using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private int _moDir, _moNext;
    private Enemy _moFoe;
    private string _moTag = "";
    private int _moShot, _moEvery = 2;
    private float _moUntil;

    /// <summary>
    /// --scenario=motion --shots=DIR [--hero=...] [--cam3d=0,4,8] (with --fixed-fps 60): the hero runs, then cuts three times
    /// at a golem in reach (a, b and the finisher), then runs and cuts on the move; a cropped picture of the hero every few
    /// frames (mo_TAG_NNN.png) for judging the poses and where the blade's light falls.
    /// </summary>
    private void MotionScenario()
    {
        var p = G.Player;
        if (_moDir == 0)
        {
            if (_scT < 0.3f) return;
            // toward whichever side has open floor (the start spot varies by cave)
            int Open(int side) { int n = 0; for (int k = 1; k <= 10; k++) if (!G.Cave.IsSolid(p.GlobalPosition + new Vector2(side * k * 18, -10)) && G.Cave.FindFloor(p.GlobalPosition + new Vector2(side * k * 18, -10), 40, out _)) n++; else break; return n; }
            _moDir = Open(1) >= Open(-1) ? 1 : -1;
            p.Stats.ThirdCombo = true;
            p.Stats.ComboResets = 2;
            p.Stats.MaxHp = 5000; p.Hp = 5000;
            _scT = 0;
        }
        foreach (var e in G.Enemies.ToArray()) if (e != _moFoe && !e.IsBoss) e.QueueFree();
        float t = _scT;
        bool Edge(float at) => t >= at && t - (float)GetProcessDeltaTime() < at;
        var run = new Vector2(_moDir, 0);
        _scInput = default;
        // (steps: each begins a capture that lasts a moment)
        if (t > 0.6f && t < 1.9f) _scInput.Move = run;
        if (Edge(1.3f)) Begin("run", 0.7f, 3);
        if (Edge(2.2f))
        {
            _moFoe = new Golem { Position = p.GlobalPosition + new Vector2(_moDir * 34f, -8f) };
            _moFoe.SetMeta("test", true);
            _moFoe.MaxHp = _moFoe.Hp = 99999;
            _world.AddChild(_moFoe);
            _moFoe.Freeze(60f, hold: true);
        }
        if (Edge(2.7f)) { _scInput.Attack = true; _scInput.Aim = run; Begin("cutA", 0.42f, 1); }
        if (Edge(3.3f)) { _scInput.Attack = true; _scInput.Aim = run; Begin("cutB", 0.42f, 1); }
        if (Edge(3.9f)) { _scInput.Attack = true; _scInput.Aim = run; Begin("cutC", 0.5f, 1); }
        if (Edge(4.6f) && IsInstanceValid(_moFoe)) _moFoe.QueueFree();
        if (t > 4.9f && t < 6.3f) _scInput.Move = run;
        if (Edge(5.5f)) { _scInput.Attack = true; _scInput.Aim = run; Begin("runcut", 0.5f, 1); }
        if (Edge(6.5f)) { _scInput.Attack = true; _scInput.Aim = new Vector2(_moDir, -1f).Normalized(); Begin("cutUp", 0.42f, 1); }
        if (t > 7.4f) { ScCheck("the motion frames were saved", _moShot > 20); ScEnd(); return; }
        if (t < _moUntil && ++_moNext % _moEvery == 0)
        {
            var img = GetViewport().GetTexture().GetImage();
            var sp = p.GetGlobalTransformWithCanvas().Origin;
            var scale = img.GetSize() / GetViewport().GetVisibleRect().Size;
            int w = 260, h = 200;
            var r = new Rect2I((int)(sp.X * scale.X) - w / 2, (int)(sp.Y * scale.Y) - h * 3 / 5, w, h);
            r = r.Intersection(new Rect2I(Vector2I.Zero, img.GetSize()));
            img.GetRegion(r).SavePng($"{_shotDir}/mo_{_moTag}_{_moShot++:000}.png");
        }

        void Begin(string tag, float seconds, int every)
        {
            _moTag = tag; _moUntil = t + seconds; _moEvery = every; _moNext = 0;
        }
    }
}

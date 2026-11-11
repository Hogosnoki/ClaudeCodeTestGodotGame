using System;
using Godot;

namespace DaggerCave;

/// <summary>
/// `--modelsheet=fx --sheetfx --shots=DIR`: the effects' contact sheet. Every kind of particle effect is set off once, side by
/// side on a grid (bursts, sparks, a pop, an explosion; dust, smoke, debris, rings; a flash, embers, a splash, a spray, a
/// healing swell and a shockwave), the effects' clock is stepped by hand at 1/60 s, and DIR/fx_0.png .. fx_3.png are saved at
/// four moments of their lives, so their shapes, spread and sizes can be judged however slowly the machine draws.
/// </summary>
public partial class ModelSheet
{
    private bool _fxSheet;
    private FxLayer _fxLayer;
    private int _fxShot, _fxHold;
    private float _fxClock;
    private static readonly float[] FxMoments = { 0.03f, 0.1f, 0.22f, 0.42f };

    private void SetupFx()
    {
        _fxLayer = new FxLayer { Visible = false };
        AddChild(_fxLayer);
        _fxLayer.SetProcess(false);
        G.Fx = _fxLayer;
        AddChild(new Fx3D());
        const float cell = 150f;
        Vector2 At(int cx, int cy) => new((cx - 2) * cell, (cy - 1) * cell);
        var fx = _fxLayer;
        var fire = new Color(1f, 0.6f, 0.2f);
        var blood = new Color(0.8f, 0.15f, 0.12f);
        var frost = new Color(0.6f, 0.85f, 1f);
        var green = new Color(0.4f, 0.95f, 0.5f);
        // row 0
        fx.Burst(At(0, 0), fire, 16, 170, 2.5f, 0.5f);
        fx.Spark(At(1, 0), Vector2.Right, true, new Color(1f, 0.9f, 0.6f));
        fx.Burst(At(1, 0), blood, 8, 140, 2.2f, 0.45f);
        for (int k = 0; k < 3; k++) fx.Spark(At(2, 0) + new Vector2((k - 1) * 34, 0), new Vector2(1, -0.3f * (k - 1)), false, frost);
        fx.Pop(At(3, 0), blood, 14f);
        fx.Explosion(At(4, 0), fire, 1f);
        // row 1
        fx.Dust(At(0, 1), 8, 1.4f);
        fx.Smoke(At(1, 1), 7, new Color(0.3f, 0.28f, 0.26f, 0.6f), 60f);
        fx.Debris(At(2, 1), new Color(0.5f, 0.45f, 0.4f), 12, 200f);
        fx.Ring(At(3, 1), 42f, new Color(frost, 0.9f), 0.45f);
        fx.Ring(At(4, 1), 42f, new Color(fire, 0.9f), 0.45f, FxLayer.RingStyle.Burst);
        // row 2
        fx.Flash(At(0, 2), 30f, new Color(1f, 0.95f, 0.8f), 0.3f);
        for (int k = 0; k < 14; k++) fx.Ember(At(1, 2) + new Vector2(G.Range(-24, 24), G.Range(-6, 20)), fire);
        fx.Splash(At(2, 2), 0.8f, new Color(0.45f, 0.7f, 0.95f, 0.85f));
        fx.Directional(At(3, 2), new Vector2(1, -0.4f).Normalized(), 0.5f, green, 12, 190f, 2f, 0.45f);
        fx.Ring(At(4, 2), 40f, new Color(green, 0.9f), 0.5f, FxLayer.RingStyle.Wave);
        fx.Shockwave(At(4, 2) + new Vector2(0, 30), 46f, new Color(1f, 1f, 1f, 0.8f), 0.45f);
        float tanV = MathF.Tan(Mathf.DegToRad(_cam.Fov * 0.5f));
        float halfH = 1.5f * cell / W3.Ppu + 3f;
        _cam.Position = new Vector3(0, 0, halfH / tanV);
        _cam.LookAt(Vector3.Zero, Vector3.Up);
    }

    private void FxFrame()
    {
        if (_fxShot >= FxMoments.Length) return;
        if (_fxClock < FxMoments[_fxShot])
        {
            // (the effects' own clock, stepped by hand)
            _fxLayer._Process(1.0 / 60.0);
            _fxClock += 1f / 60f;
            _fxHold = 0;
            return;
        }
        if (++_fxHold < 4) return;
        GetViewport().GetTexture().GetImage().SavePng($"{_dir}/fx_{_fxShot}.png");
        _fxShot++;
        if (_fxShot >= FxMoments.Length) { GD.Print($"[modelsheet] saved the effects sheet to {_dir}"); SafeQuit.Request(this); }
    }
}

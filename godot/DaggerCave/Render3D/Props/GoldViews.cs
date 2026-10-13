using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>A gold coin standing on its edge and turning, or a little heap of them (four to eight lying on one another), glinting.</summary>
public partial class CoinView : PropView
{
    private Node3D _coin;
    private MeshInstance3D _glow;
    private OmniLight3D _light;
    private bool _pile;

    protected override void Build()
    {
        var c = (CoinPickup)Owner2D;
        _pile = c.IsPile;
        _coin = new Node3D();
        AddChild(_coin);
        var mb = new MeshBuilder();
        // a coin: a short fat disc (its face along z, the way it faces you)
        void Disc(Vector3 at, float r, float rotZ, float tilt)
        {
            var path = new List<Vector3> { at + new Vector3(0, 0, -0.02f), at + new Vector3(0, 0, 0.02f) };
            if (tilt != 0f) path = path.Select(p => at + (p - at).Rotated(Vector3.Right, tilt).Rotated(Vector3.Forward, rotZ)).ToList();
            mb.Tube(path, new List<float> { r, r }, 14, Colors.White, true);
        }
        if (!_pile) Disc(new Vector3(0, 0.2f, 0), 0.2f, 0f, 0f);
        else
        {
            // a heap: the coins lie flat and tilted, one on another, wider at the foot
            var rng = new Random(c.Count * 31 + (int)(c.Position.X * 7));
            for (int k = 0; k < c.Count + 3; k++)
            {
                float layer = k < 4 ? 0 : k < 7 ? 1 : 2;
                float spread = 0.34f - layer * 0.1f;
                var at = new Vector3((float)(rng.NextDouble() - 0.5) * 2f * spread, 0.05f + layer * 0.06f + (float)rng.NextDouble() * 0.03f, (float)(rng.NextDouble() - 0.5) * spread);
                Disc(at, 0.17f, (float)(rng.NextDouble() - 0.5) * 1.2f, 1.2f + (float)(rng.NextDouble() - 0.5) * 0.8f);
            }
        }
        _coin.AddChild(PropViews.Mesh(mb, PropViews.Gold, false));
        _glow = PropViews.Sprite(new Color(1f, 0.82f, 0.35f), 0, _pile ? 1.1f : 0.8f, 0.9f);
        AddChild(_glow);
        _light = PropViews.Light(new Color(1f, 0.8f, 0.4f), _pile ? 1.4f : 0.9f, _pile ? 4f : 3f);
        AddChild(_light);
    }

    protected override void Sync(float dt)
    {
        var c = (CoinPickup)Owner2D;
        Follow(new Vector2(0, -1), 0.2f);
        if (!_pile) _coin.Rotation = new Vector3(0, c.T * 2.2f, 0);
        float tw = 0.55f + 0.25f * MathF.Sin(c.T * 4f + Position.X);
        PropViews.SetSprite(_glow, new Color(1f, 0.82f, 0.35f, tw * (_pile ? 0.9f : 0.6f)), 0, _pile ? 1.1f : 0.8f);
        _light.LightEnergy = (_pile ? 0.7f : 0.35f) * (0.8f + 0.2f * MathF.Sin(c.T * 4f));
    }
}

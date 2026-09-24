using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// Global access point for the running game. The game is small enough that a handful of
/// well-known singletons (the current cave, the player, the enemy list, audio, fx) is simpler
/// than threading references through every node.
/// </summary>
public static class G
{
    public const uint LayerTerrain = 1;
    public const uint LayerPlayer = 2;
    public const uint LayerEnemy = 4;

    public static Main Main;
    public static CaveData Cave;
    public static Player Player;
    public static Node2D World;
    public static FxLayer Fx;
    public static SoundBank Sfx;
    public static readonly List<Enemy> Enemies = new();

    public static Random Rng = new();
    public static int Depth = 1;

    public static float RandF() => (float)Rng.NextDouble();
    public static float Range(float a, float b) => a + (b - a) * (float)Rng.NextDouble();
    public static int RangeI(int a, int bInclusive) => Rng.Next(a, bInclusive + 1);
    public static bool Chance(float p) => Rng.NextDouble() < p;
    public static Vector2 RandDir() { float a = Range(0, Mathf.Tau); return new Vector2(Mathf.Cos(a), Mathf.Sin(a)); }
    public static T Pick<T>(IList<T> list) => list[Rng.Next(list.Count)];

    /// <summary>Enemy health multiplier for the current depth.</summary>
    public static float DepthHp => 1f + 0.35f * (Depth - 1);
    /// <summary>Enemy damage multiplier for the current depth.</summary>
    public static float DepthDmg => 1f + 0.2f * (Depth - 1);

    public static void Spawn(Node n) => World.AddChild(n);
}

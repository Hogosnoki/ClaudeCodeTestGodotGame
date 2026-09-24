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

    /// <summary>Seconds of play in this run (excluding pauses/menus); drives the difficulty curve.</summary>
    public static float RunTime;

    /// <summary>
    /// Continuous difficulty curve: every enemy stat doubles every 10 minutes of play
    /// (2^(t/600)), so a run keeps getting harder smoothly rather than in steps.
    /// </summary>
    public static float Threat => MathF.Pow(2f, RunTime / (Tune.Difficulty.DoublingMinutes * 60f));

    /// <summary>Enemy movement/attack tempo: the same curve, capped at 2x so fights stay readable.</summary>
    public static float Tempo => MathF.Min(Tune.Difficulty.TempoCap, Threat);

    /// <summary>Spawn intensity 0..3: how busy the cave is (grows over the first half hour).</summary>
    public static float Pace => MathF.Min(Tune.Difficulty.PaceMax, RunTime / (Tune.Difficulty.PaceMinutesPerStep * 60f));

    /// <summary>Enemy health multiplier.</summary>
    public static float DepthHp => Threat;
    /// <summary>Enemy damage multiplier.</summary>
    public static float DepthDmg => Threat;

    public static void Spawn(Node n) => World.AddChild(n);
}

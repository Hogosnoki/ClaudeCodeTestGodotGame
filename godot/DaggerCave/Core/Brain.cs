using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Godot;

namespace DaggerCave;

/// <summary>
/// A tiny actor-critic network shared by every creature of one type:
///   inputs -> 24 tanh -> 24 tanh -> [one logit per action | state value]
///
/// Learning (advantage actor-critic with TD targets):
///  * Policy: raise the log-probability of actions that turned out better than the critic
///    expected (advantage = target - V(s)), with an entropy bonus so it keeps exploring.
///  * Critic: regress V(s) toward target = reward + gamma^n * V(next state).
///  * Teacher: a cross-entropy pull toward what the old scripted AI would have done. It starts
///    strong and fades with experience, so a fresh brain behaves sensibly on day one and RL then
///    refines it. (Tune.Brains.TeacherStart = 0 disables it.)
/// Speed-ups: Adam optimiser, advantage normalisation, gradient-norm clipping, Huber value loss,
/// one shared brain per creature type, invalid actions masked out of the softmax.
/// </summary>
public sealed class Brain
{
    public const int Version = 1;
    public const int H1 = 24, H2 = 24;

    public string Name;
    public int In, Act;
    public long Experience;      // decisions made while learning
    public int Updates;          // optimiser steps taken
    public float AvgReward;      // moving average reward per decision
    public float AvgDealt;       // moving average player damage dealt per decision
    public float AvgTaken;       // moving average damage taken per decision
    public bool Dirty;

    // parameters: W1[H1,In] b1[H1] W2[H2,H1] b2[H2] W3[(Act+1),H2] b3[Act+1], flattened into one array
    private float[] _p, _m, _v, _g;
    private int _oW1, _oB1, _oW2, _oB2, _oW3, _oB3;
    private int _adamT;
    private readonly List<Transition> _buffer = new();
    private readonly Random _rng;

    public struct Transition
    {
        public float[] X;
        public bool[] Mask;
        public int Action, Teacher;
        public bool Forced;     // the teacher's action was executed (skip the policy-gradient term)
        public float Target;    // TD target for V(s)
    }

    public Brain(string name, int inputs, int actions, int seed = 0)
    {
        Name = name; In = inputs; Act = actions;
        _rng = new Random(seed == 0 ? name.GetHashCode() : seed);
        Layout();
        // Xavier-ish init; small last layer so the initial policy is near uniform
        Init(_oW1, H1 * In, MathF.Sqrt(1f / In));
        Init(_oW2, H2 * H1, MathF.Sqrt(1f / H1));
        Init(_oW3, (Act + 1) * H2, 0.1f * MathF.Sqrt(1f / H2));
    }

    private void Layout()
    {
        _oW1 = 0; _oB1 = _oW1 + H1 * In;
        _oW2 = _oB1 + H1; _oB2 = _oW2 + H2 * H1;
        _oW3 = _oB2 + H2; _oB3 = _oW3 + (Act + 1) * H2;
        int n = _oB3 + Act + 1;
        _p = new float[n]; _m = new float[n]; _v = new float[n]; _g = new float[n];
    }

    private void Init(int off, int count, float scale)
    {
        for (int i = 0; i < count; i++) _p[off + i] = (float)(_rng.NextDouble() * 2 - 1) * scale * 1.7f;
    }

    public int ParamCount => _p.Length;
    public float TeacherWeight => Tune.Brains.TeacherStart * MathF.Exp(-Experience / Math.Max(1f, Tune.Brains.TeacherHalfLife) * 0.6931f);

    // ------------------------------------------------------------------ forward

    /// <summary>Runs the net. out has Act logits followed by the value.</summary>
    public void Forward(float[] x, float[] h1, float[] h2, float[] outp)
    {
        for (int j = 0; j < H1; j++)
        {
            float s = _p[_oB1 + j];
            int row = _oW1 + j * In;
            for (int i = 0; i < In; i++) s += _p[row + i] * x[i];
            h1[j] = MathF.Tanh(s);
        }
        for (int j = 0; j < H2; j++)
        {
            float s = _p[_oB2 + j];
            int row = _oW2 + j * H1;
            for (int i = 0; i < H1; i++) s += _p[row + i] * h1[i];
            h2[j] = MathF.Tanh(s);
        }
        for (int k = 0; k <= Act; k++)
        {
            float s = _p[_oB3 + k];
            int row = _oW3 + k * H2;
            for (int i = 0; i < H2; i++) s += _p[row + i] * h2[i];
            outp[k] = s;
        }
    }

    public static void MaskedSoftmax(float[] logits, bool[] mask, int n, float[] probs, float temperature = 1f)
    {
        float mx = float.NegativeInfinity;
        for (int k = 0; k < n; k++) if (mask[k]) mx = Math.Max(mx, logits[k] / temperature);
        float sum = 0;
        for (int k = 0; k < n; k++)
        {
            probs[k] = mask[k] ? MathF.Exp(logits[k] / temperature - mx) : 0f;
            sum += probs[k];
        }
        if (sum <= 0) { for (int k = 0; k < n; k++) probs[k] = mask[k] ? 1 : 0; sum = 0; for (int k = 0; k < n; k++) sum += probs[k]; }
        for (int k = 0; k < n; k++) probs[k] /= sum;
    }

    /// <summary>Picks an action (sampled, or the most likely one) and returns the critic's value estimate.</summary>
    public int Choose(float[] x, bool[] mask, bool sample, out float value, float[] probs)
    {
        var h1 = new float[H1]; var h2 = new float[H2]; var o = new float[Act + 1];
        Forward(x, h1, h2, o);
        value = o[Act];
        MaskedSoftmax(o, mask, Act, probs, sample ? 1f : Tune.Brains.PlayTemperature);
        double r = _rng.NextDouble(), acc = 0;
        int last = -1;
        for (int k = 0; k < Act; k++)
        {
            if (!mask[k]) continue;
            last = k;
            acc += probs[k];
            if (r <= acc) return k;
        }
        return Math.Max(0, last);
    }

    public float Value(float[] x)
    {
        var h1 = new float[H1]; var h2 = new float[H2]; var o = new float[Act + 1];
        Forward(x, h1, h2, o);
        return o[Act];
    }

    // ------------------------------------------------------------------ learning

    public void Remember(Transition t)
    {
        _buffer.Add(t);
        if (_buffer.Count >= Tune.Brains.BatchSize) Train();
    }

    public void NoteStats(float reward, float dealt, float taken)
    {
        const float a = 0.01f;
        AvgReward += (reward - AvgReward) * a;
        AvgDealt += (dealt - AvgDealt) * a;
        AvgTaken += (taken - AvgTaken) * a;
    }

    /// <summary>One Adam step on the buffered transitions.</summary>
    public void Train()
    {
        int n = _buffer.Count;
        if (n == 0) return;
        Array.Clear(_g);
        var h1 = new float[H1]; var h2 = new float[H2]; var o = new float[Act + 1];
        var probs = new float[Act];
        var dOut = new float[Act + 1];
        var dH2 = new float[H2]; var dH1 = new float[H1];

        // advantages with the current critic, normalised over the batch
        var adv = new float[n];
        for (int s = 0; s < n; s++) { Forward(_buffer[s].X, h1, h2, o); adv[s] = _buffer[s].Target - o[Act]; }
        float mean = 0, var = 0;
        for (int s = 0; s < n; s++) mean += adv[s];
        mean /= n;
        for (int s = 0; s < n; s++) var += (adv[s] - mean) * (adv[s] - mean);
        float std = MathF.Sqrt(var / n) + 1e-4f;
        bool normalise = n >= 8;

        float beta = Tune.Brains.EntropyBonus, cv = Tune.Brains.ValueLossWeight, lam = TeacherWeight;
        for (int s = 0; s < n; s++)
        {
            var t = _buffer[s];
            Forward(t.X, h1, h2, o);
            MaskedSoftmax(o, t.Mask, Act, probs);
            float A = normalise ? (adv[s] - mean) / std : adv[s];
            float H = 0;
            for (int k = 0; k < Act; k++) if (probs[k] > 1e-8f) H -= probs[k] * MathF.Log(probs[k]);
            for (int k = 0; k < Act; k++)
            {
                if (!t.Mask[k]) { dOut[k] = 0; continue; }
                float d = 0;
                float onehotA = k == t.Action ? 1 : 0;
                if (!t.Forced) d += -A * (onehotA - probs[k]);                       // policy gradient
                if (lam > 0 && t.Teacher >= 0) d += lam * (probs[k] - (k == t.Teacher ? 1 : 0)); // imitation
                if (probs[k] > 1e-8f) d += beta * probs[k] * (MathF.Log(probs[k]) + H); // entropy bonus
                dOut[k] = d / n;
            }
            float err = o[Act] - t.Target;
            dOut[Act] = cv * 2f * Math.Clamp(err, -1f, 1f) / n; // Huber-style value gradient

            // backprop: output layer
            Array.Clear(dH2);
            for (int k = 0; k <= Act; k++)
            {
                float d = dOut[k];
                if (d == 0) continue;
                int row = _oW3 + k * H2;
                for (int i = 0; i < H2; i++) { _g[row + i] += d * h2[i]; dH2[i] += d * _p[row + i]; }
                _g[_oB3 + k] += d;
            }
            Array.Clear(dH1);
            for (int j = 0; j < H2; j++)
            {
                float d = dH2[j] * (1 - h2[j] * h2[j]);
                int row = _oW2 + j * H1;
                for (int i = 0; i < H1; i++) { _g[row + i] += d * h1[i]; dH1[i] += d * _p[row + i]; }
                _g[_oB2 + j] += d;
            }
            for (int j = 0; j < H1; j++)
            {
                float d = dH1[j] * (1 - h1[j] * h1[j]);
                int row = _oW1 + j * In;
                for (int i = 0; i < In; i++) _g[row + i] += d * t.X[i];
                _g[_oB1 + j] += d;
            }
        }
        _buffer.Clear();
        Step();
    }

    private void Step()
    {
        // global-norm gradient clipping
        double norm = 0;
        for (int i = 0; i < _g.Length; i++) norm += _g[i] * _g[i];
        norm = Math.Sqrt(norm);
        float clip = norm > Tune.Brains.GradClip ? (float)(Tune.Brains.GradClip / norm) : 1f;
        const float b1 = 0.9f, b2 = 0.999f, eps = 1e-6f;
        _adamT++;
        float lr = Tune.Brains.LearningRate;
        float c1 = 1 - MathF.Pow(b1, _adamT), c2 = 1 - MathF.Pow(b2, _adamT);
        for (int i = 0; i < _p.Length; i++)
        {
            float g = _g[i] * clip;
            _m[i] = b1 * _m[i] + (1 - b1) * g;
            _v[i] = b2 * _v[i] + (1 - b2) * g * g;
            _p[i] -= lr * (_m[i] / c1) / (MathF.Sqrt(_v[i] / c2) + eps);
        }
        Updates++;
        Dirty = true;
    }

    // ------------------------------------------------------------------ testing

    /// <summary>Loss for one transition (used by the gradient check).</summary>
    public float LossOf(Transition t, float advantage)
    {
        var h1 = new float[H1]; var h2 = new float[H2]; var o = new float[Act + 1]; var p = new float[Act];
        Forward(t.X, h1, h2, o);
        MaskedSoftmax(o, t.Mask, Act, p);
        float H = 0;
        for (int k = 0; k < Act; k++) if (p[k] > 1e-8f) H -= p[k] * MathF.Log(p[k]);
        float loss = 0;
        if (!t.Forced) loss += -advantage * MathF.Log(p[t.Action] + 1e-12f);
        if (TeacherWeight > 0 && t.Teacher >= 0) loss += -TeacherWeight * MathF.Log(p[t.Teacher] + 1e-12f);
        loss += -Tune.Brains.EntropyBonus * H;
        float err = o[Act] - t.Target;
        loss += Tune.Brains.ValueLossWeight * err * err;
        return loss;
    }

    /// <summary>Compares backprop to finite differences on random data; returns the worst relative error.</summary>
    public static float GradientCheck()
    {
        var b = new Brain("gradcheck", 7, 4, 42);
        var rng = new Random(3);
        var t = new Transition { X = new float[7], Mask = new[] { true, true, false, true }, Action = 1, Teacher = 3, Target = 0.3f };
        for (int i = 0; i < 7; i++) t.X[i] = (float)rng.NextDouble() * 2 - 1;
        // analytic: single sample, no normalisation, advantage = target - V
        float adv = t.Target - b.Value(t.X);
        b._buffer.Add(t);
        var pBefore = (float[])b._p.Clone();
        // run Train's gradient computation without stepping
        b.GradOnly();
        var g = (float[])b._g.Clone();
        float worst = 0;
        for (int trial = 0; trial < 200; trial++)
        {
            int i = rng.Next(b._p.Length);
            const float h = 5e-3f; // float32 loss: a larger step keeps rounding noise down
            float old = b._p[i];
            b._p[i] = old + h; float lp = b.LossOf(t, adv);
            b._p[i] = old - h; float lm = b.LossOf(t, adv);
            b._p[i] = old;
            float num = (lp - lm) / (2 * h);
            // the value term in Train uses a clamped (Huber) gradient; the check keeps |err|<1
            float rel = Math.Abs(num - g[i]) / Math.Max(1e-2f, Math.Abs(num) + Math.Abs(g[i]));
            worst = Math.Max(worst, rel);
        }
        Array.Copy(pBefore, b._p, pBefore.Length);
        return worst;
    }

    private void GradOnly()
    {
        // same as Train() but keeps the parameters: compute gradients then undo nothing
        var saved = (float[])_p.Clone();
        var m = (float[])_m.Clone(); var v = (float[])_v.Clone(); int t = _adamT; int u = Updates;
        Train();
        Array.Copy(saved, _p, saved.Length); Array.Copy(m, _m, m.Length); Array.Copy(v, _v, v.Length); _adamT = t; Updates = u;
    }

    // ------------------------------------------------------------------ persistence

    private sealed class Saved
    {
        public int version { get; set; }
        public string name { get; set; }
        public int inputs { get; set; }
        public int actions { get; set; }
        public int h1 { get; set; }
        public int h2 { get; set; }
        public long experience { get; set; }
        public int updates { get; set; }
        public float avgReward { get; set; }
        public float avgDealt { get; set; }
        public float avgTaken { get; set; }
        public float[] parameters { get; set; }
    }

    public string ToJson() => JsonSerializer.Serialize(new Saved
    {
        version = Version, name = Name, inputs = In, actions = Act, h1 = H1, h2 = H2,
        experience = Experience, updates = Updates, avgReward = AvgReward, avgDealt = AvgDealt, avgTaken = AvgTaken, parameters = _p,
    });

    /// <summary>Returns null if the file is missing or doesn't match this network shape.</summary>
    public static Brain FromJson(string json, string name, int inputs, int actions)
    {
        try
        {
            var s = JsonSerializer.Deserialize<Saved>(json);
            if (s == null || s.version != Version || s.inputs != inputs || s.actions != actions || s.h1 != H1 || s.h2 != H2) return null;
            var b = new Brain(name, inputs, actions);
            if (s.parameters.Length != b._p.Length) return null;
            Array.Copy(s.parameters, b._p, b._p.Length);
            b.Experience = s.experience; b.Updates = s.updates;
            b.AvgReward = s.avgReward; b.AvgDealt = s.avgDealt; b.AvgTaken = s.avgTaken;
            return b;
        }
        catch { return null; }
    }
}

/// <summary>
/// All creature brains: loading, saving, the training switch, per-type locks, and a per-frame
/// budget so many enemies thinking at once can't stall a frame.
/// </summary>
public static class Brains
{
    private static readonly Dictionary<string, Brain> All = new();
    private static ulong _frame;
    private static int _usedThisFrame;

    /// <summary>Debug training mode (F9 in game, or launch with --train).</summary>
    public static bool Training;
    /// <summary>Show each creature's current move above it while training.</summary>
    public static bool ShowLabels = true;

    public static IEnumerable<Brain> Loaded => All.Values;

    /// <summary>Brains are saved into the project (so they can be committed) when running from the
    /// editor, and into the user data folder in an exported build.</summary>
    public static string SaveDir => DirOverride ?? (OS.HasFeature("editor")
        ? ProjectSettings.GlobalizePath("res://DaggerCave/Brains")
        : ProjectSettings.GlobalizePath("user://brains"));

    /// <summary>--braindir=PATH (tests): load from and save to this folder only.</summary>
    public static string DirOverride;
    public static string LastSaveText = "";
    private static float _autosaveT;

    /// <summary>Call every frame: autosaves while training.</summary>
    public static void Tick(float dt)
    {
        if (!Training) return;
        _autosaveT += dt;
        if (_autosaveT >= Tune.Brains.AutosaveSeconds) SaveAll();
    }

    public static Brain Get(string name, int inputs, int actions)
    {
        if (All.TryGetValue(name, out var b) && b.In == inputs && b.Act == actions) return b;
        b = TryLoad(Path.Combine(SaveDir, name + ".json"), name, inputs, actions);
        if (DirOverride == null)
            b ??= TryLoad(ProjectSettings.GlobalizePath($"res://DaggerCave/Brains/{name}.json"), name, inputs, actions)
                ?? LoadPacked(name, inputs, actions);
        b ??= new Brain(name, inputs, actions);
        All[name] = b;
        return b;
    }

    private static Brain TryLoad(string path, string name, int inputs, int actions)
    {
        try { return File.Exists(path) ? Brain.FromJson(File.ReadAllText(path), name, inputs, actions) : null; }
        catch { return null; }
    }

    private static Brain LoadPacked(string name, int inputs, int actions)
    {
        // exported builds: brains shipped inside the .pck
        string p = $"res://DaggerCave/Brains/{name}.json";
        return Godot.FileAccess.FileExists(p) ? Brain.FromJson(Godot.FileAccess.GetFileAsString(p), name, inputs, actions) : null;
    }

    public static void SaveAll(bool force = false)
    {
        _autosaveT = 0;
        int n = 0;
        try
        {
            Directory.CreateDirectory(SaveDir);
            foreach (var b in All.Values)
            {
                if (!b.Dirty && !force) continue;
                // write then rename, so a crash mid-save can't corrupt a trained brain
                string path = Path.Combine(SaveDir, b.Name + ".json"), tmp = path + ".tmp";
                File.WriteAllText(tmp, b.ToJson());
                File.Move(tmp, path, true);
                b.Dirty = false;
                n++;
            }
            if (n > 0) GD.Print($"[brains] saved {n} to {SaveDir}");
            LastSaveText = $"saved {Time.GetTimeStringFromSystem()}";
        }
        catch (Exception e) { GD.PrintErr($"[brains] save failed: {e.Message}"); LastSaveText = "SAVE FAILED"; }
    }

    public static bool IsLocked(string name) => name switch
    {
        "bat" => BrainLocks.Bat,
        "frog" => BrainLocks.Frog,
        "goblin" => BrainLocks.Goblin,
        "slinger" => BrainLocks.Slinger,
        "spider" => BrainLocks.Spider,
        "magma" => BrainLocks.Magma,
        "golem" => BrainLocks.Golem,
        "fish" => BrainLocks.Fish,
        "urchin" => BrainLocks.Urchin,
        "eel" => BrainLocks.Eel,
        "boss" => BrainLocks.Boss,
        "rat" => BrainLocks.Rat,
        "bear" => BrainLocks.Bear,
        "scorpion" => BrainLocks.Scorpion,
        "hornet" => BrainLocks.Hornet,
        "skeleton" => BrainLocks.Skeleton,
        "sporeling" => BrainLocks.Sporeling,
        "wraith" => BrainLocks.Wraith,
        "shardling" => BrainLocks.Shardling,
        "dragon" => BrainLocks.Dragon,
        _ => false,
    };

    /// <summary>Whether a creature type is driven by its brain (vs. the original scripted AI).</summary>
    public static bool Drives(Brain b) => Tune.Brains.Enabled && (Training || b.Experience >= Tune.Brains.MinExperienceToPlay);

    public static bool Learns(Brain b) => Training && !IsLocked(b.Name);

    /// <summary>Staggering: at most N decisions per physics frame across all enemies.</summary>
    public static bool TakeDecisionSlot()
    {
        ulong f = Engine.GetPhysicsFrames();
        if (f != _frame) { _frame = f; _usedThisFrame = 0; }
        if (_usedThisFrame >= Tune.Brains.MaxDecisionsPerFrame) return false;
        _usedThisFrame++;
        return true;
    }
}

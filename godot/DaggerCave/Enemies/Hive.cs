using System;
using System.Collections.Generic;
using Godot;

namespace DaggerCave;

/// <summary>
/// The Nest's hornet hive: a tough paper nest hung from the ceiling of a room. While a hero is near it keeps <see cref="Tune.Hive.Keep"/> hornets about it:
/// it sends out a new one about <see cref="Tune.Hive.Respawn"/> s after one is lost. It strikes at nothing itself; destroying it leaves a chest of upgrades
/// (see Main.PlaceHive), and the hornets already out stay (they just aren't replaced).
/// </summary>
public partial class Hive : Enemy
{
    public override string HitSound => "hit_stone";
    private readonly List<Hornet> _brood = new();
    private readonly List<float> _pending = new();
    private bool _primed;

    protected override bool UsesGravity => false;

    /// <summary>Hornets of this hive in the air now.</summary>
    public int BroodAlive { get { Prune(); return _brood.Count; } }
    /// <summary>The hornets of this hive in the air (for the tests).</summary>
    public IReadOnlyList<Hornet> BroodList => _brood;
    /// <summary>Hornets on their way out (counting down to be sent).</summary>
    public int BroodPending => _pending.Count;
    /// <summary>Hornets this hive has sent out so far (for the tests).</summary>
    public int Released { get; private set; }

    public Hive() { MaxHp = Tune.Hive.Hp; BodyRadius = 20; ContactDamage = 0; XpValue = Tune.Hive.Xp; KnockResist = 1f; }

    protected override void Setup()
    {
        DisplayName = "Hornet Hive";
        UseSprite("hive");
        Anim.AllowTurns = false;
        MotionMode = MotionModeEnum.Floating;
        ManualMove = true;
    }

    private void Prune() => _brood.RemoveAll(h => h == null || !IsInstanceValid(h) || h.Dead);

    protected override void Think(float dt)
    {
        Velocity = Vector2.Zero;
        if (!Awake) return;
        // (the hornets of an asleep hive are as they were; nothing is sent out for nobody)
        if (!_primed) { _primed = true; for (int k = 0; k < Tune.Hive.Keep; k++) Release(); }
        Prune();
        // each hornet lost starts its own count of about four seconds
        int missing = Tune.Hive.Keep - _brood.Count - _pending.Count;
        for (int k = 0; k < missing; k++) _pending.Add(Tune.Hive.Respawn + G.Range(-Tune.Hive.Jitter, Tune.Hive.Jitter));
        for (int k = _pending.Count - 1; k >= 0; k--)
        {
            _pending[k] -= dt;
            if (_pending[k] > 0f) continue;
            _pending.RemoveAt(k);
            Release();
        }
    }

    /// <summary>A hornet crawls out of the mouth.</summary>
    private void Release()
    {
        var h = new Hornet { Brood = true, Position = GlobalPosition + new Vector2(G.Range(-5f, 5f), HitRadius * 0.75f) };
        h.Wake();
        h.Velocity = new Vector2(G.Range(-60f, 60f), 50f);
        G.World.AddChild(h);
        _brood.Add(h);
        Released++;
        G.Sfx.Play("buzz", GlobalPosition, -8, 0.1f, 1.1f);
        G.Fx.Burst(GlobalPosition + new Vector2(0, HitRadius * 0.8f), new Color(0.75f, 0.65f, 0.4f, 0.8f), 6, 60, 1.2f, 0.4f);
    }

    protected override void Animate() => Anim.Loop("idle", 1f);
    protected override Color BloodColor => new(0.8f, 0.68f, 0.42f);

    public override void _Draw() => DrawHealthBar();
}

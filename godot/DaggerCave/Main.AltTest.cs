using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    /// <summary>--alttest (with --hero=...): the herotest's stage, checking the hero's alterations instead.</summary>
    private bool _altTest;
    private int _altSwings, _altCharged;
    private float _altMark;

    /// <summary>Takes a card as if picked (an alteration swapped for another first drops the old one).</summary>
    private static void Take(Player p, string id, string drop = null)
    {
        if (drop != null)
        {
            p.Stats.Stacks.Remove(drop);
            switch (drop)
            {
                case "hex_burst": p.Stats.BlightBurst = false; break;
                case "throw_ricochet": p.Stats.Ricochet = false; break;
            }
        }
        Upgrades.Apply(Upgrades.Get(id), p.Stats, p);
    }

    private Enemy AltDummy(Player p, Enemy e, float dx, float dy = -6)
    {
        e.Position = p.GlobalPosition + new Vector2(_dir * dx, dy);
        e.SetMeta("test", true);
        _world.AddChild(e);
        return e;
    }

    private void AltStep(int s, Player p)
    {
        switch (p.Stats.Hero)
        {
            case HeroKind.Warden: AltWarden(s, p); break;
            case HeroKind.Vitalist: AltVitalist(s, p); break;
            case HeroKind.Elementalist: AltElementalist(s, p); break;
            case HeroKind.Rogue: AltRogue(s, p); break;
            default: AltSwordsman(s, p); break;
        }
    }

    // ---------------------------------------------------------------- Swordsman

    private void AltSwordsman(int s, Player p)
    {
        // Relentless Charge: every strike of the next combo carries the charge (at 80%)
        if (s > 12 && s < 40 && p.AttacksStarted > _altSwings)
        {
            _altSwings = p.AttacksStarted;
            if (p.SwingCharged) _altCharged++;
        }
        switch (s)
        {
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                Take(p, "charge_combo");
                _probeEnemy = AltDummy(p, new Golem(), 34);
                _probeEnemy.MaxHp = _probeEnemy.Hp = 5000;
                _probeEnemy.Freeze(8f, hold: true);
                break;
            case 10:
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 11:
                _altSwings = p.AttacksStarted;
                _altCharged = 0;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            case 40:
                _heroInput = default;
                Check($"Relentless Charge: the whole combo is charged ({_altCharged} charged strikes, want {p.Stats.ComboResets + 1})", _altCharged >= p.Stats.ComboResets + 1);
                break;
            case 56:
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 57:
                Check($"and the next combo isn't (swinging {p.IsSwinging}, charged {p.SwingCharged})", p.IsSwinging && !p.SwingCharged);
                _heroInput = default;
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- Swift Heave: with a charge waiting, the heave is instant and works in the air
            case 62:
                Take(p, "heave_swift");
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 66:
                _heroInput = new PlayerInput { Jump = true, JumpHeld = true };
                break;
            case 69:
                _heroInput = new PlayerInput { Ability2 = true, JumpHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            case 70:
                Check($"Swift Heave: a heave in the air, at once (airborne {!p.IsOnFloor()}, swift {p.SwiftHeaving})", !p.IsOnFloor() && p.SwiftHeaving);
                Check($"it spends the charge on speed, not force (charged {p.Charged}, swing charged {p.SwingCharged})", p.Charged == 0 && !p.SwingCharged);
                _heroInput = default;
                break;
            case 80:
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 81:
                Check($"without a charge it's the usual rooted heave (heaving {p.Heaving}, swift {p.SwiftHeaving})", p.Heaving && !p.SwiftHeaving);
                _heroInput = default;
                break;

            // ---- Counter Roll: a melee blow met mid-roll is stopped and answered
            case 100:
            {
                Take(p, "dodge_counter");
                p.ResetAbilityCooldowns();
                _probeEnemy = AltDummy(p, new Goblin(), 26, -4);
                _probeEnemy.Freeze(8f, hold: true);
                _heroInput = new PlayerInput { Dodge = true, Move = new Vector2(_dir, 0) };
                break;
            }
            case 101:
            {
                _heroInput = default;
                _hpMark = p.Hp;
                _altMark = _probeEnemy.Hp;
                int before = p.Counters;
                bool dodging = p.IsDodging;
                float took = p.Hurt(8, _probeEnemy.GlobalPosition, 230, _probeEnemy);
                Check($"Counter Roll: a club met mid-roll does nothing (rolling {dodging}, took {took:0.0}, hp {_hpMark:0} -> {p.Hp:0})", dodging && took == 0 && p.Hp == _hpMark);
                Check($"the roll ends in a counter swing (counters {before} -> {p.Counters}, rolling {p.IsDodging}, swinging {p.IsSwinging})", p.Counters == before + 1 && !p.IsDodging && p.IsSwinging);
                break;
            }
            case 106:
                Check($"and the counter lands (goblin hp {_altMark:0} -> {(IsInstanceValid(_probeEnemy) ? _probeEnemy.Hp : 0):0})", !IsInstanceValid(_probeEnemy) || _probeEnemy.Hp < _altMark);
                break;
            case 112:
            {
                // a shot from afar isn't a melee blow: no counter
                var shooter = AltDummy(p, new Goblin { Slinger = true }, 200, -4);
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true, Move = new Vector2(-_dir, 0) };
                _probe2 = shooter;
                break;
            }
            case 113:
            {
                _heroInput = default;
                int before = p.Counters;
                p.Hurt(4, _probe2.GlobalPosition, 230, _probe2);
                Check($"a blow from 200 px away isn't countered (counters {before} -> {p.Counters})", p.Counters == before);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                Finish();
                break;
            }
        }
    }

    // ---------------------------------------------------------------- Warden

    private void AltWarden(int s, Player p)
    {
        switch (s)
        {
            // ---- Unyielding Shield: stops 70% of each blow and never weakens or breaks
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                Take(p, "shield_unyielding");
                _heroInput = new PlayerInput { GuardHeld = true, GuardAim = new Vector2(_dir, 0) };
                break;
            case 10:
                _hpMark = p.Hp; _shieldMark = p.ShieldHp;
                _probe = Shoot(new Vector2(_dir * 120, -4));
                break;
            case 18:
            {
                float through = 6f * (1f - p.Stats.UnyieldingShare) * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult;
                Check($"Unyielding Shield: stops {p.Stats.UnyieldingShare:P0} of a shot (hp {_hpMark:0.00} -> {p.Hp:0.00}, want -{through:0.00})", Math.Abs(_hpMark - p.Hp - through) < 0.1f);
                Check($"and doesn't weaken (shield {_shieldMark:0.0} -> {p.ShieldHp:0.0})", p.ShieldHp >= _shieldMark - 0.01f);
                break;
            }
            case 20: case 22: case 24: case 26: case 28: case 30: case 32: case 34:
                p.Hurt(30, p.GlobalPosition + new Vector2(_dir * 30, -4));
                break;
            case 36:
                Check($"eight heavy blows later it still stands (broken {p.ShieldBroken}, shield {p.ShieldHp:0}, raised {p.ShieldRaised})", !p.ShieldBroken && p.ShieldRaised);
                Take(p, "unyielding_more");
                _hpMark = p.Hp;
                p.Hurt(10, p.GlobalPosition + new Vector2(_dir * 30, -4));
                break;
            case 42:
            {
                float through = 10f * (1f - Tune.Warden.UnyieldingShare - 0.05f) * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult;
                Check($"Braced: it stops 5% more (hp {_hpMark:0.00} -> {p.Hp:0.00}, want -{through:0.00})", Math.Abs(_hpMark - p.Hp - through) < 0.1f);
                _heroInput = default;
                break;
            }

            // ---- Guardian's Charge: alone, the barrier wraps you
            case 50:
                Take(p, "dash_guardian");
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 51:
                _heroInput = default;
                Check($"Guardian's Charge: alone, you're wrapped in a barrier of {Tune.Warden.BarrierAmount:0} (barrier {p.BarrierHp:0}, given {p.GuardedBy})", Math.Abs(p.BarrierHp - Tune.Warden.BarrierAmount) < 0.01f && p.GuardedBy == 1);
                break;
            case 56:
                _hpMark = p.Hp;
                p.Hurt(8, p.GlobalPosition + new Vector2(-_dir * 30, -4));
                float soaked = 8 * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult;
                Check($"it soaks a blow whole (hp {_hpMark:0} -> {p.Hp:0}, barrier {p.BarrierHp:0.0}, want {Tune.Warden.BarrierAmount - soaked:0.0})", p.Hp == _hpMark && Math.Abs(p.BarrierHp - (Tune.Warden.BarrierAmount - soaked)) < 0.05f);
                break;
            case 58:
            {
                _hpMark = p.Hp;
                float left = p.BarrierHp;
                p.Hurt(20, p.GlobalPosition + new Vector2(-_dir * 30, -4));
                float through = (20 * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult) - left;
                Check($"and what's left of a bigger one (hp {_hpMark:0.0} -> {p.Hp:0.0}, want -{through:0.0}; barrier {p.BarrierHp:0})", Math.Abs(_hpMark - p.Hp - through) < 0.2f && p.BarrierHp == 0);
                break;
            }
            case 62:
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 63: _heroInput = default; break;
            case 62 + 23:
                Check($"the barrier is gone after {Tune.Warden.BarrierSeconds:0} s (barrier {p.BarrierHp:0})", p.BarrierHp == 0);
                break;

            // ---- Deflecting Bash: no stun, but the shots in front go back where they came from
            case 90:
                Take(p, "bash_deflect");
                p.ResetAbilityCooldowns();
                // (not frozen: a frozen creature's stun would never wear off)
                _probeEnemy = AltDummy(p, new Goblin(), 24, -4);
                _probe = Shoot(new Vector2(_dir * 110, -8));
                break;
            case 92:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 93: _heroInput = default; break;
            case 97:
                Check($"Deflecting Bash: the shot goes back (sent back {p.Deflected}, reflected {IsInstanceValid(_probe) && _probe.Reflected})", p.Deflected >= 1 && (!IsInstanceValid(_probe) || _probe.Reflected));
                Check($"and the goblin it hit isn't stunned (hp {_hpMark:0} -> {_probeEnemy.Hp:0}, reeling {_probeEnemy.Reeling})", _probeEnemy.Hp < _hpMark && !_probeEnemy.Reeling);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                Finish();
                break;
        }
    }

    // ---------------------------------------------------------------- Vitalist

    private void AltVitalist(int s, Player p)
    {
        switch (s)
        {
            // ---- Blight Burst: the hex strikes as it spreads
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                Take(p, "hex_burst");
                _probeEnemy = AltDummy(p, new Golem(), 30);
                _probeEnemy.Freeze(8f, hold: true);
                break;
            case 8:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 9:
            {
                _heroInput = default;
                float dealt = _hpMark - _probeEnemy.Hp, want = Tune.Vitalist.BlightDamage * p.Stats.DamageMult;
                Check($"Blight Burst: the hex deals {want:0} as it spreads (hp {_hpMark:0} -> {_probeEnemy.Hp:0})", dealt >= want - 0.5f && _probeEnemy.Hexed);
                break;
            }

            // ---- Endless Hex: no cooldown, 10 vital force a cast
            case 12:
                Take(p, "hex_endless", drop: "hex_burst");
                p.ResetAbilityCooldowns();
                p.SetVitalForce(25);
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 13: _heroInput = default; break;
            case 18: _heroInput = new PlayerInput { Dodge = true }; break;
            case 19:
                _heroInput = default;
                Check($"Endless Hex: two hexes half a second apart, 10 vital force each (25 -> {p.VitalForce:0})", Math.Abs(p.VitalForce - 5) < 0.01f);
                break;
            case 24: _heroInput = new PlayerInput { Dodge = true }; break;
            case 25:
                _heroInput = default;
                Check($"a third, without the vital force, is refused ({p.VitalForce:0} left)", Math.Abs(p.VitalForce - 5) < 0.01f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- Slow Mending: half now, half over 6 s
            case 30:
                Take(p, "heal_slow");
                p.ResetAbilityCooldowns();
                p.Hp = 100;
                p.SetVitalForce(40);
                _hpMark = p.Hp;
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 31:
            {
                _heroInput = default;
                float half = Tune.Vitalist.HealAmount * p.Stats.HealMult * 0.5f;
                Check($"Slow Mending: half the heal at once (hp {_hpMark:0.0} -> {p.Hp:0.0}, want +{half:0.0}), mending {p.Mended}", p.Hp - _hpMark > half - 0.5f && p.Hp - _hpMark < half + 1.5f && p.Mended);
                break;
            }
            case 31 + 62:
            {
                float all = Tune.Vitalist.HealAmount * p.Stats.HealMult;
                Check($"and the rest over {Tune.Vitalist.MendSeconds:0} s (hp {_hpMark:0.0} -> {p.Hp:0.0}, want +{all:0.0}), still mending {p.Mended}", Math.Abs(p.Hp - _hpMark - all) < 0.6f && !p.Mended);
                break;
            }
            case 95:
                Take(p, "heal_warding");
                p.ResetAbilityCooldowns();
                p.Hp = 100;
                p.SetVitalForce(40);
                _heroInput = new PlayerInput { Ability = true };
                break;
            case 96:
            {
                _heroInput = default;
                _hpMark = p.Hp;
                p.Hurt(20, p.GlobalPosition + new Vector2(_dir * 30, -4));
                float want = 20 * (1f - p.Stats.DamageReduction) * p.Stats.DamageTakenMult * (1f - Tune.Vitalist.WardingShare);
                // (the mending ticks in the same frame: a hair of healing)
                Check($"Warding Mending: 20% less damage while mending (hp {_hpMark:0.0} -> {p.Hp:0.0}, want -{want:0.0})", Math.Abs(_hpMark - p.Hp - want) < 0.5f);
                break;
            }

            // ---- Lifebloom: alone, the rupture blooms on you
            case 160:
                Take(p, "rupture_bloom");
                p.ResetAbilityCooldowns();
                p.Hp = 100;
                p.SetVitalForce(40);
                _hpMark = p.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 161: _heroInput = default; break;
            case 161 + 8:
            {
                float want = Tune.Vitalist.BloomHeal * p.Stats.HealMult;
                Check($"Lifebloom: alone, it blooms on you and heals {want:0} (hp {_hpMark:0} -> {p.Hp:0}, blooms {p.Blooms})", p.Blooms == 1 && p.Hp - _hpMark > want - 0.5f && p.Hp - _hpMark < want + 1.5f);
                break;
            }
            case 175:
                Take(p, "bloom_pool");
                p.ResetAbilityCooldowns();
                p.Hp = 100;
                p.SetVitalForce(40);
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 176: _heroInput = default; break;
            case 176 + 8:
                Check($"Healing Pool: the bloom leaves a pool ({_world.GetChildren().OfType<HealingPool>().Count()})", _world.GetChildren().OfType<HealingPool>().Any());
                _hpMark = p.Hp;
                break;
            case 176 + 28:
            {
                float want = Tune.Vitalist.PoolRate * p.Stats.HealMult * 2f;
                Check($"that heals you {Tune.Vitalist.PoolRate:0} a second as you stand in it (hp {_hpMark:0.0} -> {p.Hp:0.0} in 2 s, want +{want:0.0})", Math.Abs(p.Hp - _hpMark - want) < 1f);
                break;
            }
            case 176 + 60:
                Check($"and fades after {Tune.Vitalist.PoolSeconds:0} s ({_world.GetChildren().OfType<HealingPool>().Count()} left)", !_world.GetChildren().OfType<HealingPool>().Any());
                Finish();
                break;
        }
    }

    // ---------------------------------------------------------------- Elementalist

    private void AltElementalist(int s, Player p)
    {
        switch (s)
        {
            // ---- Frostbolt: quicker, weaker bolts of frost that chill, and now and then freeze
            case 5:
                p.Stats.MaxHp = 500; p.Hp = 500;
                Take(p, "bolt_frost");
                p.SetAlimus(p.Stats.AlimusMax);
                p.Stats.FreezeBonus = -1f;
                _probeEnemy = AltDummy(p, new Golem(), 90);
                _probeEnemy.MaxHp = _probeEnemy.Hp = 3000;
                _probeEnemy.Freeze(60f, hold: true);
                break;
            case 8:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 9: _heroInput = default; break;
            case 14:
            {
                float dealt = _hpMark - _probeEnemy.Hp, want = Tune.Elementalist.FrostDamage * p.Stats.DamageMult;
                Check($"Frostbolt: a bolt of frost strikes for {want:0} ({dealt:0.0}) and chills (chilled {_probeEnemy.Chilled}, burning {_probeEnemy.Ignited})",
                    Math.Abs(dealt - want) < 0.5f && _probeEnemy.Chilled && !_probeEnemy.Ignited);
                _altSwings = p.BoltsCast;
                _heroInput = new PlayerInput { AttackHeld = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 26:
                Check($"holding the attack throws what the staff holds, then one bolt a second ({p.BoltsCast - _altSwings} bolts in 1.2 s, {p.BoltCharges} of {p.BoltChargesMax} left)", p.BoltsCast - _altSwings >= 2 && p.BoltsCast - _altSwings <= 4 && p.BoltChargesMax == Tune.Elementalist.FrostCharges);
                _heroInput = default;
                // (now every bolt freezes)
                p.Stats.FreezeBonus = 1f;
                _heroInput = new PlayerInput { Attack = true, Aim = new Vector2(_dir, 0) };
                break;
            case 27: _heroInput = default; break;
            case 32:
                Check($"and it can freeze a regular creature solid (frozen {_probeEnemy.FrozenSolid})", _probeEnemy.FrozenSolid);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                break;

            // ---- the updraft is narrow and aimed: held to aim (at any angle), raised where the button is let go
            case 40:
                p.SetAlimus(p.Stats.AlimusMax);
                _elUpdraft = p.LastUpdraft;
                // (first aimed up and to the right, the button held)
                _heroInput = new PlayerInput { Dodge = true, GuardHeld = true, Aim = new Vector2(1, -1).Normalized(), AimGiven = true };
                break;
            case 42:
                Check($"the button held only aims: no column yet ({p.AimingDraft}, a new one {p.LastUpdraft != _elUpdraft}, alimus {p.Alimus:0})", p.AimingDraft && p.LastUpdraft == _elUpdraft && p.Alimus > p.Stats.AlimusMax - 0.5f);
                // (the aim swings round to level, right: the final angle is what counts)
                _heroInput = new PlayerInput { GuardHeld = true, Aim = new Vector2(1, 0), AimGiven = true };
                break;
            case 44:
                _heroInput = default;
                break;
            case 46:
            {
                var u = p.LastUpdraft;
                Check($"let go, the column rises at the final angle ({Mathf.RadToDeg(u?.Angle ?? 0):0} degrees, aimed level: 90)", u != null && u != _elUpdraft && Math.Abs((u.Angle) - Mathf.Pi / 2f) < 0.05f);
                Check($"it is narrow ({u.Width:0} px) and long-lived ({u.TotalLife:0} s) by default", u != null && Math.Abs(u.Width - Tune.Elementalist.UpdraftWidth) < 0.1f && Math.Abs(u.TotalLife - Tune.Elementalist.UpdraftSeconds) < 0.1f);
                Check($"it cost the alimus ({p.Alimus:0} left of {p.Stats.AlimusMax:0})", Math.Abs(p.Alimus - (p.Stats.AlimusMax - Tune.Elementalist.UpdraftCost)) < 0.6f);
                if (IsInstanceValid(u)) u.QueueFree();
                // (and straight down: a downdraft)
                p.SetAlimus(p.Stats.AlimusMax);
                _elUpdraft = p.LastUpdraft;
                p.TestResetUpdraft();
                _heroInput = new PlayerInput { Dodge = true, GuardHeld = true, Aim = new Vector2(0, 1), AimGiven = true };
                break;
            }
            case 48: _heroInput = default; break;
            case 50:
            {
                var u = p.LastUpdraft;
                Check($"aimed straight down it points down ({Mathf.RadToDeg(u?.Angle ?? 0):0} degrees, 180)", u != null && u != _elUpdraft && Math.Abs(Math.Abs(u.Angle) - Mathf.Pi) < 0.05f);
                if (u != null && IsInstanceValid(u)) u.QueueFree();
                break;
            }

            // ---- Firestorm: a blizzard of fire, harder, that sets creatures alight
            case 60:
                Take(p, "blizzard_fire");
                p.SetAlimus(p.Stats.AlimusMax);
                p.ResetAbilityCooldowns();
                // (every strike catches)
                p.Stats.IgniteChance = 2f;
                _probeEnemy = AltDummy(p, new Golem(), 90);
                _probeEnemy.MaxHp = _probeEnemy.Hp = 3000;
                _probeEnemy.Freeze(60f, hold: true);
                break;
            case 62:
                p.Stats.BlizzardSecondsMult = 0.5f; // (the test waits out half a storm)
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 63: _heroInput = default; break;
            case 66:
                Check($"Firestorm: a storm of fire (fire {p.LastBlizzard?.Fire}), and what it strikes is set alight (burning {_probeEnemy.Ignited})", p.LastBlizzard != null && p.LastBlizzard.Fire && _probeEnemy.Ignited);
                break;
            case 97:
            {
                // (the storm's strikes, and the fire burning since the first of them)
                float dealt = _hpMark - _probeEnemy.Hp, strikes = Tune.Elementalist.BlizzardTicks / 2 * Tune.Elementalist.FirestormDamage * p.Stats.DamageMult;
                Check($"it strikes {Tune.Elementalist.BlizzardTicks / 2} times for {Tune.Elementalist.FirestormDamage:0} ({strikes:0}), and the fire burns on top ({dealt:0.0} in all)", dealt > strikes + 6f && dealt < strikes + 16f);
                break;
            }

            // ---- Snap: burning creatures burst, and frozen ones, each its own way
            case 100:
            {
                p.SetAlimus(p.Stats.AlimusMax);
                var gob = AltDummy(p, new Goblin(), 90 + 170, -4);
                gob.FreezeSolid(60f);
                _probe2 = gob;
                _probeEnemy.Ignite(4f, 10f);
                _hpMark = _probeEnemy.Hp;
                _altMark = gob.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            }
            case 101: _heroInput = default; break;
            case 105:
            {
                float dealt = _hpMark - _probeEnemy.Hp, splash = _altMark - (IsInstanceValid(_probe2) ? _probe2.Hp : 0);
                float want = Tune.Elementalist.CinderDamage * p.Stats.DamageMult, wantSplash = Tune.Elementalist.CinderSplash * p.Stats.DamageMult;
                // (the golem's fire burns a little in the moment before it bursts)
                Check($"Snap: the burning golem bursts for {want:0} ({dealt:0.0}), its fire spent (burning {_probeEnemy.Ignited})", dealt > want - 0.5f && dealt < want + 1.5f && !_probeEnemy.Ignited);
                Check($"and the frozen goblin shatters for {Tune.Elementalist.SnapDamage * p.Stats.DamageMult:0} ({splash:0.0}), both in one snap", Math.Abs(splash - Tune.Elementalist.SnapDamage * p.Stats.DamageMult) < 1.5f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(_probe2)) _probe2.QueueFree();
                Finish();
                break;
            }
        }
    }

    // ---------------------------------------------------------------- Rogue

    private Enemy _rgThird;

    private void AltRogue(int s, Player p)
    {
        Enemy Held(Enemy e, float dx, float dy = -6)
        {
            AltDummy(p, e, dx, dy);
            e.MaxHp = e.Hp = 3000;
            e.Freeze(60f, hold: true);
            return e;
        }
        float Hp(Enemy e) => IsInstanceValid(e) ? e.Hp : 0;
        bool AnyStuck() => Enumerable.Range(0, 2).Any(k => p.ThrownDaggerAt(k) is ThrownDagger d && IsInstanceValid(d) && d.State == ThrownDagger.Phase.Stuck);
        float throwDmg = Tune.Rogue.ThrowDamage * p.Stats.DamageMult;
        switch (s)
        {
            // ---- Ricochet: the dagger springs on to one more creature, then comes back (never sticking)
            case 5:
                _rgHome = p.GlobalPosition;
                p.Stats.MaxHp = 500; p.Hp = 500;
                p.Stats.CritChance = 0f;
                Take(p, "throw_ricochet");
                // three golems in a row, each in a ricochet's reach of the one before
                _probeEnemy = Held(new Golem(), 80);
                _probe2 = Held(new Golem(), 80 + 70);
                _rgThird = Held(new Golem(), 80 + 140);
                break;
            case 7:
                _hpMark = _probeEnemy.Hp; _altMark = _probe2.Hp; _shieldMark = _rgThird.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 8: _heroInput = default; break;
            case 9:
            case 10:
            case 11:
            case 12:
                if (AnyStuck()) Check("Ricochet: (a dagger stuck)", false);
                break;
            case 13:
            {
                float a = _hpMark - Hp(_probeEnemy), b = _altMark - Hp(_probe2), c = _shieldMark - Hp(_rgThird);
                Check($"Ricochet: the dagger strikes the first golem for {throwDmg:0} ({a:0.0}), springs on to the next for {throwDmg:0} ({b:0.0}) and no farther ({c:0.0}), and never sticks",
                    Math.Abs(a - throwDmg) < 0.3f && Math.Abs(b - throwDmg) < 0.3f && c < 0.01f && !AnyStuck());
                break;
            }
            case 20:
                Check($"then it flies back to you ({p.DaggersInHand} in hand)", p.DaggersInHand == 2);
                // Chain Ricochet: one creature more
                Take(p, "ricochet_more");
                _hpMark = _probeEnemy.Hp; _altMark = _probe2.Hp; _shieldMark = _rgThird.Hp;
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 21: _heroInput = default; break;
            case 27:
            {
                float a = _hpMark - Hp(_probeEnemy), b = _altMark - Hp(_probe2), c = _shieldMark - Hp(_rgThird);
                Check($"Chain Ricochet: it springs on to a third ({a:0.0}, {b:0.0}, {c:0.0})",
                    Math.Abs(a - throwDmg) < 0.3f && Math.Abs(b - throwDmg) < 0.3f && Math.Abs(c - throwDmg) < 0.3f);
                break;
            }
            case 34:
                Check($"and still comes back ({p.DaggersInHand} in hand)", p.DaggersInHand == 2);
                foreach (var e in new[] { _probeEnemy, _probe2, _rgThird }) if (IsInstanceValid(e)) e.QueueFree();
                break;

            // ---- Smoke Bomb: a cloud of smoke hides whoever is in it, and blinds whatever is
            case 40:
                Home(p);
                Take(p, "vanish_smoke");
                p.ResetAbilityCooldowns();
                _probeEnemy = AltDummy(p, new Goblin(), 50, -4);
                _heroInput = new PlayerInput { Dodge = true };
                break;
            case 41: _heroInput = default; break;
            case 42:
            {
                var c = p.LastSmoke;
                Check($"Smoke Bomb: the dodge button throws down a cloud of smoke for {Tune.Rogue.SmokeSeconds:0} s ({c?.TotalLife:0.0}), hiding you in it (hidden {p.Hidden}, vanished {p.Vanished})",
                    c != null && Math.Abs(c.TotalLife - Tune.Rogue.SmokeSeconds) < 0.1f && p.Hidden && !p.Vanished);
                break;
            }
            case 46:
                Check($"and the goblin in it loses you (lost track {_probeEnemy.LostTrack})", _probeEnemy.LostTrack);
                // out of the cloud (creatures don't block the way)
                _heroInput = new PlayerInput { Move = new Vector2(_dir, 0) };
                break;
            case 54:
            {
                _heroInput = default;
                float out_ = p.GlobalPosition.DistanceTo(p.LastSmoke.GlobalPosition);
                Check($"out of the smoke, you're seen again ({out_:0} px from it, hidden {p.Hidden})", out_ > p.LastSmoke.Radius && !p.Hidden);
                Check($"but the goblin still in it can't find anyone (lost track {_probeEnemy.LostTrack})", _probeEnemy.LostTrack);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                if (IsInstanceValid(p.LastSmoke)) p.LastSmoke.QueueFree();
                // Thick Smoke: a wider cloud
                Take(p, "smoke_wide");
                p.ResetAbilityCooldowns();
                _heroInput = new PlayerInput { Dodge = true };
                break;
            }
            case 55:
            {
                _heroInput = default;
                float want = Tune.Rogue.SmokeRadius * 1.4f;
                Check($"Thick Smoke: the cloud is 40% wider ({p.LastSmoke?.Radius:0} px round, want {want:0})", p.LastSmoke != null && Math.Abs(p.LastSmoke.Radius - want) < 0.5f);
                if (IsInstanceValid(p.LastSmoke)) p.LastSmoke.QueueFree();
                break;
            }

            // ---- Tether: recall pulls you to the dagger (striking as you arrive)
            case 60:
                Home(p);
                // (a ricocheting dagger never sticks: Tether rules it out)
                Take(p, "recall_tether", drop: "throw_ricochet");
                _probeEnemy = Held(new Golem(), 110);
                break;
            case 62:
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 63: _heroInput = default; break;
            case 66:
                Check($"(the dagger sticks in the golem: {AnyStuck()})", AnyStuck());
                _hpMark = _probeEnemy.Hp;
                _posMark = p.GlobalPosition;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 67:
                _heroInput = default;
                Check($"Tether: recall pulls you along the line to the dagger (tethering {p.Tethering})", p.Tethering || p.GlobalPosition.DistanceTo(_posMark) > 20);
                break;
            case 72:
            {
                float moved = (p.GlobalPosition.X - _posMark.X) * _dir, dealt = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.RecallDamage * p.Stats.DamageMult;
                Check($"you arrive at the golem ({moved:0} px of {110 - 10:0}) and strike it for {want:0} ({dealt:0.0})", moved > 70 && Math.Abs(dealt - want) < 0.3f);
                break;
            }
            case 80:
                Check($"and the dagger's back in hand ({p.DaggersInHand})", p.DaggersInHand == 2 && !p.Tethering);
                // Pounce: arriving by tether is always a critical strike
                Home(p);
                Take(p, "pounce");
                _heroInput = new PlayerInput { Ability = true, Aim = new Vector2(_dir, 0) };
                break;
            case 81: _heroInput = default; break;
            case 84:
                _hpMark = _probeEnemy.Hp;
                _heroInput = new PlayerInput { Ability2 = true, Aim = new Vector2(_dir, 0) };
                break;
            case 85: _heroInput = default; break;
            case 90:
            {
                float dealt = _hpMark - Hp(_probeEnemy), want = Tune.Rogue.RecallDamage * Tune.Rogue.CritMult * p.Stats.DamageMult;
                Check($"Pounce: arriving by tether lands a critical strike ({dealt:0.0}, want {want:0.0})", Math.Abs(dealt - want) < 0.3f);
                if (IsInstanceValid(_probeEnemy)) _probeEnemy.QueueFree();
                Finish();
                break;
            }
        }
    }
}

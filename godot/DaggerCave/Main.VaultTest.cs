using System;
using System.Linq;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private VaultGate _scGate;
    private Chest _scVaultChest;
    private KeyPickup _scKey;
    private Vector2 _scMark;
    private int _scDepth;

    /// <summary>
    /// --scenario=vault (the Den by default): the level has its vault (a shut gate, a chest behind
    /// it) and its hidden keys; the gate stops a hero without a key and says so; a key picked up
    /// opens it and is spent; the vault's chest deals side-grades and a rare class card; a hero
    /// carries three keys at most; keys go down to the next level; and the first mini-boss slain
    /// on a level drops a key.
    /// </summary>
    private void VaultScenario()
    {
        var p = G.Player;
        var cave = G.Cave;
        // (no creatures about, until the mini-boss is woken on purpose)
        if (_scStep < 10) foreach (var e in G.Enemies.ToArray()) e.QueueFree();
        switch (_scStep)
        {
            case 0:
            {
                if (_scT < 0.4f) return;
                _scGate = Gate;
                _scVaultChest = _world.GetChildren().OfType<Chest>().FirstOrDefault(c => c.Vault);
                var hidden = _world.GetChildren().OfType<KeyPickup>().Where(k => k.Stashed).ToList();
                int wantHidden = Tune.Vault.KeysPerLevel - (cave.Rooms.Any(r => r.Kind == RoomKind.MiniBoss) ? 1 : 0);
                ScCheck($"the level has its vault: a shut gate ({_scGate != null && !_scGate.Opened}) and a chest behind it ({_scVaultChest != null}), marked on the map ({cave.Vault != null})",
                    _scGate != null && !_scGate.Opened && _scVaultChest != null && cave.Vault != null);
                ScCheck($"and {wantHidden} hidden key(s) ({hidden.Count}), well away from the start (nearest {(hidden.Count > 0 ? hidden.Min(k => k.GlobalPosition.DistanceTo(cave.StartPos)) : 0):0} px)",
                    hidden.Count == wantHidden && hidden.All(k => k.GlobalPosition.DistanceTo(cave.StartPos) >= Tune.Vault.HiddenKeyFromStart - 20));
                if (_scGate == null || _scVaultChest == null) { ScEnd(); return; }
                p.Stats.MaxHp = 800; p.Hp = 800;
                p.Keys = 0;
                // at its doorstep, walking at it
                p.GlobalPosition = cave.Vault.Approach + new Vector2(0, -14);
                p.Velocity = Vector2.Zero;
                _scInput = new PlayerInput { Move = new Vector2(_scGate.Side, 0) };
                _scStep = 1; _scT = 0;
                break;
            }
            case 1:
            {
                if (_scT < 1.2f) return;
                float past = (p.GlobalPosition.X - _scGate.GlobalPosition.X) * _scGate.Side;
                ScCheck($"the shut gate stops a hero walking into it ({past:0} px from it)", past < -4);
                ScShot("vault_0_locked");
                _scInput = new PlayerInput { Interact = true };
                _scStep = 2; _scT = 0;
                break;
            }
            case 2:
                _scInput = default;
                if (_scT < 0.3f) return;
                ScCheck($"interacting without a key leaves it shut (open {_scGate.Opened})", !_scGate.Opened);
                // a key at the hero's feet
                _scKey = new KeyPickup { Position = p.GlobalPosition + new Vector2(0, -4) };
                _world.AddChild(_scKey);
                _scStep = 3; _scT = 0;
                break;
            case 3:
                if (_scT < 0.3f) return;
                ScCheck($"walking over a key picks it up (keys {p.Keys}, key gone {!IsInstanceValid(_scKey)})", p.Keys == 1 && !IsInstanceValid(_scKey));
                _scInput = new PlayerInput { Interact = true };
                _scStep = 4; _scT = 0;
                break;
            case 4:
                _scInput = default;
                if (_scT < 1.3f) return;
                ScCheck($"with a key the gate opens (open {_scGate.Opened}), and the key is spent (keys {p.Keys})", _scGate.Opened && p.Keys == 0);
                ScShot("vault_1_open");
                _scInput = new PlayerInput { Move = new Vector2(_scGate.Side, 0) };
                _scStep = 5; _scT = 0;
                break;
            case 5:
            {
                // on in, to the chest
                bool there = Chest.At(p.GlobalPosition) == _scVaultChest;
                if (!there && _scT < 3f) return;
                _scInput = default;
                ScCheck($"through the gate to the vault's chest (at it {there})", there);
                _scInput = new PlayerInput { Interact = true };
                _scStep = 6; _scT = 0;
                break;
            }
            case 6:
            {
                _scInput = default;
                if (_scT < 0.6f) return;
                var cards = (_scVaultChest.Cards ?? Array.Empty<string>()).Select(Upgrades.Get).ToList();
                int sides = cards.Count(u => u.Kind == UpgradeKind.RiskReward), rare = cards.Count(Upgrades.IsRare);
                ScCheck($"the vault's chest holds risk-rewards and a rare class card ({string.Join(", ", cards.Select(u => $"{u.Name} ({u.Kind})"))}), never an alteration",
                    _upgradeMenu.Visible && cards.Count == 3 && sides == 2 && rare == 1 && !cards.Any(u => u.Alteration));
                ScShot("vault_2_chest");
                if (_upgradeMenu.Visible) _upgradeMenu.ChooseFirstOpen();
                _scStep = 7; _scT = 0;
                break;
            }
            case 7:
                if (_scT < 0.5f) return;
                ScCheck($"taking a card spends it (spent {_scVaultChest.Open})", _scVaultChest.Open);
                // a hero carries three keys at most
                p.Keys = Tune.Vault.MaxKeys;
                _scKey = new KeyPickup { Position = p.GlobalPosition + new Vector2(0, -4) };
                _world.AddChild(_scKey);
                _scStep = 8; _scT = 0;
                break;
            case 8:
                if (_scT < 0.4f) return;
                ScCheck($"a hero carries {Tune.Vault.MaxKeys} keys at most (keys {p.Keys}, the fourth still lying there {IsInstanceValid(_scKey)})", p.Keys == Tune.Vault.MaxKeys && IsInstanceValid(_scKey));
                if (IsInstanceValid(_scKey)) _scKey.QueueFree();
                // keys go on down with you
                p.Keys = 2;
                _scDepth = G.Depth;
                EnterExit(G.Biome, G.Depth + 1);
                _scStep = 9; _scT = 0;
                break;
            case 9:
                if (_scT < 0.6f) return;
                ScCheck($"keys go down to the next level (depth {_scDepth} -> {G.Depth}, keys {G.Player.Keys})", G.Depth == _scDepth + 1 && G.Player.Keys == 2);
                _scStep = 10; _scT = 0;
                break;
            case 10:
            {
                // the first mini-boss slain drops a key: walk into a lair
                var lair = cave.Rooms.FirstOrDefault(r => r.Kind == RoomKind.MiniBoss && !r.Triggered);
                if (lair == null) { GD.Print("[scenario] (no mini-boss lair on this level to test the drop)"); ScEnd(); return; }
                _scMark = lair.Center;
                p.GlobalPosition = lair.Floor + new Vector2(0, -14);
                p.Velocity = Vector2.Zero;
                _scStep = 11; _scT = 0;
                break;
            }
            case 11:
            {
                if (_scT < 0.5f) return;
                // (the lair's own: the elite nearest it, not one that wandered in elsewhere)
                var boss = G.Enemies.Where(e => e.Elite && !e.Dead && e.GlobalPosition.DistanceTo(_scMark) < 400).OrderBy(e => e.GlobalPosition.DistanceTo(_scMark)).FirstOrDefault();
                if (boss == null) { if (_scT > 3f) { ScCheck("a mini-boss wakes in its lair", false); ScEnd(); } return; }
                _scMark = boss.GlobalPosition;
                boss.SetMeta("test", true);
                boss.Hurt(1e6f, Vector2.Zero, boss.GlobalPosition);
                _scStep = 12; _scT = 0;
                break;
            }
            case 12:
            {
                if (_scT < 1.2f) return;
                var dropped = _world.GetChildren().OfType<KeyPickup>().FirstOrDefault(k => !k.Stashed);
                ScCheck($"the first mini-boss slain drops a key ({(dropped != null ? $"{dropped.GlobalPosition.DistanceTo(_scMark):0} px from where it fell" : "none")})",
                    dropped != null && dropped.GlobalPosition.DistanceTo(_scMark) < 160);
                ScShot("vault_3_key");
                ScEnd();
                break;
            }
        }
    }
}

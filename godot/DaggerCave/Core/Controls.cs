using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace DaggerCave;

/// <summary>
/// The game's controls: the default bindings, rebinding (up to <see cref="Slots"/> keys or mouse
/// buttons and as many controller inputs per action), saving them with the settings, and the
/// names shown on screen for whichever device is in use.
/// </summary>
public static class Controls
{
    /// <summary>Bindings per action and device (keyboard and mouse, or controller).</summary>
    public const int Slots = 3;

    /// <summary>The actions a player can rebind, in menu order, with their menu labels.</summary>
    public static readonly (string Action, string Label)[] Rebindable =
    {
        ("move_left", "Move left"),
        ("move_right", "Move right"),
        ("move_up", "Up  ·  swim up  ·  go down an exit"),
        ("move_down", "Down  ·  swim down"),
        ("jump", "Jump"),
        ("attack", "Attack"),
        ("ability", "Ability"),
        ("ability2", "Second ability"),
        // (each dot keeps to the word after it, so a wrapped name breaks before one)
        ("dodge", "Dodge  ·\u00a0\u00a0shield  ·\u00a0\u00a0hex  ·\u00a0\u00a0updraft  ·\u00a0\u00a0vanish"),
        ("potion", "Drink a potion"),
        ("milestone", "Spend a milestone point"),
        ("support", "Support ability  ·  shout · taunt · tap · stalag · expose · mark · howl"),
        ("rope", "Lower a rope  ·  party only"),
        ("interact", "Open a chest  ·  go down an exit  ·  revive"),
        ("pause", "Pause"),
    };

    private static InputEventKey K(Key k) => new() { PhysicalKeycode = k };
    private static InputEventMouseButton M(MouseButton b) => new() { ButtonIndex = b };
    private static InputEventJoypadButton J(JoyButton b) => new() { ButtonIndex = b };
    private static InputEventJoypadMotion Ax(JoyAxis a, float v) => new() { Axis = a, AxisValue = v };

    /// <summary>The default bindings of every action (rebindable or not).</summary>
    private static Dictionary<string, InputEvent[]> Defaults() => new()
    {
        ["move_left"] = new InputEvent[] { K(Key.A), K(Key.Left), Ax(JoyAxis.LeftX, -1), J(JoyButton.DpadLeft) },
        ["move_right"] = new InputEvent[] { K(Key.D), K(Key.Right), Ax(JoyAxis.LeftX, 1), J(JoyButton.DpadRight) },
        ["move_up"] = new InputEvent[] { K(Key.W), K(Key.Up), Ax(JoyAxis.LeftY, -1), J(JoyButton.DpadUp) },
        ["move_down"] = new InputEvent[] { K(Key.S), K(Key.Down), Ax(JoyAxis.LeftY, 1), J(JoyButton.DpadDown) },
        ["jump"] = new InputEvent[] { K(Key.Space), J(JoyButton.A) },
        ["attack"] = new InputEvent[] { M(MouseButton.Left), K(Key.J), J(JoyButton.X) },
        ["ability"] = new InputEvent[] { M(MouseButton.Right), K(Key.K), J(JoyButton.RightShoulder) },
        ["ability2"] = new InputEvent[] { K(Key.F), M(MouseButton.Middle), K(Key.I), Ax(JoyAxis.TriggerRight, 1) },
        ["dodge"] = new InputEvent[] { K(Key.Shift), K(Key.L), J(JoyButton.B), J(JoyButton.LeftShoulder) },
        ["potion"] = new InputEvent[] { K(Key.Q), J(JoyButton.Y) },
        ["milestone"] = new InputEvent[] { K(Key.M), J(JoyButton.Back) },
        ["rope"] = new InputEvent[] { K(Key.C), J(JoyButton.RightStick) },
        ["support"] = new InputEvent[] { K(Key.V), M(MouseButton.Xbutton1), J(JoyButton.LeftStick) },
        // (a trigger: held to revive a friend while both thumbs keep you swimming)
        ["interact"] = new InputEvent[] { K(Key.E), Ax(JoyAxis.TriggerLeft, 1) },
        ["pause"] = new InputEvent[] { K(Key.Escape), J(JoyButton.Start) },
        // menus (not rebindable)
        ["confirm"] = new InputEvent[] { K(Key.Enter), K(Key.KpEnter), J(JoyButton.A) },
        ["restart"] = new InputEvent[] { K(Key.R) },
        ["online"] = new InputEvent[] { K(Key.O), J(JoyButton.Y) },
        ["pick_1"] = new InputEvent[] { K(Key.Key1) },
        ["pick_2"] = new InputEvent[] { K(Key.Key2) },
        ["pick_3"] = new InputEvent[] { K(Key.Key3) },
        ["pick_4"] = new InputEvent[] { K(Key.Key4) },
        ["meta"] = new InputEvent[] { K(Key.U), J(JoyButton.Back) },
        ["perks"] = new InputEvent[] { K(Key.P) },
        ["loadout"] = new InputEvent[] { K(Key.L) },
        ["skip"] = new InputEvent[] { K(Key.X), J(JoyButton.X) },
        // your build, while picking a card (from the pause menu otherwise)
        ["build"] = new InputEvent[] { K(Key.Tab), J(JoyButton.Back) },
    };

    /// <summary>Creates every action with its default bindings (then the saved settings may rebind them).</summary>
    public static void SetupDefaults()
    {
        foreach (var (action, evs) in Defaults())
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action, 0.25f);
            InputMap.ActionEraseEvents(action);
            foreach (var e in evs) InputMap.ActionAddEvent(action, e);
        }
    }

    /// <summary>Puts one action's bindings for one device back to the defaults.</summary>
    public static void ResetAction(string action)
    {
        if (!Defaults().TryGetValue(action, out var evs)) return;
        InputMap.ActionEraseEvents(action);
        foreach (var e in evs) InputMap.ActionAddEvent(action, e);
    }

    public static bool IsPad(InputEvent e) => e is InputEventJoypadButton || e is InputEventJoypadMotion;

    /// <summary>An action's bindings for one device, in order.</summary>
    public static List<InputEvent> Bindings(string action, bool pad)
        => InputMap.HasAction(action) ? InputMap.ActionGetEvents(action).Where(e => IsPad(e) == pad).ToList() : new List<InputEvent>();

    /// <summary>
    /// Binds <paramref name="e"/> to an action in a slot (replacing what was there; a slot past the
    /// end adds it). The same input bound to another rebindable action is taken off that one.
    /// Returns the label of the action it was taken from, if any.
    /// </summary>
    public static string Rebind(string action, int slot, InputEvent e)
    {
        bool pad = IsPad(e);
        e = Clean(e);
        string stolen = null;
        foreach (var (other, label) in Rebindable)
        {
            if (other == action) continue;
            foreach (var old in InputMap.ActionGetEvents(other))
                if (Same(old, e)) { InputMap.ActionEraseEvent(other, old); stolen = label; }
        }
        var list = Bindings(action, pad);
        // already bound here in another slot: move it
        list.RemoveAll(x => Same(x, e));
        if (slot < list.Count) list[slot] = e; else list.Add(e);
        Replace(action, pad, list);
        return stolen;
    }

    /// <summary>Removes the binding in a slot.</summary>
    public static void Clear(string action, bool pad, int slot)
    {
        var list = Bindings(action, pad);
        if (slot >= list.Count) return;
        list.RemoveAt(slot);
        Replace(action, pad, list);
    }

    private static void Replace(string action, bool pad, List<InputEvent> list)
    {
        var keep = Bindings(action, !pad);
        InputMap.ActionEraseEvents(action);
        // keyboard and mouse first, then the controller (the order names are taken in)
        foreach (var e in pad ? keep : list) InputMap.ActionAddEvent(action, e);
        foreach (var e in pad ? list : keep) InputMap.ActionAddEvent(action, e);
    }

    /// <summary>A copy of an input without device ids or pressure, as a binding.</summary>
    private static InputEvent Clean(InputEvent e) => e switch
    {
        InputEventKey k => K(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode),
        InputEventMouseButton m => M(m.ButtonIndex),
        InputEventJoypadButton j => J(j.ButtonIndex),
        InputEventJoypadMotion a => Ax(a.Axis, Math.Sign(a.AxisValue)),
        _ => e,
    };

    public static bool Same(InputEvent a, InputEvent b) => (a, b) switch
    {
        (InputEventKey x, InputEventKey y) => (x.PhysicalKeycode != Key.None ? x.PhysicalKeycode : x.Keycode) == (y.PhysicalKeycode != Key.None ? y.PhysicalKeycode : y.Keycode),
        (InputEventMouseButton x, InputEventMouseButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadButton x, InputEventJoypadButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadMotion x, InputEventJoypadMotion y) => x.Axis == y.Axis && Math.Sign(x.AxisValue) == Math.Sign(y.AxisValue),
        _ => false,
    };

    // ---------------------------------------------------------------- saving

    private static string Serialize(InputEvent e) => e switch
    {
        InputEventKey k => $"key:{(long)(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode)}",
        InputEventMouseButton m => $"mouse:{(int)m.ButtonIndex}",
        InputEventJoypadButton j => $"joy:{(int)j.ButtonIndex}",
        InputEventJoypadMotion a => $"axis:{(int)a.Axis}:{Math.Sign(a.AxisValue)}",
        _ => "",
    };

    private static InputEvent Deserialize(string s)
    {
        var p = s.Split(':');
        try
        {
            return p[0] switch
            {
                "key" => K((Key)long.Parse(p[1])),
                "mouse" => M((MouseButton)int.Parse(p[1])),
                "joy" => J((JoyButton)int.Parse(p[1])),
                "axis" => Ax((JoyAxis)int.Parse(p[1]), int.Parse(p[2])),
                _ => null,
            };
        }
        catch (Exception) { return null; }
    }

    /// <summary>Bumped when the defaults change in a way saved bindings should pick up.</summary>
    private const int BindingsVersion = 4;

    public static void Save(ConfigFile cfg)
    {
        foreach (var (action, _) in Rebindable)
            cfg.SetValue("bindings", action, string.Join(" ", InputMap.ActionGetEvents(action).Select(Serialize).Where(x => x != "")));
        cfg.SetValue("bindings", "_version", BindingsVersion);
    }

    public static void Load(ConfigFile cfg)
    {
        foreach (var (action, _) in Rebindable)
        {
            if (!cfg.HasSectionKey("bindings", action)) continue;
            var evs = ((string)cfg.GetValue("bindings", action)).Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(Deserialize).Where(e => e != null).ToList();
            InputMap.ActionEraseEvents(action);
            foreach (var e in evs) InputMap.ActionAddEvent(action, e);
        }
        int version = cfg.HasSectionKey("bindings", "_version") ? (int)cfg.GetValue("bindings", "_version") : 1;
        if (!cfg.HasSection("bindings")) return;
        // version 3: interact (chests, exits, reviving) has a controller input of its own, LT,
        // and the dodge button (the Warden's shield) is on B and LB. Version 1 had no interact
        // on the controller and dodge on B, LB and LT; version 2 moved interact to LB and dodge
        // to B and LT. Bindings still as those versions left them move over; changed ones stay.
        var lb = J(JoyButton.LeftShoulder);
        var lt = Ax(JoyAxis.TriggerLeft, 1);
        var interact = Bindings("interact", true);
        var dodge = Bindings("dodge", true);
        if (version < 2 && interact.Count == 0)
            Rebind("interact", 0, lt); // (taking LT off the dodge button, which keeps B and LB)
        else if (version == 2 && interact.Count == 1 && Same(interact[0], lb)
                 && dodge.Count == 2 && Same(dodge[0], J(JoyButton.B)) && Same(dodge[1], lt))
        {
            Rebind("interact", 0, lt);
            Rebind("dodge", 1, lb);
        }
        // version 4: the support ability is on L3, the rope on R3 and the milestone on BACK (a saved set
        // that still has the old controller buttons, or none, moves over)
        if (version < 4)
        {
            void SetPad(string action, JoyButton from, JoyButton to, bool onlyIfPlain)
            {
                var pad = Bindings(action, true);
                bool plain = pad.Count == 0 || (pad.Count == 1 && pad[0] is InputEventJoypadButton jb && jb.ButtonIndex == from);
                if (onlyIfPlain && !plain) return;
                foreach (var e in pad) InputMap.ActionEraseEvent(action, e);
                InputMap.ActionAddEvent(action, J(to));
            }
            SetPad("support", JoyButton.Misc1, JoyButton.LeftStick, true);
            SetPad("rope", JoyButton.LeftStick, JoyButton.RightStick, true);
            SetPad("milestone", JoyButton.RightStick, JoyButton.Back, true);
        }
    }

    // ---------------------------------------------------------------- names

    /// <summary>The name of an action's first binding on the device in use ("F", "RT", "LEFT CLICK").</summary>
    public static string Name(string action) => Name(action, G.Main?.UsingPad ?? false);

    public static string Name(string action, bool pad)
    {
        var list = Bindings(action, pad);
        return list.Count > 0 ? NameOf(list[0]) : "(unbound)";
    }

    public static string NameOf(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey k:
                {
                    // (the key's name on this keyboard's layout; a window-less test run has no layout)
                    var code = k.PhysicalKeycode == Key.None ? k.Keycode
                        : DisplayServer.GetName() == "headless" ? k.PhysicalKeycode
                        : DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode);
                    string n = OS.GetKeycodeString(code);
                    return string.IsNullOrEmpty(n) ? "?" : n.ToUpperInvariant();
                }
            case InputEventMouseButton m:
                return m.ButtonIndex switch
                {
                    MouseButton.Left => "LEFT CLICK",
                    MouseButton.Right => "RIGHT CLICK",
                    MouseButton.Middle => "MIDDLE CLICK",
                    MouseButton.WheelUp => "WHEEL UP",
                    MouseButton.WheelDown => "WHEEL DOWN",
                    MouseButton.Xbutton1 => "MOUSE 4",
                    MouseButton.Xbutton2 => "MOUSE 5",
                    _ => $"MOUSE {(int)m.ButtonIndex}",
                };
            case InputEventJoypadButton j:
                return j.ButtonIndex switch
                {
                    JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y",
                    JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB",
                    JoyButton.Back => "BACK", JoyButton.Start => "START", JoyButton.Guide => "GUIDE",
                    JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3",
                    JoyButton.DpadUp => "D-PAD UP", JoyButton.DpadDown => "D-PAD DOWN", JoyButton.DpadLeft => "D-PAD LEFT", JoyButton.DpadRight => "D-PAD RIGHT",
                    JoyButton.Misc1 => "SHARE",
                    _ => $"BUTTON {(int)j.ButtonIndex}",
                };
            case InputEventJoypadMotion a:
                return a.Axis switch
                {
                    JoyAxis.TriggerLeft => "LT",
                    JoyAxis.TriggerRight => "RT",
                    JoyAxis.LeftX => a.AxisValue < 0 ? "LEFT STICK LEFT" : "LEFT STICK RIGHT",
                    JoyAxis.LeftY => a.AxisValue < 0 ? "LEFT STICK UP" : "LEFT STICK DOWN",
                    JoyAxis.RightX => a.AxisValue < 0 ? "RIGHT STICK LEFT" : "RIGHT STICK RIGHT",
                    JoyAxis.RightY => a.AxisValue < 0 ? "RIGHT STICK UP" : "RIGHT STICK DOWN",
                    _ => $"AXIS {(int)a.Axis}",
                };
        }
        return "?";
    }
}

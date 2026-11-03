using Godot;

namespace DaggerCave;

/// <summary>
/// Abilities never wait on one another: one pressed while another is under way starts at once. A cast's wind-up gathers on
/// regardless (Player.Hold.cs: several can gather together), and an action that holds the hero's body (a roll, a heaving
/// swing, a swing, a charge, a shield bash, a tether's pull) is cut short by the new one. Only the new ability's own
/// cooldown can make it wait, and an action is cut only once the new one is sure to start.
/// </summary>
public partial class Player
{
    /// <summary>The swing under way (a heaving one too) ends where it is.</summary>
    private void CutSwing()
    {
        _swingT = -1;
        _heaveRootT = 0f;
    }

    /// <summary>The roll (or air dash) under way ends; its speed carries on.</summary>
    private void CutDodge()
    {
        _dodgeT = 0f;
        _airDashT = 0f;
    }

    /// <summary>The Warden's charge and shield bash under way end where they are.</summary>
    private void CutShieldMoves()
    {
        if (_dashT > 0f) { _dashT = 0f; WardenDashEnd(); }
        _bashT = 0f;
    }

    /// <summary>The Rogue's pull toward her dagger lets go (the dagger comes home by itself).</summary>
    private void CutTether()
    {
        if (_tetherTo == null) return;
        if (IsInstanceValid(_tetherTo)) _tetherTo.ComeBack();
        _tetherTo = null;
        _tetherSurprise = false;
    }
}

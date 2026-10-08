using System;
using Godot;

namespace DaggerCave;

public partial class Main
{
    private int _sfxStage;
    private float _sfxT, _sfxPeakLoud = -200f, _sfxPeakQuiet = -200f;

    /// <summary>`--scenario=sfxvol`: the Sound effects volume turns the sound effects down (measured on the Master bus's peak, so it needs
    /// real mixing: run it with Godot's --write-movie, as the dummy audio driver mixes nothing).</summary>
    private void SfxVolScenario()
    {
        if (_scT < 0.6f) return;
        int sfx = AudioServer.GetBusIndex("SFX");
        float Peak() => Math.Max(AudioServer.GetBusPeakVolumeLeftDb(0, 0), AudioServer.GetBusPeakVolumeRightDb(0, 0));
        _sfxT += (float)GetProcessDeltaTime();
        switch (_sfxStage)
        {
            case 0:
                ScCheck($"an SFX bus ({sfx}) the effects play on ({G.Sfx.TestBuses()}); buses: {AudioServer.BusCount}", sfx > 0 && G.Sfx.TestBuses() == "SFX");
                GameSettings.MusicVolume = 0f; GameSettings.SfxVolume = 1f; GameSettings.ApplySound();
                G.Sfx.StopAll();
                _sfxStage = 1; _sfxT = 0; break;
            case 1:
                if (_sfxT > 0.05f && _sfxT < 0.5f && ((int)(_sfxT * 20) % 3 == 0)) G.Sfx.Play("hit", G.Player.GlobalPosition);
                if (_sfxT > 0.1f) _sfxPeakLoud = Math.Max(_sfxPeakLoud, Peak());
                if (_sfxT > 1.2f) { GameSettings.SfxVolume = 0.1f; GameSettings.ApplySound(); _sfxStage = 2; _sfxT = 0; }
                break;
            case 2:
                if (_sfxT > 0.05f && _sfxT < 0.5f && ((int)(_sfxT * 20) % 3 == 0)) G.Sfx.Play("hit", G.Player.GlobalPosition);
                if (_sfxT > 0.1f) _sfxPeakQuiet = Math.Max(_sfxPeakQuiet, Peak());
                if (_sfxT > 1.2f)
                {
                    ScCheck($"at 10% the effects peak {_sfxPeakLoud - _sfxPeakQuiet:0.0} dB lower ({_sfxPeakLoud:0.0} -> {_sfxPeakQuiet:0.0} dB; SFX bus at {AudioServer.GetBusVolumeDb(sfx):0.0} dB)",
                        _sfxPeakLoud > -100f && _sfxPeakLoud - _sfxPeakQuiet > 30f);
                    ScEnd();
                }
                break;
        }
    }
}

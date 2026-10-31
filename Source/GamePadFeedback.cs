using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

public sealed partial class IronNebulaGame
{
    private float _rumbleLow, _rumbleHigh;

    private bool CanRumble => IsActive && _pad.IsConnected && (!CoopActive || _pilot.Lives>0) &&
        _mode==GameMode.Playing && !_helpOpen && !_settingsOpen && !_difficultyPromptOpen;

    private void PulseGamePad(float low, float high)
    {
        if(_coopHost!=null && _coopActorId!=0) return;
        if(!CanRumble) return;
        // Mix by maximum, so rapid fire cannot overpower a damage impact.
        _rumbleLow=MathF.Max(_rumbleLow,Math.Clamp(low,0f,1f));
        _rumbleHigh=MathF.Max(_rumbleHigh,Math.Clamp(high,0f,1f));
    }

    private void StopGamePadFeedback()
    {
        _rumbleLow=_rumbleHigh=0f;
        GamePad.SetVibration(_activePadIndex,0f,0f);
        _dualSense.SetFeedback(false,false,false,false,0,0,0,0,false,false,Color.Black);
    }

    private void UpdateGamePadFeedback(float dt)
    {
        if(!CanRumble) { StopGamePadFeedback(); return; }
        _rumbleLow=MathF.Max(0f,_rumbleLow-dt*2.5f);
        _rumbleHigh=MathF.Max(0f,_rumbleHigh-dt*4f);
        // Beam is continuous; discrete weapons enqueue short recoil pulses.
        float beamLow=_beamActive?.10f:0f;
        float beamHigh=_beamActive?.20f:0f;
        float low=MathF.Max(_rumbleLow,beamLow),high=MathF.Max(_rumbleHigh,beamHigh);
        if(_usingDualSense)
        {
            Color color=_pilot.Health<=1?Color.OrangeRed:
                _pilot.Shield>0?Color.DeepSkyBlue:PrimaryWeaponColor(_pilot.PrimaryMode);
            _dualSense.SetFeedback(true,_adaptiveTriggers,_controllerHaptics,_bluetoothHaptics,
                _controllerStrength/100f,low,high,(int)_pilot.PrimaryMode,
                _pad.Triggers.Right>.25f || PadDown(Buttons.X),_precisionFire,color);
        }
        else GamePad.SetVibration(_activePadIndex,_controllerHaptics?low*_controllerStrength/100f:0,
            _controllerHaptics?high*_controllerStrength/100f:0);
    }

    protected override void OnDeactivated(object sender, EventArgs args)
    {
        StopGamePadFeedback();
        base.OnDeactivated(sender,args);
    }

    protected override void OnExiting(object sender, ExitingEventArgs args)
    {
        StopGamePadFeedback();
        _dualSense.Dispose();
        base.OnExiting(sender,args);
    }
}

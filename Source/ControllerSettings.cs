using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

public sealed partial class IronNebulaGame
{
    private bool _controllerHaptics=true,_adaptiveTriggers=true,_bluetoothHaptics;
    private int _controllerStrength=65,_controllerSelection;
    private string ControllerStatus => _usingDualSense?_dualSense.Status:
        _pad.IsConnected?"XBOX / XINPUT CONNECTED / SLOT "+((int)_activePadIndex+1):_dualSense.Status;

    private void UpdateControllerSettings(KeyboardState keys)
    {
        if(MenuUp(keys)) _controllerSelection=(_controllerSelection+3)%4;
        if(MenuDown(keys)) _controllerSelection=(_controllerSelection+1)%4;
        if(MenuLeft(keys)) ChangeControllerSetting(_controllerSelection,-1);
        else if(MenuRight(keys)||MenuConfirm(keys)) ChangeControllerSetting(_controllerSelection,1);
    }

    private void ChangeControllerSetting(int index,int direction)
    {
        switch(index)
        {
            case 0:_controllerHaptics=!_controllerHaptics;break;
            case 1:_controllerStrength=Math.Clamp(_controllerStrength+direction*5,0,100);break;
            case 2:_adaptiveTriggers=!_adaptiveTriggers;break;
            case 3:_bluetoothHaptics=!_bluetoothHaptics;break;
        }
        StopGamePadFeedback();SaveGuiPreferences();
    }

    private void DrawControllerSettings()
    {
        Color cyan=new(105,225,252),gold=new(255,205,107);
        _ui.TextFit(_batch,ControllerStatus,new Vector2(112,342),.43f,1660,cyan);
        string[] labels={"HAPTICS / XBOX VIBRATION","HAPTIC STRENGTH","DUALSENSE ADAPTIVE L2 / R2","BLUETOOTH HD HAPTICS (EXPERIMENTAL)"};
        string[] values={_controllerHaptics?"ON":"OFF",_controllerStrength+"%",
            _adaptiveTriggers?"ON":"OFF",_bluetoothHaptics?"ON":"OFF - RUMBLE FALLBACK"};
        for(int i=0;i<4;i++)
        {
            int y=410+i*124;bool chosen=i==_controllerSelection;
            _ui.Rect(_batch,new Rectangle(103,y,1696,112),chosen?new Color(24,70,105,248):new Color(5,27,47,240));
            _ui.TextFit(_batch,labels[i],new Vector2(133,y+12),.48f,1510,Color.White);
            _ui.TextFit(_batch,values[i],new Vector2(135,y+63),.43f,1510,chosen?gold:cyan);
        }
        _ui.TextFit(_batch,"USB: HD ACTUATOR AUDIO WHEN AVAILABLE. BT: RUMBLE OR EXPERIMENTAL HD STREAM.",
            new Vector2(110,934),.36f,1670,cyan);
        _ui.TextFit(_batch,NavigatePrompt+" ADJUST   "+PagePrompt+" NEXT PAGE   "+BackPrompt+" CLOSE",
            new Vector2(110,1017),.37f,1670,Color.White);
    }
}

using Microsoft.Xna.Framework.Input;

namespace IronNebula;

internal sealed class ControllerNavigation
{
    private readonly float[] _repeat=new float[4];
    private readonly bool[] _held=new bool[4],_blocked=new bool[4],_pulse=new bool[4];
    internal bool this[int direction] => _pulse[direction];

    internal void Reset(GamePadState state)
    {
        for(int i=0;i<4;i++) { _blocked[i]=Held(state,i);_held[i]=false;_pulse[i]=false;_repeat[i]=.4f; }
    }

    internal void Update(GamePadState state,bool active,float dt)
    {
        for(int i=0;i<4;i++)
        {
            bool down=Held(state,i);
            if(!active) _blocked[i]=down;
            if(!down) _blocked[i]=false;
            bool held=active && down && !_blocked[i];
            _pulse[i]=held && !_held[i];
            if(!held || !_held[i]) _repeat[i]=.4f;
            else
            {
                _repeat[i]-=dt;
                if(_repeat[i]<=0) { _pulse[i]=true;_repeat[i]=.12f; }
            }
            _held[i]=held;
        }
    }

    private static bool Held(GamePadState state,int direction)
    {
        Buttons button=direction switch { 0=>Buttons.DPadUp,1=>Buttons.DPadDown,2=>Buttons.DPadLeft,_=>Buttons.DPadRight };
        float axis=direction switch { 0=>state.ThumbSticks.Left.Y,1=>-state.ThumbSticks.Left.Y,2=>-state.ThumbSticks.Left.X,_=>state.ThumbSticks.Left.X };
        return state.IsConnected && (state.IsButtonDown(button)||axis>.65f);
    }
}

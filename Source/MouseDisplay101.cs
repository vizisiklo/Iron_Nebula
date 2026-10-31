using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

/// <summary>Mouse flight and click-accessible native 1920x1080 menus, including resolution dropdown.</summary>
public sealed partial class IronNebulaGame
{
    private MouseState _mouse, _previousMouse;
    private Vector2 _mouseUi, _mouseTarget;
    private bool _mouseValid, _mouseClick, _mouseMoved, _mouseFlightActive;
    private bool _resolutionDropdownOpen;
    private int _resolutionHoverIndex;
    private int _aspectRatio; // 0 = 16:9, 1 = 16:10
    private static readonly int[][] RatioChoices = { new[]{0,1,2,3,4,5}, new[]{6,7,8,9,10,11} };

    private bool MouseFire => _mouseValid && _mouse.LeftButton==ButtonState.Pressed;
    private bool MousePrecision => _mouseValid && _mouse.RightButton==ButtonState.Pressed;
    private bool MouseHit(Rectangle r) => _mouseClick && _mouseValid && r.Contains((int)_mouseUi.X,(int)_mouseUi.Y);

    private void SampleMouseInput()
    {
        _previousMouse=_mouse;
        _mouse=Mouse.GetState();
        _mouseClick=_mouse.LeftButton==ButtonState.Pressed && _previousMouse.LeftButton==ButtonState.Released;
        _mouseMoved=_mouse.X!=_previousMouse.X || _mouse.Y!=_previousMouse.Y;
        // Same letterboxed virtual viewport used by PresentSceneWithHitFeedback.
        int w=Math.Max(1,Window.ClientBounds.Width),h=Math.Max(1,Window.ClientBounds.Height);
        float scale=Math.Min(w/(float)ViewWidth,h/(float)ViewHeight);
        float originX=(w-ViewWidth*scale)*.5f,originY=(h-ViewHeight*scale)*.5f;
        _mouseUi=new Vector2((_mouse.X-originX)/scale,(_mouse.Y-originY)/scale);
        _mouseValid=_mouse.X>=originX && _mouse.Y>=originY &&
            _mouse.X<originX+ViewWidth*scale && _mouse.Y<originY+ViewHeight*scale;
        bool liveFlight=IsActive && _mode==GameMode.Playing && !_helpOpen && !_settingsOpen && !_difficultyPromptOpen;
        // Do not carry a stale mouse target through pause, settings or New Game.
        if(!liveFlight) _mouseFlightActive=false;
        if(liveFlight && _mouseValid && (_mouseMoved || _mouseClick || _mouseFlightActive))
        {
            // Pointer target in the letterboxed game world; flight follows with
            // a short critically damped tether rather than teleporting.
            _mouseTarget=new Vector2(MathHelper.Clamp(_mouseUi.X,105f,ViewWidth-145f),
                MathHelper.Clamp(_mouseUi.Y,110f,ViewHeight-115f));
            if(_mouseMoved || _mouseClick) _mouseFlightActive=true;
        }
        IsMouseVisible=true;
    }

    private bool MovePilotDirectlyWithMouse(Vector2 keyboardOrPad, float dt)
    {
        if(keyboardOrPad.LengthSquared()>.0001f)
        {
            // Any WASD/arrow/controller movement takes priority. A subsequent
            // mouse move switches straight back to pointer-based flight.
            _mouseFlightActive=false;
            return false;
        }
        if(!_mouseFlightActive) return false;
        if(!IsActive || !_mouseValid)
        {
            // Avoid drifting beyond the edge when the mouse leaves the viewport
            // or the window loses focus.
            _pilot.Velocity=Vector2.Zero;
            return true;
        }
        // Exact critically damped spring integration: stable for variable dt,
        // smooth acceleration, no overshoot from a stationary start.
        float spring=_precisionFire?12f:18f;
        Vector2 offset=_pilot.Position-_mouseTarget;
        Vector2 impulse=_pilot.Velocity+spring*offset;
        float decay=MathF.Exp(-spring*dt);
        _pilot.Position=_mouseTarget+(offset+impulse*dt)*decay;
        _pilot.Velocity=(_pilot.Velocity-spring*impulse*dt)*decay;
        _pilot.Position=new Vector2(MathHelper.Clamp(_pilot.Position.X,105f,ViewWidth-145f),
            MathHelper.Clamp(_pilot.Position.Y,110f,ViewHeight-115f));
        return true;
    }

    private bool HandleTitleMouseInput()
    {
        if(!_mouseValid || (!_mouseMoved && !_mouseClick)) return false;
        for(int i=0;i<6;i++)
        {
            Rectangle r=new(110,375+i*103,827,94);
            if(r.Contains((int)_mouseUi.X,(int)_mouseUi.Y))
            {
                _titleMenuSelection=i;
                return MouseHit(r);
            }
        }
        return false;
    }
    private bool HandlePauseMouseInput()
    {
        if(!_mouseValid || (!_mouseMoved && !_mouseClick)) return false;
        for(int i=0;i<4;i++)
        {
            Rectangle r=new(486,365+i*129,950,115);
            if(r.Contains((int)_mouseUi.X,(int)_mouseUi.Y))
            {
                _pauseMenuSelection=i;
                return MouseHit(r);
            }
        }
        return false;
    }
    private bool HandleDifficultyMouseInput()
    {
        if(!_mouseClick || !_mouseValid) return false;
        // Selected-difficulty screen has the same 3 wide option rows as its artwork.
        for(int i=0;i<3;i++)
        {
            if(MouseHit(new Rectangle(450,400+i*145,1020,129)))
            {
                _selectedDifficulty=(DifficultyMode)i;
                SaveGuiPreferences();
                _difficultyPromptOpen=false;
                StartNewGame();
                return true;
            }
        }
        return false;
    }
    private bool HandleShopMouseInput()
    {
        if(!_mouseClick || !_mouseValid) return false;
        for(int t=0;t<3;t++)
            if(MouseHit(new Rectangle(498+t*320,289,309,71)))
            {
                _shopTab=t;_shopSelection=ActiveShopItems()[0];return true;
            }
        int[] entries=ActiveShopItems();
        int selected=Math.Max(0,Array.IndexOf(entries,_shopSelection));
        int first=Math.Clamp(selected-1,0,Math.Max(0,entries.Length-3));
        for(int slot=0;slot<3;slot++)
        {
            int at=first+slot;
            if(at<entries.Length && MouseHit(new Rectangle(498,383+slot*183,958,169)))
            { _shopSelection=entries[at];return true; }
        }
        if(MouseHit(new Rectangle(1508,908,352,61))) {PurchaseSelectedItem();return true;}
        if(MouseHit(new Rectangle(1636,990,264,78))) {ContinueShopByMouse();return true;}
        return false;
    }
    private void ContinueShopByMouse()
    {
        if(CoopActive) { _coopReady=!_coopReady;return; }
        if(_shopAfterFinalSector && _infernoActive) FinishRun(true);
        else
        {
            _mode=GameMode.Playing;
            if(_shopAfterFinalSector) BeginInfernoSector();
            else BeginRiftSector();
        }
    }

    private bool HandleModalMouseInput()
    {
        if(!_mouseClick || !_mouseValid) return false;
        if(_helpOpen)
        {
            if(MouseHit(new Rectangle(0,940,ViewWidth/2,140))) _helpPage=(_helpPage+3)%4;
            else if(MouseHit(new Rectangle(ViewWidth/2,940,ViewWidth/2,140))) _helpPage=(_helpPage+1)%4;
            else if(MouseHit(new Rectangle(1780,0,140,210))) _helpOpen=false;
            return true;
        }
        if(!_settingsOpen) return false;
        if(MouseHit(new Rectangle(103,239,548,85))) { _settingsTab=0;return true; }
        if(MouseHit(new Rectangle(677,239,548,85))) { _settingsTab=1;return true; }
        if(MouseHit(new Rectangle(1251,239,548,85))) { _settingsTab=2;return true; }
        if(_settingsTab==2)
        {
            for(int i=0;i<4;i++)
                if(MouseHit(new Rectangle(103,410+i*124,1696,112)))
                { _controllerSelection=i;ChangeControllerSetting(i,1);return true; }
            return false;
        }
        if(_settingsTab==1)
        {
            int first=(_controlsSelection/8)*8;
            for(int slot=0;slot<8;slot++)
            {
                int x=101+(slot/4)*875,y=409+(slot%4)*135;
                if(MouseHit(new Rectangle(x,y,842,124)))
                {
                    int index=first+slot;
                    if(index<ControlBindings.All.Length)
                    {
                        if(_controlsSelection==index) { _bindingCapture=true; _bindingNotice="PRESS NEW KEY - ESC CANCEL"; }
                        else _controlsSelection=index;
                    }
                    return true;
                }
            }
            return false;
        }
        for(int i=0;i<14;i++)
        {
            int x=102+(i/7)*875,y=333+(i%7)*96;
            if(MouseHit(new Rectangle(x,y,842,88)))
            {
                _settingsSelection=i;
                if(i is 0 or 1 or 11 or 12)
                {
                    // Slider region: set directly by mouse position, rounded to 5 or 10.
                    if(_mouseUi.X>x+445)
                    {
                        int v=Math.Clamp((int)MathF.Round((_mouseUi.X-(x+460))/335f*100f),0,100);
                        if(i<=1 && _audio!=null) _audio.SetVolume(i==0,v);
                        else if(i==11) _enemyShotGlow=(v/10)*10;
                        else if(i==12) _hitEffectStrength=(v/10)*10;
                        SaveGuiPreferences();
                    }
                    else ChangeAudioSetting(i,1,true);
                }
                else ChangeAudioSetting(i,1,true);
                return true;
            }
        }
        return false;
    }

    private void ChangeAspectRatio(int direction)
    {
        int previousHeight=ResolutionOptions[_resolutionIndex].Height;
        _aspectRatio=1-_aspectRatio;
        int closest=RatioChoices[_aspectRatio][0],error=int.MaxValue;
        foreach(int index in RatioChoices[_aspectRatio])
        {
            int delta=Math.Abs(ResolutionOptions[index].Height-previousHeight);
            if(delta<error) {error=delta;closest=index;}
        }
        _resolutionIndex=closest;
        _resolutionDropdownOpen=false;
        ApplyDisplayConfiguration();
    }

    private void OpenResolutionDropdown()
    {
        _resolutionDropdownOpen=true;
        _resolutionHoverIndex=Array.IndexOf(RatioChoices[_aspectRatio],_resolutionIndex);
        if(_resolutionHoverIndex<0) _resolutionHoverIndex=0;
    }

    private void UpdateResolutionDropdown(KeyboardState keys)
    {
        int[] list=RatioChoices[_aspectRatio];
        if(MenuUp(keys)) _resolutionHoverIndex=(_resolutionHoverIndex+list.Length-1)%list.Length;
        if(MenuDown(keys)) _resolutionHoverIndex=(_resolutionHoverIndex+1)%list.Length;
        // Hover highlights without changing the current screen mode.
        for(int i=0;i<list.Length;i++)
        {
            Rectangle row=new(1060,416+i*70,780,64);
            if(_mouseValid && row.Contains((int)_mouseUi.X,(int)_mouseUi.Y))
            {
                if(_mouseMoved) _resolutionHoverIndex=i;
                if(MouseHit(row)) { CommitResolutionChoice(list[i]);return; }
            }
        }
        if(MenuBack(keys)||MouseHit(new Rectangle(0,0,ViewWidth,320)))
        { _resolutionDropdownOpen=false;return; }
        if(MenuConfirm(keys)) CommitResolutionChoice(list[_resolutionHoverIndex]);
    }

    private void CommitResolutionChoice(int index)
    {
        _resolutionIndex=index;
        _resolutionDropdownOpen=false;
        ApplyDisplayConfiguration();
        SaveGuiPreferences();
    }

    private void DrawResolutionDropdown()
    {
        Color cyan=new(106,223,252),gold=new(255,202,104);
        Rectangle panel=new(1041,350,828,533);
        _ui.Rect(_batch,panel,new Color(2,12,31,252));
        _ui.Frame(_batch,panel,cyan,2);
        _ui.TextShadow(_batch,"AVAILABLE RESOLUTIONS   "+(_aspectRatio==0?"16:9":"16:10"),new Vector2(1060,367),.43f,Color.White);
        int[] list=RatioChoices[_aspectRatio];
        for(int i=0;i<list.Length;i++)
        {
            int index=list[i];
            Rectangle r=new(1060,416+i*70,780,64);
            bool active=_resolutionHoverIndex==i;
            _ui.Rect(_batch,r,active?new Color(26,77,114,255):new Color(8,30,52,253));
            _ui.Rect(_batch,new Rectangle(r.X,r.Y,active?9:3,r.Height),active?gold:cyan*.6f);
            _ui.Text(_batch,ResolutionOptions[index].Name,new Vector2(r.X+25,r.Y+12),.53f,active?gold:Color.White);
        }
        _ui.TextFit(_batch,ConfirmPrompt+" APPLY     "+BackPrompt+" CANCEL",new Vector2(1062,846),.33f,780,cyan);
    }
}

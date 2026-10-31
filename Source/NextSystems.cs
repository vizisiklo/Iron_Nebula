using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

/// <summary>Checkpoint/shop persistence, remappable flight controls and gamepad input.</summary>
public sealed partial class IronNebulaGame
{
    private readonly ControlBindings _bindings = new();
    private GamePadState _pad, _previousPad;
    private PlayerIndex _activePadIndex = PlayerIndex.One;
    private bool _padWasConnected;
    private readonly DualSenseDevice _dualSense=new();
    private bool _usingDualSense;
    private int _dualSenseGeneration;
    private readonly ControllerNavigation _padNavigation=new();
    private Vector2 FlightStick => _pad.ThumbSticks.Right.LengthSquared()>_pad.ThumbSticks.Left.LengthSquared()
        ? _pad.ThumbSticks.Right : _pad.ThumbSticks.Left;

    private void SampleGamePadInput(float dt)
    {
        if(_dualSense.TryState(out var sony,out int generation))
        {
            bool changed=!_usingDualSense || generation!=_dualSenseGeneration;
            if(changed) StopGamePadFeedback();
            _usingDualSense=true;_dualSenseGeneration=generation;
            _pad=sony;
            if(changed) { _previousPad=sony;_padNavigation.Reset(sony); }
            _padWasConnected=true;
            UpdatePadMenuRepeat(dt);
            return;
        }
        if(_usingDualSense)
        {
            StopGamePadFeedback();_usingDualSense=false;_padWasConnected=false;
            _padNavigation.Reset(default);
        }
        // Retain the selected XInput slot until disconnected; Windows may assign
        // an Xbox controller to any of the four slots.
        _pad=GamePad.GetState(_activePadIndex,GamePadDeadZone.Circular);
        if(_pad.IsConnected)
        {
            if(!_padWasConnected) { _previousPad=_pad;_padNavigation.Reset(_pad); }
            _padWasConnected=true;
            UpdatePadMenuRepeat(dt);
            return;
        }
        StopGamePadFeedback();
        _padWasConnected=false;
        _previousPad=default;
        for(int i=0;i<4;i++)
        {
            var index=(PlayerIndex)i;
            var candidate=GamePad.GetState(index,GamePadDeadZone.Circular);
            if(!candidate.IsConnected) continue;
            _activePadIndex=index;
            _pad=candidate;
            // A held button on reconnect must not activate a menu or fire an edge.
            _previousPad=candidate;
            _padNavigation.Reset(candidate);
            _padWasConnected=true;
            break;
        }
        UpdatePadMenuRepeat(dt);
    }

    private void UpdatePadMenuRepeat(float dt)
        =>_padNavigation.Update(_pad,IsActive,dt);
    private bool _restoringRun;
    private bool _hasDiskSave;
    private int _settingsTab; // 0 audiovisual, 1 key bindings, 2 controllers
    private int _controlsSelection, _helpPage;
    private bool _bindingCapture;
    private string _bindingNotice="";

    private bool PadPressed(Buttons b) => _pad.IsConnected && _pad.IsButtonDown(b) && !_previousPad.IsButtonDown(b);
    private bool PadDown(Buttons b) => _pad.IsConnected && _pad.IsButtonDown(b);
    private bool PadUp() => _padNavigation[0];
    private bool PadDownMenu() => _padNavigation[1];
    private bool PadLeft() => _padNavigation[2];
    private bool PadRight() => _padNavigation[3];
    private bool MenuUp(KeyboardState k) => KeyPressed(k,Keys.Up)||KeyPressed(k,Keys.W)||PadUp();
    private bool MenuDown(KeyboardState k) => KeyPressed(k,Keys.Down)||KeyPressed(k,Keys.S)||PadDownMenu();
    private bool MenuLeft(KeyboardState k) => KeyPressed(k,Keys.Left)||KeyPressed(k,Keys.A)||PadLeft();
    private bool MenuRight(KeyboardState k) => KeyPressed(k,Keys.Right)||KeyPressed(k,Keys.D)||PadRight();
    private bool MenuConfirm(KeyboardState k) => KeyPressed(k,Keys.Enter)||KeyPressed(k,Keys.Space)||PadPressed(Buttons.A);
    private bool MenuBack(KeyboardState k) => KeyPressed(k,Keys.Escape)||PadPressed(Buttons.B);
    private bool ActionDown(KeyboardState k,GameAction a)
    {
        if(_bindings.Down(k,a)) return true;
        if(!_pad.IsConnected) return false;
        return a switch
        {
            GameAction.MoveUp => _pad.DPad.Up==ButtonState.Pressed || _pad.ThumbSticks.Left.Y>.23f,
            GameAction.MoveDown => _pad.DPad.Down==ButtonState.Pressed || _pad.ThumbSticks.Left.Y<-.23f,
            GameAction.MoveLeft => _pad.DPad.Left==ButtonState.Pressed || _pad.ThumbSticks.Left.X<-.23f,
            GameAction.MoveRight => _pad.DPad.Right==ButtonState.Pressed || _pad.ThumbSticks.Left.X>.23f,
            GameAction.Fire => PadDown(Buttons.X) || _pad.Triggers.Right>.25f,
            GameAction.Precision => _pad.Triggers.Left>.25f,
            GameAction.WeaponNext => PadDown(Buttons.RightShoulder),
            GameAction.WeaponPrevious => PadDown(Buttons.LeftShoulder),
            GameAction.Pause => PadDown(Buttons.Start),
            _ => false
        };
    }
    private bool ActionPressed(KeyboardState k,GameAction a)
    {
        bool keyPressed=KeyPressed(k,_bindings[a]);
        return a switch
        {
            GameAction.WeaponNext => keyPressed || PadPressed(Buttons.RightShoulder),
            GameAction.WeaponPrevious => keyPressed || PadPressed(Buttons.LeftShoulder),
            GameAction.Pause => keyPressed || PadPressed(Buttons.Start),
            _ => keyPressed
        };
    }
    private Vector2 FlightDirection(KeyboardState keys)
    {
        Vector2 dir=Vector2.Zero;
        if(_bindings.Down(keys,GameAction.MoveLeft)) dir.X-=1f;
        if(_bindings.Down(keys,GameAction.MoveRight)) dir.X+=1f;
        if(_bindings.Down(keys,GameAction.MoveUp)) dir.Y-=1f;
        if(_bindings.Down(keys,GameAction.MoveDown)) dir.Y+=1f;
        if(_pad.IsConnected)
        {
            Vector2 stick=FlightStick;
            Vector2 analog=new(stick.X,-stick.Y);
            bool keyboardMove=_bindings.Down(keys,GameAction.MoveLeft)||_bindings.Down(keys,GameAction.MoveRight)||
                _bindings.Down(keys,GameAction.MoveUp)||_bindings.Down(keys,GameAction.MoveDown);
            if(!keyboardMove)
            {
                // Circular dead zone is already applied by MonoGame. Preserve
                // proportional stick speed instead of a second digital threshold.
                dir=analog;
                if(_pad.DPad.Left==ButtonState.Pressed) dir.X=-1f;
                if(_pad.DPad.Right==ButtonState.Pressed) dir.X=1f;
                if(_pad.DPad.Up==ButtonState.Pressed) dir.Y=-1f;
                if(_pad.DPad.Down==ButtonState.Pressed) dir.Y=1f;
            }
        }
        if(dir.LengthSquared()>1f) dir.Normalize();
        return dir;
    }

    private void SaveBoundary(string stage)
    {
        if(CoopActive||_trainingRun||_restoringRun) return;
        bool shop=stage=="Shop";
        var data=new RunSaveData
        {
            Stage=stage,
            Difficulty=(int)_runDifficulty,
            Sector=shop?(_infernoActive?3:(_shopAfterFinalSector?2:1)):_checkpointSector,
            Score=shop?_score:_checkpointScore,
            Credits=shop?_credits:_checkpointCredits,
            OwnedWeapons=shop?_ownedWeaponsMask:_checkpointUnlockMask,
            Lives=shop?_pilot.Lives:_checkpointLives,
            WeaponLevel=shop?_pilot.WeaponLevel:_checkpointWeaponLevel,
            PrimaryMode=(int)(shop?_pilot.PrimaryMode:_checkpointWeaponMode),
            Shield=shop?_pilot.Shield:_checkpointShield,
            MaxShield=shop?_pilot.MaxShield:_checkpointMaxShield,
            Health=shop?_pilot.Health:_checkpointMaxHull,
            MaxHull=shop?_pilot.MaxHealth:_checkpointMaxHull,
            DroneUpgrade=shop?_pilot.DroneUpgrade:_checkpointDroneUpgrade,
            Magnet=shop?_pilot.PickupMagnetLevel:_checkpointMagnet,
            WeaponPowerLevel=shop?_pilot.WeaponPowerLevel:_checkpointWeaponPowerLevel,
            EngineLevel=shop?_pilot.EngineLevel:_checkpointEngineLevel,
            SectorRareCaches=shop?_sectorRareCaches:_checkpointRareCaches,
            ShopRareCaches=_shopRareCaches,
            ShopRank=_shopRank,ShopRankBonus=_shopRankBonus,
            SectorStartScore=shop?_sectorStartScore:_checkpointSectorStartScore,
            SectorCreditsEarned=shop?_sectorCreditsEarned:_checkpointSectorCreditsEarned,
            SectorCreditPickups=shop?_sectorCreditPickups:_checkpointSectorCreditPickups,
            SectorWeaponPickups=shop?_sectorWeaponPickups:_checkpointSectorWeaponPickups,
            SectorSupplyPickups=shop?_sectorSupplyPickups:_checkpointSectorSupplyPickups,
            SectorKills=shop?_sectorKills:_checkpointSectorKills,
            FinalShop=_shopAfterFinalSector,
            ShopScore=_shopScore,ShopEarned=_shopEarned,
            ShopCreditPickups=_shopLootCount,ShopWeaponPickups=_shopWeaponCount,
            ShopSupplyPickups=_shopSupplyCount,ShopKills=_shopKills
        };
        if(RunSaveStore.Write(data)) _hasDiskSave=true;
    }

    private void ContinueSavedRun()
    {
        RunSaveData? state=RunSaveStore.Read();
        if(state is null) { _hasDiskSave=false;return; }
        _restoringRun=true;
        try
        {
            StartNewGame();
            _runDifficulty=(DifficultyMode)state.Difficulty;
            _checkpointSector=state.Sector;
            _checkpointWeaponLevel=state.WeaponLevel;
            _checkpointWeaponMode=(PrimaryWeaponMode)state.PrimaryMode;
            _checkpointShield=state.Shield;
            _checkpointCredits=state.Credits;
            _checkpointUnlockMask=state.OwnedWeapons|1;
            _checkpointLives=state.Lives;
            _checkpointScore=state.Score;
            _checkpointMaxHull=state.MaxHull;
            _checkpointMaxShield=state.MaxShield;
            _checkpointDroneUpgrade=state.DroneUpgrade;
            _checkpointMagnet=state.Magnet;
            _checkpointWeaponPowerLevel=state.WeaponPowerLevel;
            _checkpointEngineLevel=state.EngineLevel;
            _checkpointRareCaches=state.SectorRareCaches;
            _checkpointSectorStartScore=state.SectorStartScore;
            _checkpointSectorCreditsEarned=state.SectorCreditsEarned;
            _checkpointSectorCreditPickups=state.SectorCreditPickups;
            _checkpointSectorWeaponPickups=state.SectorWeaponPickups;
            _checkpointSectorSupplyPickups=state.SectorSupplyPickups;
            _checkpointSectorKills=state.SectorKills;
            _checkpointUnlocked=true;
            ResumeAtCheckpoint();
            _pilot.Shield=Math.Clamp(state.Shield,0,state.MaxShield);
            _pilot.Health=Math.Clamp(state.Health,1,state.MaxHull);
            if(state.Stage=="Shop")
            {
                _mode=GameMode.Shop;
                _pilot.Health=state.Health;
                _shopAfterFinalSector=state.FinalShop;
                _shopTab=0;_shopSelection=1;
                _shopScore=state.ShopScore;_shopEarned=state.ShopEarned;
                _shopLootCount=state.ShopCreditPickups;
                _shopWeaponCount=state.ShopWeaponPickups;
                _shopSupplyCount=state.ShopSupplyPickups;
                _shopKills=state.ShopKills;
                _shopRareCaches=state.ShopRareCaches;
                _shopRank=state.ShopRank;
                _shopRankBonus=state.ShopRankBonus;
                _shopFeedback="PROGRESS RESTORED";
                _shopFeedbackTime=3f;
            }
            else Alert("CHECKPOINT RESTORED",3f);
            _hasDiskSave=true;
        }
        finally { _restoringRun=false; }
    }

    private void UpdateControlsMenu(KeyboardState keys)
    {
        if(_bindingCapture)
        {
            if(MenuBack(keys)) { _bindingCapture=false;_bindingNotice="BINDING CANCELLED";return; }
            foreach(Keys candidate in keys.GetPressedKeys())
            {
                if(_previousKeys.IsKeyDown(candidate)) continue;
                GameAction action=ControlBindings.All[_controlsSelection];
                if(_bindings.TryBind(action,candidate))
                {
                    _bindings.Save();
                    _bindingNotice=action.ToString().ToUpperInvariant()+" : "+candidate;
                    _bindingCapture=false;
                }
                else _bindingNotice="RESERVED OR ALREADY USED - TRY ANOTHER";
                return;
            }
            return;
        }
        if(MenuUp(keys)) _controlsSelection=(_controlsSelection+ControlBindings.All.Length-1)%ControlBindings.All.Length;
        if(MenuDown(keys)) _controlsSelection=(_controlsSelection+1)%ControlBindings.All.Length;
        if(MenuConfirm(keys)) { _bindingCapture=true;_bindingNotice="PRESS A NEW KEY - ESC CANCEL"; }
        if(KeyPressed(keys,Keys.Delete)) { _bindings.RestoreDefaults();_bindings.Save();_bindingNotice="DEFAULT CONTROLS RESTORED"; }
    }
}

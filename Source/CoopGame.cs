using System.Net;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Graphics;

namespace IronNebula;

public sealed partial class IronNebulaGame
{
    private sealed class PartyPilot
    {
        internal int Id;internal string Name="";internal Pilot Pilot=new();
        internal float Bank,BeamTimer,BeamPhase,GatlingPhase;
        internal int BeamTick,PowerIndex,DamageDealt;
        internal bool Beam,Precision,Ready;
        internal Vector2 BeamStart,BeamEnd;
        internal long LastSequence;
        internal long LastWeaponRevision;
    }
    private CoopHost? _coopHost;
    private CoopClient? _coopClient;
    private readonly Dictionary<int,PartyPilot> _party=new();
    private CoopFrame? _receivedCoopFrame;
    private bool _coopMenu,_coopStarted,_coopPause,_coopReady,_coopAddressFocus;
    private float _coopPreviousShotTimer;
    private readonly Dictionary<int,Vector2> _coopRenderPositions=new();
    private int _coopMenuSelection,_coopPauseSelection,_coopActorId;
    private string _coopAddress="127.0.0.1",_coopStatus="",_coopName="PILOT";
    private long _coopSequence,_coopTick;
    private long _coopWeaponRevision;
    private int _coopWeaponChoice=-1;
    private float _coopSendTimer;
    private bool CoopActive=>_coopHost!=null || _coopClient!=null;
    private static readonly Color[] PartyColors={Color.Cyan,Color.LimeGreen,Color.Orange,Color.Violet,Color.Yellow,Color.HotPink};
    private Vector2 CoopSpawnPoint(int id)=>new(250+(id%2)*170,200+(id/2)*300);

    private void OpenCoopMenu()
    {
        _coopMenu=true;_coopMenuSelection=0;_coopStatus="";
        Window.TextInput-=CoopTextInput;Window.TextInput+=CoopTextInput;
    }
    private void CoopTextInput(object? sender,TextInputEventArgs args)
    {
        if(!_coopMenu || !_coopAddressFocus || CoopActive) return;
        if(args.Character=='\b') { if(_coopAddress.Length>0) _coopAddress=_coopAddress[..^1]; }
        else if(_coopAddress.Length<253 && (char.IsAsciiLetterOrDigit(args.Character)||args.Character is '.' or '-' or ':' or '/'))
            _coopAddress+=args.Character;
    }
    private void HostCoop(int port=CoopWire.DefaultPort)
    {
        try
        {
            StopCoop();string room=_coopSmoke?"":System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000,1000000).ToString();
            _coopHost=new CoopHost(port,room,_coopSmoke);_coopMenu=true;_coopStarted=false;
            _party[0]=new PartyPilot { Id=0,Name="HOST",Pilot=_pilot };
            string addresses=string.Join(" / ",Dns.GetHostAddresses(Dns.GetHostName()).Where(a=>a.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork));
            _coopStatus="HOST "+addresses+":"+_coopHost.Port+(room.Length>0?"/"+room:"")+" / UP TO 6 PILOTS";
        }
        catch(Exception ex) when(ex is System.Net.Sockets.SocketException or ArgumentOutOfRangeException)
        { _coopStatus="CANNOT HOST: "+ex.Message; }
    }
    private void JoinCoop()
    {
        if(string.IsNullOrWhiteSpace(_coopAddress)) { _coopStatus="ENTER HOST ADDRESS";return; }
        string address=_coopAddress,room="";int port=CoopWire.DefaultPort;
        int roomSeparator=address.LastIndexOf('/');
        if(roomSeparator>=0) { room=address[(roomSeparator+1)..];address=address[..roomSeparator]; }
        int separator=address.LastIndexOf(':');
        if(separator>0 && int.TryParse(address[(separator+1)..],out int customPort) && customPort is >0 and <65536)
        { port=customPort;address=address[..separator]; }
        StopCoop();_coopMenu=true;
        _coopClient=new CoopClient(address,port,_coopName,room);
    }
    private void StopCoop()
    {
        bool wasActive=CoopActive;
        _coopHost?.Dispose();_coopClient?.Dispose();_coopHost=null;_coopClient=null;
        _party.Clear();_receivedCoopFrame=null;_coopStarted=false;_coopPause=false;
        _coopRenderPositions.Clear();_coopPreviousShotTimer=0;
        _coopReady=false;_coopSendTimer=0;_coopSequence=0;_coopTick=0;
        _coopWeaponRevision=0;_coopWeaponChoice=-1;
        StopGamePadFeedback();_audio?.Beam(false);
        if(wasActive) { _checkpointUnlocked=false;_hasDiskSave=RunSaveStore.Read()!=null;_trainingRun=false;_pilot=new Pilot(); }
    }
    private void StartCoopRun()
    {
        if(_coopHost==null || _coopHost.Seats.Length<1) { _coopStatus="WAITING FOR AT LEAST ONE GUEST";return; }
        _coopHost.InLobby=false;
        _restoringRun=true;
        try { StartNewGame(); } finally { _restoringRun=false; }
        _trainingRun=_coopSmoke;_coopStarted=true;_coopMenu=false;_coopHost.InLobby=false;
        _party.Clear();_party[0]=new PartyPilot { Id=0,Name="HOST",Pilot=_pilot };
        foreach(var seat in _coopHost.Seats) _party[seat.Id]=new PartyPilot { Id=seat.Id,Name=seat.Name };
        foreach(var actor in _party.Values) { actor.Pilot.Position=CoopSpawnPoint(actor.Id);actor.Pilot.Invincible=3; }
        Alert("CO-OP FLIGHT / "+_party.Count+" PILOTS",3);
    }
    private CoopInput ReadCoopInput(KeyboardState keys,bool blocked)
    {
        bool ready=_coopReady;
        if(blocked) return new CoopInput(++_coopSequence,Ready:ready);
        Vector2 direction=FlightDirection(keys);
        int delta=ActionPressed(keys,GameAction.WeaponNext)?1:ActionPressed(keys,GameAction.WeaponPrevious)?-1:0;
        int direct=ActionPressed(keys,GameAction.Pulse)?0:ActionPressed(keys,GameAction.Lance)?1:
            ActionPressed(keys,GameAction.Arc)?2:ActionPressed(keys,GameAction.Gatling)?3:ActionPressed(keys,GameAction.Beam)?4:-1;
        if(direct>=0 && OwnsWeapon((PrimaryWeaponMode)direct)) { _coopWeaponChoice=direct;_coopWeaponRevision++; }
        else if(delta!=0)
        {
            int candidate=_coopWeaponChoice>=0?_coopWeaponChoice:(int)_pilot.PrimaryMode;
            for(int i=0;i<5;i++) { candidate=(candidate+delta+5)%5;if(OwnsWeapon((PrimaryWeaponMode)candidate)) break; }
            _coopWeaponChoice=candidate;_coopWeaponRevision++;
        }
        bool target=_mouseFlightActive && _mouseValid && direction.LengthSquared()<.0001f;
        return new CoopInput(++_coopSequence,direction.X,direction.Y,ActionDown(keys,GameAction.Fire)||MouseFire,
            ActionDown(keys,GameAction.Precision)||MousePrecision,delta,ready,target,
            target?_mouseTarget.X:0,target?_mouseTarget.Y:0,_coopWeaponRevision,_coopWeaponChoice);
    }
    private bool UpdateCoop(float dt,KeyboardState keys)
    {
        if(!CoopActive && !_coopMenu) return false;
        if(!CoopActive)
        {
            if(MenuBack(keys)) { _coopMenu=false;_coopAddressFocus=false;return true; }
            if(MenuUp(keys)) _coopMenuSelection=(_coopMenuSelection+2)%3;
            if(MenuDown(keys)) _coopMenuSelection=(_coopMenuSelection+1)%3;
            if(_mouseClick)
            {
                if(MouseHit(new Rectangle(350,300,1220,100))) { _coopAddressFocus=true;_coopMenuSelection=1;return true; }
                for(int i=0;i<3;i++) if(MouseHit(new Rectangle(500,465+i*130,920,110))) { _coopMenuSelection=i;ActivateCoopMenu();return true; }
            }
            if(MenuConfirm(keys)) ActivateCoopMenu();
            return true;
        }
        bool localBlocked=!IsActive || _coopPause || _settingsOpen || _helpOpen;
        if(IsActive)
        {
            if(KeyPressed(keys,Keys.F11)) SetScreenMode();
            if(KeyPressed(keys,Keys.F10)) _performanceOverlay=!_performanceOverlay;
            if(PadPressed(Buttons.Y)||KeyPressed(keys,Keys.O)||KeyPressed(keys,Keys.F12)) { _settingsOpen=!_settingsOpen;_helpOpen=false; }
            if(PadPressed(Buttons.Back)||KeyPressed(keys,Keys.F1)) { _helpOpen=!_helpOpen;_settingsOpen=false; }
            if(_helpOpen || _settingsOpen)
            {
                if(MenuBack(keys)) { _helpOpen=_settingsOpen=false;_bindingCapture=false; }
                else if(!HandleModalMouseInput())
                {
                    if(_helpOpen) { if(MenuLeft(keys)) _helpPage=(_helpPage+3)%4;if(MenuRight(keys)) _helpPage=(_helpPage+1)%4; }
                    else if(_resolutionDropdownOpen && _settingsTab==0) UpdateResolutionDropdown(keys);
                    else if(!_bindingCapture && (KeyPressed(keys,Keys.Tab)||PadPressed(Buttons.RightShoulder))) _settingsTab=(_settingsTab+1)%3;
                    else if(_settingsTab==0) UpdateAudioSettings(keys);
                    else if(_settingsTab==1) UpdateControlsMenu(keys);
                    else UpdateControllerSettings(keys);
                }
            }
            else if(_coopPause)
            {
                if(MenuBack(keys)||ActionPressed(keys,GameAction.Pause)) _coopPause=false;
                else
                {
                    if(MenuUp(keys)) _coopPauseSelection=(_coopPauseSelection+2)%3;
                    if(MenuDown(keys)) _coopPauseSelection=(_coopPauseSelection+1)%3;
                    bool activate=MenuConfirm(keys);
                    for(int i=0;i<3;i++) if(MouseHit(new Rectangle(500,410+i*140,920,115))) { _coopPauseSelection=i;activate=true; }
                    if(activate)
                    {
                        if(_coopPauseSelection==0) _coopPause=false;
                        else if(_coopPauseSelection==1) _settingsOpen=true;
                        else { StopCoop();_coopMenu=false;_mode=GameMode.Title;return true; }
                    }
                }
            }
            else if(MenuBack(keys)||(_mode!=GameMode.Shop && _coopStarted && ActionPressed(keys,GameAction.Pause))) _coopPause=true;
            if(_mode==GameMode.Shop && (KeyPressed(keys,Keys.C)||PadPressed(Buttons.Start))) _coopReady=!_coopReady;
        }
        localBlocked|=_coopPause || _settingsOpen || _helpOpen || !_coopStarted;
        if(localBlocked || _pilot.Lives<=0) { StopGamePadFeedback();_audio?.Beam(false); }
        var input=ReadCoopInput(keys,localBlocked);
        if(_coopSmoke || _coopVisualTest) input=new CoopInput(++_coopSequence,0,(float)Math.Sin(_coopSequence*.01)*.12f,true,Ready:_mode==GameMode.Shop);
        _coopSendTimer-=dt;
        if(_coopClient!=null)
        {
            if(_mode==GameMode.Shop && !localBlocked && MouseHit(new Rectangle(1636,990,264,78))) _coopReady=!_coopReady;
            // Send edges immediately; periodic neutral input is also the heartbeat.
            if(_coopSendTimer<=0 || input.WeaponDelta!=0)
            { _coopClient.Input(input);_coopSendTimer=1f/30; }
            if(_coopClient.Latest is { } frame && frame!=_receivedCoopFrame)
            { ApplyCoopFrame(frame);_receivedCoopFrame=frame; }
            TickCoopVisualSmoke();
            if(_coopStarted && _mode==GameMode.Playing && _pilot.Lives>0)
            {
                if(!localBlocked)
                {
                    var direction=new Vector2(input.X,input.Y);if(direction.LengthSquared()>1) direction.Normalize();
                    if(input.HasTarget)
                    {
                        float spring=input.Precision?12:18;var target=new Vector2(input.TargetX,input.TargetY);
                        Vector2 offset=_pilot.Position-target,impulse=_pilot.Velocity+spring*offset;
                        float decay=MathF.Exp(-spring*dt);
                        _pilot.Position=target+(offset+impulse*dt)*decay;
                        _pilot.Velocity=(_pilot.Velocity-spring*impulse*dt)*decay;
                    }
                    else
                    {
                        float speed=(input.Precision?260:550)*(1+.08f*_pilot.EngineLevel);
                        _pilot.Velocity=Vector2.Lerp(_pilot.Velocity,direction*speed,1-MathF.Exp(-dt*(direction==Vector2.Zero?14:9)));
                        _pilot.Position+=_pilot.Velocity*dt;
                    }
                    _pilot.Position=new Vector2(Math.Clamp(_pilot.Position.X,105,ViewWidth-145),Math.Clamp(_pilot.Position.Y,110,ViewHeight-115));
                }
                _precisionFire=input.Precision;_beamPhase+=dt*12;
                _audio?.Beam(_beamActive && !localBlocked);
            }
            if(_coopClient.Disconnected) { _coopStatus=_coopClient.Status;StopCoop();_coopMenu=true;_mode=GameMode.Title; }
            UpdateBackground(dt,_coopStarted?1:.2f);
            return true;
        }
        var host=_coopHost!;
        if(!_coopStarted)
        {
            // Lobby confirmation remains available even though flight input is neutral.
            if(IsActive && !_coopPause && !_settingsOpen && !_helpOpen && MenuConfirm(keys)) StartCoopRun();
            RunCoopSmokeLobby();
        }
        else if(_mode==GameMode.Playing)
        {
            SimulateCoop(dt,input,host.Seats);
            _audio?.Beam(_party.TryGetValue(0,out var localActor) && localActor.Beam && localActor.Pilot.Lives>0 && !localBlocked);
        }
        else if(_mode==GameMode.Shop)
        {
            if(_coopSmoke) { _coopReady=true;_coopSmokeShopTicks++; }
            if(!localBlocked) UpdateShop(dt,keys);
            ApplyCoopTeamUpgrades();
            if(_coopReady && host.Seats.All(s=>s.Input.Ready)) AdvanceCoopShop();
        }
        else if(_mode is GameMode.GameOver or GameMode.Victory && IsActive && !_coopPause && MenuConfirm(keys))
        { _coopStarted=false;_coopMenu=true;host.InLobby=true;_mode=GameMode.Title;_coopReady=false; }
        RunCoopSmokeFinish();
        if(_coopSendTimer<=0)
        { host.Publish(CaptureCoopFrame());_coopSendTimer=1f/20; }
        return true;
    }
    private void ActivateCoopMenu()
    {
        _coopAddressFocus=false;
        if(_coopMenuSelection==0) HostCoop();
        else if(_coopMenuSelection==1) JoinCoop();
        else _coopMenu=false;
    }
    private void UsePartyPilot(PartyPilot actor,Action work)
    {
        var oldPilot=_pilot;float bank=_pilotBank,timer=_beamDamageTimer,phase=_beamPhase,gatling=_gatlingPhase;
        int tick=_beamTickCount,power=_powerShotIndex,oldId=_coopActorId;bool beam=_beamActive,precision=_precisionFire;
        var start=_beamStart;var end=_beamEnd;
        _pilot=actor.Pilot;_pilotBank=actor.Bank;_beamDamageTimer=actor.BeamTimer;_beamPhase=actor.BeamPhase;
        _gatlingPhase=actor.GatlingPhase;_beamTickCount=actor.BeamTick;_powerShotIndex=actor.PowerIndex;
        _beamActive=actor.Beam;_precisionFire=actor.Precision;_beamStart=actor.BeamStart;_beamEnd=actor.BeamEnd;_coopActorId=actor.Id;
        try { work(); }
        finally
        {
            actor.Bank=_pilotBank;actor.BeamTimer=_beamDamageTimer;actor.BeamPhase=_beamPhase;
            actor.GatlingPhase=_gatlingPhase;actor.BeamTick=_beamTickCount;actor.PowerIndex=_powerShotIndex;
            actor.Beam=_beamActive;actor.Precision=_precisionFire;actor.BeamStart=_beamStart;actor.BeamEnd=_beamEnd;
            _pilot=oldPilot;_pilotBank=bank;_beamDamageTimer=timer;_beamPhase=phase;_gatlingPhase=gatling;
            _beamTickCount=tick;_powerShotIndex=power;_beamActive=beam;_precisionFire=precision;_beamStart=start;_beamEnd=end;_coopActorId=oldId;
        }
    }
    private void SimulateCoop(float dt,CoopInput local,CoopSeat[] seats)
    {
        foreach(int id in _party.Keys.Where(id=>id!=0 && !seats.Any(s=>s.Id==id)).ToArray()) _party.Remove(id);
        _coopTick++;_elapsed+=dt;_waveClock-=dt;UpdateBackground(dt);
        _shake=Math.Max(0,_shake-dt*27);_flash=Math.Max(0,_flash-dt*3);_messageTime=Math.Max(0,_messageTime-dt);
        _formationNoticeTime=Math.Max(0,_formationNoticeTime-dt);
        foreach(var actor in _party.Values.ToArray())
        {
            var seat=seats.FirstOrDefault(s=>s.Id==actor.Id);
            var command=actor.Id==0?local:seat!=null && Environment.TickCount64-seat.LastInput<250?seat.Input:new CoopInput();
            actor.Ready=command.Ready;
            if(actor.Pilot.Lives<=0) { actor.Beam=false;continue; }
            UsePartyPilot(actor,()=>SimulateCoopPilot(dt,command,actor));
        }
        var living=_party.Values.Where(a=>a.Pilot.Lives>0).ToArray();
        if(living.Length==0) { FinishRun(false);return; }
        // Rotate the authoritative target; every client receives the resulting attack.
        _pilot=living[(int)(_coopTick/180%living.Length)].Pilot;
        try
        {
            UpdateSectorDirector(dt);UpdateDebris(dt);UpdateEnemies(dt);UpdateShots(dt);UpdateLaserLanes(dt);
            UpdatePickups(dt);UpdateParticles(dt);UpdateCombatFx(dt);UpdatePrimaryImpacts(dt);UpdatePrimaryReadouts(dt);
            UpdateExplosionSprites(dt);UpdateBossBreaches(dt);UpdateDamageSmokeSprites(dt);UpdateEnemyDamageSmoke(dt);
            UpdateEnemySmokeSprites(dt);UpdateScheduledDeathBursts(dt);UpdateBossFinale(dt);UpdateShockwaves(dt);
        }
        finally { _pilot=_party[0].Pilot; }
        _pilotBank=_party[0].Bank;_beamActive=_party[0].Beam;_beamStart=_party[0].BeamStart;_beamEnd=_party[0].BeamEnd;
        RunCoopSmokeWorld();
    }
    private void SimulateCoopPilot(float dt,CoopInput input,PartyPilot actor)
    {
        _pilot.Invincible=Math.Max(0,_pilot.Invincible-dt);
        if(input.WeaponRevision>actor.LastWeaponRevision && input.WeaponChoice>=0)
        { SetPrimaryWeapon((PrimaryWeaponMode)input.WeaponChoice);actor.LastWeaponRevision=input.WeaponRevision; }
        else if(input.WeaponRevision==0 && input.Sequence>actor.LastSequence && input.WeaponDelta!=0) CycleOwnedWeapon(input.WeaponDelta);
        actor.LastSequence=Math.Max(actor.LastSequence,input.Sequence);
        _precisionFire=input.Precision;
        Vector2 direction=new(input.X,input.Y);if(direction.LengthSquared()>1) direction.Normalize();
        if(input.HasTarget)
        {
            float spring=_precisionFire?12:18;Vector2 offset=_pilot.Position-new Vector2(input.TargetX,input.TargetY);
            Vector2 impulse=_pilot.Velocity+spring*offset;float decay=MathF.Exp(-spring*dt);
            _pilot.Position=new Vector2(input.TargetX,input.TargetY)+(offset+impulse*dt)*decay;
            _pilot.Velocity=(_pilot.Velocity-spring*impulse*dt)*decay;
        }
        else
        {
            float speed=(_precisionFire?260:550)*(1+.08f*_pilot.EngineLevel);
            _pilot.Velocity=Vector2.Lerp(_pilot.Velocity,direction*speed,1-MathF.Exp(-dt*(direction==Vector2.Zero?14:9)));
            _pilot.Position+=_pilot.Velocity*dt;
        }
        _pilot.Position=new Vector2(Math.Clamp(_pilot.Position.X,105,ViewWidth-145),Math.Clamp(_pilot.Position.Y,110,ViewHeight-115));
        _pilotBank=MathHelper.Lerp(_pilotBank,Math.Clamp(_pilot.Velocity.Y/550,-1,1),Math.Min(1,dt*6));
        _pilot.ShotTimer-=dt;_beamActive=input.Fire && _pilot.PrimaryMode==PrimaryWeaponMode.Beam && _bossFinaleTimer<=0;
        _beamPhase+=dt*(9+_pilot.WeaponLevel*1.8f);
        if(_beamActive) { _beamDamageTimer-=dt;UpdateContinuousBeam(dt); }
        else _beamDamageTimer=0;
        if(input.Fire && !_beamActive && _pilot.ShotTimer<=0 && _bossFinaleTimer<=0)
        {
            FirePlayerWeapon();float t=Math.Clamp((_pilot.WeaponLevel-1)/6f,0,1);
            _pilot.ShotTimer=_pilot.PrimaryMode switch { PrimaryWeaponMode.Lance=>MathHelper.Lerp(.31f,.41f,t),
                PrimaryWeaponMode.Arc=>MathHelper.Lerp(.21f,.265f,t),PrimaryWeaponMode.Gatling=>MathHelper.Lerp(.120f,.045f,t),_=>MathHelper.Lerp(.165f,.238f,t) };
        }
        UpdateDrone(dt);
    }
    private bool HurtCoopCircle(Vector2 position,float radius)
    {
        if(!CoopActive)
        {
            if(_pilot.Invincible>0 || Vector2.DistanceSquared(position,_pilot.Position)>=MathF.Pow(radius+_pilot.Radius,2)) return false;
            HurtPilot();return true;
        }
        foreach(var actor in _party.Values)
            if(actor.Pilot.Lives>0 && actor.Pilot.Invincible<=0 && Vector2.DistanceSquared(position,actor.Pilot.Position)<MathF.Pow(radius+actor.Pilot.Radius,2))
            { UsePartyPilot(actor,HurtPilot);return true; }
        return false;
    }
    private bool HurtCoopShot(Projectile shot)
    {
        foreach(var actor in _party.Values)
            if(actor.Pilot.Lives>0 && actor.Pilot.Invincible<=0 && SegmentDistanceSquared(actor.Pilot.Position,shot.PreviousPosition,shot.Position)<MathF.Pow(shot.Radius+actor.Pilot.Radius,2))
            { UsePartyPilot(actor,HurtPilot);return true; }
        return false;
    }
    private void HurtCoopLane(float y)
    {
        if(!CoopActive) { if(_pilot.Invincible<=0 && Math.Abs(_pilot.Position.Y-y)<54) HurtPilot();return; }
        foreach(var actor in _party.Values) if(actor.Pilot.Lives>0 && actor.Pilot.Invincible<=0 && Math.Abs(actor.Pilot.Position.Y-y)<54) UsePartyPilot(actor,HurtPilot);
    }
    private void SelectCoopPickupPilot(Vector2 location)
    {
        if(_coopHost!=null)
        {
            var nearest=_party.Values.Where(a=>a.Pilot.Lives>0).MinBy(a=>Vector2.DistanceSquared(a.Pilot.Position,location));
            if(nearest!=null) _pilot=nearest.Pilot;
        }
    }
    private void CoopRecordDamage(Projectile shot,int damage)
    { if(_coopHost!=null && _party.TryGetValue(shot.OwnerPlayerId,out var actor)) actor.DamageDealt+=damage; }
    private void ApplyCoopTeamUpgrades()
    {
        var host=_party[0].Pilot;
        foreach(var actor in _party.Values.Where(a=>a.Id!=0))
        {
            actor.Pilot.MaxHealth=host.MaxHealth;actor.Pilot.MaxShield=host.MaxShield;
            actor.Pilot.EngineLevel=host.EngineLevel;actor.Pilot.WeaponPowerLevel=host.WeaponPowerLevel;
            actor.Pilot.DroneUpgrade=host.DroneUpgrade;actor.Pilot.PickupMagnetLevel=host.PickupMagnetLevel;
        }
    }
    private void AdvanceCoopShop()
    {
        _coopReady=false;foreach(var actor in _party.Values) actor.Ready=false;
        if(_shopAfterFinalSector && _infernoActive) FinishRun(true);
        else
        {
            foreach(var actor in _party.Values)
            {
                if(actor.Pilot.Lives<=0) { actor.Pilot.Lives=1;actor.Pilot.Health=actor.Pilot.MaxHealth; }
                actor.Pilot.Position=CoopSpawnPoint(actor.Id);actor.Pilot.Velocity=Vector2.Zero;actor.Pilot.Invincible=3;
            }
            _mode=GameMode.Playing;if(_shopAfterFinalSector) BeginInfernoSector();else BeginRiftSector();
        }
    }
    private CoopFrame CaptureCoopFrame()=>new() {
        Tick=_coopTick,Mode=_coopStarted?_mode.ToString():"Lobby",Score=_score,Credits=_credits,Weapons=_ownedWeaponsMask,
        Sector=_infernoActive?3:_riftActive?2:1,RiftBossStage=_riftBossStage,InfernoBossStage=_infernoBossStage,
        Selection=_shopSelection,ShopTab=_shopTab,FinalShop=_shopAfterFinalSector,
        InfernoPhase=_infernoPhase,SpawnCount=_spawnCount,SectorZone=_sectorZone,RiftWaveCount=_riftWaveCount,
        InfernoWaveCount=_infernoWaveCount,Difficulty=(int)_runDifficulty,ShopRank=_shopRank,
        ShopStats=new[]{_shopScore,_shopEarned,_shopLootCount,_shopWeaponCount,_shopSupplyCount,_shopKills,_shopRankBonus},
        Travel=_travel,Elapsed=_elapsed,RiftElapsed=_riftElapsed,InfernoElapsed=_infernoElapsed,
        Players=(_coopStarted?_party.Values.Select(a=>new CoopPilotFrame { Id=a.Id,Name=a.Name,Pilot=a.Pilot,Bank=a.Bank,
            Beam=a.Beam,BeamStart=a.BeamStart,BeamEnd=a.BeamEnd,
            Ready=a.Id==0?_coopReady:_coopHost!.Seats.FirstOrDefault(s=>s.Id==a.Id)?.Input.Ready??false,DamageDealt=a.DamageDealt }):
            new[]{new CoopPilotFrame { Id=0,Name="HOST" }}.Concat(_coopHost!.Seats.Select(s=>new CoopPilotFrame { Id=s.Id,Name=s.Name,Ready=s.Input.Ready }))).ToArray(),
        Enemies=_enemies.ToArray(),Parents=_enemies.Select(e=>e.Parent==null?-1:_enemies.IndexOf(e.Parent)).ToArray(),
        Shots=_shots.ToArray(),Debris=_debris.ToArray(),Pickups=_pickups.ToArray(),Lasers=_laserLanes.ToArray(),
        Explosions=_explosionSprites.ToArray(),Shockwaves=_shockwaves.ToArray()
    };
    private void ApplyCoopFrame(CoopFrame frame)
    {
        if(frame.Mode!="Shop" && _mode==GameMode.Shop || frame.Mode=="Lobby") _coopReady=false;
        if(frame.Mode=="Lobby") { _coopWeaponChoice=-1;_coopWeaponRevision=0; }
        _coopStarted=frame.Mode!="Lobby";_coopMenu=!_coopStarted;
        if(Enum.TryParse<GameMode>(frame.Mode,out var mode)) _mode=mode;
        else _mode=GameMode.Title;
        var own=frame.Players.FirstOrDefault(p=>p.Id==_coopClient!.Id);
        if(own!=null)
        {
            if(_coopStarted && (own.Pilot.Health<_pilot.Health || own.Pilot.Shield<_pilot.Shield)) PulseGamePad(.7f,.8f);
            Vector2 previous=_pilot.Position;
            _pilot=JsonSerializer.Deserialize<Pilot>(JsonSerializer.Serialize(own.Pilot,CoopWire.Json),CoopWire.Json)!;
            if(_receivedCoopFrame?.Mode=="Playing" && frame.Mode=="Playing" && Vector2.DistanceSquared(previous,_pilot.Position)<300*300)
                _pilot.Position=Vector2.Lerp(previous,_pilot.Position,.4f);
            if(own.Pilot.ShotTimer>_coopPreviousShotTimer+.01f && _coopStarted)
            {
                PulseGamePad(.08f,.2f);
                _audio?.Effect(own.Pilot.PrimaryMode==PrimaryWeaponMode.Lance?"lance":own.Pilot.PrimaryMode==PrimaryWeaponMode.Arc?"arc":"pulse",.05f,.3f);
            }
            _coopPreviousShotTimer=own.Pilot.ShotTimer;
            _pilotBank=own.Bank;_beamActive=own.Beam;_beamStart=own.BeamStart;_beamEnd=own.BeamEnd;
        }
        _score=frame.Score;_credits=frame.Credits;_ownedWeaponsMask=frame.Weapons;
        _riftActive=frame.Sector==2;_infernoActive=frame.Sector==3;_riftBossStage=frame.RiftBossStage;_infernoBossStage=frame.InfernoBossStage;
        _infernoPhase=frame.InfernoPhase;_infernoPhaseFade=1;_spawnCount=frame.SpawnCount;_sectorZone=frame.SectorZone;
        _riftWaveCount=frame.RiftWaveCount;_infernoWaveCount=frame.InfernoWaveCount;_runDifficulty=(DifficultyMode)Math.Clamp(frame.Difficulty,0,2);
        _elapsed=frame.Elapsed;_travel=frame.Travel;_riftElapsed=frame.RiftElapsed;_infernoElapsed=frame.InfernoElapsed;
        _scroll=frame.Travel*132;_riftFade=_infernoFade=1;
        _shopSelection=frame.Selection;_shopTab=frame.ShopTab;_shopAfterFinalSector=frame.FinalShop;
        _shopRank=frame.ShopRank;
        if(frame.ShopStats.Length==7)
        { _shopScore=frame.ShopStats[0];_shopEarned=frame.ShopStats[1];_shopLootCount=frame.ShopStats[2];_shopWeaponCount=frame.ShopStats[3];_shopSupplyCount=frame.ShopStats[4];_shopKills=frame.ShopStats[5];_shopRankBonus=frame.ShopStats[6]; }
        _enemies.Clear();_enemies.AddRange(frame.Enemies);
        for(int i=0;i<_enemies.Count && i<frame.Parents.Length;i++)
            if(frame.Parents[i]>=0 && frame.Parents[i]<_enemies.Count) _enemies[i].Parent=_enemies[frame.Parents[i]];
        _shots.Clear();_shots.AddRange(frame.Shots);_debris.Clear();_debris.AddRange(frame.Debris);
        _pickups.Clear();_pickups.AddRange(frame.Pickups);_laserLanes.Clear();_laserLanes.AddRange(frame.Lasers);
        _explosionSprites.Clear();_explosionSprites.AddRange(frame.Explosions);_shockwaves.Clear();_shockwaves.AddRange(frame.Shockwaves);
    }
}

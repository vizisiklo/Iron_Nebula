using System.Text.Json;
using Microsoft.Xna.Framework;

namespace IronNebula;

// Explicit command-line verification mode. Never enabled during a normal run.
public sealed partial class IronNebulaGame
{
    private bool _coopSmoke;
    private bool _coopVisualTest,_coopVisualCaptured;
    private int _coopVisualFrames,_coopVisualMaxPlayers;
    private readonly HashSet<int> _coopSmokeSectors=new();
    private readonly HashSet<EnemyKind> _coopSmokeBosses=new();
    private int _coopSmokeExpected=6;
    private string? _coopSmokeLog;
    private long _coopSmokeStarted;
    private int _coopSmokeTicks;
    private int _coopSmokeShopTicks;
    private long _coopSmokeFinishAt;
    private readonly HashSet<int> _coopSmokeOwners=new();
    private readonly HashSet<PrimaryWeaponMode> _coopSmokeWeapons=new();
    private void InitializeCoopArguments()
    {
        string[] args=Environment.GetCommandLineArgs();
        string Value(string key,string fallback) { int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback; }
        _coopSmoke=args.Contains("--coop-smoke");
        _coopVisualTest=args.Contains("--coop-visual-client");
        if(!_coopSmoke && !_coopVisualTest) return;
        _coopSmokeLog=Value("--coop-log",Path.Combine(AppContext.BaseDirectory,"coop-smoke-host.json"));
        _coopSmokeExpected=int.Parse(Value("--coop-players","6"));
        int port=int.Parse(Value("--coop-port",CoopWire.DefaultPort.ToString()));
        _coopSmokeStarted=Environment.TickCount64;
        if(_coopVisualTest) { _coopMenu=true;_coopClient=new CoopClient("127.0.0.1",port,"RENDER CLIENT"); }
        else HostCoop(port);
    }
    private void RunCoopSmokeLobby()
    {
        if(!_coopSmoke) return;
        if(Environment.TickCount64-_coopSmokeStarted>45000) { WriteCoopSmokeResult(false,"Waiting for guests timed out");Exit();return; }
        if(_coopHost!.Seats.Length+1>=_coopSmokeExpected)
        {
            StartCoopRun();_ownedWeaponsMask=31;
            foreach(var actor in _party.Values) { actor.Pilot.WeaponLevel=7;actor.Pilot.MaxHealth=100;actor.Pilot.Health=100; }
        }
    }
    private void RunCoopSmokeWorld()
    {
        if(!_coopSmoke) return;
        _coopSmokeTicks++;
        _coopSmokeSectors.Add(_infernoActive?3:_riftActive?2:1);
        foreach(var boss in _enemies.Where(e=>e.Kind is EnemyKind.RiftGuardian or EnemyKind.InfernoGuardian)) _coopSmokeBosses.Add(boss.Kind);
        if(_coopSmokeTicks==320) BeginRiftSector();
        if(_coopSmokeTicks==400) SpawnRiftBoss(false);
        if(_coopSmokeTicks==640) BeginInfernoSector();
        if(_coopSmokeTicks==720) SpawnInfernoBoss(false);
        if(_coopSmokeTicks==180) foreach(var actor in _party.Values) UsePartyPilot(actor,()=> { _pilot.Invincible=0;HurtPilot(); });
        foreach(var actor in _party.Values)
        {
            if(_coopSmokeTicks%120==1) actor.Pilot.PrimaryMode=(PrimaryWeaponMode)((_coopSmokeTicks/120+actor.Id)%5);
            // Stationary targets exercise the actual projectile/beam collision code.
            if(!_enemies.Any(e=>e.BaseY==actor.Id+10000))
                _enemies.Add(new Enemy { Kind=EnemyKind.Turret,Position=new Vector2(1400,actor.Pilot.Position.Y),
                    BaseY=actor.Id+10000,Health=10000,MaxHealth=10000,Radius=80,FireTimer=1000,DifficultyApplied=true });
        }
        foreach(var shot in _shots.Where(s=>s.Friendly)) { _coopSmokeOwners.Add(shot.OwnerPlayerId);_coopSmokeWeapons.Add(shot.PrimaryMode); }
        foreach(var actor in _party.Values.Where(a=>a.Beam)) _coopSmokeWeapons.Add(PrimaryWeaponMode.Beam);
        if(_coopSmokeTicks==1200) OpenSectorShop(true);
    }
    private void RunCoopSmokeFinish()
    {
        if(!_coopSmoke || _mode!=GameMode.Victory) return;
        if(_coopSmokeFinishAt==0) _coopSmokeFinishAt=Environment.TickCount64;
        if(Environment.TickCount64-_coopSmokeFinishAt<500) return;
        WriteCoopSmokeResult(_party.Count==_coopSmokeExpected && _coopSmokeOwners.Count>=_coopSmokeExpected && _coopSmokeWeapons.Count==5 &&
            _party.Values.All(p=>p.DamageDealt>0) && _coopSmokeSectors.Count==3 && _coopSmokeBosses.Count==2 && _coopSmokeShopTicks>1,
            "Completed actual game simulation and all-ready shop/victory barrier");Exit();
    }
    private void WriteCoopSmokeResult(bool success,string reason)
    {
        if(_coopSmokeLog==null) return;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(_coopSmokeLog))!);
        File.WriteAllText(_coopSmokeLog,JsonSerializer.Serialize(new { success,reason,ticks=_coopSmokeTicks,
            players=_party.Count,owners=_coopSmokeOwners.Order().ToArray(),weapons=_coopSmokeWeapons.Select(w=>w.ToString()).ToArray(),
            damage=_party.Values.ToDictionary(p=>p.Id,p=>p.DamageDealt),
            sectors=_coopSmokeSectors.Order().ToArray(),bosses=_coopSmokeBosses.Select(b=>b.ToString()).ToArray(),
            shopTicks=_coopSmokeShopTicks,mode=_mode.ToString(),
            positions=_party.Values.Select(p=>new { p.Id,p.Pilot.Position.X,p.Pilot.Position.Y }).ToArray() }));
    }
    private void TickCoopVisualSmoke()
    {
        if(!_coopVisualTest) return;
        if(_receivedCoopFrame is { } frame) { _coopVisualFrames++;_coopVisualMaxPlayers=Math.Max(_coopVisualMaxPlayers,frame.Players.Length); }
        if(_coopClient?.Disconnected==true || Environment.TickCount64-_coopSmokeStarted>50000)
        {
            File.WriteAllText(_coopSmokeLog!,JsonSerializer.Serialize(new { success=_coopVisualCaptured && _coopVisualFrames>30 && _coopVisualMaxPlayers==6,
                rendered=true,frames=_coopVisualFrames,maxPlayers=_coopVisualMaxPlayers,screenshot=_coopSmokeLog+".png" }));
            Exit();
        }
    }
    private void CaptureCoopVisualSmoke()
    {
        if((!_coopVisualTest && !_coopSmoke) || _coopVisualCaptured || _receivedCoopFrame?.Tick<250 || !_coopStarted) return;
        if(_coopSmoke && _coopTick<250) return;
        using var image=File.Create(_coopSmokeLog+".png");
        _scene.SaveAsPng(image,ViewWidth,ViewHeight);_coopVisualCaptured=true;
    }
}

internal static class CoopSmokeClient
{
    internal static async Task<int> Run(string[] args)
    {
        string Value(string key,string fallback) { int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback; }
        using var client=new CoopClient(Value("--address","127.0.0.1"),int.Parse(Value("--coop-port","28771")),Value("--name","TEST PILOT"));
        string log=Value("--coop-log","coop-bot.json");long started=Environment.TickCount64,sequence=0;
        CoopFrame? previousFrame=null;
        int frames=0,maxPlayers=0;bool moved=false,fired=false,shop=false,victory=false;Vector2? original=null;
        while(Environment.TickCount64-started<40000 && !client.Disconnected)
        {
            double t=(Environment.TickCount64-started)/1000d;
            client.Input(new CoopInput(++sequence,0,(float)Math.Sin(t)*.12f,true,Ready:client.Latest?.Mode=="Shop"));
            if(client.Latest is { } frame && frame!=previousFrame)
            {
                previousFrame=frame;frames++;maxPlayers=Math.Max(maxPlayers,frame.Players.Length);
                shop|=frame.Mode=="Shop";victory|=frame.Mode=="Victory";
                var own=frame.Players.FirstOrDefault(p=>p.Id==client.Id);
                if(frame.Mode=="Playing" && own!=null)
                {
                    original??=own.Pilot.Position;
                    moved|=Vector2.DistanceSquared(original.Value,own.Pilot.Position)>25;
                    fired|=own.Beam || frame.Shots.Any(s=>s.OwnerPlayerId==client.Id && s.Friendly);
                }
            }
            await Task.Delay(34);
        }
        bool success=frames>30 && maxPlayers==6 && moved && fired;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(log))!);
        File.WriteAllText(log,JsonSerializer.Serialize(new { success,client.Id,frames,maxPlayers,moved,fired,shop,victory,status=client.Status }));
        return success?0:1;
    }
}

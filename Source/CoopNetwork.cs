using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Xna.Framework;

namespace IronNebula;

internal sealed record CoopInput(long Sequence=0,float X=0,float Y=0,bool Fire=false,bool Precision=false,
    int WeaponDelta=0,bool Ready=false,bool HasTarget=false,float TargetX=0,float TargetY=0,long WeaponRevision=0,int WeaponChoice=-1);
internal sealed record CoopSeat(int Id,string Name,CoopInput Input,long LastInput);
internal sealed class CoopPacket
{
    public int Version { get; set; }=1;
    public string Kind { get; set; }="";
    public string Name { get; set; }="PILOT";
    public string Room { get; set; }="";
    public int Id { get; set; }=-1;
    public string Error { get; set; }="";
    public CoopInput? Input { get; set; }
    public CoopFrame? Frame { get; set; }
}
internal sealed class CoopPilotFrame
{
    public int Id;
    public string Name="";
    public Pilot Pilot=new();
    public float Bank;
    public bool Beam;
    public Vector2 BeamStart,BeamEnd;
    public bool Ready;
    public int DamageDealt;
}
internal sealed class CoopFrame
{
    public long Tick;
    public string Mode="Lobby";
    public int Score,Credits,Weapons=1,Sector=1,RiftBossStage,InfernoBossStage,Selection,ShopTab;
    public int InfernoPhase,SpawnCount,SectorZone,RiftWaveCount,InfernoWaveCount,Difficulty=1;
    public int[] ShopStats=Array.Empty<int>();
    public string ShopRank="C";
    public bool FinalShop;
    public float Travel,Elapsed,RiftElapsed,InfernoElapsed;
    public CoopPilotFrame[] Players=Array.Empty<CoopPilotFrame>();
    public Enemy[] Enemies=Array.Empty<Enemy>();
    public int[] Parents=Array.Empty<int>();
    public Projectile[] Shots=Array.Empty<Projectile>();
    public Debris[] Debris=Array.Empty<Debris>();
    public Pickup[] Pickups=Array.Empty<Pickup>();
    public LaserLane[] Lasers=Array.Empty<LaserLane>();
    public ExplosionSprite[] Explosions=Array.Empty<ExplosionSprite>();
    public Shockwave[] Shockwaves=Array.Empty<Shockwave>();
}

internal static class CoopWire
{
    internal const int DefaultPort=28771,MaxPlayers=6,MaxFrameBytes=2*1024*1024;
    internal static readonly JsonSerializerOptions Json=new() { IncludeFields=true,MaxDepth=16 };
    internal static byte[] Encode(CoopPacket packet)=>JsonSerializer.SerializeToUtf8Bytes(packet,Json);
    internal static async Task Send(NetworkStream stream,byte[] bytes,CancellationToken token)
    {
        if(bytes.Length>MaxFrameBytes) throw new IOException("Co-op frame exceeds size limit.");
        byte[] header=BitConverter.GetBytes(IPAddress.HostToNetworkOrder(bytes.Length));
        await stream.WriteAsync(header,token);await stream.WriteAsync(bytes,token);
    }
    internal static async Task<CoopPacket> Read(NetworkStream stream,int maximum,CancellationToken token)
    {
        byte[] header=new byte[4];await stream.ReadExactlyAsync(header,token);
        int length=IPAddress.NetworkToHostOrder(BitConverter.ToInt32(header));
        if(length<2 || length>maximum) throw new IOException("Invalid co-op message length.");
        byte[] bytes=new byte[length];await stream.ReadExactlyAsync(bytes,token);
        var packet=JsonSerializer.Deserialize<CoopPacket>(bytes,Json)??throw new IOException("Empty co-op packet.");
        if(packet.Version!=1) throw new IOException("Incompatible co-op protocol.");
        return packet;
    }
    internal static bool Valid(CoopInput input)=>input.Sequence>0 && float.IsFinite(input.X) &&
        float.IsFinite(input.Y) && Math.Abs(input.X)<=1 && Math.Abs(input.Y)<=1 &&
        input.WeaponDelta is >=-1 and <=1 && float.IsFinite(input.TargetX) && float.IsFinite(input.TargetY) &&
        input.WeaponRevision>=0 && input.WeaponChoice is >=-1 and <=4 &&
        (!input.HasTarget || input.TargetX is >=105 and <=1775 && input.TargetY is >=110 and <=965);
}

/// <summary>One authoritative host and up to five guests. Background tasks never touch the world.</summary>
internal sealed class CoopHost : IDisposable
{
    private sealed class Peer
    {
        internal readonly TcpClient Client;
        internal readonly Channel<byte[]> Out=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1) {
            FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=false });
        internal CoopSeat Seat;
        internal Peer(TcpClient client,CoopSeat seat) { Client=client;Seat=seat; }
    }
    private readonly CancellationTokenSource _stop=new();
    private readonly TcpListener _listener;
    private readonly ConcurrentDictionary<int,Peer> _peers=new();
    private readonly SemaphoreSlim _handshakes=new(16);
    private readonly string _room;
    private volatile bool _inLobby=true;
    internal bool InLobby { get=>_inLobby;set=>_inLobby=value; }
    internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
    internal CoopHost(int port,string room="",bool loopbackOnly=false)
    {
        _room=room;
        _listener=new TcpListener(loopbackOnly?IPAddress.Loopback:IPAddress.Any,port);_listener.Start(8);
        _=Accept();
    }
    internal CoopSeat[] Seats=>_peers.Values.Select(p=>p.Seat).OrderBy(p=>p.Id).ToArray();
    internal void Publish(CoopFrame frame)
    {
        byte[] bytes=CoopWire.Encode(new CoopPacket { Kind="frame",Frame=frame });
        foreach(var peer in _peers.Values) peer.Out.Writer.TryWrite(bytes);
    }
    private async Task Accept()
    {
        while(!_stop.IsCancellationRequested)
        {
            try
            {
                var client=await _listener.AcceptTcpClientAsync(_stop.Token);
                if(!_handshakes.Wait(0)) { client.Dispose();continue; }
                _=Run(client);
            }
            catch(OperationCanceledException) { break; }
            catch(SocketException) when(_stop.IsCancellationRequested) { break; }
        }
    }
    private async Task Run(TcpClient client)
    {
        int id=-1;Task? writer=null;
        using var lifetime=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        try
        {
            client.NoDelay=true;
            var stream=client.GetStream();
            using(var helloTimeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                helloTimeout.CancelAfter(5000);
                var hello=await CoopWire.Read(stream,4096,helloTimeout.Token);
                if(hello.Kind!="hello") throw new IOException("Hello required.");
                if(!string.Equals(hello.Room,_room,StringComparison.Ordinal))
                { await CoopWire.Send(stream,CoopWire.Encode(new CoopPacket { Kind="error",Error="WRONG ROOM CODE" }),lifetime.Token);return; }
                var name=new string((hello.Name??"").Where(c=>c is >= ' ' and <= '~').Take(16).ToArray());
                if(!_inLobby) { await CoopWire.Send(stream,CoopWire.Encode(new CoopPacket { Kind="error",Error="RUN STARTED - JOIN IN LOBBY" }),lifetime.Token);return; }
                Peer? peer=null;
                for(int candidate=1;candidate<CoopWire.MaxPlayers;candidate++)
                {
                    var value=new Peer(client,new CoopSeat(candidate,name.Length==0?"PILOT "+(candidate+1):name,new CoopInput(),Environment.TickCount64));
                    if(_peers.TryAdd(candidate,value)) { id=candidate;peer=value;break; }
                }
                if(peer==null) { await CoopWire.Send(stream,CoopWire.Encode(new CoopPacket { Kind="error",Error="PARTY FULL (6/6)" }),lifetime.Token);return; }
                await CoopWire.Send(stream,CoopWire.Encode(new CoopPacket { Kind="welcome",Id=id }),lifetime.Token);
                writer=Task.Run(async()=> {
                    try { await foreach(var bytes in peer.Out.Reader.ReadAllAsync(lifetime.Token))
                        { using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(3000);await CoopWire.Send(stream,bytes,timeout.Token); } }
                    finally { lifetime.Cancel(); }
                });
                long lastSequence=0;int messages=0;long window=Environment.TickCount64;
                while(!lifetime.IsCancellationRequested)
                {
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);timeout.CancelAfter(10000);
                    var message=await CoopWire.Read(stream,4096,timeout.Token);
                    long now=Environment.TickCount64;
                    if(now-window>=1000) { messages=0;window=now; }
                    if(++messages>100) throw new IOException("Input rate exceeded.");
                    if(message.Kind!="input" || message.Input is not { } input || !CoopWire.Valid(input)) throw new IOException("Invalid input.");
                    if(input.Sequence<=lastSequence) continue;
                    lastSequence=input.Sequence;peer.Seat=peer.Seat with { Input=input,LastInput=now };
                }
            }
        }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or JsonException or ObjectDisposedException) { }
        finally
        {
            lifetime.Cancel();if(id>=0) _peers.TryRemove(id,out _);client.Dispose();
            if(writer!=null) try { await writer; } catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
            _handshakes.Release();
        }
    }
    public void Dispose()
    {
        _stop.Cancel();_listener.Stop();foreach(var peer in _peers.Values) peer.Client.Dispose();_peers.Clear();
    }
}

internal sealed class CoopClient : IDisposable
{
    private readonly CancellationTokenSource _stop=new();
    private readonly TcpClient _client=new();
    private readonly Channel<byte[]> _out=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(1) {
        FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true,SingleWriter=false });
    private CoopFrame? _latest;
    private int _id=-1;
    private string _status="CONNECTING...";
    internal int Id=>Volatile.Read(ref _id);
    internal string Status=>Volatile.Read(ref _status);
    internal CoopFrame? Latest=>Volatile.Read(ref _latest);
    internal bool Disconnected { get; private set; }
    internal CoopClient(string address,int port,string name,string room="") { _=Run(address,port,name,room); }
    internal void Input(CoopInput input)=>_out.Writer.TryWrite(CoopWire.Encode(new CoopPacket { Kind="input",Input=input }));
    private async Task Run(string address,int port,string name,string room)
    {
        Task? writer=null;
        try
        {
            using(var connect=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            { connect.CancelAfter(8000);await _client.ConnectAsync(address,port,connect.Token); }
            _client.NoDelay=true;var stream=_client.GetStream();
            await CoopWire.Send(stream,CoopWire.Encode(new CoopPacket { Kind="hello",Name=name,Room=room }),_stop.Token);
            using(var hello=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token))
            {
                hello.CancelAfter(5000);var welcome=await CoopWire.Read(stream,4096,hello.Token);
                if(welcome.Kind!="welcome") throw new IOException(welcome.Error);
                if(welcome.Id is <1 or >=6) throw new IOException("Invalid assigned player.");
                Volatile.Write(ref _id,welcome.Id);_status="CONNECTED / PLAYER "+(welcome.Id+1);
            }
            writer=Task.Run(async()=> {
                try { await foreach(var bytes in _out.Reader.ReadAllAsync(_stop.Token)) await CoopWire.Send(stream,bytes,_stop.Token); }
                finally { _stop.Cancel(); }
            });
            while(!_stop.IsCancellationRequested)
            {
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);timeout.CancelAfter(10000);
                var packet=await CoopWire.Read(stream,CoopWire.MaxFrameBytes,timeout.Token);
                if(packet.Kind!="frame" || packet.Frame is not { } frame || frame.Players is null || frame.Shots is null || frame.Enemies is null ||
                    frame.Players.Length>6 || frame.Shots.Length>8192 || frame.Enemies.Length>512 ||
                    frame.Parents is null || frame.Debris is null || frame.Pickups is null || frame.Lasers is null ||
                    frame.Explosions is null || frame.Shockwaves is null || frame.ShopStats is null ||
                    frame.Debris.Length>512 || frame.Pickups.Length>2048 || frame.Lasers.Length>512 || frame.Explosions.Length>2048 ||
                    frame.Players.Any(p=>p is null || p.Id is <0 or >=6 || p.Pilot is null) || frame.Players.Select(p=>p.Id).Distinct().Count()!=frame.Players.Length)
                    throw new IOException("Invalid host frame.");
                Volatile.Write(ref _latest,frame);
            }
        }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or JsonException or ObjectDisposedException)
        { _status=ex is EndOfStreamException?"HOST CLOSED THE SESSION":"DISCONNECTED: "+ex.Message; }
        finally
        {
            Disconnected=true;_stop.Cancel();_client.Dispose();
            if(writer!=null) try { await writer; } catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException) { }
        }
    }
    public void Dispose() { _stop.Cancel();_client.Dispose(); }
}

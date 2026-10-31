using IronNebula;
using Microsoft.Xna.Framework;
using System.Net.Sockets;

int checks=0;
void Check(bool condition,string label) { checks++;if(!condition) throw new Exception(label); }
async Task Until(Func<bool> condition,string label)
{
    for(int i=0;i<250;i++) { if(condition()) return;await Task.Delay(20); }
    throw new Exception("Timed out: "+label);
}
Check(!CoopWire.Valid(new CoopInput(1,float.NaN,0)),"Reject NaN input");
Check(!CoopWire.Valid(new CoopInput(1,2,0)),"Reject speed injection");
Check(!CoopWire.Valid(new CoopInput(1,0,0,HasTarget:true,TargetX:100000)),"Reject out-of-world mouse target");
Check(CoopWire.Valid(new CoopInput(1,1,1,true,true,1,true)),"Valid diagonal and actions");
using var host=new CoopHost(0,loopbackOnly:true);
var guests=Enumerable.Range(1,5).Select(i=>new CoopClient("127.0.0.1",host.Port,"PILOT "+i)).ToArray();
try
{
    await Until(()=>guests.All(c=>c.Id>0) && host.Seats.Length==5,"Five guests join");
    Check(guests.Select(c=>c.Id).Distinct().Count()==5,"Unique player IDs");
    using(var overflow=new CoopClient("127.0.0.1",host.Port,"SEVENTH"))
    { await Until(()=>overflow.Disconnected,"Reject seventh player");Check(overflow.Status.Contains("FULL"),"Party limit includes host"); }
    for(int i=0;i<5;i++) guests[i].Input(new CoopInput(1,.1f*i,-.2f,true,false,1));
    await Until(()=>host.Seats.All(s=>s.Input.Sequence==1),"Inputs reach host");
    Check(host.Seats.All(s=>s.Input.Fire),"All guests fire independently");
    host.Publish(new CoopFrame { Tick=42,Mode="Playing",Players=Enumerable.Range(0,6).Select(i=>new CoopPilotFrame {
        Id=i,Pilot=new Pilot { Position=new Vector2(i*100,300),Health=4-i%3,PrimaryMode=(PrimaryWeaponMode)(i%5) } }).ToArray(),
        Enemies=new[]{new Enemy { Kind=EnemyKind.Heavy,Health=23,Position=new Vector2(1000,400),Radius=40 }},
        Shots=new[]{new Projectile { OwnerPlayerId=4,Friendly=true,Position=new Vector2(600,500),Tint=Color.Cyan }},
        Credits=137,Score=999 });
    await Until(()=>guests.All(c=>c.Latest?.Tick==42),"All clients receive same snapshot");
    foreach(var guest in guests)
    {
        var frame=guest.Latest!;
        Check(frame.Players.Length==6,"Six replicated ships");
        Check(frame.Shots[0].OwnerPlayerId==4 && frame.Shots[0].Tint==Color.Cyan,"Projectile ownership and color");
        Check(frame.Enemies[0].Health==23 && frame.Credits==137 && frame.Score==999,"Authoritative combat/economy state");
        Check(frame.Players[5].Pilot.Position.X==500,"Vector2 field serialization");
    }
    // Replayed input cannot replace the newest command.
    guests[0].Input(new CoopInput(1,-1,0,false));await Task.Delay(80);
    Check(host.Seats.Single(s=>s.Id==guests[0].Id).Input.Fire,"Reject replayed input sequence");
    guests[0].Input(new CoopInput(2,0,0,Ready:true));
    await Until(()=>host.Seats.Single(s=>s.Id==guests[0].Id).Input.Ready,"Ready state");
    guests[0].Dispose();await Until(()=>host.Seats.Length==4,"Disconnect frees seat");
    host.InLobby=false;
    using(var late=new CoopClient("127.0.0.1",host.Port,"LATE"))
    { await Until(()=>late.Disconnected,"Late join policy");Check(late.Status.Contains("LOBBY"),"No mid-run state injection"); }
    using var malformed=new TcpClient();await malformed.ConnectAsync("127.0.0.1",host.Port);
    await malformed.GetStream().WriteAsync(new byte[]{0x7f,0xff,0xff,0xff});
    await Task.Delay(100);Check(host.Seats.Length==4,"Oversized frame cannot allocate a player");
    using(var privateHost=new CoopHost(0,"123456",loopbackOnly:true))
    {
        using var denied=new CoopClient("127.0.0.1",privateHost.Port,"WRONG","999999");
        await Until(()=>denied.Disconnected,"Wrong room code");Check(denied.Status.Contains("CODE"),"Wrong room code rejected");
        using var admitted=new CoopClient("127.0.0.1",privateHost.Port,"RIGHT","123456");
        await Until(()=>admitted.Id>0,"Correct room code");Check(privateHost.Seats.Length==1,"Private room admission");
    }
    Console.WriteLine($"PASS: {checks} co-op transport checks; host + five clients over real loopback TCP.");
}
finally { foreach(var guest in guests) guest.Dispose(); }

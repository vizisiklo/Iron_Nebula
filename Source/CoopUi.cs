using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IronNebula;

public sealed partial class IronNebulaGame
{
    private void DrawCoopOtherPilots()
    {
        if(!CoopActive || !_coopStarted || _mode is not (GameMode.Playing or GameMode.Paused)) return;
        int local=_coopClient?.Id??0;
        IEnumerable<CoopPilotFrame> players=_coopHost!=null?_party.Values.Select(a=>new CoopPilotFrame {
            Id=a.Id,Name=a.Name,Pilot=a.Pilot,Bank=a.Bank,Beam=a.Beam,BeamStart=a.BeamStart,BeamEnd=a.BeamEnd }):
            _receivedCoopFrame?.Players??Array.Empty<CoopPilotFrame>();
        foreach(var actor in players)
        {
            if(actor.Pilot.Lives<=0) continue;
            Color color=PartyColors[actor.Id%6];
            Vector2 position=actor.Pilot.Position;
            if(_coopClient!=null && actor.Id!=local)
            {
                if(_coopRenderPositions.TryGetValue(actor.Id,out var previous))
                    position=Vector2.Lerp(previous,position,.28f);
                _coopRenderPositions[actor.Id]=position;
            }
            if(actor.Id!=local)
            {
                _batch.Draw(_hdPlayer,position,null,Color.Lerp(Color.White,color,.28f),actor.Bank*MaxPilotRoll,
                    PlayerOrigin,PlayerScale,SpriteEffects.None,0);
                if(actor.Pilot.Shield>0) _batch.Draw(_shieldRing,position,null,color*.55f,0,
                    new Vector2(_shieldRing.Width/2f,_shieldRing.Height/2f),.62f*CombatZoom,SpriteEffects.None,0);
                if(actor.Pilot.DroneActive) _batch.Draw(_droneSprite,actor.Pilot.DronePosition,null,color,0,
                    new Vector2(_droneSprite.Width/2f,_droneSprite.Height/2f),.43f*CombatZoom,SpriteEffects.None,0);
                if(actor.Beam) DrawBeam(actor.BeamStart,actor.BeamEnd,12,color*.85f);
            }
            _ui.TextShadow(_batch,"P"+(actor.Id+1)+(actor.Id==local?" YOU":""),position+new Vector2(0,48),.25f,color,true);
        }
    }
    private void DrawCoopOverlay()
    {
        if(!CoopActive && !_coopMenu) return;
        Color cyan=new(110,225,255);
        if(!_coopStarted)
        {
            ModalPanel("ONLINE / LAN CO-OP   /   2-6 PILOTS");
            if(!CoopActive)
            {
                _ui.Text(_batch,"CLICK TO EDIT: HOST:PORT/ROOMCODE (SHOWN BY HOST)",new Vector2(350,247),.4f,cyan);
                _ui.Rect(_batch,new Rectangle(350,300,1220,100),new Color(20,48,75));
                _ui.TextFit(_batch,_coopAddress+(_coopAddressFocus?"_":""),new Vector2(375,325),.6f,1160,Color.White);
                string[] labels={"HOST NEW PARTY","JOIN HOST ADDRESS","BACK"};
                for(int i=0;i<3;i++) DrawMenuButton(new Rectangle(500,465+i*130,920,110),labels[i],i==_coopMenuSelection,false,.65f);
                _ui.TextFit(_batch,_coopStatus,new Vector2(110,897),.4f,1670,Color.Gold);
                _ui.TextFit(_batch,"LAN: HOST IP. INTERNET: REACHABLE HOST / PORT FORWARDING OR VPN. TCP 28771.",new Vector2(110,970),.34f,1700,cyan);
            }
            else
            {
                var players=_coopHost!=null?new[]{new CoopPilotFrame { Id=0,Name="HOST" }}.Concat(_coopHost.Seats.Select(s=>new CoopPilotFrame { Id=s.Id,Name=s.Name })).ToArray():
                    _coopClient?.Latest?.Players??Array.Empty<CoopPilotFrame>();
                _ui.TextFit(_batch,_coopHost!=null?_coopStatus:_coopClient!.Status,new Vector2(110,245),.39f,1700,cyan);
                for(int i=0;i<6;i++)
                {
                    int y=325+i*86;var seat=players.FirstOrDefault(p=>p.Id==i);
                    _ui.Rect(_batch,new Rectangle(350,y,1220,75),new Color(8,34,55));
                    _ui.Text(_batch,"P"+(i+1)+"   "+(seat?.Name??"WAITING FOR PILOT"),new Vector2(380,y+15),.45f,seat==null?Color.Gray:PartyColors[i]);
                }
                _ui.TextShadow(_batch,_coopHost!=null?ConfirmPrompt+" START FLIGHT (2-6 PLAYERS)":
                    "WAITING FOR HOST TO LAUNCH",new Vector2(ViewWidth/2,905),.48f,cyan,true);
                _ui.Text(_batch,BackPrompt+" OPEN DISCONNECT MENU",new Vector2(350,985),.36f,Color.White);
            }
        }
        else
        {
            var players=_coopHost!=null?_party.Values.Select(a=>new CoopPilotFrame { Id=a.Id,Pilot=a.Pilot,Ready=a.Id==0?_coopReady:a.Ready }).ToArray():
                _receivedCoopFrame?.Players??Array.Empty<CoopPilotFrame>();
            for(int i=0;i<players.Length;i++)
            {
                var actor=players[i];int x=25+i*315;
                int y=_mode==GameMode.Shop?174:ViewHeight-57;
                _ui.Rect(_batch,new Rectangle(x,y,303,_mode==GameMode.Shop?29:51),new Color(2,14,28,225));
                string label=_mode==GameMode.Shop?"P"+(actor.Id+1)+(actor.Ready?" READY":" WAITING"):
                    "P"+(actor.Id+1)+" HULL "+actor.Pilot.Health+" / "+actor.Pilot.MaxHealth+"  LIVES "+actor.Pilot.Lives;
                _ui.TextFit(_batch,label,new Vector2(x+10,y+5),.28f,285,PartyColors[actor.Id%6]);
            }
            if(_mode==GameMode.Shop) _ui.TextFit(_batch,(_coopHost!=null?"HOST BUYS TEAM UPGRADES. ":"HOST CONTROLS SHOP. ")+
                ContinuePrompt+" TOGGLE READY / ALL PILOTS MUST BE READY",new Vector2(110,147),.26f,1690,Color.Gold);
        }
        if(_coopPause)
        {
            Shade();DrawCleanPanel(new Rectangle(350,220,1220,720),cyan);
            _ui.TextShadow(_batch,"LOCAL MENU / PARTY KEEPS FLYING",new Vector2(ViewWidth/2,285),.6f,Color.White,true);
            string[] labels={"RETURN TO FLIGHT","SETTINGS","DISCONNECT / LEAVE PARTY"};
            for(int i=0;i<3;i++) DrawMenuButton(new Rectangle(500,410+i*140,920,115),labels[i],i==_coopPauseSelection,false,.6f);
        }
    }
}

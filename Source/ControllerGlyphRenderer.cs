using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IronNebula;

internal sealed partial class UiRenderer
{
    private void DrawControllerGlyph(SpriteBatch batch,char glyph,Vector2 center,float scale,Color tint)
    {
        int id=glyph-'\uE000';
        bool shadow=tint.R<10 && tint.G<10 && tint.B<20;
        Color ink=shadow?tint:Color.White*(tint.A/255f);
        void Line(float x1,float y1,float x2,float y2,Color color,float thickness=3.8f)
        {
            Vector2 from=center+new Vector2(x1,y1)*scale,to=center+new Vector2(x2,y2)*scale;
            Vector2 delta=to-from;
            batch.Draw(_pixel,from,null,color,MathF.Atan2(delta.Y,delta.X),new Vector2(0,.5f),
                new Vector2(delta.Length(),MathF.Max(1f,thickness*scale)),SpriteEffects.None,0);
        }
        void Ring(float radius,Color color)
        {
            for(int i=0;i<40;i++)
            {
                float a=i*MathHelper.TwoPi/40,b=(i+1)*MathHelper.TwoPi/40;
                Line(MathF.Cos(a)*radius,MathF.Sin(a)*radius,MathF.Cos(b)*radius,MathF.Sin(b)*radius,color,3);
            }
        }
        void Box(float x,float y,float width,float height)
        { Line(x,y,x+width,y,ink);Line(x+width,y,x+width,y+height,ink);Line(x+width,y+height,x,y+height,ink);Line(x,y+height,x,y,ink); }
        void Label(string text,float size=.65f,float rise=29)
            =>Text(batch,text,center-new Vector2(0,rise*scale),size*scale/ReadabilityScale,ink,true);

        if(id<8)
        {
            Color face=id switch { 0=>new Color(108,231,103),1=>new Color(255,111,113),2=>new Color(100,181,255),
                3=>new Color(255,214,91),4=>new Color(137,191,255),5=>new Color(255,140,151),
                6=>new Color(236,164,236),_=>new Color(125,235,191) };
            if(shadow) face=tint;else face*=tint.A/255f;
            Ring(30,id<4?face:ink);
            if(id<4) { ink=face;Label(new[]{"A","B","X","Y"}[id],.74f); }
            else if(id==4) { Line(-12,-12,12,12,face);Line(-12,12,12,-12,face); }
            else if(id==5) Ring(15,face);
            else if(id==6) { Color previous=ink;ink=face;Box(-14,-14,28,28);ink=previous; }
            else { Line(0,-17,18,14,face);Line(18,14,-18,14,face);Line(-18,14,0,-17,face); }
            return;
        }
        if(id==8) // D-pad silhouette with directional tips.
        {
            Line(-27,0,27,0,ink,12);Line(0,-27,0,27,ink,12);
            return;
        }
        if(id is 9 or 10) { Ring(29,ink);Label(id==9?"LS":"RS",.51f,20);return; }
        if(id is >=11 and <=18)
        {
            Line(-31,-20,31,-20,ink);Line(31,-20,34,22,ink);Line(34,22,-34,22,ink);Line(-34,22,-31,-20,ink);
            string[] labels={"LB","RB","LT","RT","L1","R1","L2","R2"};
            Label(labels[id-11],.52f,20);return;
        }
        Ring(30,ink);
        if(id is 19 or 21) // Xbox Menu / DualSense Options.
        { for(int y=-12;y<=12;y+=12) Line(-15,y,15,y,ink); }
        else if(id==20) { Box(-17,-13,23,20);Box(-5,-3,23,20); }
        else if(id==22) // DualSense Create: three rays around the button.
        { Box(-4,-4,15,20);Line(-15,-7,-23,-14,ink);Line(-6,-15,-10,-25,ink);Line(7,-16,9,-26,ink); }
        else if(id==23)
        {
            Box(-21,-12,42,25);
            for(int x=-12;x<=12;x+=8) Line(x,-4,x+1,-4,ink,2);
        }
    }
}

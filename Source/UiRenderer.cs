using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IronNebula;

/// <summary>
/// Noto Sans Display SemiCondensed Bold (Google Noto, SIL OFL 1.1).
/// Pre-rasterized in ui_atlas.png; no font files, runtime font dependencies or content pipeline required.
/// Variable advances and a common glyph baseline fix the former fixed-cell character spacing.
/// Atlas: ASCII 32-126, 16 columns, 72x88 px per cell.
/// </summary>
internal sealed partial class UiRenderer
{
    private const int GlyphCellWidth=72;
    private const int GlyphCellHeight=88;
    private const float LineHeight=82f;
    private const float ReadabilityScale=1.08f;
    private static readonly byte[] Advance = {
        21, 19, 32, 40, 36, 57, 46, 19, 23, 23, 34, 36, 19, 22, 19, 28, 36, 36, 36,
        36, 36, 36, 36, 36, 36, 36, 19, 19, 36, 36, 36, 30, 54, 42, 41, 39, 44, 34,
        33, 44, 46, 26, 22, 41, 34, 58, 49, 47, 38, 47, 40, 34, 35, 45, 41, 60, 41,
        39, 36, 22, 28, 22, 35, 31, 25, 37, 39, 32, 39, 37, 25, 39, 40, 20, 20, 38,
        20, 59, 40, 38, 39, 39, 28, 31, 27, 40, 36, 53, 36, 36, 30, 27, 33, 27, 36
    };

    private readonly Texture2D _atlas;
    private readonly Texture2D _pixel;
    public UiRenderer(Texture2D atlas,Texture2D pixel)
    {
        _atlas=atlas ?? throw new ArgumentNullException(nameof(atlas));
        _pixel=pixel ?? throw new ArgumentNullException(nameof(pixel));
        if(_atlas.Width != GlyphCellWidth*16 || _atlas.Height != GlyphCellHeight*6)
            throw new ArgumentException("The installed ui_atlas.png does not match the new font renderer. Install the entire Alpha 0.9.2.3 patch.");
    }

    private static int GlyphIndex(char c)
    {
        if(c=='\u00a0' || c=='\u202f' || c=='\u2009') return 0; // non-breaking / thin spaces
        return c>=32 && c<=126?c-32:'?'-32;
    }

    private static bool IsControllerGlyph(char c) => c>='\uE000' && c<='\uE017';
    private static float GlyphAdvance(char c) => IsControllerGlyph(c)?84:Advance[GlyphIndex(c)];

    private static float WidthOf(string text)
    {
        float width=0;
        foreach(char c in text)
            if(c!='\n' && c!='\r') width+=GlyphAdvance(c);
        return width;
    }

    public void Text(SpriteBatch batch,string message,Vector2 at,float scale,Color color,bool centered=false)
    {
        if(string.IsNullOrEmpty(message))return;
        string[] lines=message.ToUpperInvariant().Replace("\r",string.Empty).Split('\n');
        float px=scale*ReadabilityScale;
        for(int line=0;line<lines.Length;line++)
        {
            string value=lines[line];
            float x=at.X-(centered?WidthOf(value)*px*.5f:0f);
            float y=at.Y+line*LineHeight*px;
            foreach(char ch in value)
            {
                if(IsControllerGlyph(ch))
                {
                    DrawControllerGlyph(batch,ch,new Vector2(x+39*px,y+40*px),px,color);
                    x+=GlyphAdvance(ch)*px;
                    continue;
                }
                int glyph=GlyphIndex(ch);
                if(glyph!=0)
                {
                    Rectangle src=new((glyph%16)*GlyphCellWidth,(glyph/16)*GlyphCellHeight,
                                      GlyphCellWidth,GlyphCellHeight);
                    batch.Draw(_atlas,new Vector2(MathF.Round(x),MathF.Round(y)),src,color,0f,
                               Vector2.Zero,px,SpriteEffects.None,0f);
                }
                x+=Advance[glyph]*px;
            }
        }
    }

    public void TextFit(SpriteBatch batch,string message,Vector2 at,float desiredScale,
                        float maximumWidth,Color color,bool centered=false)
    {
        if(string.IsNullOrEmpty(message))return;
        float measured=WidthOf(message.ToUpperInvariant())*ReadabilityScale*desiredScale;
        float scale=measured>MathF.Max(1f,maximumWidth)
            ?desiredScale*(maximumWidth/measured):desiredScale;
        Text(batch,message,at,scale,color,centered);
    }

    public void TextWrap(SpriteBatch batch,string message,Vector2 at,float scale,float width,
                         Color color,float lineSpacing=1.13f)
    {
        if(string.IsNullOrWhiteSpace(message))return;
        float px=scale*ReadabilityScale;
        float maxAdvance=MathF.Max(1f,width/MathF.Max(.001f,px));
        float y=at.Y;
        string line="";
        foreach(string word in message.ToUpperInvariant().Replace("\r",string.Empty).Split(' ',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string test=line.Length==0?word:line+" "+word;
            if(line.Length>0 && WidthOf(test)>maxAdvance)
            {
                Text(batch,line,new Vector2(at.X,y),scale,color);
                y+=LineHeight*px*lineSpacing;
                line="";
            }
            // Split unusually long tokens rather than running outside the panel.
            if(line.Length>0)line+=" ";
            foreach(char c in word)
            {
                if(line.Length>0 && WidthOf(line+c)>maxAdvance)
                {
                    Text(batch,line,new Vector2(at.X,y),scale,color);
                    y+=LineHeight*px*lineSpacing;
                    line="";
                }
                line+=c;
            }
        }
        if(line.Trim().Length>0) Text(batch,line.TrimEnd(),new Vector2(at.X,y),scale,color);
    }

    public void TextShadow(SpriteBatch batch,string message,Vector2 at,float scale,
                           Color color,bool centered=false)
    {
        Text(batch,message,new Vector2(at.X+2f,at.Y+3f),scale,new Color(0,5,14,235),centered);
        Text(batch,message,at,scale,color,centered);
    }

    public void Rect(SpriteBatch batch,Rectangle rect,Color color)=>batch.Draw(_pixel,rect,color);

    public void Frame(SpriteBatch batch,Rectangle rect,Color color,int thick=2)
    {
        Rect(batch,new Rectangle(rect.X,rect.Y,rect.Width,thick),color);
        Rect(batch,new Rectangle(rect.X,rect.Bottom-thick,rect.Width,thick),color);
        Rect(batch,new Rectangle(rect.X,rect.Y,thick,rect.Height),color);
        Rect(batch,new Rectangle(rect.Right-thick,rect.Y,thick,rect.Height),color);
    }

    public void Bar(SpriteBatch batch,Rectangle rect,float fraction,Color fill)
    {
        Rect(batch,rect,new Color(7,17,34,218));
        Frame(batch,rect,new Color(81,138,167,190),2);
        int width=(int)((rect.Width-8)*MathHelper.Clamp(fraction,0f,1f));
        if(width>0)Rect(batch,new Rectangle(rect.X+4,rect.Y+4,width,rect.Height-8),fill);
    }
}

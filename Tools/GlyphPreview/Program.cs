using IronNebula;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

if(args.Length!=2) throw new ArgumentException("Usage: GlyphPreview <ui_atlas.png> <output.png>");
using var game=new GlyphPreview(Path.GetFullPath(args[0]),Path.GetFullPath(args[1]));
game.Run();

sealed class GlyphPreview : Game
{
    private readonly string _atlasPath,_outputPath;
    private SpriteBatch _batch;
    private Texture2D _atlas,_pixel;
    private UiRenderer _ui;
    public GlyphPreview(string atlasPath,string outputPath)
    {
        _atlasPath=atlasPath;_outputPath=outputPath;
        new GraphicsDeviceManager(this) { PreferredBackBufferWidth=1280,PreferredBackBufferHeight=600 };
        Window.Title="Iron Nebula controller glyph render verification";
    }
    protected override void LoadContent()
    {
        _batch=new SpriteBatch(GraphicsDevice);
        using var stream=File.OpenRead(_atlasPath);
        _atlas=Texture2D.FromStream(GraphicsDevice,stream);
        _pixel=new Texture2D(GraphicsDevice,1,1);_pixel.SetData(new[]{Color.White});
        _ui=new UiRenderer(_atlas,_pixel);
    }
    protected override void Draw(GameTime gameTime)
    {
        using var target=new RenderTarget2D(GraphicsDevice,1920,900);
        GraphicsDevice.SetRenderTarget(target);GraphicsDevice.Clear(new Color(4,15,30));
        _batch.Begin(samplerState:SamplerState.LinearClamp);
        _ui.Text(_batch,"CONTROLLER ICONS / NATIVE GAME RENDERER",new Vector2(60,30),.7f,Color.White);
        string[] names={"XBOX A","XBOX B","XBOX X","XBOX Y","CROSS","CIRCLE","SQUARE","TRIANGLE",
            "D-PAD","LEFT STICK","RIGHT STICK","LB","RB","LT","RT","L1","R1","L2","R2","MENU","VIEW","OPTIONS","CREATE","TOUCHPAD"};
        for(int i=0;i<24;i++)
        {
            int x=65+(i%12)*153,y=140+(i/12)*200;
            _ui.TextShadow(_batch,((char)('\uE000'+i)).ToString(),new Vector2(x,y),1.05f,Color.White);
            _ui.TextFit(_batch,names[i],new Vector2(x,y+100),.3f,145,Color.LightCyan);
        }
        _ui.Text(_batch,"XBOX",new Vector2(60,560),.45f,Color.LawnGreen);
        _ui.TextShadow(_batch,"\uE008 / \uE009 SELECT   \uE000 CONFIRM   \uE014 HELP   \uE003 SETTINGS",new Vector2(60,610),.46f,Color.Cyan);
        _ui.Text(_batch,"DUALSENSE",new Vector2(60,690),.45f,Color.LightBlue);
        _ui.TextShadow(_batch,"\uE008 / \uE009 SELECT   \uE004 CONFIRM   \uE016 / \uE017 HELP   \uE007 SETTINGS",new Vector2(60,740),.46f,Color.Cyan);
        _batch.End();GraphicsDevice.SetRenderTarget(null);
        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
        using(var output=File.Create(_outputPath)) target.SaveAsPng(output,1920,900);
        Console.WriteLine("Rendered 24 controller icons and both menu footers: "+_outputPath);
        Exit();
    }
    protected override void UnloadContent() { _batch?.Dispose();_atlas?.Dispose();_pixel?.Dispose(); }
}

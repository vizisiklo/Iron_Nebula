using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace IronNebula;

// Alpha 0.9.7: difficulty is selected for the NEXT run. The active run difficulty
// is preserved through checkpoints and disk saves. Normal retains legacy values.
public sealed partial class IronNebulaGame
{
    private DifficultyMode _selectedDifficulty=DifficultyMode.Normal;
    private DifficultyMode _runDifficulty=DifficultyMode.Normal;
    private bool _performanceOverlay;
    private double _performanceSeconds;
    private int _renderedFrames;
    private int _recentFps;

    private static string DifficultyName(DifficultyMode mode)=>mode switch
    {
        DifficultyMode.Easy=>"EASY",DifficultyMode.Hard=>"HARD",_=>"NORMAL"
    };
    private float DifficultyHpFactor=>_runDifficulty switch
    {
        DifficultyMode.Easy=>.78f,DifficultyMode.Hard=>1.32f,_=>1f
    };
    private float DifficultyShotTimeFactor=>_runDifficulty switch
    {
        DifficultyMode.Easy=>1.18f,DifficultyMode.Hard=>.82f,_=>1f
    };
    private float DifficultyProjectileSpeed=>_runDifficulty switch
    {
        DifficultyMode.Easy=>.88f,DifficultyMode.Hard=>1.12f,_=>1f
    };
    private float DifficultyScoreFactor=>_runDifficulty switch
    {
        DifficultyMode.Easy=>.80f,DifficultyMode.Hard=>1.25f,_=>1f
    };
    private void ApplyEnemyDifficultyOnce(Enemy enemy)
    {
        if(enemy.DifficultyApplied) return;
        enemy.DifficultyApplied=true;
        float partyFactor=_coopHost!=null?1f+.65f*Math.Max(0,_party.Count-1):1f;
        int hp=Math.Max(1,(int)MathF.Ceiling(enemy.MaxHealth*DifficultyHpFactor*partyFactor));
        enemy.Health=hp;enemy.MaxHealth=hp;
        // Cooldown scales continuously in the enemy update loops, including phase changes.
    }
    private void CountRenderedFrame(GameTime time)
    {
        _performanceSeconds+=Math.Max(0,time.ElapsedGameTime.TotalSeconds);
        _renderedFrames++;
        if(_performanceSeconds>=.60)
        {
            _recentFps=(int)Math.Round(_renderedFrames/_performanceSeconds);
            _performanceSeconds=0;_renderedFrames=0;
        }
    }
    private void DrawPerformanceOverlay()
    {
        // Telemetry is intentionally small and outside primary combat indicators.
        var area=new Rectangle(1492,174,400,196);
        _ui.Rect(_batch,area,new Color(2,9,20,226));
        _ui.Frame(_batch,area,new Color(92,218,239,155),2);
        Color tint=new(126,235,251);
        _ui.Text(_batch,"PERFORMANCE F10",new Vector2(1510,189),.35f,tint);
        _ui.Text(_batch,"FPS "+_recentFps.ToString("000"),new Vector2(1510,226),.33f,Color.White);
        _ui.Text(_batch,"SHOTS "+_shots.Count+"   ENEMIES "+_enemies.Count,new Vector2(1510,260),.31f,Color.White);
        _ui.Text(_batch,"FX "+(_particles.Count+_combatSparks.Count+_explosionSprites.Count),new Vector2(1510,292),.31f,Color.White);
        _ui.Text(_batch,"DIFFICULTY "+DifficultyName(_runDifficulty),new Vector2(1510,326),.30f,tint);
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace IronNebula;

public sealed partial class IronNebulaGame : Game
{
    public const int ViewWidth=1920, ViewHeight=1080;
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _batch = null!;
    private RenderTarget2D _scene = null!;
    private RenderTarget2D? _hitBlurTarget;
    private Texture2D _pixel=null!;
    private Texture2D _sentinel=null!, _logo=null!;
    // Alpha 0.5: side-view player refit with true roll-style banking around a custom hull origin.
    private Texture2D _hdPlayer=null!, _hdPlayerPlume=null!, _hdBackdrop=null!;
    // Sector 02: independent alien fleet, clean Rift backdrop and two boss encounters.
    private Texture2D _riftCleanBackdrop=null!;
    private Texture2D _cockpitHud=null!;
    private Texture2D _guiBackground=null!, _guiGlassPanel=null!, _guiTab=null!;
    private Texture2D _guiPanel092=null!, _guiButton092=null!, _guiButtonActive092=null!;
    private int _titleMenuSelection, _pauseMenuSelection, _shopTab;
    private bool _vsyncEnabled=true;
    private readonly Texture2D[] _riftEnemies=new Texture2D[5];
    // Native-resolution silhouette masks; a tight colored rim plus dark outer separation.
    private readonly Texture2D[] _enemyEdgeMasks=new Texture2D[11];
    private bool _enemyReadabilityEnabled=true;
    private readonly Texture2D[,] _riftDofRocks=new Texture2D[3,12];
    // Independent, pre-blurred Rift-only wreckage: background scenery, never collision objects.
    private readonly Texture2D[,] _riftDrifterArt=new Texture2D[3,4];
    private readonly List<RiftDrifter> _riftDrifters=new();
    private sealed class RiftDrifter
    {
        public float StartX, Y, Speed, Size, Phase, Rotation, Spin;
        public int Layer, Variant;
    }
    private Texture2D _riftHazard=null!;
    private Texture2D _riftGuardianSprite=null!, _riftColossusSprite=null!, _riftBossTurretSprite=null!;
    private int _riftBossStage; // 0 waves, 1 guardian, 2 waves, 3 colossus, 4 defeated
    private const float GuardianAtSeconds=54f, ColossusAtSeconds=117f;
    private bool _riftActive;
    private float _riftElapsed, _riftFade;
    private int _riftWaveCount, _checkpointSector=1;
    private const float RiftDuration=115f;
    private const int SectorOneCreditCap=900; // Total Sector 01 earnings including boss rewards.
    private readonly Texture2D[] _hullDamageLayers=new Texture2D[3];
    private readonly Texture2D[] _hdEnemies=new Texture2D[3];
    private readonly Texture2D[] _enemyScorch=new Texture2D[3];
    private Texture2D _turretScorch=null!, _wardenScorch=null!, _sentinelScorch=null!;
    private readonly Texture2D[] _hdRocks=new Texture2D[12];
    // Pre-rendered distance-specific DOF. The blurred object cores are opaque,
    // unlike Alpha 0.5.2's multiply-tinted translucent sprite overlays.
    private readonly Texture2D[,] _dofRocks=new Texture2D[3,12];
    private bool _dofEnabled=true;
    private readonly List<ParallaxRock> _parallaxRocks=new();
    private float _pilotBank;
    private const float MaxPilotRoll=0.17f; // about 9.7 degrees: visible bank without cartoon wobble
    private const float CombatZoom=0.82f; // visual zoom-out, player + enemy sprites
    private const float PlayerScale=0.26f*CombatZoom;
    private static readonly Vector2 PlayerOrigin=new(690f,292f);
    private Texture2D _blueGlow=null!, _redGlow=null!, _orangeGlow=null!, _purpleGlow=null!;
    private Texture2D _explosionSheet=null!, _damageSmokeSheet=null!;
    private readonly Rectangle[] _explosionFrames=new Rectangle[8];
    private readonly Rectangle[,] _damageSmokeFrames=new Rectangle[4,3];
    private Texture2D _droneSprite=null!, _shieldRing=null!, _shockwaveRing=null!;
    private Texture2D _pickupWeapon=null!, _pickupRepair=null!, _pickupShield=null!, _pickupDrone=null!, _pickupCredits=null!, _shieldShell=null!;
    private Texture2D _pickupRareCache=null!, _shopPowerIcon=null!, _shopEngineIcon=null!;
    private Texture2D _turretSprite=null!, _wardenSprite=null!;
    private UiRenderer _ui=null!;
    private readonly Random _random=new(48120);
    // Separate RNG: visual-only sparks must not change gameplay spawn/drop rolls.
    private readonly Random _fxRandom=new(98123);
    private Pilot _pilot=new();
    private readonly List<Enemy> _enemies=new();
    private readonly List<Projectile> _shots=new();
    // Immutable, pre-rendered primary projectiles and directional contact effects.
    private readonly Texture2D[] _primaryShots=new Texture2D[5];
    private readonly Texture2D[] _primaryHits=new Texture2D[5];
    private Texture2D _beamStrip=null!, _beamMuzzle=null!;
    private bool _beamActive;
    private float _beamDamageTimer;
    private float _beamPhase;
    private float _gatlingPhase;
    private int _beamTickCount;
    private Vector2 _beamStart, _beamEnd;
    private int _checkpointWeaponLevel=2;
    private PrimaryWeaponMode _checkpointWeaponMode=PrimaryWeaponMode.Pulse;
    private int _checkpointShield=2;
    private readonly List<PrimaryImpact> _primaryImpacts=new();
    private readonly List<PrimaryDamageReadout> _primaryReadouts=new();
    private bool _precisionFire;
    private bool _trainingRun; // F9 developer stack testing disables best-score updates
    private bool _primaryVfxEnabled=true;
    private readonly List<Particle> _particles=new();
    private readonly List<ExplosionSprite> _explosionSprites=new();
    private readonly List<DamageSmoke> _damageSmokes=new();
    private readonly List<DamageSmoke> _enemySmokes=new();
    private readonly List<ScheduledDeathBurst> _scheduledDeathBursts=new();
    private float _bossFinaleTimer;
    private readonly List<CombatSpark> _combatSparks=new();
    private readonly List<CombatFlash> _combatFlashes=new();
    private bool _combatFxEnabled=true;
    private readonly List<Pickup> _pickups=new();
    private readonly List<Star> _stars=new();
    private readonly List<Shockwave> _shockwaves=new();
    private readonly List<Debris> _debris=new();
    private readonly List<LaserLane> _laserLanes=new();
    private KeyboardState _previousKeys;
    private GameMode _mode=GameMode.Title;
    private GameAudio? _audio;
    private bool _helpOpen, _settingsOpen;
    private bool _difficultyPromptOpen;
    private int _settingsSelection;

    private float _travel, _scroll, _elapsed, _waveClock, _shake, _flash, _messageTime;
    private string _message=string.Empty;
    private int _score,_spawnCount,_supportDropStage,_bestScore,_sectorZone;
    // Credits are distinct from score; bought primary weapons are unlocked for the run.
    private int _credits,_ownedWeaponsMask=1,_sectorStartScore,_sectorCreditsEarned;
    private int _sectorCreditPickups,_sectorWeaponPickups,_sectorSupplyPickups,_sectorKills;
    private int _sectorRareCaches,_rareCacheSpawns,_shopRareCaches,_shopRankBonus;
    private string _shopRank="C";
    private int _powerShotIndex;
    private int _shopSelection,_shopScore,_shopEarned,_shopLootCount,_shopWeaponCount,_shopSupplyCount,_shopKills;
    private bool _shopAfterFinalSector;
    private string _shopFeedback=string.Empty;
    private float _shopFeedbackTime;
    // Snapshot stores the exact economy state at checkpoint, preventing duplicate farming.
    private int _checkpointCredits,_checkpointUnlockMask=1,_checkpointLives=3,_checkpointScore;
    private int _checkpointWeaponPowerLevel,_checkpointEngineLevel,_checkpointRareCaches;
    private int _checkpointSectorStartScore,_checkpointSectorCreditsEarned,_checkpointSectorCreditPickups;
    private int _checkpointSectorWeaponPickups,_checkpointSectorSupplyPickups,_checkpointSectorKills;
    private int _checkpointMaxHull=4,_checkpointMaxShield=3,_checkpointDroneUpgrade,_checkpointMagnet;
    private string _formationNotice=string.Empty;
    private float _formationNoticeTime;
    private float _debrisClock;
    private float _damageSmokeTimer;
    private bool _wardenLaunched,_wardenDefeated,_checkpointUnlocked;
    private bool _bossLaunched;
    private bool _fullscreen;
    private const float BossAtSeconds=110f;

    public IronNebulaGame()
    {
        _graphics=new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth=1600,
            PreferredBackBufferHeight=900,
            SynchronizeWithVerticalRetrace=true,
            GraphicsProfile=GraphicsProfile.HiDef,
            PreferMultiSampling=false
        };
        IsFixedTimeStep=true;
        TargetElapsedTime=TimeSpan.FromSeconds(1.0/60.0);
        IsMouseVisible=true;
        Window.Title="IRON NEBULA  |  ALPHA 0.9.12 - ONLINE CO-OP / XBOX / DUALSENSE";
        Window.AllowUserResizing=true;
        InactiveSleepTime=TimeSpan.FromMilliseconds(5);
    }

    protected override void LoadContent()
    {
        _batch=new SpriteBatch(GraphicsDevice);
        _scene=new RenderTarget2D(GraphicsDevice,ViewWidth,ViewHeight,false,SurfaceFormat.Color,DepthFormat.None);
        _pixel=new Texture2D(GraphicsDevice,1,1);
        _pixel.SetData(new[]{Color.White});
        _hdBackdrop=LoadImage("HD/outer_frontier_cinematic.png");
        _riftCleanBackdrop=LoadImage("Rift/rift_clean_075.png");
        LoadInfernoContent();
        _cockpitHud=LoadImage("FX/cockpit_hud_slim_075.png");
        _guiBackground=LoadImage("UI/menu_backdrop_092.jpg");
        _guiGlassPanel=LoadImage("UI/glass_panel.png");
        _guiTab=LoadImage("UI/menu_tab.png");
        _guiPanel092=LoadImage("UI/panel_frame_092.png");
        _guiButton092=LoadImage("UI/button_normal_092.png");
        _guiButtonActive092=LoadImage("UI/button_active_092.png");
        string[] riftNames={"rift_dart","rift_wisp","rift_mauler","rift_prism","rift_stalker"};
        for(int i=0;i<riftNames.Length;i++) _riftEnemies[i]=LoadImage($"Rift/{riftNames[i]}.png");
        string[] edgeNames={"scout","interceptor","heavy","turret","warden","sentinel",
            "rift_dart","rift_wisp","rift_mauler","rift_prism","rift_stalker"};
        for(int i=0;i<edgeNames.Length;i++)
            _enemyEdgeMasks[i]=LoadImage($"FX/EnemyEdges/{edgeNames[i]}_edge.png");
        _riftHazard=LoadImage("Rift/rift_hazard.png");
        _riftGuardianSprite=LoadImage("Rift/rift_guardian_boss.png");
        _riftColossusSprite=LoadImage("Rift/rift_colossus_boss.png");
        _riftBossTurretSprite=LoadImage("Rift/rift_boss_turret.png");
        string[] depthNames={"far","mid","near"};
        for(int layer=0;layer<3;layer++)
        for(int rock=0;rock<12;rock++)
            _riftDofRocks[layer,rock]=LoadImage($"Rift/rift_dof_{depthNames[layer]}_{rock:00}.png");
        for(int layer=0;layer<3;layer++)
        for(int variant=0;variant<4;variant++)
            _riftDrifterArt[layer,variant]=LoadImage($"Rift/Drifters/rift_{depthNames[layer]}_{variant:00}.png");
        // Depth-of-field wreck drift: only 8 small objects, mostly recognizable metal wrecks.
        // Avoid huge asteroid blobs and keep the combat lanes and Rift station readable.
        for(int layer=0;layer<3;layer++)
        {
            int count=layer==2?2:3;
            for(int i=0;i<count;i++)
            {
                int artVariant=i==2?(layer==0?2:3):(i+layer)%2;
                float y=((i+layer)%2==0?FxRand(200f,340f):FxRand(745f,925f));
                _riftDrifters.Add(new RiftDrifter
                {
                    Layer=layer, Variant=artVariant,
                    StartX=FxRand(180f,ViewWidth+530f), Y=y,
                    Speed=layer==0?FxRand(8f,13f):layer==1?FxRand(15f,23f):FxRand(25f,35f),
                    Size=layer==0?FxRand(98f,130f):layer==1?FxRand(135f,172f):FxRand(176f,215f),
                    Phase=FxRand(0f,MathHelper.TwoPi),Rotation=FxRand(-.23f,.23f),
                    Spin=layer==0?FxRand(-.005f,.005f):FxRand(-.009f,.009f)
                });
            }
        }
        _hdPlayer=LoadImage("HD/player_sideview_refit.png");
        _hdPlayerPlume=LoadImage("HD/player_engine_plume.png");
        for(int tier=0;tier<_hullDamageLayers.Length;tier++)
            _hullDamageLayers[tier]=LoadImage($"HD/hull_damage_{tier+1}.png");
        _hdEnemies[0]=LoadImage("HD/enemy_interceptor_stable.png");
        _hdEnemies[1]=LoadImage("HD/enemy_heavy_stable.png");
        _hdEnemies[2]=LoadImage("HD/enemy_scout_stable.png");
        _enemyScorch[0]=LoadImage("HD/scorch_interceptor.png");
        _enemyScorch[1]=LoadImage("HD/scorch_heavy.png");
        _enemyScorch[2]=LoadImage("HD/scorch_scout.png");
        _turretScorch=LoadImage("HD/scorch_turret.png");
        _wardenScorch=LoadImage("HD/scorch_warden.png");
        _sentinelScorch=LoadImage("HD/scorch_sentinel.png");
        for(int i=0;i<_hdRocks.Length;i++)
        {
            _hdRocks[i]=LoadImage($"HD/rock_{i:00}.png");
            _dofRocks[0,i]=LoadImage($"HD/dof_far_{i:00}.png");
            _dofRocks[1,i]=LoadImage($"HD/dof_mid_{i:00}.png");
            _dofRocks[2,i]=LoadImage($"HD/dof_near_{i:00}.png");
        }
        _sentinel=LoadImage("boss_sentinel.png");_logo=LoadImage("logo.png");
        _blueGlow=LoadImage("glow_blue.png");_redGlow=LoadImage("glow_red.png");
        _explosionSheet=LoadImage("FX/explosion_sheet.png");
        _damageSmokeSheet=LoadImage("FX/damage_smoke_sheet.png");
        InitFxSpriteFrames();
        _orangeGlow=LoadImage("glow_orange.png");_purpleGlow=LoadImage("glow_purple.png");
        _droneSprite=LoadImage("support_drone.png");
        _shieldRing=LoadImage("shield_ring.png");
        _shockwaveRing=LoadImage("shockwave_ring.png");
        _pickupWeapon=LoadImage("FX/pickup_weapon.png");
        _pickupRepair=LoadImage("FX/pickup_repair.png");
        _pickupShield=LoadImage("FX/pickup_shield.png");
        _pickupDrone=LoadImage("FX/pickup_drone.png");
        _pickupCredits=LoadImage("FX/pickup_credits.png");
        _pickupRareCache=LoadImage("FX/pickup_rare_cache.png");
        _shopPowerIcon=LoadImage("FX/shop_weapon_power.png");
        _shopEngineIcon=LoadImage("FX/shop_engine_tuning.png");
        _primaryShots[0]=LoadImage("FX/primary_pulse.png");
        _primaryShots[1]=LoadImage("FX/primary_lance.png");
        _primaryShots[2]=LoadImage("FX/primary_arc.png");
        _primaryHits[0]=LoadImage("FX/impact_pulse.png");
        _primaryHits[1]=LoadImage("FX/impact_lance.png");
        _primaryHits[2]=LoadImage("FX/impact_arc.png");
        _primaryShots[3]=LoadImage("FX/primary_gatling.png");
        _primaryHits[3]=LoadImage("FX/impact_gatling.png");
        _primaryHits[4]=LoadImage("FX/impact_beam.png");
        _beamStrip=LoadImage("FX/primary_beam_strip.png");
        _beamMuzzle=LoadImage("FX/primary_beam_muzzle.png");
        _shieldShell=LoadImage("FX/shield_shell.png");
        _turretSprite=LoadImage("defense_turret.png");
        _wardenSprite=LoadImage("mini_warden.png");
        _bestScore=ScoreStore.ReadBest();
        _ui=new UiRenderer(LoadImage("ui_atlas.png"),_pixel);
        ValidateShopArtwork();
        for(int i=0;i<145;i++)
        {
            _stars.Add(new Star
            {
                Position=new Vector2(Rand(0,ViewWidth),Rand(0,ViewHeight)),
                Speed=Rand(55,340),Radius=Rand(1.0f,2.8f),Alpha=Rand(.25f,.9f),Phase=Rand(0,6.28f)
            });
        }
        // Three fixed parallax depths: far / mid / near. All purely decorative.
        // Distinct distances mean distinct blur texture, speed and size distributions.
        int[] layerCounts={5,3,1};
        for(int layer=0;layer<layerCounts.Length;layer++)
        for(int i=0;i<layerCounts[layer];i++)
        {
            bool wreck=(layer==2 && i%2==0) || (layer==1 && i%4==0);
            _parallaxRocks.Add(new ParallaxRock
            {
                StartX=Rand(-420,ViewWidth+480),
                Y=Rand(180,ViewHeight-130),
                Diameter=layer==0?Rand(65,145):layer==1?Rand(90,175):Rand(110,220),
                SpeedFactor=layer==0?Rand(.09f,.18f):layer==1?Rand(.24f,.40f):Rand(.50f,.72f),
                Rotation=Rand(0,MathHelper.TwoPi),
                Spin=layer==0?Rand(-.055f,.055f):layer==1?Rand(-.11f,.11f):Rand(-.17f,.17f),
                Opacity=1f,
                Variant=wreck?_random.Next(10,12):_random.Next(0,10),
                Depth=layer==0?.15f:layer==1?.51f:.84f,
                DepthLayer=layer
            });
        }
        LoadBossFeedback099();
        _audio=new GameAudio(Path.Combine(AppContext.BaseDirectory,"Assets"));
        LoadGuiPreferences();
        _bindings.Load();
        _hasDiskSave=RunSaveStore.Read()!=null;
        InitializeCoopArguments();
        base.LoadContent();
    }

    private Texture2D LoadImage(string filename)
    {
        using var stream=File.OpenRead(Path.Combine(AppContext.BaseDirectory,"Assets",filename));
        return Texture2D.FromStream(GraphicsDevice,stream);
    }

    private void InitFxSpriteFrames()
    {
        int explosionCellW=_explosionSheet.Width/4, explosionCellH=_explosionSheet.Height/2;
        int index=0;
        for(int row=0;row<2;row++)
        for(int col=0;col<4;col++)
            _explosionFrames[index++]=new Rectangle(col*explosionCellW,row*explosionCellH,explosionCellW,explosionCellH);

        int smokeCellW=_damageSmokeSheet.Width/3, smokeCellH=_damageSmokeSheet.Height/4;
        for(int row=0;row<4;row++)
        for(int col=0;col<3;col++)
            _damageSmokeFrames[row,col]=new Rectangle(col*smokeCellW,row*smokeCellH,smokeCellW,smokeCellH);
    }

    private bool KeyPressed(KeyboardState keys, Keys key)
        => keys.IsKeyDown(key) && !_previousKeys.IsKeyDown(key);

    private void SetScreenMode()
    {
        _fullscreen=!_fullscreen;
        ApplyDisplayConfiguration();
        SaveGuiPreferences();
    }

    private void StartNewGame()
    {
        if(!_restoringRun) { RunSaveStore.Delete(); _hasDiskSave=false; }
        _mode=GameMode.Playing;
        _runDifficulty=_selectedDifficulty;
        _audio?.Beam(false);
        _pilot.Position=new Vector2(330,540);
        _pilot.Health=4;_pilot.MaxHealth=4;_pilot.MaxShield=3;_pilot.DroneUpgrade=0;_pilot.PickupMagnetLevel=0;_pilot.WeaponPowerLevel=0;_pilot.EngineLevel=0;_powerShotIndex=0;_pilot.Lives=3;_pilot.WeaponLevel=1;_pilot.PrimaryMode=PrimaryWeaponMode.Pulse;
        _precisionFire=false;_trainingRun=false;
        _beamActive=false;_beamDamageTimer=0f;_beamPhase=0f;_gatlingPhase=0f;_beamTickCount=0;
        _checkpointWeaponLevel=2;_checkpointWeaponMode=PrimaryWeaponMode.Pulse;_checkpointShield=2;
        _pilot.Velocity=Vector2.Zero;
        _pilot.Shield=0;
        _pilot.DroneActive=false;
        _pilot.DronePosition=_pilot.Position+new Vector2(-125,-110);
        _pilot.DroneShotTimer=0;
        _pilot.DroneBlockTimer=0;
        _pilot.ShotTimer=0;_pilot.Invincible=2;
        _pilotBank=0;
        _score=0;_travel=0;_scroll=0;_elapsed=0;_waveClock=0;
        _credits=0;_ownedWeaponsMask=1;_sectorStartScore=0;_sectorCreditsEarned=0;_riftBossStage=0;
        _sectorCreditPickups=0;_sectorWeaponPickups=0;_sectorSupplyPickups=0;_sectorKills=0;
        _sectorRareCaches=0;_rareCacheSpawns=0;_shopRareCaches=0;_shopRank="C";_shopRankBonus=0;
        _shopSelection=0;_shopTab=0;_shopScore=0;_shopEarned=0;_shopLootCount=0;
        _shopWeaponCount=0;_shopSupplyCount=0;_shopKills=0;
        _shopAfterFinalSector=false;_shopFeedback=string.Empty;_shopFeedbackTime=0f;
        _checkpointCredits=0;_checkpointUnlockMask=1;_checkpointLives=3;_checkpointScore=0;
        _checkpointMaxHull=4;_checkpointMaxShield=3;_checkpointDroneUpgrade=0;_checkpointMagnet=0;
        _checkpointWeaponPowerLevel=0;_checkpointEngineLevel=0;_checkpointRareCaches=0;
        _checkpointSectorStartScore=0;_checkpointSectorCreditsEarned=0;
        _checkpointSectorCreditPickups=0;_checkpointSectorWeaponPickups=0;
        _checkpointSectorSupplyPickups=0;_checkpointSectorKills=0;
        _spawnCount=0;_supportDropStage=0;_bossLaunched=false;_shake=0;_flash=0;_hitFeedbackTime=0f;
        _formationNotice=string.Empty;_formationNoticeTime=0;
        _shots.Clear();_enemies.Clear();_particles.Clear();_pickups.Clear();_shockwaves.Clear();
        _primaryImpacts.Clear();_primaryReadouts.Clear();
        _explosionSprites.Clear();_damageSmokes.Clear();_damageSmokeTimer=0;
        _scheduledDeathBursts.Clear();_bossFinaleTimer=0;
        _enemySmokes.Clear();
        _bossBreaches.Clear();
        _combatSparks.Clear();_combatFlashes.Clear();
        _debris.Clear();_laserLanes.Clear();
        _debrisClock=1f;_sectorZone=0;_wardenLaunched=false;_wardenDefeated=false;_checkpointUnlocked=false;
        _riftActive=false;_riftElapsed=0;_riftFade=0;_riftWaveCount=0;_checkpointSector=1;
        _infernoActive=false;_infernoElapsed=0;_infernoFade=0;_infernoWaveCount=0;_infernoPhase=-1;
        Alert("SECTOR 01  -  OUTER FRONTIER",3);
    }

    private void CaptureEconomyCheckpoint()
    {
        _checkpointCredits=_credits;_checkpointUnlockMask=_ownedWeaponsMask;
        _checkpointLives=_pilot.Lives;_checkpointScore=_score;
        _checkpointMaxHull=_pilot.MaxHealth;_checkpointMaxShield=_pilot.MaxShield;
        _checkpointDroneUpgrade=_pilot.DroneUpgrade;_checkpointMagnet=_pilot.PickupMagnetLevel;
        _checkpointWeaponPowerLevel=_pilot.WeaponPowerLevel;
        _checkpointEngineLevel=_pilot.EngineLevel;
        _checkpointRareCaches=_sectorRareCaches;
        _checkpointSectorStartScore=_sectorStartScore;
        _checkpointSectorCreditsEarned=_sectorCreditsEarned;
        _checkpointSectorCreditPickups=_sectorCreditPickups;
        _checkpointSectorWeaponPickups=_sectorWeaponPickups;
        _checkpointSectorSupplyPickups=_sectorSupplyPickups;
        _checkpointSectorKills=_sectorKills;
        SaveBoundary("Checkpoint");
    }

    private void ResumeAtCheckpoint()
    {
        bool wasTraining=_trainingRun;
        DifficultyMode savedDifficulty=_runDifficulty;
        int savedSector=_checkpointSector;
        int savedLevel=_checkpointWeaponLevel;
        PrimaryWeaponMode savedMode=_checkpointWeaponMode;
        int savedShield=_checkpointShield;
        int savedCredits=_checkpointCredits, savedMask=_checkpointUnlockMask;
        int savedMaxHull=_checkpointMaxHull, savedMaxShield=_checkpointMaxShield;
        int savedDrone=_checkpointDroneUpgrade, savedMagnet=_checkpointMagnet;
        int savedPower=_checkpointWeaponPowerLevel, savedEngine=_checkpointEngineLevel, savedRare=_checkpointRareCaches;
        int savedLives=_checkpointLives, savedScore=_checkpointScore;
        int savedStartScore=_checkpointSectorStartScore;
        int savedEarned=_checkpointSectorCreditsEarned;
        int savedLoot=_checkpointSectorCreditPickups;
        int savedWeapons=_checkpointSectorWeaponPickups;
        int savedSupplies=_checkpointSectorSupplyPickups;
        int savedKills=_checkpointSectorKills;
        bool restoringBefore=_restoringRun;
        _restoringRun=true; // Restoring an existing checkpoint must not delete the disk save.
        try { StartNewGame(); }
        finally { _restoringRun=restoringBefore; }
        _trainingRun=wasTraining;
        _runDifficulty=savedDifficulty;
        _checkpointWeaponLevel=savedLevel;_checkpointWeaponMode=savedMode;
        _checkpointShield=savedShield;
        _credits=savedCredits;_ownedWeaponsMask=savedMask|1;
        _pilot.MaxHealth=savedMaxHull;_pilot.MaxShield=savedMaxShield;
        _pilot.DroneUpgrade=savedDrone;_pilot.PickupMagnetLevel=savedMagnet;
        _pilot.WeaponPowerLevel=savedPower;_pilot.EngineLevel=savedEngine;
        _sectorRareCaches=savedRare;
        _pilot.Health=savedMaxHull;
        _pilot.Lives=Math.Max(1,savedLives);_score=savedScore;
        _sectorStartScore=savedStartScore;_sectorCreditsEarned=savedEarned;
        _sectorCreditPickups=savedLoot;_sectorWeaponPickups=savedWeapons;
        _sectorSupplyPickups=savedSupplies;_sectorKills=savedKills;
        _elapsed=68f;
        _scroll=_elapsed*132f;
        _travel=_elapsed;
        _sectorZone=3;
        _wardenLaunched=true;
        _wardenDefeated=true;
        _checkpointUnlocked=true;
        // Already elapsed support opportunities must not be replayed.
        _supportDropStage=6;
        _pilot.WeaponLevel=_checkpointWeaponLevel;
        _pilot.PrimaryMode=OwnsWeapon(_checkpointWeaponMode)?_checkpointWeaponMode:PrimaryWeaponMode.Pulse;
        _pilot.Shield=_checkpointShield;
        _pilot.DroneActive=true;
        _pilot.DronePosition=_pilot.Position+new Vector2(-125,-100);
        _pilot.Invincible=2.8f;
        if(savedSector==2 || savedSector==3)
        {
            if(savedSector==3) BeginInfernoSector();
            else BeginRiftSector();
            if(savedSector==3) { _infernoElapsed=16f;_infernoFade=1f; }
            else { _riftElapsed=16f;_riftFade=1f; }
            _checkpointSector=savedSector;
            // Restore the exact snapshot: BeginRiftSector normally resets sector tally.
            _credits=savedCredits;_ownedWeaponsMask=savedMask|1;
        _pilot.MaxHealth=savedMaxHull;_pilot.MaxShield=savedMaxShield;
        _pilot.DroneUpgrade=savedDrone;_pilot.PickupMagnetLevel=savedMagnet;
        _pilot.WeaponPowerLevel=savedPower;_pilot.EngineLevel=savedEngine;
        _sectorRareCaches=savedRare;
        _pilot.Health=savedMaxHull;
            _pilot.Lives=Math.Max(1,savedLives);_score=savedScore;
            _sectorStartScore=savedStartScore;_sectorCreditsEarned=savedEarned;
            _sectorCreditPickups=savedLoot;_sectorWeaponPickups=savedWeapons;
            _sectorSupplyPickups=savedSupplies;_sectorKills=savedKills;
            CaptureEconomyCheckpoint();
            Alert(savedSector==3?"CHECKPOINT  -  INFERNO BELT":"CHECKPOINT  -  RIFT APPROACH",3.2f);
        }
        else
        {
            _checkpointSector=1;
            CaptureEconomyCheckpoint();
            Alert("CHECKPOINT  -  DEFENSE RING",3.2f);
        }
    }

    private void FinishRun(bool completed)
    {
        if(completed) _score+=_pilot.Lives*1000+_pilot.Health*250;
        if(!CoopActive && !_trainingRun && _score>_bestScore)
        {
            _bestScore=_score;
            ScoreStore.WriteBest(_bestScore);
        }
        _mode=completed?GameMode.Victory:GameMode.GameOver;
        if(completed && !CoopActive && !_trainingRun) { RunSaveStore.Delete(); _hasDiskSave=false; }
    }

    private void Alert(string message,float seconds)
    {
        _message=message;_messageTime=seconds;
    }

    protected override void Update(GameTime gameTime)
    {
        float dt=Math.Min(.05f,(float)gameTime.ElapsedGameTime.TotalSeconds);
        if(_mode==GameMode.Playing && !_settingsOpen && !_helpOpen)
            _hitFeedbackTime=MathF.Max(0f,_hitFeedbackTime-dt);
        _audio?.SetMusicMood(_mode==GameMode.Playing && !_helpOpen && !_settingsOpen && !_difficultyPromptOpen,
            !_helpOpen && !_settingsOpen && HasActiveBossForMusic(),_mode==GameMode.Shop);
        _audio?.Tick(dt);
        SampleMouseInput();
        KeyboardState keys=Keyboard.GetState();
        SampleGamePadInput(dt);
        UpdateGamePadFeedback(dt);
        if(UpdateCoop(dt,keys))
        {
            _previousKeys=keys;_previousPad=_pad;base.Update(gameTime);return;
        }
        if(!IsActive)
        {
            if(_mode==GameMode.Playing) { _mode=GameMode.Paused;_pauseMenuSelection=0; }
            _beamActive=false;
            _beamDamageTimer=0f;
            _audio?.Beam(false);
            _previousKeys=keys;
            _previousPad=_pad;
            base.Update(gameTime);
            return;
        }
        if(!_bindingCapture && !_difficultyPromptOpen && (KeyPressed(keys,Keys.F1)||PadPressed(Buttons.Back)))
        { _helpOpen=!_helpOpen;_settingsOpen=false;_bindingCapture=false;_audio?.Beam(false); }
        if(!_bindingCapture && !_difficultyPromptOpen && (KeyPressed(keys,Keys.F12) || KeyPressed(keys,Keys.O) || PadPressed(Buttons.Y)))
        { _settingsOpen=!_settingsOpen;_helpOpen=false;_bindingCapture=false;_audio?.Beam(false); }
        if(_helpOpen || _settingsOpen)
        {
            if(_settingsOpen && _settingsTab==0 && _resolutionDropdownOpen)
            {
                UpdateResolutionDropdown(keys);
                _previousKeys=keys;
                _previousPad=_pad;
                base.Update(gameTime);
                return;
            }
            if(HandleModalMouseInput())
            {
                _previousKeys=keys;
                _previousPad=_pad;
                base.Update(gameTime);
                return;
            }
            if(!_bindingCapture && MenuBack(keys)) { _helpOpen=false;_settingsOpen=false; }
            else if(_helpOpen)
            {
                if(MenuLeft(keys)) _helpPage=(_helpPage+3)%4;
                if(MenuRight(keys)) _helpPage=(_helpPage+1)%4;
            }
            else if(_settingsOpen)
            {
                if(!_bindingCapture && (KeyPressed(keys,Keys.Tab)||PadPressed(Buttons.RightShoulder)))
                { _settingsTab=(_settingsTab+1)%3;_bindingNotice=""; }
                else if(_settingsTab==1) UpdateControlsMenu(keys);
                else if(_settingsTab==2) UpdateControllerSettings(keys);
                else UpdateAudioSettings(keys);
            }
            _previousKeys=keys;
            _previousPad=_pad;
            base.Update(gameTime);
            return;
        }
        if(_difficultyPromptOpen)
        {
            if(HandleDifficultyMouseInput()) { _previousKeys=keys;_previousPad=_pad;base.Update(gameTime);return; }
            if(MenuLeft(keys)||MenuUp(keys)) _selectedDifficulty=(DifficultyMode)(((int)_selectedDifficulty+2)%3);
            else if(MenuRight(keys)||MenuDown(keys)) _selectedDifficulty=(DifficultyMode)(((int)_selectedDifficulty+1)%3);
            else if(MenuBack(keys)) _difficultyPromptOpen=false;
            else if(MenuConfirm(keys))
            {
                _difficultyPromptOpen=false;
                SaveGuiPreferences();
                StartNewGame();
            }
            _previousKeys=keys;
            _previousPad=_pad;
            base.Update(gameTime);
            return;
        }
        if(_mode!=GameMode.Playing) _audio?.Beam(false);
        if(KeyPressed(keys,Keys.F11)) SetScreenMode();
        if(KeyPressed(keys,Keys.F10)) _performanceOverlay=!_performanceOverlay;
        if(KeyPressed(keys,Keys.F4)) _enemyReadabilityEnabled=!_enemyReadabilityEnabled; // Enemy silhouettes A/B
        if(KeyPressed(keys,Keys.F6)) _dofEnabled=!_dofEnabled; // DOF A/B
        if(KeyPressed(keys,Keys.F8))
        {
            _primaryVfxEnabled=!_primaryVfxEnabled;
            _primaryImpacts.Clear();_primaryReadouts.Clear();
        }
        if(KeyPressed(keys,Keys.F7))
        {
            _combatFxEnabled=!_combatFxEnabled; // Combat FX A/B
            if(!_combatFxEnabled)
            {
                _combatSparks.Clear();_combatFlashes.Clear();_explosionSprites.Clear();
                _enemySmokes.Clear();
            }
        }

        if(_mode==GameMode.Title)
        {
            UpdateBackground(dt,.36f);
            if(MenuUp(keys)) _titleMenuSelection=(_titleMenuSelection+5)%6;
            if(MenuDown(keys)) _titleMenuSelection=(_titleMenuSelection+1)%6;
            if(MenuConfirm(keys) || HandleTitleMouseInput()) ActivateTitleMenu();
        }
        else if(_mode==GameMode.GameOver || _mode==GameMode.Victory)
        {
            UpdateBackground(dt,.42f);
            UpdateParticles(dt);
            UpdateCombatFx(dt);
            UpdatePrimaryImpacts(dt);
        UpdatePrimaryReadouts(dt);
            UpdateExplosionSprites(dt);
            UpdateBossBreaches(dt);
            UpdateDamageSmokeSprites(dt);
            UpdateEnemySmokeSprites(dt);
            UpdateScheduledDeathBursts(dt);
            UpdateShockwaves(dt);
            if(KeyPressed(keys,Keys.R)||MenuConfirm(keys)||MouseHit(new Rectangle(535,709,850,94))) OpenNewGameDifficulty();
            else if(_mode==GameMode.GameOver && (KeyPressed(keys,Keys.C)||PadPressed(Buttons.X)) && _checkpointUnlocked) ResumeAtCheckpoint();
            if(MenuBack(keys)) { _titleMenuSelection=0;_mode=GameMode.Title; }
        }
        else if(_mode==GameMode.Shop)
        {
            UpdateBackground(dt,.16f);
            UpdateShop(dt,keys);
        }
        else if(_mode==GameMode.Paused)
        {
            if(MenuBack(keys)||ActionPressed(keys,GameAction.Pause)) _mode=GameMode.Playing;
            else
            {
                if(MenuUp(keys)) _pauseMenuSelection=(_pauseMenuSelection+3)%4;
                if(MenuDown(keys)) _pauseMenuSelection=(_pauseMenuSelection+1)%4;
                if(MenuConfirm(keys) || HandlePauseMouseInput()) ActivatePauseMenu();
            }
        }
        else if(_mode==GameMode.Playing)
        {
            if(MenuBack(keys)||ActionPressed(keys,GameAction.Pause))
            {
                _mode=GameMode.Paused;
                _pauseMenuSelection=0;
                _beamActive=false;
                _beamDamageTimer=0f;
                _audio?.Beam(false);
            }
            else
            {
                UpdatePlaying(dt,keys);
                _audio?.Beam(_mode==GameMode.Playing && _beamActive);
            }
        }
        _previousKeys=keys;
        _previousPad=_pad;
        base.Update(gameTime);
    }

    private void ActivateTitleMenu()
    {
        _audio?.Effect("shop",0,.34f);
        switch(_titleMenuSelection)
        {
            case 0: OpenNewGameDifficulty(); break;
            case 1:
                if(_hasDiskSave) ContinueSavedRun();
                else if(_checkpointUnlocked) ResumeAtCheckpoint();
                break;
            case 2: _settingsOpen=true;_settingsSelection=0;_settingsTab=0; break;
            case 3: _helpOpen=true;_helpPage=0; break;
            case 4: OpenCoopMenu();break;
            case 5: Exit(); break;
        }
    }

    private void ActivatePauseMenu()
    {
        _audio?.Effect("shop",0,.34f);
        switch(_pauseMenuSelection)
        {
            case 0: _mode=GameMode.Playing;break;
            case 1: _settingsOpen=true;_settingsSelection=0;_settingsTab=0;break;
            case 2: _helpOpen=true;_helpPage=0;break;
            case 3: _titleMenuSelection=0;_mode=GameMode.Title;_audio?.Beam(false);break;
        }
    }

    private void LoadGuiPreferences()
    {
        var v=GuiPreferences.Read();
        _vsyncEnabled=v.Vsync;
        _dofEnabled=v.Dof;
        _combatFxEnabled=v.CombatFx;
        _enemyReadabilityEnabled=v.EnemyOutlines;
        _primaryVfxEnabled=v.WeaponVfx;
        _fullscreen=v.Fullscreen;
        _selectedDifficulty=(DifficultyMode)Math.Clamp(v.Difficulty,0,2);
        _resolutionIndex=Math.Clamp(v.ResolutionIndex,0,ResolutionOptions.Length-1);
        _aspectRatio=ResolutionOptions[_resolutionIndex].Width*10==ResolutionOptions[_resolutionIndex].Height*16 ? 1 : 0;
        _enemyShotGlow=Math.Clamp(v.EnemyShotGlow,0,100);
        _hitEffectStrength=Math.Clamp(v.HitEffectStrength,0,100);
        _msaaRequest=Math.Clamp(v.Antialiasing,0,3);
        _controllerHaptics=v.ControllerHaptics;
        _adaptiveTriggers=v.AdaptiveTriggers;
        _controllerStrength=Math.Clamp(v.ControllerStrength,0,100);
        _bluetoothHaptics=v.BluetoothHaptics;
        ApplyDisplayConfiguration();
    }

    private void SaveGuiPreferences()
        =>GuiPreferences.Write(new GuiPreferences.Settings(_vsyncEnabled,_dofEnabled,
            _combatFxEnabled,_enemyReadabilityEnabled,_primaryVfxEnabled,_fullscreen,(int)_selectedDifficulty,
            _resolutionIndex,_enemyShotGlow,_hitEffectStrength,_msaaRequest,
            _controllerHaptics,_adaptiveTriggers,_controllerStrength,_bluetoothHaptics));

    private static int ShopTabOf(int item)=>item switch
    {
        <=4=>0,
        6 or 7 or 8 or 10 or 11 or 12=>1,
        _=>2
    };
    private int[] ActiveShopItems()=>_shopTab switch
    {
        0=>new[]{0,1,2,3,4},
        1=>new[]{6,7,8,10,11,12},
        _=>new[]{5,9}
    };

    private static int ShopPrice(int selection) => selection switch
    {
        1=>700, 2=>1250, 3=>1850, 4=>2600, 5=>950,
        6=>850, 7=>1100, 8=>1350, 9=>350, 10=>500,
        11=>1700, 12=>800,
        _=>0
    };

    private int EffectiveShopPrice(int selection) => selection switch
    {
        6=>850+(_pilot.MaxShield-3)*500,
        7=>1100+(_pilot.MaxHealth-4)*650,
        8=>1350+_pilot.DroneUpgrade*650,
        10=>500+_pilot.PickupMagnetLevel*380,
        11=>1700+_pilot.WeaponPowerLevel*1050,
        12=>800+_pilot.EngineLevel*650,
        _=>ShopPrice(selection)
    };

    private void OpenSectorShop(bool finalSector)
    {
        _audio?.Beam(false);
        if(_mode==GameMode.Shop) return;
        _mode=GameMode.Shop;
        _shopAfterFinalSector=finalSector;
        _shopSelection=1;_shopTab=0;_shopFeedback=string.Empty;_shopFeedbackTime=0f;
        _shopScore=Math.Max(0,_score-_sectorStartScore);
        _shopRank=ComputeSectorRank(_sectorKills,_pilot.Health,_pilot.MaxHealth,_sectorWeaponPickups+_sectorSupplyPickups+_sectorCreditPickups,_trainingRun);
        int requestedBonus=SectorRankBonus(_shopRank,_riftActive,_infernoActive);
        int walletBefore=_credits;
        AwardCredits(requestedBonus); // only once on opening; never on shop restore
        _shopRankBonus=_credits-walletBefore;
        _shopEarned=_sectorCreditsEarned;
        _shopLootCount=_sectorCreditPickups;
        _shopWeaponCount=_sectorWeaponPickups;
        _shopSupplyCount=_sectorSupplyPickups;
        _shopKills=_sectorKills;_shopRareCaches=_sectorRareCaches;
        _beamActive=false;_beamDamageTimer=0f;
        _shots.Clear();_enemies.Clear();_debris.Clear();_laserLanes.Clear();
        _formationNoticeTime=0f;_messageTime=0f;
        SaveBoundary("Shop");
    }

    private void ShopNotice(string value)
    {
        _shopFeedback=value;
        _shopFeedbackTime=2.5f;
    }

    private void PurchaseSelectedItem()
    {
        int selection=_shopSelection;
        if(selection==0) { ShopNotice("PULSE IS YOUR STARTER WEAPON");return; }
        if(selection>=1 && selection<=4 && OwnsWeapon((PrimaryWeaponMode)selection))
        { ShopNotice("WEAPON ALREADY UNLOCKED");return; }
        if(selection==5 && _pilot.Lives>=6) { ShopNotice("MAXIMUM 6 LIVES");return; }
        if(selection==6 && _pilot.MaxShield>=5) { ShopNotice("MAX SHIELD CAPACITY");return; }
        if(selection==7 && _pilot.MaxHealth>=6) { ShopNotice("MAX HULL CAPACITY");return; }
        if(selection==8 && _pilot.DroneUpgrade>=2) { ShopNotice("MAX DRONE UPGRADE");return; }
        if(selection==9 && _pilot.Shield==_pilot.MaxShield) { ShopNotice("SHIELD ALREADY FULL");return; }
        if(selection==10 && _pilot.PickupMagnetLevel>=2) { ShopNotice("MAX PICKUP RANGE");return; }
        if(selection==11 && _pilot.WeaponPowerLevel>=2) { ShopNotice("MAX WEAPON POWER");return; }
        if(selection==12 && _pilot.EngineLevel>=2) { ShopNotice("MAX ENGINE TUNING");return; }
        int price=EffectiveShopPrice(selection);
        if(_credits<price)
        { ShopNotice("NOT ENOUGH CREDITS  /  NEED "+price);return; }
        _credits-=price;
        _audio?.Effect("shop",.08f);
        if(selection==5)
        {
            _pilot.Lives++;
            ShopNotice("EXTRA LIFE PURCHASED  /  LIVES "+_pilot.Lives);
        }
        else if(selection>=1 && selection<=4)
        {
            _ownedWeaponsMask|=1<<selection;
            ShopNotice(PrimaryWeaponName((PrimaryWeaponMode)selection)+" UNLOCKED");
        }
        else if(selection==6)
        {
            _pilot.MaxShield++;_pilot.Shield=_pilot.MaxShield;
            ShopNotice("SHIELD CAPACITY  "+_pilot.MaxShield+" / 5");
        }
        else if(selection==7)
        {
            _pilot.MaxHealth++;_pilot.Health=_pilot.MaxHealth;
            ShopNotice("MAX HULL  "+_pilot.MaxHealth+" / 6");
        }
        else if(selection==8)
        {
            _pilot.DroneUpgrade++;
            _pilot.DroneActive=true;
            _pilot.DronePosition=_pilot.Position+new Vector2(-125,-100);
            ShopNotice("DRONE UPGRADE  "+_pilot.DroneUpgrade+" / 2");
        }
        else if(selection==9)
        {
            _pilot.Shield=_pilot.MaxShield;
            ShopNotice("SHIELD FULLY RECHARGED");
        }
        else if(selection==10)
        {
            _pilot.PickupMagnetLevel++;
            ShopNotice("PICKUP RANGE  "+_pilot.PickupMagnetLevel+" / 2");
        }
        else if(selection==11)
        {
            _pilot.WeaponPowerLevel++;
            ShopNotice("WEAPON POWER  "+_pilot.WeaponPowerLevel+" / 2");
        }
        else if(selection==12)
        {
            _pilot.EngineLevel++;
            ShopNotice("ENGINE TUNING  "+_pilot.EngineLevel+" / 2");
        }
        SaveBoundary("Shop");
    }

    private void UpdateShop(float dt,KeyboardState keys)
    {
        _shopFeedbackTime=MathF.Max(0f,_shopFeedbackTime-dt);
        if(HandleShopMouseInput()) return;
        if(_mouseValid && _mouse.ScrollWheelValue!=_previousMouse.ScrollWheelValue)
        {
            int[] items=ActiveShopItems();
            int wheelIndex=Math.Max(0,Array.IndexOf(items,_shopSelection));
            _shopSelection=items[(wheelIndex+items.Length+(_mouse.ScrollWheelValue<_previousMouse.ScrollWheelValue?1:-1))%items.Length];
        }
        bool left=MenuLeft(keys);
        bool right=MenuRight(keys);
        if(left||right)
        {
            _shopTab=(_shopTab+(left?2:1))%3;
            _shopSelection=ActiveShopItems()[0];
            _audio?.Effect("shop",0,.20f);
        }
        var visible=ActiveShopItems();
        int pos=Array.IndexOf(visible,_shopSelection);
        if(pos<0)pos=0;
        if(MenuUp(keys))
            _shopSelection=visible[(pos+visible.Length-1)%visible.Length];
        if(MenuDown(keys))
            _shopSelection=visible[(pos+1)%visible.Length];
        if(MenuConfirm(keys)) PurchaseSelectedItem();
        if(KeyPressed(keys,Keys.C)||PadPressed(Buttons.Start))
        {
            if(CoopActive) return; // Party-ready barrier is handled by UpdateCoop.
            if(_shopAfterFinalSector && _infernoActive) FinishRun(true);
            else
            {
                _mode=GameMode.Playing;
                if(_shopAfterFinalSector) BeginInfernoSector();
                else BeginRiftSector();
            }
        }
    }

    private void ShopBox(Rectangle rect,Color edge)
    {
        _ui.Rect(_batch,rect,new Color(3,13,29,238));
        _ui.Frame(_batch,rect,edge,2);
    }

    private Texture2D ShopItemIcon(int shopIndex)
    {
        // Never index _primaryShots[4]: Beam uses the separately loaded muzzle
        // and continuous strip assets instead of a projectile sprite.
        Texture2D? icon=shopIndex switch
        {
            0=>_primaryShots[0],
            1=>_primaryShots[1],
            2=>_primaryShots[2],
            3=>_primaryShots[3],
            4=>_beamMuzzle,
            5 or 7=>_pickupRepair,
            6 or 9=>_pickupShield,
            8=>_pickupDrone,
            10=>_pickupCredits,
            11=>_shopPowerIcon,
            12=>_shopEngineIcon,
            _=>_pickupWeapon
        };
        // Defensive fallback: a future missing optional icon should not crash
        // the game at the sector/shop transition.
        return icon ?? _pickupWeapon ?? _pixel;
    }

    private void ValidateShopArtwork()
    {
        // Fail with a descriptive message during loading, rather than a
        // SpriteBatch.ArgumentNullException after the first sector's boss.
        for(int i=0;i<13;i++)
        {
            Texture2D icon=ShopItemIcon(i);
            if(icon is null || icon.Width<=0 || icon.Height<=0)
                throw new InvalidOperationException($"Shop item {i} has no usable icon texture.");
        }
    }

    private static readonly string[] ShopItemNames={"PULSE", "LANCE", "ARC", "GATLING", "BEAM",
        "EXTRA LIFE", "SHIELD CAPACITY", "HULL CAPACITY", "DRONE UPGRADE", "SHIELD RECHARGE", "PICKUP MAGNET",
        "WEAPON POWER", "ENGINE TUNING"};
    private static readonly string[] ShopItemDescriptions={
        "STANDARD PRIMARY  /  STARTING WEAPON",
        "FAST SHOTS  /  PIERCES TARGETS",
        "SPREAD PLASMA  /  AREA CONTROL",
        "RAPID FIRE  /  WAVE PATTERN",
        "CONTINUOUS LASER  /  HOLD SPACE",
        "GAIN ONE ADDITIONAL LIFE  /  SIX MAXIMUM",
        "RAISE MAX SHIELD CAPACITY +1",
        "RAISE MAX HULL CAPACITY +1",
        "FASTER DRONE  /  MORE DAMAGE",
        "REFILL SHIELD CHARGES TO MAXIMUM",
        "INCREASE PICKUP COLLECTION RANGE",
        "RAISE PRIMARY SHOT DAMAGE / TWO LEVELS",
        "RAISE ENGINE MOVEMENT SPEED / TWO LEVELS"};

private void DrawShop()
{
    DrawGuiBackground();
    Color cyan=new(105,223,252), gold=new(255,204,100), muted=new(172,201,224);
    // A single bold header replaces the old stack of ornamental metal frames.
    _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,163),new Color(2,11,27,243));
    _ui.Rect(_batch,new Rectangle(0,157,ViewWidth,5),cyan*.92f);
    _ui.TextShadow(_batch,_infernoActive?"SECTOR 03 CLEARED":_shopAfterFinalSector?"SECTOR 02 CLEARED":"SECTOR 01 CLEARED",
        new Vector2(70,29),.98f,Color.White);
    _ui.TextShadow(_batch,"MISSION DEBRIEF / SECTOR ARMORY",new Vector2(74,114),.45f,cyan);
    _ui.TextShadow(_batch,_credits.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)+" CR",new Vector2(1828,43),.91f,gold,true);

    // Left: scoreboard, not an icon/card squeezed into the item list.
    DrawCleanPanel(new Rectangle(50,205,418,750),cyan);
    _ui.TextShadow(_batch,"MISSION RESULTS",new Vector2(72,234),.60f,Color.White);
    _batch.Draw(_pickupCredits,new Rectangle(71,318,91,91),Color.White);
    _ui.TextShadow(_batch,"+"+_shopEarned.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),new Vector2(179,326),.75f,gold);
    _ui.TextShadow(_batch,"CREDITS EARNED",new Vector2(182,392),.34f,muted);
    _ui.TextFit(_batch,"RANK  "+_shopRank+"    BONUS +"+_shopRankBonus+" CR",new Vector2(75,431),.43f,386,gold);
    string[] labels={"SCORE GAINED","TOTAL SCORE","CREDIT PICKUPS","WEAPON PICKUPS","SUPPLY PICKUPS","ENEMIES DESTROYED"};
    string[] values={_shopScore.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),_score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),_shopLootCount.ToString(),
        _shopWeaponCount.ToString(),_shopSupplyCount.ToString(),_shopKills.ToString()};
    for(int i=0;i<labels.Length;i++)
    {
        int y=475+i*70;
        if(i%2==0) _ui.Rect(_batch,new Rectangle(67,y-5,385,64),new Color(20,43,70,175));
        _ui.TextFit(_batch,labels[i],new Vector2(77,y),.43f,240,muted);
        _ui.TextFit(_batch,values[i],new Vector2(416,y+1),.49f,175,Color.White,true);
    }

    _ui.TextFit(_batch,"RARE CACHES  "+_shopRareCaches,new Vector2(76,915),.37f,382,gold);

    // Center: three full-sized cards, no tiny 5-item list and no double frames.
    _ui.TextShadow(_batch,"EQUIPMENT ARMORY",new Vector2(512,205),.79f,Color.White);
    string[] tabs={"WEAPONS","SHIP UPGRADES","SUPPORT & LIVES"};
    for(int t=0;t<3;t++)
    {
        int tx=498+t*320;
        Rectangle tab=new(tx,289,309,71);
        DrawMenuButton(tab,tabs[t],t==_shopTab,false,.53f);
    }
    int[] indices=ActiveShopItems();
    int selectedIndex=Array.IndexOf(indices,_shopSelection);
    if(selectedIndex<0) selectedIndex=0;
    int first=Math.Clamp(selectedIndex-1,0,Math.Max(0,indices.Length-3));
    for(int slot=0;slot<3;slot++)
    {
        int index=first+slot;
        if(index>=indices.Length) break;
        int i=indices[index], y=383+slot*183;
        bool chosen=i==_shopSelection;
        bool owned=i<=4 && OwnsWeapon((PrimaryWeaponMode)i);
        bool maxed=i switch {5=>_pilot.Lives>=6,6=>_pilot.MaxShield>=5,
            7=>_pilot.MaxHealth>=6,8=>_pilot.DroneUpgrade>=2,
            9=>_pilot.Shield>=_pilot.MaxShield,10=>_pilot.PickupMagnetLevel>=2,
            11=>_pilot.WeaponPowerLevel>=2,12=>_pilot.EngineLevel>=2,_=>false};
        Rectangle r=new(498,y,958,169);
        _ui.Rect(_batch,r,chosen?new Color(23,62,94,248):new Color(6,23,44,235));
        _ui.Rect(_batch,new Rectangle(r.X,r.Y,chosen?10:4,r.Height),chosen?gold:new Color(53,117,159));
        if(chosen) _ui.Rect(_batch,new Rectangle(r.X+14,r.Bottom-4,r.Width-14,4),gold*.76f);
        Texture2D icon=ShopItemIcon(i);
        _batch.Draw(icon,new Rectangle(r.X+27,r.Y+19,126,126),Color.White);
        _ui.TextFit(_batch,ShopItemNames[i],new Vector2(r.X+175,r.Y+21),.72f,605,chosen?gold:Color.White);
        _ui.TextFit(_batch,ShopItemDescriptions[i],new Vector2(r.X+178,r.Y+100),.40f,730,muted);
        string status=owned?"OWNED":maxed?"MAXED":EffectiveShopPrice(i).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)+" CR";
        Color statusColor=owned?Color.LawnGreen:maxed?muted:_credits>=EffectiveShopPrice(i)?gold:Color.OrangeRed;
        _ui.TextFit(_batch,status,new Vector2(r.Right-138,r.Y+32),.47f,238,statusColor,true);
    }
    _ui.TextShadow(_batch,(first+1)+"-"+Math.Min(first+3,indices.Length)+" / "+indices.Length+" ITEMS   UP / DOWN BROWSE",
        new Vector2(500,946),.39f,cyan);

    // Right: large, stable selected-item illustration and readable explanation.
    DrawCleanPanel(new Rectangle(1480,205,390,750),new Color(163,134,243));
    _ui.TextShadow(_batch,"SELECTED ITEM",new Vector2(1501,239),.56f,Color.White);
    var selectedIcon=ShopItemIcon(_shopSelection);
    _batch.Draw(selectedIcon,new Rectangle(1534,319,284,284),Color.White);
    _ui.TextFit(_batch,ShopItemNames[_shopSelection],new Vector2(1503,640),.73f,347,gold);
    _ui.TextWrap(_batch,ShopItemDescriptions[_shopSelection],new Vector2(1504,706),.43f,344,muted);
    _ui.TextFit(_batch,ShopItemStatLine(_shopSelection),new Vector2(1504,787),.38f,348,cyan);
    bool selectedOwned=_shopSelection<=4 && OwnsWeapon((PrimaryWeaponMode)_shopSelection);
    string priceText=selectedOwned?"OWNED":EffectiveShopPrice(_shopSelection).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)+" CR";
    _ui.TextFit(_batch,priceText,new Vector2(1512,847),.74f,345,gold);
    if(_shopFeedbackTime>0)
        _ui.TextFit(_batch,_shopFeedback,new Vector2(515,912),.39f,650,Color.LawnGreen);
    DrawMenuButton(new Rectangle(1508,908,352,61),_coopClient!=null?"HOST ONLY":ConfirmPrompt+" BUY",false,_coopClient!=null,.45f);
    _ui.Rect(_batch,new Rectangle(0,982,ViewWidth,98),new Color(2,11,27,247));
    _ui.TextShadow(_batch,HorizontalPrompt+" CATEGORY     "+ConfirmPrompt+" BUY",
        new Vector2(750,1007),.41f,cyan,true);
    DrawMenuButton(new Rectangle(1636,990,264,78),ContinuePrompt+(CoopActive?" READY":" NEXT"),true,false,.42f);
}

    private void UpdateBackground(float dt,float factor=1)
    {
        _travel+=dt*factor;
        _scroll+=dt*132*factor;
        foreach(var star in _stars)
        {
            star.Position.X-=star.Speed*dt*factor;
            star.Phase+=dt;
            if(star.Position.X< -15) star.Position=new Vector2(ViewWidth+10,Rand(0,ViewHeight));
        }
    }

    private void UpdatePlaying(float dt, KeyboardState keys)
    {
        _elapsed+=dt; _waveClock-=dt;
        UpdateBackground(dt);
        _shake=Math.Max(0,_shake-dt*27);
        _flash=Math.Max(0,_flash-dt*3.0f);
        _messageTime=Math.Max(0,_messageTime-dt);
        _formationNoticeTime=Math.Max(0,_formationNoticeTime-dt);
        _pilot.Invincible=Math.Max(0,_pilot.Invincible-dt);

        if(ActionPressed(keys,GameAction.WeaponNext))
            CycleOwnedWeapon(1);
        else if(ActionPressed(keys,GameAction.WeaponPrevious))
            CycleOwnedWeapon(-1);
        else if(ActionPressed(keys,GameAction.Pulse)) SetPrimaryWeapon(PrimaryWeaponMode.Pulse);
        else if(ActionPressed(keys,GameAction.Lance)) SetPrimaryWeapon(PrimaryWeaponMode.Lance);
        else if(ActionPressed(keys,GameAction.Arc)) SetPrimaryWeapon(PrimaryWeaponMode.Arc);
        else if(ActionPressed(keys,GameAction.Gatling)) SetPrimaryWeapon(PrimaryWeaponMode.Gatling);
        else if(ActionPressed(keys,GameAction.Beam)) SetPrimaryWeapon(PrimaryWeaponMode.Beam);

        if(KeyPressed(keys,Keys.F3) && !_riftActive && !_infernoActive)
        {
            // Shop demo: enough test funds, marked unranked; does not alter normal economy.
            _trainingRun=true;
            _credits+=3300;
            OpenSectorShop(false);
            _shopFeedback="TEST SHOP  +3300 CR  /  UNRANKED";
            _shopFeedbackTime=4f;
            return;
        }
        if(KeyPressed(keys,Keys.F2) && _riftActive && !_infernoActive && (_riftBossStage==0 || _riftBossStage==2))
        {
            // Developer shortcut for validating the two bosses without replaying Sector 02.
            // All such runs are excluded from the high-score file.
            _trainingRun=true;
            if(_riftBossStage==0) _riftElapsed=MathF.Max(_riftElapsed,GuardianAtSeconds);
            else _riftElapsed=MathF.Max(_riftElapsed,ColossusAtSeconds);
            SpawnRiftBoss(_riftBossStage==2);
            Alert("BOSS TEST  -  UNRANKED",2f);
        }
        if(KeyPressed(keys,Keys.F2) && _infernoActive &&
            (_infernoBossStage==0 || _infernoBossStage==2))
        {
            _trainingRun=true;
            bool titan=_infernoBossStage==2;
            _infernoElapsed=MathF.Max(_infernoElapsed,titan?InfernoTitanAt:InfernoGuardianAt);
            SpawnInfernoBoss(titan);
            Alert("INFERNO BOSS TEST  -  UNRANKED",2.2f);
        }
        if(KeyPressed(keys,Keys.F5) && !_infernoActive)
        {
            // F5 skips one sector per press, always unranked.
            _trainingRun=true;
            if(_riftActive) { BeginInfernoSector();Alert("TEST: SECTOR 03  -  UNRANKED",3f); }
            else { BeginRiftSector();Alert("TEST: SECTOR 02  -  UNRANKED",3f); }
        }

        if(KeyPressed(keys,Keys.F9))
        {
            // Alpha-only stack tester, not a normal upgrade: score becomes unranked.
            _trainingRun=true;
            _pilot.WeaponLevel=(_pilot.WeaponLevel%Pilot.MaxWeaponLevel)+1;
            Alert("TEST ROWS  "+_pilot.WeaponLevel+" / "+Pilot.MaxWeaponLevel+"  -  UNRANKED",2.0f);
        }

        Vector2 direction=FlightDirection(keys);
        _precisionFire=ActionDown(keys,GameAction.Precision) || MousePrecision;
        if(!MovePilotDirectlyWithMouse(direction,dt))
        {
            // Keyboard and controller retain their normal inertia and speed upgrades.
            float speed=(_precisionFire?260f:550f)*(1f+.08f*_pilot.EngineLevel);
            float blend=1f-MathF.Exp(-dt*(direction==Vector2.Zero?14f:9f));
            _pilot.Velocity=Vector2.Lerp(_pilot.Velocity,direction*speed,blend);
            _pilot.Position+=_pilot.Velocity*dt;
            _pilot.Position=new Vector2(MathHelper.Clamp(_pilot.Position.X,105,ViewWidth-145),MathHelper.Clamp(_pilot.Position.Y,110,ViewHeight-115));
            if(_pilot.Position.X<=105 && _pilot.Velocity.X<0 || _pilot.Position.X>=ViewWidth-145 && _pilot.Velocity.X>0) _pilot.Velocity.X=0;
            if(_pilot.Position.Y<=110 && _pilot.Velocity.Y<0 || _pilot.Position.Y>=ViewHeight-115 && _pilot.Velocity.Y>0) _pilot.Velocity.Y=0;
        }
        // Only the hull's rotation changes. No sprite swapping, so it cannot "hop".
        float targetPitch=MathHelper.Clamp(_pilot.Velocity.Y/550f,-1f,1f);
        _pilotBank=MathHelper.Lerp(_pilotBank,targetPitch,Math.Min(1f,dt*6f));
        // Subtle, stable exhaust; sprite itself stays perfectly unchanged.
        if(_random.NextDouble()<.15)
            Thruster(PlayerLocalOffset(new Vector2(-144,2)));

        _pilot.ShotTimer-=dt;
        bool firing=(ActionDown(keys,GameAction.Fire) || MouseFire) && _bossFinaleTimer<=0;
        _beamActive=firing && _pilot.PrimaryMode==PrimaryWeaponMode.Beam;
        _beamPhase+=dt*(9f+_pilot.WeaponLevel*1.8f);
        if(_beamActive)
        {
            // Holding fire drives one continuous damage ray; NEVER spawn seven beam projectiles.
            _beamDamageTimer-=dt;
        }
        else _beamDamageTimer=0f;
        if(firing && !_beamActive && _pilot.ShotTimer<=0f)
        {
            FirePlayerWeapon();
            float t=MathHelper.Clamp((_pilot.WeaponLevel-1)/6f,0f,1f);
            _pilot.ShotTimer=_pilot.PrimaryMode switch
            {
                PrimaryWeaponMode.Lance=>MathHelper.Lerp(.31f,.41f,t),
                PrimaryWeaponMode.Arc=>MathHelper.Lerp(.21f,.265f,t),
                PrimaryWeaponMode.Gatling=>MathHelper.Lerp(.120f,.045f,t),
                _=>MathHelper.Lerp(.165f,.238f,t)
            };
        }

        UpdateSectorDirector(dt);
        UpdateDebris(dt);
        UpdateEnemies(dt);
        UpdateDrone(dt);
        UpdateShots(dt);
        if(_beamActive) UpdateContinuousBeam(dt);
        UpdateLaserLanes(dt);
        UpdatePickups(dt);
        UpdateParticles(dt);
        UpdateCombatFx(dt);
        UpdatePrimaryImpacts(dt);
        UpdatePrimaryReadouts(dt);
        UpdateExplosionSprites(dt);
        UpdateBossBreaches(dt);
        UpdateDamageSmoke(dt);
        UpdateDamageSmokeSprites(dt);
        UpdateEnemyDamageSmoke(dt);
        UpdateEnemySmokeSprites(dt);
        UpdateScheduledDeathBursts(dt);
        UpdateBossFinale(dt);
        UpdateShockwaves(dt);
    }

    private float PlayerRoll => _pilotBank*MaxPilotRoll;

    private Vector2 PlayerLocalOffset(Vector2 offset)
    {
        offset*=CombatZoom; // all attachments track visual ship dimensions
        float c=(float)Math.Cos(PlayerRoll), sn=(float)Math.Sin(PlayerRoll);
        return _pilot.Position+new Vector2(offset.X*c-offset.Y*sn,offset.X*sn+offset.Y*c);
    }

    private bool OwnsWeapon(PrimaryWeaponMode mode) =>
        (_ownedWeaponsMask & (1<<(int)mode))!=0;

    private void CycleOwnedWeapon(int direction)
    {
        int current=(int)_pilot.PrimaryMode;
        for(int step=1;step<=5;step++)
        {
            int index=(current+direction*step+25)%5;
            PrimaryWeaponMode next=(PrimaryWeaponMode)index;
            if(OwnsWeapon(next)) { SetPrimaryWeapon(next); return; }
        }
    }

    private void SetPrimaryWeapon(PrimaryWeaponMode mode)
    {
        if(!OwnsWeapon(mode))
        {
            Alert(PrimaryWeaponName(mode)+" LOCKED  -  BUY IN SECTOR SHOP",2.4f);
            return;
        }
        if(_pilot.PrimaryMode==mode) return;
        _pilot.PrimaryMode=mode;
        _beamActive=false;_beamDamageTimer=0f;
        _pilot.ShotTimer=Math.Min(_pilot.ShotTimer,.05f);
        Alert("PRIMARY  "+PrimaryWeaponName(mode)+"  /  LV "+_pilot.WeaponLevel,1.7f);
        SpawnCombatFlash(PlayerLocalOffset(new Vector2(140,15)),Vector2.UnitX,PrimaryWeaponColor(mode),34f,.13f);
    }

    private static string PrimaryWeaponName(PrimaryWeaponMode mode)=>mode switch
    {
        PrimaryWeaponMode.Lance=>"LANCE",
        PrimaryWeaponMode.Arc=>"ARC",
        PrimaryWeaponMode.Gatling=>"GATLING",
        PrimaryWeaponMode.Beam=>"BEAM",
        _=>"PULSE"
    };

    private static Color PrimaryWeaponColor(PrimaryWeaponMode mode)=>mode switch
    {
        PrimaryWeaponMode.Lance=>new Color(188,236,255),
        PrimaryWeaponMode.Arc=>new Color(104,255,188),
        PrimaryWeaponMode.Gatling=>new Color(255,160,53),
        PrimaryWeaponMode.Beam=>new Color(190,112,255),
        _=>new Color(62,218,255)
    };

    private void FirePlayerWeapon()
    {
        PulseGamePad(_pilot.PrimaryMode==PrimaryWeaponMode.Lance?.25f:.08f,
            _pilot.PrimaryMode==PrimaryWeaponMode.Gatling?.12f:.22f);
        // Pulse/Lance/Arc: exactly one projectile per level. Gatling fires ONE rapid shot;
        // Beam is handled by UpdateContinuousBeam and never creates ordinary shots.
        int level=Math.Clamp(_pilot.WeaponLevel,1,Pilot.MaxWeaponLevel);
        if(_pilot.PrimaryMode==PrimaryWeaponMode.Gatling)
        {
            _audio?.Effect("gatling",.075f,.40f);
            Vector2 muzzle=PlayerLocalOffset(new Vector2(171,24));
            // Both fire rate and wave oscillation frequency increase with every pickup.
            _gatlingPhase+=.52f+.115f*level;
            float swing=MathF.Sin(_gatlingPhase)*(.050f+.017f*level);
            // Tiny light projectile; high cadence but low single-hit damage.
            Vector2 velocity=new Vector2(MathF.Cos(swing),MathF.Sin(swing))*1550f;
            CreateShot(muzzle,velocity,true,1,5f,PrimaryWeaponColor(PrimaryWeaponMode.Gatling));
            _shots[^1].PrimaryMode=PrimaryWeaponMode.Gatling;
            _shots[^1].Duration=1.6f;
            _shots[^1].Damage+=NextPrimaryPowerBonus();
            if(_combatFxEnabled) SpawnCombatFlash(muzzle,Vector2.UnitX,PrimaryWeaponColor(PrimaryWeaponMode.Gatling),13f,.055f);
            return;
        }
        PrimaryWeaponMode mode=_pilot.PrimaryMode;
        _audio?.Effect(mode==PrimaryWeaponMode.Lance?"lance":mode==PrimaryWeaponMode.Arc?"arc":"pulse",.105f,.38f);
        Vector2 barrel=PlayerLocalOffset(new Vector2(167,24));
        float center=(level-1)*.5f;
        float spacing=mode==PrimaryWeaponMode.Lance?15f:
            mode==PrimaryWeaponMode.Arc?(_precisionFire?16f:21f):(_precisionFire?16f:24f);
        Color tint=PrimaryWeaponColor(mode);
        for(int i=0;i<level;i++)
        {
            float row=i-center;
            // Give each row a distinct launch position; not a three-shot jump at level 2.
            Vector2 muzzle=barrel+new Vector2(-Math.Abs(row)*3.5f,row*spacing);
            Vector2 velocity;
            float radius;
            int damage;
            int hits=1;
            switch(mode)
            {
                case PrimaryWeaponMode.Lance:
                    velocity=new Vector2(1510f,0f);
                    radius=7f;
                    damage=level>=5?2:1;
                    hits=level>=4?3:2; // Can strike different targets, never repeated hits on one.
                    break;
                case PrimaryWeaponMode.Arc:
                    float angle=row*(_precisionFire ? 0.028f : 0.053f);
                    velocity=new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*995f;
                    radius=11f;
                    damage=1;
                    break;
                default:
                    velocity=new Vector2(1190f,0f);
                    radius=9f;
                    damage=1;
                    break;
            }
            CreateShot(muzzle,velocity,true,damage,radius,tint);
            Projectile shot=_shots[^1];
            shot.PrimaryMode=mode;
            shot.Damage+=NextPrimaryPowerBonus();
            shot.HitsRemaining=hits;
            shot.Duration=mode==PrimaryWeaponMode.Lance?1.7f:2.2f;
        }
        // Muzzle feedback only; no extra projectile or independent sidearm.
        SpawnCombatFlash(barrel,Vector2.UnitX,tint,mode==PrimaryWeaponMode.Lance?42f:30f,.10f);
        if(_combatFxEnabled)
            SpawnCombatBurst(barrel,tint,3+level/2,90f,210f);
    }

    private void CreateShot(Vector2 position,Vector2 velocity,bool friendly,int damage,float radius,Color color)
    {
        if(!friendly) velocity*=DifficultyProjectileSpeed;
        // Fixed maximum projectile count; discard oldest hostile shots first under overload.
        if(_shots.Count>=1100)
        {
            int oldestHostile=_shots.FindIndex(s=>!s.Friendly);
            if(oldestHostile>=0) _shots.RemoveAt(oldestHostile);
            else _shots.RemoveAt(0);
        }
        _shots.Add(new Projectile{Position=position,PreviousPosition=position,Velocity=velocity,Friendly=friendly,OwnerPlayerId=_coopActorId,Damage=damage,Radius=radius,Tint=color});
    }

    private void SpawnPrimaryImpact(Projectile projectile,Vector2 point)
    {
        if(!_primaryVfxEnabled || projectile.IsDroneShot || !projectile.Friendly) return;
        if(_primaryImpacts.Count>=96) _primaryImpacts.RemoveAt(0);
        float duration=projectile.PrimaryMode switch
        {
            PrimaryWeaponMode.Lance=>.16f,
            PrimaryWeaponMode.Arc=>.24f,
            PrimaryWeaponMode.Gatling=>.12f,
            PrimaryWeaponMode.Beam=>.115f,
            _=>.19f
        };
        _primaryImpacts.Add(new PrimaryImpact
        {
            Position=point,
            Mode=projectile.PrimaryMode,
            Age=0f,
            Life=duration,
            Rotation=projectile.PrimaryMode==PrimaryWeaponMode.Arc ? FxRand(-.24f,.24f) : 0f,
            Size=projectile.PrimaryMode==PrimaryWeaponMode.Arc ? .62f :
                 projectile.PrimaryMode==PrimaryWeaponMode.Gatling ? .28f :
                 projectile.PrimaryMode==PrimaryWeaponMode.Beam ? .48f : .50f
        });
    }

    private void RecordPrimaryDamage(Enemy target,int damage,Projectile shot)
    {
        CoopRecordDamage(shot,damage);
        if(!_primaryVfxEnabled || shot.IsDroneShot || !shot.Friendly) return;
        for(int i=_primaryReadouts.Count-1;i>=0;i--)
        {
            PrimaryDamageReadout existing=_primaryReadouts[i];
            if(existing.Target==target && existing.Mode==shot.PrimaryMode && existing.Age<.17f)
            {
                existing.Damage+=damage;
                existing.Age=0;
                existing.Position=target.Position+new Vector2(0f,-target.Radius-24f);
                return;
            }
        }
        if(_primaryReadouts.Count>=36) _primaryReadouts.RemoveAt(0);
        _primaryReadouts.Add(new PrimaryDamageReadout
        {
            Target=target,Mode=shot.PrimaryMode,Damage=damage,
            Position=target.Position+new Vector2(0f,-target.Radius-24f)
        });
    }

    private void UpdatePrimaryReadouts(float dt)
    {
        for(int i=_primaryReadouts.Count-1;i>=0;i--)
        {
            PrimaryDamageReadout item=_primaryReadouts[i];
            item.Age+=dt;
            item.Position+=new Vector2(0f,-32f*dt);
            if(item.Age>=item.Life) _primaryReadouts.RemoveAt(i);
        }
    }

    private void DrawPrimaryReadouts()
    {
        if(!_primaryVfxEnabled) return;
        foreach(PrimaryDamageReadout item in _primaryReadouts)
        {
            float t=MathHelper.Clamp(item.Age/item.Life,0f,1f);
            float opacity=t<.60f?1f:MathHelper.Clamp((1f-t)/.40f,0f,1f);
            Color color=PrimaryWeaponColor(item.Mode)*opacity;
            _ui.Text(_batch,item.Damage.ToString(),item.Position,
                item.Mode==PrimaryWeaponMode.Lance ? 0.38f : 0.32f,color,true);
        }
    }

    private void UpdatePrimaryImpacts(float dt)
    {
        for(int i=_primaryImpacts.Count-1;i>=0;i--)
        {
            _primaryImpacts[i].Age+=dt;
            if(_primaryImpacts[i].Age>=_primaryImpacts[i].Life) _primaryImpacts.RemoveAt(i);
        }
    }

    private void DrawContinuousBeam()
    {
        float length=_beamEnd.X-_beamStart.X;
        if(length<=0) return;
        int lvl=Math.Clamp(_pilot.WeaponLevel,1,Pilot.MaxWeaponLevel);
        float width=8f+3.4f*(lvl-1); // one widening ray, NOT seven separate beams
        float pulse=.78f+.22f*MathF.Sin(_beamPhase*2.15f);
        Color tint=PrimaryWeaponColor(PrimaryWeaponMode.Beam);
        if(_primaryVfxEnabled)
        {
            // PNG beam texture tiles repeatedly; pulse and wave are embedded in each strip.
            // A non-scaling segment width prevents the texture from turning into a flat rectangle.
            for(float x=_beamStart.X;x<_beamEnd.X;x+=192f)
            {
                int widthPx=(int)MathF.Min(192f,_beamEnd.X-x);
                if(widthPx<=0) break;
                var dstRect=new Rectangle((int)x,(int)(_beamStart.Y-width*1.30f),widthPx,(int)(width*2.60f));
                var srcRect=new Rectangle(0,0,widthPx,_beamStrip.Height);
                _batch.Draw(_beamStrip,dstRect,srcRect,Color.White*pulse);
            }
            _batch.Draw(_beamMuzzle,_beamStart,null,Color.White*pulse,0f,
                new Vector2(_beamMuzzle.Width*.5f,_beamMuzzle.Height*.5f),.70f+lvl*.04f,SpriteEffects.None,0f);
        }
        DrawBeam(_beamStart,_beamEnd,MathF.Max(2f,width*.24f),new Color(222,189,255)*pulse);
        DrawBeam(_beamStart,_beamEnd,MathF.Max(1.5f,width*.11f),Color.White*(pulse*.87f));
    }

    private void DrawPrimaryImpactSprites()
    {
        if(!_primaryVfxEnabled) return;
        foreach(PrimaryImpact impact in _primaryImpacts)
        {
            Texture2D texture=_primaryHits[(int)impact.Mode];
            float t=MathHelper.Clamp(impact.Age/impact.Life,0f,1f);
            float strength=t<.20f ? .75f+t*1.25f : 1f-(t-.20f)/.80f;
            _batch.Draw(texture,impact.Position,null,Color.White*MathHelper.Clamp(strength,0f,1f),impact.Rotation,
                new Vector2(texture.Width*.5f,texture.Height*.5f),impact.Size*(.75f+.42f*t),SpriteEffects.None,0f);
        }
    }

    private void UpdateSectorDirector(float dt)
    {
        if(_infernoActive) { UpdateInfernoSector(dt); return; }
        if(_riftActive)
        {
            UpdateRiftSector(dt);
            return;
        }
        // Timed environmental pacing with a kill-gated miniboss.
        if(_elapsed>=26f && _sectorZone==0)
        {
            _sectorZone=1;
            Alert("DEBRIS FIELD  -  AVOID COLLISIONS",3.3f);
        }
        if(_elapsed>=54f && !_wardenLaunched)
        {
            _wardenLaunched=true;
            _sectorZone=2;
            _enemies.Clear();
            _shots.RemoveAll(s=>!s.Friendly);
            _debris.Clear();
            SpawnWarden();
            Alert("INCOMING  -  WARDEN FRIGATE",3.5f);
        }
        if(_wardenDefeated && _sectorZone==2)
        {
            _sectorZone=3;
            _checkpointUnlocked=true;
            _waveClock=1.25f;
            _shots.RemoveAll(s=>!s.Friendly);
            _debris.Clear();
            _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+1);
            _checkpointWeaponLevel=_pilot.WeaponLevel;
            _checkpointWeaponMode=_pilot.PrimaryMode;
            _checkpointShield=_pilot.Shield;
            CaptureEconomyCheckpoint();
            Alert("CHECKPOINT REACHED  -  DEFENSE RING",3.6f);
        }

        bool wardenBlocking=_wardenLaunched && !_wardenDefeated;
        if(!_bossLaunched && !wardenBlocking && _elapsed<BossAtSeconds && _waveClock<=0)
        {
            if(SpawnWave())
                _waveClock=_sectorZone switch { 0=>2.7f, 1=>3.05f, _=>1.85f };
            else
                _waveClock=1.1f; // crowded screens retry shortly without advancing wave index
        }
        // Both major regions include moving, destructible asteroids.
        bool inDebris=(_sectorZone==1 || _sectorZone==3) && !_bossLaunched;
        if(inDebris)
        {
            _debrisClock-=dt;
            if(_debrisClock<=0)
            {
                SpawnDebris();
                _debrisClock=_sectorZone==1?1.75f:2.5f;
            }
        }
        SpawnScheduledSupport();
        if(!_bossLaunched && _wardenDefeated && _elapsed>=BossAtSeconds)
        {
            _bossLaunched=true;
            _enemies.Clear();
            _debris.Clear();
            _shots.RemoveAll(s=>!s.Friendly);
            SpawnBoss();
            Alert("FINAL DEFENSE  -  SENTINEL CORE",4f);
        }
    }

    private static bool IsRiftEnemy(EnemyKind kind) =>
        kind>=EnemyKind.RiftDart && kind<=EnemyKind.RiftStalker;

    private void BeginRiftSector()
    {
        _riftActive=true;
        _riftElapsed=0f;_riftFade=0f;_riftWaveCount=0;_riftBossStage=0;
        _bossLaunched=false;_bossFinaleTimer=0f;
        _waveClock=2.6f;_debrisClock=1.6f;
        _enemies.Clear();_debris.Clear();_laserLanes.Clear();_shots.RemoveAll(p=>!p.Friendly);
        _pilot.Health=Math.Min(_pilot.MaxHealth,_pilot.Health+1);
        _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+1);
        _pilot.Invincible=3f;
        _checkpointUnlocked=true;
        _checkpointSector=2;
        _checkpointWeaponLevel=_pilot.WeaponLevel;
        _checkpointWeaponMode=_pilot.PrimaryMode;
        _checkpointShield=_pilot.Shield;
        // New stage starts a new tally, but wallet and unlocked guns carry forward.
        _sectorStartScore=_score;_sectorCreditsEarned=0;
        _sectorCreditPickups=0;_sectorWeaponPickups=0;_sectorSupplyPickups=0;_sectorKills=0;
        _sectorRareCaches=0;_rareCacheSpawns=0;
        CaptureEconomyCheckpoint();
        _audio?.StartTransitionDuck();
        _audio?.Effect("sector_transition",1.5f,.75f);
        Alert("SECTOR 02  -  THE RIFT",4.2f);
        _formationNotice="ALIEN CONTACTS DETECTED";
        _formationNoticeTime=3.8f;
    }

    private void UpdateRiftSector(float dt)
    {
        _riftElapsed+=dt;
        _riftFade=MathHelper.Clamp(_riftFade+dt*.29f,0f,1f);
        // The Rift no longer auto-clears: players must defeat both unique bosses.
        if(_riftBossStage==0 && _riftElapsed>=GuardianAtSeconds)
        {
            SpawnRiftBoss(false);
            return;
        }
        if(_riftBossStage==2 && _riftElapsed>=ColossusAtSeconds)
        {
            SpawnRiftBoss(true);
            return;
        }
        bool activeBoss=_riftBossStage==1 || _riftBossStage==3 || _riftBossStage==4;
        if(!activeBoss && _riftElapsed<ColossusAtSeconds && _waveClock<=0f)
        {
            if(_enemies.Count<16)
            {
                SpawnRiftWave();
                _waveClock=_riftElapsed<45f?3.7f:_riftElapsed<82f?3.05f:2.55f;
            }
            else _waveClock=1.05f;
        }
        if(!activeBoss && _riftElapsed<ColossusAtSeconds-7f)
        {
            _debrisClock-=dt;
            if(_debrisClock<=0)
            {
                SpawnRiftHazard();
                _debrisClock=_riftElapsed<55f?4.4f:3.6f;
            }
        }
        // Final clearance comes only from Rift Colossus death -> staged explosion -> shop.
    }

    private void SpawnRiftBoss(bool finalBoss)
    {
        _riftBossStage=finalBoss?3:1;
        _enemies.Clear();_debris.Clear();_laserLanes.Clear();
        _shots.RemoveAll(p=>!p.Friendly);
        Enemy leader=new()
        {
            Kind=finalBoss?EnemyKind.RiftColossus:EnemyKind.RiftGuardian,
            Position=new Vector2(ViewWidth+365f,540f),BaseY=540f,
            Speed=finalBoss?100f:153f,Radius=finalBoss?164f:126f,
            Health=finalBoss?310:128,MaxHealth=finalBoss?310:128,
            FireTimer=1.65f
        };
        _enemies.Add(leader);
        // Independently destructible weapon pylons; destroying a pylon disables its fire.
        for(int i=0;i<(finalBoss?4:2);i++)
        {
            float angle=(i+.5f)*(MathHelper.TwoPi/(finalBoss?4:2));
            Vector2 offset=finalBoss?
                new Vector2((float)Math.Cos(angle)*130f,(float)Math.Sin(angle)*165f):
                new Vector2(-46f,i==0?-136f:136f);
            _enemies.Add(new Enemy
            {
                Kind=EnemyKind.RiftBossTurret,Parent=leader,MountOffset=offset,
                Position=leader.Position+offset,BaseY=540f,Health=finalBoss?25:18,
                MaxHealth=finalBoss?25:18,Radius=29f,FireTimer=2.1f+i*.33f,
                Speed=0f
            });
        }
        _waveClock=3f;
        Alert(finalBoss?"WARNING  -  RIFT COLOSSUS":"RIFT GUARDIAN  -  APPROACHING",4f);
        _formationNotice=finalBoss?"RIFT COLOSSUS":"RIFT GUARDIAN";
        _formationNoticeTime=3f;
        _pilot.Invincible=MathF.Max(_pilot.Invincible,1.8f);
    }

    private void FireRiftBoss(Enemy boss)
    {
        int phase=boss.LastPhase;
        bool finalBoss=boss.Kind==EnemyKind.RiftColossus;
        int count=finalBoss?phase==3?11:phase==2?9:7:phase==2?7:5;
        float totalSpread=finalBoss?phase==3?2.20f:1.68f:phase==2?1.60f:1.08f;
        Vector2 root=boss.Position+new Vector2(finalBoss?-178f:-135f,0f);
        float aim=MathF.Atan2(_pilot.Position.Y-root.Y,_pilot.Position.X-root.X);
        for(int i=0;i<count;i++)
        {
            float a=aim+(i-(count-1)*.5f)*(totalSpread/Math.Max(1,count-1));
            Vector2 dir=new(MathF.Cos(a),MathF.Sin(a));
            CreateShot(root,dir*(finalBoss?phase==3?430f:380f:375f),false,1,11f,Color.MediumPurple);
        }
        SpawnCombatFlash(root,-Vector2.UnitX,Color.MediumPurple,70f,.19f);
        if(phase>=2 && ++boss.FireCycle%3==0 && _laserLanes.Count==0)
        {
            // Long warning gives players time to exit the announced horizontal lane.
            _laserLanes.Add(new LaserLane{Y=MathHelper.Clamp(_pilot.Position.Y,220f,ViewHeight-160f),Warning=1.35f,Active=.63f});
            Alert("WARNING  -  RIFT ENERGY BEAM",1.15f);
            _audio?.Effect("boss_warning",.6f,.70f);
        }
        if(finalBoss && phase==3 && boss.FireCycle%3==1)
        {
            SpawnRiftEnemy(EnemyKind.RiftDart,new Vector2(ViewWidth+90f,250f),0f,250f);
            SpawnRiftEnemy(EnemyKind.RiftDart,new Vector2(ViewWidth+180f,835f),1.0f,835f);
        }
    }

    private void SpawnRiftHazard()
    {
        if(_debris.Count>=5) return;
        float radius=Rand(41,72);
        _debris.Add(new Debris
        {
            IsRiftShard=true,Position=new Vector2(ViewWidth+radius+35,Rand(210,ViewHeight-175)),
            Velocity=new Vector2(-Rand(175,265),Rand(-15,15)),
            Radius=radius,Health=radius>56?4:3,Variant=0,
            Rotation=Rand(0,MathHelper.TwoPi),Spin=Rand(-.8f,.8f)
        });
    }

    private void SpawnRiftWave()
    {
        int pattern=_riftWaveCount++%7;
        if(_riftWaveCount==8 && _rareCacheSpawns==0)
        {
            SpawnRareCreditCache(new Vector2(ViewWidth-130,MathHelper.Clamp(_pilot.Position.Y+110f,255f,835f)),210);
            _rareCacheSpawns=1;
        }
        int count=pattern==5?3:pattern==6?5:pattern==3?4:6;
        _formationNotice=pattern switch
        {
            0=>"RIFT DART SWARM",1=>"WISP PHASE FLIGHT",2=>"MAULER ESCORT",
            3=>"PRISM LASER GRID",4=>"STALKER PINCER",5=>"HEAVY BREAKTHROUGH",
            _=>"ALIEN MIXED FORMATION"
        };
        _formationNoticeTime=2f;
        if(_riftWaveCount%3==0)
            SpawnCreditPickup(new Vector2(ViewWidth-210,Rand(270,ViewHeight-200)),110);
        // A weapon supply route survives into sector two; each pickup still grants exactly +1.
        if(_riftWaveCount%4==0 && _pilot.WeaponLevel<Pilot.MaxWeaponLevel)
            _pickups.Add(new Pickup
            {
                Kind=PickupKind.Weapon,
                Position=new Vector2(ViewWidth+90f,MathHelper.Clamp(_pilot.Position.Y,230f,830f)),
                Velocity=new Vector2(-78f,0f)
            });
        float target=MathHelper.Clamp(_pilot.Position.Y,280f,795f);
        for(int i=0;i<count;i++)
        {
            EnemyKind kind=pattern switch
            {
                0=>EnemyKind.RiftDart,1=>EnemyKind.RiftWisp,
                2=>i==count/2?EnemyKind.RiftMauler:EnemyKind.RiftDart,
                3=>EnemyKind.RiftPrism,4=>EnemyKind.RiftStalker,
                5=>i==1?EnemyKind.RiftMauler:EnemyKind.RiftPrism,
                _=>i%3==0?EnemyKind.RiftStalker:i%3==1?EnemyKind.RiftWisp:EnemyKind.RiftDart
            };
            float y=pattern==4?(i%2==0?260f:835f):
                pattern==6?290+i*115:295+(i%3)*220+(i/3)*42;
            float lockedY=MathHelper.Clamp(target+(i-(count-1)*.5f)*49f,200f,890f);
            SpawnRiftEnemy(kind,new Vector2(ViewWidth+140+i*125,y),i*.68f,lockedY);
        }
    }

    private void SpawnRiftEnemy(EnemyKind kind,Vector2 pos,float phase,float targetY)
    {
        int hp=kind switch
        {
            EnemyKind.RiftDart=>3,EnemyKind.RiftWisp=>4,EnemyKind.RiftMauler=>12,
            EnemyKind.RiftPrism=>6,_=>5
        };
        float size=kind==EnemyKind.RiftMauler?73f:kind==EnemyKind.RiftPrism?48f:40f;
        float speed=kind switch
        {
            EnemyKind.RiftDart=>355f,EnemyKind.RiftWisp=>286f,
            EnemyKind.RiftMauler=>158f,EnemyKind.RiftPrism=>215f,_=>340f
        };
        _enemies.Add(new Enemy
        {
            Kind=kind,Position=pos,BaseY=pos.Y,TargetY=targetY,
            Speed=speed,Radius=size,Health=hp,MaxHealth=hp,Phase=phase,
            FireTimer=Rand(1.0f,2.3f),MotionPattern=20+(int)kind-(int)EnemyKind.RiftDart
        });
    }

    private void UpdateRiftEnemy(Enemy e,float dt)
    {
        e.Position.X-=e.Speed*dt;
        float t=MathHelper.Clamp((e.Age-.10f)/2.1f,0f,1f);
        switch(e.Kind)
        {
            case EnemyKind.RiftDart:
                e.Position.Y=e.BaseY+MathF.Sin(e.Age*4.7f+e.Phase)*55f;
                break;
            case EnemyKind.RiftWisp:
                e.Position.Y=e.BaseY+MathF.Sin(e.Age*3.1f+e.Phase)*116f;
                break;
            case EnemyKind.RiftMauler:
                e.Position.Y=e.BaseY+MathF.Sin(e.Age*1.7f+e.Phase)*32f;
                break;
            case EnemyKind.RiftPrism:
                e.Position.Y=e.BaseY+MathF.Sin(e.Age*2.35f+e.Phase)*64f;
                break;
            case EnemyKind.RiftStalker:
                t=t*t*(3f-2f*t);
                e.Position.Y=MathHelper.Lerp(e.BaseY,e.TargetY,t)+MathF.Sin(e.Age*5.1f+e.Phase)*19f;
                break;
        }
        e.Position.Y=MathHelper.Clamp(e.Position.Y,165f,920f);
        e.FireTimer-=dt/DifficultyShotTimeFactor;
        if(e.Position.X>ViewWidth-140 || e.Position.X<150 || e.FireTimer>0) return;
        Vector2 origin=e.Position+new Vector2(-48,0);
        if(e.Kind==EnemyKind.RiftDart) AimFire(origin,new Vector2(-460f,0),8f);
        else if(e.Kind==EnemyKind.RiftWisp)
        {
            for(int i=-1;i<=1;i+=2)
                CreateShot(origin+new Vector2(0,i*17),new Vector2(-425f,i*135f),false,1,9f,Color.MediumPurple);
        }
        else if(e.Kind==EnemyKind.RiftMauler)
        {
            for(int i=-1;i<=1;i++)
                CreateShot(origin+new Vector2(0,i*26),new Vector2(-375f,i*95f),false,1,13f,Color.OrangeRed);
        }
        else if(e.Kind==EnemyKind.RiftPrism)
        {
            Vector2 dir=_pilot.Position-origin;
            if(dir.LengthSquared()<1f) dir=-Vector2.UnitX;
            dir.Normalize();
            for(int i=-1;i<=1;i++)
                CreateShot(origin,new Vector2(dir.X*445f,dir.Y*445f+i*65f),false,1,10f,Color.MediumPurple);
            SpawnCombatFlash(origin,dir,Color.MediumPurple,35f,.16f);
        }
        else
        {
            AimFire(origin,new Vector2(-480f,0),12f);
            CreateShot(origin+new Vector2(0,24),new Vector2(-460f,115f),false,1,8f,Color.OrangeRed);
        }
        e.FireTimer=e.Kind==EnemyKind.RiftMauler?1.7f:e.Kind==EnemyKind.RiftPrism?2.2f:e.Kind==EnemyKind.RiftStalker?2f:2.75f;
    }

    private void SpawnDebris()
    {
        if(_debris.Count>=9) return;
        float r=Rand(39,82);
        float y=Rand(205,ViewHeight-175);
        _debris.Add(new Debris
        {
            Position=new Vector2(ViewWidth+r+25,y),
            Velocity=new Vector2(-Rand(205,345),Rand(-27,27)),
            Radius=r,Spin=Rand(-1.2f,1.2f),Rotation=Rand(0,6.28f),
            Health=r>58?4:2,Variant=_random.Next(_hdRocks.Length)
        });
    }

    private void UpdateDebris(float dt)
    {
        for(int i=_debris.Count-1;i>=0;i--)
        {
            Debris d=_debris[i];
            d.Position+=d.Velocity*dt;
            d.Rotation+=d.Spin*dt;
            if(d.Position.X< -d.Radius-30)
            {
                _debris.RemoveAt(i);
                continue;
            }
            if(HurtCoopCircle(d.Position,d.Radius*.82f))
            {
                d.Health=0;
                Boom(d.Position,15,Color.Orange);
                _debris.RemoveAt(i);
            }
        }
    }

    private void SpawnWarden()
    {
        _enemies.Add(new Enemy
        {
            Kind=EnemyKind.Warden,Position=new Vector2(ViewWidth+330,540),
            BaseY=540,Speed=180,Radius=142,Health=86,MaxHealth=86,FireTimer=1.5f
        });
    }

    private void UpdateLaserLanes(float dt)
    {
        for(int i=_laserLanes.Count-1;i>=0;i--)
        {
            LaserLane laser=_laserLanes[i];
            laser.Age+=dt;
            if(laser.Age>=laser.Warning+laser.Active)
            {
                _laserLanes.RemoveAt(i);
                continue;
            }
            if(laser.Age>=laser.Warning) HurtCoopLane(laser.Y);
        }
    }

    private void SpawnScheduledSupport()
    {
        // Six guaranteed weapon opportunities across Sector One: collecting all can reach L7.
        // Defensive supplies remain available; random enemy drops are additional.
        float[] times={10f,20f,31f,43f,55f,68f,80f,92f,104f};
        PickupKind[] kinds={PickupKind.Weapon,PickupKind.Weapon,PickupKind.Weapon,PickupKind.Drone,
                            PickupKind.Weapon,PickupKind.Repair,PickupKind.Weapon,PickupKind.Weapon,PickupKind.Credits};
        if(_supportDropStage>=times.Length || _elapsed<times[_supportDropStage]) return;
        PickupKind kind=kinds[_supportDropStage++];
        Vector2 location=new(Math.Min(ViewWidth-220,_pilot.Position.X+540),MathHelper.Clamp(_pilot.Position.Y+Rand(-110,110),220,870));
        if(kind==PickupKind.Credits) SpawnCreditPickup(location,45);
        else _pickups.Add(new Pickup{Kind=kind,Position=location,Velocity=new Vector2(-72f,Rand(-12f,12f))});
        if(_supportDropStage==2 || _supportDropStage==4 || _supportDropStage==7 || _supportDropStage==9)
            SpawnCreditPickup(location+new Vector2(62,56),45);
        Alert(kind==PickupKind.Credits?"CREDIT CACHE DETECTED":kind==PickupKind.Drone?"SUPPORT DRONE SIGNAL":
            kind==PickupKind.Shield?"SHIELD ENERGY DETECTED":"WEAPON ENERGY DETECTED",2.3f);
    }

    private void UpdateDrone(float dt)
    {
        if(!_pilot.DroneActive) return;
        _pilot.DroneShotTimer-=dt;
        _pilot.DroneBlockTimer=Math.Max(0,_pilot.DroneBlockTimer-dt);
        Vector2 target=_pilot.Position+new Vector2(-125,-100+13f*(float)Math.Sin(_travel*3.8f));
        _pilot.DronePosition=Vector2.Lerp(_pilot.DronePosition,target,Math.Min(1f,dt*7.5f));

        // The drone intercepts at most one enemy projectile per cooldown window.
        if(_pilot.DroneBlockTimer<=0)
        {
            for(int i=_shots.Count-1;i>=0;i--)
            {
                Projectile shot=_shots[i];
                if(shot.Friendly || SegmentDistanceSquared(_pilot.DronePosition,shot.Position,shot.Position+shot.Velocity*dt)>MathF.Pow(shot.Radius+26,2)) continue;
                _shots.RemoveAt(i);
                _pilot.DroneBlockTimer=1.25f;
                Boom(_pilot.DronePosition,7,Color.MediumPurple);
                break;
            }
        }
        if(_pilot.DroneShotTimer>0) return;
        Enemy? closest=null;
        float best=float.MaxValue;
        foreach(Enemy e in _enemies)
        {
            if(e.Position.X<_pilot.DronePosition.X+40) continue;
            float distance=Vector2.DistanceSquared(e.Position,_pilot.DronePosition);
            if(distance>=best) continue;
            closest=e;best=distance;
        }
        if(closest==null || best>1250f*1250f) return;
        Vector2 origin=_pilot.DronePosition+new Vector2(48,0);
        Vector2 heading=closest.Position-origin;
        if(heading.LengthSquared()<1) heading=Vector2.UnitX;
        heading.Normalize();
        CreateShot(origin,heading*950,true,1+_pilot.DroneUpgrade,9,Color.MediumPurple);
        _shots[^1].IsDroneShot=true;
        _pilot.DroneShotTimer=.44f-.08f*_pilot.DroneUpgrade;
        for(int k=0;k<3;k++) AddParticle(origin,new Vector2(Rand(20,120),Rand(-50,50)),Color.MediumPurple,.22f,10);
    }

    private bool SpawnWave()
    {
        // Retain the original six wave patterns; tactical formations recur only every ninth wave.
        // A ceiling prevents a late dense wave from flooding an already crowded screen.
        if(_enemies.Count>=18) return false;
        if(_sectorZone==3 && _spawnCount%3==0)
            SpawnEnemy(EnemyKind.Turret,new Vector2(ViewWidth+160,Rand(285,795)),0,0);

        int pattern=_spawnCount++%9;
        if(pattern>=6)
        {
            SpawnTacticalFormation(pattern);
            return true;
        }

        int count=pattern==3?2:pattern==4?7:pattern==5?3:pattern==2?4:5;
        for(int i=0;i<count;i++)
        {
            EnemyKind type=pattern==3?EnemyKind.Heavy:pattern==2||pattern==5&&i!=1?EnemyKind.Interceptor:pattern==5?EnemyKind.Heavy:EnemyKind.Scout;
            float y=pattern switch
            {
                0=>245+i*135,
                1=>Rand(210,860),
                2=>540+(i%2==0?-1:1)*(80+(i/2)*175),
                3=>330+i*420,
                4=>300+(i%2)*450+(float)Math.Sin(i*.8f)*55,
                _=>300+i*240
            };
            SpawnEnemy(type,new Vector2(ViewWidth+145+i*(pattern==4?102:pattern==5?220:150),y),i*.55f,pattern);
        }
        return true;
    }

    private void SpawnTacticalFormation(int pattern)
    {
        // Player position is sampled once at formation creation: fair, readable paths.
        // The formations do not home in on the ship during their attack run.
        float targetY=MathHelper.Clamp(_pilot.Position.Y,270f,810f);
        _formationNotice=pattern switch
        {
            6=>"INCOMING  -  DIVE INTERCEPTORS",
            7=>"INCOMING  -  WEAVING SQUADRON",
            _=>"INCOMING  -  PINCER ATTACK"
        };
        _formationNoticeTime=2.1f;

        if(pattern==6)
        {
            for(int i=0;i<4;i++)
            {
                bool upper=i%2==0;
                float startY=upper?260f:815f;
                float lockedY=MathHelper.Clamp(targetY+(upper?-130f:130f),200f,900f);
                SpawnEnemy(EnemyKind.Interceptor,new Vector2(ViewWidth+165+i*170,startY),i*.65f,6,lockedY);
            }
        }
        else if(pattern==7)
        {
            for(int i=0;i<5;i++)
            {
                float y=275+i*132;
                SpawnEnemy(EnemyKind.Scout,new Vector2(ViewWidth+155+i*140,y),i*.85f,7);
            }
        }
        else
        {
            // Two fast flankers precede two slower gunships.
            for(int i=0;i<4;i++)
            {
                bool upper=i%2==0;
                bool heavy=i>=2;
                float startY=upper?275f:795f;
                float lockedY=MathHelper.Clamp(targetY+(upper?-170f:170f),200f,900f);
                SpawnEnemy(heavy?EnemyKind.Heavy:EnemyKind.Interceptor,
                    new Vector2(ViewWidth+180+i*160,startY),i*.75f,8,lockedY);
            }
        }
    }

    private void SpawnEnemy(EnemyKind type,Vector2 at,float phase,int motion=0,float targetY=float.NaN)
    {
        int hp=type==EnemyKind.Turret?12:type==EnemyKind.Heavy?7:type==EnemyKind.Interceptor?3:2;
        _enemies.Add(new Enemy
        {
            Kind=type, Position=at, BaseY=at.Y, TargetY=float.IsNaN(targetY)?at.Y:targetY,
            Health=hp,MaxHealth=hp,Phase=phase,
            Radius=type==EnemyKind.Turret?72:type==EnemyKind.Heavy?65:38,
            Speed=type==EnemyKind.Turret?120:type==EnemyKind.Heavy?155:type==EnemyKind.Interceptor?370:285,
            FireTimer=Rand(1.3f,3.1f),MotionPattern=motion
        });
    }

    private void SpawnBoss()
    {
        _enemies.Add(new Enemy
        {
            Kind=EnemyKind.Sentinel,Position=new Vector2(ViewWidth+360,540),
            BaseY=540,Speed=150,Radius=206,Health=160,MaxHealth=160,FireTimer=1.6f
        });
    }

    private void UpdateEnemies(float dt)
    {
        for(int i=_enemies.Count-1;i>=0;i--)
        {
            if(i>=_enemies.Count) continue; // Boss phase can add/remove modules during iteration.
            Enemy e=_enemies[i];
            ApplyEnemyDifficultyOnce(e);
            e.Age+=dt;
            float previousY=e.Position.Y;
            e.HitFlash=Math.Max(0,e.HitFlash-dt*4.3f);
            e.HitReaction=Math.Max(0f,e.HitReaction-dt);
            if(IsInfernoBoss(e.Kind) || e.Kind==EnemyKind.InfernoBossTurret)
            {
                UpdateInfernoBoss(e,dt);
                if(!_enemies.Contains(e)) continue;
            }
            else if(e.Kind==EnemyKind.RiftBossTurret)
            {
                if(e.Parent==null || !_enemies.Contains(e.Parent)) { _enemies.Remove(e);continue; }
                e.Position=e.Parent.Position+e.MountOffset;
                e.FireTimer-=dt/DifficultyShotTimeFactor;
                if(e.Parent.Position.X<=1510f && e.FireTimer<=0f)
                {
                    AimFire(e.Position,new Vector2(-420f,0f),11f);
                    e.FireTimer=2.45f;
                }
            }
            else if(e.Kind==EnemyKind.RiftGuardian || e.Kind==EnemyKind.RiftColossus)
            {
                bool colossus=e.Kind==EnemyKind.RiftColossus;
                float anchor=colossus?1510f:1480f;
                if(e.Position.X>anchor) e.Position.X=MathF.Max(anchor,e.Position.X-e.Speed*dt);
                else e.Position.Y=540f+MathF.Sin(e.Age*(colossus?.76f:1.16f))*(colossus?130f:185f);
                int phase=colossus?(e.Health<=e.MaxHealth*.31f?3:e.Health<=e.MaxHealth*.66f?2:1):
                    (e.Health<=e.MaxHealth*.50f?2:1);
                if(phase>e.LastPhase)
                {
                    e.LastPhase=phase;
                    OnBossPhaseChanged(e,phase);
                    _shake=MathF.Max(_shake,13f);
                    Boom(e.Position+new Vector2(-60f,0f),34,Color.MediumPurple);
                    Alert(colossus?"RIFT COLOSSUS  -  PHASE "+phase:"RIFT GUARDIAN  -  OVERDRIVE",3f);
                }
                e.FireTimer-=dt/DifficultyShotTimeFactor;
                if(e.Position.X<=anchor && e.FireTimer<=0f)
                {
                    FireRiftBoss(e);
                    e.FireTimer=colossus?(phase==3?.88f:phase==2?1.18f:1.52f):phase==2?.90f:1.60f;
                }
            }
            else if(e.Kind==EnemyKind.Warden)
            {
                if(e.Position.X>1500) e.Position.X=Math.Max(1500,e.Position.X-e.Speed*dt);
                else e.Position.Y=540+(float)Math.Sin(e.Age*1.23f)*235;
                int phase=e.Health<=e.MaxHealth*.45f?2:1;
                if(phase>e.LastPhase)
                {
                    e.LastPhase=phase;
                    OnBossPhaseChanged(e,phase);
                    Boom(e.Position,28,Color.MediumPurple);
                    SpawnCombatBurst(e.Position+new Vector2(-85,0),Color.MediumPurple,30,120f,470f);
                    Alert("WARDEN FRIGATE  -  OVERDRIVE",2.5f);
                }
                e.FireTimer-=dt/DifficultyShotTimeFactor;
                if(e.Position.X<=1500 && e.FireTimer<=0)
                {
                    WardenFire(e);
                    e.FireTimer=phase==2?.62f:1.1f;
                }
            }
            else if(e.Kind==EnemyKind.Sentinel)
            {
                if(e.Position.X>1510) e.Position.X=Math.Max(1510,e.Position.X-e.Speed*dt);
                else e.Position.Y=540+(float)Math.Sin(e.Age*(e.Health<e.MaxHealth*.3f?1.5f:.75f))*185;
                int phase=e.Health<=e.MaxHealth*.30f?3:e.Health<=e.MaxHealth*.65f?2:1;
                if(phase>e.LastPhase)
                {
                    e.LastPhase=phase;
                    OnBossPhaseChanged(e,phase);
                    _shake=Math.Max(_shake,18);
                    _flash=Math.Max(_flash,.4f);
                    Boom(e.Position+new Vector2(-130,0),32,Color.OrangeRed);
                    SpawnCombatBurst(e.Position+new Vector2(-140,0),Color.OrangeRed,42,150f,620f);
                    SpawnCombatFlash(e.Position+new Vector2(-175,0),-Vector2.UnitX,Color.OrangeRed,145f,.28f);
                    Alert("SENTINEL - PHASE "+phase,3.0f);
                    // Escort units make the transitions tactically distinct.
                    SpawnEnemy(EnemyKind.Interceptor,new Vector2(ViewWidth+150,230),.3f,2);
                    SpawnEnemy(EnemyKind.Interceptor,new Vector2(ViewWidth+275,865),1.7f,2);
                }
                e.FireTimer-=dt/DifficultyShotTimeFactor;
                if(e.Position.X<=1510 && e.FireTimer<=0)
                {
                    BossFire(e);
                    e.FireTimer=phase==3?.42f:phase==2?.66f:.95f;
                }
            }
            else
            {
                if(IsInfernoEnemy(e.Kind)) UpdateInfernoEnemy(e,dt);
                else if(IsRiftEnemy(e.Kind)) UpdateRiftEnemy(e,dt);
                else
                {
                e.Position.X-=e.Speed*dt;
                float freq=e.Kind==EnemyKind.Interceptor?4.1f:2.2f;
                float amplitude=e.Kind==EnemyKind.Heavy?32: e.Kind==EnemyKind.Interceptor?145:73;
                if(e.Kind==EnemyKind.Turret) {freq=.6f;amplitude=35;}
                if(e.MotionPattern==4) {freq=5.0f;amplitude=105;}
                if(e.MotionPattern==2) {freq=4.9f;amplitude=120;}
                if(e.MotionPattern==5) {freq=2.6f;amplitude*=.6f;}
                if(e.MotionPattern==6 || e.MotionPattern==8)
                {
                    // Locked attack coordinate: smooth curve from entry band to target band.
                    float duration=e.MotionPattern==6?1.85f:2.55f;
                    float t=MathHelper.Clamp((e.Age-.15f)/duration,0f,1f);
                    t=t*t*(3f-2f*t);
                    float sway=e.MotionPattern==6?22f:15f;
                    e.Position.Y=MathHelper.Clamp(MathHelper.Lerp(e.BaseY,e.TargetY,t)
                        +(float)Math.Sin(e.Age*3.8f+e.Phase)*sway,170f,910f);
                }
                else if(e.MotionPattern==7)
                {
                    // Offset wave: no jumps between animation frames or scripted teleports.
                    e.Position.Y=MathHelper.Clamp(e.BaseY
                        +(float)Math.Sin(e.Age*3.7f+e.Phase)*64f
                        +(float)Math.Sin(e.Age*1.65f+e.Phase*.5f)*28f,175f,910f);
                }
                else e.Position.Y=e.BaseY+(float)Math.Sin(e.Age*freq+e.Phase)*amplitude;

                e.FireTimer-=dt/DifficultyShotTimeFactor;
                if(e.Position.X<ViewWidth-130 && e.Position.X>180 && e.FireTimer<=0)
                {
                    if(e.MotionPattern==6) FireLeadingInterceptor(e);
                    else if(e.MotionPattern==7) FireWeavingPair(e);
                    else AimFire(e.Position,new Vector2(-480,0),e.Kind==EnemyKind.Turret?15:e.Kind==EnemyKind.Heavy?14:9);
                    if(e.Kind==EnemyKind.Heavy || e.Kind==EnemyKind.Turret || e.MotionPattern==5)
                    {
                        // Original heavy/turret crossfire remains in place.
                        CreateShot(e.Position+new Vector2(-50,18),new Vector2(-420,110),false,1,12,Color.OrangeRed);
                    }
                    e.FireTimer=e.Kind==EnemyKind.Turret?1.6f:e.Kind==EnemyKind.Heavy?2.1f:3.4f;
                }
                }
            }
            if(e.Kind!=EnemyKind.Sentinel && e.Kind!=EnemyKind.Warden && e.Kind!=EnemyKind.Turret &&
                e.Kind!=EnemyKind.RiftGuardian && e.Kind!=EnemyKind.RiftColossus && e.Kind!=EnemyKind.RiftBossTurret &&
                !IsInfernoBoss(e.Kind) && e.Kind!=EnemyKind.InfernoBossTurret)
            {
                float bankTarget=MathHelper.Clamp((e.Position.Y-previousY)/(Math.Max(.001f,dt)*400f),-1f,1f);
                e.Banking=MathHelper.Lerp(e.Banking,bankTarget,Math.Min(1f,dt*7f));
            }
            if(e.Position.X< -235)
            {
                _enemies.Remove(e);
                continue;
            }
            if(HurtCoopCircle(e.Position,e.Radius))
            {
                if(e.Kind!=EnemyKind.Sentinel && e.Kind!=EnemyKind.Warden &&
                    e.Kind!=EnemyKind.RiftGuardian && e.Kind!=EnemyKind.RiftColossus && e.Kind!=EnemyKind.RiftBossTurret &&
                    !IsInfernoBoss(e.Kind) && e.Kind!=EnemyKind.InfernoBossTurret)
                {
                    Boom(e.Position,18,Color.OrangeRed);
                    _enemies.Remove(e);
                }
            }
        }
    }

    private void AimFire(Vector2 origin,Vector2 defaultVelocity,float radius)
    {
        Vector2 aim=_pilot.Position-origin;
        if(aim.LengthSquared()<.001f) aim=defaultVelocity;
        aim.Normalize();
        Vector2 muzzle=origin+new Vector2(-50,0);
        CreateShot(muzzle,aim*420,false,1,radius,Color.OrangeRed);
        SpawnCombatFlash(muzzle,aim,Color.OrangeRed,18f,.10f);
    }

    private void FireLeadingInterceptor(Enemy e)
    {
        // Short prediction interval; bounded to the viewport to avoid unavoidable shots.
        Vector2 muzzle=e.Position+new Vector2(-50f,0);
        Vector2 predicted=_pilot.Position+_pilot.Velocity*.22f;
        predicted.X=MathHelper.Clamp(predicted.X,90f,ViewWidth-130f);
        predicted.Y=MathHelper.Clamp(predicted.Y,115f,ViewHeight-120f);
        Vector2 direction=predicted-muzzle;
        if(direction.LengthSquared()<1f) direction=-Vector2.UnitX;
        direction.Normalize();
        CreateShot(muzzle,direction*390f,false,1,10f,Color.OrangeRed);
        SpawnCombatFlash(muzzle,direction,Color.OrangeRed,22f,.12f);
    }

    private void FireWeavingPair(Enemy e)
    {
        // Twin low-speed lanes replace one standard aimed shot; both are dodgeable.
        Vector2 muzzle=e.Position+new Vector2(-50f,0);
        Vector2 direction=_pilot.Position-muzzle;
        if(direction.LengthSquared()<1f) direction=-Vector2.UnitX;
        direction.Normalize();
        float baseAngle=(float)Math.Atan2(direction.Y,direction.X);
        for(int i=0;i<2;i++)
        {
            float angle=baseAngle+(i==0?-.16f:.16f);
            CreateShot(muzzle,new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*315f,
                false,1,9f,Color.OrangeRed);
        }
        SpawnCombatFlash(muzzle,direction,Color.OrangeRed,24f,.12f);
    }

    private void WardenFire(Enemy boss)
    {
        Vector2 root=boss.Position+new Vector2(-132,0);
        float aim=(float)Math.Atan2(_pilot.Position.Y-root.Y,_pilot.Position.X-root.X);
        int count=boss.LastPhase==2?5:3;
        for(int i=0;i<count;i++)
        {
            float spread=(i-(count-1)*.5f)*.18f;
            float angle=aim+spread;
            CreateShot(root,new Vector2((float)Math.Cos(angle),(float)Math.Sin(angle))*(boss.LastPhase==2?535:445),false,1,12,Color.MediumPurple);
        }
        SpawnCombatFlash(root,new Vector2((float)Math.Cos(aim),(float)Math.Sin(aim)),Color.MediumPurple,53f,.19f);
    }

    private void BossFire(Enemy boss)
    {
        int phase=boss.LastPhase;
        int count=phase==3?9:phase==2?7:5;
        Vector2 root=boss.Position+new Vector2(-215,0);
        for(int k=0;k<count;k++)
        {
            float spread=phase==3?1.94f:phase==2?1.64f:1.40f;
            float angle=MathHelper.Pi-spread*.5f+(spread*k/(count-1));
            Vector2 dir=new((float)Math.Cos(angle),(float)Math.Sin(angle));
            CreateShot(root,dir*(phase==3?560:phase==2?490:430),false,1,phase==3?17:15,Color.OrangeRed);
        }
        SpawnCombatFlash(root,-Vector2.UnitX,Color.OrangeRed,phase==3?78f:55f,.15f);
        // Every second volley in the final two phases also aims at the pilot.
        if(phase>=2 && boss.FireCycle++%2==0) AimFire(root,new Vector2(-520,0),14);
        // Telegraph the beam before it becomes dangerous. A lane is fixed when announced.
        if(phase>=2 && boss.FireCycle%4==0 && _laserLanes.Count==0)
        {
            _laserLanes.Add(new LaserLane{Y=MathHelper.Clamp(_pilot.Position.Y,230,ViewHeight-150)});
            _audio?.Effect("boss_warning",.7f,.68f);
            Alert("WARNING  -  HORIZONTAL LASER",1.1f);
        }
        _shake=Math.Max(_shake,5);
    }

    // Shared kill path for projectile hits and continuous Beam ticks.
    // Maintains score, death variants, checkpoint progression and drop logic consistently.
    private void DestroyEnemy(Enemy enemy,int k)
    {
        bool infernoBoss=IsInfernoBoss(enemy.Kind);
        bool boss=enemy.Kind==EnemyKind.Sentinel || enemy.Kind==EnemyKind.RiftColossus ||
            enemy.Kind==EnemyKind.InfernoTitan;
        bool miniboss=enemy.Kind==EnemyKind.Warden || enemy.Kind==EnemyKind.RiftGuardian ||
            enemy.Kind==EnemyKind.InfernoGuardian;
        bool riftBoss=enemy.Kind==EnemyKind.RiftGuardian || enemy.Kind==EnemyKind.RiftColossus;
        if(enemy.Kind==EnemyKind.InfernoBossTurret)
        {
            OnBossModuleDestroyed(enemy);
            Boom(enemy.Position,25,Color.OrangeRed);
            _score+=380;_sectorKills++;
            _enemies.Remove(enemy);
            return;
        }
        if(enemy.Kind==EnemyKind.RiftBossTurret)
        {
            OnBossModuleDestroyed(enemy);
            Boom(enemy.Position,25,Color.MediumPurple);
            _score+=300;_sectorKills++;
            _enemies.Remove(enemy);
            return;
        }
        if(infernoBoss)
        {
            // Removing attached weapons here also disables their own fire logic.
            _enemies.RemoveAll(e=>e.Kind==EnemyKind.InfernoBossTurret && e.Parent==enemy);
            k=_enemies.IndexOf(enemy);
            _infernoBossStage=enemy.Kind==EnemyKind.InfernoGuardian?2:4;
        }
        if(riftBoss)
        {
            // Orphaned pylons cannot continue firing during death animations.
            for(int j=_enemies.Count-1;j>=0;j--)
                if(j!=k && _enemies[j].Kind==EnemyKind.RiftBossTurret &&
                   _enemies[j].Parent==enemy) _enemies.RemoveAt(j);
            k=_enemies.IndexOf(enemy);
            if(enemy.Kind==EnemyKind.RiftGuardian) _riftBossStage=2;
            else _riftBossStage=4;
        }
        // Each class now has distinct death intensity and pacing.
        // Retain the approved 0.5.6 explosion PNGs and additive blending.
        if(boss)
        {
            BeginBossFinale(enemy.Position,riftBoss,enemy.Kind==EnemyKind.InfernoTitan);
        }
        else
        {
            int deathAmount=enemy.Kind switch
            {
                EnemyKind.Scout=>12,
                EnemyKind.Interceptor=>19,
                EnemyKind.Heavy=>37,
                EnemyKind.Turret=>32,
                EnemyKind.Warden=>73,EnemyKind.RiftGuardian=>115,
                EnemyKind.InfernoGuardian=>126,
                EnemyKind.RiftDart=>17,EnemyKind.RiftWisp=>19,
                EnemyKind.RiftMauler=>47,EnemyKind.RiftPrism=>29,
                EnemyKind.RiftStalker=>24,
                EnemyKind.InfernoTank=>42,EnemyKind.InfernoBomber=>34,
                _=>20
            };
            Boom(enemy.Position,deathAmount,miniboss && !infernoBoss?Color.MediumPurple:Color.OrangeRed);
            if(miniboss)
            {
                // Staggered hull ruptures follow the first Warden detonation.
                QueueDeathBurst(enemy.Position+new Vector2(-84,-39),.12f,.62f,.060f);
                QueueDeathBurst(enemy.Position+new Vector2(85,63),.30f,.76f,.065f);
                QueueDeathBurst(enemy.Position+new Vector2(-9,8),.52f,1.12f,.085f,true);
            }
            else if(enemy.Kind==EnemyKind.Heavy || enemy.Kind==EnemyKind.Turret)
            {
                QueueDeathBurst(enemy.Position+new Vector2(34,-19),.15f,.46f,.048f);
            }
        }
        _shake=Math.Max(_shake,boss?26:miniboss?18:enemy.Kind==EnemyKind.Heavy?10:4);
        int baseScore=boss?(enemy.Kind==EnemyKind.InfernoTitan?11000:riftBoss?8000:5000):
            miniboss?(enemy.Kind==EnemyKind.InfernoGuardian?4200:riftBoss?3000:2000):enemy.Kind==EnemyKind.Turret?650:
            enemy.Kind==EnemyKind.RiftMauler?700:enemy.Kind==EnemyKind.RiftPrism?390:
            enemy.Kind==EnemyKind.RiftStalker?320:enemy.Kind==EnemyKind.RiftWisp?230:
            enemy.Kind==EnemyKind.RiftDart?170:enemy.Kind==EnemyKind.InfernoTank?830:
            enemy.Kind==EnemyKind.InfernoBomber?560:IsInfernoEnemy(enemy.Kind)?350:
            enemy.Kind==EnemyKind.Heavy?500:
            enemy.Kind==EnemyKind.Interceptor?250:100;
        _score+=(int)MathF.Round(baseScore*DifficultyScoreFactor);
        if(boss) AwardCredits(enemy.Kind==EnemyKind.InfernoTitan?1600:riftBoss?1150:575);
        else if(miniboss) AwardCredits(enemy.Kind==EnemyKind.InfernoGuardian?540:riftBoss?420:125);
        else
        {
            // Coins are collectible world pickups, not an automatic score conversion.
            // Small, infrequent, collectible loot. The guaranteed boss rewards
            // ensure Pulse->Lance is affordable even if the pilot misses coins.
            if(!_riftActive && !_infernoActive)
            {
                if(_random.NextDouble()<.10) SpawnCreditPickup(enemy.Position,_random.Next(10,21));
            }
            else if(_infernoActive)
            {
                if(_random.NextDouble()<.19) SpawnCreditPickup(enemy.Position,_random.Next(35,61));
            }
            else if(_random.NextDouble()<.16)
                SpawnCreditPickup(enemy.Position,_random.Next(25,46));
            if(_random.NextDouble()<.15) SpawnPickup(enemy.Position);
        }
        _sectorKills++;
        _enemies.Remove(enemy);
        if(miniboss)
        {
            _shots.RemoveAll(shot=>!shot.Friendly);
            _laserLanes.Clear();
            if(infernoBoss)
            {
                _waveClock=3.4f;
                _bossLaunched=false;
                _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+1);
                Alert("INFERNO GUARDIAN ELIMINATED  -  +540 CR",3.5f);
            }
            else if(!riftBoss) _wardenDefeated=true;
            else
            {
                _waveClock=2.5f;
                _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+1);
                Alert("RIFT GUARDIAN ELIMINATED  -  +420 CR",3f);
            }
        }
    }

    private void UpdateContinuousBeam(float dt)
    {
        _beamStart=PlayerLocalOffset(new Vector2(167,24));
        _beamEnd=new Vector2(ViewWidth+10f,_beamStart.Y);
        int lvl=Math.Clamp(_pilot.WeaponLevel,1,Pilot.MaxWeaponLevel);
        float halfWidth=(8f+3.4f*(lvl-1))*.50f;
        Enemy? target=null;
        Debris? rockTarget=null;
        float nearestX=_beamEnd.X;
        foreach(Enemy e in _enemies)
        {
            float hitMargin=halfWidth+e.Radius*.82f;
            if(e.Position.X<_beamStart.X || Math.Abs(e.Position.Y-_beamStart.Y)>hitMargin) continue;
            float surface=e.Position.X-MathF.Sqrt(MathF.Max(0f,hitMargin*hitMargin-(e.Position.Y-_beamStart.Y)*(e.Position.Y-_beamStart.Y)));
            if(surface<nearestX) { nearestX=surface;target=e;rockTarget=null; }
        }
        foreach(Debris rock in _debris)
        {
            float hitMargin=halfWidth+rock.Radius*.85f;
            if(rock.Position.X<_beamStart.X || Math.Abs(rock.Position.Y-_beamStart.Y)>hitMargin) continue;
            float surface=rock.Position.X-MathF.Sqrt(MathF.Max(0f,hitMargin*hitMargin-(rock.Position.Y-_beamStart.Y)*(rock.Position.Y-_beamStart.Y)));
            if(surface<nearestX) { nearestX=surface;rockTarget=rock;target=null; }
        }
        _beamEnd.X=MathF.Max(_beamStart.X+15,nearestX);
        if(_beamDamageTimer>0) return;
        _beamTickCount++;
        // One low-damage hit at a steady level-scaled cadence; only a single target.
        float interval=.165f-.010f*(lvl-1); // L1 ~6 DPS, L7 ~9.5 DPS
        _beamDamageTimer+=interval;
        if(target!=null)
        {
            int beamDamage=1+NextPrimaryPowerBonus();
            target.Health-=beamDamage;
            target.HitFlash=MathF.Max(target.HitFlash,.50f);
            target.HitReaction=MathF.Max(target.HitReaction,.07f);
            Projectile beamContact=new() { Friendly=true,OwnerPlayerId=_coopActorId,PrimaryMode=PrimaryWeaponMode.Beam,Tint=PrimaryWeaponColor(PrimaryWeaponMode.Beam) };
            RecordPrimaryDamage(target,beamDamage,beamContact);
            SpawnPrimaryImpact(beamContact,_beamEnd);
            OnBossImpact(target,_beamEnd);
            if(target.Health<=0)
            {
                int enemyIndex=_enemies.IndexOf(target);
                if(enemyIndex>=0) DestroyEnemy(target,enemyIndex);
            }
        }
        else if(rockTarget!=null)
        {
            rockTarget.Health-=1+NextPrimaryPowerBonus();
            Projectile beamContact=new() { Friendly=true,PrimaryMode=PrimaryWeaponMode.Beam };
            SpawnPrimaryImpact(beamContact,_beamEnd);
            if(rockTarget.Health<=0)
            {
                Boom(rockTarget.Position,22,Color.Orange);
                _debris.Remove(rockTarget);
                _score+=75;
            }
        }
    }

    private void UpdateShots(float dt)
    {
        // Boss/miniboss deaths may RemoveAll hostile shots during this update.
        // Their removal shifts projectile indices, so never assume a previously
        // captured i still refers to the projectile being processed.
        for(int i=_shots.Count-1;i>=0;i--)
        {
            if(i>=_shots.Count) continue; // A boss death shortened the list.
            Projectile shot=_shots[i];
            shot.PreviousPosition=shot.Position;
            shot.Position+=shot.Velocity*dt;
            shot.Age+=dt;
            if(shot.Age>shot.Duration || shot.Position.X < -100 || shot.Position.X>ViewWidth+100 || shot.Position.Y< -100 || shot.Position.Y>ViewHeight+100)
            {
                _shots.Remove(shot);
                continue;
            }
            if(shot.Friendly)
            {
                bool consumed=false;
                for(int d=_debris.Count-1;d>=0;d--)
                {
                    Debris rock=_debris[d];
                    if(shot.HitRocks.Contains(rock)) continue;
                    if(SegmentDistanceSquared(rock.Position,shot.PreviousPosition,shot.Position)>MathF.Pow(rock.Radius*.85f+shot.Radius,2)) continue;
                    rock.Health-=shot.Damage;
                    SpawnPrimaryImpact(shot,shot.Position);
                    SpawnCombatBurst(shot.Position,Color.Orange,6,115f,310f);
                    SpawnCombatFlash(shot.Position,Vector2.UnitX,Color.Orange,22f,.11f);
                    Boom(shot.Position,3,Color.Orange);
                    if(rock.Health<=0)
                    {
                        Boom(rock.Position,22,Color.Orange);
                        _debris.RemoveAt(d);
                        _score+=75;
                    }
                    shot.HitRocks.Add(rock);
                    shot.HitsRemaining--;
                    if(shot.HitsRemaining<=0) { consumed=true; break; }
                }
                if(consumed)
                {
                    _shots.Remove(shot);
                    continue;
                }
                for(int k=_enemies.Count-1;k>=0;k--)
                {
                    // Rift bosses also remove their attached turrets on death.
                    if(k>=_enemies.Count) continue;
                    Enemy enemy=_enemies[k];
                    if(shot.HitEnemies.Contains(enemy)) continue;
                    if(SegmentDistanceSquared(enemy.Position,shot.PreviousPosition,shot.Position)>MathF.Pow(shot.Radius+enemy.Radius,2)) continue;
                    enemy.Health-=shot.Damage;
                    RecordPrimaryDamage(enemy,shot.Damage,shot);
                    SpawnPrimaryImpact(shot,shot.Position);
                    OnBossImpact(enemy,shot.Position);
                    enemy.HitFlash=.7f;
                    enemy.HitReaction=Math.Max(enemy.HitReaction,
                        enemy.Kind==EnemyKind.Sentinel || enemy.Kind==EnemyKind.Warden ? .17f : .11f);
                    SpawnCombatBurst(shot.Position,enemy.Kind==EnemyKind.Warden?Color.MediumPurple:shot.Tint,8,170f,390f);
                    SpawnCombatFlash(shot.Position,shot.Velocity,shot.Tint,28f,.11f);
                    for(int p=0;p<5;p++) AddParticle(shot.Position,new Vector2(Rand(-150,150),Rand(-150,150)),Color.LightSkyBlue,Rand(.13f,.37f),Rand(3,11));
                    if(enemy.Health<=0) DestroyEnemy(enemy,k);
                    shot.HitEnemies.Add(enemy);
                    shot.HitsRemaining--;
                    if(shot.HitsRemaining<=0) { consumed=true; break; }
                }
                if(consumed) _shots.Remove(shot);
            }
            else if(CoopActive?HurtCoopShot(shot):_pilot.Invincible<=0 && SegmentDistanceSquared(_pilot.Position,shot.PreviousPosition,shot.Position)<MathF.Pow(shot.Radius+_pilot.Radius,2))
            {
                if(!CoopActive) HurtPilot();
                _shots.Remove(shot);
            }
        }
    }

    // Swept projectile hit-test, preventing high-speed shots from tunnelling between frames.
    private static float SegmentDistanceSquared(Vector2 point,Vector2 start,Vector2 end)
    {
        Vector2 segment=end-start;
        float lenSquared=segment.LengthSquared();
        float t=lenSquared<.00001f?0:MathHelper.Clamp(Vector2.Dot(point-start,segment)/lenSquared,0f,1f);
        return Vector2.DistanceSquared(point,start+segment*t);
    }

    private void HurtPilot()
    {
        if(_pilot.Invincible>0 || _bossFinaleTimer>0) return;
        PulseGamePad(_pilot.Shield>0?.45f:1f,_pilot.Shield>0?.70f:.85f);
        if(_hitEffectStrength>0) _hitFeedbackTime=.5f;
        _audio?.Effect(_pilot.Shield>0?"shield":"damage",.20f,.7f);
        if(_pilot.Shield>0)
        {
            _pilot.Shield--;
            _pilot.Invincible=.62f;
            _shake=6;
            _flash=.12f;
            AddShockwave(_pilot.Position,Color.DeepSkyBlue,190,.42f);
            SpawnCombatBurst(_pilot.Position,Color.Cyan,22,160f,450f);
            SpawnCombatFlash(_pilot.Position,-Vector2.UnitX,Color.Cyan,78f,.19f);
            for(int j=0;j<18;j++) AddParticle(_pilot.Position,new Vector2(Rand(-270,270),Rand(-270,270)),Color.Cyan,Rand(.2f,.6f),Rand(5,14));
            Alert("SHIELD IMPACT",.95f);
            return;
        }
        _pilot.Health--;
        _pilot.Invincible=1.6f;
        _shake=13;
        _flash=.3f;
        Boom(_pilot.Position,16,Color.Cyan);
        if(_pilot.Health>0) return;
        _pilot.Lives--;
        Boom(_pilot.Position,45,Color.Cyan);
        if(_pilot.Lives<=0)
        {
            if(CoopActive) { _pilot.Health=0;_pilot.Invincible=float.MaxValue;_pilot.Velocity=Vector2.Zero;return; }
            FinishRun(false);
            return;
        }
        _pilot.Health=_pilot.MaxHealth;
        _pilot.WeaponLevel=Math.Max(1,_pilot.WeaponLevel-1);
        _damageSmokes.Clear();
        _damageSmokeTimer=0;
        _pilot.Position=CoopActive?CoopSpawnPoint(_coopActorId):new Vector2(315,540);
        _pilot.Velocity=Vector2.Zero;
        _pilot.DronePosition=_pilot.Position+new Vector2(-125,-100);
        _pilotBank=0;
        _pilot.Invincible=3;
        Alert("SHIP REDEPLOYED",2);
    }

    private void AwardCredits(int amount)
    {
        if(amount<=0) return;
        // Sector-specific earning limits control farming without affecting the wallet.
        // The first sector is a controlled introductory economy: no amount of
        // random enemy farming permits unlocking more than the first paid gun.
        // Limit *earned* credits, not the wallet. Spending or checkpoint restore
        // cannot reset/re-trigger this sector limit.
        int sectorCap=_infernoActive?5300:_riftActive?3000:SectorOneCreditCap;
        amount=Math.Min(amount,Math.Max(0,sectorCap-_sectorCreditsEarned));
        if(amount<=0) return;
        _credits+=amount;
        _sectorCreditsEarned+=amount;
    }

    private void SpawnCreditPickup(Vector2 origin,int amount)
    {
        if(_pickups.Count>=56) return;
        _pickups.Add(new Pickup
        {
            Kind=PickupKind.Credits,Credits=amount,Position=origin,
            Velocity=new Vector2(-93f,Rand(-22f,22f))
        });
    }

    private void SpawnRareCreditCache(Vector2 origin,int amount)
    {
        if(_pickups.Count>=56) return;
        _pickups.Add(new Pickup
        {
            Kind=PickupKind.RareCache,Credits=amount,Position=origin,
            Velocity=new Vector2(-88f,0f)
        });
        Alert("RARE CREDIT CACHE DETECTED",2f);
    }

    private int NextPrimaryPowerBonus()
    {
        if(_pilot.WeaponPowerLevel==0) return 0;
        if(_pilot.WeaponPowerLevel>=2) return 1;
        return (++_powerShotIndex%2==0)?1:0;
    }

    private void SpawnPickup(Vector2 origin)
    {
        double chance=_random.NextDouble();
        // Favor weapons below L7, shift surplus drops toward defense once fully upgraded.
        bool capped=_pilot.WeaponLevel>=Pilot.MaxWeaponLevel;
        PickupKind kind=capped
            ? (chance<.24?PickupKind.Weapon:chance<.52?PickupKind.Repair:chance<.90?PickupKind.Shield:PickupKind.Drone)
            : (chance<.53?PickupKind.Weapon:chance<.72?PickupKind.Repair:chance<.94?PickupKind.Shield:PickupKind.Drone);
        _pickups.Add(new Pickup{Position=origin,Kind=kind,Velocity=new Vector2(-95f,Rand(-18f,18f))});
    }

    private void UpdatePickups(float dt)
    {
        for(int i=_pickups.Count-1;i>=0;i--)
        {
            var power=_pickups[i];
            SelectCoopPickupPilot(power.Position);
            power.Age+=dt;
            // gentle pickup magnet: logical QoL step so upgrades are easier to collect in combat
            Vector2 toPilot=_pilot.Position-power.Position;
            float distSq=toPilot.LengthSquared();
            if(distSq<(240f+_pilot.PickupMagnetLevel*90f)*(240f+_pilot.PickupMagnetLevel*90f))
            {
                float dist=MathF.Max(1f,MathF.Sqrt(distSq));
                Vector2 dir=toPilot/dist;
                float pull=MathHelper.Lerp(40f,520f,1f-MathHelper.Clamp(dist/(240f+_pilot.PickupMagnetLevel*90f),0f,1f));
                power.Velocity+=dir*pull*dt;
            }
            power.Velocity.X=MathF.Min(power.Velocity.X,-55f);
            power.Velocity*=MathF.Max(0f,1f-dt*.35f);
            power.Position+=power.Velocity*dt;
            if(distSq<80f*80f)
            {
                _audio?.Effect("pickup",.10f,.48f);
                Color burst=Color.Cyan;
                switch(power.Kind)
                {
                    case PickupKind.RareCache:
                        int beforeRare=_credits;
                        AwardCredits(power.Credits);
                        _sectorCreditPickups++;_sectorRareCaches++;
                        Alert("RARE CACHE  +"+(_credits-beforeRare)+" CR",2.3f);
                        burst=new Color(255,160,55);
                        break;
                    case PickupKind.Credits:
                        AwardCredits(power.Credits);
                        _sectorCreditPickups++;
                        Alert("CREDITS  +"+power.Credits+"  /  WALLET "+_credits,1.9f);
                        burst=new Color(255,192,66);
                        break;
                    case PickupKind.Weapon:
                        _sectorWeaponPickups++;
                        if(_pilot.WeaponLevel<Pilot.MaxWeaponLevel)
                        {
                            _pilot.WeaponLevel++;
                            Alert("PRIMARY POWER  LV "+_pilot.WeaponLevel+" / "+Pilot.MaxWeaponLevel,2);
                            _score+=200;
                        }
                        else
                        {
                            // Never waste a max-level pickup. No level 8 or second gun appears.
                            _score+=750;
                            Alert("MAX POWER  +750 BONUS",1.7f);
                        }
                        break;
                    case PickupKind.Repair:
                        _sectorSupplyPickups++;
                        _pilot.Health=Math.Min(_pilot.MaxHealth,_pilot.Health+1);
                        Alert("HULL REPAIRED",2);
                        burst=Color.LawnGreen;
                        break;
                    case PickupKind.Shield:
                        _sectorSupplyPickups++;
                        _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+2);
                        Alert("SHIELD CHARGED  "+_pilot.Shield+" / "+_pilot.MaxShield,2);
                        burst=Color.DeepSkyBlue;
                        break;
                    case PickupKind.Drone:
                        _sectorSupplyPickups++;
                        if(!_pilot.DroneActive)
                        {
                            _pilot.DroneActive=true;
                            _pilot.DronePosition=_pilot.Position+new Vector2(-125,-100);
                            _pilot.DroneShotTimer=.15f;
                            Alert("SUPPORT DRONE ONLINE",2.5f);
                        }
                        else
                        {
                            _pilot.Shield=Math.Min(_pilot.MaxShield,_pilot.Shield+1);
                            Alert("DRONE RECHARGED SHIELD",2);
                        }
                        burst=Color.MediumPurple;
                        break;
                }
                AddShockwave(power.Position,burst,145,.42f);
                for(int j=0;j<25;j++) AddParticle(power.Position,new Vector2(Rand(-210,210),Rand(-210,210)),burst,Rand(.3f,.75f),Rand(7,22));
                _pickups.RemoveAt(i);
            }
            else if(power.Position.X< -70 || power.Age>12) _pickups.RemoveAt(i);
        }
    }

    private void Thruster(Vector2 origin)
    {
        AddParticle(origin,new Vector2(Rand(-430,-120),Rand(-50,50)),_random.NextDouble()<.65?Color.Cyan:Color.Orange,
            Rand(.15f,.38f),Rand(7,22));
    }

    private void AddParticle(Vector2 where,Vector2 velocity,Color tint,float life,float size)
    {
        if(_particles.Count>1300) _particles.RemoveAt(0);
        _particles.Add(new Particle{Position=where,Velocity=velocity,Tint=tint,Life=life,Size=size});
    }

    private void Boom(Vector2 where,int amount,Color baseColor)
    {
        if(amount>=16)
        {
            float proximity=1f-MathHelper.Clamp(Vector2.Distance(where,_pilot.Position)/1200f,0f,1f);
            float impact=MathHelper.Clamp(amount/120f,.08f,.65f)*proximity;
            PulseGamePad(impact,impact*.45f);
        }
        if(amount>=16) _audio?.Effect("explosion",.15f,amount>=60?.85f:.55f);
        if(amount>=7 && _combatFxEnabled)
        {
            float scale=amount>=120?1.40f:amount>=70?1.08f:amount>=35?.73f:amount>=20?.54f:amount>=15?.44f:.34f;
            float frameTime=amount>=70?.075f:amount>=35?.061f:amount>=20?.052f:.043f;
            SpawnExplosionSprite(where,scale,frameTime);
        }
        // Tiny impacts should not emit giant concentric rings every few frames.
        if(amount>=12)
            AddShockwave(where,baseColor,amount>80?420:amount>30?185:95,amount>80?.85f:.40f);
        if(amount>=12)
        {
            SpawnCombatBurst(where,baseColor,Math.Min(58,amount/2),100f,amount>80?690f:440f);
            SpawnCombatFlash(where,-Vector2.UnitX,baseColor,amount>80?180f:amount>30?95f:48f,amount>80?.32f:.18f);
        }
        for(int j=0;j<amount;j++)
        {
            float theta=Rand(0,MathHelper.TwoPi),speed=Rand(55,540);
            Color color=j%3==0?Color.White:j%3==1?baseColor:Color.Orange;
            AddParticle(where,new Vector2((float)Math.Cos(theta),(float)Math.Sin(theta))*speed,color,Rand(.3f,1.4f),Rand(6,35));
        }
    }

    // Deliberately narrow/elongated foreground effects: NO circular DOF/bokeh particles.
    // This method does not touch parallax, background rocks, the player hull or gameplay physics.
    private void SpawnCombatBurst(Vector2 origin,Color tint,int count,float minSpeed,float maxSpeed)
    {
        if(!_combatFxEnabled) return;
        for(int i=0;i<count;i++)
        {
            if(_combatSparks.Count>=550) _combatSparks.RemoveAt(0);
            float angle=FxRand(0,MathHelper.TwoPi);
            float velocity=FxRand(minSpeed,maxSpeed);
            Vector2 heading=new((float)Math.Cos(angle),(float)Math.Sin(angle));
            _combatSparks.Add(new CombatSpark
            {
                Position=origin,
                Velocity=heading*velocity,
                Tint=i%4==0?Color.White:tint,
                Life=FxRand(.14f,.46f),
                Length=FxRand(14f,43f),
                Thickness=FxRand(1.6f,3.7f)
            });
        }
    }

    private void SpawnCombatFlash(Vector2 origin,Vector2 direction,Color tint,float size,float duration)
    {
        if(!_combatFxEnabled) return;
        if(_combatFlashes.Count>=55) _combatFlashes.RemoveAt(0);
        if(direction.LengthSquared()<.001f) direction=Vector2.UnitX;
        direction.Normalize();
        _combatFlashes.Add(new CombatFlash
        {
            Position=origin,
            Direction=direction,
            Tint=tint,
            Size=size,
            Life=duration
        });
    }

    private void UpdateCombatFx(float dt)
    {
        for(int i=_combatSparks.Count-1;i>=0;i--)
        {
            CombatSpark p=_combatSparks[i];
            p.Age+=dt;
            if(p.Age>=p.Life) { _combatSparks.RemoveAt(i); continue; }
            p.Position+=p.Velocity*dt;
            p.Velocity*=MathF.Max(0f,1f-dt*3.1f);
        }
        for(int i=_combatFlashes.Count-1;i>=0;i--)
        {
            _combatFlashes[i].Age+=dt;
            if(_combatFlashes[i].Age>=_combatFlashes[i].Life) _combatFlashes.RemoveAt(i);
        }
    }

    private void SpawnExplosionSprite(Vector2 where,float scale,float frameTime=.055f)
    {
        if(_explosionSprites.Count>48) _explosionSprites.RemoveAt(0);
        _explosionSprites.Add(new ExplosionSprite
        {
            Position=where,
            Scale=scale,
            FrameTime=frameTime,
            Rotation=FxRand(-.16f,.16f)
        });
    }

    private void QueueDeathBurst(Vector2 position,float delay,float scale,float duration,bool finale=false)
    {
        if(!_combatFxEnabled) return;
        _scheduledDeathBursts.Add(new ScheduledDeathBurst
        {
            Position=position,Delay=delay,Scale=scale,Duration=duration,Finale=finale
        });
    }

    private void BeginBossFinale(Vector2 position,bool riftBoss=false,bool infernoBoss=false)
    {
        // Don't switch to the shaded victory overlay until the final blast finishes.
        _bossFinaleTimer=2.25f;
        OnBossFinale(position,riftBoss,infernoBoss);
        _laserLanes.Clear();
        Alert(infernoBoss?"INFERNO TITAN  -  CORE MELTDOWN":
            riftBoss?"RIFT COLOSSUS  -  CORE COLLAPSE":
            "SENTINEL CORE  -  CRITICAL FAILURE",2.25f);
        QueueDeathBurst(position+new Vector2(-130,-68),.06f,.64f,.052f);
        QueueDeathBurst(position+new Vector2(120,66),.24f,.72f,.057f);
        QueueDeathBurst(position+new Vector2(-62,102),.46f,.88f,.062f);
        QueueDeathBurst(position+new Vector2(78,-107),.69f,.96f,.068f);
        QueueDeathBurst(position+new Vector2(-14,12),1.06f,1.19f,.076f);
        QueueDeathBurst(position,1.48f,1.85f,.087f,true);
        SpawnCombatBurst(position,Color.OrangeRed,44,160f,550f);
    }

    private void UpdateScheduledDeathBursts(float dt)
    {
        for(int i=_scheduledDeathBursts.Count-1;i>=0;i--)
        {
            ScheduledDeathBurst burst=_scheduledDeathBursts[i];
            burst.Delay-=dt;
            if(burst.Delay>0) continue;
            if(_combatFxEnabled)
            {
                SpawnExplosionSprite(burst.Position,burst.Scale,burst.Duration);
                SpawnCombatFlash(burst.Position,-Vector2.UnitX,Color.OrangeRed,burst.Finale?175f:85f,.16f);
                SpawnCombatBurst(burst.Position,Color.OrangeRed,burst.Finale?58:20,130f,burst.Finale?660f:390f);
            }
            _scheduledDeathBursts.RemoveAt(i);
        }
    }

    private void UpdateBossFinale(float dt)
    {
        if(_bossFinaleTimer<=0) return;
        // Pending enemy bullets/lasers are removed only after UpdateShots finishes.
        _shots.RemoveAll(s=>!s.Friendly);
        _laserLanes.Clear();
        _bossFinaleTimer=Math.Max(0,_bossFinaleTimer-dt);
        if(_bossFinaleTimer<=0)
        {
            if(_riftActive || _infernoActive) OpenSectorShop(true);
            else OpenSectorShop(false);
        }
    }

    private void UpdateExplosionSprites(float dt)
    {
        for(int i=_explosionSprites.Count-1;i>=0;i--)
        {
            var fx=_explosionSprites[i];
            fx.Age+=dt;
            if(fx.Age>=fx.FrameTime*_explosionFrames.Length) _explosionSprites.RemoveAt(i);
        }
    }

    private void UpdateDamageSmoke(float dt)
    {
        if(_mode!=GameMode.Playing || _pilot.Health>=_pilot.MaxHealth) return;
        _damageSmokeTimer-=dt;
        int severity=Math.Clamp(_pilot.MaxHealth-_pilot.Health,1,3);
        float interval=severity==1?.30f:severity==2?.18f:.10f;
        while(_damageSmokeTimer<=0)
        {
            EmitDamageSmoke(severity);
            _damageSmokeTimer+=interval;
        }
    }

    private void EmitDamageSmoke(int severity)
    {
        if(_damageSmokes.Count>72) _damageSmokes.RemoveAt(0);
        int row=severity==1?0:severity==2?1:(_pilot.Health<=1 && _fxRandom.NextDouble()<.55?3:2);
        _damageSmokes.Add(new DamageSmoke
        {
            Position=PlayerLocalOffset(new Vector2(-42+FxRand(-12f,6f),-8+FxRand(-18f,16f))),
            Velocity=new Vector2(FxRand(-180f,-65f)+_pilot.Velocity.X*.15f,FxRand(-44f,24f)+_pilot.Velocity.Y*.10f),
            Life=severity==1?0.78f:severity==2?1.00f:1.22f,
            Scale=severity==1?.55f:severity==2?.72f:.92f,
            Variant=_fxRandom.Next(0,3),
            SeverityRow=row,
            Rotation=FxRand(-.10f,.10f),
            Spin=FxRand(-.22f,.22f)
        });
    }

    private void UpdateDamageSmokeSprites(float dt)
    {
        for(int i=_damageSmokes.Count-1;i>=0;i--)
        {
            var puff=_damageSmokes[i];
            puff.Age+=dt;
            if(puff.Age>=puff.Life) { _damageSmokes.RemoveAt(i); continue; }
            puff.Position+=puff.Velocity*dt;
            puff.Velocity*=MathF.Max(0f,1f-dt*.8f);
            puff.Velocity+=new Vector2(-14f,-22f)*dt;
            puff.Rotation+=puff.Spin*dt;
        }
    }

    // Enemy damage smoke uses the approved player smoke sprites but a separate FX-only pool.
    // It never changes enemy positions, weapons, HP or gameplay random rolls.
    private void UpdateEnemyDamageSmoke(float dt)
    {
        if(!_combatFxEnabled) return;
        foreach(Enemy enemy in _enemies)
        {
            if(enemy.MaxHealth<=1 || enemy.Health<=0) continue;
            float damage=1f-enemy.Health/(float)enemy.MaxHealth;
            float threshold=enemy.Kind switch
            {
                EnemyKind.Scout=>.45f,EnemyKind.Interceptor=>.34f,
                EnemyKind.Heavy=>.25f,EnemyKind.Turret=>.25f,
                _=>.20f
            };
            if(damage<threshold) { enemy.SmokeTimer=0f; continue; }
            // Do not create smoke for offscreen enemies.
            if(enemy.Position.X < -80f || enemy.Position.X>ViewWidth+160f) continue;
            enemy.SmokeTimer-=dt;
            if(enemy.SmokeTimer>0f) continue;
            EmitEnemySmoke(enemy,damage);
            float interval=enemy.Kind switch
            {
                EnemyKind.Warden or EnemyKind.Sentinel=>.26f,
                EnemyKind.Heavy or EnemyKind.Turret=>.34f,
                _=>.43f
            };
            enemy.SmokeTimer=interval*MathHelper.Lerp(1.12f,.60f,damage);
        }
    }

    private void EmitEnemySmoke(Enemy enemy,float damage)
    {
        if(_enemySmokes.Count>=95) _enemySmokes.RemoveAt(0);
        bool capital=enemy.Kind==EnemyKind.Warden || enemy.Kind==EnemyKind.Sentinel;
        bool heavy=capital || enemy.Kind==EnemyKind.Heavy || enemy.Kind==EnemyKind.Turret;
        int row=damage>=.82f?3:damage>=.63f?2:damage>=.42f?1:0;
        float size=capital? .72f:heavy?.43f:.28f;
        // Enemy hulls fly left; smoke trails to the right, away from the damaged hull.
        _enemySmokes.Add(new DamageSmoke
        {
            Position=enemy.Position+new Vector2(capital?FxRand(-38f,55f):FxRand(0f,33f),FxRand(-27f,16f)),
            Velocity=new Vector2(FxRand(76f,174f),FxRand(-52f,12f)),
            Life=capital?1.28f:heavy?.95f:.72f,
            Scale=size*MathHelper.Lerp(.85f,1.30f,damage),
            Variant=_fxRandom.Next(3),SeverityRow=row,
            Rotation=FxRand(-.14f,.14f),Spin=FxRand(-.24f,.24f)
        });
    }

    private void UpdateEnemySmokeSprites(float dt)
    {
        for(int i=_enemySmokes.Count-1;i>=0;i--)
        {
            DamageSmoke puff=_enemySmokes[i];
            puff.Age+=dt;
            if(puff.Age>=puff.Life) { _enemySmokes.RemoveAt(i); continue; }
            puff.Position+=puff.Velocity*dt;
            puff.Velocity*=MathF.Max(0,1f-dt*.65f);
            puff.Velocity+=new Vector2(18f,-17f)*dt;
            puff.Rotation+=puff.Spin*dt;
        }
    }

    private void DrawEnemySmoke()
    {
        if(!_combatFxEnabled) return;
        foreach(DamageSmoke puff in _enemySmokes)
        {
            float t=MathHelper.Clamp(puff.Age/puff.Life,0f,1f);
            float opacity=(1f-t)*.75f;
            Rectangle frame=_damageSmokeFrames[puff.SeverityRow,puff.Variant];
            _batch.Draw(_damageSmokeSheet,puff.Position,frame,Color.White*opacity,puff.Rotation,
                new Vector2(frame.Width*.52f,frame.Height*.5f),puff.Scale*(.80f+.40f*t),
                SpriteEffects.None,0);
        }
    }

    // Warn of an imminent shot by lighting up the weapon port; actual firing logic is unchanged.
    private void DrawBossWeaponTelegraphs()
    {
        if(!_combatFxEnabled || _bossFinaleTimer>0f) return;
        foreach(Enemy enemy in _enemies)
        {
            bool warden=enemy.Kind==EnemyKind.Warden || enemy.Kind==EnemyKind.RiftGuardian ||
                enemy.Kind==EnemyKind.InfernoGuardian;
            if(!warden && enemy.Kind!=EnemyKind.Sentinel && enemy.Kind!=EnemyKind.RiftColossus &&
                enemy.Kind!=EnemyKind.InfernoTitan) continue;
            if(enemy.Position.X>(warden?1480f:1510f)) continue;
            float anticipation=warden?.32f:.26f;
            if(enemy.FireTimer<=0f || enemy.FireTimer>=anticipation) continue;
            float charge=1f-enemy.FireTimer/anticipation;
            Color tint=_riftActive?Color.MediumPurple:warden?Color.MediumPurple:Color.OrangeRed;
            Vector2 muzzle=enemy.Position+(warden?new Vector2(-132,0):new Vector2(-215,0));
            Vector2 heading=-Vector2.UnitX;
            if(warden)
            {
                heading=_pilot.Position-muzzle;
                if(heading.LengthSquared()<1f) heading=-Vector2.UnitX;
                heading.Normalize();
            }
            Vector2 forward=muzzle+heading*(32f+64f*charge);
            // Thin directional cue; no artificial circular particles or screen-wide flash.
            DrawBeam(muzzle,forward,3.2f+3f*charge,tint*(.25f+.47f*charge));
            DrawBeam(muzzle,forward,1.1f,Color.White*(.15f+.50f*charge));
        }
    }

    private void AddShockwave(Vector2 where,Color tint,float maxRadius,float life)
    {
        if(_shockwaves.Count>35) _shockwaves.RemoveAt(0);
        _shockwaves.Add(new Shockwave{Position=where,Tint=tint,MaxRadius=maxRadius,Life=life});
    }

    private void UpdateShockwaves(float dt)
    {
        for(int i=_shockwaves.Count-1;i>=0;i--)
        {
            _shockwaves[i].Age+=dt;
            if(_shockwaves[i].Age>=_shockwaves[i].Life) _shockwaves.RemoveAt(i);
        }
    }

    private void UpdateParticles(float dt)
    {
        for(int i=_particles.Count-1;i>=0;i--)
        {
            var p=_particles[i];p.Age+=dt;
            p.Position+=p.Velocity*dt;
            p.Velocity*=MathF.Max(0,1-dt*.7f);
            if(p.Age>=p.Life) _particles.RemoveAt(i);
        }
    }

    private float Rand(float a,float b)=>(float)(_random.NextDouble()*(b-a)+a);
    private float FxRand(float a,float b)=>(float)(_fxRandom.NextDouble()*(b-a)+a);

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.SetRenderTarget(_scene);
        GraphicsDevice.Clear(new Color(5,8,20));

        _batch.Begin(blendState:BlendState.AlphaBlend,samplerState:SamplerState.LinearClamp);
        DrawScrollingBackground();
        DrawStars();
        _batch.End();

        if(_mode!=GameMode.Title)
        {
            // Stability-first: do not inject random camera offsets into ship transforms.
            // Screen shake can return later as a separate, controlled background effect.
            Vector2 jitter=Vector2.Zero;
            _batch.Begin(blendState:BlendState.AlphaBlend,samplerState:SamplerState.LinearClamp,transformMatrix:Matrix.CreateTranslation(jitter.X,jitter.Y,0));
            DrawActors();
            DrawCoopOtherPilots();
            _batch.End();
            _batch.Begin(blendState:BlendState.Additive,samplerState:SamplerState.LinearClamp,transformMatrix:Matrix.CreateTranslation(jitter.X,jitter.Y,0));
            DrawLights();
            _batch.End();
        }
        _batch.Begin(blendState:BlendState.AlphaBlend,samplerState:SamplerState.LinearClamp);
        DrawOverlay();
        DrawCoopOverlay();
        if(_performanceOverlay && (_mode==GameMode.Playing || _mode==GameMode.Paused)) DrawPerformanceOverlay();
        if(_helpOpen) DrawHelpOverlay();
        if(_settingsOpen) DrawSettingsOverlay();
        if(_difficultyPromptOpen) DrawNewGameDifficultyPrompt();
        if(_flash>0) _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),Color.LightBlue*_flash*.40f);
        _batch.End();

        PresentSceneWithHitFeedback();
        CaptureCoopVisualSmoke();
        CountRenderedFrame(gameTime);
        base.Draw(gameTime);
    }

    private void DrawScrollingBackground()
    {
        if(_infernoActive)
        {
            DrawInfernoBackground();
            if(_dofEnabled) DrawInfernoDrifters();
            return;
        }
        if(_riftActive)
        {
            // Preserve a short crossfade from Sector 01, then remove its flat plate.
            float entryFade=MathHelper.Clamp(_riftFade,0f,1f);
            if(entryFade<.999f)
            {
                const int oldWidth=2500, oldHeight=1407;
                int oldX=-65-(int)Math.Min(420,_scroll*.027f);
                _batch.Draw(_hdBackdrop,new Rectangle(oldX,(ViewHeight-oldHeight)/2,oldWidth,oldHeight),Color.White);
            }
            // One approved, deliberately quiet purple-green backdrop across Sector 02.
            // No bright plate swapping: important combat sprites remain distinguishable.
            // A tiny slow pan adds motion without visibly shifting the distant station.
            int pan=(int)(MathF.Sin(_riftElapsed*.014f)*5f);
            Rectangle destination=new(-10+pan,-6,ViewWidth+20,ViewHeight+12);
            _batch.Draw(_riftCleanBackdrop,destination,Color.White*entryFade);
            // Only a very faint darkening during combat. Never hide the purple/green atmosphere.
            _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),new Color(0,3,10,6));
        }
        else
        {
            const int pictureW=2500, pictureH=1407;
            int x=-65-(int)Math.Min(420,_scroll*.027f);
            int y=(ViewHeight-pictureH)/2;
            _batch.Draw(_hdBackdrop,new Rectangle(x,y,pictureW,pictureH),Color.White);
            _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),new Color(0,7,20,65));
        }
        DrawParallaxRocks();
        if(_riftActive && _dofEnabled) DrawRiftDrifters(); // F6 shows/hides the entire DOF layer.
    }

    private void DrawRiftDrifters()
    {
        // Already blurred in PNG: farther wrecks drift more slowly and are smaller.
        // Rendered under ships, projectiles and combat FX: no collision or obscuring overlays.
        const float wrap=ViewWidth+880f;
        foreach(RiftDrifter d in _riftDrifters)
        {
            Texture2D tex=_riftDrifterArt[d.Layer,d.Variant];
            float x=(d.StartX+wrap*10f-_riftElapsed*d.Speed)%wrap-360f;
            float y=d.Y+MathF.Sin(_riftElapsed*.18f+d.Phase)*(d.Layer==0?8f:13f);
            float angle=d.Rotation+d.Spin*_riftElapsed;
            float size=d.Size/tex.Width;
            // Texture alpha stays solid on the wreck, with a small physical edge feather.
            // No additive bloom, purple fog, or translucent geometry shells.
            _batch.Draw(tex,new Vector2(x,y),null,Color.White,angle,
                new Vector2(tex.Width*.5f,tex.Height*.5f),size,SpriteEffects.None,0f);
        }
    }

    private void DrawParallaxRocks()
    {
        // Render furthest first, nearest last. The DOF textures already carry their
        // atmospheric color, edge feather and nearly opaque cores. Don't multiply
        // them by a translucent tint or overlay circular pseudo-DOF particles.
        const float wrapWidth=ViewWidth+700f;
        for(int layer=0;layer<3;layer++)
        foreach(ParallaxRock rock in _parallaxRocks)
        {
            if(rock.DepthLayer!=layer) continue;
            Texture2D tex=_dofEnabled?
                (_riftActive?_riftDofRocks[layer,rock.Variant]:_dofRocks[layer,rock.Variant]):
                _hdRocks[rock.Variant];
            if(_riftActive)
            {
                // The approved background already contains distant debris.
                // Only two or three small far-layer decorative rocks remain.
                if(layer!=0 || (rock.Variant%7)!=0) continue;
            }
            float x=(rock.StartX-_scroll*rock.SpeedFactor)%wrapWidth;
            if(x< -270f) x+=wrapWidth;
            float y=rock.Y+(float)Math.Sin(_travel*.23f+rock.Rotation)*(layer==0?2f:layer==1?3f:4f);
            // Each DOF atlas is normalized to 218 px central subject size.
            float scale=rock.Diameter/(_dofEnabled?218f:Math.Max(tex.Width,tex.Height));
            if(_riftActive) scale*=.42f;
            float angle=rock.Rotation+rock.Spin*_travel;
            Color tint=Color.White;
            if(_riftActive) tint=new Color(193,213,230,88);
            _batch.Draw(tex,new Vector2(x,y),null,tint,angle,
                new Vector2(tex.Width*.5f,tex.Height*.5f),scale,SpriteEffects.None,0);
        }
    }

    private void DrawStars()
    {
        foreach(Star star in _stars)
        {
            if(_riftActive && star.Phase>1.45f) continue; // restrained starfield over photographic nebula
            float blink=.64f+.36f*(float)Math.Sin(star.Phase*2.1f);
            int size=(int)Math.Max(1,star.Radius*2);
            _ui.Rect(_batch,new Rectangle((int)star.Position.X,(int)star.Position.Y,size,size),new Color(215,233,255)*(star.Alpha*blink*.85f));
            if(star.Speed>250)
                _ui.Rect(_batch,new Rectangle((int)star.Position.X-10,(int)star.Position.Y,11,1),new Color(145,196,255)*(star.Alpha*.16f));
        }
    }

    private void DrawCompactEnemyHealth(Enemy enemy,int barWidth,int barY)
    {
        // A thin but filled 12px life bar. Avoid UiRenderer.Bar: it reserves 8px padding.
        float fraction=MathHelper.Clamp(enemy.Health/(float)Math.Max(1,enemy.MaxHealth),0f,1f);
        int x=(int)enemy.Position.X-barWidth/2;
        _ui.Rect(_batch,new Rectangle(x-2,barY-2,barWidth+4,16),new Color(1,8,20,214));
        _ui.Frame(_batch,new Rectangle(x,barY,barWidth,12),new Color(93,164,193,222),1);
        int fillWidth=(int)MathF.Ceiling((barWidth-4)*fraction);
        if(fillWidth>0)
        {
            Color fill=enemy.HitFlash>.08f?new Color(255,239,150):
                fraction<.30f?new Color(252,80,88):
                fraction<.60f?new Color(255,166,79):new Color(92,221,227);
            _ui.Rect(_batch,new Rectangle(x+2,barY+3,fillWidth,6),fill);
        }
    }

    private void DrawEnemyReadabilityEdge(Enemy enemy,Vector2 artPos,Texture2D sprite,
        float scale,float rotation,SpriteEffects effects)
    {
        // Cosmetic only: identical position, pivot, scale and banking to the hull.
        // No circles, backlights, hitbox changes or artificial global bloom.
        if(!_enemyReadabilityEnabled) return;
        Texture2D edge=_enemyEdgeMasks[(int)enemy.Kind];
        if(edge.Width!=sprite.Width || edge.Height!=sprite.Height) return;
        _batch.Draw(edge,artPos,null,Color.White,rotation,
            new Vector2(sprite.Width*.5f,sprite.Height*.5f),scale,effects,0f);
    }

    private void DrawActors()
    {
        foreach(Shockwave wave in _shockwaves)
        {
            float t=MathHelper.Clamp(wave.Age/wave.Life,0,1);
            float radius=MathHelper.Lerp(13,wave.MaxRadius,t);
            _batch.Draw(_shockwaveRing,wave.Position,null,wave.Tint*((1-t)*.72f),0,new Vector2(_shockwaveRing.Width*.5f,_shockwaveRing.Height*.5f),radius/(_shockwaveRing.Width*.5f),SpriteEffects.None,0);
        }
        // Collision debris remains crisp for readable hazards. Only purely decorative
        // background debris uses depth of field.
        foreach(Debris rock in _debris)
        {
            Texture2D tex=rock.IsRiftShard?_riftHazard:_hdRocks[Math.Clamp(rock.Variant,0,_hdRocks.Length-1)];
            float scale=(rock.Radius*2f)/tex.Width;
            _batch.Draw(tex,rock.Position,null,_infernoActive?new Color(255,176,121):Color.White,rock.Rotation,
                new Vector2(tex.Width*.5f,tex.Height*.5f),scale,SpriteEffects.None,0);
        }
        foreach(LaserLane lane in _laserLanes)
        {
            bool active=lane.Age>=lane.Warning;
            float flicker=.55f+.45f*(float)Math.Sin(_travel*25f);
            Color color=active?Color.OrangeRed:Color.Orange;
            DrawBeam(new Vector2(0,lane.Y),new Vector2(ViewWidth,lane.Y),active?66:10,color*(active?.7f:.25f+.2f*flicker));
            if(active) DrawBeam(new Vector2(0,lane.Y),new Vector2(ViewWidth,lane.Y),15,Color.White*.90f);
        }
        foreach(Pickup p in _pickups)
        {
            float pulse=1f+.08f*(float)Math.Sin(p.Age*6.4f);
            Texture2D icon=PickupIconTexture(p.Kind);
            // Borderless collectible: keep the transparent icon and a tiny shadow,
            // never a square UI panel, frame or rotating shield ring in the playfield.
            Vector2 origin=new(icon.Width*.5f,icon.Height*.5f);
            float iconScale=(p.Kind==PickupKind.RareCache ? .60f : p.Kind==PickupKind.Credits ? .44f : .41f)*pulse;
            _batch.Draw(icon,p.Position+new Vector2(2,3),null,new Color(5,9,22)*.72f,0,origin,iconScale*1.08f,SpriteEffects.None,0);
            _batch.Draw(icon,p.Position,null,Color.White,0,origin,iconScale,SpriteEffects.None,0);
        }
        DrawEnemySmoke();
        foreach(Enemy e in _enemies)
        {
            // Visual-only subpixel impact jolt; collision center remains e.Position.
            Vector2 artPos=e.Position+new Vector2(MathF.Sin(e.HitReaction*89f)*e.HitReaction*48f,0f);
            Color shipTint=e.HitFlash>0?Color.Lerp(Color.White,Color.Cyan,MathHelper.Clamp(e.HitFlash,0f,1f)):Color.White;
            float damage=1f-e.Health/(float)e.MaxHealth;
            float scorchAlpha=MathHelper.Clamp((damage-.18f)/.60f,0f,1f);
            if(IsInfernoBoss(e.Kind) || e.Kind==EnemyKind.InfernoBossTurret)
            {
                DrawInfernoBoss(e,artPos,shipTint);
            }
            else if(e.Kind==EnemyKind.RiftGuardian || e.Kind==EnemyKind.RiftColossus || e.Kind==EnemyKind.RiftBossTurret)
            {
                bool guardian=e.Kind==EnemyKind.RiftGuardian;
                bool colossus=e.Kind==EnemyKind.RiftColossus;
                Texture2D tex=guardian?_riftGuardianSprite:colossus?_riftColossusSprite:_riftBossTurretSprite;
                float scale=guardian?.48f:colossus?.54f:.34f;
                _batch.Draw(tex,artPos,null,shipTint,0f,
                    new Vector2(tex.Width*.5f,tex.Height*.5f),scale,SpriteEffects.None,0f);
                if(scorchAlpha>0f)
                    _batch.Draw(_purpleGlow,artPos,null,new Color(160,64,200)*(scorchAlpha*.13f),0f,
                        new Vector2(_purpleGlow.Width*.5f,_purpleGlow.Height*.5f),colossus?1.5f:.8f,SpriteEffects.None,0f);
            }
            else if(IsInfernoEnemy(e.Kind))
            {
                int idx=(int)e.Kind-(int)EnemyKind.InfernoScorch;
                Texture2D tex=_infernoEnemies[idx];
                float visualSize=(e.Kind==EnemyKind.InfernoTank||e.Kind==EnemyKind.InfernoBomber)?.73f:.62f;
                visualSize*=CombatZoom;
                float banking=MathHelper.Clamp(e.Banking,-1f,1f)*.045f;
                _batch.Draw(tex,artPos,null,shipTint,banking,
                    new Vector2(tex.Width*.5f,tex.Height*.5f),visualSize,SpriteEffects.None,0f);
            }
            else if(IsRiftEnemy(e.Kind))
            {
                int idx=(int)e.Kind-(int)EnemyKind.RiftDart;
                Texture2D tex=_riftEnemies[idx];
                float visualSize=(e.Kind==EnemyKind.RiftMauler?.46f:e.Kind==EnemyKind.RiftPrism?.37f:.32f)*CombatZoom;
                float bank=MathHelper.Clamp(e.Banking,-1f,1f)*.064f;
                DrawEnemyReadabilityEdge(e,artPos,tex,visualSize,bank,SpriteEffects.None);
                _batch.Draw(tex,artPos,null,shipTint,bank,
                    new Vector2(tex.Width*.5f,tex.Height*.5f),visualSize,SpriteEffects.None,0f);
                // A subtle damage glint follows the approved hit / smoke system.
                if(scorchAlpha>0)
                    _batch.Draw(_redGlow,artPos+new Vector2(24,3),null,Color.OrangeRed*(scorchAlpha*.14f),0f,
                        new Vector2(_redGlow.Width*.5f,_redGlow.Height*.5f),.45f,SpriteEffects.None,0f);
            }
            else if(e.Kind==EnemyKind.Scout || e.Kind==EnemyKind.Interceptor || e.Kind==EnemyKind.Heavy)
            {
                // Fixed enemy hulls: the earlier generated angle frames were not identical models.
                int index=e.Kind switch {EnemyKind.Interceptor=>0,EnemyKind.Heavy=>1,_=>2};
                Texture2D hull=_hdEnemies[index];
                float scale=(e.Kind==EnemyKind.Heavy?.50f:e.Kind==EnemyKind.Interceptor?.42f:.41f)*CombatZoom;
                float enemyPitch=MathHelper.Clamp(e.Banking,-1f,1f)*.055f;
                DrawEnemyReadabilityEdge(e,artPos,hull,scale,enemyPitch,SpriteEffects.FlipHorizontally);
                _batch.Draw(hull,artPos,null,shipTint,enemyPitch,
                    new Vector2(hull.Width*.5f,hull.Height*.5f),scale,SpriteEffects.FlipHorizontally,0);
                // Old damage overlays belonged to the retired vector hull shapes.
                // Keep the approved sprite smoke/hit flash rather than misaligned art.
            }
            else
            {
                // Legacy Sector 01 bosses and fixed defenses retain their visuals.
                Texture2D tex=e.Kind switch {EnemyKind.Turret=>_turretSprite,EnemyKind.Warden=>_wardenSprite,_=>_sentinel};
                float scale=(e.Kind==EnemyKind.Sentinel?1.0f:e.Kind==EnemyKind.Warden?.83f:.60f)*CombatZoom;
                SpriteEffects facing=e.Kind==EnemyKind.Sentinel?SpriteEffects.None:SpriteEffects.FlipHorizontally;
                Vector2 origin=new(tex.Width*.5f,tex.Height*.5f);
                DrawEnemyReadabilityEdge(e,artPos,tex,scale,0f,facing);
                _batch.Draw(tex,artPos,null,shipTint,0,origin,scale,facing,0);
                // No legacy scorch hull overlay; it does not fit the new HD model.
            }
            // Every multi-hit regular enemy (including the Rift turrets) shows REAL remaining HP.
            // The old 4px Bar() had an 8px inner margin: its colored fill was always invisible.
            if(e.MaxHealth>1 && e.Health>0 && e.Kind!=EnemyKind.Sentinel &&
               e.Kind!=EnemyKind.Warden && e.Kind!=EnemyKind.RiftGuardian &&
               e.Kind!=EnemyKind.RiftColossus && !IsInfernoBoss(e.Kind))
            {
                int barWidth=e.Kind==EnemyKind.RiftBossTurret || e.Kind==EnemyKind.InfernoBossTurret?74:
                    e.Kind==EnemyKind.RiftMauler||e.Kind==EnemyKind.Heavy?90:66;
                int barY=(int)MathHelper.Clamp(e.Position.Y+e.Radius*.80f+15f,169f,ViewHeight-22f);
                DrawCompactEnemyHealth(e,barWidth,barY);
            }
        }
        foreach(DamageSmoke puff in _damageSmokes)
        {
            float t=MathHelper.Clamp(puff.Age/puff.Life,0f,1f);
            float alpha=(1f-t)*(puff.SeverityRow>=2?.92f:.78f);
            float scale=puff.Scale*(.92f+.45f*t);
            Rectangle src=_damageSmokeFrames[puff.SeverityRow,puff.Variant];
            _batch.Draw(_damageSmokeSheet,puff.Position,src,Color.White*alpha,puff.Rotation,
                new Vector2(src.Width*.52f,src.Height*.50f),scale,SpriteEffects.FlipHorizontally,0);
        }

        DrawTacticalAimCues();
        if((_mode==GameMode.Playing || _mode==GameMode.Paused) && (!CoopActive || _pilot.Lives>0))
        {
            // Always show the hull: old invulnerability flashing made it disappear repeatedly.
            float invulnerabilityGlow=_pilot.Invincible>0 ? .12f+.04f*(float)Math.Sin(_travel*6f) : 0f;
            Color hullTint=Color.Lerp(Color.White,Color.LightSkyBlue,invulnerabilityGlow);
            _batch.Draw(_hdPlayer,_pilot.Position,null,hullTint,PlayerRoll,
                PlayerOrigin,PlayerScale,SpriteEffects.None,0);
            int damageTier=Math.Clamp(_pilot.MaxHealth-_pilot.Health,0,_hullDamageLayers.Length);
            if(damageTier>0)
                _batch.Draw(_hullDamageLayers[damageTier-1],_pilot.Position,null,Color.White,PlayerRoll,
                    PlayerOrigin,PlayerScale,SpriteEffects.None,0);
        }
        if((_mode==GameMode.Playing || _mode==GameMode.Paused) && _pilot.Shield>0)
        {
            float pulse=1f+.028f*(float)Math.Sin(_travel*5.6f);
            float intensity=.42f+.08f*_pilot.Shield;
            _batch.Draw(_shieldShell,_pilot.Position,null,new Color(150,240,255)*(intensity*.80f),_travel*.45f,new Vector2(_shieldShell.Width/2f,_shieldShell.Height/2f),.78f*pulse*CombatZoom,SpriteEffects.None,0);
            _batch.Draw(_shieldRing,_pilot.Position,null,new Color(60,210,255)*(intensity*.36f),-_travel*.28f,new Vector2(_shieldRing.Width/2f,_shieldRing.Height/2f),.62f*pulse*CombatZoom,SpriteEffects.None,0);
            _batch.Draw(_blueGlow,_pilot.Position,null,new Color(70,220,255)*(.12f+.05f*_pilot.Shield),0,new Vector2(_blueGlow.Width/2f,_blueGlow.Height/2f),.40f*pulse*CombatZoom,SpriteEffects.None,0);
        }
        if((_mode==GameMode.Playing || _mode==GameMode.Paused) && _pilot.DroneActive)
        {
            _batch.Draw(_droneSprite,_pilot.DronePosition,null,Color.White,0,new Vector2(_droneSprite.Width/2f,_droneSprite.Height/2f),.43f*CombatZoom,SpriteEffects.None,0);
        }
        foreach(Projectile p in _shots)
        {
            Color beamTint=p.Tint;
            Vector2 dir=p.Velocity;
            if(dir.LengthSquared()<1f) dir=Vector2.UnitX;
            dir.Normalize();
            if(p.Friendly && !p.IsDroneShot)
            {
                if(_primaryVfxEnabled)
                {
                    Texture2D sprite=_primaryShots[(int)p.PrimaryMode];
                    float rotation=(float)Math.Atan2(dir.Y,dir.X);
                    float scale=p.PrimaryMode switch
                    {
                        PrimaryWeaponMode.Lance=>.59f,
                        PrimaryWeaponMode.Arc=>.51f,
                        PrimaryWeaponMode.Gatling=>.36f,
                        _=>.54f
                    };
                    _batch.Draw(sprite,p.Position,null,Color.White,rotation,
                        new Vector2(sprite.Width*.60f,sprite.Height*.5f),scale,SpriteEffects.None,0f);
                }
                else
                {
                    // F8 comparison restores the proven geometric 0.6.1 shot styles.
                    switch(p.PrimaryMode)
                    {
                        case PrimaryWeaponMode.Lance:
                            DrawBeam(p.Position-dir*73,p.Position+dir*22,8f,beamTint*.22f);
                            DrawBeam(p.Position-dir*60,p.Position+dir*20,3.8f,beamTint*.84f);
                            DrawBeam(p.Position-dir*37,p.Position+dir*16,1.7f,Color.White*.95f);
                            break;
                        case PrimaryWeaponMode.Gatling:
                            DrawBeam(p.Position-dir*22,p.Position+dir*13,11f,beamTint*.18f);
                            DrawBeam(p.Position-dir*17,p.Position+dir*12,4f,beamTint);
                            DrawBeam(p.Position-dir*6,p.Position+dir*9,1.6f,Color.White);
                            break;
                        case PrimaryWeaponMode.Arc:
                            DrawBeam(p.Position-dir*28,p.Position+dir*24,25f,beamTint*.19f);
                            DrawBeam(p.Position-dir*18,p.Position+dir*18,11f,beamTint*.85f);
                            DrawBeam(p.Position-dir*6,p.Position+dir*11,4f,Color.White*.87f);
                            break;
                        default:
                            DrawBeam(p.Position-dir*45,p.Position+dir*17,17f,beamTint*.25f);
                            DrawBeam(p.Position-dir*38,p.Position+dir*17,7f,beamTint);
                            DrawBeam(p.Position-dir*8,p.Position+dir*12,3f,Color.White*.95f);
                            break;
                    }
                }
            }
            else
            {
                // Enemy shots: thin dark separation followed by the original
                // orange/purple weapon color and a crisp core. No bloom blob.
                float len=p.Friendly?60:43;
                float width=p.Friendly?1f:MathHelper.Clamp(p.Radius/10f,.75f,1.55f);
                Vector2 tail=p.Position-dir*len*.72f,tip=p.Position+dir*len*.28f;
                DrawBeam(tail,tip,10f*width,new Color(3,9,23)*.80f);
                DrawBeam(tail,tip,5.3f*width,beamTint*.93f);
                DrawBeam(p.Position-dir*9,p.Position+dir*12,1.9f*width,Color.White*.95f);
            }
        }
        if(_beamActive) DrawContinuousBeam();
        DrawPrimaryImpactSprites();
        DrawPrimaryReadouts();
    }

    private Texture2D PickupIconTexture(PickupKind kind)=>kind switch
    {
        PickupKind.Credits=>_pickupCredits,
        PickupKind.RareCache=>_pickupRareCache,
        PickupKind.Weapon=>_pickupWeapon,
        PickupKind.Repair=>_pickupRepair,
        PickupKind.Shield=>_pickupShield,
        _=>_pickupDrone
    };

    private static Color PickupColor(PickupKind kind)=>kind switch
    {
        PickupKind.Credits=>new Color(255,200,80),
        PickupKind.RareCache=>new Color(255,148,66),
        PickupKind.Weapon=>Color.Cyan,
        PickupKind.Repair=>Color.LawnGreen,
        PickupKind.Shield=>Color.DeepSkyBlue,
        _=>Color.MediumPurple
    };

    private void DrawBeam(Vector2 start,Vector2 end,float thickness,Color tint)
    {
        Vector2 path=end-start;
        float length=path.Length();
        if(length<.01f) return;
        _batch.Draw(_pixel,start,null,tint,(float)Math.Atan2(path.Y,path.X),new Vector2(0,.5f),new Vector2(length,thickness),SpriteEffects.None,0);
    }

    private void DrawTacticalAimCues()
    {
        if(!_combatFxEnabled || _mode!=GameMode.Playing) return;
        foreach(Enemy e in _enemies)
        {
            if(e.Kind==EnemyKind.Sentinel || e.Kind==EnemyKind.Warden) continue;
            if(e.Kind!=EnemyKind.Turret && e.Kind!=EnemyKind.Heavy && e.MotionPattern!=6 && e.MotionPattern!=8) continue;
            if(e.Position.X>=ViewWidth-130f || e.Position.X<=180f) continue;
            const float warningWindow=.43f;
            if(e.FireTimer<=0f || e.FireTimer>warningWindow) continue;
            float progress=1f-e.FireTimer/warningWindow;
            Vector2 muzzle=e.Position+new Vector2(-50f,0);
            Vector2 dir=_pilot.Position-muzzle;
            if(dir.LengthSquared()<1f) continue;
            dir.Normalize();
            // Short, tapered direction indicator. Never a full-screen beam or hitbox.
            float intensity=(.16f+.44f*progress);
            DrawBeam(muzzle,muzzle+dir*(85f+100f*progress),1.5f+progress*1.6f,
                new Color(255,162,94)*intensity);
        }
    }

    private void DrawCombatFx()
    {
        if(!_combatFxEnabled) return;
        foreach(CombatSpark spark in _combatSparks)
        {
            float life=MathHelper.Clamp(1f-spark.Age/spark.Life,0f,1f);
            Vector2 forward=spark.Velocity;
            if(forward.LengthSquared()<.0001f) forward=Vector2.UnitX;
            forward.Normalize();
            float extent=spark.Length*(.48f+.52f*life);
            DrawBeam(spark.Position-forward*extent,spark.Position+forward*extent*.12f,
                spark.Thickness*1.6f,spark.Tint*(life*.32f));
            DrawBeam(spark.Position-forward*extent*.64f,spark.Position+forward*extent*.08f,
                Math.Max(1f,spark.Thickness*.43f),Color.White*(life*.78f));
        }
        foreach(CombatFlash flash in _combatFlashes)
        {
            float life=MathHelper.Clamp(1f-flash.Age/flash.Life,0f,1f);
            float size=flash.Size*(.45f+.55f*life);
            Vector2 normal=new(-flash.Direction.Y,flash.Direction.X);
            DrawBeam(flash.Position-flash.Direction*size*.55f,flash.Position+flash.Direction*size*.90f,
                9f*life+1f,flash.Tint*(life*.55f));
            DrawBeam(flash.Position-normal*size*.25f,flash.Position+normal*size*.25f,
                4.5f*life+1f,Color.White*(life*.55f));
            DrawBeam(flash.Position-flash.Direction*size*.14f,flash.Position+flash.Direction*size*.38f,
                2.2f*life+1f,Color.White*(life*.88f));
        }
    }

    private void DrawLights()
    {
        DrawCombatFx();
        DrawBossWeaponTelegraphs();
        DrawBossBreaches();
        foreach(ExplosionSprite fx in _explosionSprites)
        {
            int frame=Math.Clamp((int)(fx.Age/fx.FrameTime),0,_explosionFrames.Length-1);
            float t=MathHelper.Clamp(fx.Age/(fx.FrameTime*_explosionFrames.Length),0f,1f);
            float fade=t<.72f?1f:MathHelper.Clamp(1f-(t-.72f)/.28f,0f,1f);
            Rectangle src=_explosionFrames[frame];
            Vector2 origin=new(src.Width*.5f,src.Height*.5f);
            // Additive rendering removes the milky alpha-blended look of the sprite edges.
            _batch.Draw(_explosionSheet,fx.Position,src,new Color(255,240,228)*(fade*.95f),fx.Rotation,origin,fx.Scale,SpriteEffects.None,0);
            _batch.Draw(_explosionSheet,fx.Position,src,new Color(255,170,92)*(fade*.52f),fx.Rotation,origin,fx.Scale*1.06f,SpriteEffects.None,0);
        }
        foreach(Particle p in _particles)
        {
            float rem=MathHelper.Clamp(1-p.Age/p.Life,0,1);
            Texture2D light=p.Tint==Color.Orange||p.Tint==Color.OrangeRed? _orangeGlow:_blueGlow;
            _batch.Draw(light,p.Position,null,p.Tint*(rem*(_combatFxEnabled ? .55f : 1f)),0,
                new Vector2(64,64),p.Size/31f,SpriteEffects.None,0);
        }
        if(_beamActive)
        {
            float w=8f+3.4f*(Math.Clamp(_pilot.WeaponLevel,1,7)-1);
            float pulse=.64f+.30f*MathF.Sin(_beamPhase*2.15f);
            DrawBeam(_beamStart,_beamEnd,w*1.45f,new Color(110,48,235)*(pulse*.17f));
            _batch.Draw(_purpleGlow,_beamEnd,null,Color.White*(pulse*.30f),0f,
                new Vector2(_purpleGlow.Width*.5f,_purpleGlow.Height*.5f),.34f+w*.015f,SpriteEffects.None,0f);
        }
        foreach(Projectile p in _shots)
        {
            Texture2D glow=p.IsDroneShot?_purpleGlow:p.Friendly?_blueGlow:
                p.Tint==Color.MediumPurple?_purpleGlow:_orangeGlow;
            Color tint=p.Friendly&&!p.IsDroneShot?p.Tint:(p.Friendly?Color.White:p.Tint);
            float intensity=(p.Friendly && !p.IsDroneShot) ? (_primaryVfxEnabled ? .19f : .40f) :
                (p.Friendly ? .68f : _enemyShotGlow/100f*.65f);
            float scale=(p.Friendly && !p.IsDroneShot)
                ? (p.PrimaryMode==PrimaryWeaponMode.Arc ? 0.72f : p.PrimaryMode==PrimaryWeaponMode.Lance ? 0.42f : 0.55f)
                : (p.Friendly ? 0.85f : .46f+_enemyShotGlow/100f*.28f);
            _batch.Draw(glow,p.Position,null,tint*intensity,0,new Vector2(glow.Width*.5f,glow.Height*.5f),scale,SpriteEffects.None,0);
        }
        foreach(Pickup p in _pickups)
        {
            Texture2D glow=p.Kind==PickupKind.Drone?_purpleGlow:
                p.Kind==PickupKind.Repair || p.Kind==PickupKind.Credits || p.Kind==PickupKind.RareCache?_orangeGlow:_blueGlow;
            Color glowColor=p.Kind==PickupKind.Credits || p.Kind==PickupKind.RareCache?new Color(255,183,55)*.30f:Color.White*.76f;
            float glowSize=(p.Kind==PickupKind.Credits || p.Kind==PickupKind.RareCache) ? .50f : 1.2f;
            _batch.Draw(glow,p.Position,null,glowColor,0,new Vector2(64,64),glowSize,SpriteEffects.None,0);
        }
        if(_mode==GameMode.Playing)
        {
            // Separate plume sprite + subtle glow: stable hull, animated propulsion only.
            float glowPulse=.90f+.10f*(float)Math.Sin(_travel*12f);
            Vector2 plumePos=PlayerLocalOffset(new Vector2(-150,2));
            _batch.Draw(_hdPlayerPlume,plumePos,null,Color.White*(.74f*glowPulse),PlayerRoll,
                new Vector2(_hdPlayerPlume.Width*.92f,_hdPlayerPlume.Height*.5f),(0.34f+0.02f*(float)Math.Sin(_travel*12f))*CombatZoom,SpriteEffects.None,0);
            Vector2 glowPos=PlayerLocalOffset(new Vector2(-138,1));
            _batch.Draw(_blueGlow,glowPos,null,Color.Cyan*(.26f*glowPulse),0,
                new Vector2(_blueGlow.Width*.5f,_blueGlow.Height*.5f),.70f*glowPulse,SpriteEffects.None,0);
        }
        if((_mode==GameMode.Playing || _mode==GameMode.Paused) && _pilot.DroneActive)
            _batch.Draw(_purpleGlow,_pilot.DronePosition+new Vector2(-35,0),null,Color.MediumPurple*.82f,0,new Vector2(64,64),.68f,SpriteEffects.None,0);
        foreach(Enemy e in _enemies)
        {
            if(e.Kind==EnemyKind.Sentinel)
                _batch.Draw(_redGlow,e.Position+new Vector2(-63,0),null,Color.White*.82f,0,new Vector2(64,64),2.2f,SpriteEffects.None,0);
            if(e.Kind==EnemyKind.Warden)
                _batch.Draw(_purpleGlow,e.Position+new Vector2(-95,0),null,Color.White*.74f,0,new Vector2(64,64),1.65f,SpriteEffects.None,0);
        }
        foreach(LaserLane lane in _laserLanes)
        {
            if(lane.Age<lane.Warning) continue;
            DrawBeam(new Vector2(0,lane.Y),new Vector2(ViewWidth,lane.Y),118,Color.OrangeRed*.30f);
        }
    }

    private void UpdateAudioSettings(KeyboardState keys)
    {
        const int count=14;
        if(MenuUp(keys)) _settingsSelection=(_settingsSelection+count-1)%count;
        if(MenuDown(keys)) _settingsSelection=(_settingsSelection+1)%count;
        bool right=MenuRight(keys);
        bool left=MenuLeft(keys);
        bool activate=MenuConfirm(keys);
        if(!right&&!left&&!activate) return;
        ChangeAudioSetting(_settingsSelection,left?-1:1,activate);
    }

    private void ChangeAudioSetting(int index,int direction,bool activate)
    {
        if(index<=1)
        {
            if(_audio!=null)
            {
                bool music=index==0;
                _audio.SetVolume(music,(music?_audio.MusicVolume:_audio.SfxVolume)+(direction*5));
                if(!music) _audio.Effect("pickup",0,.3f);
            }
            return;
        }
        switch(index)
        {
            case 2: SetScreenMode();break;
            case 3: ChangeAspectRatio(direction);break;
            case 4: OpenResolutionDropdown();break;
            case 5:
                _msaaRequest=(_msaaRequest+MsaaOptions.Length+direction)%MsaaOptions.Length;
                // MSAA changes ONLY the scene render target; do not touch window sizing.
                RebuildDisplayTargets();break;
            case 6: _vsyncEnabled=!_vsyncEnabled;ApplyDisplayConfiguration();break;
            case 7: _dofEnabled=!_dofEnabled;break;
            case 8: _combatFxEnabled=!_combatFxEnabled;break;
            case 9: _enemyReadabilityEnabled=!_enemyReadabilityEnabled;break;
            case 10: _primaryVfxEnabled=!_primaryVfxEnabled;break;
            case 11: _enemyShotGlow=Math.Clamp(_enemyShotGlow+direction*10,0,100);break;
            case 12: _hitEffectStrength=Math.Clamp(_hitEffectStrength+direction*10,0,100);break;
            case 13: _selectedDifficulty=(DifficultyMode)(((int)_selectedDifficulty+(direction<0?2:1))%3);break;
        }
        SaveGuiPreferences();
    }

private void DrawGuiBackground()
{
    _batch.Draw(_guiBackground,new Rectangle(0,0,ViewWidth,ViewHeight),Color.White);
    _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),new Color(1,6,17,100));
}

private void DrawCleanPanel(Rectangle r,Color accent)
{
    // Deliberately no ornate border PNGs or double nested frames.
    _ui.Rect(_batch,r,new Color(3,17,35,235));
    _ui.Rect(_batch,new Rectangle(r.X,r.Y,5,r.Height),accent*.83f);
    _ui.Rect(_batch,new Rectangle(r.X+5,r.Y,r.Width-5,2),accent*.38f);
}

private void DrawTechPanel(Rectangle r,Color accent)
    =>DrawCleanPanel(r,accent);

private void DrawMenuButton(Rectangle rect,string title,bool selected,bool disabled,float size)
{
    Color gold=new(255,206,104),cyan=new(107,221,252);
    Color fill=selected?new Color(24,73,110,248):new Color(5,24,45,234);
    _ui.Rect(_batch,rect,fill);
    _ui.Rect(_batch,new Rectangle(rect.X,rect.Y,selected?10:4,rect.Height),
        selected?gold:new Color(46,122,167));
    if(selected)
    {
        _ui.Rect(_batch,new Rectangle(rect.X+10,rect.Bottom-4,rect.Width-10,4),cyan*.66f);
        _ui.Text(_batch,">",new Vector2(rect.Right-50,rect.Y+(rect.Height-72f*.55f)/2),.55f,gold);
    }
    _ui.TextFit(_batch,title,new Vector2(rect.X+40,rect.Y+(rect.Height-72*size)*.46f),
        size,rect.Width-125,disabled?new Color(111,140,164):selected?gold:Color.White);
}

private void ModalPanel(string heading)
{
    _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),new Color(0,4,15,245));
    _ui.Rect(_batch,new Rectangle(0,0,ViewWidth,212),new Color(4,19,40,245));
    _ui.Rect(_batch,new Rectangle(0,207,ViewWidth,5),new Color(76,213,246));
    _ui.TextShadow(_batch,heading,new Vector2(108,74),1.07f,Color.White);
}

private void DrawHelpOverlay()
{
    ModalPanel("FLIGHT REFERENCE");
    Color cyan=new(105,223,252),muted=new(190,219,239),gold=new(255,205,107);
    string[] titles={"FLIGHT & WEAPONS","MENUS & SHOP","GAMEPAD CONTROLS","TEST & DEBUG KEYS"};
    _ui.TextShadow(_batch,"0"+(_helpPage+1)+" / 04    "+titles[_helpPage],new Vector2(106,243),.62f,cyan);
    string[] keysA,labelsA;
    if(_helpPage==0)
    {
        keysA=new[]{_bindings[GameAction.MoveUp]+" / "+_bindings[GameAction.MoveDown],
            _bindings[GameAction.MoveLeft]+" / "+_bindings[GameAction.MoveRight],
            _bindings[GameAction.Fire].ToString(),_bindings[GameAction.Precision].ToString(),
            _bindings[GameAction.WeaponNext].ToString(),_bindings[GameAction.WeaponPrevious].ToString(),
            _bindings[GameAction.Pulse]+" / "+_bindings[GameAction.Lance],
            _bindings[GameAction.Arc]+" / "+_bindings[GameAction.Gatling]+" / "+_bindings[GameAction.Beam],
            _bindings[GameAction.Pause]+" / ESC"};
        labelsA=new[]{"MOVE VERTICALLY / MOUSE","MOVE HORIZONTALLY / MOUSE","FIRE / LEFT MOUSE",
            "PRECISION / RIGHT MOUSE","NEXT OWNED WEAPON","PREVIOUS OWNED WEAPON",
            "PULSE / LANCE SELECT","ARC / GATLING / BEAM","PAUSE / RESUME"};
    }
    else if(_helpPage==1)
    {
        keysA=new[]{NavigatePrompt,ConfirmPrompt,BackPrompt,HorizontalPrompt,ContinuePrompt,
            HelpPrompt,SettingsPrompt,PagePrompt,"DELETE (KEYBOARD)",ConfirmPrompt};
        labelsA=new[]{"NAVIGATE MENUS","CONFIRM / BUY","RETURN / CLOSE",
            "CHANGE SHOP CATEGORY","NEXT SECTOR AFTER SHOP","OPEN / CLOSE HELP",
            "OPEN SETTINGS","SETTINGS PAGE SWITCH","RESET KEY BINDINGS", "NEW GAME: CHOOSE DIFFICULTY"};
    }
    else if(_helpPage==2)
    {
        keysA=_usingDualSense
            ?new[]{"\uE009 / \uE00A","\uE008","\uE012 / \uE006","\uE011","\uE00F / \uE010","\uE015","\uE004 / \uE005","\uE016 / \uE017","\uE007","\uE006"}
            :new[]{"\uE009 / \uE00A","\uE008","\uE00E / \uE002","\uE00D","\uE00B / \uE00C","\uE013","\uE000 / \uE001","\uE014","\uE003","\uE002"};
        labelsA=new[]{"ANALOG SHIP MOVEMENT","DIGITAL MOVE / MENUS",
            "FIRE / HOLD BEAM","PRECISION MOVEMENT","PREV / NEXT WEAPON",
            "PAUSE / NEXT SECTOR","CONFIRM / CANCEL","OPEN / CLOSE HELP","OPEN / CLOSE SETTINGS","RESUME CHECKPOINT"};
    }
    else
    {
        keysA=new[]{"F2","F3","F4","F5","F6","F7","F8","F9","F10","F11"};
        labelsA=new[]{"SECTOR BOSS TEST","SHOP CREDIT TEST","ENEMY OUTLINES",
            "F5: NEXT SECTOR TEST","DEPTH OF FIELD","COMBAT FX",
            "PRIMARY WEAPON VFX","WEAPON LEVEL TEST","PERFORMANCE HUD","FULLSCREEN TOGGLE"};
    }
    for(int i=0;i<keysA.Length;i++)
    {
        int col=i/5,row=i%5;
        int x=98+col*870,y=332+row*123;
        Rectangle rect=new(x,y,846,114);
        _ui.Rect(_batch,rect,new Color(6,26,48,233));
        _ui.Rect(_batch,new Rectangle(x,y,7,114),col==0?cyan:new Color(161,125,250));
        _ui.TextFit(_batch,keysA[i].ToUpperInvariant(),new Vector2(x+27,y+7),.61f,780,gold);
        _ui.TextFit(_batch,labelsA[i],new Vector2(x+30,y+66),.47f,785,muted);
    }
    _ui.TextShadow(_batch,HorizontalPrompt+" PAGE     "+HelpPrompt+" / "+BackPrompt+" CLOSE",
        new Vector2(ViewWidth/2,1009),.46f,cyan,true);
}

private void DrawSettingsOverlay()
{
    ModalPanel("SYSTEM CONFIGURATION");
    Color cyan=new(105,225,252),gold=new(255,205,107),muted=new(188,218,240);
    DrawMenuButton(new Rectangle(103,239,548,85),"AUDIO / VIDEO",_settingsTab==0,false,.60f);
    DrawMenuButton(new Rectangle(677,239,548,85),"KEY BINDINGS",_settingsTab==1,false,.60f);
    DrawMenuButton(new Rectangle(1251,239,548,85),"CONTROLLERS",_settingsTab==2,false,.60f);
    if(_settingsTab==2) { DrawControllerSettings();return; }
    if(_settingsTab==1)
    {
        int page=_controlsSelection/8;
        int first=page*8;
        _ui.TextShadow(_batch,"BINDINGS PAGE "+(page+1)+" / "+((ControlBindings.All.Length+7)/8),
            new Vector2(112,340),.45f,cyan);
        for(int slot=0;slot<8;slot++)
        {
            int i=first+slot;
            if(i>=ControlBindings.All.Length)break;
            GameAction action=ControlBindings.All[i];
            int col=slot/4,row=slot%4;
            int x=101+col*875,y=409+row*135;
            bool chosen=i==_controlsSelection;
            Rectangle r=new(x,y,842,124);
            _ui.Rect(_batch,r,chosen?new Color(23,68,100,248):new Color(5,26,47,237));
            _ui.Rect(_batch,new Rectangle(x,y,chosen?9:4,124),chosen?gold:new Color(52,128,167));
            _ui.TextFit(_batch,action.ToString().ToUpperInvariant(),new Vector2(x+31,y+16),.57f,670,chosen?Color.White:muted);
            _ui.TextFit(_batch,_bindings[action].ToString().ToUpperInvariant(),new Vector2(x+33,y+72),.54f,610,chosen?gold:cyan);
        }
        _ui.TextFit(_batch,_bindingCapture?"PRESS NEW KEY - "+BackPrompt+" CANCEL":_bindingNotice,
            new Vector2(107,966),.43f,1680,_bindingCapture?gold:cyan);
        _ui.TextShadow(_batch,NavigatePrompt+" SELECT   "+ConfirmPrompt+" REBIND   DELETE RESET   "+PagePrompt+" NEXT PAGE",
            new Vector2(ViewWidth/2,1024),.40f,Color.White,true);
        return;
    }
    string[] labels={"MUSIC VOLUME","SOUND EFFECTS","DISPLAY MODE","ASPECT RATIO",
        "RESOLUTION","ANTIALIASING","VERTICAL SYNC","DEPTH OF FIELD","COMBAT EFFECTS",
        "ENEMY OUTLINES","WEAPON VFX","ENEMY SHOT GLOW","HIT SHAKE / BLUR","DIFFICULTY"};
    string[] values={(_audio?.MusicVolume??55)+"%",(_audio?.SfxVolume??75)+"%",
        _fullscreen?"FULLSCREEN":"WINDOWED",_aspectRatio==0?"16:9":"16:10",ResolutionLabel()+"  V",
        MsaaOptions[_msaaRequest]==0?"OFF":(_effectiveMsaa==0?"UNSUPPORTED":_effectiveMsaa+"x MSAA"),
        _vsyncEnabled?"ON":"OFF",_dofEnabled?"ON":"OFF",
        _combatFxEnabled?"ON":"OFF",_enemyReadabilityEnabled?"ON":"OFF",
        _primaryVfxEnabled?"ON":"OFF",_enemyShotGlow+"%",_hitEffectStrength+"%",
        DifficultyName(_selectedDifficulty)};
    for(int i=0;i<labels.Length;i++)
    {
        int col=i/7,row=i%7;
        int x=102+col*875,y=333+row*96;
        bool chosen=i==_settingsSelection;
        Rectangle r=new(x,y,842,88);
        _ui.Rect(_batch,r,chosen?new Color(24,70,105,248):new Color(5,27,47,240));
        _ui.Rect(_batch,new Rectangle(x,y,chosen?9:4,88),chosen?gold:new Color(52,128,167));
        _ui.TextFit(_batch,labels[i],new Vector2(x+27,y+4),.42f,780,chosen?Color.White:muted);
        _ui.TextFit(_batch,values[i],new Vector2(x+28,y+45),.48f,745,chosen?gold:cyan);
        if(i==0 || i==1 || i==11 || i==12)
        {
            int val=i==0?(_audio?.MusicVolume??55):i==1?(_audio?.SfxVolume??75):i==11?_enemyShotGlow:_hitEffectStrength;
            _ui.Bar(_batch,new Rectangle(x+460,y+58,335,14),val/100f,cyan);
        }
    }
    _ui.TextShadow(_batch,ConfirmPrompt+" SELECT   "+NavigatePrompt+" ADJUST   "+PagePrompt+" NEXT PAGE   "+BackPrompt+" CLOSE",
        new Vector2(ViewWidth/2,1017),.43f,cyan,true);
    if(_resolutionDropdownOpen) DrawResolutionDropdown();
}

    private void DrawOverlay()
    {
        switch(_mode)
        {
            case GameMode.Title: DrawTitle();break;
            case GameMode.Playing: DrawHud();break;
            case GameMode.Shop: DrawShop();break;
            case GameMode.Paused: DrawPauseMenu();break;
            case GameMode.GameOver: DrawEndScreen(false);break;
            case GameMode.Victory: DrawEndScreen(true);break;
        }
    }

    private void Shade()
        =>_ui.Rect(_batch,new Rectangle(0,0,ViewWidth,ViewHeight),new Color(0,4,14,205));

private void DrawTitle()
{
    DrawGuiBackground();
    Color cyan=new(112,228,255),gold=new(255,205,107);
    _batch.Draw(_logo,new Rectangle(96,66,1480,233),Color.White);
    _ui.TextShadow(_batch,"PILOT COMMAND / MISSION SELECT",new Vector2(125,298),.52f,cyan);
    _ui.TextFit(_batch,ControllerStatus,
        new Vector2(125,344),.30f,1640,_pad.IsConnected?Color.LawnGreen:gold);
    string[] labels={"NEW GAME","CONTINUE MISSION","SETTINGS","CONTROLS / HELP","ONLINE / LAN CO-OP","EXIT GAME"};
    for(int i=0;i<labels.Length;i++)
    {
        Rectangle r=new(110,375+i*103,827,94);
        DrawMenuButton(r,labels[i],i==_titleMenuSelection,
            i==1&&!(_checkpointUnlocked||_hasDiskSave),.88f);
        _ui.Text(_batch,"0"+(i+1),new Vector2(961,390+i*103),.43f,cyan);
    }
    DrawCleanPanel(new Rectangle(1020,381,803,608),new Color(144,131,251));
    _ui.TextShadow(_batch,"MK-I INTERCEPTOR",new Vector2(1053,420),.70f,Color.White);
    _batch.Draw(_hdPlayer,new Rectangle(1100,513,669,330),Color.White);
    _ui.TextFit(_batch,"BEST SCORE   "+_bestScore.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),new Vector2(1055,838),.60f,728,Color.White);
    _ui.TextFit(_batch,_hasDiskSave?"CAMPAIGN SAVE AVAILABLE":"NEW CAMPAIGN  /  NO SAVE FOUND",
        new Vector2(1055,916),.43f,720,_hasDiskSave?Color.LawnGreen:cyan);
    _ui.Text(_batch,"DEFAULT DIFFICULTY: "+DifficultyName(_selectedDifficulty),new Vector2(1055,959),.40f,gold);
    _ui.Rect(_batch,new Rectangle(0,1005,ViewWidth,75),new Color(3,12,28,250));
    _ui.TextShadow(_batch,NavigatePrompt+" SELECT   "+ConfirmPrompt+" CONFIRM   "+HelpPrompt+" HELP   "+SettingsPrompt+" SETTINGS",
        new Vector2(ViewWidth/2,1024),.43f,cyan,true);
}

private void DrawPauseMenu()
{
    Shade();
    Color cyan=new(105,223,252);
    _ui.Rect(_batch,new Rectangle(395,115,1130,842),new Color(3,17,35,246));
    _ui.Rect(_batch,new Rectangle(395,115,8,842),cyan);
    _ui.TextShadow(_batch,"FLIGHT SUSPENDED",new Vector2(ViewWidth/2,197),1.10f,Color.White,true);
    string[] labels={"RESUME FLIGHT","SETTINGS","CONTROLS / HELP","RETURN TO BASE"};
    for(int i=0;i<labels.Length;i++)
        DrawMenuButton(new Rectangle(486,365+i*129,950,115),labels[i],i==_pauseMenuSelection,false,.86f);
    _ui.TextShadow(_batch,ResumePrompt+" RESUME     "+ConfirmPrompt+" SELECT",
        new Vector2(ViewWidth/2,929),.47f,cyan,true);
}

    private void DrawEndScreen(bool complete)
    {
        Shade();
        DrawTechPanel(new Rectangle(365,160,1190,770),complete?Color.Cyan:new Color(249,100,101));
        _ui.TextShadow(_batch,complete?"SECTOR SECURED":"MISSION FAILED",
            new Vector2(ViewWidth/2,240),1.0f,complete?Color.Cyan:Color.OrangeRed,true);
        _ui.TextShadow(_batch,complete?(_infernoActive?"INFERNO BELT SURVIVED":"RIFT COLOSSUS ELIMINATED"):"RETURN TO BASE",
            new Vector2(ViewWidth/2,358),.42f,Color.White,true);
        _ui.TextShadow(_batch,"SCORE  "+_score.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),new Vector2(ViewWidth/2,486),.75f,Color.White,true);
        _ui.TextShadow(_batch,"BEST  "+_bestScore.ToString("N0", System.Globalization.CultureInfo.InvariantCulture),new Vector2(ViewWidth/2,594),.50f,Color.Cyan,true);
        string replay=CoopActive?(_coopHost!=null?ConfirmPrompt+": PARTY LOBBY":"WAITING FOR HOST"):ConfirmPrompt+": NEW GAME";
        DrawMenuButton(new Rectangle(535,709,850,94),replay,true,_coopClient!=null,.52f);
        string checkpoint=CheckpointPrompt+": CHECKPOINT";
        _ui.TextShadow(_batch,CoopActive?BackPrompt+": LOCAL MENU":_checkpointUnlocked&&!complete?checkpoint+"     "+BackPrompt+": MAIN MENU":BackPrompt+": MAIN MENU",
            new Vector2(ViewWidth/2,848),.35f,Color.Cyan,true);
    }

    private void DrawHud()
    {
        // Compact top-only cockpit: no side rails and no bottom obstruction.
        // The art contains no hard-coded values; every changing indicator below is live.
        const int hudHeight=132;
        _batch.Draw(_cockpitHud,new Rectangle(0,0,ViewWidth,hudHeight),Color.White);
        Color cyan=new(110,237,255);
        Color green=new(116,255,208);
        Color weaponTint=PrimaryWeaponColor(_pilot.PrimaryMode);
        string region=_infernoActive?
            (_infernoPhase==0?"OUTER BELT":_infernoPhase==1?"BROKEN FLEET":"INFERNO CORE"):
            _riftActive?(_riftElapsed<40f?"RIFT APPROACH":_riftElapsed<82f?"ALIEN RUINS":"FRACTURE CORE"):
            (_sectorZone switch {0=>"OUTER FRONTIER",1=>"DEBRIS FIELD",2=>"WARDEN",_=>"DEFENSE RING"});

        // Top instrument glass fields.
        _ui.Text(_batch,_infernoActive?"03":_riftActive?"02":"01",new Vector2(221,61),.35f,cyan);
        _ui.Text(_batch,region,new Vector2(276,69),.25f,green);
        _ui.Text(_batch,_score.ToString("0000000"),new Vector2(574,37),.46f,Color.White);
        _ui.Text(_batch,_pilot.Lives.ToString("00"),new Vector2(1230,37),.46f,Color.White);
        _ui.Bar(_batch,new Rectangle(1406,50,244,20),
            _pilot.Shield/(float)_pilot.MaxShield,Color.DeepSkyBlue);

        // Three short translucent status tabs nest immediately under the top frame.
        // They leave almost all of the scene (including the bottom and both sides) exposed.
        _ui.Rect(_batch,new Rectangle(82,116,272,30),new Color(2,12,28,196));
        _ui.Frame(_batch,new Rectangle(82,116,272,30),new Color(66,154,199,130),1);
        _ui.Text(_batch,"HP "+_pilot.Health+"/"+_pilot.MaxHealth,new Vector2(93,116),.35f,
            _pilot.Health<=1?Color.OrangeRed:cyan);
        _ui.Bar(_batch,new Rectangle(239,124,97,10),_pilot.Health/(float)_pilot.MaxHealth,
            _pilot.Health<=1?Color.OrangeRed:new Color(63,212,244));

        _ui.Rect(_batch,new Rectangle(731,116,340,30),new Color(2,12,28,196));
        _ui.Frame(_batch,new Rectangle(731,116,340,30),new Color(66,154,199,130),1);
        if(_infernoActive)
        {
            _ui.Text(_batch,"INFERNO "+_infernoWaveCount.ToString("00"),new Vector2(744,116),.35f,Color.Orange);
            _ui.Bar(_batch,new Rectangle(957,124,97,10),_infernoElapsed/InfernoDuration,Color.OrangeRed);
        }
        else if(_riftActive)
        {
            _ui.Text(_batch,"RIFT WAVE "+_riftWaveCount.ToString("00"),new Vector2(744,116),.35f,green);
            _ui.Bar(_batch,new Rectangle(957,124,97,10),_riftElapsed/RiftDuration,green);
        }
        else
        {
            _ui.Text(_batch,"WAVE "+_spawnCount.ToString("00"),new Vector2(744,116),.35f,cyan);
            _ui.Bar(_batch,new Rectangle(957,124,97,10),_elapsed/BossAtSeconds,cyan);
        }

        _ui.Rect(_batch,new Rectangle(1501,116,319,30),new Color(2,12,28,196));
        _ui.Frame(_batch,new Rectangle(1501,116,319,30),new Color(66,154,199,130),1);
        _ui.Text(_batch,PrimaryWeaponName(_pilot.PrimaryMode),new Vector2(1515,116),.35f,weaponTint);
        for(int i=0;i<Pilot.MaxWeaponLevel;i++)
        {
            Rectangle cell=new(1720+i*13,123,9,10);
            _ui.Rect(_batch,cell,i<_pilot.WeaponLevel?weaponTint:new Color(20,45,67));
            _ui.Frame(_batch,cell,new Color(80,147,192),1);
        }

        // Separate visible wallet; score is never spent on shop purchases.
        _ui.Rect(_batch,new Rectangle(1100,113,254,37),new Color(2,12,28,205));
        _ui.Frame(_batch,new Rectangle(1100,113,254,37),new Color(255,179,72,155),1);
        _batch.Draw(_pickupCredits,new Rectangle(1106,115,31,31),Color.White);
        _ui.Text(_batch,"CR "+_credits.ToString("00000"),new Vector2(1145,112),.39f,new Color(255,215,113));
        if(_trainingRun || CoopActive)
            _ui.Text(_batch,CoopActive?"CO-OP / SEPARATE FROM SOLO SCORES":"TEST RUN - SCORE NOT SAVED",new Vector2(ViewWidth/2,184),.38f,Color.Orange,true);
        foreach(Enemy e in _enemies)
        {
            if(e.Kind!=EnemyKind.Sentinel && e.Kind!=EnemyKind.Warden &&
               e.Kind!=EnemyKind.RiftGuardian && e.Kind!=EnemyKind.RiftColossus &&
               !IsInfernoBoss(e.Kind)) continue;
            Color tint=IsInfernoBoss(e.Kind)||e.Kind==EnemyKind.Sentinel?Color.OrangeRed:Color.MediumPurple;
            string bossName=e.Kind switch
            {
                EnemyKind.RiftGuardian=>"RIFT GUARDIAN",
                EnemyKind.RiftColossus=>"RIFT COLOSSUS",
                EnemyKind.InfernoGuardian=>"INFERNO GUARDIAN",
                EnemyKind.InfernoTitan=>"INFERNO TITAN",
                EnemyKind.Sentinel=>"SENTINEL CORE",
                _=>"WARDEN"
            };
            _ui.Text(_batch,bossName+"  PHASE "+e.LastPhase,
                new Vector2(ViewWidth/2,188),.34f,tint,true);
            _ui.Bar(_batch,new Rectangle(650,214,620,18),e.Health/(float)Math.Max(1,e.MaxHealth),tint);
            _ui.Text(_batch,e.Health+" / "+e.MaxHealth,new Vector2(1300,208),.33f,Color.White);
        }
        if(_messageTime>0f)
            _ui.TextShadow(_batch,_message,new Vector2(ViewWidth/2,248),.55f,cyan,true);
        if(_formationNoticeTime>0f && _mode==GameMode.Playing && !_bossLaunched)
        {
            float alpha=MathHelper.Clamp(_formationNoticeTime/.45f,0f,1f);
            float scale=MathF.Min(.70f,1150f/MathF.Max(48,_formationNotice.Length*48f));
            // High-contrast, borderless yellow tactical warning.
            _ui.Text(_batch,_formationNotice,new Vector2(ViewWidth/2+4,330),scale,new Color(0,5,16)*alpha,true);
            _ui.Text(_batch,_formationNotice,new Vector2(ViewWidth/2,325),scale,new Color(255,224,74)*alpha,true);
        }
    }

    protected override void UnloadContent()
    {
        StopCoop();
        StopGamePadFeedback();
        _dualSense.Dispose();
        _audio?.Dispose();_audio=null;
        _scene?.Dispose();_pixel?.Dispose();
        base.UnloadContent();
    }
}

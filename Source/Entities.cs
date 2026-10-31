using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace IronNebula;

internal enum GameMode { Title, Playing, Paused, GameOver, Victory, Shop }
internal enum DifficultyMode { Easy=0, Normal=1, Hard=2 }
internal enum EnemyKind { Scout, Interceptor, Heavy, Turret, Warden, Sentinel,
    RiftDart, RiftWisp, RiftMauler, RiftPrism, RiftStalker,
    RiftGuardian, RiftColossus, RiftBossTurret,
    InfernoScorch, InfernoLavaDrone, InfernoBomber, InfernoTank, InfernoWreckHunter, InfernoGunship,
    InfernoGuardian, InfernoTitan, InfernoBossTurret }
internal enum PickupKind { Weapon, Repair, Shield, Drone, Credits, RareCache }
// Only one primary firing system is active at a time. All use the same 1..7 row stack.
internal enum PrimaryWeaponMode { Pulse, Lance, Arc, Gatling, Beam }

internal sealed class Pilot
{
    public Vector2 Position = new(330, 540);
    public readonly float Radius = 31f; // zoom-adjusted ship collision profile
    public int Health = 4;
    public int MaxHealth = 4;
    public int MaxShield = 3;
    public int DroneUpgrade;
    public int WeaponPowerLevel; // 0..2, applies to player primary fire only
    public int EngineLevel; // 0..2, moderate ship speed increase
    public int PickupMagnetLevel;
    public int Lives = 3;
    public int WeaponLevel = 1;
    public const int MaxWeaponLevel = 7;
    public PrimaryWeaponMode PrimaryMode = PrimaryWeaponMode.Pulse;
    public float Invincible;
    public float ShotTimer;
    public Vector2 Velocity;
    public int Shield;
    public bool DroneActive;
    public Vector2 DronePosition;
    public float DroneShotTimer;
    public float DroneBlockTimer;
}

internal sealed class Enemy
{
    public EnemyKind Kind;
    public Vector2 Position;
    public float Age;
    public float BaseY;
    public float TargetY; // locked target for tactical dive/pincer; never tracks player continuously
    public float Speed;
    public float Radius;
    public float FireTimer;
    public int Health;
    public int MaxHealth;
    public float Phase;
    public int MotionPattern;
    public int LastPhase = 1;
    public int FireCycle;
    public float HitFlash;
    public float HitReaction; // purely visual: impact shake, never collision movement
    public float LastBossImpactAge=-100f; // per-entity cooldown for heavy-hit VFX
    public float SmokeTimer; // purely visual, per-enemy smoke cadence
    public float Banking; // smoothed visual movement angle (-1 up, +1 down)
    public bool DifficultyApplied; // health / fire rate tuned exactly once on first update
    [System.Text.Json.Serialization.JsonIgnore]
    public Enemy? Parent; // boss weapon module; destroyed independently
    public Vector2 MountOffset;
}

internal sealed class Projectile
{
    public Vector2 Position;
    public Vector2 PreviousPosition;
    public Vector2 Velocity;
    public bool Friendly;
    public int OwnerPlayerId;
    public bool IsDroneShot;
    public float Radius;
    public float Age;
    public float Duration = 4.0f;
    public int Damage = 1;
    public Color Tint;
    public PrimaryWeaponMode PrimaryMode = PrimaryWeaponMode.Pulse;
    // A Lance projectile may cross several distinct targets; never damage one twice.
    public int HitsRemaining = 1;
    [System.Text.Json.Serialization.JsonIgnore]
    public readonly HashSet<Enemy> HitEnemies = new();
    [System.Text.Json.Serialization.JsonIgnore]
    public readonly HashSet<Debris> HitRocks = new();
}

// Weapon-specific contact effect. Purely visual; does not modify collision or damage.
internal sealed class PrimaryImpact
{
    public Vector2 Position;
    public PrimaryWeaponMode Mode;
    public float Age;
    public float Life;
    public float Rotation;
    public float Size;
}

// Short-lived, aggregated player-primary damage indicator. Never modifies combat damage.
internal sealed class PrimaryDamageReadout
{
    public Enemy Target=null!;
    public PrimaryWeaponMode Mode;
    public Vector2 Position;
    public int Damage;
    public float Age;
    public float Life=.70f;
}

internal sealed class Particle
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Tint;
    public float Age;
    public float Life;
    public float Size;
}

// Short-lived foreground-only streaks; never used for background/parallax visuals.
internal sealed class CombatSpark
{
    public Vector2 Position;
    public Vector2 Velocity;
    public Color Tint;
    public float Age;
    public float Life;
    public float Length;
    public float Thickness;
}

internal sealed class CombatFlash
{
    public Vector2 Position;
    public Vector2 Direction;
    public Color Tint;
    public float Age;
    public float Life;
    public float Size;
}

internal sealed class ExplosionSprite
{
    public Vector2 Position;
    public float Age;
    public float FrameTime = .055f;
    public float Scale = 1f;
    public float Rotation;
}

// Scheduled visual detonations, independent of enemy physics and scoring.
internal sealed class ScheduledDeathBurst
{
    public Vector2 Position;
    public float Delay;
    public float Scale;
    public float Duration;
    public bool Finale;
}

internal sealed class DamageSmoke
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Age;
    public float Life;
    public float Scale;
    public int Variant;
    public int SeverityRow;
    public float Rotation;
    public float Spin;
}

internal sealed class Pickup
{
    public PickupKind Kind;
    public Vector2 Position;
    public float Age;
    public Vector2 Velocity;
    public int Credits;
}

internal sealed class Star
{
    public Vector2 Position;
    public float Speed;
    public float Radius;
    public float Alpha;
    public float Phase;
}

internal sealed class Shockwave
{
    public Vector2 Position;
    public Color Tint;
    public float Age;
    public float Life;
    public float MaxRadius;
}

// Destructible environmental debris, independent of enemy waves.
internal sealed class Debris
{
    public Vector2 Position;
    public Vector2 Velocity;
    public float Radius;
    public float Rotation;
    public float Spin;
    public int Health;
    public int Variant;
    public bool IsRiftShard;
}

// Final boss telegraphed horizontal beam: warning then damage window.
internal sealed class LaserLane
{
    public float Y;
    public float Age;
    public float Warning = 1.15f;
    public float Active = .75f;
}

// Purely decorative scenery that moves independently of gameplay asteroids.
internal sealed class ParallaxRock
{
    public float StartX;
    public float Y;
    public float Diameter;
    public float SpeedFactor;
    public float Rotation;
    public float Spin;
    public float Opacity;
    public int Variant;
    public float Depth;
    public int DepthLayer; // 0 = far, 1 = mid, 2 = near; decorative only
}

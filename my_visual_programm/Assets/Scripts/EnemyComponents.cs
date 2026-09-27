using Unity.Entities;
using Unity.Mathematics;

public enum EnemyMode : byte
{
    Go = 0,
    Attack = 1,
    Die = 2
}

public struct Health : IComponentData
{
    public float Current;
}

public struct Stamina : IComponentData
{
    public float Current;
    public float Max;
    public float RegenPerSecond;
    public float MoveDrainPerSecond;
    public float ResumeAt;
    public byte Moving;
}

public struct Enemy : IComponentData
{
    public float Speed;
    public float AnimPhase;
}

public struct EnemyState : IComponentData
{
    public EnemyMode Mode;
    public float DieLeft;
    public float AttackCooldown;
}

public struct PathCursor : IComponentData
{
    public int Index;
    public float RepathIn;
}

public struct PathPoint : IBufferElementData
{
    public float3 Position;
}

public struct PlayerBody : IComponentData
{
    public float3 Position;
    public float Radius;
    public float Health;
    public float MaxHealth;
    public float WeaponCooldown;
    public float WeaponRadius;
    public float WeaponDamage;
    public float WeaponPulse;
    public byte WavesOpen;
}

public struct BattleStats : IComponentData
{
    public int Wave;
    public int Killed;
    public int Pending;
    public float NextWaveTime;
    public uint Seed;
}

using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(EnemyPathSystem))]
public partial struct EnemyWaveSystem : ISystem
{
    public const int WaveSize = 2000;
    public const int MaxAlive = 10000;
    public const float WaveInterval = 4f;

    EntityArchetype archetype;
    EntityQuery enemies;
    Random random;

    public void OnCreate(ref SystemState state)
    {
        archetype = state.EntityManager.CreateArchetype(
            typeof(LocalTransform),
            typeof(Health),
            typeof(Stamina),
            typeof(Enemy),
            typeof(EnemyState),
            typeof(PathCursor),
            typeof(PathPoint));
        enemies = state.GetEntityQuery(ComponentType.ReadOnly<Enemy>());
        random = new Random(17);
        state.RequireForUpdate<BattleStats>();
        state.RequireForUpdate<PlayerBody>();
    }

    public void OnUpdate(ref SystemState state)
    {
        PlayerBody player = SystemAPI.GetSingleton<PlayerBody>();
        if (player.WavesOpen == 0 || player.Health <= 0f)
            return;

        var stats = SystemAPI.GetSingletonRW<BattleStats>();
        if (stats.ValueRO.Pending > 0)
        {
            int batch = stats.ValueRO.Wave <= 1
                ? stats.ValueRO.Pending
                : math.min(stats.ValueRO.Pending, random.NextInt(20, 46));
            Spawn(ref state, batch, player.Position);
            stats.ValueRW.Pending -= batch;
            if (stats.ValueRW.Pending == 0)
                stats.ValueRW.NextWaveTime = (float)SystemAPI.Time.ElapsedTime + WaveInterval;
            return;
        }

        if (SystemAPI.Time.ElapsedTime < stats.ValueRO.NextWaveTime)
            return;

        int alive = enemies.CalculateEntityCount();
        if (alive >= MaxAlive)
        {
            stats.ValueRW.NextWaveTime = (float)SystemAPI.Time.ElapsedTime + 0.5f;
            return;
        }

        stats.ValueRW.Wave++;
        stats.ValueRW.Pending = math.min(WaveSize, MaxAlive - alive);
    }

    void Spawn(ref SystemState state, int count, float3 playerPosition)
    {
        var spawned = state.EntityManager.CreateEntity(archetype, count, Allocator.Temp);
        for (int i = 0; i < spawned.Length; i++)
        {
            float angle = random.NextFloat(0f, math.PI * 2f);
            float radius = random.NextFloat(28f, 46f);
            float3 position = new float3(math.cos(angle) * radius, 0.4f, math.sin(angle) * radius);

            state.EntityManager.SetComponentData(spawned[i],
                LocalTransform.FromPositionRotationScale(position, quaternion.identity, 0.55f));
            state.EntityManager.SetComponentData(spawned[i], new Health { Current = 80f });
            state.EntityManager.SetComponentData(spawned[i], new Stamina
            {
                Current = 4f,
                Max = 4f,
                RegenPerSecond = 1.1f,
                MoveDrainPerSecond = 1.6f,
                ResumeAt = 1.4f,
                Moving = 1
            });
            state.EntityManager.SetComponentData(spawned[i], new Enemy
            {
                Speed = random.NextFloat(4.5f, 6.5f),
                AnimPhase = random.NextFloat(0f, 10f)
            });
            state.EntityManager.SetComponentData(spawned[i], new EnemyState
            {
                Mode = EnemyMode.Go
            });
            state.EntityManager.SetComponentData(spawned[i], new PathCursor
            {
                Index = 1,
                RepathIn = random.NextFloat(0f, 0.4f)
            });

            var path = state.EntityManager.GetBuffer<PathPoint>(spawned[i]);
            path.Add(new PathPoint { Position = position });
            path.Add(new PathPoint { Position = playerPosition });
        }

        spawned.Dispose();
    }
}

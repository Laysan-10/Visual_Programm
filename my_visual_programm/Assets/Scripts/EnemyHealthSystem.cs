using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(EnemyPathSystem))]
[BurstCompile]
public partial struct EnemyBrainSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBody>();
        state.RequireForUpdate<Enemy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        PlayerBody player = SystemAPI.GetSingleton<PlayerBody>();
        float reach = player.Radius + 1.2f;
        float reachSq = reach * reach;
        float3 playerPosition = player.Position;

        foreach (var (transform, health, brain) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<Health>, RefRW<EnemyState>>().WithAll<Enemy>())
        {
            if (brain.ValueRO.Mode == EnemyMode.Die)
            {
                brain.ValueRW.DieLeft -= dt;
                continue;
            }

            if (health.ValueRO.Current <= 0f)
            {
                brain.ValueRW.Mode = EnemyMode.Die;
                brain.ValueRW.DieLeft = 0.75f;
                continue;
            }

            float3 offset = playerPosition - transform.ValueRO.Position;
            offset.y = 0f;
            if (math.lengthsq(offset) > reachSq)
            {
                if (brain.ValueRO.Mode == EnemyMode.Attack)
                    brain.ValueRW.Mode = EnemyMode.Go;
                continue;
            }

            if (brain.ValueRO.Mode != EnemyMode.Attack)
                brain.ValueRW.Mode = EnemyMode.Attack;
        }
    }
}

[UpdateAfter(typeof(EnemyBrainSystem))]
[BurstCompile]
public partial struct EnemyDeathSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BattleStats>();
        state.RequireForUpdate<Enemy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged);
        int killedNow = 0;

        foreach (var (brain, entity) in
                 SystemAPI.Query<RefRO<EnemyState>>().WithAll<Enemy>().WithEntityAccess())
        {
            if (brain.ValueRO.Mode != EnemyMode.Die || brain.ValueRO.DieLeft > 0f)
                continue;

            ecb.DestroyEntity(entity);
            killedNow++;
        }

        if (killedNow == 0)
            return;

        var stats = SystemAPI.GetSingletonRW<BattleStats>();
        stats.ValueRW.Killed += killedNow;
    }
}

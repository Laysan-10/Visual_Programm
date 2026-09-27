using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateBefore(typeof(EnemyBrainSystem))]
[BurstCompile]
public partial struct PlayerWeaponSystem : ISystem
{
    Random random;

    public void OnCreate(ref SystemState state)
    {
        random = new Random(99);
        state.RequireForUpdate<PlayerBody>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var player = SystemAPI.GetSingletonRW<PlayerBody>();
        if (player.ValueRO.WavesOpen == 0 || player.ValueRO.Health <= 0f)
            return;

        float cooldown = player.ValueRO.WeaponCooldown - SystemAPI.Time.DeltaTime;
        float pulse = math.max(0f, player.ValueRO.WeaponPulse - SystemAPI.Time.DeltaTime);
        if (cooldown > 0f)
        {
            player.ValueRW.WeaponCooldown = cooldown;
            player.ValueRW.WeaponPulse = pulse;
            return;
        }

        player.ValueRW.WeaponCooldown = random.NextFloat(3f, 5f);
        player.ValueRW.WeaponPulse = 0.25f;
        float radiusSq = player.ValueRO.WeaponRadius * player.ValueRO.WeaponRadius;
        float damage = player.ValueRO.WeaponDamage;
        float3 origin = player.ValueRO.Position;

        foreach (var (transform, health, brain) in
                 SystemAPI.Query<RefRO<LocalTransform>, RefRW<Health>, RefRO<EnemyState>>().WithAll<Enemy>())
        {
            if (brain.ValueRO.Mode == EnemyMode.Die || health.ValueRO.Current <= 0f)
                continue;

            float3 offset = transform.ValueRO.Position - origin;
            offset.y = 0f;
            if (math.lengthsq(offset) <= radiusSq)
                health.ValueRW.Current -= damage;
        }
    }
}

[UpdateAfter(typeof(PlayerWeaponSystem))]
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
        var player = SystemAPI.GetSingletonRW<PlayerBody>();
        float playerHealth = player.ValueRO.Health;
        float reach = player.ValueRO.Radius + 0.7f;
        float reachSq = reach * reach;
        float3 playerPosition = player.ValueRO.Position;

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

            brain.ValueRW.Mode = EnemyMode.Attack;
            float cooldown = brain.ValueRO.AttackCooldown - dt;
            if (cooldown > 0f)
            {
                brain.ValueRW.AttackCooldown = cooldown;
                continue;
            }

            brain.ValueRW.AttackCooldown = 0.55f;
            health.ValueRW.Current -= 22f;
            playerHealth -= 8f;
        }

        player.ValueRW.Health = math.max(0f, playerHealth);
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

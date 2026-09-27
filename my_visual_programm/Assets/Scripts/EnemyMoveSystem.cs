using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(StaminaRegenSystem))]
[UpdateAfter(typeof(EnemyPathSystem))]
[BurstCompile]
public partial struct EnemyMoveSystem : ISystem
{
    const float WaypointReach = 0.8f;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerBody>();
        state.RequireForUpdate<Enemy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        float3 playerPosition = SystemAPI.GetSingleton<PlayerBody>().Position;

        foreach (var (transform, stamina, enemy, cursor, path, brain) in
                 SystemAPI.Query<RefRW<LocalTransform>, RefRW<Stamina>, RefRO<Enemy>, RefRW<PathCursor>, DynamicBuffer<PathPoint>, RefRO<EnemyState>>())
        {
            if (brain.ValueRO.Mode != EnemyMode.Go)
                continue;

            float3 toPlayer = playerPosition - transform.ValueRO.Position;
            toPlayer.y = 0f;
            float playerDistance = math.length(toPlayer);
            if (playerDistance <= 0.75f)
                continue;

            float3 target = playerPosition;
            if (path.Length > 0)
            {
                int index = math.clamp(cursor.ValueRO.Index, 0, path.Length - 1);
                float3 corner = path[index].Position;
                float3 cornerToPlayer = playerPosition - corner;
                cornerToPlayer.y = 0f;
                if (math.lengthsq(cornerToPlayer) + 0.25f < playerDistance * playerDistance)
                    target = corner;
                else if (index < path.Length - 1)
                    cursor.ValueRW.Index = index + 1;
            }

            float3 offset = target - transform.ValueRO.Position;
            offset.y = 0f;
            float distance = math.length(offset);
            if (distance < 0.05f)
                offset = toPlayer;

            var staminaValue = stamina.ValueRO;
            float speedScale = staminaValue.Current > 0.2f ? 1f : 0.55f;
            float3 direction = math.normalize(offset);
            float step = math.min(enemy.ValueRO.Speed * speedScale * dt, playerDistance - 0.75f);
            float3 position = transform.ValueRO.Position + direction * step;
            position.y = 0.4f;
            transform.ValueRW.Position = position;
            transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());

            if (speedScale > 0.9f)
                staminaValue.Current = math.max(0f, staminaValue.Current - staminaValue.MoveDrainPerSecond * dt);
            staminaValue.Moving = 1;
            stamina.ValueRW = staminaValue;
        }
    }
}

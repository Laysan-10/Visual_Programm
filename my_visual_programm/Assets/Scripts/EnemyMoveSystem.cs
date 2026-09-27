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

            int index = path.Length == 0 ? 0 : math.clamp(cursor.ValueRO.Index, 0, path.Length - 1);
            bool lastPoint = path.Length == 0 || index >= path.Length - 1;
            float3 target = lastPoint ? playerPosition : path[index].Position;
            float3 offset = target - transform.ValueRO.Position;
            offset.y = 0f;
            float distance = math.length(offset);

            if (!lastPoint && distance <= WaypointReach)
            {
                index += 1;
                cursor.ValueRW.Index = index;
                lastPoint = index >= path.Length - 1;
                target = lastPoint ? playerPosition : path[index].Position;
                offset = target - transform.ValueRO.Position;
                offset.y = 0f;
                distance = math.length(offset);
            }

            var staminaValue = stamina.ValueRO;
            if (distance <= 0.75f)
            {
                stamina.ValueRW = staminaValue;
                continue;
            }

            if (staminaValue.Moving == 1)
            {
                if (staminaValue.Current <= 0f)
                    staminaValue.Moving = 0;
            }
            else if (staminaValue.Current >= staminaValue.ResumeAt)
            {
                staminaValue.Moving = 1;
            }

            if (staminaValue.Moving == 1)
            {
                float3 direction = offset / distance;
                float step = math.min(enemy.ValueRO.Speed * dt, distance - 0.75f);
                float3 position = transform.ValueRO.Position + direction * step;
                position.y = 0.4f;
                transform.ValueRW.Position = position;
                transform.ValueRW.Rotation = quaternion.LookRotationSafe(direction, math.up());
                staminaValue.Current = math.max(0f, staminaValue.Current - staminaValue.MoveDrainPerSecond * dt);
                if (staminaValue.Current <= 0f)
                    staminaValue.Moving = 0;
            }

            stamina.ValueRW = staminaValue;
        }
    }
}

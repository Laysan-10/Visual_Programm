using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct StaminaRegenSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<Enemy>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float dt = SystemAPI.Time.DeltaTime;
        foreach (var stamina in SystemAPI.Query<RefRW<Stamina>>().WithAll<Enemy>())
        {
            stamina.ValueRW.Current = math.min(
                stamina.ValueRO.Max,
                stamina.ValueRO.Current + stamina.ValueRO.RegenPerSecond * dt);
        }
    }
}

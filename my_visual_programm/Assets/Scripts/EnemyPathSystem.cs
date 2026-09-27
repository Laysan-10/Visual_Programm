using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

[UpdateAfter(typeof(EnemyBrainSystem))]
[UpdateBefore(typeof(EnemyMoveSystem))]
public partial class EnemyPathSystem : SystemBase
{
    const int PathsPerFrame = 40;

    NavMeshPath navPath;
    List<Vector3> corners;

    protected override void OnCreate()
    {
        navPath = new NavMeshPath();
        corners = new List<Vector3>(24);
        RequireForUpdate<PlayerBody>();
        RequireForUpdate<Enemy>();
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;
        float3 playerPosition = SystemAPI.GetSingleton<PlayerBody>().Position;
        var due = new NativeList<Entity>(PathsPerFrame, Allocator.Temp);

        foreach (var (cursor, brain, entity) in
                 SystemAPI.Query<RefRW<PathCursor>, RefRO<EnemyState>>().WithAll<Enemy>().WithEntityAccess())
        {
            if (brain.ValueRO.Mode != EnemyMode.Go)
                continue;

            if (cursor.ValueRO.RepathIn > 0f)
            {
                cursor.ValueRW.RepathIn -= dt;
                continue;
            }

            if (due.Length < PathsPerFrame)
                due.Add(entity);
        }

        var manager = EntityManager;
        foreach (var entity in due)
        {
            var transform = manager.GetComponentData<LocalTransform>(entity);
            BuildPath(transform.Position, playerPosition, entity.Index);

            var buffer = manager.GetBuffer<PathPoint>(entity);
            buffer.Clear();
            for (int i = 0; i < corners.Count; i++)
            {
                Vector3 point = corners[i];
                buffer.Add(new PathPoint { Position = new float3(point.x, point.y, point.z) });
            }

            manager.SetComponentData(entity, new PathCursor { Index = 0, RepathIn = 2.5f });
        }

        due.Dispose();
    }

    void BuildPath(float3 from, float3 goal, int salt)
    {
        corners.Clear();
        var random = new Unity.Mathematics.Random((uint)math.max(1, salt + 1) * 7919u);
        Vector3 viaWish = new Vector3(random.NextFloat(-32f, 32f), 0f, random.NextFloat(-32f, 32f));
        Vector3 start = from;
        Vector3 end = goal;

        bool hasStart = NavMesh.SamplePosition(start, out NavMeshHit startHit, 6f, NavMesh.AllAreas);
        bool hasEnd = NavMesh.SamplePosition(end, out NavMeshHit endHit, 6f, NavMesh.AllAreas);
        bool hasVia = NavMesh.SamplePosition(viaWish, out NavMeshHit viaHit, 8f, NavMesh.AllAreas);
        if (hasStart)
            start = startHit.position;
        if (hasEnd)
            end = endHit.position;

        bool reachedVia = hasStart && hasVia && Append(start, viaHit.position);
        bool reachedEnd = false;
        if (reachedVia && hasEnd)
            reachedEnd = Append(viaHit.position, end);
        else if (hasStart && hasEnd)
            reachedEnd = Append(start, end);

        if (!reachedEnd)
        {
            if (corners.Count == 0)
                corners.Add(start);
            corners.Add(end);
        }
    }

    bool Append(Vector3 from, Vector3 to)
    {
        if (!NavMesh.CalculatePath(from, to, NavMesh.AllAreas, navPath))
            return false;
        if (navPath.status == NavMeshPathStatus.PathInvalid || navPath.corners.Length == 0)
            return false;

        int first = corners.Count > 0 ? 1 : 0;
        for (int i = first; i < navPath.corners.Length; i++)
            corners.Add(navPath.corners[i]);
        return true;
    }
}

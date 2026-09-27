using Unity.AI.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.AI;

public class EnemyView : MonoBehaviour
{
    Mesh mesh;
    Material goMaterial;
    Material waitMaterial;
    Material attackMaterial;
    Material dieMaterial;
    EntityQuery enemies;
    EntityQuery stats;
    EntityQuery player;
    Matrix4x4[] goBatch;
    Matrix4x4[] waitBatch;
    Matrix4x4[] attackBatch;
    Matrix4x4[] dieBatch;
    GUIStyle labelStyle;
    bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var demo = new GameObject("Enemy Wave Demo");
        demo.AddComponent<EnemyView>();
    }

    void Start()
    {
        var template = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mesh = template.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(template);

        goMaterial = CreateMaterial(new Color(0.86f, 0.18f, 0.14f));
        waitMaterial = CreateMaterial(new Color(0.22f, 0.26f, 0.34f));
        attackMaterial = CreateMaterial(new Color(1f, 0.55f, 0.1f));
        dieMaterial = CreateMaterial(new Color(0.45f, 0.45f, 0.48f));
        goBatch = new Matrix4x4[1023];
        waitBatch = new Matrix4x4[1023];
        attackBatch = new Matrix4x4[1023];
        dieBatch = new Matrix4x4[1023];

        if (GameObject.Find("Ground") == null)
        {
            CreateGround();
            CreateObstacle(new Vector3(0f, 1f, 0f), new Vector3(8f, 2f, 8f));
            CreateObstacle(new Vector3(16f, 1f, 12f), new Vector3(5f, 2f, 14f));
            CreateObstacle(new Vector3(-18f, 1f, -8f), new Vector3(4f, 2f, 16f));
            CreateObstacle(new Vector3(8f, 1f, -18f), new Vector3(14f, 2f, 4f));
        }

        var surface = FindAnyObjectByType<NavMeshSurface>();
        if (surface == null)
        {
            var surfaceObject = new GameObject("NavMesh");
            surface = surfaceObject.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        }

        var trigger = GameObject.Find("Wave Trigger");
        if (trigger != null)
            trigger.SetActive(false);
        surface.BuildNavMesh();
        if (trigger != null)
            trigger.SetActive(true);
        else
            CreateTriggerPad();

        if (GameObject.Find("Player") == null)
            CreatePlayer();

        Camera camera = Camera.main;
        if (camera != null)
            camera.farClipPlane = 250f;

        var world = World.DefaultGameObjectInjectionWorld;
        var manager = world.EntityManager;
        manager.AddComponentData(manager.CreateEntity(), new BattleStats { Seed = 1 });
        enemies = manager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadOnly<Stamina>(),
            ComponentType.ReadOnly<EnemyState>(),
            ComponentType.ReadOnly<Enemy>());
        stats = manager.CreateEntityQuery(ComponentType.ReadOnly<BattleStats>());
        player = manager.CreateEntityQuery(ComponentType.ReadOnly<PlayerBody>());
        ready = true;
        Debug.Log("Сцена волн запущена.");
    }

    void OnDestroy()
    {
        World world = World.DefaultGameObjectInjectionWorld;
        if (!ready || world == null || !world.IsCreated)
            return;

        enemies.Dispose();
        stats.Dispose();
        player.Dispose();
        ready = false;
    }

    void LateUpdate()
    {
        if (!ready || mesh == null)
            return;

        using NativeArray<LocalTransform> transforms = enemies.ToComponentDataArray<LocalTransform>(Allocator.Temp);
        using NativeArray<Stamina> staminas = enemies.ToComponentDataArray<Stamina>(Allocator.Temp);
        using NativeArray<EnemyState> brains = enemies.ToComponentDataArray<EnemyState>(Allocator.Temp);
        using NativeArray<Enemy> enemyData = enemies.ToComponentDataArray<Enemy>(Allocator.Temp);

        int going = 0;
        int waiting = 0;
        int attacking = 0;
        int dying = 0;
        float time = Time.time;

        for (int i = 0; i < transforms.Length; i++)
        {
            LocalTransform transform = transforms[i];
            float bob = 0f;
            float scale = transform.Scale;
            float pitch = 0f;
            bool attack = false;
            bool die = false;

            if (brains[i].Mode == EnemyMode.Die)
            {
                float t = 1f - Mathf.Clamp01(brains[i].DieLeft / 0.75f);
                bob = -0.55f * t;
                scale *= Mathf.Max(0.05f, 1f - t);
                pitch = 80f * t;
                die = true;
            }
            else if (brains[i].Mode == EnemyMode.Attack)
            {
                bob = Mathf.Abs(Mathf.Sin(time * 16f + enemyData[i].AnimPhase)) * 0.28f;
                scale *= 1.2f;
                attack = true;
            }
            else if (staminas[i].Moving == 1)
            {
                bob = Mathf.Sin(time * 10f + enemyData[i].AnimPhase) * 0.16f;
                pitch = Mathf.Sin(time * 10f + enemyData[i].AnimPhase) * 8f;
            }

            float4 rotation = transform.Rotation.value;
            Quaternion look = new Quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            Quaternion lean = Quaternion.Euler(pitch, 0f, 0f) * look;
            Matrix4x4 matrix = Matrix4x4.TRS(
                new Vector3(transform.Position.x, transform.Position.y + bob, transform.Position.z),
                lean,
                Vector3.one * scale);

            if (die)
                Push(dieBatch, dieMaterial, ref dying, matrix);
            else if (attack)
                Push(attackBatch, attackMaterial, ref attacking, matrix);
            else if (staminas[i].Moving == 1)
                Push(goBatch, goMaterial, ref going, matrix);
            else
                Push(waitBatch, waitMaterial, ref waiting, matrix);
        }

        Flush(goBatch, goMaterial, going);
        Flush(waitBatch, waitMaterial, waiting);
        Flush(attackBatch, attackMaterial, attacking);
        Flush(dieBatch, dieMaterial, dying);
    }

    void OnGUI()
    {
        if (!ready || stats.IsEmpty)
            return;

        BattleStats battle = stats.GetSingleton<BattleStats>();
        string weapon = "—";
        string health = "—";
        string hint = "W A S D — иди в синюю зону, тогда пойдут волны.";
        if (!player.IsEmpty)
        {
            PlayerBody body = player.GetSingleton<PlayerBody>();
            health = $"{Mathf.CeilToInt(body.Health)}";
            weapon = body.WavesOpen == 0 ? "ждёт зону" : $"{Mathf.Max(0f, body.WeaponCooldown):0.0} с";
            if (body.Health <= 0f)
                hint = "Игрок погиб.";
            else if (body.WavesOpen == 1)
                hint = "Красные идут по пути, тёмные копят стамину, оранжевые атакуют, серые умирают.";
        }

        if (labelStyle == null)
        {
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                normal = { textColor = Color.white }
            };
        }

        string text =
            $"Волна: {battle.Wave}\n" +
            $"Живых: {enemies.CalculateEntityCount()} / {EnemyWaveSystem.MaxAlive}\n" +
            $"Уничтожено: {battle.Killed}\n" +
            $"Здоровье игрока: {health}\n" +
            $"Перезарядка: {weapon}\n" +
            hint;
        GUI.Label(new Rect(16f, 16f, 820f, 170f), text, labelStyle);
    }

    void Push(Matrix4x4[] batch, Material material, ref int count, Matrix4x4 matrix)
    {
        batch[count++] = matrix;
        if (count < batch.Length)
            return;

        Graphics.DrawMeshInstanced(mesh, 0, material, batch, count);
        count = 0;
    }

    void Flush(Matrix4x4[] batch, Material material, int count)
    {
        if (count > 0)
            Graphics.DrawMeshInstanced(mesh, 0, material, batch, count);
    }

    void CreateGround()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.localScale = new Vector3(12f, 1f, 12f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.16f, 0.22f, 0.16f));
    }

    void CreateObstacle(Vector3 position, Vector3 scale)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = "Obstacle";
        box.transform.position = position;
        box.transform.localScale = scale;
        box.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.28f, 0.24f, 0.2f));
    }

    void CreateTriggerPad()
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = "Wave Trigger";
        pad.transform.position = new Vector3(0f, 0.25f, -36f);
        pad.transform.localScale = new Vector3(14f, 0.5f, 8f);
        pad.GetComponent<BoxCollider>().isTrigger = true;
        var body = pad.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        pad.AddComponent<WaveTrigger>();
        pad.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.2f, 0.45f, 0.95f));
    }

    void CreatePlayer()
    {
        var playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObject.name = "Player";
        playerObject.transform.position = new Vector3(0f, 1f, -50f);
        Destroy(playerObject.GetComponent<CapsuleCollider>());
        var controller = playerObject.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = Vector3.zero;
        playerObject.AddComponent<PlayerController>();
        playerObject.GetComponent<MeshRenderer>().sharedMaterial = CreateMaterial(new Color(0.35f, 0.75f, 0.95f));
    }

    static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);
        material.enableInstancing = true;
        material.SetColor("_BaseColor", color);
        return material;
    }
}

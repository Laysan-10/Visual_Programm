using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public float speed = 8f;

    Entity entity;
    EntityManager manager;
    public static bool WavesStarted;

    CharacterController controller;
    Transform flash;
    bool entityReady;

    void Awake()
    {
        WavesStarted = false;
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        manager = world.EntityManager;
        using (var existing = manager.CreateEntityQuery(typeof(PlayerBody)))
        {
            using var found = existing.ToEntityArray(Allocator.Temp);
            if (found.Length > 0)
            {
                entity = found[0];
                for (int i = 1; i < found.Length; i++)
                    manager.DestroyEntity(found[i]);
            }
            else
            {
                entity = manager.CreateEntity();
                manager.AddComponentData(entity, new PlayerBody
                {
                    Position = transform.position,
                    Radius = 0.4f,
                    Health = 100f,
                    MaxHealth = 100f,
                    WeaponCooldown = 2f,
                    WeaponRadius = 7f,
                    WeaponDamage = 80f
                });
            }
        }
        entityReady = true;

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(sphere.GetComponent<Collider>());
        sphere.name = "Weapon Pulse";
        var renderer = sphere.GetComponent<MeshRenderer>();
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var material = new Material(shader);
        material.SetColor("_BaseColor", new Color(1f, 0.85f, 0.2f, 0.35f));
        renderer.sharedMaterial = material;
        flash = sphere.transform;
        flash.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!entityReady)
            return;

        PlayerBody body = manager.GetComponentData<PlayerBody>(entity);
        if (body.Health > 0f)
        {
            Vector2 input = ReadMove();
            Camera camera = Camera.main;
            Vector3 forward = camera != null ? camera.transform.forward : Vector3.forward;
            Vector3 right = camera != null ? camera.transform.right : Vector3.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();
            Vector3 move = forward * input.y + right * input.x;
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            if (move.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(move);

            controller.Move(move * speed * Time.deltaTime + Vector3.down * 6f * Time.deltaTime);
        }

        body = manager.GetComponentData<PlayerBody>(entity);
        body.Position = transform.position;
        ApplyDamage(ref body);
        manager.SetComponentData(entity, body);
    }

    void ApplyDamage(ref PlayerBody body)
    {
        if (body.WavesOpen == 0 || body.Health <= 0f)
            return;

        float dt = Time.deltaTime;
        body.WeaponPulse = math.max(0f, body.WeaponPulse - dt);
        body.WeaponCooldown -= dt;
        bool shot = body.WeaponCooldown <= 0f;
        if (shot)
        {
            body.WeaponCooldown = UnityEngine.Random.Range(3f, 5f);
            body.WeaponPulse = 0.25f;
        }

        float3 origin = transform.position;
        float weaponRadiusSq = body.WeaponRadius * body.WeaponRadius;
        float reach = body.Radius + 1.2f;
        float reachSq = reach * reach;
        using var query = manager.CreateEntityQuery(
            ComponentType.ReadOnly<LocalTransform>(),
            ComponentType.ReadWrite<Health>(),
            ComponentType.ReadWrite<EnemyState>(),
            ComponentType.ReadOnly<Enemy>());
        using var entities = query.ToEntityArray(Allocator.Temp);
        for (int i = 0; i < entities.Length; i++)
        {
            var enemyTransform = manager.GetComponentData<LocalTransform>(entities[i]);
            var health = manager.GetComponentData<Health>(entities[i]);
            var state = manager.GetComponentData<EnemyState>(entities[i]);
            if (state.Mode == EnemyMode.Die || health.Current <= 0f)
                continue;

            float3 offset = enemyTransform.Position - origin;
            offset.y = 0f;
            float distanceSq = math.lengthsq(offset);
            if (shot && distanceSq <= weaponRadiusSq)
                health.Current -= body.WeaponDamage;

            if (distanceSq <= reachSq)
            {
                state.Mode = EnemyMode.Attack;
                state.AttackCooldown -= dt;
                if (state.AttackCooldown <= 0f)
                {
                    state.AttackCooldown = 0.55f;
                    body.Health -= 8f;
                }
            }
            else if (state.Mode == EnemyMode.Attack)
            {
                state.Mode = EnemyMode.Go;
            }

            manager.SetComponentData(entities[i], health);
            manager.SetComponentData(entities[i], state);
        }

        body.Health = math.max(0f, body.Health);
    }

    void LateUpdate()
    {
        if (!entityReady)
            return;

        PlayerBody body = manager.GetComponentData<PlayerBody>(entity);
        Camera camera = Camera.main;
        if (camera != null)
        {
            Vector3 look = transform.position + Vector3.up * 1.2f;
            camera.transform.position = transform.position + new Vector3(0f, 16f, -14f);
            camera.transform.rotation = Quaternion.LookRotation(look - camera.transform.position, Vector3.up);
        }

        bool showPulse = body.WeaponPulse > 0f;
        flash.gameObject.SetActive(showPulse);
        if (!showPulse)
            return;

        flash.position = transform.position + Vector3.up * 0.5f;
        flash.localScale = Vector3.one * (body.WeaponRadius * 2f);
    }

    public void OpenWaves()
    {
        if (!entityReady)
            return;

        PlayerBody body = manager.GetComponentData<PlayerBody>(entity);
        body.WavesOpen = 1;
        manager.SetComponentData(entity, body);
        WavesStarted = true;
    }

    static Vector2 ReadMove()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return Vector2.zero;

        float x = 0f;
        float y = 0f;
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            x -= 1f;
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            x += 1f;
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            y -= 1f;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            y += 1f;
        return new Vector2(x, y);
    }
}

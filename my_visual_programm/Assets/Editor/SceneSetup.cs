using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneSetup
{
    static SceneSetup()
    {
        EditorApplication.delayCall += BuildIfNeeded;
    }

    public static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        if (GameObject.Find("Ground") != null)
        {
            if (GameObject.Find("Wave Trigger") != null)
                return;

            CreateTriggerPad();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }

        var ground = ColoredPrimitive(PrimitiveType.Plane, "Ground", Vector3.zero, new Vector3(12f, 1f, 12f),
            new Color(0.16f, 0.22f, 0.16f));
        ground.transform.SetParent(null);

        ColoredPrimitive(PrimitiveType.Cube, "Obstacle", new Vector3(0f, 1f, 0f), new Vector3(8f, 2f, 8f),
            new Color(0.28f, 0.24f, 0.2f));
        ColoredPrimitive(PrimitiveType.Cube, "Obstacle", new Vector3(16f, 1f, 12f), new Vector3(5f, 2f, 14f),
            new Color(0.28f, 0.24f, 0.2f));
        ColoredPrimitive(PrimitiveType.Cube, "Obstacle", new Vector3(-18f, 1f, -8f), new Vector3(4f, 2f, 16f),
            new Color(0.28f, 0.24f, 0.2f));
        ColoredPrimitive(PrimitiveType.Cube, "Obstacle", new Vector3(8f, 1f, -18f), new Vector3(14f, 2f, 4f),
            new Color(0.28f, 0.24f, 0.2f));

        var surfaceObject = new GameObject("NavMesh");
        var surface = surfaceObject.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.All;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;

        var player = ColoredPrimitive(PrimitiveType.Capsule, "Player", new Vector3(0f, 1f, -50f), Vector3.one,
            new Color(0.35f, 0.75f, 0.95f));
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        var controller = player.AddComponent<CharacterController>();
        controller.height = 2f;
        controller.radius = 0.4f;
        controller.center = Vector3.zero;
        player.AddComponent<PlayerController>();

        var crowd = new GameObject("Crowd");
        crowd.AddComponent<CrowdPreview>();
        CreateTriggerPad();

        Camera camera = Camera.main;
        if (camera != null)
        {
            camera.transform.position = new Vector3(0f, 52f, -38f);
            camera.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 8f) - camera.transform.position, Vector3.up);
            camera.farClipPlane = 250f;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void CreateTriggerPad()
    {
        var pad = ColoredPrimitive(PrimitiveType.Cube, "WaveTrigger", new Vector3(0f, 0.25f, -36f),
            new Vector3(14f, 0.5f, 8f), new Color(0.2f, 0.45f, 0.95f));
        pad.name = "Wave Trigger";
        pad.GetComponent<BoxCollider>().isTrigger = true;
        var body = pad.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        pad.AddComponent<WaveTrigger>();
    }

    static GameObject ColoredPrimitive(PrimitiveType type, string objectName, Vector3 position, Vector3 scale, Color color)
    {
        var primitive = GameObject.CreatePrimitive(type);
        primitive.name = objectName;
        primitive.transform.position = position;
        primitive.transform.localScale = scale;
        primitive.GetComponent<MeshRenderer>().sharedMaterial = LoadOrCreateMaterial(objectName, color);
        return primitive;
    }

    static Material LoadOrCreateMaterial(string objectName, Color color)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = $"Assets/Materials/{objectName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        material = new Material(shader);
        material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }
}

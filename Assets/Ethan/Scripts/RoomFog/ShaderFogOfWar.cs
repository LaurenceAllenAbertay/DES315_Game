using System.Collections.Generic;
using UnityEngine;

//Shader based fog of war - darknes unexplored rooms -EM//
//Particle fog for atmosphere - EM//
public class ShaderFogOfWar : MonoBehaviour
{
    [Header("Darkness Settings")]
    [Tooltip("Colour to tint unexplored rooms (usually very dark)")]
    public Color darknessColor = new Color(0.05f, 0.05f, 0.1f, 1f);

    [Tooltip("How much to darken (0 = no change, 1 = full darkness)")]
    [Range(0f, 1f)]
    public float darknessStrength = 0.95f;

    [Header("Particle Fog (Optional)")]
    [Tooltip("Add atmospheric fog particles to unexplored rooms")]
    public bool useParticleFog = true;

    [Tooltip("Prefab for fog particles (leave empty for auto-generated)")]
    public GameObject fogParticlePrefab;

    [Tooltip("Density of fog particles")]
    [Range(10, 100)]
    public int particleDensity = 30;

    [Header("Room Detection")]
    [Tooltip("Layer for the player")]
    public LayerMask playerLayer = 1 << 3;

    [Header("Setup")]
    [Tooltip("Parent transform containing all room objects")]
    public Transform roomsParent;

    [Tooltip("Auto setup when play starts (disable if rooms built at runtime)")]
    public bool autoSetupOnStart = true;

    [Header("Debug")]
    public bool showDebugLogs = false;

    //Track explored rooms and their original materials//
    private HashSet<GameObject> exploredRooms = new HashSet<GameObject>();
    private Dictionary<GameObject, List<OriginalMaterialData>> roomMaterials = new Dictionary<GameObject, List<OriginalMaterialData>>();
    private Dictionary<GameObject, GameObject> roomFogParticles = new Dictionary<GameObject, GameObject>();

    private struct OriginalMaterialData
    {
        public Renderer renderer;
        public Material[] originalMaterials;
        public Material[] darkenedMaterials;
    }

    private void Start()
    {
        if(autoSetupOnStart)
        {
            Invoke(nameof(SetupFogOfWar), 0.1f);
        }
    }

    [ContextMenu("Setup Fog Of War")]
    public void SetupFogOfWar()
    {
        //If called during play mode, delay to let DynamicFloorProps spawn//
        if(Application.isPlaying)
        {
            StartCoroutine(DelayedSetup());
            return;
        }

        PerformSetup();
    }

    //Wait 2 frames for pillars to spawn -EM//
    private System.Collections.IEnumerator DelayedSetup()
    {
        //Wait 2 frames for DynamicFloorProps to spawn//
        yield return null;
        yield return null;

        PerformSetup();
    }

    //The actual setup logic//
    private void PerformSetup()
    {
        if (roomsParent == null)
        {
            GameObject roomsObject = GameObject.Find("Rooms");
            if (roomsObject != null)
            {
                roomsParent = roomsObject.transform;
            }
            else
            {
                Debug.LogError("[ShaderFogOfWar] No rooms parent found!");
                return;
            }
        }

        int roomsSetup = 0;

        //Setup each room//
        foreach (Transform roomTransform in roomsParent)
        {
            if (roomTransform.name.StartsWith("Room_"))
            {
                bool isLobby = roomTransform.name.Contains("Type0") || roomTransform.name.ToLower().Contains("lobby");

                SetupRoom(roomTransform.gameObject, isLobby);
                roomsSetup++;

                if (isLobby)
                {
                    exploredRooms.Add(roomTransform.gameObject);
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log($"[ShaderFogOfWar] Setup complete! {roomsSetup} rooms configured");
        }
    }

    private void SetupRoom(GameObject room, bool isLobby)
    {
        //Setup materials for darkness effect//
        SetupRoomMaterials(room, isLobby);

        //Setup particle fog if enabled//
        if(useParticleFog && !isLobby)
        {
            SetupRoomParticleFog(room);
        }

        //Setup trigger for player direction//
        SetupRoomTrigger(room);
    }

    private void SetupRoomMaterials(GameObject room, bool isLobby)
    {
        List<OriginalMaterialData> materialDataList = new List<OriginalMaterialData>();

        //Get all renderers in the room//
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>(true);

        foreach (Renderer renderer in renderers)
        {
            //Skip vision cones and other special objects//
            if (renderer.GetComponent<EnemyVisionCone>() != null) continue;

            //Skip particle renderers//
            if (renderer is ParticleSystemRenderer) continue;

            OriginalMaterialData data = new OriginalMaterialData();
            data.renderer = renderer;
            data.originalMaterials = renderer.sharedMaterials;

            //Create darkened versions//
            if (!isLobby)
            {
                data.darkenedMaterials = new Material[data.originalMaterials.Length];
                for (int i = 0; i < data.originalMaterials.Length; i++)
                {
                    data.darkenedMaterials[i] = CreateDarkenedMaterial(data.originalMaterials[i]);
                }

                //Apply darkened materials immediatley//
                renderer.materials = data.darkenedMaterials;
            }
            materialDataList.Add(data);
        }

        roomMaterials[room] = materialDataList;

        if (showDebugLogs)
        {
            Debug.Log($"[ShaderFogOfWar] setup materials for {room.name} ({materialDataList.Count} renderers)");
        }
    }

    private Material CreateDarkenedMaterial(Material original)
    {
        if(original == null)
        {
            return null;
        }

        //Create a copy of the material//
        Material darkened = new Material(original);
        darkened.name = original.name + "_Darkened";

        //Darken the colour//
        if (darkened.HasProperty("_BaseColor"))
        {
            Color originalColor = darkened.GetColor("_BaseColor");
            darkened.SetColor("_BaseColor", Color.Lerp(originalColor, darknessColor, darknessStrength));
        }
        else if(darkened.HasProperty("_Color"))
        {
            Color originalColor = darkened.GetColor("_Color");
            darkened.SetColor("_Color", Color.Lerp(originalColor, darknessColor, darknessStrength));
        }

        return darkened;
    }

    private void SetupRoomParticleFog(GameObject room)
    {
        GameObject fogObject;

        if(fogParticlePrefab != null)
        {
            //Use provided prefab//
            fogObject = Instantiate(fogParticlePrefab, room.transform);
        }
        else
        {
            //Create simple fog particle system//
            fogObject = CreateDefaultFogParticles(room);
        }

        fogObject.name = "FogParticles";
        roomFogParticles[room] = fogObject;

        if(showDebugLogs)
        {
            Debug.Log($"[ShaderFogOfWar] Added particle fog to {room.name}");
        }
    }

    private GameObject CreateDefaultFogParticles(GameObject room)
    {
        GameObject fogObject = new GameObject("FogParticles");
        fogObject.transform.SetParent(room.transform);

        //Calculate room bounds//
        Bounds roomBounds = CalculateRoomBounds(room);
        fogObject.transform.position = roomBounds.center;

        //Create particle system//
        ParticleSystem ps = fogObject.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 5f;
        main.startSpeed = 0.1f;
        main.startSize = 2f;
        main.startColor = new Color(0.5f, 0.5f, 0.6f, 0.3f); //Light blue-gray, semi-transparent//
        main.maxParticles = particleDensity;
        main.loop = true;

        var emission = ps.emission;
        emission.rateOverTime = particleDensity / 5f; //Spawn rate//

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = roomBounds.size;

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.material = CreateFogParticleMaterial();

        return fogObject;
    }

    private Material CreateFogParticleMaterial()
    {
        //Try to find particle shader//
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");

        Material mat = new Material(shader);
        mat.color = new Color(1f, 1f, 1f, 0.3f);

        //Enable transparency//
        if (mat.HasProperty("_Surface"))
        {
            mat.SetFloat("_Surface", 1);
        }

        return mat;
    }

    private void SetupRoomTrigger(GameObject room)
    {
        //Check if already has a trigger//
        Collider[] colliders = room.GetComponents<Collider>();
        bool hasTrigger = false;

        foreach (Collider col in colliders)
        {
            if (col.isTrigger)
            {
                hasTrigger = true;
                break;
            }
        }

        if (!hasTrigger)
        {
            BoxCollider trigger = room.AddComponent<BoxCollider>();
            trigger.isTrigger = true;

            Bounds bounds = CalculateRoomBounds(room);
            trigger.center = room.transform.InverseTransformPoint(bounds.center);
            trigger.size = bounds.size;
        }

        //Add detector component//
        RoomShaderDetector detector = room.GetComponent<RoomShaderDetector>();
        if (detector == null)
        {
            detector = room.AddComponent<RoomShaderDetector>();
        }

        detector.fogManager = this;
        detector.playerLayerMask = playerLayer;
    }

    private Bounds CalculateRoomBounds(GameObject room)
    {
        Renderer[] renderers = room.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return new Bounds(room.transform.position, Vector3.one * 10f);
        }

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return bounds;
    }

    public void OnRoomEntered(GameObject room)
    {
        if (exploredRooms.Contains(room))
        {
            return;
        }

        exploredRooms.Add(room);
        RevealRoom(room);

        if (showDebugLogs)
        {
            Debug.Log($"[ShaderFogOfWar] Player entered {room.name} - revealing!");
        }
    }

    private void RevealRoom(GameObject room)
    {
        //Restore original materials//
        if (roomMaterials.TryGetValue(room, out List<OriginalMaterialData> materialDataList))
        {
            foreach (var data in materialDataList)
            {
                if (data.renderer != null)
                {
                    data.renderer.materials = data.originalMaterials;
                }
            }
        }

        //Remove particle fog//
        if (roomFogParticles.TryGetValue(room, out GameObject fogObject))
        {
            if (fogObject != null)
            {
                Destroy(fogObject);
            }
            roomFogParticles.Remove(room);
        }
    }

    [ContextMenu("Reveal All Rooms")]
    public void RevealAllRooms()
    {
        foreach (Transform roomTransform in roomsParent)
        {
            if (roomTransform.name.StartsWith("Room_"))
            {
                OnRoomEntered(roomTransform.gameObject);
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("[ShaderFogOfWar] Revealed all rooms");
        }
    }

    [ContextMenu("Reset All Fog")]
    public void ResetAllFog()
    {
        exploredRooms.Clear();

        //Re-setup everything//
        SetupFogOfWar();

        if (showDebugLogs)
        {
            Debug.Log("[ShaderFogOfWar] Reset all fog");
        }
    }

    [ContextMenu("Toggle Fog Off (Test)")]
    public void ToggleFogOff()
    {
        // Temporarily reveal all rooms without marking them as explored
        foreach (var kvp in roomMaterials)
        {
            GameObject room = kvp.Key;
            List<OriginalMaterialData> materialDataList = kvp.Value;

            foreach (var data in materialDataList)
            {
                if (data.renderer != null)
                {
                    data.renderer.materials = data.originalMaterials;
                }
            }

            // Also hide fog particles if present
            if (roomFogParticles.TryGetValue(room, out GameObject fogObject))
            {
                if (fogObject != null)
                {
                    fogObject.SetActive(false);
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("[ShaderFogOfWar] Fog toggled OFF for testing");
        }
    }

    [ContextMenu("Toggle Fog On (Test)")]
    public void ToggleFogOn()
    {
        // Re-apply darkness to unexplored rooms
        foreach (var kvp in roomMaterials)
        {
            GameObject room = kvp.Key;

            // Skip if already explored
            if (exploredRooms.Contains(room))
                continue;

            List<OriginalMaterialData> materialDataList = kvp.Value;

            foreach (var data in materialDataList)
            {
                if (data.renderer != null && data.darkenedMaterials != null)
                {
                    data.renderer.materials = data.darkenedMaterials;
                }
            }

            // Re-show fog particles if present
            if (roomFogParticles.TryGetValue(room, out GameObject fogObject))
            {
                if (fogObject != null)
                {
                    fogObject.SetActive(true);
                }
            }
        }

        if (showDebugLogs)
        {
            Debug.Log("[ShaderFogOfWar] Fog toggled ON");
        }
    }
}

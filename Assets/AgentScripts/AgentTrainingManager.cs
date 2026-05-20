using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Spawns multiple drone instances for parallel ML-Agents training on a SINGLE shared track.
/// All drones run on the same waypoints simultaneously, each tracking their own progress.
/// Uses physics layer isolation so drones don't collide with each other.
/// </summary>
public class AgentTrainingManager : MonoBehaviour
{
    [Header("Drone Setup")]
    [Tooltip("The drone prefab to spawn. Must have AgentDroneController, Rigidbody, and Collider.\nAll spawned drones share the same Behavior Name for single-policy training.")]
    [SerializeField] private GameObject dronePrefab;

    [Tooltip("Number of parallel drone instances. All run on the SAME track simultaneously.\nIncrease for faster training (more experience per step). Typical: 20-100.")]
    [SerializeField] private int droneCount = 20;

    [Header("Shared Track")]
    [Tooltip("The single track generator. NOT duplicated — all drones share the same waypoints.\nEach drone independently tracks its own waypoint progress.")]
    [SerializeField] private AgentTrackGenerator sharedTrack;

    [Header("Spawn")]
    [Tooltip("The spawn point for all drones. All drones start from this position at episode begin.\nIf not set, uses this GameObject's position.")]
    [SerializeField] private Transform spawnPoint;

    [Header("Physics Layer")]
    [Tooltip("Name of the physics layer for training drones.\nThis layer must exist in Unity (Edit > Project Settings > Tags and Layers).\nIn Physics settings, disable collision between this layer and itself.")]
    [SerializeField] private string droneLayerName = "TrainingDrone";

    // Aesthetically pleasing color palette
    private static readonly Color[] DronePalette = new Color[]
    {
        new Color(0.20f, 0.60f, 1.00f), // Sky Blue
        new Color(1.00f, 0.40f, 0.40f), // Coral Red
        new Color(0.30f, 0.85f, 0.45f), // Emerald Green
        new Color(1.00f, 0.75f, 0.20f), // Amber
        new Color(0.70f, 0.40f, 0.90f), // Violet
        new Color(0.00f, 0.85f, 0.85f), // Teal
        new Color(1.00f, 0.50f, 0.70f), // Pink
        new Color(0.95f, 0.90f, 0.25f), // Lemon
        new Color(0.55f, 0.35f, 0.20f), // Bronze
        new Color(0.40f, 0.75f, 0.70f), // Seafoam
        new Color(0.85f, 0.35f, 0.10f), // Burnt Orange
        new Color(0.45f, 0.55f, 0.85f), // Periwinkle
        new Color(0.75f, 0.85f, 0.30f), // Lime
        new Color(0.90f, 0.30f, 0.55f), // Magenta
        new Color(0.30f, 0.50f, 0.35f), // Forest
        new Color(0.85f, 0.65f, 0.50f), // Peach
    };

    private List<GameObject> spawnedDrones = new List<GameObject>();

    private void Start()
    {
        // Generate track first (so waypoints exist before drones subscribe)
        if (sharedTrack != null && sharedTrack.WaypointCount == 0)
        {
            sharedTrack.GenerateTrack();
        }

        SpawnDrones();
    }

    /// <summary>
    /// Spawn all drone instances at the shared spawn point.
    /// </summary>
    [ContextMenu("Spawn Drones")]
    public void SpawnDrones()
    {
        ClearDrones();

        if (dronePrefab == null)
        {
            Debug.LogError("[AgentTrainingManager] Drone prefab is not assigned!");
            return;
        }

        if (sharedTrack == null)
        {
            Debug.LogError("[AgentTrainingManager] Shared track is not assigned!");
            return;
        }

        // Resolve layer
        int droneLayer = LayerMask.NameToLayer(droneLayerName);
        if (droneLayer == -1)
        {
            Debug.LogError($"[AgentTrainingManager] Layer '{droneLayerName}' does not exist! " +
                           "Create it in Edit > Project Settings > Tags and Layers. " +
                           "Then disable its self-collision in Edit > Project Settings > Physics > Layer Collision Matrix.");
            return;
        }

        // Verify layer self-collision is disabled
        if (!Physics.GetIgnoreLayerCollision(droneLayer, droneLayer))
        {
            Debug.LogWarning($"[AgentTrainingManager] Layer '{droneLayerName}' can still collide with itself! " +
                             "Go to Edit > Project Settings > Physics > Layer Collision Matrix and uncheck the intersection of " +
                             $"'{droneLayerName}' with '{droneLayerName}'.");
            // Auto-fix at runtime
            Physics.IgnoreLayerCollision(droneLayer, droneLayer, true);
            Debug.Log($"[AgentTrainingManager] Auto-disabled '{droneLayerName}' self-collision for this session.");
        }

        Vector3 spawnPos = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion spawnRot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        for (int i = 0; i < droneCount; i++)
        {
            // All drones spawn at the SAME position
            GameObject drone = Instantiate(dronePrefab, spawnPos, spawnRot, transform);
            drone.name = $"TrainingDrone_{i:D3}";

            // Set layer recursively (drone + all children)
            SetLayerRecursive(drone, droneLayer);

            // Ensure child colliders are tagged "Player" for waypoint trigger detection
            EnsurePlayerTagOnColliders(drone);

            // Assign unique color via material INSTANCE (not shared)
            ApplyColorMaterialInstance(drone, DronePalette[i % DronePalette.Length]);

            // Configure agent
            AgentDroneController agent = drone.GetComponent<AgentDroneController>();
            if (agent != null)
            {
                agent.SetSpawnPosition(spawnPos, spawnRot);
                agent.SetTrackGenerator(sharedTrack);
            }

            spawnedDrones.Add(drone);
        }

        Debug.Log($"[AgentTrainingManager] Spawned {droneCount} drones on shared track '{sharedTrack.name}', layer '{droneLayerName}'.");
    }

    /// <summary>
    /// Destroy all spawned drones.
    /// </summary>
    [ContextMenu("Clear Drones")]
    public void ClearDrones()
    {
        foreach (var drone in spawnedDrones)
        {
            if (drone != null)
            {
                if (Application.isPlaying)
                    Destroy(drone);
                else
                    DestroyImmediate(drone);
            }
        }
        spawnedDrones.Clear();
    }

    private void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }

    private void EnsurePlayerTagOnColliders(GameObject obj)
    {
        // Tag all child objects that have colliders as "Player" so waypoint triggers detect them.
        // The root drone does NOT need the Player tag — only collider children.
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            if (!col.isTrigger) // Don't re-tag trigger colliders
            {
                col.gameObject.tag = "Player";
            }
        }
    }

    private void ApplyColorMaterialInstance(GameObject drone, Color color)
    {
        Renderer[] renderers = drone.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            // Create a new material INSTANCE for each drone (not shared)
            Material[] mats = renderer.materials; // .materials returns copies
            for (int m = 0; m < mats.Length; m++)
            {
                mats[m].color = color;
                mats[m].SetColor("_BaseColor", color); // URP/HDRP
                if (mats[m].HasProperty("_EmissionColor"))
                {
                    mats[m].EnableKeyword("_EMISSION");
                    mats[m].SetColor("_EmissionColor", color * 0.3f);
                }
            }
            renderer.materials = mats;
        }
    }
}

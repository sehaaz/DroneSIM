using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapRenderer : MonoBehaviour
{
    [Header("Targeting")]
    public Transform player;
    public Camera playerCamera; // To visualize view angle
    public RaceManager raceManager;

    [Header("Map Settings")]
    public RectTransform mapContainer;
    [Tooltip("Size of the visible area in world meters (width).")]
    public float mapSizeMeters = 100f;
    public bool rotateMapWithPlayer = false;

    [Header("Visuals")]
    [Tooltip("If null, a simple square will be generated.")]
    public Sprite iconSprite;
    public Color playerColor = Color.cyan;
    public Color activeGateColor = Color.green;
    public Color nextGateColor = Color.yellow;
    public Color otherGateColor = Color.gray;
    public Color worldObjectColor = new Color(0.5f, 0.2f, 0.8f); // Purple-ish
    public Color signalRangeColor = new Color(1f, 0f, 0f, 0.1f);

    private Dictionary<Transform, RectTransform> markers = new Dictionary<Transform, RectTransform>();
    private RectTransform playerMarker;
    private RectTransform viewCone;
    private RectTransform signalZone;

    private void Start()
    {
        if (raceManager == null) raceManager = FindObjectOfType<RaceManager>();
        if (player == null)
        {
            var drone = FindObjectOfType<DroneController>();
            if (drone != null) player = drone.transform;
        }
        if (playerCamera == null) playerCamera = Camera.main;
        if (mapContainer == null) mapContainer = GetComponent<RectTransform>();

        // Create Markers
        CreatePlayerMarker();
        CreateGateMarkers();
        CreateWorldObjectMarkers();
        CreateSignalZone();
    }

    private void LateUpdate()
    {
        if (player == null || mapContainer == null) return;

        UpdatePlayerMarker();
        UpdateGateMarkers();
        UpdateWorldObjectMarkers();
        UpdateSignalZone();
    }

    private void CreatePlayerMarker()
    {
        playerMarker = CreateIcon("PlayerMarker", playerColor, 15f);

        // Create View Cone as child
        GameObject coneObj = new GameObject("ViewCone", typeof(Image));
        coneObj.transform.SetParent(playerMarker, false);
        Image coneImg = coneObj.GetComponent<Image>();
        coneImg.color = new Color(1f, 1f, 1f, 0.3f);

        // Make it a trapezoid/cone shape by manipulating rect or sprite? 
        // Simple distinct visual: A long semi-transparent rectangle extending 'up'
        RectTransform rt = coneObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f); // Pivot at bottom
        rt.sizeDelta = new Vector2(20f, 60f); // Wide beam
        rt.anchoredPosition = Vector2.zero;

        viewCone = rt;
    }

    private void CreateSignalZone()
    {
        // One large circle for the signal limit
        GameObject zoneObj = new GameObject("SignalZone", typeof(Image));
        zoneObj.transform.SetParent(mapContainer, false);
        // Push to back
        zoneObj.transform.SetAsFirstSibling();

        Image img = zoneObj.GetComponent<Image>();
        img.color = signalRangeColor;

        // If we don't have a circle sprite, it's just a square. 
        // We can try to rely on the user having a standard 'Knob' or 'Circle' sprite, 
        // or just accept a square for now.
        // Or we can generate a circle texture?
        img.sprite = CreateCircleTexture(128, signalRangeColor);

        signalZone = zoneObj.GetComponent<RectTransform>();
        signalZone.gameObject.SetActive(false);
    }

    private void CreateGateMarkers()
    {
        if (raceManager == null) return;

        foreach (var gate in raceManager.gates)
        {
            if (gate != null && !markers.ContainsKey(gate.transform))
            {
                RectTransform marker = CreateIcon($"Gate_{gate.name}", otherGateColor, 20f);
                markers.Add(gate.transform, marker);
            }
        }
    }

    private void CreateWorldObjectMarkers()
    {
        // Find by tag
        GameObject[] objs = GameObject.FindGameObjectsWithTag("World Object"); // Check exact tag from DroneController
        foreach (var obj in objs)
        {
            if (!markers.ContainsKey(obj.transform))
            {
                RectTransform marker = CreateIcon($"Obj_{obj.name}", worldObjectColor, 12f);
                markers.Add(obj.transform, marker);
            }
        }
    }

    // --- Updaters ---

    private void UpdatePlayerMarker()
    {
        // Player is always center of the map logic IF we are moving the specific markers.
        // Actually, minimaps usually:
        // A) Keep player center, move everything else.
        // B) Keep map static, move player.

        // Let's implement Type A (Player Centered) effectively by moving markers relative to map center.

        // Player Marker stays at center (0,0)
        playerMarker.anchoredPosition = Vector2.zero;

        // Rotation
        float mapRot = rotateMapWithPlayer ? -player.eulerAngles.y : 0f;

        // If map rotates, player icon stays pointing UP (0).
        // If map is static (North Up), player icon rotates to match player heading.
        float playerIconRot = rotateMapWithPlayer ? 0f : -player.eulerAngles.y;
        playerMarker.localEulerAngles = new Vector3(0, 0, playerIconRot);

        // View Cone reflects Camera Header
        if (playerCamera != null)
        {
            float camAngle = playerCamera.transform.eulerAngles.y;
            // Relative to player?
            // If player icon is at 'playerIconRot', then camera is relative to that.
            // Actually, simplest is:
            // Map Static: Player Icon = Player Rot. View Cone = Camera Rot.
            if (!rotateMapWithPlayer)
            {
                viewCone.localEulerAngles = new Vector3(0, 0, -camAngle + player.eulerAngles.y);
                // Wait, view cone is child of marker.
                // Marker is at -PlayerRot.
                // Children is at -CamRot (global) - (-PlayerRot) = (-Cam + Player)?
                // Let's try simpler:
                // View Cone should point in Camera Direction.
                // If Marker is rot=0, Cone absolute rot = -CamRot.
            }
        }
    }

    private void UpdateGateMarkers()
    {
        if (raceManager == null) return;

        int activeIndex = raceManager.CurrentGateIndex;
        int nextIndex = (activeIndex + 1) % raceManager.gates.Count;

        UpdateMarkersList(raceManager.gates, activeIndex, nextIndex);
    }

    private void UpdateWorldObjectMarkers()
    {
        // Static objects don't change state much, just position
        // Re-iterate markers that are NOT gates?
        // Let simplified: just iterate existing dictionary.

        foreach (var kvp in markers)
        {
            Transform t = kvp.Key;
            RectTransform rt = kvp.Value;

            if (t == null) continue; // Object destroyed?

            // Check if it's a gate to update color
            var gate = t.GetComponent<RaceGate>();
            if (gate != null)
            {
                // Colors handled in UpdateMarkersList? 
                // Let's separate positions from colors if possible, but unified is closer.
                // Refactor: Just update positions here for ALL markers.
            }

            UpdateMarkerPosition(t, rt);
        }
    }

    private void UpdateMarkersList(List<RaceGate> gates, int activeIdx, int nextIdx)
    {
        for (int i = 0; i < gates.Count; i++)
        {
            var gate = gates[i];
            if (gate == null) continue;
            if (!markers.TryGetValue(gate.transform, out RectTransform rt)) continue;

            Image img = rt.GetComponent<Image>();
            if (i == activeIdx) img.color = activeGateColor;
            else if (i == nextIdx) img.color = nextGateColor;
            else img.color = otherGateColor;
        }
    }

    private void UpdateSignalZone()
    {
        if (raceManager == null || raceManager.gates.Count == 0)
        {
            signalZone.gameObject.SetActive(false);
            return;
        }

        signalZone.gameObject.SetActive(true);

        // Find Active Gate
        int idx = raceManager.CurrentGateIndex;
        if (idx < 0 || idx >= raceManager.gates.Count) return;

        RaceGate activeGate = raceManager.gates[idx];
        if (activeGate == null) return;

        // Position relative to player
        UpdateMarkerPosition(activeGate.transform, signalZone);

        // Scale size
        // Size in World Distance = maxDistanceToGate * 2 (Diameter)
        float worldDiameter = raceManager.maxDistanceToGate * 2f;
        float uiSize = worldDiameter * GetPixelsPerMeter();

        signalZone.sizeDelta = new Vector2(uiSize, uiSize);
    }

    private void UpdateMarkerPosition(Transform target, RectTransform ui)
    {
        Vector3 worldPos = target.position;
        Vector3 playerPos = player.position;

        Vector3 diff = worldPos - playerPos;

        // Rotate diff if Map Rotates (so forward is always up)
        if (rotateMapWithPlayer)
        {
            // Rotate vector by -PlayerY
            Quaternion turn = Quaternion.Euler(0, -player.eulerAngles.y, 0);
            diff = turn * diff;
        }

        // Map X/Z to UI X/Y
        // World X -> UI X
        // World Z -> UI Y
        // Scaling
        float scale = GetPixelsPerMeter();

        ui.anchoredPosition = new Vector2(diff.x * scale, diff.z * scale);

        // Clamp to map bounds? User didn't ask, but good for cleanliness.
        // For now, let them float off.
    }

    private float GetPixelsPerMeter()
    {
        // If Map container is 200px wide, and MapSizeMeters is 100m
        // Then 2px = 1m
        return mapContainer.rect.width / mapSizeMeters;
    }

    // --- Helpers ---

    private RectTransform CreateIcon(string name, Color color, float size)
    {
        GameObject obj = new GameObject(name, typeof(Image));
        obj.transform.SetParent(mapContainer, false);

        Image img = obj.GetComponent<Image>();
        if (iconSprite != null) img.sprite = iconSprite;
        else img.sprite = CreateSquareTexture(); // Simple fallback

        img.color = color;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(size, size);

        return rt;
    }

    private Sprite CreateSquareTexture()
    {
        Texture2D tex = new Texture2D(2, 2);
        tex.SetPixels(new Color[] { Color.white, Color.white, Color.white, Color.white });
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f));
    }

    private Sprite CreateCircleTexture(int size, Color c)
    {
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d < radius) colors[y * size + x] = Color.white;
                else colors[y * size + x] = Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}

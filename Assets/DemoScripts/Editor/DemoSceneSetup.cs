using UnityEngine;
using UnityEditor;

public class DemoSceneSetup : EditorWindow
{
    private int waypointCount = 8;
    private float trackRadius = 30f;
    private float trackHeight = 8f;
    private float heightVariation = 4f;
    private bool createGround = true;

    [MenuItem("DroneSIM/Demo Scene Setup")]
    public static void ShowWindow()
    {
        GetWindow<DemoSceneSetup>("Demo Setup");
    }

    private void OnGUI()
    {
        GUILayout.Label("FPV Demo Scene Setup", EditorStyles.boldLabel);
        GUILayout.Space(10);

        waypointCount = EditorGUILayout.IntSlider("Waypoint Count", waypointCount, 4, 20);
        trackRadius = EditorGUILayout.Slider("Track Radius", trackRadius, 10f, 100f);
        trackHeight = EditorGUILayout.Slider("Base Height", trackHeight, 2f, 30f);
        heightVariation = EditorGUILayout.Slider("Height Variation", heightVariation, 0f, 15f);
        createGround = EditorGUILayout.Toggle("Create Ground Plane", createGround);

        GUILayout.Space(15);

        if (GUILayout.Button("Build Demo Scene", GUILayout.Height(40)))
        {
            BuildDemoScene();
        }

        GUILayout.Space(10);
        EditorGUILayout.HelpBox(
            "This will create:\n" +
            "- FPV Drone (with physics + autopilot)\n" +
            "- Waypoint Path (circular track)\n" +
            "- Gate visuals at each waypoint\n" +
            "- Demo Camera\n" +
            "- Demo HUD\n" +
            "- Gate Manager\n\n" +
            "You can move waypoints freely after creation.",
            MessageType.Info);
    }

    private void BuildDemoScene()
    {
        // Ground
        if (createGround)
        {
            var existingGround = GameObject.Find("DemoGround");
            if (existingGround == null)
            {
                var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "DemoGround";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = new Vector3(20, 1, 20);
                ground.tag = "Ground";

                var renderer = ground.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.15f, 0.25f, 0.15f);
                renderer.material = mat;
            }
        }

        // Drone
        GameObject drone = CreateDrone();

        // Waypoint Path
        GameObject pathObj = CreateWaypointPath();

        // Camera
        GameObject camObj = SetupCamera(drone.transform);

        // HUD
        GameObject hudObj = new GameObject("DemoHUD");
        var hud = hudObj.AddComponent<DemoHUD>();

        // Gate Manager
        GameObject gateManagerObj = new GameObject("DemoGateManager");
        var gateManager = gateManagerObj.AddComponent<DemoGateManager>();

        // Wire up references via SerializedObject
        var autopilot = drone.GetComponent<DemoAutopilot>();
        var waypointPath = pathObj.GetComponent<DemoWaypointPath>();

        // Autopilot -> Path
        var apSO = new SerializedObject(autopilot);
        apSO.FindProperty("waypointPath").objectReferenceValue = waypointPath;
        apSO.ApplyModifiedProperties();

        // HUD -> references
        var hudSO = new SerializedObject(hud);
        hudSO.FindProperty("autopilot").objectReferenceValue = autopilot;
        hudSO.FindProperty("waypointPath").objectReferenceValue = waypointPath;
        hudSO.FindProperty("fpvCamera").objectReferenceValue = camObj.GetComponent<DemoFPVCamera>();
        hudSO.ApplyModifiedProperties();

        // Gate Manager -> references
        var gmSO = new SerializedObject(gateManager);
        gmSO.FindProperty("waypointPath").objectReferenceValue = waypointPath;
        gmSO.FindProperty("autopilot").objectReferenceValue = autopilot;
        gmSO.ApplyModifiedProperties();

        Selection.activeGameObject = drone;

        Debug.Log("[DemoSceneSetup] Demo scene created! Press Play to see the drone fly.");
    }

    private GameObject CreateDrone()
    {
        var existing = GameObject.Find("DemoFPVDrone");
        if (existing != null) DestroyImmediate(existing);

        GameObject drone = new GameObject("DemoFPVDrone");
        drone.transform.position = new Vector3(0f, 2f, 0f);

        // Body
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(drone.transform);
        body.transform.localPosition = Vector3.zero;
        body.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        var bodyRenderer = body.GetComponent<Renderer>();
        var bodyMat = new Material(Shader.Find("Standard"));
        bodyMat.color = new Color(0.1f, 0.1f, 0.12f);
        bodyRenderer.material = bodyMat;
        body.tag = "Player";

        // Arms and props
        Vector3[] armPositions = {
            new Vector3(0.2f, 0, 0.2f),
            new Vector3(-0.2f, 0, 0.2f),
            new Vector3(0.2f, 0, -0.2f),
            new Vector3(-0.2f, 0, -0.2f)
        };

        Color[] motorColors = {
            new Color(1f, 0.2f, 0.2f),
            new Color(0.2f, 0.2f, 1f),
            new Color(1f, 0.2f, 0.2f),
            new Color(0.2f, 0.2f, 1f)
        };

        Transform[] propTransforms = new Transform[4];

        for (int i = 0; i < 4; i++)
        {
            // Arm
            GameObject arm = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arm.name = $"Arm_{i}";
            arm.transform.SetParent(drone.transform);
            arm.transform.localPosition = armPositions[i] * 0.5f;
            arm.transform.localScale = new Vector3(0.04f, 0.03f, 0.04f);
            arm.GetComponent<Renderer>().material = bodyMat;

            // Motor mount
            GameObject motor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            motor.name = $"Motor_{i}";
            motor.transform.SetParent(drone.transform);
            motor.transform.localPosition = armPositions[i] + Vector3.up * 0.02f;
            motor.transform.localScale = new Vector3(0.06f, 0.025f, 0.06f);
            var motorMat = new Material(Shader.Find("Standard"));
            motorMat.color = motorColors[i];
            motor.GetComponent<Renderer>().material = motorMat;

            // Prop disc (flat cylinder to represent spinning prop)
            GameObject prop = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            prop.name = $"Prop_{i}";
            prop.transform.SetParent(drone.transform);
            prop.transform.localPosition = armPositions[i] + Vector3.up * 0.05f;
            prop.transform.localScale = new Vector3(0.22f, 0.003f, 0.22f);
            var propMat = new Material(Shader.Find("Standard"));
            propMat.color = new Color(0.6f, 0.6f, 0.6f, 0.4f);
            if (propMat.HasProperty("_Mode"))
            {
                propMat.SetFloat("_Mode", 3);
                propMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                propMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                propMat.SetInt("_ZWrite", 0);
                propMat.DisableKeyword("_ALPHATEST_ON");
                propMat.EnableKeyword("_ALPHABLEND_ON");
                propMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                propMat.renderQueue = 3000;
            }
            prop.GetComponent<Renderer>().material = propMat;

            // Remove collider from prop
            DestroyImmediate(prop.GetComponent<Collider>());
            DestroyImmediate(motor.GetComponent<Collider>());
            DestroyImmediate(arm.GetComponent<Collider>());

            propTransforms[i] = prop.transform;
        }

        // FPV Camera indicator (small red cube at front)
        GameObject fpvMarker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fpvMarker.name = "FPV_Camera_Mount";
        fpvMarker.transform.SetParent(drone.transform);
        fpvMarker.transform.localPosition = new Vector3(0f, 0.02f, 0.2f);
        fpvMarker.transform.localScale = new Vector3(0.04f, 0.04f, 0.04f);
        var fpvMat = new Material(Shader.Find("Standard"));
        fpvMat.color = Color.red;
        fpvMat.EnableKeyword("_EMISSION");
        fpvMat.SetColor("_EmissionColor", Color.red * 0.5f);
        fpvMarker.GetComponent<Renderer>().material = fpvMat;
        DestroyImmediate(fpvMarker.GetComponent<Collider>());

        // Add components
        var rb = drone.AddComponent<Rigidbody>();
        rb.mass = 0.8f;

        drone.AddComponent<DemoDronePhysics>();
        drone.AddComponent<DemoAutopilot>();

        var spinner = drone.AddComponent<DemoPropSpinner>();
        var spinnerSO = new SerializedObject(spinner);
        var propsArray = spinnerSO.FindProperty("propellers");
        propsArray.arraySize = 4;
        for (int i = 0; i < 4; i++)
            propsArray.GetArrayElementAtIndex(i).objectReferenceValue = propTransforms[i];
        spinnerSO.ApplyModifiedProperties();

        return drone;
    }

    private GameObject CreateWaypointPath()
    {
        var existing = GameObject.Find("DemoWaypointPath");
        if (existing != null) DestroyImmediate(existing);

        GameObject pathObj = new GameObject("DemoWaypointPath");
        pathObj.AddComponent<DemoWaypointPath>();

        for (int i = 0; i < waypointCount; i++)
        {
            float angle = (float)i / waypointCount * Mathf.PI * 2f;
            float x = Mathf.Sin(angle) * trackRadius;
            float z = Mathf.Cos(angle) * trackRadius;
            float y = trackHeight + Mathf.Sin(angle * 2f) * heightVariation;

            GameObject wpObj = new GameObject($"Waypoint_{i:D2}");
            wpObj.transform.SetParent(pathObj.transform);
            wpObj.transform.position = new Vector3(x, y, z);

            // Look direction: tangent of the circle
            float nextAngle = (float)(i + 1) / waypointCount * Mathf.PI * 2f;
            Vector3 nextPos = new Vector3(Mathf.Sin(nextAngle) * trackRadius, y, Mathf.Cos(nextAngle) * trackRadius);
            Vector3 forward = (nextPos - wpObj.transform.position).normalized;
            if (forward.sqrMagnitude > 0.01f)
                wpObj.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // Gate visual: a rectangular frame
            CreateGateVisual(wpObj);
        }

        return pathObj;
    }

    private void CreateGateVisual(GameObject parent)
    {
        float gateWidth = 5f;
        float gateHeight = 3.5f;
        float gateDepth = 0.6f;

        var gateComp = parent.AddComponent<DemoWaypointGate>();

        // Transparent prism material
        Material gateMat = new Material(Shader.Find("Standard"));
        Color gateColor = new Color(0.1f, 1f, 0.4f, 0.18f);
        gateMat.SetFloat("_Mode", 3); // Transparent
        gateMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        gateMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        gateMat.SetInt("_ZWrite", 0);
        gateMat.DisableKeyword("_ALPHATEST_ON");
        gateMat.EnableKeyword("_ALPHABLEND_ON");
        gateMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        gateMat.renderQueue = 3000;
        gateMat.color = gateColor;
        gateMat.EnableKeyword("_EMISSION");
        gateMat.SetColor("_EmissionColor", new Color(0.1f, 1f, 0.4f) * 0.3f);

        // Main transparent prism (the gate the drone flies through)
        GameObject prism = GameObject.CreatePrimitive(PrimitiveType.Cube);
        prism.name = "GatePrism";
        prism.transform.SetParent(parent.transform);
        prism.transform.localPosition = Vector3.zero;
        prism.transform.localScale = new Vector3(gateWidth, gateHeight, gateDepth);
        prism.transform.localRotation = Quaternion.identity;
        prism.GetComponent<Renderer>().material = gateMat;
        DestroyImmediate(prism.GetComponent<Collider>());

        // Thin edge wireframe using 4 edge bars for visibility
        Material edgeMat = new Material(Shader.Find("Standard"));
        Color edgeColor = new Color(0.2f, 1f, 0.5f, 0.7f);
        edgeMat.SetFloat("_Mode", 3);
        edgeMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        edgeMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        edgeMat.SetInt("_ZWrite", 0);
        edgeMat.DisableKeyword("_ALPHATEST_ON");
        edgeMat.EnableKeyword("_ALPHABLEND_ON");
        edgeMat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        edgeMat.renderQueue = 3001;
        edgeMat.color = edgeColor;
        edgeMat.EnableKeyword("_EMISSION");
        edgeMat.SetColor("_EmissionColor", new Color(0.2f, 1f, 0.5f) * 0.6f);

        float bar = 0.08f;
        float hw = gateWidth / 2f;
        float hh = gateHeight / 2f;

        // Top edge
        CreateEdgeBar(parent.transform, "EdgeTop", new Vector3(0, hh, 0), new Vector3(gateWidth, bar, gateDepth + 0.02f), edgeMat);
        // Bottom edge
        CreateEdgeBar(parent.transform, "EdgeBottom", new Vector3(0, -hh, 0), new Vector3(gateWidth, bar, gateDepth + 0.02f), edgeMat);
        // Left edge
        CreateEdgeBar(parent.transform, "EdgeLeft", new Vector3(-hw, 0, 0), new Vector3(bar, gateHeight, gateDepth + 0.02f), edgeMat);
        // Right edge
        CreateEdgeBar(parent.transform, "EdgeRight", new Vector3(hw, 0, 0), new Vector3(bar, gateHeight, gateDepth + 0.02f), edgeMat);

        // Wire up renderer reference (use the main prism)
        var gateSO = new SerializedObject(gateComp);
        gateSO.FindProperty("gateRenderer").objectReferenceValue = prism.GetComponent<Renderer>();
        gateSO.ApplyModifiedProperties();
    }

    private void CreateEdgeBar(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bar.name = name;
        bar.transform.SetParent(parent);
        bar.transform.localPosition = localPos;
        bar.transform.localScale = scale;
        bar.transform.localRotation = Quaternion.identity;
        bar.GetComponent<Renderer>().material = mat;
        DestroyImmediate(bar.GetComponent<Collider>());
    }

    private GameObject SetupCamera(Transform droneTransform)
    {
        Camera mainCam = Camera.main;
        GameObject camObj;

        if (mainCam != null)
        {
            camObj = mainCam.gameObject;
        }
        else
        {
            camObj = new GameObject("DemoCamera");
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
        }

        camObj.transform.position = droneTransform.position + new Vector3(0f, 2f, -5f);

        var existingFpvCam = camObj.GetComponent<DemoFPVCamera>();
        if (existingFpvCam != null) DestroyImmediate(existingFpvCam);

        var fpvCam = camObj.AddComponent<DemoFPVCamera>();
        var camSO = new SerializedObject(fpvCam);
        camSO.FindProperty("target").objectReferenceValue = droneTransform;
        camSO.ApplyModifiedProperties();

        return camObj;
    }
}

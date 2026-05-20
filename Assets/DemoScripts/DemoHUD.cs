using UnityEngine;

public class DemoHUD : MonoBehaviour
{
    [SerializeField] private DemoAutopilot autopilot;
    [SerializeField] private DemoWaypointPath waypointPath;
    [SerializeField] private DemoFPVCamera fpvCamera;

    private Rigidbody droneRb;
    private float raceTimer;
    private bool timerStarted;
    private GUIStyle boxStyle;
    private GUIStyle labelStyle;
    private GUIStyle headerStyle;

    private void Start()
    {
        if (autopilot == null)
            autopilot = FindObjectOfType<DemoAutopilot>();
        if (waypointPath == null)
            waypointPath = FindObjectOfType<DemoWaypointPath>();
        if (fpvCamera == null)
            fpvCamera = FindObjectOfType<DemoFPVCamera>();
        if (autopilot != null)
            droneRb = autopilot.GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (autopilot == null) return;

        if (autopilot.IsFlying && !autopilot.IsFinished)
        {
            timerStarted = true;
            raceTimer += Time.deltaTime;
        }
    }

    private void OnGUI()
    {
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box);
            boxStyle.fontSize = 14;
            boxStyle.normal.textColor = Color.white;

            labelStyle = new GUIStyle(GUI.skin.label);
            labelStyle.fontSize = 16;
            labelStyle.fontStyle = FontStyle.Bold;
            labelStyle.normal.textColor = Color.white;

            headerStyle = new GUIStyle(GUI.skin.label);
            headerStyle.fontSize = 22;
            headerStyle.fontStyle = FontStyle.Bold;
            headerStyle.normal.textColor = new Color(0.2f, 1f, 0.5f);
            headerStyle.alignment = TextAnchor.MiddleCenter;
        }

        float speed = droneRb != null ? droneRb.velocity.magnitude : 0f;
        float altitude = autopilot != null ? autopilot.transform.position.y : 0f;
        int wpCount = waypointPath != null ? waypointPath.Count : 0;
        int currentWp = autopilot != null ? autopilot.CurrentWaypointIndex : 0;

        // Top-left telemetry panel
        GUILayout.BeginArea(new Rect(15, 15, 220, 160));
        GUI.Box(new Rect(0, 0, 220, 160), "");

        GUILayout.Space(8);
        GUILayout.Label($"  Speed: {speed:F1} m/s", labelStyle);
        GUILayout.Label($"  Alt:   {altitude:F1} m", labelStyle);
        GUILayout.Label($"  WP:    {Mathf.Min(currentWp + 1, wpCount)} / {wpCount}", labelStyle);

        int min = Mathf.FloorToInt(raceTimer / 60f);
        float sec = raceTimer % 60f;
        GUILayout.Label($"  Time:  {min:D2}:{sec:05.2f}", labelStyle);

        GUILayout.EndArea();

        // Center message when finished
        if (autopilot != null && autopilot.IsFinished)
        {
            float w = 400f;
            float h = 50f;
            Rect center = new Rect((Screen.width - w) / 2f, 80, w, h);
            GUI.Label(center, $"TRACK COMPLETE  -  {min:D2}:{sec:05.2f}", headerStyle);
        }

        // Bottom-center camera mode
        string modeText = "C: Switch Camera";
        GUIStyle bottomStyle = new GUIStyle(GUI.skin.label);
        bottomStyle.fontSize = 13;
        bottomStyle.normal.textColor = new Color(1f, 1f, 1f, 0.6f);
        bottomStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(0, Screen.height - 35, Screen.width, 30), modeText, bottomStyle);
    }
}

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Main controller for the drone selection scene.
/// Manages drone cards, detail panel, control type selection, and scene transition.
/// </summary>
public class DroneSelectionUI : MonoBehaviour
{
    [Header("Drone Configurations")]
    [SerializeField] private DroneConfig[] droneConfigs;

    [Header("Drone Cards")]
    [SerializeField] private DroneCardUI[] droneCards;

    [Header("Detail Panel - Identity")]
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailDescription;
    [SerializeField] private TextMeshProUGUI detailDifficulty;

    [Header("Detail Panel - Spec Bars")]
    [SerializeField] private Slider barMass;
    [SerializeField] private Slider barThrust;
    [SerializeField] private Slider barAgility;
    [SerializeField] private Slider barStability;

    [Header("Detail Panel - Technical Data")]
    [SerializeField] private TextMeshProUGUI specMass;
    [SerializeField] private TextMeshProUGUI specThrust;
    [SerializeField] private TextMeshProUGUI specTiltAngle;
    [SerializeField] private TextMeshProUGUI specYawRate;
    [SerializeField] private TextMeshProUGUI specAcroRate;
    [SerializeField] private TextMeshProUGUI specPID;
    [SerializeField] private TextMeshProUGUI specDrag;

    [Header("Controls")]
    [SerializeField] private TMP_Dropdown controlDropdown;
    [SerializeField] private Button startButton;
    [SerializeField] private Button exitButton;

    [Header("Scene")]
    [SerializeField] private string simulationSceneName = "SampleScene";

    private int selectedIndex = 0;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        Time.timeScale = 1f;

        // Setup control dropdown
        if (controlDropdown != null)
        {
            controlDropdown.ClearOptions();
            controlDropdown.AddOptions(new System.Collections.Generic.List<string>
            {
                "Keyboard", "Joystick", "UDP (Mobile)"
            });
            controlDropdown.value = 0;
        }

        // Setup buttons
        if (startButton != null)
            startButton.onClick.AddListener(OnStartClicked);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitClicked);

        // Initialize cards
        for (int i = 0; i < droneCards.Length && i < droneConfigs.Length; i++)
        {
            int index = i;
            droneCards[i].Initialize(droneConfigs[i], () => SelectDrone(index));
        }

        // Select first drone by default
        SelectDrone(0);
    }

    private void SelectDrone(int index)
    {
        selectedIndex = index;

        for (int i = 0; i < droneCards.Length; i++)
        {
            droneCards[i].SetSelected(i == index);
        }

        UpdateDetailPanel(droneConfigs[index]);
    }

    private void UpdateDetailPanel(DroneConfig config)
    {
        // Identity
        if (detailName != null) detailName.text = config.droneName;
        if (detailDescription != null) detailDescription.text = config.description;

        if (detailDifficulty != null)
        {
            detailDifficulty.text = config.difficulty.ToString().ToUpper();
            detailDifficulty.color = DroneCardUI.GetDifficultyColor(config.difficulty);
        }

        // Spec bars (normalized across the range of all 5 drones)
        if (barMass != null) barMass.value = Mathf.InverseLerp(0.5f, 1.6f, config.mass);
        if (barThrust != null) barThrust.value = Mathf.InverseLerp(1.5f, 3.2f, config.maxThrustScale);
        if (barAgility != null) barAgility.value = Mathf.InverseLerp(300f, 1000f, config.acroRate + config.yawRate);
        if (barStability != null) barStability.value = Mathf.InverseLerp(3f, 12f, config.pidP + config.baseDrag + config.pidD);

        // Technical data text
        if (specMass != null) specMass.text = $"{config.mass:F2} kg";
        if (specThrust != null) specThrust.text = $"{config.maxThrustScale:F1}x";
        if (specTiltAngle != null) specTiltAngle.text = $"{config.maxTiltAngle:F0}\u00b0";
        if (specYawRate != null) specYawRate.text = $"{config.yawRate:F0}\u00b0/s";
        if (specAcroRate != null) specAcroRate.text = $"{config.acroRate:F0}\u00b0/s";
        if (specPID != null) specPID.text = $"P:{config.pidP:F1}  I:{config.pidI:F2}  D:{config.pidD:F1}";
        if (specDrag != null) specDrag.text = $"{config.baseDrag:F2}";
    }

    private void OnStartClicked()
    {
        GameSession.SelectedDroneConfig = droneConfigs[selectedIndex];
        GameSession.SelectedInputSource = (DroneController.InputSource)controlDropdown.value;
        GameSession.HasSelection = true;

        SceneManager.LoadScene(simulationSceneName);
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

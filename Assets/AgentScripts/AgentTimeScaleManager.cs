using UnityEngine;

/// <summary>
/// Controls Time.timeScale and Time.fixedDeltaTime for training acceleration.
/// Attach to any GameObject in the training scene.
/// </summary>
public class AgentTimeScaleManager : MonoBehaviour
{
    [Header("Time Scale")]
    [Tooltip("Simulation speed multiplier. 1 = normal speed, 20 = 20x faster.\nHigher values speed up training but may cause physics instability.\nStart with 1x to verify behavior, then increase gradually.\nIf drones clip through objects or behave erratically, reduce this value.")]
    [Range(1f, 20f)]
    [SerializeField] private float timeScale = 1f;

    [Header("Fixed Timestep")]
    [Tooltip("If enabled, fixedDeltaTime is automatically scaled proportionally to timeScale.\nThis keeps physics simulation consistent at high speeds.\nDisable only if you want to manually control fixedDeltaTime.")]
    [SerializeField] private bool autoAdjustFixedDeltaTime = true;

    [Tooltip("Base fixedDeltaTime at timeScale = 1. Default is Unity's standard 0.02 (50 Hz).\nLower values = more accurate physics but slower simulation.\nHigher values = less accurate but faster. Don't exceed 0.04.")]
    [SerializeField] private float baseFixedDeltaTime = 0.02f;

    private float previousTimeScale = 1f;

    private void OnEnable()
    {
        ApplyTimeScale();
    }

    private void Update()
    {
        // Detect Inspector changes
        if (!Mathf.Approximately(timeScale, previousTimeScale))
        {
            ApplyTimeScale();
        }
    }

    private void ApplyTimeScale()
    {
        Time.timeScale = timeScale;

        if (autoAdjustFixedDeltaTime)
        {
            Time.fixedDeltaTime = baseFixedDeltaTime * timeScale;
        }

        previousTimeScale = timeScale;
    }

    private void OnDisable()
    {
        // Restore defaults when disabled
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
    }
}

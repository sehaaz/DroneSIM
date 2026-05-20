using UnityEngine;

public class DemoPropSpinner : MonoBehaviour
{
    [SerializeField] private Transform[] propellers;
    [SerializeField] private float baseRPM = 3000f;
    [SerializeField] private float maxRPM = 8000f;
    [SerializeField] private bool[] clockwise;

    private DemoDronePhysics drone;

    private void Start()
    {
        drone = GetComponentInParent<DemoDronePhysics>();

        if (clockwise == null || clockwise.Length != propellers.Length)
        {
            clockwise = new bool[propellers.Length];
            for (int i = 0; i < clockwise.Length; i++)
                clockwise[i] = i % 2 == 0;
        }
    }

    private void Update()
    {
        if (propellers == null) return;

        float speedFactor = 1f;
        if (drone != null && drone.Rb != null)
            speedFactor = Mathf.Clamp(0.5f + drone.Rb.velocity.magnitude * 0.05f, 0.5f, 1.5f);

        float rpm = Mathf.Lerp(baseRPM, maxRPM, speedFactor - 0.5f);
        float degreesPerFrame = rpm * 6f * Time.deltaTime;

        for (int i = 0; i < propellers.Length; i++)
        {
            if (propellers[i] == null) continue;
            float dir = clockwise[i] ? 1f : -1f;
            propellers[i].Rotate(Vector3.up, degreesPerFrame * dir, Space.Self);
        }
    }
}

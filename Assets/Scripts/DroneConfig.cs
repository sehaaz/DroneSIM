using UnityEngine;

public enum DifficultyRating
{
    Easy,
    Medium,
    Hard
}

[CreateAssetMenu(fileName = "NewDroneConfig", menuName = "DroneSIM/Drone Config")]
public class DroneConfig : ScriptableObject
{
    [Header("Identity")]
    public string droneName;
    [TextArea(2, 4)]
    public string description;
    public Sprite thumbnail;

    [Header("Difficulty")]
    public DifficultyRating difficulty;

    [Header("Physics - Mass & Thrust")]
    public float mass = 1.0f;
    public float maxThrustScale = 2.0f;

    [Header("Physics - Rotation Rates")]
    public float maxTiltAngle = 45f;
    public float yawRate = 180f;
    public float acroRate = 360f;

    [Header("Physics - PID Stabilization")]
    public float pidP = 5.0f;
    public float pidI = 0.1f;
    public float pidD = 0.5f;

    [Header("Physics - Drag")]
    public float baseDrag = 0.5f;
    public float angleAngularDrag = 2.0f;
    public float acroAngularDrag = 0.5f;

    [Header("Physics - Horizon Mode")]
    public float horizonTransitionPoint = 0.5f;

    [Header("Default Flight Mode")]
    public DronePhysics.FlightMode defaultFlightMode = DronePhysics.FlightMode.Angle;
}

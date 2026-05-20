using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class DemoDronePhysics : MonoBehaviour
{
    [Header("Mass & Thrust")]
    [SerializeField] private float mass = 0.8f;
    [SerializeField] private float maxThrustMultiplier = 2.5f;

    [Header("Torque")]
    [SerializeField] private float pitchTorque = 25f;
    [SerializeField] private float rollTorque = 25f;
    [SerializeField] private float yawTorque = 12f;

    [Header("Drag")]
    [SerializeField] private float linearDrag = 0.8f;
    [SerializeField] private float angularDrag = 4.0f;

    private Rigidbody rb;
    private float hoverThrust;

    public Rigidbody Rb => rb;
    public float HoverThrust => hoverThrust;
    public float MaxThrustMultiplier => maxThrustMultiplier;

    public void Init()
    {
        rb = GetComponent<Rigidbody>();
        rb.mass = mass;
        rb.drag = linearDrag;
        rb.angularDrag = angularDrag;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.useGravity = true;

        hoverThrust = rb.mass * Physics.gravity.magnitude;
    }

    public void ApplyThrust(float normalizedThrottle)
    {
        float thrust = normalizedThrottle * hoverThrust * maxThrustMultiplier;
        rb.AddForce(transform.up * thrust, ForceMode.Force);
    }

    public void ApplyPitchTorque(float input)
    {
        rb.AddRelativeTorque(Vector3.right * input * pitchTorque, ForceMode.Acceleration);
    }

    public void ApplyRollTorque(float input)
    {
        rb.AddRelativeTorque(Vector3.forward * input * rollTorque, ForceMode.Acceleration);
    }

    public void ApplyYawTorque(float input)
    {
        rb.AddRelativeTorque(Vector3.up * input * yawTorque, ForceMode.Acceleration);
    }
}

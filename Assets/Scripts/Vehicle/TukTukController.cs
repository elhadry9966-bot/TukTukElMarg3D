
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class TukTukController : MonoBehaviour
{
    [Header("Driving")]
    [SerializeField, Min(0f)] private float acceleration = 8f;
    [SerializeField, Min(0f)] private float maximumForwardSpeed = 13f;
    [SerializeField, Min(0f)] private float maximumReverseSpeed = 4f;
    [SerializeField, Min(0f)] private float steeringSpeed = 85f;
    [SerializeField, Min(0f)] private float brakeStrength = 11f;

    private Rigidbody body;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float throttleInput;
    private float steeringInput;
    private bool isBraking;

    public float CurrentSpeedKph
    {
        get
        {
            if (body == null)
                return 0f;

            return Mathf.Abs(Vector3.Dot(body.linearVelocity, transform.forward)) * 3.6f;
        }
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.mass = 650f;
        body.linearDamping = 0.45f;
        body.angularDamping = 4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        body.centerOfMass = new Vector3(0f, -0.42f, 0f);

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetVehicle();
            return;
        }

        throttleInput = (keyboard.wKey.isPressed ? 1f : 0f) -
                       (keyboard.sKey.isPressed ? 1f : 0f);
        steeringInput = (keyboard.dKey.isPressed ? 1f : 0f) -
                        (keyboard.aKey.isPressed ? 1f : 0f);
        isBraking = keyboard.spaceKey.isPressed;
    }

    private void FixedUpdate()
    {
        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);

        if ((throttleInput > 0f && forwardSpeed < maximumForwardSpeed) ||
            (throttleInput < 0f && forwardSpeed > -maximumReverseSpeed))
        {
            body.AddForce(
                transform.forward * throttleInput * acceleration,
                ForceMode.Acceleration);
        }

        if (isBraking)
            body.AddForce(-body.linearVelocity * brakeStrength, ForceMode.Acceleration);

        forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2f);
        if (Mathf.Abs(forwardSpeed) > 0.15f && Mathf.Abs(steeringInput) > 0.01f)
        {
            float direction = forwardSpeed >= 0f ? 1f : -1f;
            float yaw = steeringInput * steeringSpeed * direction * speedFactor *
                        Time.fixedDeltaTime;
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        Vector3 velocity = body.linearVelocity;
        float clampedForwardSpeed = Mathf.Clamp(
            Vector3.Dot(velocity, transform.forward),
            -maximumReverseSpeed,
            maximumForwardSpeed);
        body.linearVelocity += transform.forward * (clampedForwardSpeed - forwardSpeed);
    }

    private void ResetVehicle()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = startPosition;
        body.rotation = startRotation;
    }
}

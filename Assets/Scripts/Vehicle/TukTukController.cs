
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
    [SerializeField, Min(0f)] private float coastingDeceleration = 2.5f;
    [SerializeField, Min(0f)] private float lateralGrip = 4f;

    private Rigidbody body;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float throttleInput;
    private float steeringInput;
    private bool isBraking;
    private bool warnedAboutMissingKeyboard;

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
        body.isKinematic = false;
        body.useGravity = true;
        body.detectCollisions = true;
        body.mass = 650f;
        body.linearDamping = 0.45f;
        body.angularDamping = 4f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX |
                           RigidbodyConstraints.FreezeRotationZ;
        body.centerOfMass = new Vector3(0f, -0.42f, 0f);
        body.WakeUp();

        startPosition = transform.position;
        startRotation = transform.rotation;
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            throttleInput = 0f;
            steeringInput = 0f;
            isBraking = false;
            if (!warnedAboutMissingKeyboard)
            {
                Debug.LogWarning(
                    "Tuk-tuk controls require an active keyboard. Check the Input System device and Game view focus.",
                    this);
                warnedAboutMissingKeyboard = true;
            }
            return;
        }

        warnedAboutMissingKeyboard = false;

        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetVehicle();
            return;
        }

        bool forward = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        bool reverse = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        bool right = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        bool left = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        throttleInput = (forward ? 1f : 0f) - (reverse ? 1f : 0f);
        steeringInput = (right ? 1f : 0f) - (left ? 1f : 0f);
        isBraking = keyboard.spaceKey.isPressed;
    }

    private void FixedUpdate()
    {
        float forwardSpeed = Vector3.Dot(body.linearVelocity, transform.forward);
        float targetSpeed = throttleInput > 0f
            ? maximumForwardSpeed
            : throttleInput < 0f
                ? -maximumReverseSpeed
                : 0f;
        float speedChangeRate = isBraking ||
                                (throttleInput != 0f &&
                                 forwardSpeed * throttleInput < 0f)
            ? brakeStrength
            : throttleInput == 0f
                ? coastingDeceleration
                : acceleration;

        if (isBraking)
            targetSpeed = 0f;

        float adjustedSpeed = Mathf.MoveTowards(
            forwardSpeed,
            targetSpeed,
            speedChangeRate * Time.fixedDeltaTime);
        body.AddForce(
            transform.forward * ((adjustedSpeed - forwardSpeed) / Time.fixedDeltaTime),
            ForceMode.Acceleration);

        float lateralSpeed = Vector3.Dot(body.linearVelocity, transform.right);
        body.AddForce(
            -transform.right * lateralSpeed * lateralGrip,
            ForceMode.Acceleration);

        float speedFactor = Mathf.Clamp01(
            Mathf.Abs(forwardSpeed) / Mathf.Max(0.1f, maximumForwardSpeed));
        if (Mathf.Abs(forwardSpeed) > 0.15f && Mathf.Abs(steeringInput) > 0.01f)
        {
            float direction = forwardSpeed >= 0f ? 1f : -1f;
            float yaw = steeringInput * steeringSpeed * direction * speedFactor *
                        Time.fixedDeltaTime;
            body.MoveRotation(body.rotation * Quaternion.Euler(0f, yaw, 0f));
        }

        Vector3 velocity = body.linearVelocity;
        forwardSpeed = Vector3.Dot(velocity, transform.forward);
        float clampedForwardSpeed = Mathf.Clamp(
            forwardSpeed,
            -maximumReverseSpeed,
            maximumForwardSpeed);
        body.linearVelocity = velocity + transform.forward * (clampedForwardSpeed - forwardSpeed);
    }

    private void ResetVehicle()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = startPosition;
        body.rotation = startRotation;
        throttleInput = 0f;
        steeringInput = 0f;
        isBraking = false;
    }
}

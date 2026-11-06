
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
    [SerializeField] private bool showControlDebugOverlay = true;

    private Rigidbody body;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private float throttleInput;
    private float steeringInput;
    private bool isBraking;
    private bool warnedAboutMissingKeyboard;
    private bool keyboardDetected;
    private bool forwardKeyPressed;
    private bool reverseKeyPressed;
    private bool leftKeyPressed;
    private bool rightKeyPressed;
    private bool isGrounded;
    private Vector3 appliedMovement;
    private Vector3 physicsStepDisplacement;
    private Vector3 previousPhysicsPosition;
    private string collisionContact = "none";

    public float ThrottleInput => throttleInput;
    public float SteeringInput => steeringInput;
    public bool KeyboardDetected => keyboardDetected;
    public bool ForwardKeyPressed => forwardKeyPressed;
    public bool ReverseKeyPressed => reverseKeyPressed;
    public bool LeftKeyPressed => leftKeyPressed;
    public bool RightKeyPressed => rightKeyPressed;
    public bool IsGrounded => isGrounded;
    public bool IsBraking => isBraking;
    public Vector3 AppliedMovement => appliedMovement;
    public Vector3 PhysicsStepDisplacement => physicsStepDisplacement;
    public Vector3 RigidbodyVelocity => body != null ? body.linearVelocity : Vector3.zero;
    public Vector3 RigidbodyPosition => body != null ? body.position : Vector3.zero;
    public string CollisionContact => collisionContact;

#if ENABLE_INPUT_SYSTEM
    public string InputBackend => "Input System";
#elif ENABLE_LEGACY_INPUT_MANAGER
    public string InputBackend => "Legacy Input Manager";
#else
    public string InputBackend => "No input backend";
#endif

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
        body.centerOfMass = new Vector3(0f, -0.42f, 0f);
        body.WakeUp();

        startPosition = transform.position;
        startRotation = transform.rotation;
        previousPhysicsPosition = body.position;
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            keyboardDetected = false;
            ClearInput();
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

        keyboardDetected = true;
        forwardKeyPressed = keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed;
        reverseKeyPressed = keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed;
        leftKeyPressed = keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed;
        rightKeyPressed = keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed;
        isBraking = keyboard.spaceKey.isPressed;
        if (keyboard.rKey.wasPressedThisFrame)
        {
            ResetVehicle();
            return;
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        keyboardDetected = true;
        forwardKeyPressed = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        reverseKeyPressed = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        leftKeyPressed = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        rightKeyPressed = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
        isBraking = Input.GetKey(KeyCode.Space);
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetVehicle();
            return;
        }
#else
        keyboardDetected = false;
        ClearInput();
        if (!warnedAboutMissingKeyboard)
        {
            Debug.LogError(
                "Tuk-tuk controls are unavailable because neither Unity input backend is enabled.",
                this);
            warnedAboutMissingKeyboard = true;
        }
        return;
#endif

        throttleInput = (forwardKeyPressed ? 1f : 0f) - (reverseKeyPressed ? 1f : 0f);
        steeringInput = (rightKeyPressed ? 1f : 0f) - (leftKeyPressed ? 1f : 0f);
    }

    private void FixedUpdate()
    {
        physicsStepDisplacement = body.position - previousPhysicsPosition;
        previousPhysicsPosition = body.position;
        isGrounded = body.SweepTest(
            Vector3.down,
            out _,
            Mathf.Max(0.1f, Time.fixedDeltaTime * 2f),
            QueryTriggerInteraction.Ignore);

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
        appliedMovement = transform.forward * (adjustedSpeed - forwardSpeed);
        body.AddForce(
            appliedMovement / Time.fixedDeltaTime,
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

    }

    private void OnCollisionStay(Collision collision)
    {
        collisionContact = $"{collision.gameObject.name} (impulse {collision.impulse.magnitude:0.00})";
    }

    private void OnCollisionExit(Collision collision)
    {
        if (collisionContact.StartsWith(collision.gameObject.name + " ", System.StringComparison.Ordinal))
            collisionContact = "none";
    }

    private void ResetVehicle()
    {
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = startPosition;
        body.rotation = Quaternion.Euler(0f, startRotation.eulerAngles.y, 0f);
        throttleInput = 0f;
        steeringInput = 0f;
        isBraking = false;
        forwardKeyPressed = false;
        reverseKeyPressed = false;
        leftKeyPressed = false;
        rightKeyPressed = false;
        appliedMovement = Vector3.zero;
        physicsStepDisplacement = Vector3.zero;
        previousPhysicsPosition = body.position;
    }

    private void ClearInput()
    {
        throttleInput = 0f;
        steeringInput = 0f;
        isBraking = false;
        forwardKeyPressed = false;
        reverseKeyPressed = false;
        leftKeyPressed = false;
        rightKeyPressed = false;
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!showControlDebugOverlay)
            return;

        string keyboardState = keyboardDetected
            ? InputBackend == "Legacy Input Manager" ? "API active" : "DETECTED"
            : "NOT DETECTED";
        string rigidbodyState = body == null
            ? "MISSING"
            : $"active={body.gameObject.activeInHierarchy}, kinematic={body.isKinematic}, collisions={body.detectCollisions}, constraints={body.constraints}";
        GUI.Box(new Rect(12f, 12f, 470f, 236f), "TukTuk Controls");
        GUI.Label(
            new Rect(22f, 38f, 450f, 202f),
            $"Input: {InputBackend} | Keyboard: {keyboardState}\n" +
            $"W/Up: {forwardKeyPressed}  S/Down: {reverseKeyPressed}  A/Left: {leftKeyPressed}  D/Right: {rightKeyPressed}\n" +
            $"Forward Input: {throttleInput:0.0} | Steering Input: {steeringInput:0.0} | Brake: {(isBraking ? "ON" : "OFF")}\n" +
            $"Speed: {CurrentSpeedKph:0.0} km/h | Applied Movement Δv: {appliedMovement} m/s\n" +
            $"Rigidbody Velocity: {RigidbodyVelocity} m/s\n" +
            $"Rigidbody Position: {RigidbodyPosition} | Physics ΔPosition: {physicsStepDisplacement}\n" +
            $"Grounded: {isGrounded} | Contact: {collisionContact}\n" +
            $"Time Scale: {Time.timeScale:0.00} | Fixed Timestep: {Time.fixedDeltaTime:0.000}\n" +
            $"Controller: {(isActiveAndEnabled ? "ENABLED" : "DISABLED")} | Rigidbody: {rigidbodyState}");
#endif
    }
}

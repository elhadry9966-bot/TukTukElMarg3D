using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class TukTukCamera : MonoBehaviour
{
    [SerializeField] private TukTukController target;
    [SerializeField, Min(0.1f)] private float followDistance = 7.5f;
    [SerializeField, Min(0f)] private float followHeight = 1.6f;
    [SerializeField, Min(0f)] private float positionSmoothTime = 0.12f;
    [SerializeField, Min(0f)] private float rotationSmoothSpeed = 12f;
    [SerializeField, Min(0f)] private float mouseSensitivity = 0.12f;
    [SerializeField, Min(0f)] private float touchSensitivity = 0.12f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;

    private readonly RaycastHit[] cameraHits = new RaycastHit[16];
    private Vector3 positionVelocity;
    private float orbitYaw;
    private float orbitPitch = 13f;
    private bool firstPerson;

    public bool IsFirstPerson => firstPerson;
    public TukTukController Target => target;

    public void SetTarget(TukTukController vehicle)
    {
        target = vehicle;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            firstPerson = !firstPerson;

        if (target.MobileCameraTogglePressedThisFrame)
            firstPerson = !firstPerson;

        UpdateOrbitInput();

        if (firstPerson)
        {
            UpdateCockpitView();
            return;
        }

        UpdateFollowView();
    }

    private void UpdateOrbitInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            ApplyOrbitDelta(delta, mouseSensitivity);
        }

        // Touches are read by TukTukController through EnhancedTouch, avoiding
        // polling Touchscreen directly and keeping camera gestures separate
        // from the driving-control zones.
        if (target.MobileCameraDelta.sqrMagnitude > 0f)
            ApplyOrbitDelta(target.MobileCameraDelta, touchSensitivity);
    }

    private void ApplyOrbitDelta(Vector2 delta, float sensitivity)
    {
        orbitYaw += delta.x * sensitivity;
        orbitPitch = Mathf.Clamp(orbitPitch - delta.y * sensitivity, -5f, 55f);
    }

    private void UpdateFollowView()
    {
        Vector3 pivot = target.transform.position +
                        Vector3.up * followHeight +
                        target.transform.forward * 0.35f;

        Quaternion orbit = Quaternion.Euler(
            orbitPitch,
            target.transform.eulerAngles.y + orbitYaw,
            0f);

        Vector3 desiredPosition = pivot + orbit * (Vector3.back * followDistance);
        desiredPosition = ResolveCameraCollision(pivot, desiredPosition);
        desiredPosition.y = Mathf.Max(0.4f, desiredPosition.y);

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        Vector3 lookPoint = pivot + target.transform.forward * 3.5f + Vector3.up * 0.25f;
        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 offset = desiredPosition - pivot;
        float distance = offset.magnitude;
        if (distance <= 0.01f)
            return desiredPosition;

        Vector3 direction = offset / distance;
        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            0.2f,
            direction,
            cameraHits,
            distance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            Transform hitTransform = cameraHits[i].transform;
            if (hitTransform == target.transform ||
                hitTransform.IsChildOf(target.transform))
                continue;

            nearestDistance = Mathf.Min(nearestDistance, cameraHits[i].distance - 0.2f);
        }

        return pivot + direction * Mathf.Max(0.35f, nearestDistance);
    }

    private void UpdateCockpitView()
    {
        Transform vehicle = target.transform;
        Vector3 desiredPosition = vehicle.TransformPoint(new Vector3(0f, 1.62f, 0.62f));
        Quaternion desiredRotation = vehicle.rotation;

        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime));
    }

    private void OnGUI()
    {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
        if (!Application.isMobilePlatform && !Application.isEditor)
            return;

        Color previous = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, 0.82f);

        Rect screenRect = new Rect(
            Screen.width * 0.82f,
            Screen.height * 0.05f,
            Screen.width * 0.15f,
            Screen.height * 0.11f);

        Rect guiRect = new Rect(
            screenRect.x,
            Screen.height - screenRect.y - screenRect.height,
            screenRect.width,
            screenRect.height);

        GUI.Box(guiRect, firstPerson ? "CAMERA\nDRIVER" : "CAMERA\nFOLLOW");
        GUI.color = previous;
#endif
    }
}

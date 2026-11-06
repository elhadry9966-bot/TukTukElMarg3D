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
    [SerializeField, Min(0f)] private float mobileSensitivity = 0.12f;
    [SerializeField] private LayerMask cameraCollisionMask = ~0;
    [Tooltip("The camera never gets closer than this to the tuk-tuk; when something blocks it, it rises instead.")]
    [SerializeField, Min(0.5f)] private float minimumFollowDistance = 3.5f;

    private const float CameraRadius = 0.2f;
    private const float AbsoluteMinimumDistance = 1.5f;
    private const float MaximumPitch = 65f;

    private readonly RaycastHit[] cameraHits = new RaycastHit[16];
    private Vector3 positionVelocity;
    private float orbitYaw;
    private float orbitPitch = 13f;
    private float currentPitch = 13f;
    private float currentDistance;
    private bool firstPerson;
    private bool snapToTarget = true;

    public bool IsFirstPerson => firstPerson;
    public TukTukController Target => target;

    public void SetTarget(TukTukController vehicle)
    {
        target = vehicle;
        snapToTarget = true;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Keyboard keyboard = Keyboard.current;
        bool togglePressed =
            (keyboard != null && keyboard.vKey.wasPressedThisFrame) ||
            target.MobileCameraTogglePressedThisFrame;
        if (togglePressed)
        {
            firstPerson = !firstPerson;
            snapToTarget = true;
        }

        if (firstPerson)
        {
            UpdateCockpitView();
            return;
        }

        UpdateOrbitInput();
        UpdateFollowView();
    }

    private void UpdateOrbitInput()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.rightButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            orbitYaw += delta.x * mouseSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch - delta.y * mouseSensitivity, -5f, 55f);
        }

        Vector2 mobileDelta = target.MobileCameraDelta;
        if (mobileDelta.sqrMagnitude > 0f)
        {
            orbitYaw += mobileDelta.x * mobileSensitivity;
            orbitPitch = Mathf.Clamp(orbitPitch - mobileDelta.y * mobileSensitivity, -5f, 55f);
        }
    }

    private void UpdateFollowView()
    {
        Transform vehicle = target.transform;
        Vector3 pivot = vehicle.position +
                        Vector3.up * followHeight +
                        vehicle.forward * 0.35f;
        float yaw = vehicle.eulerAngles.y + orbitYaw;

        // Find a pitch where the view behind the tuk-tuk is free. If a wall or
        // another object is in the way, the camera rises and looks down over it
        // instead of being squeezed inside the tuk-tuk.
        float bestPitch = orbitPitch;
        float bestDistance = -1f;
        for (int step = 0; step < 6; step++)
        {
            float pitch = Mathf.Min(orbitPitch + step * 10f, MaximumPitch);
            float free = FreeDistance(pivot, Quaternion.Euler(pitch, yaw, 0f));
            if (free > bestDistance)
            {
                bestDistance = free;
                bestPitch = pitch;
            }

            if (free >= minimumFollowDistance)
                break;
        }

        float desiredDistance = Mathf.Clamp(bestDistance, AbsoluteMinimumDistance, followDistance);

        if (snapToTarget)
        {
            currentPitch = bestPitch;
            currentDistance = desiredDistance;
        }
        else
        {
            currentPitch = Mathf.Lerp(currentPitch, bestPitch, 1f - Mathf.Exp(-8f * Time.deltaTime));
            currentDistance = desiredDistance < currentDistance
                ? desiredDistance
                : Mathf.Lerp(currentDistance, desiredDistance, 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        Quaternion orbit = Quaternion.Euler(currentPitch, yaw, 0f);
        Vector3 desiredPosition = pivot + orbit * (Vector3.back * currentDistance);
        desiredPosition.y = Mathf.Max(0.4f, desiredPosition.y);

        Vector3 lookPoint = pivot + vehicle.forward * 3.5f + Vector3.up * 0.25f;

        if (snapToTarget)
        {
            transform.position = desiredPosition;
            transform.rotation = Quaternion.LookRotation(lookPoint - desiredPosition);
            positionVelocity = Vector3.zero;
            snapToTarget = false;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref positionVelocity,
            positionSmoothTime);

        Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            desiredRotation,
            1f - Exp(rotationSmoothSpeed));
    }

    private static float Exp(float speed)
    {
        return Mathf.Exp(-speed * Time.deltaTime);
    }

    private float FreeDistance(Vector3 pivot, Quaternion orbit)
    {
        Vector3 direction = orbit * Vector3.back;
        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            CameraRadius,
            direction,
            cameraHits,
            followDistance,
            cameraCollisionMask,
            QueryTriggerInteraction.Ignore);

        float free = followDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = cameraHits[i];
            Collider hitCollider = hit.collider;
            if (hitCollider == null)
                continue;

            // Ignore the tuk-tuk itself.
            Transform hitTransform = hitCollider.transform;
            if (hitTransform == target.transform || hitTransform.IsChildOf(target.transform))
                continue;

            // A hit at distance 0 means the cast started inside this collider.
            if (hit.distance <= 0.001f)
                continue;

            // Low things (curbs, bins) below the camera line never block the view.
            if (hitCollider.bounds.max.y < pivot.y - 0.5f)
                continue;

            free = Mathf.Min(free, hit.distance - CameraRadius);
        }

        return Mathf.Max(0f, free);
    }

    private void UpdateCockpitView()
    {
        Transform vehicle = target.transform;
        Vector3 desiredPosition = vehicle.TransformPoint(new Vector3(0f, 1.62f, 0.62f));
        Quaternion desiredRotation = vehicle.rotation;

        if (snapToTarget)
        {
            transform.position = desiredPosition;
            transform.rotation = desiredRotation;
            snapToTarget = false;
            return;
        }

        float blend = 1f - Mathf.Exp(-rotationSmoothSpeed * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, desiredPosition, blend);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, blend);
    }
}

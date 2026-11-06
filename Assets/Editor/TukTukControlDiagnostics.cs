using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public static class TukTukControlDiagnostics
{
    [MenuItem("Tools/TukTuk El Marg/Diagnose Controls")]
    private static void DiagnoseControls()
    {
        int failures = 0;
        var scene = EditorSceneManager.GetActiveScene();
        TukTukController[] controllers = FindInScene<TukTukController>(scene);
        bool hasSingleController = controllers.Length == 1;
        Report("Driving script", hasSingleController,
            controllers.Length == 0
                ? $"No TukTukController exists in the active scene {scene.name}."
                : $"Found {controllers.Length} TukTukController component(s) in {scene.name}; expected exactly one.");
        if (!hasSingleController)
            failures++;

        foreach (TukTukController controller in controllers)
        {
            bool enabled = controller != null && controller.isActiveAndEnabled;
            Report($"Controller enabled ({controller.name})", enabled,
                enabled ? "Component and GameObject are enabled." : "Enable the component and its GameObject.");
            if (!enabled)
                failures++;

            Rigidbody body = controller != null ? controller.GetComponent<Rigidbody>() : null;
            bool bodyReady = body != null &&
                             body.gameObject.activeInHierarchy &&
                             body.detectCollisions &&
                             !body.isKinematic &&
                             (body.constraints & (RigidbodyConstraints.FreezePositionX |
                                                  RigidbodyConstraints.FreezePositionY |
                                                  RigidbodyConstraints.FreezePositionZ |
                                                  RigidbodyConstraints.FreezeRotationY)) == 0;
            Report($"Rigidbody ({controller.name})", bodyReady,
                body == null
                    ? "Missing Rigidbody on the controlled GameObject."
                    : $"GameObject active={body.gameObject.activeInHierarchy}, kinematic={body.isKinematic}, collisions={body.detectCollisions}, constraints={body.constraints}, mass={body.mass:0.0}, linear damping={body.linearDamping:0.00}, angular damping={body.angularDamping:0.00}, interpolation={body.interpolation}, collision detection={body.collisionDetectionMode}, velocity={body.linearVelocity}, position={body.position}");
            if (!bodyReady)
                failures++;

            BoxCollider collider = controller != null ? controller.GetComponent<BoxCollider>() : null;
            bool colliderReady = collider != null && collider.enabled && !collider.isTrigger;
            Report($"Collision collider ({controller.name})", colliderReady,
                collider == null
                    ? "Missing BoxCollider on the controlled GameObject."
                    : $"enabled={collider.enabled}, trigger={collider.isTrigger}");
            if (!colliderReady)
                failures++;

            if (EditorApplication.isPlaying && controller != null)
            {
                Report("Play Mode input readings", controller.KeyboardDetected,
                    $"keyboard={controller.KeyboardDetected}, W/Up={controller.ForwardKeyPressed}, S/Down={controller.ReverseKeyPressed}, A/Left={controller.LeftKeyPressed}, D/Right={controller.RightKeyPressed}, forward={controller.ThrottleInput:0.0}, steering={controller.SteeringInput:0.0}, speed={controller.CurrentSpeedKph:0.0} km/h");
                Debug.Log(
                    $"[TukTuk Controls] LIVE — Play Mode movement readings: applied movement Δv={controller.AppliedMovement} m/s, Rigidbody velocity={controller.RigidbodyVelocity} m/s, position={controller.RigidbodyPosition}, physics Δposition={controller.PhysicsStepDisplacement}, grounded={controller.IsGrounded}, brake={controller.IsBraking}, contact={controller.CollisionContact}, timeScale={Time.timeScale:0.00}, fixedDeltaTime={Time.fixedDeltaTime:0.000}",
                    controller);
            }
        }

        bool inputBackendAvailable = false;
        string inputMode = "None";
#if ENABLE_INPUT_SYSTEM
        inputBackendAvailable = true;
        inputMode = "Input System";
#elif ENABLE_LEGACY_INPUT_MANAGER
        inputBackendAvailable = true;
        inputMode = "Legacy Input Manager";
#endif
        string controllerBackend = controllers.Length > 0
            ? controllers[0].InputBackend
            : "unavailable";
        Report("Input mode compatibility", inputBackendAvailable,
            $"Compiled input backend: {inputMode}; controller backend: {controllerBackend}.");
        if (!inputBackendAvailable)
            failures++;

#if ENABLE_INPUT_SYSTEM
        bool inputSystemInitialized = InputSystem.settings != null;
        Report("Input System initialized", inputSystemInitialized,
            inputSystemInitialized ? "InputSystem.settings is available." : "Input System settings are unavailable.");
        if (!inputSystemInitialized)
            failures++;

        Keyboard keyboard = Keyboard.current;
        bool keyboardAvailable = keyboard != null;
        Report("Keyboard device", keyboardAvailable,
            keyboardAvailable
                ? $"Readable keyboard: {keyboard.displayName}. Focus the Game view before pressing controls."
                : "No active Keyboard device; check the Input System package and connected device.");
        if (!keyboardAvailable)
            failures++;
        else
        {
            Report("Keyboard state read",
                true,
                $"W={keyboard.wKey.isPressed}, A={keyboard.aKey.isPressed}, S={keyboard.sKey.isPressed}, D={keyboard.dKey.isPressed}, Space={keyboard.spaceKey.isPressed}, R={keyboard.rKey.isPressed}");
        }
#elif ENABLE_LEGACY_INPUT_MANAGER
        Report("Keyboard device", null,
            "Legacy Input Manager is active; physical keyboard availability is shown by the controller's live key readings in Play Mode.");
#else
        Report("Keyboard device", false, "Neither the Input System nor Legacy Input Manager is enabled.");
        failures++;
#endif

        Report("PlayerInput / Input Actions", true,
            "Not required: the single driving controller polls the active keyboard backend directly.");

        EditorWindow focusedWindow = EditorWindow.focusedWindow;
        bool gameViewFocused = focusedWindow != null &&
                               focusedWindow.GetType().Name == "GameView";
        Report("Game view focus", gameViewFocused ? (bool?)true : null,
            gameViewFocused
                ? "The Game view currently has focus."
                : $"Focused window: {(focusedWindow != null ? focusedWindow.GetType().Name : "none")}. Focus the Game view before pressing controls.");

        TukTukCamera[] cameras = FindInScene<TukTukCamera>(scene);
        bool cameraReferencesReady = true;
        foreach (TukTukCamera camera in cameras)
        {
            if (camera.Target == null)
            {
                cameraReferencesReady = false;
                break;
            }
        }

        Report("Camera references", cameraReferencesReady,
            cameras.Length == 0
                ? "No TukTukCamera component in this scene; no driving reference is missing."
                : cameraReferencesReady
                    ? $"All {cameras.Length} TukTukCamera target reference(s) are assigned."
                    : "A TukTukCamera has no target; assign the controlled vehicle in its Inspector.");
        if (!cameraReferencesReady)
            failures++;

        if (!EditorApplication.isPlaying)
        {
            Report("Play Mode motion test", null,
                "Not run. Enter Play Mode, focus the Game view, then press W/A/S/D and run this menu again.");
        }

        Debug.Log(failures == 0
            ? "TukTuk control diagnosis complete: no configuration failures found."
            : $"TukTuk control diagnosis complete: {failures} failure(s) found. See the failed checks above.");
    }

    private static T[] FindInScene<T>(UnityEngine.SceneManagement.Scene scene)
        where T : Component
    {
        var found = new System.Collections.Generic.List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
            found.AddRange(root.GetComponentsInChildren<T>(true));

        return found.ToArray();
    }

    private static void Report(string test, bool? passed, string details)
    {
        string status = passed.HasValue
            ? passed.Value ? "PASS" : "FAIL"
            : "NOT RUN";
        string message = $"[TukTuk Controls] {status} — {test}: {details}";

        if (passed == false)
            Debug.LogError(message);
        else if (passed == null)
            Debug.LogWarning(message);
        else
            Debug.Log(message);
    }
}

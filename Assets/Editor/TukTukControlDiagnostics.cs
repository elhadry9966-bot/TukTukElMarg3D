using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class TukTukControlDiagnostics
{
    [MenuItem("Tools/TukTuk El Marg/Diagnose Controls")]
    private static void DiagnoseControls()
    {
        int failures = 0;
        var scene = EditorSceneManager.GetActiveScene();
        TukTukController[] controllers = FindInScene<TukTukController>(scene);
        bool hasController = controllers.Length > 0;
        Report("Driving script", hasController,
            hasController
                ? $"Found {controllers.Length} TukTukController component(s) in {scene.name}."
                : $"No TukTukController exists in the active scene {scene.name}.");
        if (!hasController)
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
                    : $"GameObject active={body.gameObject.activeInHierarchy}, kinematic={body.isKinematic}, collisions={body.detectCollisions}, constraints={body.constraints}");
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
                Report("Play Mode movement values", true,
                    $"speed={controller.CurrentSpeedKph:0.0} km/h, forward={controller.ThrottleInput:0.0}, steering={controller.SteeringInput:0.0}");
            }
        }

        PropertyInfo inputModeProperty = typeof(PlayerSettings).GetProperty(
            "activeInputHandler",
            BindingFlags.Public | BindingFlags.Static);
        if (inputModeProperty == null)
        {
            Report("Input System mode", false, "Unity does not expose PlayerSettings.activeInputHandler.");
            failures++;
        }
        else
        {
            object inputMode = inputModeProperty.GetValue(null);
            int modeValue = Convert.ToInt32(inputMode);
            bool newInputEnabled = modeValue == 1 || modeValue == 2;
            Report("Input System mode", newInputEnabled,
                $"{inputMode} ({modeValue}); this controller uses UnityEngine.InputSystem.Keyboard.");
            if (!newInputEnabled)
                failures++;
        }

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

        Report("PlayerInput / Input Actions", true,
            "Not required: the single driving controller polls Keyboard.current directly.");

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

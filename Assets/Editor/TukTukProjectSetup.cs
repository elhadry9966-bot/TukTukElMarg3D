using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TukTukProjectSetup
{
    private const string SceneFolder = "Assets/Scenes";
    private const string ScenePath = SceneFolder + "/MainGame.unity";
    private const string MaterialFolder = "Assets/Materials/TukTukPrototype";

    private static readonly Dictionary<string, Material> Materials =
        new Dictionary<string, Material>();

    [MenuItem("Tools/TukTuk El Marg/Generate Prototype")]
    public static void GeneratePrototype()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        EnsureFolder("Assets/Materials");
        EnsureFolder(MaterialFolder);
        Materials.Clear();

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Single);
        GameObject root = new GameObject("TukTuk El Marg Prototype");

        ConfigureSceneLighting();
        CreateEnvironment(root.transform);
        TukTukController vehicle = CreateVehicle(root.transform);
        TukTukCamera followCamera = CreateCamera(root.transform, vehicle);
        CreateHud(root.transform, vehicle, followCamera);

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new InvalidOperationException($"Could not save the generated scene to {ScenePath}.");

        AddSceneToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeGameObject = root;
        Debug.Log($"TukTuk El Marg prototype generated at {ScenePath}.");
    }

    private static void ConfigureSceneLighting()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.56f, 0.67f, 0.78f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.5f, 0.42f);
        RenderSettings.ambientGroundColor = new Color(0.28f, 0.25f, 0.2f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.68f, 0.74f, 0.78f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 90f;
        RenderSettings.fogEndDistance = 190f;

        GameObject sunlight = new GameObject("Egyptian Afternoon Sun");
        Light light = sunlight.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.88f, 0.72f);
        light.intensity = 1.35f;
        sunlight.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
    }

    private static void CreateEnvironment(Transform root)
    {
        Transform environment = new GameObject("Street Environment").transform;
        environment.SetParent(root, false);

        Material asphalt = GetMaterial("Asphalt", new Color(0.16f, 0.18f, 0.19f), 0.05f);
        Material ground = GetMaterial("Desert Concrete", new Color(0.48f, 0.4f, 0.29f));
        Material curb = GetMaterial("Pale Curb", new Color(0.72f, 0.66f, 0.55f));
        Material line = GetMaterial("Road Markings", new Color(0.93f, 0.84f, 0.56f));
        Material[] facades =
        {
            GetMaterial("Sandstone", new Color(0.72f, 0.55f, 0.38f)),
            GetMaterial("Faded Peach", new Color(0.69f, 0.43f, 0.32f)),
            GetMaterial("Dusty Cream", new Color(0.79f, 0.7f, 0.54f)),
            GetMaterial("Warm Plaster", new Color(0.59f, 0.48f, 0.38f))
        };
        Material window = GetMaterial("Blue Window Glass", new Color(0.13f, 0.28f, 0.34f), 0.15f);
        Material door = GetMaterial("Shop Doors", new Color(0.25f, 0.19f, 0.14f));
        Material sign = GetMaterial("Shop Signs", new Color(0.12f, 0.3f, 0.24f));
        Material lampMetal = GetMaterial("Lamp Metal", new Color(0.17f, 0.19f, 0.18f), 0.65f);
        Material lampGlow = GetMaterial("Lamp Glow", new Color(1f, 0.72f, 0.32f), 0f, 0.2f, true);

        CreateBox("Ground", environment, new Vector3(0f, -0.35f, 0f),
            new Vector3(100f, 0.7f, 170f), ground);
        CreateBox("Main Asphalt Road", environment, new Vector3(0f, 0.025f, 0f),
            new Vector3(12f, 0.05f, 150f), asphalt);
        CreateBox("Cross Street", environment, new Vector3(0f, 0.028f, 19f),
            new Vector3(48f, 0.055f, 12f), asphalt);

        for (int side = -1; side <= 1; side += 2)
        {
            float sidewalkX = side * 7.35f;
            CreateBox($"Sidewalk {side}", environment, new Vector3(sidewalkX, 0.12f, 0f),
                new Vector3(2.7f, 0.24f, 150f), curb);
            CreateBox($"Cross Street Sidewalk {side}", environment,
                new Vector3(side * 13.35f, 0.12f, 19f),
                new Vector3(21.3f, 0.24f, 2.7f), curb);
        }

        for (int z = -68; z <= 68; z += 8)
        {
            if (z >= 11 && z <= 27)
                continue;

            CreateBox("Center Road Dash", environment, new Vector3(0f, 0.058f, z),
                new Vector3(0.12f, 0.015f, 3.2f), line, false);
        }

        for (int x = -17; x <= 17; x += 3)
        {
            if (Mathf.Abs(x) < 7)
                continue;

            CreateBox("Crosswalk Marking", environment, new Vector3(x, 0.06f, 12.2f),
                new Vector3(1.5f, 0.015f, 0.35f), line, false);
            CreateBox("Crosswalk Marking", environment, new Vector3(x, 0.06f, 25.8f),
                new Vector3(1.5f, 0.015f, 0.35f), line, false);
        }

        for (int i = 0, z = -61; z <= 61; i++, z += 17)
        {
            if (z >= 10 && z <= 27)
                continue;

            CreateShopfront(environment, 1, z, facades[i % facades.Length],
                window, door, sign);
            CreateShopfront(environment, -1, z + 7, facades[(i + 2) % facades.Length],
                window, door, sign);
        }

        for (int z = -56; z <= 56; z += 22)
        {
            if (z >= 10 && z <= 27)
                continue;

            CreateStreetLamp(environment, 1, z, lampMetal, lampGlow);
            CreateStreetLamp(environment, -1, z + 9, lampMetal, lampGlow);
        }
    }

    private static void CreateShopfront(
        Transform parent,
        int side,
        float z,
        Material facade,
        Material window,
        Material door,
        Material sign)
    {
        float centerX = side * 13f;
        CreateBox("Popular Apartment Building", parent, new Vector3(centerX, 4f, z),
            new Vector3(6f, 8f, 11f), facade);

        float frontX = side * 9.94f;
        CreateBox("Ground Floor Door", parent, new Vector3(frontX, 1.15f, z - 2.7f),
            new Vector3(0.08f, 2.3f, 1.25f), door, false);
        CreateBox("Shop Window", parent, new Vector3(frontX, 1.45f, z + 1.1f),
            new Vector3(0.08f, 1.65f, 2.4f), window, false);
        CreateBox("Shop Signboard", parent, new Vector3(side * 9.82f, 3.05f, z),
            new Vector3(0.22f, 0.65f, 4.2f), sign, false);

        CreateSignText(parent, side, new Vector3(side * 9.68f, 3.05f, z),
            new[] { "قهوة المَرْج", "بقالة الحارة", "كشري البلد", "مخبز شعبي" }[
                Mathf.Abs(Mathf.RoundToInt(z)) % 4]);

        for (int level = 0; level < 2; level++)
        {
            float y = 4.7f + level * 2.25f;
            for (int offset = -1; offset <= 1; offset += 2)
            {
                CreateBox("Apartment Window", parent,
                    new Vector3(frontX, y, z + offset * 1.65f),
                    new Vector3(0.08f, 1.05f, 1.15f), window, false);
                CreateBox("Window Sill", parent,
                    new Vector3(side * 9.83f, y - 0.58f, z + offset * 1.65f),
                    new Vector3(0.22f, 0.12f, 1.3f), door, false);
            }
        }
    }

    private static void CreateSignText(Transform parent, int side, Vector3 position, string label)
    {
        GameObject signObject = new GameObject("Arabic Shop Name");
        signObject.transform.SetParent(parent, false);
        signObject.transform.localPosition = position;
        signObject.transform.localRotation = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
        TextMesh text = signObject.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.32f;
        text.fontSize = 36;
        text.color = new Color(1f, 0.91f, 0.66f);
    }

    private static void CreateStreetLamp(
        Transform parent,
        int side,
        float z,
        Material metal,
        Material glow)
    {
        float x = side * 8.6f;
        CreateCylinder("Streetlight Pole", parent, new Vector3(x, 2.7f, z),
            new Vector3(0.12f, 2.7f, 0.12f), metal);
        CreateBox("Streetlight Arm", parent, new Vector3(x - side * 0.42f, 5.2f, z),
            new Vector3(0.9f, 0.1f, 0.12f), metal, false);
        CreateBox("Streetlight Head", parent, new Vector3(x - side * 0.82f, 5.12f, z),
            new Vector3(0.42f, 0.16f, 0.3f), glow, false);

        if (Mathf.Abs(z % 44f) < 1f)
        {
            GameObject lamp = new GameObject("Streetlight");
            lamp.transform.SetParent(parent, false);
            lamp.transform.localPosition = new Vector3(x - side * 0.82f, 5f, z);
            Light light = lamp.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.75f, 0.45f);
            light.intensity = 1.2f;
            light.range = 10f;
        }
    }

    private static TukTukController CreateVehicle(Transform root)
    {
        Transform vehicleRoot = new GameObject("Egyptian TukTuk").transform;
        vehicleRoot.SetParent(root, false);
        vehicleRoot.localPosition = new Vector3(0f, 0f, -14f);

        Material body = GetMaterial("TukTuk Egyptian Teal", new Color(0.04f, 0.39f, 0.31f), 0.28f);
        Material trim = GetMaterial("TukTuk Warm Ivory", new Color(0.88f, 0.79f, 0.59f), 0.2f);
        Material black = GetMaterial("TukTuk Rubber", new Color(0.055f, 0.06f, 0.06f));
        Material seat = GetMaterial("TukTuk Vinyl", new Color(0.29f, 0.12f, 0.08f));
        Material glass = GetMaterial(
            "TukTuk Blue Glass",
            new Color(0.18f, 0.37f, 0.43f, 0.24f),
            0.35f,
            0.35f,
            false,
            true);
        Material chrome = GetMaterial("TukTuk Chrome", new Color(0.62f, 0.65f, 0.62f), 0.75f);
        Material headlight = GetMaterial("TukTuk Headlights", new Color(1f, 0.84f, 0.49f), 0f, 0.2f, true);
        Material tailLight = GetMaterial("TukTuk Tail Lights", new Color(0.72f, 0.035f, 0.02f), 0f, 0.2f, true);

        GameObject vehicleObject = vehicleRoot.gameObject;
        Rigidbody bodyPhysics = vehicleObject.AddComponent<Rigidbody>();
        bodyPhysics.mass = 650f;
        bodyPhysics.linearDamping = 0.45f;
        bodyPhysics.angularDamping = 4f;
        bodyPhysics.interpolation = RigidbodyInterpolation.Interpolate;
        bodyPhysics.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        bodyPhysics.constraints = RigidbodyConstraints.FreezeRotationX |
                                  RigidbodyConstraints.FreezeRotationZ;
        bodyPhysics.centerOfMass = new Vector3(0f, -0.42f, 0f);
        BoxCollider bodyCollider = vehicleObject.AddComponent<BoxCollider>();
        bodyCollider.center = new Vector3(0f, 0.58f, 0f);
        bodyCollider.size = new Vector3(1.38f, 1f, 2.55f);
        TukTukController controller = vehicleObject.AddComponent<TukTukController>();

        CreateBox("Lower Chassis", vehicleRoot, new Vector3(0f, 0.55f, -0.02f),
            new Vector3(1.42f, 0.48f, 2.55f), body, false);
        CreateBox("Front Nose", vehicleRoot, new Vector3(0f, 0.88f, 1.04f),
            new Vector3(1.36f, 0.57f, 0.56f), trim, false);
        CreateBox("Front Teal Fascia", vehicleRoot, new Vector3(0f, 0.76f, 1.34f),
            new Vector3(1.25f, 0.27f, 0.08f), body, false);

        CreateBox("Driver Footwell", vehicleRoot, new Vector3(0f, 0.82f, 0.62f),
            new Vector3(1.34f, 0.24f, 0.92f), black, false);
        CreateBox("Driver Seat Cushion", vehicleRoot, new Vector3(0f, 1.08f, 0.1f),
            new Vector3(0.78f, 0.2f, 0.65f), seat, false);
        CreateBox("Driver Seat Back", vehicleRoot, new Vector3(0f, 1.47f, -0.18f),
            new Vector3(0.78f, 0.72f, 0.18f), seat, false);
        CreateBox("Rear Passenger Bench", vehicleRoot, new Vector3(0f, 1.03f, -0.82f),
            new Vector3(1.18f, 0.2f, 0.54f), seat, false);
        CreateBox("Rear Passenger Backrest", vehicleRoot, new Vector3(0f, 1.38f, -1.05f),
            new Vector3(1.18f, 0.58f, 0.17f), seat, false);

        CreateBox("Roof", vehicleRoot, new Vector3(0f, 2.18f, -0.02f),
            new Vector3(1.65f, 0.18f, 2.72f), trim, false);
        CreateBox("Windscreen", vehicleRoot, new Vector3(0f, 1.64f, 0.82f),
            new Vector3(1.27f, 0.82f, 0.06f), glass, false);
        CreateBox("Rear Passenger Canopy", vehicleRoot, new Vector3(0f, 1.66f, -0.82f),
            new Vector3(1.36f, 0.9f, 0.07f), body, false);

        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 0.7f;
            CreateBox("Cabin Front Pillar", vehicleRoot, new Vector3(x, 1.66f, 0.81f),
                new Vector3(0.1f, 1.02f, 0.12f), body, false);
            CreateBox("Cabin Rear Pillar", vehicleRoot, new Vector3(x, 1.66f, -0.73f),
                new Vector3(0.1f, 1.02f, 0.12f), body, false);
            CreateBox("Open Cabin Side Rail", vehicleRoot, new Vector3(x, 1.2f, 0.02f),
                new Vector3(0.1f, 0.12f, 1.28f), trim, false);
            CreateWheel(vehicleRoot, new Vector3(side * 0.74f, 0.43f, -0.8f), black, chrome);
        }
        CreateWheel(vehicleRoot, new Vector3(0f, 0.43f, 1.0f), black, chrome);

        for (int side = -1; side <= 1; side += 2)
        {
            CreateBox("Front Headlamp", vehicleRoot,
                new Vector3(side * 0.47f, 0.99f, 1.37f),
                new Vector3(0.28f, 0.18f, 0.09f), headlight, false);
            CreateBox("Rear Tail Lamp", vehicleRoot,
                new Vector3(side * 0.5f, 0.78f, -1.32f),
                new Vector3(0.2f, 0.24f, 0.08f), tailLight, false);
            CreateBox("Front Fender", vehicleRoot,
                new Vector3(side * 0.73f, 0.73f, 0.98f),
                new Vector3(0.14f, 0.16f, 0.88f), body, false);
        }

        CreateSteeringWheel(vehicleRoot, chrome, black);
        CreateBox("Rear Number Plate", vehicleRoot, new Vector3(0f, 0.72f, -1.37f),
            new Vector3(0.54f, 0.2f, 0.05f), trim, false);

        return controller;
    }

    private static void CreateWheel(Transform parent, Vector3 position, Material rubber, Material chrome)
    {
        GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        wheel.name = "Three-Wheel Rickshaw Tire";
        wheel.transform.SetParent(parent, false);
        wheel.transform.localPosition = position;
        wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        wheel.transform.localScale = new Vector3(0.22f, 0.42f, 0.42f);
        wheel.GetComponent<Renderer>().sharedMaterial = rubber;
        UnityEngine.Object.DestroyImmediate(wheel.GetComponent<Collider>());

        GameObject hub = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        hub.name = "Wheel Hub";
        hub.transform.SetParent(wheel.transform, false);
        hub.transform.localPosition = new Vector3(0f, 0f, 0f);
        hub.transform.localRotation = Quaternion.identity;
        hub.transform.localScale = new Vector3(1.04f, 0.38f, 0.38f);
        hub.GetComponent<Renderer>().sharedMaterial = chrome;
        UnityEngine.Object.DestroyImmediate(hub.GetComponent<Collider>());
    }

    private static void CreateSteeringWheel(Transform parent, Material chrome, Material grip)
    {
        Vector3 center = new Vector3(-0.3f, 1.34f, 0.43f);
        const int segments = 12;
        const float radius = 0.23f;
        for (int i = 0; i < segments; i++)
        {
            float firstAngle = i * Mathf.PI * 2f / segments;
            float nextAngle = (i + 1) * Mathf.PI * 2f / segments;
            Vector3 first = center + new Vector3(Mathf.Cos(firstAngle), Mathf.Sin(firstAngle), 0f) * radius;
            Vector3 next = center + new Vector3(Mathf.Cos(nextAngle), Mathf.Sin(nextAngle), 0f) * radius;
            CreateBeam("Steering Wheel Rim", parent, first, next, 0.035f, grip);
        }

        CreateBeam("Steering Wheel Spoke", parent, center,
            center + Vector3.up * radius * 0.85f, 0.025f, chrome);
        CreateBeam("Steering Wheel Spoke", parent, center,
            center + Vector3.right * radius * 0.85f, 0.025f, chrome);
        CreateBeam("Steering Column", parent, center,
            center + new Vector3(0.12f, -0.18f, 0.1f), 0.045f, chrome);
    }

    private static TukTukCamera CreateCamera(Transform root, TukTukController vehicle)
    {
        GameObject cameraObject = new GameObject("Main Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(root, false);
        cameraObject.transform.position = new Vector3(0f, 4f, -22f);
        cameraObject.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 68f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 220f;
        cameraObject.AddComponent<AudioListener>();
        TukTukCamera followCamera = cameraObject.AddComponent<TukTukCamera>();
        followCamera.SetTarget(vehicle);
        return followCamera;
    }

    private static void CreateHud(Transform root, TukTukController vehicle, TukTukCamera vehicleCamera)
    {
        GameObject canvasObject = new GameObject("Game HUD");
        canvasObject.transform.SetParent(root, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObject.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Driving Information");
        panel.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.anchoredPosition = new Vector2(24f, -24f);
        panelRect.sizeDelta = new Vector2(480f, 228f);
        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.035f, 0.055f, 0.05f, 0.86f);

        Text title = CreateHudText(panelRect, "Game Title", "TUKTUK EL MARG",
            new Vector2(18f, -16f), new Vector2(440f, 40f), 29, TextAnchor.MiddleLeft);
        title.color = new Color(0.96f, 0.78f, 0.38f);
        Text speed = CreateHudText(panelRect, "Speed", "السرعة: 0 كم/س",
            new Vector2(18f, -64f), new Vector2(440f, 38f), 25, TextAnchor.MiddleLeft);
        Text balance = CreateHudText(panelRect, "Starting Balance", "الرصيد: 100 جنيه",
            new Vector2(18f, -105f), new Vector2(440f, 34f), 21, TextAnchor.MiddleLeft);
        balance.color = new Color(0.56f, 0.88f, 0.62f);
        Text controls = CreateHudText(panelRect, "Controls",
            "W/S قيادة  |  A/D توجيه  |  SPACE فرامل\nR إعادة ضبط  |  V منظور السائق\nاسحب بزر الفأرة الأيمن لتحريك الكاميرا",
            new Vector2(18f, -143f), new Vector2(450f, 78f), 17, TextAnchor.UpperLeft);
        controls.horizontalOverflow = HorizontalWrapMode.Wrap;
        Text cameraMode = CreateHudText(panelRect, "Camera Mode", "الكاميرا: خلفية",
            new Vector2(18f, -207f), new Vector2(440f, 28f), 16, TextAnchor.MiddleLeft);
        cameraMode.color = new Color(0.72f, 0.8f, 0.76f);

        TukTukHUD hud = canvasObject.AddComponent<TukTukHUD>();
        hud.Configure(vehicle, vehicleCamera, speed, cameraMode);
    }

    private static Text CreateHudText(
        RectTransform parent,
        string objectName,
        string content,
        Vector2 position,
        Vector2 size,
        int fontSize,
        TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = content;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private static void CreateBeam(
        string objectName,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float radius,
        Material material)
    {
        Vector3 difference = end - start;
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beam.name = objectName;
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = (start + end) * 0.5f;
        beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, difference.normalized);
        beam.transform.localScale = new Vector3(radius * 2f, difference.magnitude * 0.5f, radius * 2f);
        beam.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(beam.GetComponent<Collider>());
    }

    private static GameObject CreateBox(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material material,
        bool hasCollider = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = position;
        box.transform.localScale = scale;
        box.GetComponent<Renderer>().sharedMaterial = material;
        if (!hasCollider)
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        return box;
    }

    private static GameObject CreateCylinder(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 scale,
        Material material)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = position;
        cylinder.transform.localScale = scale;
        cylinder.GetComponent<Renderer>().sharedMaterial = material;
        return cylinder;
    }

    private static Material GetMaterial(
        string name,
        Color color,
        float metallic = 0f,
        float smoothness = 0.35f,
        bool emissive = false,
        bool transparent = false)
    {
        if (Materials.TryGetValue(name, out Material cached))
            return cached;

        string path = $"{MaterialFolder}/{name.Replace(' ', '_')}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("No supported Lit/Standard shader was found.");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            Color emissionColor = color * 1.5f;
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emissionColor);
            material.EnableKeyword("_EMISSION");
        }
        if (transparent)
            SetTransparent(material);

        EditorUtility.SetDirty(material);
        Materials.Add(name, material);
        return material;
    }

    private static void SetTransparent(Material material)
    {
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separator = path.LastIndexOf('/');
        string parent = path.Substring(0, separator);
        string folderName = path.Substring(separator + 1);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, folderName);
    }

    private static void AddSceneToBuildSettings(string scenePath)
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(scene => scene.path == scenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}

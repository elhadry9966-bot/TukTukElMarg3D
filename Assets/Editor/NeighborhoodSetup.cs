using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class NeighborhoodSetup
{
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const string BackupFolder = "Assets/Scenes/Backups";
    private const string BackupPath = BackupFolder + "/MainGame_BeforeNeighborhood.unity";
    private const string MaterialsRoot = "Assets/Materials";

    [MenuItem("Tools/TukTuk El Marg/Generate Neighborhood")]
    public static void GenerateNeighborhood()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        EnsureProjectFolders();
        CreateSceneBackupIfNeeded();

        Scene scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        TukTukController vehicle = UnityEngine.Object.FindFirstObjectByType<TukTukController>();
        if (vehicle == null)
            throw new InvalidOperationException(
                "MainGame is missing TukTukController; the existing vehicle was left untouched.");

        TukTukCamera followCamera = UnityEngine.Object.FindFirstObjectByType<TukTukCamera>();
        if (followCamera == null)
            throw new InvalidOperationException(
                "MainGame is missing TukTukCamera; restore the prototype camera before generating the map.");

        Transform prototypeRoot = vehicle.transform.root;
        Transform oldEnvironment = prototypeRoot.Find("Street Environment");
        if (oldEnvironment != null)
            UnityEngine.Object.DestroyImmediate(oldEnvironment.gameObject);

        NeighborhoodGenerator neighborhood =
            UnityEngine.Object.FindFirstObjectByType<NeighborhoodGenerator>();
        if (neighborhood == null)
        {
            GameObject mapRoot = new GameObject("Neighborhood Map");
            neighborhood = mapRoot.AddComponent<NeighborhoodGenerator>();
        }

        RoadGenerator roads = GetOrAdd(neighborhood.gameObject, neighborhood.Roads);
        BuildingGenerator buildings = GetOrAdd(neighborhood.gameObject, neighborhood.Buildings);
        ShopGenerator shops = GetOrAdd(neighborhood.gameObject, neighborhood.Shops);
        LandmarkGenerator landmarks = GetOrAdd(neighborhood.gameObject, neighborhood.Landmarks);
        StreetSignGenerator streetSigns =
            GetOrAdd(neighborhood.gameObject, neighborhood.StreetSigns);
        neighborhood.Configure(roads, buildings, shops, landmarks, streetSigns);
        ValidateRoadNetwork(roads);

        ConfigureMapMaterials(roads, buildings, shops, landmarks, streetSigns);

        vehicle.transform.position = new Vector3(0f, 0f, -20f);
        vehicle.transform.rotation = Quaternion.identity;
        followCamera.SetTarget(vehicle);
        if (followCamera.Target != vehicle)
            throw new InvalidOperationException("The follow camera is not bound to the existing tuk-tuk.");

        neighborhood.Generate();
        ValidateGeneratedMap(neighborhood, roads, vehicle.transform.position);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene, ScenePath))
            throw new IOException($"Could not save the generated neighborhood to {ScenePath}.");

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = neighborhood.gameObject;
        Debug.Log(
            $"Neighborhood generated in {ScenePath}. " +
            $"The existing tuk-tuk and camera were preserved; backup: {BackupPath}.");
    }

    private static void ValidateRoadNetwork(RoadGenerator roads)
    {
        NeighborhoodRoad[] allRoads = roads.Roads;
        if (allRoads == null || allRoads.Length < 2)
            throw new InvalidOperationException("The neighborhood needs at least two connected roads.");

        for (int i = 0; i < allRoads.Length; i++)
        {
            NeighborhoodRoad road = allRoads[i];
            bool connected = false;
            for (int j = 0; j < allRoads.Length; j++)
            {
                NeighborhoodRoad crossing = allRoads[j];
                if (i == j || road.runsAlongX == crossing.runsAlongX)
                    continue;

                NeighborhoodRoad horizontal = road.runsAlongX ? road : crossing;
                NeighborhoodRoad vertical = road.runsAlongX ? crossing : road;
                bool withinRoadBounds =
                    Mathf.Abs(vertical.center.x - horizontal.center.x) <=
                    horizontal.length * 0.5f + vertical.width * 0.5f &&
                    Mathf.Abs(horizontal.center.z - vertical.center.z) <=
                    vertical.length * 0.5f + horizontal.width * 0.5f;
                if (withinRoadBounds)
                {
                    connected = true;
                    break;
                }
            }

            if (!connected)
                throw new InvalidOperationException(
                    $"Road '{road.name}' does not intersect another road. " +
                    "Adjust its position or length in the Neighborhood Map Inspector.");
        }
    }

    private static void ValidateGeneratedMap(
        NeighborhoodGenerator neighborhood,
        RoadGenerator roads,
        Vector3 vehiclePosition)
    {
        Transform generated = neighborhood.transform.Find("Generated Neighborhood");
        if (generated == null)
            throw new InvalidOperationException("The generated map root is missing from the scene.");

        bool spawnIsOnRoad = false;
        for (int i = 0; i < roads.Roads.Length; i++)
        {
            NeighborhoodRoad road = roads.Roads[i];
            if (road.runsAlongX)
            {
                spawnIsOnRoad |= Mathf.Abs(vehiclePosition.z - road.center.z) <= road.width * 0.5f &&
                                 Mathf.Abs(vehiclePosition.x - road.center.x) <= road.length * 0.5f;
            }
            else
            {
                spawnIsOnRoad |= Mathf.Abs(vehiclePosition.x - road.center.x) <= road.width * 0.5f &&
                                 Mathf.Abs(vehiclePosition.z - road.center.z) <= road.length * 0.5f;
            }
        }

        if (!spawnIsOnRoad)
            throw new InvalidOperationException("The tuk-tuk start position is not on a road.");

        Collider[] colliders = generated.GetComponentsInChildren<Collider>(true);
        if (colliders.Length < roads.Roads.Length + 4)
            throw new InvalidOperationException(
                $"The generated roads/buildings are missing colliders ({colliders.Length} found).");
    }

    private static void ConfigureMapMaterials(
        RoadGenerator roads,
        BuildingGenerator buildings,
        ShopGenerator shops,
        LandmarkGenerator landmarks,
        StreetSignGenerator streetSigns)
    {
        Material asphalt = GetMaterial("Roads", "Egyptian Asphalt", new Color(0.13f, 0.15f, 0.16f), 0.04f);
        Material sidewalk = GetMaterial("Roads", "Dusty Limestone", new Color(0.66f, 0.58f, 0.45f));
        Material markings = GetMaterial("Roads", "Faded Warm Road Paint", new Color(0.88f, 0.78f, 0.55f));
        Material pole = GetMaterial("StreetProps", "Concrete Utility Grey", new Color(0.42f, 0.41f, 0.37f));
        Material lamps = GetMaterial("StreetProps", "Amber Lamp Glass", new Color(1f, 0.73f, 0.36f), 0f, 0.25f, true);
        Material parkedPaint = GetMaterial("StreetProps", "Parked Vehicle Teal", new Color(0.12f, 0.38f, 0.34f), 0.3f);
        Material vehicleGlass = GetMaterial("StreetProps", "Parked Vehicle Glass", new Color(0.12f, 0.24f, 0.29f), 0.2f);
        Material rubber = GetMaterial("StreetProps", "Vehicle Rubber", new Color(0.045f, 0.05f, 0.05f));
        Material metal = GetMaterial("StreetProps", "Aged Street Metal", new Color(0.34f, 0.35f, 0.32f), 0.55f);

        Material[] facades =
        {
            GetMaterial("Buildings", "Old Cairo Sandstone", new Color(0.67f, 0.49f, 0.34f)),
            GetMaterial("Buildings", "Faded Rose Plaster", new Color(0.65f, 0.41f, 0.34f)),
            GetMaterial("Buildings", "Warm Limestone", new Color(0.78f, 0.69f, 0.53f)),
            GetMaterial("Buildings", "Weathered Grey Plaster", new Color(0.55f, 0.52f, 0.46f))
        };
        Material buildingTrim = GetMaterial("Buildings", "Painted Concrete Trim", new Color(0.84f, 0.76f, 0.63f));
        Material windows = GetMaterial("Buildings", "Deep Blue Window Glass", new Color(0.13f, 0.28f, 0.34f), 0.14f);
        Material timber = GetMaterial("Buildings", "Dark Apartment Doors", new Color(0.24f, 0.15f, 0.095f));
        Material roof = GetMaterial("Buildings", "Rooftop Concrete", new Color(0.43f, 0.4f, 0.35f));
        Material tanks = GetMaterial("Buildings", "Water Tank Blue", new Color(0.12f, 0.31f, 0.39f));
        Material[] shopSigns =
        {
            GetMaterial("Shops", "Shop Sign Teal", new Color(0.055f, 0.28f, 0.24f), 0.15f, 0.4f, true),
            GetMaterial("Shops", "Shop Sign Brick Red", new Color(0.48f, 0.13f, 0.075f)),
            GetMaterial("Shops", "Shop Sign Deep Blue", new Color(0.12f, 0.19f, 0.34f)),
            GetMaterial("Shops", "Shop Sign Olive Green", new Color(0.28f, 0.33f, 0.12f))
        };
        Material shopFrame = GetMaterial("Shops", "Painted Shop Frames", new Color(0.7f, 0.58f, 0.39f));
        Material shopGlass = GetMaterial("Shops", "Shop Display Glass", new Color(0.12f, 0.29f, 0.32f), 0.12f);
        Material shopWood = GetMaterial("Shops", "Market Wood", new Color(0.32f, 0.2f, 0.11f));
        Material shutters = GetMaterial("Shops", "Roller Shutters", new Color(0.38f, 0.4f, 0.38f), 0.32f);
        Material shopAccent = GetMaterial("Shops", "Awning and Crate Colors", new Color(0.63f, 0.22f, 0.1f));
        Material signBoard = GetMaterial("StreetProps", "Street Sign Green", new Color(0.08f, 0.27f, 0.21f));

        roads.ConfigureMaterials(
            asphalt, sidewalk, markings, pole, lamps,
            parkedPaint, vehicleGlass, rubber, metal);
        buildings.ConfigureMaterials(
            facades, buildingTrim, windows, timber, roof, metal, tanks);
        shops.ConfigureMaterials(
            shopSigns, shopFrame, shopGlass, shopWood, shutters, shopAccent);
        landmarks.ConfigureMaterials(
            facades, buildingTrim, windows, roof, shopSigns[0], timber);
        streetSigns.ConfigureMaterials(signBoard, pole);
    }

    private static T GetOrAdd<T>(GameObject owner, T component) where T : Component
    {
        return component != null ? component : owner.AddComponent<T>();
    }

    private static void CreateSceneBackupIfNeeded()
    {
        if (!File.Exists(ScenePath))
            throw new FileNotFoundException("The existing MainGame scene was not found.", ScenePath);

        EnsureFolder(BackupFolder);
        if (!File.Exists(BackupPath) && !AssetDatabase.CopyAsset(ScenePath, BackupPath))
            throw new IOException($"Could not create the pre-neighborhood scene backup at {BackupPath}.");

        AssetDatabase.ImportAsset(
            BackupPath,
            ImportAssetOptions.ForceSynchronousImport);
        if (!File.Exists(BackupPath))
            throw new IOException($"The scene backup does not exist: {BackupPath}.");
    }

    private static void EnsureProjectFolders()
    {
        string[] folders =
        {
            "Assets/Scripts/Map",
            "Assets/Prefabs/Buildings",
            "Assets/Prefabs/Shops",
            "Assets/Prefabs/Roads",
            "Assets/Prefabs/Landmarks",
            "Assets/Prefabs/StreetProps",
            MaterialsRoot,
            MaterialsRoot + "/Roads",
            MaterialsRoot + "/Buildings",
            MaterialsRoot + "/Shops",
            MaterialsRoot + "/StreetProps",
            "Assets/Scenes",
            "Assets/Scenes/Backups",
            "Assets/Reference"
        };

        for (int i = 0; i < folders.Length; i++)
            EnsureFolder(folders[i]);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folderName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(parent) || !AssetDatabase.IsValidFolder(parent))
            throw new DirectoryNotFoundException(
                $"Parent folder for '{path}' is missing from the Unity asset database.");

        string guid = AssetDatabase.CreateFolder(parent, folderName);
        if (string.IsNullOrEmpty(guid))
            throw new IOException($"Could not create Unity folder '{path}'.");
    }

    private static Material GetMaterial(
        string category,
        string name,
        Color color,
        float metallic = 0f,
        float smoothness = 0.32f,
        bool emissive = false)
    {
        string folder = MaterialsRoot + "/" + category;
        string path = folder + "/" + name.Replace(' ', '_') + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException(
                    $"No supported lit shader is available for material '{name}'.");

            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (emissive)
        {
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", color * 1.35f);
            material.EnableKeyword("_EMISSION");
        }

        EditorUtility.SetDirty(material);
        return material;
    }
}

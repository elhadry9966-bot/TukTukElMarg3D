using System;
using UnityEngine;

public enum NeighborhoodLandmarkType
{
    GirlsSecondarySchool,
    PoliceStation,
    PrimarySchool,
    Mosque
}

[Serializable]
public sealed class LandmarkDefinition
{
    public string arabicName;
    public NeighborhoodLandmarkType type;
    public Vector3 position;
    public Vector2 footprint = new Vector2(17f, 11f);
    [Min(3f)] public float height = 8f;
}

public sealed class LandmarkGenerator : MonoBehaviour
{
    [Header("Original neighborhood landmarks — approximate placement")]
    [SerializeField] private LandmarkDefinition[] definitions =
    {
        new LandmarkDefinition
        {
            arabicName = "مدرسة المرج الثانوية بنات",
            type = NeighborhoodLandmarkType.GirlsSecondarySchool,
            position = new Vector3(-30f, 0f, 10f),
            footprint = new Vector2(20f, 10f),
            height = 9f
        },
        new LandmarkDefinition
        {
            arabicName = "قسم شرطة المرج",
            type = NeighborhoodLandmarkType.PoliceStation,
            position = new Vector3(30f, 0f, -30f),
            footprint = new Vector2(18f, 10f),
            height = 8f
        },
        new LandmarkDefinition
        {
            arabicName = "مدرسة جمال عبد الناصر الابتدائية",
            type = NeighborhoodLandmarkType.PrimarySchool,
            position = new Vector3(-18f, 0f, 30f),
            footprint = new Vector2(20f, 10f),
            height = 8f
        },
        new LandmarkDefinition
        {
            arabicName = "مسجد الرحمن",
            type = NeighborhoodLandmarkType.Mosque,
            position = new Vector3(30f, 0f, 50f),
            footprint = new Vector2(18f, 11f),
            height = 11f
        }
    };

    [SerializeField] private Material[] facadeMaterials = new Material[4];
    [SerializeField] private Material trimMaterial;
    [SerializeField] private Material glassMaterial;
    [SerializeField] private Material roofMaterial;
    [SerializeField] private Material accentMaterial;
    [SerializeField] private Material timberMaterial;

    public LandmarkDefinition[] Definitions => definitions;

    public void ConfigureMaterials(
        Material[] facades,
        Material trim,
        Material glass,
        Material roof,
        Material accent,
        Material timber)
    {
        facadeMaterials = facades;
        trimMaterial = trim;
        glassMaterial = glass;
        roofMaterial = roof;
        accentMaterial = accent;
        timberMaterial = timber;
    }

    public void Generate(Transform output)
    {
        for (int i = 0; i < definitions.Length; i++)
        {
            LandmarkDefinition definition = definitions[i];
            switch (definition.type)
            {
                case NeighborhoodLandmarkType.GirlsSecondarySchool:
                    CreateInstitution(
                        output,
                        definition,
                        facadeMaterials != null && facadeMaterials.Length > 0
                            ? facadeMaterials[0]
                            : roofMaterial,
                        i,
                        "Secondary school");
                    break;
                case NeighborhoodLandmarkType.PoliceStation:
                    CreateInstitution(
                        output,
                        definition,
                        accentMaterial,
                        i,
                        "Neighborhood police station");
                    break;
                case NeighborhoodLandmarkType.PrimarySchool:
                    CreateInstitution(
                        output,
                        definition,
                        facadeMaterials != null && facadeMaterials.Length > 1
                            ? facadeMaterials[1]
                            : roofMaterial,
                        i,
                        "Primary school");
                    break;
                case NeighborhoodLandmarkType.Mosque:
                    CreateMosque(output, definition, i);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    private void CreateInstitution(
        Transform output,
        LandmarkDefinition definition,
        Material bodyMaterial,
        int index,
        string buildingType)
    {
        GameObject building = new GameObject($"{buildingType} — {definition.arabicName}");
        building.transform.SetParent(output, false);
        building.transform.localPosition = definition.position;
        NeighborhoodGeometry.MarkStatic(building);

        NeighborhoodGeometry.Box(
            "Solid landmark building — collision",
            building.transform,
            new Vector3(0f, definition.height * 0.5f, 0f),
            new Vector3(definition.footprint.x, definition.height, definition.footprint.y),
            bodyMaterial,
            true);
        NeighborhoodGeometry.Box(
            "Landmark entrance canopy",
            building.transform,
            new Vector3(0f, 2.65f, definition.footprint.y * 0.5f + 0.7f),
            new Vector3(5.2f, 0.38f, 1.7f),
            trimMaterial);
        NeighborhoodGeometry.Box(
            "Landmark doorway",
            building.transform,
            new Vector3(0f, 1.35f, definition.footprint.y * 0.5f + 0.12f),
            new Vector3(2.2f, 2.7f, 0.2f),
            timberMaterial);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int floor = 0; floor < 2; floor++)
            {
                NeighborhoodGeometry.Box(
                    "Landmark window",
                    building.transform,
                    new Vector3(side * 5.2f, 2.7f + floor * 3f, definition.footprint.y * 0.5f + 0.12f),
                    new Vector3(2.1f, 1.5f, 0.14f),
                    glassMaterial);
                NeighborhoodGeometry.Box(
                    "Colored window lintel",
                    building.transform,
                    new Vector3(side * 5.2f, 3.5f + floor * 3f, definition.footprint.y * 0.5f + 0.2f),
                    new Vector3(2.35f, 0.12f, 0.12f),
                    index % 2 == 0 ? accentMaterial : trimMaterial);
            }

            NeighborhoodGeometry.Box(
                "Landmark front column",
                building.transform,
                new Vector3(side * 4.4f, 2f, definition.footprint.y * 0.5f + 0.8f),
                new Vector3(0.35f, 4f, 0.35f),
                trimMaterial);
        }

        NeighborhoodGeometry.Box(
            "Flat institutional roof",
            building.transform,
            new Vector3(0f, definition.height + 0.12f, 0f),
            new Vector3(definition.footprint.x + 0.4f, 0.24f, definition.footprint.y + 0.4f),
            roofMaterial);
        AddNameSign(building.transform, definition);
        NeighborhoodGeometry.CombineVisuals(building.transform);
        CreateForecourt(output, definition);
    }

    private void CreateMosque(Transform output, LandmarkDefinition definition, int index)
    {
        GameObject mosque = new GameObject($"Neighborhood mosque — {definition.arabicName}");
        mosque.transform.SetParent(output, false);
        mosque.transform.localPosition = definition.position;
        NeighborhoodGeometry.MarkStatic(mosque);

        NeighborhoodGeometry.Box(
            "Mosque prayer hall — solid collision shell",
            mosque.transform,
            new Vector3(0f, 3.7f, 0f),
            new Vector3(definition.footprint.x, 7.4f, definition.footprint.y),
            facadeMaterials != null && facadeMaterials.Length > 2
                ? facadeMaterials[2]
                : roofMaterial,
            true);
        NeighborhoodGeometry.Box(
            "Mosque roof cornice",
            mosque.transform,
            new Vector3(0f, 7.5f, 0f),
            new Vector3(definition.footprint.x + 0.5f, 0.34f, definition.footprint.y + 0.5f),
            trimMaterial);
        NeighborhoodGeometry.Sphere(
            "Original mosque dome",
            mosque.transform,
            new Vector3(0f, 9.2f, 0f),
            new Vector3(5.5f, 3.4f, 5.5f),
            accentMaterial);
        NeighborhoodGeometry.Cylinder(
            "Mosque dome finial",
            mosque.transform,
            new Vector3(0f, 11.05f, 0f),
            new Vector3(0.15f, 0.38f, 0.15f),
            trimMaterial);

        int minaretX = index % 2 == 0 ? 1 : -1;
        float sideX = minaretX * (definition.footprint.x * 0.5f - 0.65f);
        NeighborhoodGeometry.Cylinder(
            "Neighborhood minaret",
            mosque.transform,
            new Vector3(sideX, 7.2f, definition.footprint.y * 0.43f),
            new Vector3(0.72f, 7.2f, 0.72f),
            trimMaterial,
            true);
        NeighborhoodGeometry.Cylinder(
            "Minaret balcony",
            mosque.transform,
            new Vector3(sideX, 10.2f, definition.footprint.y * 0.43f),
            new Vector3(1.05f, 0.18f, 1.05f),
            accentMaterial);
        NeighborhoodGeometry.Cylinder(
            "Minaret cap",
            mosque.transform,
            new Vector3(sideX, 14.7f, definition.footprint.y * 0.43f),
            new Vector3(0.88f, 0.95f, 0.88f),
            accentMaterial);
        NeighborhoodGeometry.Sphere(
            "Minaret finial",
            mosque.transform,
            new Vector3(sideX, 15.45f, definition.footprint.y * 0.43f),
            Vector3.one * 0.3f,
            trimMaterial);
        NeighborhoodGeometry.Box(
            "Mosque entrance",
            mosque.transform,
            new Vector3(0f, 1.45f, definition.footprint.y * 0.5f + 0.12f),
            new Vector3(2.4f, 2.9f, 0.18f),
            timberMaterial);
        AddNameSign(mosque.transform, definition);
        NeighborhoodGeometry.CombineVisuals(mosque.transform);
        CreateForecourt(output, definition);
    }

    private void AddNameSign(Transform building, LandmarkDefinition definition)
    {
        float signZ = definition.footprint.y * 0.5f + 0.38f;
        NeighborhoodGeometry.Box(
            "Arabic landmark nameboard",
            building,
            new Vector3(0f, definition.height - 1.25f, signZ),
            new Vector3(definition.footprint.x * 0.76f, 0.92f, 0.2f),
            accentMaterial);
        NeighborhoodGeometry.Text(
            "Landmark Arabic name",
            building,
            new Vector3(0f, definition.height - 1.25f, signZ + 0.13f),
            Quaternion.identity,
            definition.arabicName,
            0.14f,
            Color.white,
            40);
    }

    private void CreateForecourt(Transform output, LandmarkDefinition definition)
    {
        NeighborhoodGeometry.Box(
            "Open paved landmark forecourt",
            output,
            new Vector3(
                definition.position.x,
                0.025f,
                definition.position.z + definition.footprint.y * 0.5f + 3.2f),
            new Vector3(definition.footprint.x + 4f, 0.05f, 5f),
            trimMaterial,
            true);
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class BuildingGenerator : MonoBehaviour
{
    [Header("Residential block dimensions")]
    [SerializeField, Min(2)] private int minimumFloors = 3;
    [SerializeField, Min(2)] private int maximumFloors = 6;
    [SerializeField, Min(6f)] private float buildingWidth = 11f;
    [SerializeField, Min(5f)] private float floorHeight = 3.05f;
    [SerializeField] private float[] streetFrontagePositions = { -54f, -30f, -18f, 18f, 30f, 54f };
    [SerializeField] private Material[] facadeMaterials = new Material[4];
    [SerializeField] private Material trimMaterial;
    [SerializeField] private Material glassMaterial;
    [SerializeField] private Material timberMaterial;
    [SerializeField] private Material roofMaterial;
    [SerializeField] private Material metalMaterial;
    [SerializeField] private Material waterTankMaterial;

    private readonly List<NeighborhoodLot> lots = new List<NeighborhoodLot>();

    public IReadOnlyList<NeighborhoodLot> Lots => lots;
    public float BuildingWidth => buildingWidth;

    public void ConfigureMaterials(
        Material[] facades,
        Material trim,
        Material glass,
        Material timber,
        Material roof,
        Material metal,
        Material waterTank)
    {
        facadeMaterials = facades;
        trimMaterial = trim;
        glassMaterial = glass;
        timberMaterial = timber;
        roofMaterial = roof;
        metalMaterial = metal;
        waterTankMaterial = waterTank;
    }

    public void ClearLots()
    {
        lots.Clear();
    }

    public void Generate(Transform output, RoadGenerator roadGenerator, Vector2 halfExtents)
    {
        List<NeighborhoodRoad> horizontalRoads = new List<NeighborhoodRoad>();
        for (int i = 0; i < roadGenerator.Roads.Length; i++)
        {
            NeighborhoodRoad road = roadGenerator.Roads[i];
            if (road.runsAlongX)
                horizontalRoads.Add(road);
        }
        horizontalRoads.Sort((left, right) => left.center.z.CompareTo(right.center.z));

        int lotIndex = 0;
        for (int row = 0; row < horizontalRoads.Count - 1; row++)
        {
            NeighborhoodRoad north = horizontalRoads[row + 1];
            NeighborhoodRoad south = horizontalRoads[row];
            float gap = north.center.z - south.center.z;
            float blockDepth = Mathf.Min(
                8f,
                gap - south.width * 0.5f - north.width * 0.5f -
                roadGenerator.SidewalkWidth * 2f - 1f);
            if (blockDepth < 5f)
                continue;

            float blockZ = (north.center.z + south.center.z) * 0.5f;
            for (int slot = 0; slot < streetFrontagePositions.Length; slot++)
            {
                float x = streetFrontagePositions[slot];
                if (Mathf.Abs(x) + buildingWidth * 0.5f > halfExtents.x - 2f)
                    continue;

                if (OverlapsCrossStreet(
                        roadGenerator.Roads,
                        x,
                        blockZ,
                        buildingWidth * 0.5f,
                        blockDepth * 0.5f))
                    continue;

                if (OverlapsLandmark(output, x, blockZ, buildingWidth * 0.5f, blockDepth * 0.5f))
                    continue;

                NeighborhoodLot lot = CreateApartmentBuilding(
                    output,
                    x,
                    blockZ,
                    blockDepth,
                    lotIndex);
                lots.Add(lot);
                lotIndex++;
            }
        }
    }

    private bool OverlapsCrossStreet(
        NeighborhoodRoad[] roads,
        float x,
        float z,
        float halfBuildingWidth,
        float halfBuildingDepth)
    {
        for (int i = 0; i < roads.Length; i++)
        {
            NeighborhoodRoad road = roads[i];
            if (road.runsAlongX)
                continue;

            float dx = Mathf.Abs(x - road.center.x);
            if (dx < road.width * 0.5f + halfBuildingWidth + 1.5f &&
                Mathf.Abs(z - road.center.z) < road.length * 0.5f + halfBuildingDepth)
                return true;
        }

        return false;
    }

    private static bool OverlapsLandmark(
        Transform output,
        float x,
        float z,
        float halfWidth,
        float halfDepth)
    {
        LandmarkGenerator landmarkGenerator = output.parent.GetComponent<LandmarkGenerator>();
        if (landmarkGenerator == null)
            return false;

        LandmarkDefinition[] definitions = landmarkGenerator.Definitions;
        for (int i = 0; i < definitions.Length; i++)
        {
            LandmarkDefinition landmark = definitions[i];
            if (Mathf.Abs(x - landmark.position.x) <
                    halfWidth + landmark.footprint.x * 0.5f + 2f &&
                Mathf.Abs(z - landmark.position.z) <
                    halfDepth + landmark.footprint.y * 0.5f + 2f)
                return true;
        }

        return false;
    }

    private NeighborhoodLot CreateApartmentBuilding(
        Transform output,
        float x,
        float z,
        float depth,
        int index)
    {
        int floors = minimumFloors + Mathf.Abs(index * 17 + 3) %
            Mathf.Max(1, maximumFloors - minimumFloors + 1);
        float height = floors * floorHeight;
        int style = Mathf.Abs(index * 13 + 5) %
            Mathf.Max(1, facadeMaterials == null ? 1 : facadeMaterials.Length);

        GameObject building = new GameObject($"Residential block {index + 1:00} ({floors} floors)");
        building.transform.SetParent(output, false);
        building.transform.localPosition = new Vector3(x, 0f, z);
        NeighborhoodGeometry.MarkStatic(building);

        GameObject shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shell.name = "Solid apartment shell — collision";
        shell.transform.SetParent(building.transform, false);
        shell.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        shell.transform.localScale = new Vector3(buildingWidth, height, depth);
        if (facadeMaterials != null && facadeMaterials.Length > 0)
            NeighborhoodGeometry.ApplyMaterial(shell, facadeMaterials[style]);
        NeighborhoodGeometry.MarkStatic(shell);

        NeighborhoodGeometry.Box(
            "Parapet roof slab",
            building.transform,
            new Vector3(0f, height + 0.08f, 0f),
            new Vector3(buildingWidth + 0.25f, 0.16f, depth + 0.25f),
            roofMaterial);
        CreateRoofParapets(building.transform, height, depth);

        for (int side = -1; side <= 1; side += 2)
            CreateResidentialFacade(building.transform, side, height, depth, floors, index);

        CreateRooftopUtilities(building.transform, height, depth, index);
        NeighborhoodGeometry.CombineVisuals(building.transform);

        return new NeighborhoodLot
        {
            center = new Vector3(x, 0f, z),
            size = new Vector2(buildingWidth, depth),
            floors = floors,
            style = style,
            facesPositiveZ = index % 2 == 0,
            buildingName = building.name
        };
    }

    private void CreateResidentialFacade(
        Transform building,
        int side,
        float height,
        float depth,
        int floors,
        int index)
    {
        float face = side * (depth * 0.5f + 0.07f);
        for (int floor = 0; floor < floors; floor++)
        {
            float y = floor * floorHeight + 1.8f;
            for (int windowIndex = 0; windowIndex < 2; windowIndex++)
            {
                float x = windowIndex == 0 ? -2.55f : 2.55f;
                NeighborhoodGeometry.Box(
                    "Recessed-look apartment window",
                    building,
                    new Vector3(x, y, face),
                    new Vector3(1.5f, 1.23f, 0.14f),
                    glassMaterial);
                NeighborhoodGeometry.Box(
                    "Window surround",
                    building,
                    new Vector3(x, y - 0.67f, face + side * 0.035f),
                    new Vector3(1.72f, 0.12f, 0.2f),
                    trimMaterial);
            }

            if (floor == 0)
            {
                NeighborhoodGeometry.Box(
                    "Residential entrance",
                    building,
                    new Vector3(0f, 1.18f, face + side * 0.05f),
                    new Vector3(1.7f, 2.35f, 0.16f),
                    timberMaterial);
            }
            else if (floor % 2 == 1)
            {
                CreateBalcony(building, side, face, floor);
            }

            if (floor > 0 && (index + floor + side) % 3 == 0)
                CreateAirConditioner(building, side, face, floor);
        }

        NeighborhoodGeometry.Box(
            "Facade band",
            building,
            new Vector3(0f, 3.02f, face + side * 0.015f),
            new Vector3(buildingWidth, 0.12f, 0.16f),
            trimMaterial);
        NeighborhoodGeometry.Box(
            "Balcony side color stripe",
            building,
            new Vector3(side * (buildingWidth * 0.5f - 0.2f), height * 0.5f, face),
            new Vector3(0.16f, height, 0.2f),
            trimMaterial);
    }

    private void CreateBalcony(Transform parent, int side, float face, int floor)
    {
        float y = floor * floorHeight + 1.05f;
        float outward = face + side * 0.62f;
        NeighborhoodGeometry.Box(
            "Concrete balcony",
            parent,
            new Vector3(0f, y, outward),
            new Vector3(3.35f, 0.16f, 1.15f),
            trimMaterial);
        NeighborhoodGeometry.Box(
            "Balcony front rail",
            parent,
            new Vector3(0f, y + 0.48f, outward + side * 0.53f),
            new Vector3(3.35f, 0.08f, 0.08f),
            metalMaterial);
        for (int post = -1; post <= 1; post++)
        {
            NeighborhoodGeometry.Box(
                "Balcony railing upright",
                parent,
                new Vector3(post * 1.55f, y + 0.25f, outward + side * 0.53f),
                new Vector3(0.08f, 0.52f, 0.08f),
                metalMaterial);
        }
    }

    private void CreateAirConditioner(Transform parent, int side, float face, int floor)
    {
        float y = floor * floorHeight + 0.85f;
        NeighborhoodGeometry.Box(
            "External air-conditioner unit",
            parent,
            new Vector3(3.9f, y, face + side * 0.22f),
            new Vector3(0.85f, 0.52f, 0.42f),
            metalMaterial);
        NeighborhoodGeometry.Cylinder(
            "Air-conditioner fan grille",
            parent,
            new Vector3(3.9f, y, face + side * 0.445f),
            new Vector3(0.28f, 0.02f, 0.28f),
            trimMaterial);
    }

    private void CreateRoofParapets(Transform building, float height, float depth)
    {
        float roofY = height + 0.36f;
        NeighborhoodGeometry.Box(
            "Rooftop front parapet",
            building,
            new Vector3(0f, roofY, depth * 0.5f),
            new Vector3(buildingWidth, 0.52f, 0.22f),
            trimMaterial);
        NeighborhoodGeometry.Box(
            "Rooftop rear parapet",
            building,
            new Vector3(0f, roofY, -depth * 0.5f),
            new Vector3(buildingWidth, 0.52f, 0.22f),
            trimMaterial);
        for (int side = -1; side <= 1; side += 2)
        {
            NeighborhoodGeometry.Box(
                "Rooftop side parapet",
                building,
                new Vector3(side * buildingWidth * 0.5f, roofY, 0f),
                new Vector3(0.22f, 0.52f, depth),
                trimMaterial);
        }
    }

    private void CreateRooftopUtilities(Transform building, float height, float depth, int index)
    {
        Vector3 tankPosition = new Vector3(-buildingWidth * 0.28f, height + 0.9f, 0f);
        NeighborhoodGeometry.Cylinder(
            "Rooftop water tank",
            building,
            tankPosition,
            new Vector3(1.3f, 0.9f, 1.3f),
            waterTankMaterial);
        NeighborhoodGeometry.Cylinder(
            "Water tank lid",
            building,
            tankPosition + Vector3.up * 0.95f,
            new Vector3(1.32f, 0.08f, 1.32f),
            metalMaterial);

        float dishX = index % 2 == 0 ? buildingWidth * 0.27f : -buildingWidth * 0.27f;
        NeighborhoodGeometry.Cylinder(
            "Small satellite dish",
            building,
            new Vector3(dishX, height + 0.72f, depth * 0.25f),
            new Vector3(1.1f, 0.12f, 0.8f),
            metalMaterial);
        NeighborhoodGeometry.Cylinder(
            "Satellite dish support",
            building,
            new Vector3(dishX, height + 0.45f, depth * 0.25f),
            new Vector3(0.09f, 0.3f, 0.09f),
            metalMaterial);
    }
}

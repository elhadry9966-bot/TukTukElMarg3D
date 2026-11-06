using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class NeighborhoodRoad
{
    public string name = "شارع جديد";
    public bool runsAlongX = true;
    public Vector3 center;
    [Min(8f)] public float length = 110f;
    [Min(3f)] public float width = 7.5f;
    public bool sidewalks = true;
    public bool mainStreet;
}

public sealed class RoadGenerator : MonoBehaviour
{
    [Header("Road network — approximate layout from the supplied reference image")]
    [SerializeField] private NeighborhoodRoad[] roads =
    {
        new NeighborhoodRoad { name = "شارع الكرام", runsAlongX = true, center = new Vector3(0f, 0f, -60f), width = 8.5f, mainStreet = true },
        new NeighborhoodRoad { name = "شارع سعيد فكري", runsAlongX = true, center = new Vector3(0f, 0f, -40f), width = 7.5f },
        new NeighborhoodRoad { name = "شارع عبد السلام السيد", runsAlongX = true, center = new Vector3(0f, 0f, -20f), width = 8.5f, mainStreet = true },
        new NeighborhoodRoad { name = "شارع رفعت المنقلاوي", runsAlongX = true, center = new Vector3(0f, 0f, 0f), width = 7.5f },
        new NeighborhoodRoad { name = "شارع عبد العاطي شرابي", runsAlongX = true, center = new Vector3(0f, 0f, 20f), width = 7.5f },
        new NeighborhoodRoad { name = "شارع البرماوي", runsAlongX = true, center = new Vector3(0f, 0f, 40f), width = 7.5f },
        new NeighborhoodRoad { name = "شارع سيد هارون", runsAlongX = true, center = new Vector3(0f, 0f, 60f), width = 8.5f, mainStreet = true },
        new NeighborhoodRoad { name = "حارة المدارس", runsAlongX = false, center = new Vector3(-42f, 0f, 0f), length = 150f, width = 5f },
        new NeighborhoodRoad { name = "شارع المحطة", runsAlongX = false, center = new Vector3(0f, 0f, 0f), length = 150f, width = 6f },
        new NeighborhoodRoad { name = "شارع محمد يوسف", runsAlongX = false, center = new Vector3(42f, 0f, 0f), length = 150f, width = 8f, mainStreet = true }
    };

    [Header("Dimensions")]
    [SerializeField, Min(1f)] private float sidewalkWidth = 2.6f;
    [SerializeField, Min(0.05f)] private float sidewalkHeight = 0.22f;
    [SerializeField] private Material asphalt;
    [SerializeField] private Material sidewalk;
    [SerializeField] private Material roadMarking;
    [SerializeField] private Material poleMaterial;
    [SerializeField] private Material lampMaterial;
    [SerializeField] private Material trafficMaterial;
    [SerializeField] private Material glassMaterial;
    [SerializeField] private Material rubberMaterial;
    [SerializeField] private Material metalMaterial;

    public NeighborhoodRoad[] Roads => roads;
    public float SidewalkWidth => sidewalkWidth;

    public void ConfigureMaterials(
        Material roadSurface,
        Material sidewalkSurface,
        Material roadLine,
        Material pole,
        Material lamp,
        Material vehiclePaint,
        Material glass,
        Material rubber,
        Material metal)
    {
        asphalt = roadSurface;
        sidewalk = sidewalkSurface;
        roadMarking = roadLine;
        poleMaterial = pole;
        lampMaterial = lamp;
        trafficMaterial = vehiclePaint;
        glassMaterial = glass;
        rubberMaterial = rubber;
        metalMaterial = metal;
    }

    public void Generate(Transform output, Vector2 halfExtents)
    {
        NeighborhoodGeometry.Box(
            "Ground and collision floor",
            output,
            new Vector3(0f, -0.36f, 0f),
            new Vector3((halfExtents.x + 6f) * 2f, 0.72f, (halfExtents.y + 6f) * 2f),
            sidewalk,
            true);

        for (int i = 0; i < roads.Length; i++)
        {
            NeighborhoodRoad road = roads[i];
            float maxLength = road.runsAlongX
                ? (halfExtents.x + 4f) * 2f
                : (halfExtents.y + 4f) * 2f;
            float length = Mathf.Min(road.length, maxLength);
            Vector3 center = road.center;
            Vector3 size = road.runsAlongX
                ? new Vector3(length, 0.08f, road.width)
                : new Vector3(road.width, 0.08f, length);

            NeighborhoodGeometry.Box(
                road.name + " asphalt — collision surface",
                output,
                new Vector3(center.x, 0.025f, center.z),
                size,
                asphalt,
                true);
            CreateSidewalks(output, road, length);
            Transform streetFurniture = new GameObject(road.name + " roadside furniture").transform;
            streetFurniture.SetParent(output, false);
            CreateRoadMarkings(streetFurniture, road, length);
            CreateTrafficFurniture(streetFurniture, road, length);
            NeighborhoodGeometry.CombineVisuals(streetFurniture);
        }

        CreateParkingAreas(output, halfExtents);
    }

    private void CreateSidewalks(Transform output, NeighborhoodRoad road, float roadLength)
    {
        if (!road.sidewalks)
            return;

        List<float> gaps = new List<float>();
        for (int i = 0; i < roads.Length; i++)
        {
            NeighborhoodRoad crossing = roads[i];
            if (crossing.runsAlongX == road.runsAlongX)
                continue;

            float perpendicularDistance = road.runsAlongX
                ? Mathf.Abs(crossing.center.z - road.center.z)
                : Mathf.Abs(crossing.center.x - road.center.x);
            float crossingHalfLength = crossing.length * 0.5f;
            if (perpendicularDistance > crossingHalfLength + road.width * 0.5f)
                continue;

            float position = road.runsAlongX ? crossing.center.x : crossing.center.z;
            float start = road.runsAlongX ? road.center.x : road.center.z;
            if (Mathf.Abs(position - start) < roadLength * 0.5f)
                gaps.Add(position);
        }

        gaps.Sort();
        float roadStart = (road.runsAlongX ? road.center.x : road.center.z) - roadLength * 0.5f;
        float roadEnd = roadStart + roadLength;
        float gapHalfWidth = road.width * 0.5f + 0.15f;

        for (int side = -1; side <= 1; side += 2)
        {
            float cursor = roadStart;
            for (int i = 0; i < gaps.Count; i++)
            {
                float segmentEnd = gaps[i] - gapHalfWidth;
                CreateSidewalkSegment(output, road, side, cursor, segmentEnd);
                cursor = Mathf.Max(cursor, gaps[i] + gapHalfWidth);
            }

            CreateSidewalkSegment(output, road, side, cursor, roadEnd);
        }
    }

    private void CreateSidewalkSegment(
        Transform output,
        NeighborhoodRoad road,
        int side,
        float start,
        float end)
    {
        float length = end - start;
        if (length < 0.5f)
            return;

        Vector3 center;
        Vector3 size;
        if (road.runsAlongX)
        {
            center = new Vector3(
                (start + end) * 0.5f,
                sidewalkHeight * 0.5f,
                road.center.z + side * (road.width * 0.5f + sidewalkWidth * 0.5f));
            size = new Vector3(length, sidewalkHeight, sidewalkWidth);
        }
        else
        {
            center = new Vector3(
                road.center.x + side * (road.width * 0.5f + sidewalkWidth * 0.5f),
                sidewalkHeight * 0.5f,
                (start + end) * 0.5f);
            size = new Vector3(sidewalkWidth, sidewalkHeight, length);
        }

        NeighborhoodGeometry.Box(
            "Connected sidewalk",
            output,
            center,
            size,
            sidewalk,
            true);
    }

    private void CreateRoadMarkings(Transform output, NeighborhoodRoad road, float length)
    {
        if (roadMarking == null || !road.mainStreet)
            return;

        float start = (road.runsAlongX ? road.center.x : road.center.z) - length * 0.5f + 5f;
        float end = start + length - 10f;
        for (float along = start; along < end; along += 13f)
        {
            bool nearJunction = false;
            for (int i = 0; i < roads.Length; i++)
            {
                NeighborhoodRoad crossing = roads[i];
                if (crossing.runsAlongX == road.runsAlongX)
                    continue;

                float crossPosition = road.runsAlongX ? crossing.center.x : crossing.center.z;
                if (Mathf.Abs(along - crossPosition) < crossing.width * 0.5f + 2f)
                {
                    nearJunction = true;
                    break;
                }
            }

            if (nearJunction)
                continue;

            Vector3 position = road.runsAlongX
                ? new Vector3(along, 0.071f, road.center.z)
                : new Vector3(road.center.x, 0.071f, along);
            Vector3 size = road.runsAlongX
                ? new Vector3(4f, 0.012f, 0.12f)
                : new Vector3(0.12f, 0.012f, 4f);
            NeighborhoodGeometry.Box("Faded lane marking", output, position, size, roadMarking);
        }
    }

    private void CreateTrafficFurniture(Transform output, NeighborhoodRoad road, float length)
    {
        if (!road.sidewalks)
            return;

        float start = (road.runsAlongX ? road.center.x : road.center.z) - length * 0.5f + 10f;
        float end = start + length - 20f;
        int count = 0;
        for (float along = start; along < end; along += 34f)
        {
            bool atIntersection = false;
            for (int i = 0; i < roads.Length; i++)
            {
                NeighborhoodRoad crossing = roads[i];
                if (crossing.runsAlongX == road.runsAlongX)
                    continue;

                float crossPosition = road.runsAlongX ? crossing.center.x : crossing.center.z;
                if (Mathf.Abs(along - crossPosition) < crossing.width * 0.5f + 7f)
                {
                    atIntersection = true;
                    break;
                }
            }

            if (atIntersection)
                continue;

            int side = count % 2 == 0 ? 1 : -1;
            Vector3 basePosition = road.runsAlongX
                ? new Vector3(along, 0f, road.center.z + side * (road.width * 0.5f + sidewalkWidth * 0.58f))
                : new Vector3(road.center.x + side * (road.width * 0.5f + sidewalkWidth * 0.58f), 0f, along);
            CreateLamp(output, basePosition);
            if (count % 3 == 0)
                CreateUtilityPole(output, basePosition + new Vector3(-2.8f, 0f, 1.8f));
            if (count % 4 == 1)
                CreateStreetProps(output, basePosition + new Vector3(2f, 0f, -1.5f), count);
            count++;
        }
    }

    private void CreateLamp(Transform output, Vector3 position)
    {
        NeighborhoodGeometry.Cylinder(
            "Streetlight pole",
            output,
            position + Vector3.up * 3f,
            new Vector3(0.13f, 3f, 0.13f),
            poleMaterial,
            true);
        NeighborhoodGeometry.Beam(
            "Streetlight arm",
            output,
            position + new Vector3(0f, 5.85f, 0f),
            position + new Vector3(0.7f, 5.85f, 0f),
            0.06f,
            poleMaterial);
        NeighborhoodGeometry.Box(
            "Warm streetlight fixture",
            output,
            position + new Vector3(0.9f, 5.78f, 0f),
            new Vector3(0.4f, 0.17f, 0.26f),
            lampMaterial);
    }

    private void CreateUtilityPole(Transform output, Vector3 position)
    {
        NeighborhoodGeometry.Cylinder(
            "Concrete electricity pole",
            output,
            position + Vector3.up * 3.25f,
            new Vector3(0.22f, 3.25f, 0.22f),
            poleMaterial,
            true);
        NeighborhoodGeometry.Beam(
            "Utility crossarm",
            output,
            position + new Vector3(-1f, 5.7f, 0f),
            position + new Vector3(1f, 5.7f, 0f),
            0.12f,
            metalMaterial);
        NeighborhoodGeometry.Box(
            "Street transformer",
            output,
            position + new Vector3(0.42f, 4.55f, 0f),
            new Vector3(0.62f, 0.8f, 0.55f),
            metalMaterial);
    }

    private void CreateStreetProps(Transform output, Vector3 position, int seed)
    {
        NeighborhoodGeometry.Box(
            "Public waste bin",
            output,
            position + new Vector3(0f, 0.48f, 0f),
            new Vector3(0.7f, 0.96f, 0.58f),
            trafficMaterial,
            true);

        if (seed % 2 == 0)
        {
            NeighborhoodGeometry.Cylinder(
                "Concrete tree planter",
                output,
                position + new Vector3(2.1f, 0.2f, 0f),
                new Vector3(1.15f, 0.2f, 1.15f),
                sidewalk,
                true);
            NeighborhoodGeometry.Cylinder(
                "Street tree trunk",
                output,
                position + new Vector3(2.1f, 1.15f, 0f),
                new Vector3(0.22f, 0.95f, 0.22f),
                poleMaterial,
                true);
            NeighborhoodGeometry.Sphere(
                "Compact street tree canopy",
                output,
                position + new Vector3(2.1f, 2.4f, 0f),
                new Vector3(2.1f, 2.2f, 2.1f),
                trafficMaterial);
        }

        if (seed % 5 == 0)
            CreateParkedTukTuk(output, position + new Vector3(3.1f, 0f, 0f));
        else if (seed % 5 == 2)
            CreateParkedMotorcycle(output, position + new Vector3(3.1f, 0f, 0f));
    }

    private void CreateParkingAreas(Transform output, Vector2 halfExtents)
    {
        for (int row = 1; row < roads.Length - 1; row++)
        {
            NeighborhoodRoad road = roads[row];
            if (!road.runsAlongX)
                continue;

            float zOffset = road.width * 0.5f + sidewalkWidth + 1.35f;
            for (int side = -1; side <= 1; side += 2)
            {
                float z = road.center.z + side * zOffset;
                for (int section = -1; section <= 1; section += 2)
                {
                    float x = section * Mathf.Min(24f, halfExtents.x - 12f);
                    bool nearVerticalRoad = false;
                    for (int i = 0; i < roads.Length; i++)
                    {
                        NeighborhoodRoad crossing = roads[i];
                        if (crossing.runsAlongX)
                            continue;
                        if (Mathf.Abs(crossing.center.x - x) <
                            crossing.width * 0.5f + 5f)
                        {
                            nearVerticalRoad = true;
                            break;
                        }
                    }

                    if (!nearVerticalRoad)
                    {
                        NeighborhoodGeometry.Box(
                            "Marked curbside parking bay",
                            output,
                            new Vector3(x, 0.028f, z),
                            new Vector3(5.2f, 0.045f, 2.35f),
                            sidewalk);
                        CreateParkedVehicle(output, new Vector3(x, 0f, z), section);
                        for (int lineSide = -1; lineSide <= 1; lineSide += 2)
                        {
                            NeighborhoodGeometry.Box(
                                "Parking bay line",
                                output,
                                new Vector3(x + lineSide * 2.5f, 0.06f, z),
                                new Vector3(0.08f, 0.02f, 2.2f),
                                roadMarking);
                        }
                }
            }
        }
    }
    }

    private void CreateParkedVehicle(Transform output, Vector3 position, int orientation)
    {
        GameObject car = new GameObject("Parked neighborhood car");
        car.transform.SetParent(output, false);
        car.transform.localPosition = position;
        car.transform.localRotation = Quaternion.Euler(0f, orientation < 0 ? -90f : 90f, 0f);
        NeighborhoodGeometry.Box(
            "Car lower body",
            car.transform,
            new Vector3(0f, 0.63f, 0f),
            new Vector3(1.8f, 0.68f, 3.65f),
            trafficMaterial,
            true);
        NeighborhoodGeometry.Box(
            "Car cabin",
            car.transform,
            new Vector3(0f, 1.17f, -0.1f),
            new Vector3(1.47f, 0.68f, 1.75f),
            glassMaterial);
        for (int wheelSide = -1; wheelSide <= 1; wheelSide += 2)
        {
            for (int axle = -1; axle <= 1; axle += 2)
            {
                NeighborhoodGeometry.Cylinder(
                    "Parked car tire",
                    car.transform,
                    new Vector3(wheelSide * 0.93f, 0.38f, axle * 1.12f),
                    new Vector3(0.24f, 0.38f, 0.38f),
                    rubberMaterial);
            }
        }

        NeighborhoodGeometry.CombineVisuals(car.transform);
        }

        private void CreateParkedTukTuk(Transform output, Vector3 position)
        {
            GameObject tukTuk = new GameObject("Parked neighborhood tuk-tuk");
            tukTuk.transform.SetParent(output, false);
            tukTuk.transform.localPosition = position;
            NeighborhoodGeometry.Box(
                "Parked tuk-tuk body",
                tukTuk.transform,
                new Vector3(0f, 0.68f, 0f),
                new Vector3(1.3f, 0.72f, 2f),
                trafficMaterial,
                true);
            NeighborhoodGeometry.Box(
                "Parked tuk-tuk roof",
                tukTuk.transform,
                new Vector3(0f, 1.78f, -0.1f),
                new Vector3(1.55f, 0.16f, 1.7f),
                metalMaterial);
            NeighborhoodGeometry.Cylinder(
                "Parked tuk-tuk front wheel",
                tukTuk.transform,
                new Vector3(0f, 0.36f, 0.83f),
                new Vector3(0.2f, 0.36f, 0.36f),
                rubberMaterial);
            for (int side = -1; side <= 1; side += 2)
            {
                NeighborhoodGeometry.Cylinder(
                    "Parked tuk-tuk rear wheel",
                    tukTuk.transform,
                    new Vector3(side * 0.68f, 0.36f, -0.55f),
                    new Vector3(0.2f, 0.36f, 0.36f),
                    rubberMaterial);
                NeighborhoodGeometry.Box(
                    "Parked tuk-tuk open side",
                    tukTuk.transform,
                    new Vector3(side * 0.69f, 1.18f, -0.1f),
                    new Vector3(0.12f, 0.1f, 1.3f),
                    lampMaterial);
        }

        NeighborhoodGeometry.CombineVisuals(tukTuk.transform);
        }

        private void CreateParkedMotorcycle(Transform output, Vector3 position)
        {
            GameObject motorcycle = new GameObject("Parked neighborhood motorcycle");
            motorcycle.transform.SetParent(output, false);
            motorcycle.transform.localPosition = position;
            NeighborhoodGeometry.Cylinder(
                "Motorcycle front wheel",
                motorcycle.transform,
                new Vector3(0f, 0.4f, 0.75f),
                new Vector3(0.15f, 0.38f, 0.38f),
                rubberMaterial);
            NeighborhoodGeometry.Cylinder(
                "Motorcycle rear wheel",
                motorcycle.transform,
                new Vector3(0f, 0.4f, -0.72f),
                new Vector3(0.15f, 0.38f, 0.38f),
                rubberMaterial);
            NeighborhoodGeometry.Box(
                "Motorcycle frame",
                motorcycle.transform,
                new Vector3(0f, 0.77f, 0f),
                new Vector3(0.24f, 0.28f, 1.15f),
                metalMaterial);
            NeighborhoodGeometry.Box(
                "Motorcycle seat",
                motorcycle.transform,
                new Vector3(0f, 0.97f, -0.25f),
                new Vector3(0.4f, 0.12f, 0.82f),
                rubberMaterial);
            NeighborhoodGeometry.Box(
                "Motorcycle fuel tank",
                motorcycle.transform,
                new Vector3(0f, 1.02f, 0.33f),
                new Vector3(0.48f, 0.38f, 0.5f),
                trafficMaterial);
        NeighborhoodGeometry.CombineVisuals(motorcycle.transform);
        }
}

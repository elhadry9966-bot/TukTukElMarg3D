using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public sealed class NeighborhoodLot
{
    public Vector3 center;
    public Vector2 size;
    public int floors;
    public int style;
    public bool facesPositiveZ;
    public string buildingName;
}

[ExecuteAlways]
public sealed class NeighborhoodGenerator : MonoBehaviour
{
    [SerializeField] private RoadGenerator roads;
    [SerializeField] private BuildingGenerator buildings;
    [SerializeField] private ShopGenerator shops;
    [SerializeField] private LandmarkGenerator landmarks;
    [SerializeField] private StreetSignGenerator streetSigns;
    [SerializeField] private Vector2 mapHalfExtents = new Vector2(70f, 80f);
    [SerializeField] private string generatedRootName = "Generated Neighborhood";

    public RoadGenerator Roads => roads;
    public BuildingGenerator Buildings => buildings;
    public ShopGenerator Shops => shops;
    public LandmarkGenerator Landmarks => landmarks;
    public StreetSignGenerator StreetSigns => streetSigns;
    public Vector2 MapHalfExtents => mapHalfExtents;

    public void Configure(
        RoadGenerator roadGenerator,
        BuildingGenerator buildingGenerator,
        ShopGenerator shopGenerator,
        LandmarkGenerator landmarkGenerator,
        StreetSignGenerator signGenerator)
    {
        roads = roadGenerator;
        buildings = buildingGenerator;
        shops = shopGenerator;
        landmarks = landmarkGenerator;
        streetSigns = signGenerator;
    }

    public void Generate()
    {
        if (roads == null || buildings == null || shops == null ||
            landmarks == null || streetSigns == null)
            throw new InvalidOperationException(
                "Assign all five map generators before generating the neighborhood.");

        Transform previous = transform.Find(generatedRootName);
        if (previous != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(previous.gameObject);
            else
                Destroy(previous.gameObject);
#else
            Destroy(previous.gameObject);
#endif
        }

        GameObject generated = new GameObject(generatedRootName);
        generated.transform.SetParent(transform, false);

        buildings.ClearLots();
        roads.Generate(generated.transform, mapHalfExtents);
        buildings.Generate(generated.transform, roads, mapHalfExtents);
        shops.Generate(generated.transform, buildings.Lots);
        landmarks.Generate(generated.transform);
        streetSigns.Generate(generated.transform, roads);
    }
}

public static class NeighborhoodGeometry
{
    public static GameObject Box(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Material material,
        bool collider = false,
        bool isStatic = true)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = size;
        ApplyMaterial(item, material);
        if (!collider)
            RemoveCollider(item);
        if (isStatic)
            MarkStatic(item);
        return item;
    }

    public static GameObject Cylinder(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Material material,
        bool collider = false,
        bool isStatic = true)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = size;
        ApplyMaterial(item, material);
        if (!collider)
            RemoveCollider(item);
        if (isStatic)
            MarkStatic(item);
        return item;
    }

    public static GameObject Sphere(
        string name,
        Transform parent,
        Vector3 position,
        Vector3 size,
        Material material,
        bool isStatic = true)
    {
        GameObject item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        item.name = name;
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localScale = size;
        ApplyMaterial(item, material);
        RemoveCollider(item);
        if (isStatic)
            MarkStatic(item);
        return item;
    }

    public static GameObject Text(
        string name,
        Transform parent,
        Vector3 position,
        Quaternion rotation,
        string value,
        float characterSize,
        Color color,
        int fontSize = 42)
    {
        GameObject item = new GameObject(name);
        item.transform.SetParent(parent, false);
        item.transform.localPosition = position;
        item.transform.localRotation = rotation;
        TextMesh text = item.AddComponent<TextMesh>();
        text.text = ArabicTextLayout.ShapeAndReverse(value);
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = characterSize;
        text.fontSize = fontSize;
        text.color = color;
        text.richText = false;
        item.AddComponent<ArabicLabelText>().Configure(text, value, fontSize);
        MeshRenderer renderer = item.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = text.font.material;
        MarkStatic(item);
        return item;
    }

    public static void Beam(
        string name,
        Transform parent,
        Vector3 start,
        Vector3 end,
        float thickness,
        Material material)
    {
        Vector3 delta = end - start;
        GameObject beam = Cylinder(
            name,
            parent,
            (start + end) * 0.5f,
            new Vector3(thickness, delta.magnitude * 0.5f, thickness),
            material);
        beam.transform.localRotation = Quaternion.FromToRotation(Vector3.up, delta.normalized);
    }

    public static void CombineVisuals(Transform owner)
    {
        Dictionary<Material, List<CombineInstance>> materialMeshes =
            new Dictionary<Material, List<CombineInstance>>();
        List<GameObject> sourceObjects = new List<GameObject>();
        MeshFilter[] filters = owner.GetComponentsInChildren<MeshFilter>(true);

        for (int i = 0; i < filters.Length; i++)
        {
            MeshFilter filter = filters[i];
            if (filter.transform == owner || filter.sharedMesh == null ||
                filter.GetComponent<Collider>() != null ||
                filter.GetComponent<TextMesh>() != null)
                continue;

            MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                continue;

            if (!materialMeshes.TryGetValue(renderer.sharedMaterial, out List<CombineInstance> pieces))
            {
                pieces = new List<CombineInstance>();
                materialMeshes.Add(renderer.sharedMaterial, pieces);
            }

            pieces.Add(new CombineInstance
            {
                mesh = filter.sharedMesh,
                transform = owner.worldToLocalMatrix * filter.transform.localToWorldMatrix
            });
            sourceObjects.Add(filter.gameObject);
        }

        if (sourceObjects.Count == 0)
            return;

        foreach (KeyValuePair<Material, List<CombineInstance>> group in materialMeshes)
        {
            int vertexCount = 0;
            for (int i = 0; i < group.Value.Count; i++)
                vertexCount += group.Value[i].mesh.vertexCount;

            Mesh combined = new Mesh();
            combined.name = owner.name + " — " + group.Key.name;
            if (vertexCount > 65535)
                combined.indexFormat = IndexFormat.UInt32;
            combined.CombineMeshes(group.Value.ToArray(), true, true);

            GameObject combinedObject = new GameObject("Combined " + group.Key.name);
            combinedObject.transform.SetParent(owner, false);
            combinedObject.AddComponent<MeshFilter>().sharedMesh = combined;
            combinedObject.AddComponent<MeshRenderer>().sharedMaterial = group.Key;
            MarkStatic(combinedObject);
        }

        for (int i = 0; i < sourceObjects.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(sourceObjects[i]);
            else
                UnityEngine.Object.Destroy(sourceObjects[i]);
#else
            UnityEngine.Object.Destroy(sourceObjects[i]);
#endif
        }
    }

    public static void ApplyMaterial(GameObject target, Material material)
    {
        if (material == null)
            return;

        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = material;
    }

    public static void MarkStatic(GameObject item)
    {
#if UNITY_EDITOR
        UnityEditor.GameObjectUtility.SetStaticEditorFlags(
            item,
            UnityEditor.StaticEditorFlags.BatchingStatic |
            UnityEditor.StaticEditorFlags.OccludeeStatic);
#endif
    }

    public static void RemoveCollider(GameObject item)
    {
        Collider collider = item.GetComponent<Collider>();
        if (collider == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEngine.Object.DestroyImmediate(collider);
        else
            UnityEngine.Object.Destroy(collider);
#else
        UnityEngine.Object.Destroy(collider);
#endif
    }

    public static bool IsInsideMap(Vector3 position, Vector2 halfExtents)
    {
        return Mathf.Abs(position.x) <= halfExtents.x &&
               Mathf.Abs(position.z) <= halfExtents.y;
    }
}

using System;
using UnityEngine;

[Serializable]
public sealed class NeighborhoodShop
{
    public string shopType;
    public string fictionalName;
}

public sealed class ShopGenerator : MonoBehaviour
{
    [Header("Original fictional shop signs — edit names here")]
    [SerializeField] private NeighborhoodShop[] shopTypes =
    {
        new NeighborhoodShop { shopType = "بقالة", fictionalName = "بقالة حبايب المرج" },
        new NeighborhoodShop { shopType = "سوبر ماركت", fictionalName = "سوبر ماركت حارة الورد" },
        new NeighborhoodShop { shopType = "خضار وفاكهة", fictionalName = "خيرات الحارة" },
        new NeighborhoodShop { shopType = "صيدلية", fictionalName = "صيدلية ضيّ الحارة" },
        new NeighborhoodShop { shopType = "فول وطعمية", fictionalName = "فول وطعمية عالماشي" },
        new NeighborhoodShop { shopType = "عصائر", fictionalName = "عصير قصب السعادة" },
        new NeighborhoodShop { shopType = "ورشة ميكانيكا", fictionalName = "ورشة الصنايعي" },
        new NeighborhoodShop { shopType = "قطع غيار توك توك", fictionalName = "الريس لقطع الغيار" },
        new NeighborhoodShop { shopType = "كشك جرائد", fictionalName = "كشك حكاوي الصباح" },
        new NeighborhoodShop { shopType = "ملابس", fictionalName = "ملابس لمّة العيلة" }
    };

    [SerializeField] private Material[] signMaterials = new Material[4];
    [SerializeField] private Material frameMaterial;
    [SerializeField] private Material glassMaterial;
    [SerializeField] private Material woodMaterial;
    [SerializeField] private Material doorMaterial;
    [SerializeField] private Material accentMaterial;
    [SerializeField, Min(0.1f)] private float storefrontLightRange = 5.5f;

    public NeighborhoodShop[] ShopTypes => shopTypes;

    public void ConfigureMaterials(
        Material[] signs,
        Material frame,
        Material glass,
        Material wood,
        Material door,
        Material accent)
    {
        signMaterials = signs;
        frameMaterial = frame;
        glassMaterial = glass;
        woodMaterial = wood;
        doorMaterial = door;
        accentMaterial = accent;
    }

    public void Generate(Transform output, System.Collections.Generic.IReadOnlyList<NeighborhoodLot> lots)
    {
        if (shopTypes == null || shopTypes.Length == 0)
            return;

        for (int i = 0; i < lots.Count; i++)
        {
            if (i % 3 == 1)
                continue;

            NeighborhoodLot lot = lots[i];
            int shopIndex = Mathf.Abs(i * 7 + lot.style) % shopTypes.Length;
            NeighborhoodShop shop = shopTypes[shopIndex];
            int paletteIndex = Mathf.Abs(i + shopIndex) %
                               Mathf.Max(1, signMaterials == null ? 1 : signMaterials.Length);
            CreateStorefront(output, lot, shop, paletteIndex, i);
        }
    }

    private void CreateStorefront(
        Transform output,
        NeighborhoodLot lot,
        NeighborhoodShop shop,
        int paletteIndex,
        int index)
    {
        GameObject store = new GameObject($"{shop.shopType} — {shop.fictionalName}");
        store.transform.SetParent(output, false);
        store.transform.localPosition = lot.center;
        int side = lot.facesPositiveZ ? 1 : -1;
        float facadeZ = side * (lot.size.y * 0.5f + 0.11f);
        float width = Mathf.Min(7.7f, lot.size.x - 1.8f);

        Material sign = signMaterials != null && signMaterials.Length > 0
            ? signMaterials[paletteIndex]
            : frameMaterial;
        NeighborhoodGeometry.Box(
            "Shop roller shutter entrance",
            store.transform,
            new Vector3(-width * 0.28f, 1.38f, facadeZ),
            new Vector3(width * 0.36f, 2.7f, 0.16f),
            doorMaterial);
        NeighborhoodGeometry.Box(
            "Shop display window",
            store.transform,
            new Vector3(width * 0.22f, 1.55f, facadeZ + side * 0.03f),
            new Vector3(width * 0.43f, 1.72f, 0.13f),
            glassMaterial);
        NeighborhoodGeometry.Box(
            "Shop window frame",
            store.transform,
            new Vector3(width * 0.22f, 1.55f, facadeZ + side * 0.12f),
            new Vector3(width * 0.47f, 1.88f, 0.08f),
            frameMaterial);
        NeighborhoodGeometry.Box(
            "Illuminated Arabic shop sign",
            store.transform,
            new Vector3(0f, 3.15f, facadeZ + side * 0.12f),
            new Vector3(width, 0.82f, 0.2f),
            sign);
        NeighborhoodGeometry.Text(
            "Fictional Arabic shop name",
            store.transform,
            new Vector3(0f, 3.15f, facadeZ + side * 0.24f),
            Quaternion.Euler(side < 0 ? 180f : 0f, 0f, 0f),
            shop.fictionalName,
            0.14f,
            new Color(1f, 0.94f, 0.75f),
            38);
        NeighborhoodGeometry.Box(
            "Striped shop awning",
            store.transform,
            new Vector3(0f, 2.72f, facadeZ + side * 0.54f),
            new Vector3(width + 0.3f, 0.13f, 0.72f),
            paletteIndex % 2 == 0 ? accentMaterial : frameMaterial);

        for (int awningSide = -1; awningSide <= 1; awningSide += 2)
        {
            NeighborhoodGeometry.Box(
                "Awning support",
                store.transform,
                new Vector3(awningSide * width * 0.47f, 2.4f, facadeZ + side * 0.52f),
                new Vector3(0.1f, 0.68f, 0.1f),
                metalMaterialOrFrame());
        }

        if (index % 4 == 0)
        {
            GameObject fixture = new GameObject("Warm shopfront lighting");
            fixture.transform.SetParent(store.transform, false);
            fixture.transform.localPosition =
                new Vector3(0f, 3.72f, facadeZ + side * 0.75f);
            Light light = fixture.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.76f, 0.43f);
            light.intensity = 0.55f;
            light.range = storefrontLightRange;
            light.shadows = LightShadows.None;
        }

        if (shop.shopType == "خضار وفاكهة")
            CreateProduceCrates(store.transform, width, facadeZ, side);
        else if (shop.shopType == "ورشة ميكانيكا" || shop.shopType == "قطع غيار توك توك")
            CreateGarageDetail(store.transform, width, facadeZ, side);
        else if (shop.shopType == "كشك جرائد")
            NeighborhoodGeometry.Box(
                "Newspaper stand",
                store.transform,
                new Vector3(width * 0.54f, 0.68f, facadeZ + side * 0.65f),
                new Vector3(0.8f, 1.35f, 0.85f),
                woodMaterial);

        NeighborhoodGeometry.CombineVisuals(store.transform);
    }

    private Material metalMaterialOrFrame()
    {
        return accentMaterial != null ? accentMaterial : frameMaterial;
    }

    private void CreateProduceCrates(Transform store, float width, float facadeZ, int side)
    {
        for (int i = -1; i <= 1; i++)
        {
            NeighborhoodGeometry.Box(
                "Fruit and vegetable crate",
                store,
                new Vector3(i * 0.95f, 0.45f, facadeZ + side * 0.82f),
                new Vector3(0.82f, 0.58f, 0.62f),
                i % 2 == 0 ? accentMaterial : woodMaterial);
        }
    }

    private void CreateGarageDetail(Transform store, float width, float facadeZ, int side)
    {
        NeighborhoodGeometry.Box(
            "Garage roller shutter",
            store,
            new Vector3(width * 0.46f, 1.22f, facadeZ + side * 0.16f),
            new Vector3(width * 0.48f, 2.3f, 0.16f),
            metalMaterialOrFrame());
        NeighborhoodGeometry.Cylinder(
            "Spare tuk-tuk tire",
            store,
            new Vector3(width * 0.55f, 0.52f, facadeZ + side * 0.72f),
            new Vector3(0.18f, 0.42f, 0.42f),
            accentMaterial);
    }
}

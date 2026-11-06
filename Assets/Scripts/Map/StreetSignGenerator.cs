using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class StreetSignGenerator : MonoBehaviour
{
    [SerializeField, Min(2f)] private float signHeight = 4.1f;
    [SerializeField, Min(3f)] private float maximumBoardLength = 7.8f;
    [SerializeField, Min(0.5f)] private float intersectionClearance = 1f;
    [SerializeField, Min(2f)] private float minimumBoardLength = 3f;
    [SerializeField] private Material boardMaterial;
    [SerializeField] private Material postMaterial;
    [SerializeField] private Color letteringColor = new Color(1f, 0.94f, 0.72f);

    public void ConfigureMaterials(Material board, Material post)
    {
        boardMaterial = board;
        postMaterial = post;
    }

    public void Generate(Transform output, RoadGenerator roadGenerator)
    {
        Transform signs = new GameObject("Arabic street signs").transform;
        signs.SetParent(output, false);
        NeighborhoodRoad[] roads = roadGenerator.Roads;
        for (int i = 0; i < roads.Length; i++)
        {
            NeighborhoodRoad road = roads[i];
            if (string.IsNullOrWhiteSpace(road.name) || road.name == "حارة المدارس" ||
                road.name == "شارع المحطة")
                continue;

            if (!TryFindSignSpan(road, roadGenerator, out Vector2 signSpan))
            {
                Debug.LogWarning(
                    $"No clear sidewalk span is wide enough for a sign on '{road.name}'.",
                    this);
                continue;
            }

            for (int side = -1; side <= 1; side += 2)
                CreateRoadNameSign(signs, road, side, signSpan, roadGenerator);
        }
        NeighborhoodGeometry.CombineVisuals(signs);
    }

    private bool TryFindSignSpan(
        NeighborhoodRoad road,
        RoadGenerator roadGenerator,
        out Vector2 bestSpan)
    {
        NeighborhoodGenerator neighborhood = GetComponent<NeighborhoodGenerator>();
        Vector2 mapExtents = neighborhood != null
            ? neighborhood.MapHalfExtents
            : new Vector2(70f, 80f);
        float roadHalfExtent = road.runsAlongX ? mapExtents.x : mapExtents.y;
        float roadLength = Mathf.Min(road.length, (roadHalfExtent + 4f) * 2f);
        float alongCenter = road.runsAlongX ? road.center.x : road.center.z;
        float rangeStart = alongCenter - roadLength * 0.5f;
        float rangeEnd = alongCenter + roadLength * 0.5f;
        List<Vector2> blockedSpans = new List<Vector2>();
        NeighborhoodRoad[] roads = roadGenerator.Roads;

        for (int i = 0; i < roads.Length; i++)
        {
            NeighborhoodRoad crossing = roads[i];
            if (crossing.runsAlongX == road.runsAlongX)
                continue;

            float crossingCenter = road.runsAlongX
                ? crossing.center.x
                : crossing.center.z;
            float blockedRadius = crossing.width * 0.5f +
                                  minimumBoardLength * 0.5f +
                                  intersectionClearance;
            float blockedStart = Mathf.Max(rangeStart, crossingCenter - blockedRadius);
            float blockedEnd = Mathf.Min(rangeEnd, crossingCenter + blockedRadius);
            if (blockedStart < blockedEnd)
                blockedSpans.Add(new Vector2(blockedStart, blockedEnd));
        }

        blockedSpans.Sort((left, right) => left.x.CompareTo(right.x));
        float cursor = rangeStart;
        bestSpan = Vector2.zero;
        float bestLength = 0f;

        for (int i = 0; i <= blockedSpans.Count; i++)
        {
            float segmentEnd = i < blockedSpans.Count
                ? blockedSpans[i].x
                : rangeEnd;
            if (segmentEnd - cursor > bestLength)
            {
                bestSpan = new Vector2(cursor, segmentEnd);
                bestLength = segmentEnd - cursor;
            }

            if (i < blockedSpans.Count)
                cursor = Mathf.Max(cursor, blockedSpans[i].y);
        }

        return bestLength >= minimumBoardLength + intersectionClearance * 2f;
    }

    private void CreateRoadNameSign(
        Transform output,
        NeighborhoodRoad road,
        int side,
        Vector2 signSpan,
        RoadGenerator roadGenerator)
    {
        float along = (signSpan.x + signSpan.y) * 0.5f;
        float boardLength = Mathf.Min(
            maximumBoardLength,
            signSpan.y - signSpan.x - intersectionClearance * 2f);
        float lateralOffset = road.width * 0.5f + roadGenerator.SidewalkWidth * 0.5f;
        Vector3 position = road.runsAlongX
            ? new Vector3(along, 0f, road.center.z + side * lateralOffset)
            : new Vector3(road.center.x + side * lateralOffset, 0f, along);
        Vector3 boardSize = road.runsAlongX
            ? new Vector3(boardLength, 0.88f, 0.18f)
            : new Vector3(0.18f, 0.88f, boardLength);
        NeighborhoodGeometry.Box(
            "Arabic street-name signboard",
            output,
            position + Vector3.up * signHeight,
            boardSize,
            boardMaterial);

        Vector3 textPosition = position + Vector3.up * signHeight;
        Quaternion facing;
        if (road.runsAlongX)
        {
            facing = Quaternion.Euler(0f, side > 0 ? 180f : 0f, 0f);
            textPosition.z -= side * 0.12f;
        }
        else
        {
            facing = Quaternion.Euler(0f, side > 0 ? -90f : 90f, 0f);
            textPosition.x -= side * 0.12f;
        }

        float characterSize = Mathf.Min(0.14f, boardLength / 42f);
        NeighborhoodGeometry.Text(
            "Arabic street name — " + road.name,
            output,
            textPosition,
            facing,
            road.name,
            characterSize,
            letteringColor,
            44);

        NeighborhoodGeometry.Cylinder(
            "Street sign post",
            output,
            position + Vector3.up * (signHeight * 0.5f),
            new Vector3(0.1f, signHeight * 0.5f, 0.1f),
            postMaterial,
            true);
    }
}

public static class ArabicTextLayout
{
    private struct Forms
    {
        public readonly char isolated;
        public readonly char final;
        public readonly char initial;
        public readonly char medial;

        public Forms(char isolatedForm, char finalForm, char initialForm, char medialForm)
        {
            isolated = isolatedForm;
            final = finalForm;
            initial = initialForm;
            medial = medialForm;
        }

        public bool JoinsNext => initial != isolated;
        public bool JoinsPrevious => final != isolated;
    }

    public static string ShapeAndReverse(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        List<char> characters = new List<char>(text);
        for (int i = 0; i < characters.Count - 1; i++)
        {
            if (characters[i] != '\u0644')
                continue;

            char lamAlef = GetLamAlef(characters[i + 1]);
            if (lamAlef == '\0')
                continue;

            bool joinsPrevious = i > 0 &&
                                TryGetForms(characters[i - 1], out Forms previousForms) &&
                                previousForms.JoinsNext;
            characters[i] = joinsPrevious ? (char)(lamAlef + 1) : lamAlef;
            characters.RemoveAt(i + 1);
        }

        char[] shaped = new char[characters.Count];
        for (int i = 0; i < characters.Count; i++)
        {
            if (!TryGetForms(characters[i], out Forms forms))
            {
                shaped[i] = characters[i];
                continue;
            }

            bool joinsPrevious = i > 0 &&
                                TryGetForms(characters[i - 1], out Forms previous) &&
                                previous.JoinsNext &&
                                forms.JoinsPrevious;
            bool joinsNext = i + 1 < characters.Count &&
                             TryGetForms(characters[i + 1], out Forms next) &&
                             forms.JoinsNext &&
                             next.JoinsPrevious;

            shaped[i] = joinsPrevious && joinsNext
                ? forms.medial
                : joinsPrevious
                    ? forms.final
                    : joinsNext
                        ? forms.initial
                        : forms.isolated;
        }

        Array.Reverse(shaped);
        return new string(shaped);
    }

    private static char GetLamAlef(char alef)
    {
        switch (alef)
        {
            case '\u0622': return '\uFEF5';
            case '\u0623': return '\uFEF7';
            case '\u0625': return '\uFEF9';
            case '\u0627': return '\uFEFB';
            default: return '\0';
        }
    }

    private static bool TryGetForms(char letter, out Forms forms)
    {
        switch (letter)
        {
            case '\u0621': forms = new Forms('\uFE80', '\uFE80', '\uFE80', '\uFE80'); return true;
            case '\u0622': forms = new Forms('\uFE81', '\uFE82', '\uFE81', '\uFE82'); return true;
            case '\u0623': forms = new Forms('\uFE83', '\uFE84', '\uFE83', '\uFE84'); return true;
            case '\u0624': forms = new Forms('\uFE85', '\uFE86', '\uFE85', '\uFE86'); return true;
            case '\u0625': forms = new Forms('\uFE87', '\uFE88', '\uFE87', '\uFE88'); return true;
            case '\u0626': forms = new Forms('\uFE89', '\uFE8A', '\uFE8B', '\uFE8C'); return true;
            case '\u0627': forms = new Forms('\uFE8D', '\uFE8E', '\uFE8D', '\uFE8E'); return true;
            case '\u0628': forms = new Forms('\uFE8F', '\uFE90', '\uFE91', '\uFE92'); return true;
            case '\u0629': forms = new Forms('\uFE93', '\uFE94', '\uFE93', '\uFE94'); return true;
            case '\u062A': forms = new Forms('\uFE95', '\uFE96', '\uFE97', '\uFE98'); return true;
            case '\u062B': forms = new Forms('\uFE99', '\uFE9A', '\uFE9B', '\uFE9C'); return true;
            case '\u062C': forms = new Forms('\uFE9D', '\uFE9E', '\uFE9F', '\uFEA0'); return true;
            case '\u062D': forms = new Forms('\uFEA1', '\uFEA2', '\uFEA3', '\uFEA4'); return true;
            case '\u062E': forms = new Forms('\uFEA5', '\uFEA6', '\uFEA7', '\uFEA8'); return true;
            case '\u062F': forms = new Forms('\uFEA9', '\uFEAA', '\uFEA9', '\uFEAA'); return true;
            case '\u0630': forms = new Forms('\uFEAB', '\uFEAC', '\uFEAB', '\uFEAC'); return true;
            case '\u0631': forms = new Forms('\uFEAD', '\uFEAE', '\uFEAD', '\uFEAE'); return true;
            case '\u0632': forms = new Forms('\uFEAF', '\uFEB0', '\uFEAF', '\uFEB0'); return true;
            case '\u0633': forms = new Forms('\uFEB1', '\uFEB2', '\uFEB3', '\uFEB4'); return true;
            case '\u0634': forms = new Forms('\uFEB5', '\uFEB6', '\uFEB7', '\uFEB8'); return true;
            case '\u0635': forms = new Forms('\uFEB9', '\uFEBA', '\uFEBB', '\uFEBC'); return true;
            case '\u0636': forms = new Forms('\uFEBD', '\uFEBE', '\uFEBF', '\uFEC0'); return true;
            case '\u0637': forms = new Forms('\uFEC1', '\uFEC2', '\uFEC3', '\uFEC4'); return true;
            case '\u0638': forms = new Forms('\uFEC5', '\uFEC6', '\uFEC7', '\uFEC8'); return true;
            case '\u0639': forms = new Forms('\uFEC9', '\uFECA', '\uFECB', '\uFECC'); return true;
            case '\u063A': forms = new Forms('\uFECD', '\uFECE', '\uFECF', '\uFED0'); return true;
            case '\u0641': forms = new Forms('\uFED1', '\uFED2', '\uFED3', '\uFED4'); return true;
            case '\u0642': forms = new Forms('\uFED5', '\uFED6', '\uFED7', '\uFED8'); return true;
            case '\u0643': forms = new Forms('\uFED9', '\uFEDA', '\uFEDB', '\uFEDC'); return true;
            case '\u0644': forms = new Forms('\uFEDD', '\uFEDE', '\uFEDF', '\uFEE0'); return true;
            case '\u0645': forms = new Forms('\uFEE1', '\uFEE2', '\uFEE3', '\uFEE4'); return true;
            case '\u0646': forms = new Forms('\uFEE5', '\uFEE6', '\uFEE7', '\uFEE8'); return true;
            case '\u0647': forms = new Forms('\uFEE9', '\uFEEA', '\uFEEB', '\uFEEC'); return true;
            case '\u0648': forms = new Forms('\uFEED', '\uFEEE', '\uFEED', '\uFEEE'); return true;
            case '\u0649': forms = new Forms('\uFEEF', '\uFEF0', '\uFEEF', '\uFEF0'); return true;
            case '\u064A': forms = new Forms('\uFEF1', '\uFEF2', '\uFEF3', '\uFEF4'); return true;
            default:
                forms = default;
                return false;
        }
    }
}

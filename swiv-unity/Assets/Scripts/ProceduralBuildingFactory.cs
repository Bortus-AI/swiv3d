using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds SWIV-style structures from primitives with Intact + Fragments hierarchy
/// so ExplodeObject can fracture them on death.
/// </summary>
public static class ProceduralBuildingFactory {
    static Material sharedMat;
    static readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();

    public static GameObject Create(BuildingKind kind) {
        var root = new GameObject(kind.ToString());
        var intact = new GameObject("Intact");
        intact.transform.SetParent(root.transform, false);
        var fragments = new GameObject("Fragments");
        fragments.transform.SetParent(root.transform, false);

        Bounds bounds;
        switch (kind) {
            case BuildingKind.Apartment:
                bounds = BuildApartment(intact.transform);
                break;
            case BuildingKind.Warehouse:
                bounds = BuildWarehouse(intact.transform);
                break;
            case BuildingKind.Factory:
                bounds = BuildFactory(intact.transform);
                break;
            case BuildingKind.FuelTank:
                bounds = BuildFuelTank(intact.transform);
                break;
            case BuildingKind.Silo:
                bounds = BuildSilo(intact.transform);
                break;
            case BuildingKind.RadarTower:
                bounds = BuildRadarTower(intact.transform);
                break;
            case BuildingKind.CommsTower:
                bounds = BuildCommsTower(intact.transform);
                break;
            case BuildingKind.Bunker:
                bounds = BuildBunker(intact.transform);
                break;
            case BuildingKind.WaterTower:
                bounds = BuildWaterTower(intact.transform);
                break;
            case BuildingKind.Hangar:
                bounds = BuildHangar(intact.transform);
                break;
            default:
                bounds = BuildApartment(intact.transform);
                break;
        }

        DuplicateAsFragments(intact.transform, fragments.transform);
        fragments.SetActive(false);

        var damageable = root.AddComponent<Damageable>();
        damageable.SetMaxHealth(HealthFor(kind));

        var explode = root.AddComponent<ExplodeObject>();
        explode.Configure(
            intact.transform,
            CollectFragments(fragments.transform),
            EffectRadiusFor(kind),
            BlastColorFor(kind)
        );

        var box = root.AddComponent<BoxCollider>();
        Vector3 size = bounds.size;
        size.x = Mathf.Max(0.8f, size.x);
        size.y = Mathf.Max(0.8f, size.y);
        size.z = Mathf.Max(0.8f, size.z);
        box.size = size;
        box.center = bounds.center;

        return root;
    }

    public static float RandomScale(BuildingKind kind) {
        switch (kind) {
            case BuildingKind.Apartment: return Random.Range(6.5f, 9.5f);
            case BuildingKind.Warehouse: return Random.Range(6f, 9f);
            case BuildingKind.Factory: return Random.Range(5.5f, 8f);
            case BuildingKind.FuelTank: return Random.Range(4.5f, 7f);
            case BuildingKind.Silo: return Random.Range(5f, 7.5f);
            case BuildingKind.RadarTower: return Random.Range(5f, 7.5f);
            case BuildingKind.CommsTower: return Random.Range(5.5f, 8.5f);
            case BuildingKind.Bunker: return Random.Range(5f, 7f);
            case BuildingKind.WaterTower: return Random.Range(5f, 7.5f);
            case BuildingKind.Hangar: return Random.Range(7f, 10f);
            default: return Random.Range(5f, 7.5f);
        }
    }

    static float HealthFor(BuildingKind kind) {
        switch (kind) {
            case BuildingKind.Apartment: return 160f;
            case BuildingKind.Warehouse: return 140f;
            case BuildingKind.Factory: return 180f;
            case BuildingKind.FuelTank: return 90f;
            case BuildingKind.Silo: return 120f;
            case BuildingKind.RadarTower: return 100f;
            case BuildingKind.CommsTower: return 80f;
            case BuildingKind.Bunker: return 220f;
            case BuildingKind.WaterTower: return 110f;
            case BuildingKind.Hangar: return 200f;
            default: return 100f;
        }
    }

    static float EffectRadiusFor(BuildingKind kind) {
        switch (kind) {
            case BuildingKind.FuelTank: return 20f;
            case BuildingKind.Factory: return 18f;
            case BuildingKind.Hangar: return 18f;
            case BuildingKind.Apartment: return 16f;
            case BuildingKind.Bunker: return 15f;
            default: return 14f;
        }
    }

    static Color BlastColorFor(BuildingKind kind) {
        switch (kind) {
            case BuildingKind.FuelTank:
                return new Color(1f, 0.35f, 0.05f, 1f);
            case BuildingKind.Factory:
                return new Color(1f, 0.5f, 0.12f, 1f);
            case BuildingKind.CommsTower:
            case BuildingKind.RadarTower:
                return new Color(0.55f, 0.75f, 1f, 1f);
            default:
                return new Color(1f, 0.45f, 0.1f, 1f);
        }
    }

    // ---- type builders (local unit space, ~1–2 tall base) ----

    // All builders place the lowest face at local y = 0 so footing is reliable.

    static Bounds BuildApartment(Transform parent) {
        Color wall = new Color(0.55f, 0.5f, 0.42f);
        Color roof = new Color(0.35f, 0.32f, 0.3f);
        Color window = new Color(0.35f, 0.5f, 0.65f);
        // Cube is centered; y = halfHeight puts bottom on y=0.
        AddBox(parent, "Base", new Vector3(0f, 0.7f, 0f), new Vector3(1.6f, 1.4f, 1.2f), wall);
        AddBox(parent, "Mid", new Vector3(0f, 1.85f, 0f), new Vector3(1.55f, 0.9f, 1.15f), wall);
        AddBox(parent, "Top", new Vector3(0f, 2.75f, 0f), new Vector3(1.5f, 0.9f, 1.1f), wall);
        AddBox(parent, "Roof", new Vector3(0f, 3.35f, 0f), new Vector3(1.7f, 0.25f, 1.3f), roof);
        AddBox(parent, "WindowsF", new Vector3(0f, 1.4f, 0.61f), new Vector3(1.2f, 0.35f, 0.05f), window);
        AddBox(parent, "WindowsF2", new Vector3(0f, 2.4f, 0.58f), new Vector3(1.15f, 0.3f, 0.05f), window);
        return new Bounds(new Vector3(0f, 1.75f, 0f), new Vector3(1.7f, 3.5f, 1.3f));
    }

    static Bounds BuildWarehouse(Transform parent) {
        Color wall = new Color(0.5f, 0.45f, 0.38f);
        Color roof = new Color(0.4f, 0.25f, 0.18f);
        Color door = new Color(0.3f, 0.3f, 0.32f);
        AddBox(parent, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(2.4f, 1.1f, 1.4f), wall);
        AddBox(parent, "Roof", new Vector3(0f, 1.2f, 0f), new Vector3(2.6f, 0.2f, 1.55f), roof);
        AddBox(parent, "Door", new Vector3(0f, 0.45f, 0.72f), new Vector3(0.7f, 0.85f, 0.08f), door);
        return new Bounds(new Vector3(0f, 0.65f, 0f), new Vector3(2.6f, 1.4f, 1.55f));
    }

    static Bounds BuildFactory(Transform parent) {
        Color wall = new Color(0.48f, 0.48f, 0.5f);
        Color roof = new Color(0.3f, 0.3f, 0.32f);
        Color stack = new Color(0.4f, 0.28f, 0.25f);
        AddBox(parent, "Hall", new Vector3(0f, 0.6f, 0f), new Vector3(2f, 1.2f, 1.5f), wall);
        AddBox(parent, "Annex", new Vector3(1.1f, 0.4f, 0f), new Vector3(0.9f, 0.8f, 1.1f), wall);
        AddBox(parent, "Roof", new Vector3(0.2f, 1.3f, 0f), new Vector3(2.4f, 0.18f, 1.6f), roof);
        // Cylinder default height is 2; scale.y * 1 = half-extent along Y from center.
        AddCylinder(parent, "Stack", new Vector3(-0.55f, 1.8f, -0.3f), new Vector3(0.28f, 0.55f, 0.28f), stack);
        AddCylinder(parent, "Stack2", new Vector3(-0.15f, 1.55f, -0.35f), new Vector3(0.2f, 0.4f, 0.2f), stack);
        return new Bounds(new Vector3(0.2f, 1.2f, 0f), new Vector3(2.6f, 2.5f, 1.6f));
    }

    static Bounds BuildFuelTank(Transform parent) {
        Color tank = new Color(0.55f, 0.2f, 0.12f);
        Color band = new Color(0.75f, 0.75f, 0.7f);
        Color baseC = new Color(0.35f, 0.35f, 0.35f);
        // Cylinder: center.y = halfHeight so base sits at 0.
        AddCylinder(parent, "Base", new Vector3(0f, 0.08f, 0f), new Vector3(1.1f, 0.08f, 1.1f), baseC);
        AddCylinder(parent, "Tank", new Vector3(0f, 0.95f, 0f), new Vector3(1f, 0.85f, 1f), tank);
        AddCylinder(parent, "Band", new Vector3(0f, 0.95f, 0f), new Vector3(1.05f, 0.08f, 1.05f), band);
        AddCylinder(parent, "Cap", new Vector3(0f, 1.85f, 0f), new Vector3(0.95f, 0.1f, 0.95f), tank);
        return new Bounds(new Vector3(0f, 0.95f, 0f), new Vector3(1.2f, 2f, 1.2f));
    }

    static Bounds BuildSilo(Transform parent) {
        Color body = new Color(0.65f, 0.62f, 0.55f);
        Color roof = new Color(0.4f, 0.25f, 0.2f);
        Color baseC = new Color(0.4f, 0.4f, 0.4f);
        AddCylinder(parent, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(0.95f, 0.1f, 0.95f), baseC);
        AddCylinder(parent, "Body", new Vector3(0f, 1.2f, 0f), new Vector3(0.85f, 1.0f, 0.85f), body);
        AddCylinder(parent, "Cone", new Vector3(0f, 2.4f, 0f), new Vector3(0.9f, 0.25f, 0.9f), roof);
        return new Bounds(new Vector3(0f, 1.3f, 0f), new Vector3(1f, 2.7f, 1f));
    }

    static Bounds BuildRadarTower(Transform parent) {
        Color metal = new Color(0.55f, 0.58f, 0.6f);
        Color dish = new Color(0.75f, 0.78f, 0.8f);
        Color baseC = new Color(0.35f, 0.35f, 0.35f);
        AddBox(parent, "Pad", new Vector3(0f, 0.1f, 0f), new Vector3(1.1f, 0.2f, 1.1f), baseC);
        AddBox(parent, "Mast", new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 2.2f, 0.22f), metal);
        AddBox(parent, "CrossA", new Vector3(0f, 1.0f, 0f), new Vector3(1.0f, 0.08f, 0.08f), metal);
        AddBox(parent, "CrossB", new Vector3(0f, 1.6f, 0f), new Vector3(0.08f, 0.08f, 1.0f), metal);
        AddSphere(parent, "Dish", new Vector3(0f, 2.45f, 0.15f), new Vector3(1.1f, 0.25f, 1.1f), dish);
        AddBox(parent, "Arm", new Vector3(0f, 2.35f, 0f), new Vector3(0.12f, 0.12f, 0.5f), metal);
        return new Bounds(new Vector3(0f, 1.35f, 0f), new Vector3(1.2f, 2.8f, 1.2f));
    }

    static Bounds BuildCommsTower(Transform parent) {
        Color metal = new Color(0.7f, 0.55f, 0.2f);
        Color light = new Color(0.9f, 0.15f, 0.1f);
        // Legs: bottom at 0 → center y = half of 2.6 = 1.3
        AddBox(parent, "LegA", new Vector3(-0.25f, 1.3f, -0.25f), new Vector3(0.1f, 2.6f, 0.1f), metal);
        AddBox(parent, "LegB", new Vector3(0.25f, 1.3f, -0.25f), new Vector3(0.1f, 2.6f, 0.1f), metal);
        AddBox(parent, "LegC", new Vector3(-0.25f, 1.3f, 0.25f), new Vector3(0.1f, 2.6f, 0.1f), metal);
        AddBox(parent, "LegD", new Vector3(0.25f, 1.3f, 0.25f), new Vector3(0.1f, 2.6f, 0.1f), metal);
        AddBox(parent, "Brace1", new Vector3(0f, 0.8f, 0f), new Vector3(0.7f, 0.06f, 0.7f), metal);
        AddBox(parent, "Brace2", new Vector3(0f, 1.6f, 0f), new Vector3(0.6f, 0.06f, 0.6f), metal);
        AddBox(parent, "Brace3", new Vector3(0f, 2.3f, 0f), new Vector3(0.5f, 0.06f, 0.5f), metal);
        AddSphere(parent, "Beacon", new Vector3(0f, 2.75f, 0f), new Vector3(0.25f, 0.25f, 0.25f), light);
        return new Bounds(new Vector3(0f, 1.4f, 0f), new Vector3(0.8f, 2.9f, 0.8f));
    }

    static Bounds BuildBunker(Transform parent) {
        Color concrete = new Color(0.45f, 0.48f, 0.42f);
        Color door = new Color(0.25f, 0.28f, 0.25f);
        Color dirt = new Color(0.35f, 0.32f, 0.22f);
        AddBox(parent, "Mound", new Vector3(0f, 0.25f, 0f), new Vector3(2.2f, 0.5f, 1.8f), dirt);
        AddBox(parent, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(1.6f, 0.7f, 1.2f), concrete);
        AddBox(parent, "Entry", new Vector3(0f, 0.4f, 0.75f), new Vector3(0.7f, 0.55f, 0.5f), concrete);
        AddBox(parent, "Door", new Vector3(0f, 0.38f, 1.0f), new Vector3(0.4f, 0.45f, 0.08f), door);
        return new Bounds(new Vector3(0f, 0.45f, 0.1f), new Vector3(2.2f, 1f, 2.1f));
    }

    static Bounds BuildWaterTower(Transform parent) {
        Color metal = new Color(0.55f, 0.6f, 0.65f);
        Color tank = new Color(0.35f, 0.55f, 0.7f);
        // Legs bottom at 0: center y = 0.9, scale y = 1.8
        AddBox(parent, "LegA", new Vector3(-0.35f, 0.9f, -0.35f), new Vector3(0.12f, 1.8f, 0.12f), metal);
        AddBox(parent, "LegB", new Vector3(0.35f, 0.9f, -0.35f), new Vector3(0.12f, 1.8f, 0.12f), metal);
        AddBox(parent, "LegC", new Vector3(-0.35f, 0.9f, 0.35f), new Vector3(0.12f, 1.8f, 0.12f), metal);
        AddBox(parent, "LegD", new Vector3(0.35f, 0.9f, 0.35f), new Vector3(0.12f, 1.8f, 0.12f), metal);
        AddBox(parent, "Cross", new Vector3(0f, 0.9f, 0f), new Vector3(0.85f, 0.08f, 0.85f), metal);
        AddCylinder(parent, "Tank", new Vector3(0f, 2.1f, 0f), new Vector3(1.0f, 0.4f, 1.0f), tank);
        AddCylinder(parent, "Cap", new Vector3(0f, 2.55f, 0f), new Vector3(0.7f, 0.12f, 0.7f), metal);
        return new Bounds(new Vector3(0f, 1.4f, 0f), new Vector3(1.2f, 2.8f, 1.2f));
    }

    static Bounds BuildHangar(Transform parent) {
        Color wall = new Color(0.5f, 0.52f, 0.48f);
        Color roof = new Color(0.35f, 0.38f, 0.4f);
        Color door = new Color(0.3f, 0.32f, 0.35f);
        AddBox(parent, "Body", new Vector3(0f, 0.7f, 0f), new Vector3(2.8f, 1.4f, 2.0f), wall);
        AddBox(parent, "Roof", new Vector3(0f, 1.5f, 0f), new Vector3(3.0f, 0.25f, 2.15f), roof);
        AddBox(parent, "DoorL", new Vector3(-0.55f, 0.65f, 1.05f), new Vector3(0.9f, 1.2f, 0.1f), door);
        AddBox(parent, "DoorR", new Vector3(0.55f, 0.65f, 1.05f), new Vector3(0.9f, 1.2f, 0.1f), door);
        return new Bounds(new Vector3(0f, 0.85f, 0f), new Vector3(3f, 1.75f, 2.2f));
    }

    // ---- helpers ----

    static void DuplicateAsFragments(Transform intact, Transform fragmentsRoot) {
        for (int i = 0; i < intact.childCount; i++) {
            Transform src = intact.GetChild(i);
            GameObject clone = Object.Instantiate(src.gameObject, fragmentsRoot);
            clone.name = src.name + "_frag";
            clone.transform.localPosition = src.localPosition;
            clone.transform.localRotation = src.localRotation;
            clone.transform.localScale = src.localScale;

            // Ensure each debris piece has a collider for tumbling.
            if (clone.GetComponent<Collider>() == null) {
                var mf = clone.GetComponent<MeshFilter>();
                if (mf != null) {
                    clone.AddComponent<BoxCollider>();
                }
            }
        }
    }

    static Transform[] CollectFragments(Transform fragmentsRoot) {
        var list = new List<Transform>(fragmentsRoot.childCount);
        for (int i = 0; i < fragmentsRoot.childCount; i++) {
            list.Add(fragmentsRoot.GetChild(i));
        }
        return list.ToArray();
    }

    static void AddBox(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        // Colliders on intact pieces fight the root hitbox; remove them.
        Object.Destroy(go.GetComponent<Collider>());
    }

    static void AddCylinder(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        Object.Destroy(go.GetComponent<Collider>());
    }

    static void AddSphere(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = scale;
        ApplyColor(go, color);
        Object.Destroy(go.GetComponent<Collider>());
    }

    static void ApplyColor(GameObject go, Color color) {
        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null) {
            return;
        }
        renderer.sharedMaterial = GetMaterial(color);
    }

    static Material GetMaterial(Color color) {
        // Quantize color slightly so cache hits more often.
        Color key = new Color(
            Mathf.Round(color.r * 32f) / 32f,
            Mathf.Round(color.g * 32f) / 32f,
            Mathf.Round(color.b * 32f) / 32f,
            1f
        );
        if (matCache.TryGetValue(key, out Material cached) && cached != null) {
            return cached;
        }

        Shader shader = Shader.Find("Standard");
        if (shader == null) {
            shader = Shader.Find("Diffuse");
        }
        if (shader == null) {
            shader = Shader.Find("Sprites/Default");
        }
        var mat = new Material(shader);
        if (mat.HasProperty("_Color")) {
            mat.color = key;
        }
        if (mat.HasProperty("_BaseColor")) {
            mat.SetColor("_BaseColor", key);
        }
        matCache[key] = mat;
        return mat;
    }
}

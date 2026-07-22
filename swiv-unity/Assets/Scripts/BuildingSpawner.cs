using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Populates the map with mixed SWIV-style destructible buildings snapped to terrain.
/// Centers on the player helicopter, keeps a clear zone around the start, and
/// mixes the House001 prefab with procedural structures (factory, silo, radar, etc.).
/// </summary>
public class BuildingSpawner : MonoBehaviour {
    [Header("Prefab")]
    [SerializeField] GameObject buildingPrefab;
    [SerializeField] string resourcesPrefabPath = "Prefabs/House001";
    [Tooltip("Chance a spawned building uses the House001 prefab vs procedural types.")]
    [Range(0f, 1f)]
    [SerializeField] float housePrefabChance = 0.28f;

    [Header("Placement area")]
    [Tooltip("Used only if no helicopter is found at runtime.")]
    [SerializeField] Vector3 areaCenter = new Vector3(684f, 0f, 3160f);
    [SerializeField] bool centerOnPlayer = true;
    [SerializeField] float areaRadius = 500f;
    [Tooltip("No buildings inside this radius of the player start.")]
    [SerializeField] float minDistanceFromPlayer = 140f;
    [SerializeField] int settlementCount = 6;
    [SerializeField] int buildingsPerSettlementMin = 3;
    [SerializeField] int buildingsPerSettlementMax = 7;
    [SerializeField] int scatteredBuildingCount = 22;
    [Tooltip("Optional far ring of landmarks (0 = off). Kept well outside the start clear zone.")]
    [SerializeField] int nearbyRingCount = 0;
    [SerializeField] float nearbyRingRadius = 180f;
    [SerializeField] float minBuildingSpacing = 28f;
    [SerializeField] float settlementRadius = 45f;

    [Header("Terrain fit")]
    [SerializeField] float maxSlopeDegrees = 35f;
    [Tooltip("Small sink into the ground so foundations don't hover on bumpy terrain.")]
    [SerializeField] float groundSink = 0.25f;
    [SerializeField] float raycastHeight = 800f;
    [SerializeField] int maxPlacementAttempts = 60;
    [SerializeField] int randomSeed = 1337;
    [SerializeField] bool useRandomSeed = true;
    [Tooltip("Parent buildings to the active terrain so LoadTerrain tile shifts keep them planted.")]
    [SerializeField] bool parentToTerrain = true;

    [Header("Variety")]
    [SerializeField] Vector2 houseScaleRange = new Vector2(5f, 7.5f);
    [SerializeField] bool randomYaw = true;

    static readonly BuildingKind[] ProceduralKinds = {
        BuildingKind.Apartment,
        BuildingKind.Warehouse,
        BuildingKind.Factory,
        BuildingKind.FuelTank,
        BuildingKind.Silo,
        BuildingKind.RadarTower,
        BuildingKind.CommsTower,
        BuildingKind.Bunker,
        BuildingKind.WaterTower,
        BuildingKind.Hangar
    };

    // Relative spawn weights — industrial / military feel of classic SWIV maps.
    static readonly float[] ProceduralWeights = {
        1.1f, // Apartment
        1.3f, // Warehouse
        1.4f, // Factory
        1.0f, // FuelTank
        1.0f, // Silo
        0.7f, // RadarTower
        0.6f, // CommsTower
        0.9f, // Bunker
        0.7f, // WaterTower
        0.8f  // Hangar
    };

    readonly List<Vector3> placedPositions = new List<Vector3>();
    Transform buildingsRoot;
    int spawnedCount;

    void Start() {
        StartCoroutine(SpawnWhenReady());
    }

    IEnumerator SpawnWhenReady() {
        // Wait for terrain colliders + LoadTerrain first reposition.
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();
        yield return null;
        // Extra frames: LoadTerrain updates in Update and may shift the tile once.
        yield return null;
        yield return new WaitForFixedUpdate();

        if (useRandomSeed) {
            Random.InitState(randomSeed);
        }

        if (buildingPrefab == null) {
            buildingPrefab = Resources.Load<GameObject>(resourcesPrefabPath);
        }

        ResolveAreaCenter();

        buildingsRoot = new GameObject("Buildings").transform;
        buildingsRoot.position = Vector3.zero;
        buildingsRoot.rotation = Quaternion.identity;
        buildingsRoot.localScale = Vector3.one;

        foreach (var existing in FindObjectsOfType<ExplodeObject>()) {
            if (existing != null) {
                placedPositions.Add(Flatten(existing.transform.position));
            }
        }
        int preExisting = placedPositions.Count;

        SpawnFarLandmarkRing();
        SpawnSettlements();
        SpawnScattered();

        // Final snap after physics/terrain settle — uses terrain height, never self-colliders.
        yield return new WaitForFixedUpdate();
        yield return null;
        ResnapAllSpawned();

        Debug.Log(
            "BuildingSpawner: spawned " + spawnedCount +
            " buildings (" + preExisting + " pre-existing). Center=" + areaCenter +
            " clearZone=" + minDistanceFromPlayer +
            " firstSpawnDist=" + FirstSpawnDistanceToCenter().ToString("F1")
        );
    }

    void ResolveAreaCenter() {
        if (!centerOnPlayer) {
            return;
        }

        var heli = FindObjectOfType<HelicopterMovement>();
        if (heli != null) {
            areaCenter = heli.transform.position;
            return;
        }

        var weapons = FindObjectOfType<PlayerWeapons>();
        if (weapons != null) {
            areaCenter = weapons.transform.position;
        }
    }

    void SpawnFarLandmarkRing() {
        if (nearbyRingCount <= 0) {
            return;
        }

        float ringR = Mathf.Max(nearbyRingRadius, minDistanceFromPlayer + 20f);
        for (int i = 0; i < nearbyRingCount; i++) {
            float angle = (i / (float)nearbyRingCount) * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
            float radius = ringR + Random.Range(0f, 40f);
            Vector3 probe = areaCenter + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (SampleGround(probe, out Vector3 ground, out float slope) && slope <= maxSlopeDegrees + 10f) {
                if (IsOutsideClearZone(ground) && IsFarEnough(ground, minBuildingSpacing)) {
                    SpawnBuilding(ground);
                }
            }
        }
    }

    void SpawnSettlements() {
        for (int s = 0; s < settlementCount; s++) {
            Vector3 hub;
            if (!TryFindFlatSpot(areaCenter, areaRadius * 0.9f, out hub, enforceClearZone: true)) {
                continue;
            }

            int count = Random.Range(buildingsPerSettlementMin, buildingsPerSettlementMax + 1);
            for (int i = 0; i < count; i++) {
                Vector3 candidate;
                if (!TryFindFlatSpot(hub, settlementRadius, out candidate, enforceClearZone: true)) {
                    continue;
                }
                SpawnBuilding(candidate);
            }
        }
    }

    void SpawnScattered() {
        for (int i = 0; i < scatteredBuildingCount; i++) {
            Vector3 candidate;
            if (!TryFindFlatSpot(areaCenter, areaRadius, out candidate, enforceClearZone: true)) {
                continue;
            }
            SpawnBuilding(candidate);
        }
    }

    bool TryFindFlatSpot(Vector3 center, float radius, out Vector3 worldPos, bool enforceClearZone) {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++) {
            Vector2 offset = Random.insideUnitCircle * radius;
            if (enforceClearZone && minDistanceFromPlayer > 0f && radius > minDistanceFromPlayer) {
                float minR = minDistanceFromPlayer / Mathf.Max(radius, 0.01f);
                float t = Mathf.Sqrt(Random.Range(minR * minR, 1f));
                offset = Random.insideUnitCircle.normalized * (t * radius);
            }

            Vector3 probe = new Vector3(center.x + offset.x, center.y, center.z + offset.y);

            if (!SampleGround(probe, out Vector3 ground, out float slopeDegrees)) {
                continue;
            }
            if (slopeDegrees > maxSlopeDegrees) {
                continue;
            }
            if (enforceClearZone && !IsOutsideClearZone(ground)) {
                continue;
            }
            if (!IsFarEnough(ground, minBuildingSpacing)) {
                continue;
            }

            worldPos = ground;
            return true;
        }

        worldPos = Vector3.zero;
        return false;
    }

    bool IsOutsideClearZone(Vector3 pos) {
        if (minDistanceFromPlayer <= 0f) {
            return true;
        }
        float dist = Vector3.Distance(Flatten(pos), Flatten(areaCenter));
        return dist >= minDistanceFromPlayer;
    }

    /// <summary>
    /// Sample terrain height at XZ. Prefer Terrain.SampleHeight (never hits building colliders).
    /// Raycast fallback skips player and other buildings.
    /// </summary>
    bool SampleGround(Vector3 above, out Vector3 ground, out float slopeDegrees) {
        ground = above;
        slopeDegrees = 90f;

        Terrain terrain = FindBestTerrain(above);
        if (terrain != null && terrain.terrainData != null) {
            Vector3 tpos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = (above.x - tpos.x) / size.x;
            float nz = (above.z - tpos.z) / size.z;
            if (nx >= 0f && nx <= 1f && nz >= 0f && nz <= 1f) {
                float y = terrain.SampleHeight(above) + tpos.y;
                ground = new Vector3(above.x, y, above.z);
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
                slopeDegrees = Vector3.Angle(normal, Vector3.up);
                return true;
            }
        }

        // Fallback: physics raycast, but skip vehicles and buildings.
        Vector3 origin = new Vector3(above.x, Mathf.Max(above.y, 0f) + raycastHeight, above.z);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, raycastHeight * 3f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        for (int i = 0; i < hits.Length; i++) {
            Collider col = hits[i].collider;
            if (col == null) {
                continue;
            }
            if (col.GetComponentInParent<HelicopterMovement>() != null) {
                continue;
            }
            if (col.GetComponentInParent<ExplodeObject>() != null) {
                continue;
            }
            if (col.GetComponentInParent<Damageable>() != null) {
                continue;
            }
            ground = hits[i].point;
            slopeDegrees = Vector3.Angle(hits[i].normal, Vector3.up);
            return true;
        }

        return false;
    }

    Terrain FindBestTerrain(Vector3 worldPos) {
        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains != null && terrains.Length > 0) {
            Terrain best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < terrains.Length; i++) {
                Terrain t = terrains[i];
                if (t == null || t.terrainData == null) {
                    continue;
                }
                Vector3 tpos = t.transform.position;
                Vector3 size = t.terrainData.size;
                // Prefer a terrain that actually contains this XZ.
                float nx = (worldPos.x - tpos.x) / size.x;
                float nz = (worldPos.z - tpos.z) / size.z;
                if (nx >= 0f && nx <= 1f && nz >= 0f && nz <= 1f) {
                    return t;
                }
                Vector3 c = tpos + size * 0.5f;
                float d = (Flatten(c) - Flatten(worldPos)).sqrMagnitude;
                if (d < bestDist) {
                    bestDist = d;
                    best = t;
                }
            }
            if (best != null) {
                return best;
            }
        }

        return Terrain.activeTerrain;
    }

    bool IsFarEnough(Vector3 pos, float minSpacing) {
        float minSq = minSpacing * minSpacing;
        Vector3 flat = Flatten(pos);
        for (int i = 0; i < placedPositions.Count; i++) {
            if ((placedPositions[i] - flat).sqrMagnitude < minSq) {
                return false;
            }
        }
        return true;
    }

    void SpawnBuilding(Vector3 position) {
        float yaw = randomYaw ? Random.Range(0f, 360f) : 0f;
        bool useHouse = buildingPrefab != null && Random.value < housePrefabChance;

        GameObject building;
        float scale;
        if (useHouse) {
            scale = Random.Range(houseScaleRange.x, houseScaleRange.y);
            building = Instantiate(buildingPrefab);
            building.name = "House_" + spawnedCount;
            EnsureGameplayComponents(building);
            EnsureVisible(building);
        } else {
            BuildingKind kind = PickProceduralKind();
            building = ProceduralBuildingFactory.Create(kind);
            building.name = kind + "_" + spawnedCount;
            scale = ProceduralBuildingFactory.RandomScale(kind);
        }

        building.SetActive(true);
        // Apply scale/rotation before measuring visual bounds for footing.
        building.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        building.transform.localScale = Vector3.one * scale;
        building.transform.position = position;

        if (buildingsRoot != null) {
            building.transform.SetParent(buildingsRoot, true);
        }

        PlantOnTerrain(building);

        placedPositions.Add(Flatten(building.transform.position));
        spawnedCount++;
    }

    /// <summary>
    /// Puts the building's visual bottom on the terrain surface (handles mesh pivot offsets).
    /// Optionally parents to terrain so LoadTerrain tile shifts don't leave it floating.
    /// </summary>
    void PlantOnTerrain(GameObject building) {
        if (building == null) {
            return;
        }

        Vector3 pos = building.transform.position;
        if (!SampleGround(pos, out Vector3 ground, out _)) {
            return;
        }

        // Rough place at terrain height first.
        building.transform.position = new Vector3(pos.x, ground.y, pos.z);

        // Align the lowest visible mesh point to the ground (fixes House001Intact y=0.4 pivot etc.).
        if (TryGetVisualBottom(building, out float bottomY)) {
            float lift = ground.y - bottomY - groundSink;
            building.transform.position += Vector3.up * lift;
        } else {
            building.transform.position = new Vector3(pos.x, ground.y - groundSink, pos.z);
        }

        if (parentToTerrain) {
            Terrain terrain = FindBestTerrain(building.transform.position);
            if (terrain != null) {
                building.transform.SetParent(terrain.transform, true);
            }
        }
    }

    static bool TryGetVisualBottom(GameObject building, out float bottomY) {
        bottomY = 0f;
        var renderers = building.GetComponentsInChildren<Renderer>(true);
        bool any = false;
        float minY = float.MaxValue;

        for (int i = 0; i < renderers.Length; i++) {
            Renderer r = renderers[i];
            if (r == null || !r.enabled) {
                continue;
            }
            // Skip fragment meshes that are inactive / under Fragments root.
            if (!r.gameObject.activeInHierarchy) {
                continue;
            }
            Transform t = r.transform;
            if (IsUnderFragments(t)) {
                continue;
            }

            Bounds b = r.bounds;
            if (b.min.y < minY) {
                minY = b.min.y;
                any = true;
            }
        }

        if (!any) {
            return false;
        }
        bottomY = minY;
        return true;
    }

    static bool IsUnderFragments(Transform t) {
        while (t != null) {
            string n = t.name;
            if (n.IndexOf("Fragment", System.StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
            t = t.parent;
        }
        return false;
    }

    BuildingKind PickProceduralKind() {
        float total = 0f;
        for (int i = 0; i < ProceduralWeights.Length; i++) {
            total += ProceduralWeights[i];
        }
        float roll = Random.value * total;
        float acc = 0f;
        for (int i = 0; i < ProceduralKinds.Length; i++) {
            acc += ProceduralWeights[i];
            if (roll <= acc) {
                return ProceduralKinds[i];
            }
        }
        return ProceduralKinds[ProceduralKinds.Length - 1];
    }

    void EnsureVisible(GameObject building) {
        Transform intact = building.transform.Find("House001Intact");
        if (intact == null) {
            intact = building.transform.Find("Intact");
        }
        if (intact != null) {
            intact.gameObject.SetActive(true);
            var renderers = intact.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].enabled = true;
            }
        }

        Transform frags = building.transform.Find("House001Fragments");
        if (frags == null) {
            frags = building.transform.Find("Fragments");
        }
        if (frags != null) {
            frags.gameObject.SetActive(false);
        }

        building.SetActive(true);
        foreach (Transform child in building.transform) {
            if (child.name.Contains("Intact")) {
                child.gameObject.SetActive(true);
            }
        }
    }

    void EnsureGameplayComponents(GameObject building) {
        if (building.GetComponent<Damageable>() == null) {
            building.AddComponent<Damageable>();
        }
        if (building.GetComponent<ExplodeObject>() == null) {
            building.AddComponent<ExplodeObject>();
        }
        if (building.GetComponent<Collider>() == null) {
            var box = building.AddComponent<BoxCollider>();
            box.size = new Vector3(2.2f, 2.2f, 2.2f);
            box.center = new Vector3(0f, 0.9f, 0f);
        }
    }

    void ResnapAllSpawned() {
        // Buildings may live under Buildings root or under Terrain.
        var explodeObjects = FindObjectsOfType<ExplodeObject>();
        for (int i = 0; i < explodeObjects.Length; i++) {
            var exp = explodeObjects[i];
            if (exp == null) {
                continue;
            }
            // Only resnap things we spawned (or houses with Damageable).
            string n = exp.gameObject.name;
            bool ours = n.StartsWith("House") ||
                        n.StartsWith("Apartment") ||
                        n.StartsWith("Warehouse") ||
                        n.StartsWith("Factory") ||
                        n.StartsWith("FuelTank") ||
                        n.StartsWith("Silo") ||
                        n.StartsWith("RadarTower") ||
                        n.StartsWith("CommsTower") ||
                        n.StartsWith("Bunker") ||
                        n.StartsWith("WaterTower") ||
                        n.StartsWith("Hangar");
            if (!ours && buildingsRoot != null && exp.transform.IsChildOf(buildingsRoot)) {
                ours = true;
            }
            if (!ours) {
                continue;
            }
            if (n.StartsWith("House")) {
                EnsureVisible(exp.gameObject);
            }
            PlantOnTerrain(exp.gameObject);
        }
    }

    static Vector3 Flatten(Vector3 v) {
        return new Vector3(v.x, 0f, v.z);
    }

    float FirstSpawnDistanceToCenter() {
        var explodeObjects = FindObjectsOfType<ExplodeObject>();
        float best = float.MaxValue;
        int count = 0;
        for (int i = 0; i < explodeObjects.Length; i++) {
            if (explodeObjects[i] == null) {
                continue;
            }
            count++;
            float d = Vector3.Distance(Flatten(explodeObjects[i].transform.position), Flatten(areaCenter));
            if (d < best) {
                best = d;
            }
        }
        return count == 0 ? -1f : best;
    }
}

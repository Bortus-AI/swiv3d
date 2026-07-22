using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Populates the map with destructible buildings (House001) snapped to terrain.
/// Centers on the player helicopter so buildings always appear in the play area.
/// </summary>
public class BuildingSpawner : MonoBehaviour {
    [Header("Prefab")]
    [SerializeField] GameObject buildingPrefab;
    [SerializeField] string resourcesPrefabPath = "Prefabs/House001";

    [Header("Placement area")]
    [Tooltip("Used only if no helicopter is found at runtime.")]
    [SerializeField] Vector3 areaCenter = new Vector3(684f, 0f, 3160f);
    [SerializeField] bool centerOnPlayer = true;
    [SerializeField] float areaRadius = 450f;
    [SerializeField] int settlementCount = 7;
    [SerializeField] int buildingsPerSettlementMin = 4;
    [SerializeField] int buildingsPerSettlementMax = 9;
    [SerializeField] int scatteredBuildingCount = 28;
    [SerializeField] int nearbyRingCount = 12;
    [SerializeField] float nearbyRingRadius = 55f;
    [SerializeField] float minBuildingSpacing = 24f;
    [SerializeField] float settlementRadius = 50f;

    [Header("Terrain fit")]
    [SerializeField] float maxSlopeDegrees = 35f;
    [SerializeField] float yOffset = 0.15f;
    [SerializeField] float raycastHeight = 800f;
    [SerializeField] int maxPlacementAttempts = 50;
    [SerializeField] int randomSeed = 1337;
    [SerializeField] bool useRandomSeed = true;

    [Header("Variety")]
    [SerializeField] Vector2 scaleRange = new Vector2(5f, 7.5f);
    [SerializeField] bool randomYaw = true;

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

        if (useRandomSeed) {
            Random.InitState(randomSeed);
        }

        if (buildingPrefab == null) {
            buildingPrefab = Resources.Load<GameObject>(resourcesPrefabPath);
        }
        if (buildingPrefab == null) {
            Debug.LogError("BuildingSpawner: no building prefab assigned or found at Resources/" + resourcesPrefabPath);
            yield break;
        }

        ResolveAreaCenter();

        buildingsRoot = new GameObject("Buildings").transform;
        // Keep root at world origin so child world positions stay simple.
        buildingsRoot.position = Vector3.zero;
        buildingsRoot.rotation = Quaternion.identity;
        buildingsRoot.localScale = Vector3.one;

        foreach (var existing in FindObjectsOfType<ExplodeObject>()) {
            if (existing != null) {
                placedPositions.Add(Flatten(existing.transform.position));
            }
        }
        int preExisting = placedPositions.Count;

        // Guaranteed nearby buildings so something is always visible around the heli.
        SpawnNearbyRing();
        SpawnSettlements();
        SpawnScattered();

        // Second-pass snap: terrain may have shifted one more frame after first placements.
        yield return new WaitForFixedUpdate();
        ResnapAllSpawned();

        Debug.Log(
            "BuildingSpawner: spawned " + spawnedCount +
            " buildings (" + preExisting + " pre-existing). Center=" + areaCenter +
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

    void SpawnNearbyRing() {
        for (int i = 0; i < nearbyRingCount; i++) {
            float angle = (i / (float)nearbyRingCount) * Mathf.PI * 2f;
            // Two radii for a denser near field.
            float radius = (i % 2 == 0) ? nearbyRingRadius : nearbyRingRadius * 1.7f;
            Vector3 probe = areaCenter + new Vector3(Mathf.Cos(angle) * radius, raycastHeight, Mathf.Sin(angle) * radius);
            if (SampleGround(probe, out Vector3 ground, out float slope) && slope <= maxSlopeDegrees + 10f) {
                // Nearby ring is allowed slightly closer together.
                if (IsFarEnough(ground, minBuildingSpacing * 0.65f)) {
                    SpawnBuilding(ground);
                }
            }
        }
    }

    void SpawnSettlements() {
        for (int s = 0; s < settlementCount; s++) {
            Vector3 hub;
            if (!TryFindFlatSpot(areaCenter, areaRadius * 0.85f, out hub)) {
                continue;
            }

            int count = Random.Range(buildingsPerSettlementMin, buildingsPerSettlementMax + 1);
            for (int i = 0; i < count; i++) {
                Vector3 candidate;
                if (!TryFindFlatSpot(hub, settlementRadius, out candidate)) {
                    continue;
                }
                SpawnBuilding(candidate);
            }
        }
    }

    void SpawnScattered() {
        for (int i = 0; i < scatteredBuildingCount; i++) {
            Vector3 candidate;
            if (!TryFindFlatSpot(areaCenter, areaRadius, out candidate)) {
                continue;
            }
            SpawnBuilding(candidate);
        }
    }

    bool TryFindFlatSpot(Vector3 center, float radius, out Vector3 worldPos) {
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++) {
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 probe = new Vector3(center.x + offset.x, center.y + raycastHeight, center.z + offset.y);

            if (!SampleGround(probe, out Vector3 ground, out float slopeDegrees)) {
                continue;
            }
            if (slopeDegrees > maxSlopeDegrees) {
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

    bool SampleGround(Vector3 above, out Vector3 ground, out float slopeDegrees) {
        ground = above;
        slopeDegrees = 90f;

        // 1) Physics raycast against terrain / world colliders.
        Vector3 origin = new Vector3(above.x, Mathf.Max(above.y, raycastHeight), above.z);
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, raycastHeight * 3f, ~0, QueryTriggerInteraction.Ignore)) {
            // Ignore hits on the player vehicle.
            if (hit.collider != null && hit.collider.GetComponentInParent<HelicopterMovement>() != null) {
                // fall through to terrain sample
            } else {
                ground = hit.point + Vector3.up * yOffset;
                slopeDegrees = Vector3.Angle(hit.normal, Vector3.up);
                return true;
            }
        }

        // 2) Terrain height sample (works even if collider is briefly unavailable).
        Terrain terrain = FindBestTerrain(above);
        if (terrain != null && terrain.terrainData != null) {
            Vector3 tpos = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            float nx = (above.x - tpos.x) / size.x;
            float nz = (above.z - tpos.z) / size.z;
            if (nx >= 0f && nx <= 1f && nz >= 0f && nz <= 1f) {
                float y = terrain.SampleHeight(above) + tpos.y + yOffset;
                ground = new Vector3(above.x, y, above.z);
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(nx, nz);
                slopeDegrees = Vector3.Angle(normal, Vector3.up);
                return true;
            }
        }

        return false;
    }

    Terrain FindBestTerrain(Vector3 worldPos) {
        Terrain active = Terrain.activeTerrain;
        if (active != null) {
            return active;
        }

        Terrain[] terrains = Terrain.activeTerrains;
        if (terrains == null || terrains.Length == 0) {
            return null;
        }

        Terrain best = terrains[0];
        float bestDist = float.MaxValue;
        for (int i = 0; i < terrains.Length; i++) {
            Terrain t = terrains[i];
            if (t == null) {
                continue;
            }
            Vector3 c = t.transform.position + t.terrainData.size * 0.5f;
            float d = (Flatten(c) - Flatten(worldPos)).sqrMagnitude;
            if (d < bestDist) {
                bestDist = d;
                best = t;
            }
        }
        return best;
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
        float scale = Random.Range(scaleRange.x, scaleRange.y);

        // Instantiate unparented first so position is unambiguous world space.
        GameObject building = Instantiate(buildingPrefab);
        building.name = "Building_" + spawnedCount;
        building.SetActive(true);
        building.transform.position = position;
        building.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        building.transform.localScale = Vector3.one * scale;
        if (buildingsRoot != null) {
            building.transform.SetParent(buildingsRoot, true);
        }

        EnsureVisible(building);
        EnsureGameplayComponents(building);

        placedPositions.Add(Flatten(position));
        spawnedCount++;
    }

    void EnsureVisible(GameObject building) {
        // Intact mesh must be on; fragments stay off until explode.
        Transform intact = building.transform.Find("House001Intact");
        if (intact != null) {
            intact.gameObject.SetActive(true);
            var renderers = intact.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) {
                renderers[i].enabled = true;
            }
        }

        Transform frags = building.transform.Find("House001Fragments");
        if (frags != null) {
            frags.gameObject.SetActive(false);
        }

        // Make sure nothing on the root disabled the whole object.
        building.SetActive(true);
        foreach (Transform child in building.transform) {
            // Only force-enable intact branch.
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
        if (buildingsRoot == null) {
            return;
        }

        for (int i = 0; i < buildingsRoot.childCount; i++) {
            Transform b = buildingsRoot.GetChild(i);
            Vector3 probe = b.position + Vector3.up * raycastHeight;
            if (SampleGround(probe, out Vector3 ground, out _)) {
                b.position = ground;
            }
            EnsureVisible(b.gameObject);
        }
    }

    static Vector3 Flatten(Vector3 v) {
        return new Vector3(v.x, 0f, v.z);
    }

    float FirstSpawnDistanceToCenter() {
        if (buildingsRoot == null || buildingsRoot.childCount == 0) {
            return -1f;
        }
        return Vector3.Distance(Flatten(buildingsRoot.GetChild(0).position), Flatten(areaCenter));
    }
}

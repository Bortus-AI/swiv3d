using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Populates the map with destructible buildings (House001) snapped to terrain.
/// Spawns both clustered settlements and scattered outposts around a center point.
/// </summary>
public class BuildingSpawner : MonoBehaviour {
    [Header("Prefab")]
    [SerializeField] GameObject buildingPrefab;
    [SerializeField] string resourcesPrefabPath = "Prefabs/House001";

    [Header("Placement area")]
    [Tooltip("World XZ center for the populated region. Defaults to this transform.")]
    [SerializeField] Vector3 areaCenter = new Vector3(684f, 0f, 3160f);
    [SerializeField] float areaRadius = 550f;
    [SerializeField] int settlementCount = 7;
    [SerializeField] int buildingsPerSettlementMin = 4;
    [SerializeField] int buildingsPerSettlementMax = 9;
    [SerializeField] int scatteredBuildingCount = 28;
    [SerializeField] float minBuildingSpacing = 28f;
    [SerializeField] float settlementRadius = 55f;

    [Header("Terrain fit")]
    [SerializeField] float maxSlopeDegrees = 28f;
    [SerializeField] float yOffset = 0f;
    [SerializeField] int maxPlacementAttempts = 40;
    [SerializeField] int randomSeed = 1337;
    [SerializeField] bool useRandomSeed = true;

    [Header("Variety")]
    [SerializeField] Vector2 scaleRange = new Vector2(4.5f, 7f);
    [SerializeField] bool randomYaw = true;

    readonly List<Vector3> placedPositions = new List<Vector3>();
    Transform buildingsRoot;

    void Start() {
        StartCoroutine(SpawnWhenReady());
    }

    IEnumerator SpawnWhenReady() {
        // Wait until physics/terrain colliders are ready for ground snaps.
        yield return new WaitForFixedUpdate();
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

        buildingsRoot = new GameObject("Buildings").transform;
        buildingsRoot.SetParent(transform, false);

        // Keep the hand-placed Level 1 house as a seed position so we do not overlap it.
        foreach (var existing in FindObjectsOfType<ExplodeObject>()) {
            if (existing != null) {
                placedPositions.Add(existing.transform.position);
            }
        }

        SpawnSettlements();
        SpawnScattered();

        Debug.Log("BuildingSpawner: placed " + placedPositions.Count + " buildings (including pre-existing).");
    }

    void SpawnSettlements() {
        for (int s = 0; s < settlementCount; s++) {
            Vector3 hub;
            if (!TryFindFlatSpot(areaCenter, areaRadius * 0.9f, out hub)) {
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
            Vector3 probe = new Vector3(center.x + offset.x, center.y + 500f, center.z + offset.y);

            if (!SampleGround(probe, out Vector3 ground, out float slopeDegrees)) {
                continue;
            }
            if (slopeDegrees > maxSlopeDegrees) {
                continue;
            }
            if (!IsFarEnough(ground)) {
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

        // Prefer physics raycast so we hit the active TerrainCollider even as LoadTerrain shifts it.
        if (Physics.Raycast(above, Vector3.down, out RaycastHit hit, 2000f, ~0, QueryTriggerInteraction.Ignore)) {
            ground = hit.point + Vector3.up * yOffset;
            slopeDegrees = Vector3.Angle(hit.normal, Vector3.up);
            return true;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null) {
            float y = terrain.SampleHeight(above) + terrain.transform.position.y + yOffset;
            ground = new Vector3(above.x, y, above.z);
            Vector3 normal = terrain.terrainData.GetInterpolatedNormal(
                (above.x - terrain.transform.position.x) / terrain.terrainData.size.x,
                (above.z - terrain.transform.position.z) / terrain.terrainData.size.z
            );
            slopeDegrees = Vector3.Angle(normal, Vector3.up);
            return true;
        }

        return false;
    }

    bool IsFarEnough(Vector3 pos) {
        float minSq = minBuildingSpacing * minBuildingSpacing;
        for (int i = 0; i < placedPositions.Count; i++) {
            Vector3 delta = placedPositions[i] - pos;
            delta.y = 0f;
            if (delta.sqrMagnitude < minSq) {
                return false;
            }
        }
        return true;
    }

    void SpawnBuilding(Vector3 position) {
        float yaw = randomYaw ? Random.Range(0f, 360f) : 0f;
        GameObject building = Instantiate(buildingPrefab, position, Quaternion.Euler(0f, yaw, 0f), buildingsRoot);

        float scale = Random.Range(scaleRange.x, scaleRange.y);
        building.transform.localScale = Vector3.one * scale;

        // Ensure damage + explode wiring even if prefab was stripped somehow.
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

        placedPositions.Add(position);
    }
}

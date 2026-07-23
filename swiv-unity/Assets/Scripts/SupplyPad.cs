using UnityEngine;

/// <summary>
/// SWIV-style ammo resupply pad. Hover the helicopter over the pad to drop a red
/// winch line, grab the resting munition, and reel it up into the chopper for ammo.
/// </summary>
public class SupplyPad : MonoBehaviour {
    public enum State {
        Available,
        Dropping,
        Reeling,
        Empty
    }

    [Header("Supply")]
    [SerializeField] WeaponType weaponType = WeaponType.Rockets;
    [SerializeField] int ammoAmount = 8;
    [SerializeField] bool autoBuildVisuals = true;

    [Header("Capture")]
    [Tooltip("Horizontal distance from pad center the heli must be within.")]
    [SerializeField] float captureRadius = 12f;
    [Tooltip("Heli must be at least this high above the pad to start a grab.")]
    [SerializeField] float minHoverHeight = 8f;
    [Tooltip("Heli must stay below this height above the pad (prevents grab from map edge).")]
    [SerializeField] float maxHoverHeight = 90f;
    [SerializeField] float stayOverPadRequired = 0.15f;

    [Header("Winch")]
    [SerializeField] float dropSpeed = 45f;
    [SerializeField] float reelSpeed = 28f;
    [SerializeField] Color lineColor = new Color(0.95f, 0.05f, 0.05f, 1f);
    [SerializeField] float lineWidth = 0.12f;
    [SerializeField] Vector3 heliAttachLocalOffset = new Vector3(0f, -1.2f, 0f);

    [Header("Respawn")]
    [SerializeField] bool respawn = true;
    [SerializeField] float respawnDelay = 25f;

    [Header("Runtime refs (auto-filled if empty)")]
    [SerializeField] Transform cargo;
    [SerializeField] LineRenderer winchLine;

    State state = State.Available;
    float hoverTimer;
    float emptyTimer;
    float lineLength;
    float targetLineLength;
    Vector3 cargoRestLocalPos;
    Transform activeHeli;
    PlayerWeapons activeWeapons;
    Material padMat;
    Material arrowMat;
    Material cargoMat;

    public State CurrentState => state;
    public WeaponType WeaponType => weaponType;
    public bool IsAvailable => state == State.Available;

    void Awake() {
        if (autoBuildVisuals) {
            EnsureVisuals();
        }
        if (cargo != null) {
            cargoRestLocalPos = cargo.localPosition;
        }
        EnsureWinchLine();
    }

    void OnDestroy() {
        if (padMat != null) Destroy(padMat);
        if (arrowMat != null) Destroy(arrowMat);
        if (cargoMat != null) Destroy(cargoMat);
    }

    void Update() {
        switch (state) {
            case State.Available:
                TickAvailable();
                break;
            case State.Dropping:
                TickDropping();
                break;
            case State.Reeling:
                TickReeling();
                break;
            case State.Empty:
                TickEmpty();
                break;
        }
    }

    void TickAvailable() {
        if (!TryFindHoveringHeli(out Transform heli, out PlayerWeapons weapons)) {
            hoverTimer = 0f;
            HideLine();
            return;
        }

        hoverTimer += Time.deltaTime;
        // Preview line while hovering so the player sees the pad is active.
        DrawLine(AttachPoint(heli), CargoWorldPos());

        if (hoverTimer < stayOverPadRequired) {
            return;
        }

        activeHeli = heli;
        activeWeapons = weapons;
        targetLineLength = Vector3.Distance(AttachPoint(heli), CargoWorldPos());
        lineLength = 0.5f;
        state = State.Dropping;
    }

    void TickDropping() {
        if (activeHeli == null || !activeHeli.gameObject.activeInHierarchy) {
            AbortGrab();
            return;
        }
        if (!StillOverPad(activeHeli)) {
            AbortGrab();
            return;
        }

        Vector3 attach = AttachPoint(activeHeli);
        Vector3 cargoPos = CargoWorldPos();
        targetLineLength = Vector3.Distance(attach, cargoPos);
        lineLength = Mathf.MoveTowards(lineLength, targetLineLength, dropSpeed * Time.deltaTime);

        Vector3 tip = Vector3.Lerp(attach, cargoPos, Mathf.Clamp01(lineLength / Mathf.Max(0.01f, targetLineLength)));
        DrawLine(attach, tip);

        if (lineLength >= targetLineLength - 0.05f) {
            state = State.Reeling;
        }
    }

    void TickReeling() {
        if (activeHeli == null || !activeHeli.gameObject.activeInHierarchy) {
            AbortGrab();
            return;
        }

        Vector3 attach = AttachPoint(activeHeli);
        // Cargo follows the tip of the winch as it shortens.
        lineLength = Mathf.MoveTowards(lineLength, 0.2f, reelSpeed * Time.deltaTime);
        Vector3 cargoTarget = attach + Vector3.down * lineLength;
        if (cargo != null) {
            cargo.position = Vector3.MoveTowards(cargo.position, cargoTarget, reelSpeed * Time.deltaTime);
            cargo.Rotate(Vector3.up, 180f * Time.deltaTime, Space.World);
        }
        DrawLine(attach, cargo != null ? cargo.position : cargoTarget);

        if (lineLength <= 0.35f) {
            CompleteGrab();
        }
    }

    void TickEmpty() {
        HideLine();
        if (!respawn) {
            return;
        }
        emptyTimer += Time.deltaTime;
        if (emptyTimer >= respawnDelay) {
            RespawnCargo();
        }
    }

    void CompleteGrab() {
        if (activeWeapons != null) {
            activeWeapons.AddAmmo(weaponType, ammoAmount);
            // Auto-select the reloaded special so the player feels the pickup immediately.
            activeWeapons.SelectSpecial(weaponType);
        }

        if (cargo != null) {
            cargo.gameObject.SetActive(false);
        }
        HideLine();
        activeHeli = null;
        activeWeapons = null;
        emptyTimer = 0f;
        state = State.Empty;
    }

    void AbortGrab() {
        if (cargo != null) {
            cargo.localPosition = cargoRestLocalPos;
            cargo.localRotation = Quaternion.identity;
            cargo.gameObject.SetActive(true);
        }
        HideLine();
        activeHeli = null;
        activeWeapons = null;
        hoverTimer = 0f;
        state = State.Available;
    }

    void RespawnCargo() {
        if (cargo != null) {
            cargo.localPosition = cargoRestLocalPos;
            cargo.localRotation = Quaternion.identity;
            cargo.gameObject.SetActive(true);
        }
        hoverTimer = 0f;
        state = State.Available;
    }

    bool TryFindHoveringHeli(out Transform heli, out PlayerWeapons weapons) {
        heli = null;
        weapons = null;
        var all = FindObjectsOfType<PlayerWeapons>();
        float best = captureRadius;
        for (int i = 0; i < all.Length; i++) {
            var pw = all[i];
            if (pw == null) continue;
            Transform t = pw.transform;
            if (!IsOverPad(t)) continue;
            float dxz = HorizontalDistance(t.position, transform.position);
            if (dxz <= best) {
                best = dxz;
                heli = t;
                weapons = pw;
            }
        }
        return heli != null;
    }

    bool StillOverPad(Transform heli) {
        return heli != null && IsOverPad(heli);
    }

    bool IsOverPad(Transform heli) {
        float dxz = HorizontalDistance(heli.position, transform.position);
        if (dxz > captureRadius) {
            return false;
        }
        float height = heli.position.y - transform.position.y;
        return height >= minHoverHeight && height <= maxHoverHeight;
    }

    static float HorizontalDistance(Vector3 a, Vector3 b) {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    Vector3 AttachPoint(Transform heli) {
        return heli.TransformPoint(heliAttachLocalOffset);
    }

    Vector3 CargoWorldPos() {
        if (cargo != null && cargo.gameObject.activeInHierarchy) {
            return cargo.position;
        }
        return transform.position + Vector3.up * 1.2f;
    }

    void EnsureWinchLine() {
        if (winchLine != null) {
            return;
        }
        var go = new GameObject("WinchLine");
        go.transform.SetParent(transform, false);
        winchLine = go.AddComponent<LineRenderer>();
        winchLine.positionCount = 2;
        winchLine.useWorldSpace = true;
        winchLine.startWidth = lineWidth;
        winchLine.endWidth = lineWidth * 0.7f;
        winchLine.numCapVertices = 4;
        winchLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        winchLine.receiveShadows = false;
        winchLine.material = new Material(Shader.Find("Sprites/Default"));
        winchLine.startColor = lineColor;
        winchLine.endColor = lineColor;
        winchLine.enabled = false;
    }

    void DrawLine(Vector3 from, Vector3 to) {
        EnsureWinchLine();
        winchLine.enabled = true;
        winchLine.SetPosition(0, from);
        winchLine.SetPosition(1, to);
        winchLine.startColor = lineColor;
        winchLine.endColor = lineColor;
    }

    void HideLine() {
        if (winchLine != null) {
            winchLine.enabled = false;
        }
    }

    // Pad slab thickness / half-size used for placement clearance.
    public const float PadHalfExtent = 7f;
    public const float PadThickness = 0.55f;
    public const float PadTopClearance = 0.15f; // extra lift above highest ground under pad

    void EnsureVisuals() {
        // Dark square pad — thick enough that terrain bumps under the footprint
        // do not clip through the top deck.
        Transform padT = transform.Find("Pad");
        if (padT == null) {
            var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pad.name = "Pad";
            pad.transform.SetParent(transform, false);
            // Root at ground; deck top sits at PadThickness.
            pad.transform.localPosition = new Vector3(0f, PadThickness * 0.5f, 0f);
            pad.transform.localScale = new Vector3(PadHalfExtent * 2f, PadThickness, PadHalfExtent * 2f);
            Object.Destroy(pad.GetComponent<Collider>());
            padMat = new Material(Shader.Find("Standard"));
            padMat.color = new Color(0.18f, 0.2f, 0.22f, 1f);
            pad.GetComponent<MeshRenderer>().sharedMaterial = padMat;
        }

        // Four yellow chevron arrows on the deck surface.
        if (transform.Find("Arrows") == null) {
            var arrows = new GameObject("Arrows");
            arrows.transform.SetParent(transform, false);
            arrowMat = new Material(Shader.Find("Standard"));
            arrowMat.color = new Color(1f, 0.9f, 0.1f, 1f);
            arrowMat.EnableKeyword("_EMISSION");
            arrowMat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.1f) * 1.5f);

            float deckY = PadThickness + 0.06f;
            Vector3[] offsets = {
                new Vector3(0f, deckY, 3.5f),
                new Vector3(0f, deckY, -3.5f),
                new Vector3(3.5f, deckY, 0f),
                new Vector3(-3.5f, deckY, 0f)
            };
            for (int i = 0; i < offsets.Length; i++) {
                var arrow = BuildChevron("Arrow" + i, arrowMat);
                arrow.transform.SetParent(arrows.transform, false);
                arrow.transform.localPosition = offsets[i];
                // Lay flat on pad deck.
                arrow.transform.localRotation = Quaternion.Euler(90f, i * 90f, 0f);
                arrow.transform.localScale = Vector3.one * 1.4f;
            }
        }

        // Resting munition cargo the winch lifts — sits on top of the deck.
        if (cargo == null) {
            var existing = transform.Find("Cargo");
            if (existing != null) {
                cargo = existing;
            } else {
                cargo = BuildCargoVisual(weaponType).transform;
                cargo.name = "Cargo";
                cargo.SetParent(transform, false);
                cargo.localPosition = new Vector3(0f, PadThickness + 1.0f, 0f);
                cargoRestLocalPos = cargo.localPosition;
            }
        }
    }

    static GameObject BuildChevron(string name, Material mat) {
        // Simple arrow from two cubes as a V / chevron shape.
        var root = new GameObject(name);
        var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
        left.name = "L";
        left.transform.SetParent(root.transform, false);
        left.transform.localPosition = new Vector3(-0.25f, 0f, 0f);
        left.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
        left.transform.localScale = new Vector3(0.2f, 0.9f, 0.12f);
        Object.Destroy(left.GetComponent<Collider>());
        left.GetComponent<MeshRenderer>().sharedMaterial = mat;

        var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
        right.name = "R";
        right.transform.SetParent(root.transform, false);
        right.transform.localPosition = new Vector3(0.25f, 0f, 0f);
        right.transform.localRotation = Quaternion.Euler(0f, 0f, -35f);
        right.transform.localScale = new Vector3(0.2f, 0.9f, 0.12f);
        Object.Destroy(right.GetComponent<Collider>());
        right.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return root;
    }

    GameObject BuildCargoVisual(WeaponType type) {
        var root = new GameObject("Cargo");
        cargoMat = new Material(Shader.Find("Standard"));
        cargoMat.color = ColorFor(type);
        cargoMat.EnableKeyword("_EMISSION");
        cargoMat.SetColor("_EmissionColor", ColorFor(type) * 1.2f);

        if (type == WeaponType.SmartBomb) {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Body";
            ball.transform.SetParent(root.transform, false);
            ball.transform.localScale = Vector3.one * 1.6f;
            Object.Destroy(ball.GetComponent<Collider>());
            ball.GetComponent<MeshRenderer>().sharedMaterial = cargoMat;
        } else if (type == WeaponType.Napalm) {
            // Prefer the authored napalm bomb mesh; fall back to a squat capsule.
            var bombMesh = Resources.Load<GameObject>("Prefabs/Weapons/Napalm");
            if (bombMesh != null) {
                var bomb = Object.Instantiate(bombMesh);
                bomb.name = "Body";
                bomb.transform.SetParent(root.transform, false);
                bomb.transform.localPosition = Vector3.zero;
                bomb.transform.localRotation = Quaternion.identity;
                bomb.transform.localScale = Vector3.one * 1.8f;
                foreach (var col in bomb.GetComponentsInChildren<Collider>()) {
                    Object.Destroy(col);
                }
                // Soft yellow tint so it still reads as a supply icon without washing materials out.
                foreach (var mr in bomb.GetComponentsInChildren<MeshRenderer>()) {
                    if (mr == null) continue;
                    var mats = mr.materials;
                    for (int i = 0; i < mats.Length; i++) {
                        if (mats[i] == null) continue;
                        mats[i] = new Material(mats[i]);
                        if (mats[i].HasProperty("_EmissionColor")) {
                            mats[i].EnableKeyword("_EMISSION");
                            mats[i].SetColor("_EmissionColor", ColorFor(type) * 0.35f);
                        }
                    }
                    mr.materials = mats;
                }
            } else {
                var can = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                can.name = "Body";
                can.transform.SetParent(root.transform, false);
                can.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                can.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
                Object.Destroy(can.GetComponent<Collider>());
                can.GetComponent<MeshRenderer>().sharedMaterial = cargoMat;
            }
        } else {
            // Rocket / homing missile silhouette.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            body.transform.localScale = new Vector3(0.45f, 1.1f, 0.45f);
            Object.Destroy(body.GetComponent<Collider>());
            body.GetComponent<MeshRenderer>().sharedMaterial = cargoMat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            nose.name = "Nose";
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0f, 1.15f);
            nose.transform.localScale = new Vector3(0.42f, 0.42f, 0.7f);
            Object.Destroy(nose.GetComponent<Collider>());
            nose.GetComponent<MeshRenderer>().sharedMaterial = cargoMat;

            for (int i = 0; i < 4; i++) {
                var fin = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fin.name = "Fin" + i;
                fin.transform.SetParent(root.transform, false);
                float ang = i * 90f;
                fin.transform.localRotation = Quaternion.Euler(0f, 0f, ang);
                fin.transform.localPosition = Quaternion.Euler(0f, 0f, ang) * new Vector3(0f, 0.35f, -0.85f);
                fin.transform.localScale = new Vector3(0.08f, 0.45f, 0.35f);
                Object.Destroy(fin.GetComponent<Collider>());
                fin.GetComponent<MeshRenderer>().sharedMaterial = cargoMat;
            }
        }
        return root;
    }

    static Color ColorFor(WeaponType type) {
        switch (type) {
            case WeaponType.Rockets: return new Color(1f, 0.55f, 0.1f);
            case WeaponType.HomingMissiles: return new Color(1f, 0.2f, 0.2f);
            case WeaponType.Napalm: return new Color(1f, 0.85f, 0.15f);
            case WeaponType.SmartBomb: return new Color(1f, 1f, 0.5f);
            default: return Color.cyan;
        }
    }

    void OnDrawGizmosSelected() {
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.2f, captureRadius);
    }

    /// <summary>
    /// Spawns a fully built supply pad. Snaps so the entire pad footprint clears
    /// the highest terrain under it (pads stay level, never buried in hillsides).
    /// </summary>
    public static SupplyPad Spawn(Vector3 position, WeaponType type, int amount = 8) {
        position = PlaceOnTerrainTop(position);

        var go = new GameObject(type + "SupplyPad");
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        var pad = go.AddComponent<SupplyPad>();
        pad.weaponType = type;
        pad.ammoAmount = amount;
        pad.autoBuildVisuals = true;
        pad.EnsureVisuals();
        pad.EnsureWinchLine();
        if (pad.cargo != null) {
            pad.cargoRestLocalPos = pad.cargo.localPosition;
        }
        return pad;
    }

    /// <summary>
    /// Raises Y to the max terrain height under the pad footprint + clearance,
    /// so a flat horizontal pad never clips through ground at its corners.
    /// </summary>
    public static Vector3 PlaceOnTerrainTop(Vector3 position, float halfExtent = PadHalfExtent) {
        float maxY = SampleTerrainY(position.x, position.z);
        // Sample a grid under the pad so bumps/ridges can't poke through.
        const int steps = 4;
        for (int ix = -steps; ix <= steps; ix++) {
            for (int iz = -steps; iz <= steps; iz++) {
                float x = position.x + (ix / (float)steps) * halfExtent;
                float z = position.z + (iz / (float)steps) * halfExtent;
                maxY = Mathf.Max(maxY, SampleTerrainY(x, z));
            }
        }
        return new Vector3(position.x, maxY + PadTopClearance, position.z);
    }

    /// <summary>Re-snap this pad so its deck sits fully above terrain.</summary>
    public void ReplantOnTerrain() {
        Vector3 p = transform.position;
        transform.position = PlaceOnTerrainTop(p);
        transform.rotation = Quaternion.identity;
    }

    static float SampleTerrainY(float worldX, float worldZ) {
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null) {
            return terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + terrain.transform.position.y;
        }
        if (Physics.Raycast(new Vector3(worldX, 500f, worldZ), Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore)) {
            return hit.point.y;
        }
        return 0f;
    }

    /// <summary>World Y from active terrain if available; otherwise physics raycast.</summary>
    public static Vector3 SnapToTerrain(Vector3 position) {
        return PlaceOnTerrainTop(position);
    }
}

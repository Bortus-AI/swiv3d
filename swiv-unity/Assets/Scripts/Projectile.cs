using UnityEngine;

/// <summary>
/// Generic projectile used by plasma, rockets, homing missiles and napalm bombs.
/// Moves in a straight line (or homes / arcs under gravity for bombs), raycasts for hits,
/// applies point + splash damage.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour {
    /// <summary>Extra downward acceleration for free-fall bombs (napalm). m/s².</summary>
    const float NapalmGravity = 28f;

    WeaponDefinition definition;
    Transform owner;
    Vector3 velocity;
    Transform homingTarget;
    float age;
    bool hasExploded;
    TrailRenderer trail;
    float tumbleAngle;

    public void Initialize(WeaponDefinition def, Transform ownerTransform, Vector3 direction) {
        definition = def;
        owner = ownerTransform;
        velocity = direction.normalized * def.projectileSpeed;
        age = 0f;
        hasExploded = false;

        // Multiply (not overwrite) so prefabs authored with a non-uniform root scale
        // (e.g. Napalm's squat canister, Plasma's thin bolt) keep their proportions —
        // the sphere fallback and Rockets/HomingMissiles' empty-root prefabs default to
        // (1,1,1) anyway, so this is a no-op change for them.
        transform.localScale = Vector3.Scale(transform.localScale, Vector3.one * def.projectileScale);
        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = gameObject.GetComponent<Rigidbody>();
        if (rb == null) {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;

        ApplyVisual(def);

        if (def.isHoming) {
            FindHomingTarget();
        }
    }

    void ApplyVisual(WeaponDefinition def) {
        var renderers = GetComponentsInChildren<MeshRenderer>();
        // Napalm uses an authored multi-material bomb mesh — keep those colors.
        // Homing seeker tip keeps its own dark material.
        bool preserveAuthoredMats = def.type == WeaponType.Napalm;

        if (!preserveAuthoredMats) {
            for (int i = 0; i < renderers.Length; i++) {
                var renderer = renderers[i];
                if (renderer.gameObject.name == "SeekerTip" || renderer.material == null) {
                    continue;
                }
                renderer.material = new Material(renderer.material);
                if (renderer.material.HasProperty("_Color")) {
                    renderer.material.color = def.projectileColor;
                }
                if (renderer.material.HasProperty("_BaseColor")) {
                    renderer.material.SetColor("_BaseColor", def.projectileColor);
                }
                if (renderer.material.HasProperty("_EmissionColor")) {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", def.projectileColor * 2f);
                }
            }
        }

        trail = gameObject.GetComponent<TrailRenderer>();
        if (trail == null) {
            trail = gameObject.AddComponent<TrailRenderer>();
        }
        // Plasma is a glowing ball — short soft halo trail. Bombs get a thin smoke streak.
        bool plasmaBall = def.type == WeaponType.Plasma;
        bool napalmBomb = def.type == WeaponType.Napalm;
        trail.time = plasmaBall ? 0.08f : (napalmBomb ? 0.35f : 0.25f);
        trail.startWidth = plasmaBall ? def.projectileScale * 2.2f : def.projectileScale * 0.6f;
        trail.endWidth = plasmaBall ? 0.05f : 0.02f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = napalmBomb
            ? new Color(0.35f, 0.32f, 0.28f, 0.7f)
            : def.projectileColor;
        trail.endColor = new Color(trail.startColor.r, trail.startColor.g, trail.startColor.b, 0f);
        trail.minVertexDistance = plasmaBall ? 0.05f : 0.2f;
        // Stronger emission on plasma so the sphere reads as an energy ball, not a dull marble.
        if (plasmaBall) {
            for (int i = 0; i < renderers.Length; i++) {
                var renderer = renderers[i];
                if (renderer.material != null && renderer.material.HasProperty("_EmissionColor")) {
                    renderer.material.EnableKeyword("_EMISSION");
                    renderer.material.SetColor("_EmissionColor", def.projectileColor * 4f);
                    if (renderer.material.HasProperty("_Color")) {
                        var c = def.projectileColor;
                        renderer.material.color = new Color(
                            Mathf.Min(1f, c.r * 1.4f),
                            Mathf.Min(1f, c.g * 1.4f),
                            Mathf.Min(1f, c.b * 1.4f),
                            1f
                        );
                    }
                }
            }
        }
    }

    void Update() {
        if (hasExploded || definition == null) {
            return;
        }

        age += Time.deltaTime;
        if (age >= definition.projectileLifetime) {
            Explode(transform.position);
            return;
        }

        if (definition.isHoming) {
            UpdateHoming();
        }

        // Free-fall bomb arc (napalm) — not a powered missile.
        if (definition.type == WeaponType.Napalm) {
            velocity += Vector3.down * NapalmGravity * Time.deltaTime;
        }

        float step = velocity.magnitude * Time.deltaTime;
        Vector3 dir = velocity.normalized;
        if (step > 0f && Physics.SphereCast(transform.position, definition.projectileRadius, dir, out RaycastHit hit, step, ~0, QueryTriggerInteraction.Ignore)) {
            if (owner != null && hit.transform.IsChildOf(owner)) {
                transform.position += dir * step;
                return;
            }
            transform.position = hit.point;
            ApplyHit(hit.collider, hit.point, dir);
            return;
        }

        transform.position += velocity * Time.deltaTime;
        if (velocity.sqrMagnitude > 0.001f) {
            Quaternion facing = Quaternion.LookRotation(velocity.normalized, Vector3.up);
            if (definition.type == WeaponType.Napalm) {
                tumbleAngle += 180f * Time.deltaTime;
                transform.rotation = facing * Quaternion.Euler(tumbleAngle, 0f, 0f);
            } else {
                transform.rotation = facing;
            }
        }
    }

    void UpdateHoming() {
        if (homingTarget == null || !homingTarget.gameObject.activeInHierarchy) {
            FindHomingTarget();
            if (homingTarget == null) {
                return;
            }
        }

        var damageable = homingTarget.GetComponentInParent<Damageable>();
        if (damageable != null && damageable.IsDead) {
            FindHomingTarget();
            if (homingTarget == null) {
                return;
            }
        }

        Vector3 toTarget = (homingTarget.position - transform.position).normalized;
        Vector3 currentDir = velocity.normalized;
        Vector3 newDir = Vector3.RotateTowards(
            currentDir,
            toTarget,
            definition.homingTurnRate * Mathf.Deg2Rad * Time.deltaTime,
            0f
        );
        velocity = newDir * definition.projectileSpeed;
    }

    void FindHomingTarget() {
        homingTarget = null;
        float bestDist = definition.homingRange;
        var targets = FindObjectsOfType<Damageable>();
        for (int i = 0; i < targets.Length; i++) {
            var d = targets[i];
            if (d.IsDead) {
                continue;
            }
            if (owner != null && d.transform.IsChildOf(owner)) {
                continue;
            }
            float dist = Vector3.Distance(transform.position, d.transform.position);
            // Prefer targets roughly in front of the missile.
            Vector3 to = (d.transform.position - transform.position).normalized;
            float facing = Vector3.Dot(velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward, to);
            if (facing < -0.2f) {
                continue;
            }
            if (dist < bestDist) {
                bestDist = dist;
                homingTarget = d.transform;
            }
        }
    }

    void OnTriggerEnter(Collider other) {
        if (hasExploded) {
            return;
        }
        if (owner != null && other.transform.IsChildOf(owner)) {
            return;
        }
        ApplyHit(other, transform.position, velocity.normalized);
    }

    void ApplyHit(Collider other, Vector3 point, Vector3 direction) {
        var damageable = other.GetComponentInParent<Damageable>();
        if (damageable != null && !damageable.IsDead) {
            damageable.TakeDamage(definition.damage, point, direction);
        }
        Explode(point);
    }

    void Explode(Vector3 point) {
        if (hasExploded) {
            return;
        }
        hasExploded = true;

        if (definition.explosionRadius > 0f && definition.explosionDamage > 0f) {
            ExplosionUtil.ApplyRadiusDamage(point, definition.explosionRadius, definition.explosionDamage, owner);
        }
        WeaponVisuals.SpawnImpact(definition.type, point, definition, owner);

        if (definition.burnDuration > 0f && definition.burnRadius > 0f) {
            BurnZone.Spawn(point, definition.burnRadius, definition.burnDuration, definition.burnDamagePerSecond, owner);
        }

        Destroy(gameObject);
    }

    /// <summary>Loads the weapon's authored prefab from Resources, or builds a colored sphere as a fallback.</summary>
    public static Projectile CreateRuntime(WeaponDefinition def, Vector3 position, Vector3 direction, Transform owner) {
        var go = BuildVisualRoot(def.type);
        go.name = def.type + "Projectile";
        go.transform.position = position;
        // Prefer an existing component (if a prefab ever ships with one) so we never
        // end up with two Projectiles — an uninitialized duplicate spams NREs in Update.
        var projectile = go.GetComponent<Projectile>();
        if (projectile == null) {
            projectile = go.AddComponent<Projectile>();
        }
        // SphereCollider is required; ensure it exists before stripping extras.
        if (go.GetComponent<SphereCollider>() == null) {
            go.AddComponent<SphereCollider>();
        }
        RemoveExtraColliders(go, go.GetComponent<SphereCollider>());
        projectile.Initialize(def, owner, direction);
        return projectile;
    }

    static GameObject BuildVisualRoot(WeaponType type) {
        var prefab = Resources.Load<GameObject>("Prefabs/Weapons/" + type);
        if (prefab != null) {
            return Object.Instantiate(prefab);
        }
        return GameObject.CreatePrimitive(PrimitiveType.Sphere);
    }

    static void RemoveExtraColliders(GameObject root, Collider keep) {
        var colliders = root.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++) {
            if (colliders[i] != keep) {
                Object.Destroy(colliders[i]);
            }
        }
    }
}

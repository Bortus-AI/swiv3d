using UnityEngine;

/// <summary>
/// Generic projectile used by plasma, rockets, homing missiles and napalm.
/// Moves in a straight line (or homes), raycasts for hits, applies point + splash damage.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour {
    WeaponDefinition definition;
    Transform owner;
    Vector3 velocity;
    Transform homingTarget;
    float age;
    bool hasExploded;
    TrailRenderer trail;

    public void Initialize(WeaponDefinition def, Transform ownerTransform, Vector3 direction) {
        definition = def;
        owner = ownerTransform;
        velocity = direction.normalized * def.projectileSpeed;
        age = 0f;
        hasExploded = false;

        transform.localScale = Vector3.one * def.projectileScale;
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
        var renderer = GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null) {
            // Use a simple unlit-ish color; Shared material clone to avoid editing defaults.
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

        trail = gameObject.GetComponent<TrailRenderer>();
        if (trail == null) {
            trail = gameObject.AddComponent<TrailRenderer>();
        }
        trail.time = 0.25f;
        trail.startWidth = def.projectileScale * 0.6f;
        trail.endWidth = 0.02f;
        trail.material = new Material(Shader.Find("Sprites/Default"));
        trail.startColor = def.projectileColor;
        trail.endColor = new Color(def.projectileColor.r, def.projectileColor.g, def.projectileColor.b, 0f);
        trail.minVertexDistance = 0.2f;
    }

    void Update() {
        if (hasExploded) {
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
            transform.rotation = Quaternion.LookRotation(velocity.normalized, Vector3.up);
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
            ExplosionUtil.SpawnFlash(point, definition.explosionRadius, definition.projectileColor);
        }

        if (definition.burnDuration > 0f && definition.burnRadius > 0f) {
            BurnZone.Spawn(point, definition.burnRadius, definition.burnDuration, definition.burnDamagePerSecond, owner);
        }

        Destroy(gameObject);
    }

    /// <summary>Builds a simple colored sphere projectile at runtime when no prefab is assigned.</summary>
    public static Projectile CreateRuntime(WeaponDefinition def, Vector3 position, Vector3 direction, Transform owner) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = def.type + "Projectile";
        go.transform.position = position;
        // Remove default non-trigger collider physics material friction issues.
        Object.Destroy(go.GetComponent<Collider>());
        go.AddComponent<SphereCollider>();
        var projectile = go.AddComponent<Projectile>();
        projectile.Initialize(def, owner, direction);
        return projectile;
    }
}

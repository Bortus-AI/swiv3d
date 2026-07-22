using UnityEngine;

/// <summary>
/// Ground-lingering napalm fire zone. Damages Damageables inside the radius over time.
/// </summary>
public class BurnZone : MonoBehaviour {
    float radius;
    float duration;
    float damagePerSecond;
    Transform owner;
    float age;
    MeshRenderer meshRenderer;

    public static BurnZone Spawn(Vector3 position, float radius, float duration, float dps, Transform owner) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "NapalmBurnZone";
        // Snap slightly above ground if we can raycast down.
        Vector3 pos = position;
        if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)) {
            pos = hit.point + Vector3.up * 0.15f;
        }
        go.transform.position = pos;
        go.transform.localScale = new Vector3(radius * 2f, 0.15f, radius * 2f);

        Object.Destroy(go.GetComponent<Collider>());
        var zone = go.AddComponent<BurnZone>();
        zone.radius = radius;
        zone.duration = duration;
        zone.damagePerSecond = dps;
        zone.owner = owner;

        zone.meshRenderer = go.GetComponent<MeshRenderer>();
        if (zone.meshRenderer != null) {
            zone.meshRenderer.material = new Material(Shader.Find("Sprites/Default"));
            zone.meshRenderer.material.color = new Color(1f, 0.35f, 0.05f, 0.55f);
        }

        zone.SpawnFlameEmitter(go.transform, radius);

        return zone;
    }

    void SpawnFlameEmitter(Transform parent, float radius) {
        var go = new GameObject("BurnZoneFlames");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * 0.1f;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.loop = true;
        main.startLifetime = 0.6f;
        main.startSpeed = 1.5f;
        main.startSize = 0.6f;
        main.startColor = new Color(1f, 0.5f, 0.1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.15f;
        main.playOnAwake = true;

        var emission = ps.emission;
        emission.rateOverTime = 18f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius * 0.9f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f),
                new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.5f),
                new GradientColorKey(new Color(0.2f, 0.05f, 0f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.5f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader") {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            renderer.material = mat;
        }

        ps.Play();
    }

    void Update() {
        age += Time.deltaTime;
        if (age >= duration) {
            Destroy(gameObject);
            return;
        }

        // Pulse visual alpha.
        if (meshRenderer != null && meshRenderer.material != null) {
            float pulse = 0.35f + 0.25f * Mathf.Sin(Time.time * 10f);
            float fade = 1f - (age / duration);
            Color c = meshRenderer.material.color;
            c.a = pulse * fade;
            meshRenderer.material.color = c;
        }

        var hits = Physics.OverlapSphere(transform.position, radius);
        for (int i = 0; i < hits.Length; i++) {
            var col = hits[i];
            if (owner != null && col.transform.IsChildOf(owner)) {
                continue;
            }
            var damageable = col.GetComponentInParent<Damageable>();
            if (damageable == null || damageable.IsDead) {
                continue;
            }
            damageable.TakeDamage(damagePerSecond * Time.deltaTime, transform.position, Vector3.up);
        }
    }
}

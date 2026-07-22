using UnityEngine;

/// <summary>
/// Shared explosion helpers for rockets, missiles, napalm impact and smart bombs.
/// </summary>
public static class ExplosionUtil {
    public static void ApplyRadiusDamage(Vector3 center, float radius, float damage, Transform owner) {
        if (radius <= 0f || damage <= 0f) {
            return;
        }

        var hits = Physics.OverlapSphere(center, radius);
        for (int i = 0; i < hits.Length; i++) {
            var col = hits[i];
            if (owner != null && col.transform.IsChildOf(owner)) {
                continue;
            }

            var damageable = col.GetComponentInParent<Damageable>();
            if (damageable == null || damageable.IsDead) {
                continue;
            }

            float dist = Vector3.Distance(center, damageable.transform.position);
            float falloff = 1f - Mathf.Clamp01(dist / radius);
            float applied = damage * Mathf.Lerp(0.35f, 1f, falloff);
            Vector3 dir = (damageable.transform.position - center).normalized;
            damageable.TakeDamage(applied, center, dir);
        }
    }

    public static void SpawnFlash(Vector3 center, float radius, Color color) {
        var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flash.name = "ExplosionFlash";
        flash.transform.position = center;
        float scale = Mathf.Max(2f, radius * 0.35f);
        flash.transform.localScale = Vector3.one * scale;

        Object.Destroy(flash.GetComponent<Collider>());
        var renderer = flash.GetComponent<MeshRenderer>();
        if (renderer != null) {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = new Color(color.r, color.g, color.b, 0.85f);
        }

        var fade = flash.AddComponent<ExplosionFlash>();
        fade.lifetime = 0.35f;
        fade.startScale = scale;
        fade.endScale = scale * 2.2f;

        PlayExplosionSound(center, radius);
    }

    static void PlayExplosionSound(Vector3 center, float radius) {
        var clip = Resources.Load<AudioClip>("Audio/Weapons/smartbomb");
        if (clip == null) {
            clip = Resources.Load<AudioClip>("Audio/Weapons/rocket");
        }
        if (clip == null) {
            return;
        }
        // One-shot at world position; scale volume by blast size a bit.
        float volume = Mathf.Clamp(0.35f + radius / 120f, 0.35f, 1f);
        AudioSource.PlayClipAtPoint(clip, center, volume);
    }
}

/// <summary>Brief expanding sphere used as a cheap explosion VFX.</summary>
public class ExplosionFlash : MonoBehaviour {
    public float lifetime = 0.35f;
    public float startScale = 1f;
    public float endScale = 2f;
    float age;
    MeshRenderer meshRenderer;

    void Awake() {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Update() {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        float scale = Mathf.Lerp(startScale, endScale, t);
        transform.localScale = Vector3.one * scale;
        if (meshRenderer != null && meshRenderer.material != null) {
            Color c = meshRenderer.material.color;
            c.a = 1f - t;
            meshRenderer.material.color = c;
        }
        if (t >= 1f) {
            Destroy(gameObject);
        }
    }
}

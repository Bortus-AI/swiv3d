using UnityEngine;

/// <summary>
/// Shared explosion helpers for rockets, missiles, napalm impact, smart bombs, and buildings.
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

    /// <summary>
    /// Multi-layer building destruction: fireball, smoke, sparks, shockwave, light + boom.
    /// </summary>
    public static void SpawnBuildingBlast(Vector3 center, float radius, Color fireColor) {
        Vector3 origin = center + Vector3.up * 1.2f;
        float r = Mathf.Max(8f, radius);

        // Core flash
        SpawnFlash(origin, r * 0.85f, fireColor);

        // Secondary brighter core
        SpawnFlash(origin + Vector3.up * 0.5f, r * 0.45f, new Color(1f, 0.95f, 0.55f, 1f));

        SpawnShockwaveRing(center, r);
        SpawnExplosionLight(origin, r, fireColor);

        SpawnParticleBurst(
            "BuildingFireball",
            origin,
            burstCount: 55,
            startSpeed: 14f,
            startSize: 2.8f,
            lifetime: 0.7f,
            gravity: -0.15f,
            sphereRadius: 1.2f,
            startColor: new Color(1f, 0.55f, 0.12f, 0.95f),
            midColor: new Color(1f, 0.25f, 0.05f, 0.7f),
            endColor: new Color(0.15f, 0.08f, 0.05f, 0f),
            sizeMulEnd: 2.2f,
            destroyAfter: 2.5f
        );

        SpawnParticleBurst(
            "BuildingSmoke",
            origin + Vector3.up * 0.8f,
            burstCount: 40,
            startSpeed: 5f,
            startSize: 3.5f,
            lifetime: 2.4f,
            gravity: -0.35f,
            sphereRadius: 1.8f,
            startColor: new Color(0.25f, 0.22f, 0.2f, 0.75f),
            midColor: new Color(0.18f, 0.16f, 0.15f, 0.45f),
            endColor: new Color(0.1f, 0.1f, 0.1f, 0f),
            sizeMulEnd: 3.5f,
            destroyAfter: 4f
        );

        SpawnParticleBurst(
            "BuildingSparks",
            origin,
            burstCount: 48,
            startSpeed: 22f,
            startSize: 0.35f,
            lifetime: 0.9f,
            gravity: 1.2f,
            sphereRadius: 0.6f,
            startColor: new Color(1f, 0.85f, 0.3f, 1f),
            midColor: new Color(1f, 0.4f, 0.05f, 0.9f),
            endColor: new Color(0.3f, 0.05f, 0f, 0f),
            sizeMulEnd: 0.2f,
            destroyAfter: 2f
        );

        SpawnParticleBurst(
            "BuildingDebrisDust",
            origin,
            burstCount: 36,
            startSpeed: 9f,
            startSize: 1.8f,
            lifetime: 1.4f,
            gravity: 0.5f,
            sphereRadius: 1.5f,
            startColor: new Color(0.4f, 0.34f, 0.28f, 0.8f),
            midColor: new Color(0.28f, 0.24f, 0.2f, 0.4f),
            endColor: new Color(0.15f, 0.12f, 0.1f, 0f),
            sizeMulEnd: 2.8f,
            destroyAfter: 3f
        );
    }

    public static void SpawnShockwaveRing(Vector3 groundCenter, float radius) {
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "BuildingShockwave";
        ring.transform.position = groundCenter + Vector3.up * 0.4f;
        ring.transform.localScale = new Vector3(1.5f, 0.08f, 1.5f);
        Object.Destroy(ring.GetComponent<Collider>());

        var renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null) {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = new Color(1f, 0.7f, 0.25f, 0.65f);
        }

        var wave = ring.AddComponent<ShockwaveRing>();
        wave.lifetime = 0.45f;
        wave.startRadius = 1.5f;
        wave.endRadius = Mathf.Max(6f, radius * 1.35f);
        wave.height = 0.08f;
    }

    static void SpawnExplosionLight(Vector3 center, float radius, Color color) {
        var go = new GameObject("ExplosionLight");
        go.transform.position = center;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = Color.Lerp(color, Color.white, 0.35f);
        light.intensity = Mathf.Clamp(radius * 0.55f, 6f, 28f);
        light.range = Mathf.Max(12f, radius * 2.2f);
        light.shadows = LightShadows.None;

        var fade = go.AddComponent<ExplosionLightFade>();
        fade.lifetime = 0.55f;
        fade.startIntensity = light.intensity;
    }

    internal static void SpawnParticleBurst(
        string name,
        Vector3 position,
        int burstCount,
        float startSpeed,
        float startSize,
        float lifetime,
        float gravity,
        float sphereRadius,
        Color startColor,
        Color midColor,
        Color endColor,
        float sizeMulEnd,
        float destroyAfter
    ) {
        var go = new GameObject(name);
        go.transform.position = position;

        var ps = go.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.25f;
        main.loop = false;
        main.startLifetime = lifetime;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = startColor;
        main.gravityModifier = gravity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = burstCount + 8;
        main.playOnAwake = false;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)burstCount) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = sphereRadius;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(midColor, 0.35f),
                new GradientColorKey(endColor, 1f)
            },
            new[] {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(midColor.a, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f,
            AnimationCurve.EaseInOut(0f, 1f, 1f, sizeMulEnd)
        );

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(gravity < 0f ? 2.5f : 0f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) {
            Material mat = new Material(Shader.Find("Particles/Standard Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader") {
                mat = new Material(Shader.Find("Sprites/Default"));
            }
            renderer.material = mat;
            renderer.material.color = startColor;
        }

        ps.Play();
        Object.Destroy(go, destroyAfter);
    }

    static void PlayExplosionSound(Vector3 center, float radius) {
        var clip = Resources.Load<AudioClip>("Audio/Weapons/smartbomb");
        if (clip == null) {
            clip = Resources.Load<AudioClip>("Audio/Weapons/rocket");
        }
        if (clip == null) {
            return;
        }
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

/// <summary>Flat ring that expands on XZ only (shockwave).</summary>
public class ShockwaveRing : MonoBehaviour {
    public float lifetime = 0.45f;
    public float startRadius = 1.5f;
    public float endRadius = 20f;
    public float height = 0.08f;
    float age;
    MeshRenderer meshRenderer;

    void Awake() {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    void Update() {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        // Ease out so the ring races then softens.
        float eased = 1f - (1f - t) * (1f - t);
        float radius = Mathf.Lerp(startRadius, endRadius, eased);
        transform.localScale = new Vector3(radius, height, radius);
        if (meshRenderer != null && meshRenderer.material != null) {
            Color c = meshRenderer.material.color;
            c.a = (1f - t) * 0.7f;
            meshRenderer.material.color = c;
        }
        if (t >= 1f) {
            Destroy(gameObject);
        }
    }
}

/// <summary>Point light that dies with the fireball.</summary>
public class ExplosionLightFade : MonoBehaviour {
    public float lifetime = 0.55f;
    public float startIntensity = 12f;
    float age;
    Light pointLight;

    void Awake() {
        pointLight = GetComponent<Light>();
    }

    void Update() {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        if (pointLight != null) {
            // Bright pop then rapid falloff.
            float curve = t < 0.15f ? Mathf.Lerp(0.6f, 1f, t / 0.15f) : 1f - ((t - 0.15f) / 0.85f);
            pointLight.intensity = startIntensity * Mathf.Max(0f, curve);
        }
        if (t >= 1f) {
            Destroy(gameObject);
        }
    }
}

using UnityEngine;

/// <summary>
/// Procedural muzzle-flash and impact effects for each weapon type.
/// Projectile bodies are authored prefabs (see Projectile.CreateRuntime); this class only
/// handles the particle/light bursts around firing and hitting, reusing ExplosionUtil where possible.
/// </summary>
internal static class WeaponVisuals {
    internal static void SpawnMuzzleFlash(WeaponType type, Transform firePoint) {
        if (type == WeaponType.Plasma || type == WeaponType.SmartBomb) {
            return;
        }
        Vector3 pos = firePoint.position;
        ExplosionUtil.SpawnParticleBurst(
            "MuzzleSmoke", pos,
            burstCount: 10, startSpeed: 3f, startSize: 0.5f, lifetime: 0.3f, gravity: -0.1f, sphereRadius: 0.15f,
            startColor: new Color(0.9f, 0.9f, 0.85f, 0.8f),
            midColor: new Color(0.6f, 0.6f, 0.6f, 0.4f),
            endColor: new Color(0.4f, 0.4f, 0.4f, 0f),
            sizeMulEnd: 1.8f, destroyAfter: 0.5f
        );
        SpawnLightPop(pos, new Color(1f, 0.85f, 0.5f), 6f, 6f, 0.15f);
    }

    internal static void SpawnImpact(WeaponType type, Vector3 point, WeaponDefinition def, Transform owner) {
        switch (type) {
            case WeaponType.Rockets:
            case WeaponType.HomingMissiles:
                SpawnFireballImpact(point, def);
                break;
            case WeaponType.Napalm:
                SpawnFireSplashImpact(point, def);
                break;
            default:
                break;
        }
    }

    static void SpawnFireballImpact(Vector3 point, WeaponDefinition def) {
        ExplosionUtil.SpawnFlash(point, def.explosionRadius, def.projectileColor);
        ExplosionUtil.SpawnParticleBurst(
            "RocketSmoke", point,
            burstCount: 14, startSpeed: 4f, startSize: 1.2f, lifetime: 0.8f, gravity: -0.2f, sphereRadius: 0.4f,
            startColor: new Color(0.3f, 0.28f, 0.25f, 0.7f),
            midColor: new Color(0.2f, 0.18f, 0.16f, 0.4f),
            endColor: new Color(0.1f, 0.1f, 0.1f, 0f),
            sizeMulEnd: 2f, destroyAfter: 1.6f
        );
        ExplosionUtil.SpawnParticleBurst(
            "RocketSparks", point,
            burstCount: 10, startSpeed: 10f, startSize: 0.15f, lifetime: 0.4f, gravity: 1f, sphereRadius: 0.25f,
            startColor: new Color(1f, 0.8f, 0.3f, 1f),
            midColor: new Color(1f, 0.4f, 0.05f, 0.9f),
            endColor: new Color(0.3f, 0.05f, 0f, 0f),
            sizeMulEnd: 0.2f, destroyAfter: 0.9f
        );
    }

    static void SpawnFireSplashImpact(Vector3 point, WeaponDefinition def) {
        ExplosionUtil.SpawnFlash(point, def.explosionRadius, def.projectileColor);
        ExplosionUtil.SpawnParticleBurst(
            "NapalmSplash", point,
            burstCount: 24, startSpeed: 7f, startSize: 1.4f, lifetime: 0.9f, gravity: 0.1f,
            sphereRadius: Mathf.Max(1f, def.burnRadius * 0.5f),
            startColor: new Color(1f, 0.6f, 0.15f, 0.9f),
            midColor: new Color(1f, 0.3f, 0.05f, 0.6f),
            endColor: new Color(0.2f, 0.05f, 0f, 0f),
            sizeMulEnd: 1.6f, destroyAfter: 1.3f
        );
    }

    static void SpawnLightPop(Vector3 pos, Color color, float intensity, float range, float lifetime) {
        var go = new GameObject("MuzzleFlashLight");
        go.transform.position = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        var fade = go.AddComponent<ExplosionLightFade>();
        fade.lifetime = lifetime;
        fade.startIntensity = intensity;
    }
}

using UnityEngine;

/// <summary>
/// Procedural muzzle-flash and impact effects for each weapon type.
/// Projectile bodies are authored prefabs (see Projectile.CreateRuntime); this class only
/// handles the particle/light bursts around firing and hitting, reusing ExplosionUtil where possible.
/// </summary>
internal static class WeaponVisuals {
    internal static void SpawnMuzzleFlash(WeaponType type, Transform firePoint) {
    }

    internal static void SpawnImpact(WeaponType type, Vector3 point, WeaponDefinition def, Transform owner) {
    }
}

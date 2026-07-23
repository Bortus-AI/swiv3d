using System;
using UnityEngine;

/// <summary>
/// Tunable stats for one weapon slot. Defaults mirror SWIV 3D feel:
/// weak continuous plasma, limited specials with more punch.
/// </summary>
[Serializable]
public class WeaponDefinition {
    public WeaponType type = WeaponType.Plasma;
    public string displayName = "Plasma";
    [Tooltip("Shots per second while holding fire")]
    public float fireRate = 10f;
    public float damage = 8f;
    [Tooltip("-1 = unlimited ammo")]
    public int maxAmmo = -1;
    public int startingAmmo = -1;
    public float projectileSpeed = 200f;
    public float projectileLifetime = 3f;
    public float projectileRadius = 0.25f;
    [Tooltip("Area damage radius (rockets/missiles/bombs). 0 = point hit only.")]
    public float explosionRadius = 0f;
    public float explosionDamage = 0f;
    public Color projectileColor = Color.cyan;
    public float projectileScale = 0.4f;
    public bool isHoming = false;
    public float homingTurnRate = 180f;
    public float homingRange = 120f;
    [Tooltip("Napalm leaves a burning zone for this many seconds")]
    public float burnDuration = 0f;
    public float burnDamagePerSecond = 0f;
    public float burnRadius = 0f;
    [Tooltip("Smart bomb / ground shockwave radius")]
    public float smartBombRadius = 0f;

    public static WeaponDefinition CreateDefault(WeaponType type) {
        switch (type) {
            case WeaponType.Plasma:
                return new WeaponDefinition {
                    type = WeaponType.Plasma,
                    displayName = "Plasma",
                    fireRate = 12f,
                    damage = 6f,
                    maxAmmo = -1,
                    startingAmmo = -1,
                    projectileSpeed = 250f,
                    projectileLifetime = 1.5f,
                    projectileRadius = 0.2f,
                    projectileColor = new Color(0.35f, 0.75f, 1f, 1f),
                    projectileScale = 0.25f
                };
            case WeaponType.Rockets:
                return new WeaponDefinition {
                    type = WeaponType.Rockets,
                    displayName = "Rockets",
                    fireRate = 2f,
                    damage = 40f,
                    maxAmmo = 40,
                    startingAmmo = 16,
                    projectileSpeed = 90f,
                    projectileLifetime = 5f,
                    projectileRadius = 0.4f,
                    explosionRadius = 8f,
                    explosionDamage = 50f,
                    projectileColor = new Color(1f, 0.55f, 0.1f, 1f),
                    projectileScale = 0.55f
                };
            case WeaponType.HomingMissiles:
                return new WeaponDefinition {
                    type = WeaponType.HomingMissiles,
                    displayName = "Homing Missiles",
                    fireRate = 1.5f,
                    damage = 55f,
                    maxAmmo = 20,
                    startingAmmo = 8,
                    projectileSpeed = 70f,
                    projectileLifetime = 6f,
                    projectileRadius = 0.35f,
                    explosionRadius = 10f,
                    explosionDamage = 65f,
                    projectileColor = new Color(1f, 0.2f, 0.2f, 1f),
                    projectileScale = 0.5f,
                    isHoming = true,
                    homingTurnRate = 220f,
                    homingRange = 150f
                };
            case WeaponType.Napalm:
                return new WeaponDefinition {
                    type = WeaponType.Napalm,
                    displayName = "Napalm Bomb",
                    fireRate = 1f,
                    damage = 20f,
                    maxAmmo = 12,
                    startingAmmo = 4,
                    // Lobbed free-fall bomb (Projectile adds gravity); not a powered missile.
                    projectileSpeed = 38f,
                    projectileLifetime = 5f,
                    projectileRadius = 0.55f,
                    explosionRadius = 6f,
                    explosionDamage = 25f,
                    projectileColor = new Color(1f, 0.85f, 0.15f, 1f),
                    projectileScale = 1.15f,
                    burnDuration = 5f,
                    burnDamagePerSecond = 18f,
                    burnRadius = 12f
                };
            case WeaponType.SmartBomb:
                return new WeaponDefinition {
                    type = WeaponType.SmartBomb,
                    displayName = "Smart Bomb",
                    fireRate = 0.4f,
                    damage = 0f,
                    maxAmmo = 4,
                    startingAmmo = 1,
                    projectileSpeed = 0f,
                    projectileLifetime = 0f,
                    smartBombRadius = 80f,
                    explosionDamage = 200f,
                    projectileColor = new Color(1f, 1f, 0.6f, 1f)
                };
            default:
                return new WeaponDefinition();
        }
    }
}

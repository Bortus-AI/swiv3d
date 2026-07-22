using UnityEngine;

/// <summary>
/// Collectible ammo / special-weapon crate. Fly through it to add ammo.
/// Runtime helpers can spawn crates if no mesh is assigned.
/// </summary>
[RequireComponent(typeof(SphereCollider))]
public class WeaponPickup : MonoBehaviour {
    [SerializeField] WeaponType weaponType = WeaponType.Rockets;
    [SerializeField] int ammoAmount = 4;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobAmplitude = 0.4f;
    [SerializeField] float bobSpeed = 2f;
    [SerializeField] bool destroyOnPickup = true;

    Vector3 startPos;
    float bobPhase;

    void Reset() {
        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
    }

    void Start() {
        startPos = transform.position;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        var col = GetComponent<SphereCollider>();
        col.isTrigger = true;
        if (col.radius < 0.5f) {
            col.radius = 1.5f;
        }
    }

    void Update() {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        float y = startPos.y + Mathf.Sin(Time.time * bobSpeed + bobPhase) * bobAmplitude;
        transform.position = new Vector3(startPos.x, y, startPos.z);
    }

    void OnTriggerEnter(Collider other) {
        var weapons = other.GetComponentInParent<PlayerWeapons>();
        if (weapons == null) {
            return;
        }

        weapons.AddAmmo(weaponType, ammoAmount);
        if (destroyOnPickup) {
            Destroy(gameObject);
        }
    }

    /// <summary>Spawns a simple colored pickup cube for testing / level design.</summary>
    public static WeaponPickup Spawn(Vector3 position, WeaponType type, int amount) {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = type + "Pickup";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 2f;

        Object.Destroy(go.GetComponent<Collider>());
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.2f;

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null) {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = ColorFor(type);
        }

        var pickup = go.AddComponent<WeaponPickup>();
        pickup.weaponType = type;
        pickup.ammoAmount = amount;
        return pickup;
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
}

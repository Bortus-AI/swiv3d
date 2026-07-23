using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player weapon controller for the SWIV 3D helicopter.
/// Fire = primary plasma (unlimited). AltFire = selected special. SwitchAmmo = cycle specials.
/// Uses the existing MachineGunParticles child for plasma muzzle VFX when present.
/// </summary>
public class PlayerWeapons : MonoBehaviour {
    [Header("Muzzle")]
    [SerializeField] Transform firePoint;
    [SerializeField] ParticleSystem machineGunParticles;
    [SerializeField] float firePointForwardOffset = 2.5f;
    [SerializeField] float firePointDownOffset = 0.4f;

    [Header("Audio (optional — assign original SOUNDS.BD clips)")]
    [SerializeField] AudioSource weaponAudio;
    [SerializeField] AudioClip plasmaClip;
    [SerializeField] AudioClip rocketClip;
    [SerializeField] AudioClip missileClip;
    [SerializeField] AudioClip napalmClip;
    [SerializeField] AudioClip smartBombClip;
    [SerializeField] AudioClip emptyClip;
    [SerializeField] AudioClip switchClip;

    [Header("Overrides (leave empty to use SWIV-style defaults)")]
    [SerializeField] List<WeaponDefinition> weaponOverrides = new List<WeaponDefinition>();

    [Header("HUD")]
    [SerializeField] bool showHud = true;

    readonly Dictionary<WeaponType, WeaponDefinition> definitions = new Dictionary<WeaponType, WeaponDefinition>();
    readonly Dictionary<WeaponType, int> ammo = new Dictionary<WeaponType, int>();
    readonly List<WeaponType> specialOrder = new List<WeaponType> {
        WeaponType.Rockets,
        WeaponType.HomingMissiles,
        WeaponType.Napalm,
        WeaponType.SmartBomb
    };

    PlayerControls playerControls;
    int specialIndex;
    float nextPrimaryFireTime;
    float nextSpecialFireTime;
    float nextSwitchTime;
    bool primaryHeld;
    bool specialHeld;
    ParticleSystem.EmissionModule emission;

    public WeaponType SelectedSpecial => specialOrder[Mathf.Clamp(specialIndex, 0, specialOrder.Count - 1)];
    public int GetAmmo(WeaponType type) => ammo.TryGetValue(type, out int value) ? value : 0;

    void Awake() {
        playerControls = new PlayerControls();
        BuildDefinitions();
        InitAmmo();
        ResolveFirePoint();
        ResolveParticles();
        ResolveWeaponAudio();
        TryLoadDefaultClips();
    }

    void ResolveWeaponAudio() {
        // Prefer a dedicated source so one-shots do not fight the looping rotor AudioSource.
        if (weaponAudio != null) {
            return;
        }
        var existing = transform.Find("WeaponAudio");
        if (existing != null) {
            weaponAudio = existing.GetComponent<AudioSource>();
            if (weaponAudio != null) {
                return;
            }
        }
        var go = new GameObject("WeaponAudio");
        go.transform.SetParent(transform, false);
        weaponAudio = go.AddComponent<AudioSource>();
        weaponAudio.playOnAwake = false;
        weaponAudio.loop = false;
        weaponAudio.spatialBlend = 0f;
        weaponAudio.volume = 0.9f;
    }

    /// <summary>
    /// Loads clips from Resources/Audio/Weapons when inspector slots are empty.
    /// Safe no-op if the Resources folder is not set up.
    /// </summary>
    void TryLoadDefaultClips() {
        if (plasmaClip == null) {
            plasmaClip = Resources.Load<AudioClip>("Audio/Weapons/plasma");
        }
        if (rocketClip == null) {
            rocketClip = Resources.Load<AudioClip>("Audio/Weapons/rocket");
        }
        if (missileClip == null) {
            missileClip = Resources.Load<AudioClip>("Audio/Weapons/missile");
        }
        if (napalmClip == null) {
            napalmClip = Resources.Load<AudioClip>("Audio/Weapons/napalm");
        }
        if (smartBombClip == null) {
            smartBombClip = Resources.Load<AudioClip>("Audio/Weapons/smartbomb");
        }
        if (emptyClip == null) {
            emptyClip = Resources.Load<AudioClip>("Audio/Weapons/empty");
        }
        if (switchClip == null) {
            switchClip = Resources.Load<AudioClip>("Audio/Weapons/switch");
        }
    }

    void OnEnable() {
        if (playerControls == null) {
            return;
        }
        playerControls.Enable();
        playerControls.Player.Fire.started += OnFireStarted;
        playerControls.Player.Fire.canceled += OnFireCanceled;
        playerControls.Player.AltFire.started += OnAltFireStarted;
        playerControls.Player.AltFire.canceled += OnAltFireCanceled;
        playerControls.Player.SwitchAmmo.performed += OnSwitchAmmo;
    }

    void OnDisable() {
        if (playerControls == null) {
            return;
        }
        playerControls.Player.Fire.started -= OnFireStarted;
        playerControls.Player.Fire.canceled -= OnFireCanceled;
        playerControls.Player.AltFire.started -= OnAltFireStarted;
        playerControls.Player.AltFire.canceled -= OnAltFireCanceled;
        playerControls.Player.SwitchAmmo.performed -= OnSwitchAmmo;
        playerControls.Disable();
        SetMachineGunParticles(false);
    }

    void BuildDefinitions() {
        foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType))) {
            definitions[type] = WeaponDefinition.CreateDefault(type);
        }
        for (int i = 0; i < weaponOverrides.Count; i++) {
            var over = weaponOverrides[i];
            if (over != null) {
                definitions[over.type] = over;
            }
        }
    }

    void InitAmmo() {
        foreach (var pair in definitions) {
            int start = pair.Value.startingAmmo;
            if (start < 0 && pair.Value.maxAmmo >= 0) {
                start = pair.Value.maxAmmo;
            }
            ammo[pair.Key] = start;
        }
    }

    void ResolveFirePoint() {
        if (firePoint != null) {
            return;
        }
        var existing = transform.Find("FirePoint");
        if (existing != null) {
            firePoint = existing;
            return;
        }
        var go = new GameObject("FirePoint");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, -firePointDownOffset, firePointForwardOffset);
        firePoint = go.transform;
    }

    void ResolveParticles() {
        if (machineGunParticles == null) {
            var t = transform.Find("MachineGunParticles");
            if (t != null) {
                machineGunParticles = t.GetComponent<ParticleSystem>();
            }
        }
        if (machineGunParticles == null) {
            return;
        }

        // Stop auto-fire visual; we drive emission while primary is held.
        var main = machineGunParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        emission = machineGunParticles.emission;
        emission.enabled = true;
        emission.rateOverTime = 40f;
        machineGunParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void OnFireStarted(InputAction.CallbackContext ctx) {
        primaryHeld = true;
        SetMachineGunParticles(true);
    }

    void OnFireCanceled(InputAction.CallbackContext ctx) {
        primaryHeld = false;
        SetMachineGunParticles(false);
    }

    void OnAltFireStarted(InputAction.CallbackContext ctx) {
        specialHeld = true;
    }

    void OnAltFireCanceled(InputAction.CallbackContext ctx) {
        specialHeld = false;
    }

    void OnSwitchAmmo(InputAction.CallbackContext ctx) {
        float scroll = ctx.ReadValue<float>();
        CycleSpecial(scroll >= 0f ? 1 : -1);
    }

    void Update() {
        // Mouse wheel: SwitchAmmo is authored as Button+1DAxis and is unreliable, so poll scroll.
        if (Mouse.current != null && Time.time >= nextSwitchTime) {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f) {
                CycleSpecial(scrollY > 0f ? 1 : -1);
            }
        }

        // Number keys 1-4 jump to a special weapon.
        if (Keyboard.current != null) {
            if (Keyboard.current.digit1Key.wasPressedThisFrame) { specialIndex = 0; }
            if (Keyboard.current.digit2Key.wasPressedThisFrame) { specialIndex = 1; }
            if (Keyboard.current.digit3Key.wasPressedThisFrame) { specialIndex = 2; }
            if (Keyboard.current.digit4Key.wasPressedThisFrame) { specialIndex = 3; }
        }

        if (primaryHeld) {
            TryFirePrimary();
        }
        if (specialHeld) {
            TryFireSpecial();
        }
    }

    void CycleSpecial(int direction) {
        if (specialOrder.Count == 0) {
            return;
        }
        // Debounce wheel + input-action double fires.
        if (Time.time < nextSwitchTime) {
            return;
        }
        nextSwitchTime = Time.time + 0.12f;
        specialIndex = (specialIndex + direction + specialOrder.Count) % specialOrder.Count;
        PlayOneShot(switchClip);
    }

    void TryFirePrimary() {
        var def = definitions[WeaponType.Plasma];
        if (Time.time < nextPrimaryFireTime) {
            return;
        }
        nextPrimaryFireTime = Time.time + (1f / Mathf.Max(0.1f, def.fireRate));
        FireProjectile(def);
        PlayOneShot(plasmaClip, 0.35f);
    }

    void TryFireSpecial() {
        var type = SelectedSpecial;
        var def = definitions[type];
        if (Time.time < nextSpecialFireTime) {
            return;
        }

        if (!HasAmmo(type)) {
            PlayOneShot(emptyClip, 0.5f);
            nextSpecialFireTime = Time.time + 0.25f;
            return;
        }

        nextSpecialFireTime = Time.time + (1f / Mathf.Max(0.1f, def.fireRate));

        if (type == WeaponType.SmartBomb) {
            FireSmartBomb(def);
        } else {
            FireProjectile(def);
        }

        ConsumeAmmo(type);
        PlaySpecialSound(type);
    }

    bool HasAmmo(WeaponType type) {
        var def = definitions[type];
        if (def.maxAmmo < 0) {
            return true;
        }
        return ammo.TryGetValue(type, out int count) && count > 0;
    }

    void ConsumeAmmo(WeaponType type) {
        var def = definitions[type];
        if (def.maxAmmo < 0) {
            return;
        }
        if (!ammo.ContainsKey(type)) {
            ammo[type] = 0;
        }
        ammo[type] = Mathf.Max(0, ammo[type] - 1);
    }

    public void AddAmmo(WeaponType type, int amount) {
        if (!definitions.ContainsKey(type)) {
            return;
        }
        var def = definitions[type];
        if (def.maxAmmo < 0) {
            return;
        }
        if (!ammo.ContainsKey(type)) {
            ammo[type] = 0;
        }
        ammo[type] = Mathf.Min(def.maxAmmo, ammo[type] + amount);
    }

    /// <summary>Jump the special-weapon selector to a type (used by supply-pad reloads).</summary>
    public void SelectSpecial(WeaponType type) {
        int idx = specialOrder.IndexOf(type);
        if (idx >= 0) {
            specialIndex = idx;
        }
    }

    void FireProjectile(WeaponDefinition def) {
        Vector3 origin = firePoint.position;
        // Rockets/missiles: slight downward bias. Napalm bombs: lobbed drop, not a missile launch.
        float downBias = def.type == WeaponType.Napalm ? 0.55f : 0.05f;
        Vector3 direction = (transform.forward + Vector3.down * downBias).normalized;
        Projectile.CreateRuntime(def, origin, direction, transform);
        if (def.type != WeaponType.Napalm) {
            WeaponVisuals.SpawnMuzzleFlash(def.type, firePoint);
        }
    }

    void FireSmartBomb(WeaponDefinition def) {
        // SWIV smart bomb: nuclear shockwave across the ground from player.
        Vector3 center = transform.position;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 500f, ~0, QueryTriggerInteraction.Ignore)) {
            center = hit.point;
        }

        ExplosionUtil.ApplyRadiusDamage(center, def.smartBombRadius, def.explosionDamage, transform);
        ExplosionUtil.SpawnFlash(center, def.smartBombRadius, def.projectileColor);

        // Expanding ring visual on the ground.
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "SmartBombShockwave";
        ring.transform.position = center + Vector3.up * 0.5f;
        ring.transform.localScale = new Vector3(2f, 0.2f, 2f);
        Object.Destroy(ring.GetComponent<Collider>());
        var renderer = ring.GetComponent<MeshRenderer>();
        if (renderer != null) {
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.material.color = new Color(1f, 0.95f, 0.4f, 0.7f);
        }
        var flash = ring.AddComponent<ExplosionFlash>();
        flash.lifetime = 0.6f;
        flash.startScale = 2f;
        flash.endScale = def.smartBombRadius * 2f;
    }

    void PlaySpecialSound(WeaponType type) {
        switch (type) {
            case WeaponType.Rockets:
                PlayOneShot(rocketClip, 0.7f);
                break;
            case WeaponType.HomingMissiles:
                PlayOneShot(missileClip, 0.7f);
                break;
            case WeaponType.Napalm:
                PlayOneShot(napalmClip, 0.7f);
                break;
            case WeaponType.SmartBomb:
                PlayOneShot(smartBombClip, 1f);
                break;
        }
    }

    void PlayOneShot(AudioClip clip, float volumeScale = 1f) {
        if (weaponAudio == null || clip == null) {
            return;
        }
        weaponAudio.PlayOneShot(clip, volumeScale);
    }

    void SetMachineGunParticles(bool firing) {
        if (machineGunParticles == null) {
            return;
        }
        if (firing) {
            if (!machineGunParticles.isPlaying) {
                machineGunParticles.Play();
            }
        } else {
            machineGunParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void OnGUI() {
        if (!showHud) {
            return;
        }

        var special = SelectedSpecial;
        var def = definitions[special];
        string ammoText = def.maxAmmo < 0 ? "∞" : GetAmmo(special).ToString();

        const float pad = 12f;
        var style = new GUIStyle(GUI.skin.box) {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 16,
            normal = { textColor = Color.white }
        };
        string text =
            "Primary: Plasma (LMB hold)\n" +
            $"Special: {def.displayName}  [{ammoText}]  (RMB)\n" +
            "Scroll / 1-4: switch specials\n" +
            "Hover yellow-arrow pads to winch ammo";
        GUI.Box(new Rect(pad, Screen.height - 110f - pad, 360f, 110f), text, style);
    }
}

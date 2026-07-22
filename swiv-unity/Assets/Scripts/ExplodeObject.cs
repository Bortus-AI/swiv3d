using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fractures a building into rigidbody fragments with multi-layer explosion VFX/SFX.
/// Call Explode() from Damageable on death — does not auto-explode on Start.
/// Works with House001Intact/House001Fragments or generic Intact/Fragments roots.
/// </summary>
public class ExplodeObject : MonoBehaviour {
    [Header("Structure")]
    [SerializeField] Transform intactObject;
    [SerializeField] Transform[] fragments;
    [SerializeField] float minForce = 350f;
    [SerializeField] float maxForce = 1400f;
    [SerializeField] float radius = 16f;
    [SerializeField] float upwardModifier = 2.2f;
    [SerializeField] bool hideIntactOnExplode = true;
    [SerializeField] bool enableFragmentsOnStart = false;

    [Header("Aftermath")]
    [SerializeField] float debrisLifetime = 10f;
    [SerializeField] float debrisFadeSeconds = 2f;
    [SerializeField] bool playEffects = true;
    [SerializeField] float effectRadius = 16f;
    [SerializeField] Color explosionColor = new Color(1f, 0.45f, 0.1f, 1f);

    bool hasExploded;
    bool configured;

    /// <summary>Wire intact mesh + debris pieces for procedurally built structures.</summary>
    public void Configure(Transform intact, Transform[] fragList, float blastRadius = 14f, Color? blastColor = null) {
        intactObject = intact;
        fragments = fragList;
        effectRadius = blastRadius;
        radius = Mathf.Max(radius, blastRadius * 0.85f);
        if (blastColor.HasValue) {
            explosionColor = blastColor.Value;
        }
        // Bigger blast radius => stronger debris kick.
        minForce = Mathf.Max(minForce, blastRadius * 22f);
        maxForce = Mathf.Max(maxForce, blastRadius * 85f);
        configured = true;
        if (!enableFragmentsOnStart) {
            SetFragmentsActive(false);
        }
        if (intactObject != null) {
            intactObject.gameObject.SetActive(true);
        }
    }

    void Awake() {
        AutoWireIfNeeded();
        // Always show the intact mesh on spawn; only hide it when exploding.
        if (intactObject != null) {
            intactObject.gameObject.SetActive(true);
        }
        if (!enableFragmentsOnStart) {
            SetFragmentsActive(false);
        }
    }

    void AutoWireIfNeeded() {
        if (configured && intactObject != null && fragments != null && fragments.Length > 0) {
            return;
        }

        if (intactObject == null) {
            intactObject = FindChildByNames(transform, "House001Intact", "Intact");
        }

        if (fragments == null || fragments.Length == 0) {
            Transform fragRoot = FindChildByNames(transform, "House001Fragments", "Fragments");
            if (fragRoot != null) {
                var list = new List<Transform>();
                for (int i = 0; i < fragRoot.childCount; i++) {
                    list.Add(fragRoot.GetChild(i));
                }
                fragments = list.ToArray();
            }
        }
    }

    static Transform FindChildByNames(Transform root, params string[] names) {
        for (int n = 0; n < names.Length; n++) {
            Transform t = root.Find(names[n]);
            if (t != null) {
                return t;
            }
        }
        // Fallback: any direct child whose name contains the key tokens.
        for (int i = 0; i < root.childCount; i++) {
            Transform child = root.GetChild(i);
            string lower = child.name.ToLowerInvariant();
            for (int n = 0; n < names.Length; n++) {
                if (lower.Contains(names[n].ToLowerInvariant())) {
                    return child;
                }
            }
        }
        return null;
    }

    public void Explode() {
        Vector3 origin = intactObject != null ? intactObject.position : transform.position;
        Explode(origin);
    }

    public void Explode(Vector3 origin) {
        if (hasExploded) {
            return;
        }
        hasExploded = true;

        AutoWireIfNeeded();

        // Prefer building center for a symmetric blast.
        Vector3 blastCenter = intactObject != null ? intactObject.position : transform.position;
        if (origin.sqrMagnitude > 0.01f) {
            // Blend hit point toward building center so debris still flies outward.
            blastCenter = Vector3.Lerp(blastCenter, origin, 0.35f);
        }

        if (hideIntactOnExplode && intactObject != null) {
            intactObject.gameObject.SetActive(false);
        }

        if (playEffects) {
            ExplosionUtil.SpawnBuildingBlast(blastCenter, effectRadius, explosionColor);
        }

        if (fragments == null || fragments.Length == 0) {
            return;
        }

        SetFragmentsActive(true);

        foreach (Transform fragment in fragments) {
            if (fragment == null) {
                continue;
            }

            var rigidBody = fragment.GetComponent<Rigidbody>();
            if (rigidBody == null) {
                rigidBody = fragment.gameObject.AddComponent<Rigidbody>();
            }
            rigidBody.isKinematic = false;
            rigidBody.useGravity = true;
            rigidBody.mass = Random.Range(0.5f, 3.2f);
            rigidBody.linearDamping = 0.12f;
            rigidBody.angularDamping = 0.25f;
            rigidBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (fragment.GetComponent<Collider>() == null) {
                fragment.gameObject.AddComponent<BoxCollider>();
            }

            float force = Random.Range(minForce, maxForce);
            // Extra kick for pieces further from center so the structure rips apart.
            float dist = Vector3.Distance(fragment.position, blastCenter);
            force *= Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01(dist / Mathf.Max(1f, radius)));

            rigidBody.AddExplosionForce(force, blastCenter, radius, upwardModifier, ForceMode.Impulse);
            rigidBody.AddTorque(Random.insideUnitSphere * force * 0.08f, ForceMode.Impulse);
        }

        if (debrisLifetime > 0f) {
            StartCoroutine(CleanupDebris());
        }
    }

    IEnumerator CleanupDebris() {
        yield return new WaitForSeconds(debrisLifetime);

        if (fragments == null) {
            yield break;
        }

        float fadeEnd = Time.time + debrisFadeSeconds;
        var renderers = new List<Renderer>();
        foreach (Transform fragment in fragments) {
            if (fragment == null) {
                continue;
            }
            var rb = fragment.GetComponent<Rigidbody>();
            if (rb != null) {
                rb.isKinematic = true;
                rb.detectCollisions = false;
            }
            var cols = fragment.GetComponentsInChildren<Collider>();
            for (int i = 0; i < cols.Length; i++) {
                cols[i].enabled = false;
            }
            renderers.AddRange(fragment.GetComponentsInChildren<Renderer>());
        }

        while (Time.time < fadeEnd) {
            float t = 1f - Mathf.Clamp01((fadeEnd - Time.time) / Mathf.Max(0.01f, debrisFadeSeconds));
            for (int i = 0; i < renderers.Count; i++) {
                var r = renderers[i];
                if (r == null || r.material == null) {
                    continue;
                }
                if (r.material.HasProperty("_Color")) {
                    Color c = r.material.color;
                    c.a = 1f - t;
                    r.material.color = c;
                }
            }
            yield return null;
        }

        Destroy(gameObject);
    }

    void SetFragmentsActive(bool active) {
        if (fragments == null) {
            return;
        }

        // Activate/deactivate the fragments root once if present.
        Transform fragRoot = FindChildByNames(transform, "House001Fragments", "Fragments");
        if (fragRoot != null) {
            fragRoot.gameObject.SetActive(active);
        }

        for (int i = 0; i < fragments.Length; i++) {
            if (fragments[i] == null) {
                continue;
            }
            fragments[i].gameObject.SetActive(active);
        }
    }
}

using System.Collections;
using UnityEngine;

/// <summary>
/// Fractures a building into rigidbody fragments with explosion VFX/SFX.
/// Call Explode() from Damageable on death — does not auto-explode on Start.
/// </summary>
public class ExplodeObject : MonoBehaviour {
    [Header("Structure")]
    [SerializeField] Transform intactObject;
    [SerializeField] Transform[] fragments;
    [SerializeField] float minForce = 200f;
    [SerializeField] float maxForce = 900f;
    [SerializeField] float radius = 12f;
    [SerializeField] float upwardModifier = 1.5f;
    [SerializeField] bool hideIntactOnExplode = true;
    [SerializeField] bool enableFragmentsOnStart = false;

    [Header("Aftermath")]
    [SerializeField] float debrisLifetime = 10f;
    [SerializeField] float debrisFadeSeconds = 2f;
    [SerializeField] bool playEffects = true;
    [SerializeField] float effectRadius = 14f;
    [SerializeField] Color explosionColor = new Color(1f, 0.55f, 0.15f, 1f);

    bool hasExploded;

    void Awake() {
        AutoWireIfNeeded();
        if (!enableFragmentsOnStart) {
            SetFragmentsActive(false);
        }
    }

    void AutoWireIfNeeded() {
        if (intactObject == null) {
            var intact = transform.Find("House001Intact");
            if (intact != null) {
                intactObject = intact;
            }
        }

        if (fragments == null || fragments.Length == 0) {
            var fragRoot = transform.Find("House001Fragments");
            if (fragRoot != null) {
                var list = new System.Collections.Generic.List<Transform>();
                for (int i = 0; i < fragRoot.childCount; i++) {
                    list.Add(fragRoot.GetChild(i));
                }
                fragments = list.ToArray();
            }
        }
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

        if (hideIntactOnExplode && intactObject != null) {
            intactObject.gameObject.SetActive(false);
        }

        if (playEffects) {
            ExplosionUtil.SpawnFlash(origin + Vector3.up * 1.5f, effectRadius, explosionColor);
            SpawnDustBurst(origin);
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
            rigidBody.mass = Random.Range(0.4f, 2.5f);
            rigidBody.drag = 0.15f;
            rigidBody.angularDrag = 0.35f;

            if (fragment.GetComponent<Collider>() == null) {
                fragment.gameObject.AddComponent<BoxCollider>();
            }

            float force = Random.Range(minForce, maxForce);
            rigidBody.AddExplosionForce(force, origin, radius, upwardModifier, ForceMode.Impulse);
            rigidBody.AddTorque(Random.insideUnitSphere * force * 0.05f, ForceMode.Impulse);
        }

        if (debrisLifetime > 0f) {
            StartCoroutine(CleanupDebris());
        }
    }

    void SpawnDustBurst(Vector3 origin) {
        var dust = new GameObject("BuildingDust");
        dust.transform.position = origin + Vector3.up * 0.5f;

        var ps = dust.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = 0.4f;
        main.loop = false;
        main.startLifetime = 1.2f;
        main.startSpeed = 8f;
        main.startSize = 2.5f;
        main.startColor = new Color(0.35f, 0.3f, 0.25f, 0.75f);
        main.gravityModifier = 0.35f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 48;
        main.playOnAwake = false;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 36) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1.5f;

        var colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.45f, 0.38f, 0.3f), 0f),
                new GradientColorKey(new Color(0.2f, 0.18f, 0.15f), 1f)
            },
            new[] {
                new GradientAlphaKey(0.8f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 2.5f));

        var renderer = dust.GetComponent<ParticleSystemRenderer>();
        if (renderer != null) {
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            if (renderer.material.shader == null || renderer.material.shader.name == "Hidden/InternalErrorShader") {
                renderer.material = new Material(Shader.Find("Sprites/Default"));
            }
            renderer.material.color = new Color(0.4f, 0.35f, 0.3f, 0.7f);
        }

        ps.Play();
        Destroy(dust, 3f);
    }

    IEnumerator CleanupDebris() {
        yield return new WaitForSeconds(debrisLifetime);

        if (fragments == null) {
            yield break;
        }

        float fadeEnd = Time.time + debrisFadeSeconds;
        var renderers = new System.Collections.Generic.List<Renderer>();
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
                // Best-effort fade for standard materials.
                if (r.material.HasProperty("_Color")) {
                    Color c = r.material.color;
                    c.a = 1f - t;
                    r.material.color = c;
                }
            }
            yield return null;
        }

        // Remove the whole building instance once debris is gone.
        Destroy(gameObject);
    }

    void SetFragmentsActive(bool active) {
        if (fragments == null) {
            return;
        }
        for (int i = 0; i < fragments.Length; i++) {
            if (fragments[i] == null) {
                continue;
            }
            if (fragments[i].parent != null && fragments[i].parent != transform) {
                fragments[i].parent.gameObject.SetActive(active);
            }
            fragments[i].gameObject.SetActive(active);
        }
    }
}

using UnityEngine;

/// <summary>
/// Fractures a building into rigidbody fragments.
/// Call Explode() from Damageable on death — does not auto-explode on Start.
/// </summary>
public class ExplodeObject : MonoBehaviour {
    [SerializeField] Transform intactObject;
    [SerializeField] Transform[] fragments;
    [SerializeField] float minForce = 125f;
    [SerializeField] float maxForce = 750f;
    [SerializeField] float radius = 10f;
    [SerializeField] bool hideIntactOnExplode = true;
    [SerializeField] bool enableFragmentsOnStart = false;

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
        Explode(intactObject != null ? intactObject.position : transform.position);
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

        if (fragments == null) {
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

            if (fragment.GetComponent<Collider>() == null) {
                fragment.gameObject.AddComponent<BoxCollider>();
            }

            rigidBody.AddExplosionForce(Random.Range(minForce, maxForce), origin, radius);
        }
    }

    void SetFragmentsActive(bool active) {
        if (fragments == null) {
            return;
        }
        // Fragments may live under a parent root — enable the root too.
        for (int i = 0; i < fragments.Length; i++) {
            if (fragments[i] != null) {
                if (fragments[i].parent != null && fragments[i].parent != transform) {
                    fragments[i].parent.gameObject.SetActive(active);
                }
                fragments[i].gameObject.SetActive(active);
            }
        }
    }
}

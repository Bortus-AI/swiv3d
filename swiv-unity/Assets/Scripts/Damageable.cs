using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Health component for anything that can be destroyed by player weapons.
/// On death, optionally triggers ExplodeObject and disables colliders/renderers.
/// </summary>
public class Damageable : MonoBehaviour {
    [SerializeField] float maxHealth = 100f;
    [SerializeField] bool destroyOnDeath = false;
    [SerializeField] bool disableOnDeath = true;
    [SerializeField] UnityEvent onDeath;

    float currentHealth;
    bool isDead;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public float HealthNormalized => maxHealth <= 0f ? 0f : currentHealth / maxHealth;

    void Awake() {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitDirection) {
        if (isDead || amount <= 0f) {
            return;
        }

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f) {
            Die(hitPoint, hitDirection);
        }
    }

    public void Heal(float amount) {
        if (isDead) {
            return;
        }
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    void Die(Vector3 hitPoint, Vector3 hitDirection) {
        if (isDead) {
            return;
        }
        isDead = true;

        var explode = GetComponent<ExplodeObject>();
        if (explode != null) {
            explode.Explode(hitPoint);
        }

        onDeath?.Invoke();

        if (destroyOnDeath) {
            Destroy(gameObject);
            return;
        }

        if (disableOnDeath) {
            // If we fractured, leave fragment colliders enabled so debris can tumble.
            if (explode == null) {
                foreach (var col in GetComponentsInChildren<Collider>()) {
                    col.enabled = false;
                }
                foreach (var renderer in GetComponentsInChildren<Renderer>()) {
                    renderer.enabled = false;
                }
            } else {
                // Disable only the root hitbox so projectiles stop registering hits.
                var rootCol = GetComponent<Collider>();
                if (rootCol != null) {
                    rootCol.enabled = false;
                }
            }
        }
    }
}

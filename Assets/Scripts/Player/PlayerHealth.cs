using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.UI;

namespace HorrorEscape.Player
{
    /// <summary>
    /// Tracks player health (HP), damage effects, and death conditions.
    /// Integrates with HUD for health bar updates and red damage flash.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100.0f;
        private float currentHealth;

        [Header("Invulnerability Window")]
        [SerializeField] private float iFrameDuration = 1.0f;
        private float iFrameTimer;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsDead => currentHealth <= 0f;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        private void Start()
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
            }
        }

        private void Update()
        {
            if (iFrameTimer > 0f)
            {
                iFrameTimer -= Time.deltaTime;
            }
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || iFrameTimer > 0f) return;

            iFrameTimer = iFrameDuration;
            currentHealth = Mathf.Max(0f, currentHealth - amount);

            // Update HUD
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
                HUDManager.Instance.TriggerDamageFlash();
            }

            // Play hurt sound
            if (AudioManager.Instance != null && AudioManager.Instance.footstepClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.footstepClip, 1.0f, 0.6f);
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateHealth(currentHealth, maxHealth);
            }
        }

        private void Die()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerGameOver();
            }
        }
    }
}

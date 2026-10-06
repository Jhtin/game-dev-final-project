using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.UI;

namespace HorrorEscape.Player
{
    /// <summary>
    /// Tracks monster proximity, manages heartbeat sound intensity,
    /// triggers paranormal flashlight flicker, and HUD danger vignetting.
    /// </summary>
    public class PlayerSanity : MonoBehaviour
    {
        [Header("Proximity Settings")]
        [SerializeField] private float detectionRange = 22.0f;
        [SerializeField] private float intenseRange = 6.0f;

        [Header("References")]
        [SerializeField] private FlashlightController flashlight;

        private StalkerAI[] allEnemies;
        private float checkTimer;

        private void Start()
        {
            if (flashlight == null)
            {
                flashlight = GetComponentInChildren<FlashlightController>();
            }
            RefreshEnemies();
        }

        private void Update()
        {
            checkTimer += Time.deltaTime;
            if (checkTimer >= 0.5f)
            {
                checkTimer = 0f;
                RefreshEnemies();
            }

            float closestDistance = float.MaxValue;
            if (allEnemies != null)
            {
                foreach (var enemy in allEnemies)
                {
                    if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;
                    float dist = Vector3.Distance(transform.position, enemy.transform.position);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                    }
                }
            }

            float normalizedDanger = 0f;
            if (closestDistance < detectionRange)
            {
                normalizedDanger = 1.0f - Mathf.InverseLerp(intenseRange, detectionRange, closestDistance);
            }

            // Audio heartbeat
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.SetHeartbeatIntensity(normalizedDanger);
            }

            // Flashlight paranormal interference
            if (flashlight != null)
            {
                flashlight.SetParanormalFlicker(normalizedDanger);
            }

            // HUD danger pulse
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetDangerVignette(normalizedDanger);
            }
        }

        private void RefreshEnemies()
        {
            allEnemies = FindObjectsByType<StalkerAI>(FindObjectsSortMode.None);
        }
    }
}

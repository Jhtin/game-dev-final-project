using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Enemy
{
    /// <summary>
    /// Causes ceiling lights and ambient illumination to violently flicker and plunge into
    /// complete blackout when the Backrooms Entity is nearby or actively hunting the player.
    /// In this blackout, the player MUST use their flashlight to navigate and spot the entity.
    /// </summary>
    public class EntityBlackoutZone : MonoBehaviour
    {
        [Header("Distance Thresholds")]
        [Tooltip("Distance at which lights start flickering and buzzing violently.")]
        [SerializeField] private float flickerRange = 26.0f;

        [Tooltip("Distance at which lights completely die into pitch darkness.")]
        [SerializeField] private float blackoutRange = 16.0f;

        [Header("Ambient Lighting in Blackout")]
        [SerializeField] private Color pitchBlackAmbient = new Color(0.015f, 0.015f, 0.012f, 1.0f);
        [SerializeField] private float transitionSpeed = 5.0f;

        [Header("Audio")]
        [SerializeField] private float surgeAudioVolume = 0.85f;

        // Cached Lights
        private class TrackedLight
        {
            public Light light;
            public float originalIntensity;
            public Color originalColor;
            public bool originalEnabled;
        }

        private List<TrackedLight> trackedLights = new List<TrackedLight>();
        private Color originalAmbientColor;
        private AmbientMode originalAmbientMode;

        private Transform playerTransform;
        private FlashlightController playerFlashlight;
        private StalkerAI stalkerAI;

        private bool isBlackoutActive = false;
        private bool isFlickerActive = false;
        private float currentBlackoutAmount = 0.0f; // 0 = normal, 1 = complete blackout
        private float flickerNoiseOffset;
        private float nextPromptTime;
        private AudioSource blackoutAudioSource;

        private void Awake()
        {
            stalkerAI = GetComponent<StalkerAI>();
            flickerNoiseOffset = Random.Range(0f, 1000f);

            blackoutAudioSource = gameObject.AddComponent<AudioSource>();
            blackoutAudioSource.spatialBlend = 0.4f;
            blackoutAudioSource.playOnAwake = false;
            blackoutAudioSource.volume = surgeAudioVolume;
        }

        private void Start()
        {
            FindPlayer();
            CacheSceneLights();
            originalAmbientColor = RenderSettings.ambientLight;
            originalAmbientMode = RenderSettings.ambientMode;
        }

        private void FindPlayer()
        {
            FirstPersonController fpc = FindFirstObjectByType<FirstPersonController>();
            if (fpc != null)
            {
                playerTransform = fpc.transform;
                playerFlashlight = fpc.GetComponentInChildren<FlashlightController>();
            }
        }

        public void CacheSceneLights()
        {
            trackedLights.Clear();
            Light[] allLights = FindObjectsByType<Light>(FindObjectsSortMode.None);

            foreach (var l in allLights)
            {
                if (l == null) continue;

                // Never affect the player's flashlight, fill spill, or weapon muzzle flash
                if (l.GetComponentInParent<FirstPersonController>() != null) continue;
                if (l.name.Contains("Flashlight") || l.name.Contains("Muzzle") || l.name.Contains("EyeLight")) continue;

                trackedLights.Add(new TrackedLight
                {
                    light = l,
                    originalIntensity = l.intensity,
                    originalColor = l.color,
                    originalEnabled = l.enabled
                });
            }

            Debug.Log($"[EntityBlackoutZone] Cached {trackedLights.Count} environment lights for blackout system.");
        }

        private void Update()
        {
            if (stalkerAI != null && stalkerAI.CurrentState == StalkerState.Dead)
            {
                RestoreNormalLighting();
                return;
            }

            if (playerTransform == null)
            {
                FindPlayer();
                if (playerTransform == null) return;
            }

            float dist = Vector3.Distance(transform.position, playerTransform.position);
            bool isChasing = stalkerAI != null && stalkerAI.CurrentState == StalkerState.Chase;

            // Target blackout factor: 1.0 if chasing or within blackout distance, partial if within flicker distance
            float targetBlackout = 0.0f;
            if (isChasing || dist <= blackoutRange)
            {
                targetBlackout = 1.0f;
            }
            else if (dist <= flickerRange)
            {
                float t = 1.0f - Mathf.InverseLerp(blackoutRange, flickerRange, dist);
                targetBlackout = Mathf.Lerp(0.35f, 0.95f, t);
            }

            // Smooth transition into darkness
            currentBlackoutAmount = Mathf.MoveTowards(currentBlackoutAmount, targetBlackout, Time.deltaTime * transitionSpeed);

            bool enteringBlackout = targetBlackout >= 0.95f && !isBlackoutActive;
            bool leavingBlackout = targetBlackout < 0.2f && isBlackoutActive;

            if (enteringBlackout)
            {
                TriggerBlackoutAudio();
                isBlackoutActive = true;
                isFlickerActive = false;

                // Prompt user to turn on flashlight if it is off
                if (playerFlashlight != null && !playerFlashlight.IsOn && Time.time > nextPromptTime)
                {
                    nextPromptTime = Time.time + 8f;
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.ShowNotification("The lights died! Press [F] to use Flashlight!");
                    }
                }
            }
            else if (leavingBlackout)
            {
                TriggerPowerRestoredAudio();
                isBlackoutActive = false;
            }

            if (targetBlackout > 0.05f && targetBlackout < 0.95f)
            {
                isFlickerActive = true;
            }
            else
            {
                isFlickerActive = false;
            }

            ApplyLightingState();
        }

        private void ApplyLightingState()
        {
            // 1. Ambient Lighting lerps to pitch black
            RenderSettings.ambientLight = Color.Lerp(originalAmbientColor, pitchBlackAmbient, currentBlackoutAmount);

            // 2. Adjust Tracked Environment Ceiling Lights
            float time = Time.time;
            for (int i = 0; i < trackedLights.Count; i++)
            {
                var entry = trackedLights[i];
                if (entry.light == null) continue;

                if (currentBlackoutAmount >= 0.92f)
                {
                    // Full Blackout: lights completely cut out!
                    entry.light.intensity = 0f;
                    entry.light.enabled = false;
                }
                else if (isFlickerActive)
                {
                    // Violent Electrical Flicker
                    float noise = Mathf.PerlinNoise(time * 22f + i * 3.7f, flickerNoiseOffset);
                    float microDrop = Random.value < 0.18f ? 0.05f : 1.0f;
                    float flickerFactor = Mathf.Clamp01(noise * microDrop) * (1.0f - currentBlackoutAmount);

                    entry.light.enabled = flickerFactor > 0.02f;
                    entry.light.intensity = entry.originalIntensity * flickerFactor;
                }
                else
                {
                    // Normal / transition state
                    entry.light.enabled = entry.originalEnabled;
                    entry.light.intensity = Mathf.Lerp(entry.originalIntensity, 0f, currentBlackoutAmount);
                }
            }
        }

        private void TriggerBlackoutAudio()
        {
            if (AudioManager.Instance != null)
            {
                // Play electrical popping / power failure sound
                AudioClip popClip = AudioManager.Instance.breakerToggleClip;
                if (popClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(popClip, transform.position, 1.0f, 30f);
                }

                // Entity vocal reaction
                if (AudioManager.Instance.monsterSpottedClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.monsterSpottedClip, transform.position, 0.8f, 25f);
                }
            }

            if (blackoutAudioSource != null)
            {
                blackoutAudioSource.pitch = Random.Range(0.6f, 0.8f);
                blackoutAudioSource.Play();
            }
        }

        private void TriggerPowerRestoredAudio()
        {
            if (AudioManager.Instance != null && AudioManager.Instance.fluorescentHumClip != null)
            {
                AudioManager.Instance.PlayAtPosition(AudioManager.Instance.fluorescentHumClip, transform.position, 0.5f, 20f);
            }
        }

        private void RestoreNormalLighting()
        {
            RenderSettings.ambientLight = originalAmbientColor;
            for (int i = 0; i < trackedLights.Count; i++)
            {
                var entry = trackedLights[i];
                if (entry.light != null)
                {
                    entry.light.enabled = entry.originalEnabled;
                    entry.light.intensity = entry.originalIntensity;
                }
            }
            enabled = false;
        }

        private void OnDisable()
        {
            RestoreNormalLighting();
        }

        private void OnDestroy()
        {
            RestoreNormalLighting();
        }
    }
}

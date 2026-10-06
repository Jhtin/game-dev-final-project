using UnityEngine;
using HorrorEscape.Audio;

namespace HorrorEscape.Player
{
    /// <summary>
    /// Controls player flashlight toggle, battery drainage, smooth sway,
    /// and dynamic flickering (low battery & paranormal monster proximity).
    /// </summary>
    public class FlashlightController : MonoBehaviour
    {
        [Header("Light Settings")]
        [SerializeField] private Light flashlightLight;
        [SerializeField] private float baseIntensity = 2.5f;
        [SerializeField] private bool startsOn = true;

        [Header("Battery System")]
        [SerializeField] private float maxBattery = 100.0f;
        [SerializeField] private float batteryDrainRate = 0.48f; // Lasts ~3.5 minutes of continuous use
        [SerializeField] private float lowBatteryThreshold = 20.0f;
        private bool hasWarnedLowBattery = false;

        [Header("Flashlight Sway / Lag")]
        [SerializeField] private float swaySpeed = 8.0f;
        [SerializeField] private Transform followTarget; // Usually the player camera
        [SerializeField] private Vector3 holdOffset = new Vector3(0.24f, -0.2f, 0.35f);

        [Header("Flicker Settings")]
        [SerializeField] private float flickerSpeed = 15.0f;

        private float currentBattery;
        private bool isOn;
        private float flickerTimer;
        private bool isFlickering;
        private float proximityFlickerIntensity = 0f;

        public float CurrentBattery => currentBattery;
        public float MaxBattery => maxBattery;
        public bool IsOn => isOn;

        private void Awake()
        {
            currentBattery = maxBattery;
            if (flashlightLight == null)
            {
                flashlightLight = GetComponentInChildren<Light>();
            }

            if (followTarget == null && Camera.main != null)
            {
                followTarget = Camera.main.transform;
            }
        }

        private void Start()
        {
            SetFlashlightState(startsOn);
        }

        private void Update()
        {
            HandleInput();
            HandleBattery();
            HandleSway();
            HandleFlicker();
        }

        private void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.F) || Input.GetMouseButtonDown(1))
            {
                ToggleFlashlight();
            }
        }

        public void ToggleFlashlight()
        {
            if (currentBattery <= 0f && !isOn)
            {
                // Can't turn on dead flashlight
                PlayClickSound(0.7f);
                return;
            }

            SetFlashlightState(!isOn);
            PlayClickSound(1.0f);
        }

        private void SetFlashlightState(bool state)
        {
            isOn = state;
            if (flashlightLight != null)
            {
                flashlightLight.enabled = isOn;
            }
        }

        private void HandleBattery()
        {
            if (!isOn) return;

            currentBattery -= batteryDrainRate * Time.deltaTime;

            if (currentBattery <= lowBatteryThreshold && !hasWarnedLowBattery && currentBattery > 0f)
            {
                hasWarnedLowBattery = true;
                if (UI.HUDManager.Instance != null)
                {
                    UI.HUDManager.Instance.ShowNotification("FLASHLIGHT BATTERY LOW");
                }
            }

            if (currentBattery <= 0f)
            {
                currentBattery = 0f;
                SetFlashlightState(false);
                PlayClickSound(0.6f);
            }
        }

        private void HandleSway()
        {
            if (followTarget == null) return;

            // Smoothly interpolate rotation to match camera with slight delay
            transform.rotation = Quaternion.Slerp(transform.rotation, followTarget.rotation, Time.deltaTime * swaySpeed);
            transform.position = followTarget.position + (followTarget.rotation * holdOffset);
        }

        private void HandleFlicker()
        {
            if (!isOn || flashlightLight == null) return;

            bool isLowBattery = currentBattery <= lowBatteryThreshold;
            bool isParanormal = proximityFlickerIntensity > 0.05f;

            float batRatio = Mathf.Clamp01(currentBattery / maxBattery);
            // Light gradually becomes weaker when battery is low
            float weakenedIntensity = baseIntensity * Mathf.Lerp(0.35f, 1.0f, batRatio);

            if (isLowBattery || isParanormal)
            {
                flickerTimer += Time.deltaTime * flickerSpeed;
                float noise = Mathf.PerlinNoise(flickerTimer, 0.0f);

                float threshold = isParanormal ? (0.6f - proximityFlickerIntensity * 0.4f) : 0.48f;
                flashlightLight.enabled = noise > threshold;
                flashlightLight.intensity = weakenedIntensity;
            }
            else
            {
                flashlightLight.enabled = true;
                flashlightLight.intensity = weakenedIntensity;
            }
        }

        public void SetParanormalFlicker(float intensity)
        {
            proximityFlickerIntensity = Mathf.Clamp01(intensity);
        }

        public void Recharge(float amount)
        {
            currentBattery = Mathf.Clamp(currentBattery + amount, 0f, maxBattery);
            if (currentBattery > lowBatteryThreshold)
            {
                hasWarnedLowBattery = false;
            }
            if (!isOn && currentBattery > 5f)
            {
                SetFlashlightState(true);
            }
        }

        private void PlayClickSound(float pitch)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.flashlightClickClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.flashlightClickClip, 0.8f, pitch);
            }
        }
    }
}

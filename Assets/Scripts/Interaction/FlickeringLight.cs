using UnityEngine;

namespace HorrorEscape.Environment
{
    /// <summary>
    /// Atmospheric flickering light for horror corridors and abandoned rooms.
    /// Uses Perlin noise with randomized dips to simulate dying fluorescent tubes.
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickeringLight : MonoBehaviour
    {
        [Header("Flicker Settings")]
        [SerializeField] private float minIntensity = 0.2f;
        [SerializeField] private float maxIntensity = 2.0f;
        [SerializeField] private float flickerSpeed = 12.0f;
        [SerializeField] private float blackoutChance = 0.05f;

        private Light targetLight;
        private float noiseOffset;
        private AudioSource buzzSource;

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            noiseOffset = Random.Range(0f, 1000f);

            // Add 3D electrical buzzing sound
            buzzSource = gameObject.AddComponent<AudioSource>();
            buzzSource.spatialBlend = 1.0f; // Full 3D
            buzzSource.minDistance = 1.0f;
            buzzSource.maxDistance = 9.0f;
            buzzSource.rolloffMode = AudioRolloffMode.Linear;
            buzzSource.loop = true;
            buzzSource.volume = 0.12f;

            if (HorrorEscape.Audio.AudioManager.Instance != null && HorrorEscape.Audio.AudioManager.Instance.fluorescentHumClip != null)
            {
                buzzSource.clip = HorrorEscape.Audio.AudioManager.Instance.fluorescentHumClip;
                buzzSource.pitch = Random.Range(0.9f, 1.15f);
                buzzSource.Play();
            }
        }

        private void Update()
        {
            if (targetLight == null) return;

            // Occasional micro-blackout
            if (Random.value < blackoutChance * Time.deltaTime * 60f)
            {
                targetLight.enabled = false;
                if (buzzSource != null && buzzSource.isPlaying)
                {
                    buzzSource.volume = 0.02f; // Sudden dip in ballast power
                }
                return;
            }

            targetLight.enabled = true;
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
            targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);

            if (buzzSource != null)
            {
                if (!buzzSource.isPlaying && buzzSource.clip != null)
                {
                    buzzSource.Play();
                }
                buzzSource.volume = Mathf.Lerp(0.06f, 0.22f, noise);
            }
        }
    }
}

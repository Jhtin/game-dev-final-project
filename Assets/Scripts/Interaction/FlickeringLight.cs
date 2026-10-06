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

        private void Awake()
        {
            targetLight = GetComponent<Light>();
            noiseOffset = Random.Range(0f, 1000f);
        }

        private void Update()
        {
            if (targetLight == null) return;

            // Occasional micro-blackout
            if (Random.value < blackoutChance * Time.deltaTime * 60f)
            {
                targetLight.enabled = false;
                return;
            }

            targetLight.enabled = true;
            float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, noiseOffset);
            targetLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
        }
    }
}

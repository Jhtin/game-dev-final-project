using System.Collections.Generic;
using UnityEngine;

namespace HorrorEscape.Environment
{
    public enum DarknessZoneType
    {
        Normal,
        PartialBlackout,
        CompleteBlackout
    }

    /// <summary>
    /// Manages atmospheric lighting transitions between Normal, Partial Blackout,
    /// and Complete Blackout zones across the Backrooms.
    /// Modulates local ambient lighting, fog, and distance cues when the player enters
    /// different zones, ensuring true environmental darkness without screen-space overlays.
    /// </summary>
    public class DarknessZoneVolume : MonoBehaviour
    {
        private static readonly List<DarknessZoneVolume> activeZones = new List<DarknessZoneVolume>();
        private static DarknessZoneVolume currentActiveZone = null;
        private static Color defaultAmbientColor = new Color(0.82f, 0.79f, 0.52f);
        private static Color defaultFogColor = new Color(0.72f, 0.68f, 0.42f);
        private static float defaultFogDensity = 0.022f;
        private static bool defaultsCaptured = false;

        [Header("Zone Configuration")]
        [SerializeField] private DarknessZoneType zoneType = DarknessZoneType.CompleteBlackout;
        [SerializeField] private string zoneName = "Blackout Sector";
        [SerializeField] private Vector3 boundsSize = new Vector3(9f, 4f, 9f);

        [Header("Lighting Overrides")]
        [Tooltip("Target ambient color inside this zone")]
        [SerializeField] private Color targetAmbientColor = new Color(0.04f, 0.04f, 0.03f); // Near pitch black
        [Tooltip("Target fog color inside this zone")]
        [SerializeField] private Color targetFogColor = new Color(0.02f, 0.02f, 0.02f);
        [Tooltip("Target fog density inside this zone")]
        [SerializeField] private float targetFogDensity = 0.035f;
        [Tooltip("Speed of transition when entering/exiting")]
        [SerializeField] private float transitionSpeed = 3.5f;

        [Header("Environmental Audio Cues")]
        [SerializeField] private AudioSource zoneAudioSource;
        [SerializeField] private AudioClip eerieDroneClip;
        [Range(0f, 1f)]
        [SerializeField] private float maxAudioVolume = 0.65f;

        private BoxCollider triggerCollider;
        private bool isPlayerInside = false;

        public DarknessZoneType ZoneType => zoneType;
        public string ZoneName => zoneName;
        public static DarknessZoneType CurrentZoneType => currentActiveZone != null ? currentActiveZone.ZoneType : DarknessZoneType.Normal;

        private void Awake()
        {
            if (!defaultsCaptured)
            {
                defaultAmbientColor = RenderSettings.ambientLight;
                defaultFogColor = RenderSettings.fogColor;
                defaultFogDensity = RenderSettings.fogDensity;
                defaultsCaptured = true;
            }

            triggerCollider = GetComponent<BoxCollider>();
            if (triggerCollider == null)
            {
                triggerCollider = gameObject.AddComponent<BoxCollider>();
            }
            triggerCollider.isTrigger = true;
            triggerCollider.size = boundsSize;

            SetupZoneDefaults();
            SetupAudio();
        }

        private void OnEnable()
        {
            if (!activeZones.Contains(this))
                activeZones.Add(this);
        }

        private void OnDisable()
        {
            activeZones.Remove(this);
            if (currentActiveZone == this)
            {
                currentActiveZone = null;
                isPlayerInside = false;
            }
        }

        public void Configure(DarknessZoneType type, string name, Vector3 size)
        {
            zoneType = type;
            zoneName = name;
            boundsSize = size;
            if (triggerCollider != null)
            {
                triggerCollider.size = boundsSize;
            }
            SetupZoneDefaults();
        }

        private void SetupZoneDefaults()
        {
            switch (zoneType)
            {
                case DarknessZoneType.Normal:
                    targetAmbientColor = new Color(0.82f, 0.79f, 0.52f);
                    targetFogColor = new Color(0.72f, 0.68f, 0.42f);
                    targetFogDensity = 0.022f;
                    break;
                case DarknessZoneType.PartialBlackout:
                    // Dim, weak ambient where walls are faintly visible, but distant items are obscured
                    targetAmbientColor = new Color(0.24f, 0.22f, 0.16f);
                    targetFogColor = new Color(0.18f, 0.16f, 0.12f);
                    targetFogDensity = 0.028f;
                    break;
                case DarknessZoneType.CompleteBlackout:
                    // Near-total pitch darkness: walls and objects barely discernable without flashlight
                    targetAmbientColor = new Color(0.025f, 0.022f, 0.018f);
                    targetFogColor = new Color(0.015f, 0.015f, 0.012f);
                    targetFogDensity = 0.038f;
                    break;
            }
        }

        private void SetupAudio()
        {
            if (zoneAudioSource == null && (zoneType == DarknessZoneType.CompleteBlackout || zoneType == DarknessZoneType.PartialBlackout))
            {
                zoneAudioSource = gameObject.AddComponent<AudioSource>();
                zoneAudioSource.spatialBlend = 0.0f; // 2D ambient presence when inside
                zoneAudioSource.loop = true;
                zoneAudioSource.playOnAwake = false;
                zoneAudioSource.volume = 0f;

                // Load eerie ambient drone if available
                if (eerieDroneClip == null)
                {
#if UNITY_EDITOR
                    string clipPath = "Assets/Backrooms Ambience/4_LOOP_Backrooms Stalker (by juanjo_sound).wav";
                    eerieDroneClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
#endif
                }
                if (eerieDroneClip != null)
                {
                    zoneAudioSource.clip = eerieDroneClip;
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Player.FirstPersonController>() != null)
            {
                isPlayerInside = true;
                currentActiveZone = this;

                if (zoneAudioSource != null && !zoneAudioSource.isPlaying && eerieDroneClip != null)
                {
                    zoneAudioSource.Play();
                }

                if (zoneType == DarknessZoneType.CompleteBlackout)
                {
                    UI.HUDManager.Instance?.ShowNotification($"ENTERED: {zoneName.ToUpper()} (FLASHLIGHT REQUIRED)");
                }
                else if (zoneType == DarknessZoneType.PartialBlackout)
                {
                    UI.HUDManager.Instance?.ShowNotification($"ENTERED: {zoneName.ToUpper()}");
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<Player.FirstPersonController>() != null)
            {
                isPlayerInside = false;
                if (currentActiveZone == this)
                {
                    // Find if player is inside another overlapping zone
                    DarknessZoneVolume nextZone = null;
                    for (int i = 0; i < activeZones.Count; i++)
                    {
                        if (activeZones[i] != this && activeZones[i].isPlayerInside)
                        {
                            nextZone = activeZones[i];
                            break;
                        }
                    }
                    currentActiveZone = nextZone;
                }
            }
        }

        private void Update()
        {
            // Only the currently active zone (or the first registered zone if none active) drives RenderSettings interpolation
            if (currentActiveZone == this)
            {
                // Smoothly blend RenderSettings ambient and fog towards this zone's target
                RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, targetAmbientColor, Time.deltaTime * transitionSpeed);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, targetFogColor, Time.deltaTime * transitionSpeed);
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, targetFogDensity, Time.deltaTime * transitionSpeed);

                // Fade in zone audio
                if (zoneAudioSource != null)
                {
                    zoneAudioSource.volume = Mathf.MoveTowards(zoneAudioSource.volume, maxAudioVolume, Time.deltaTime * 0.5f);
                }
            }
            else if (currentActiveZone == null && activeZones.Count > 0 && activeZones[0] == this)
            {
                // No zone active: smoothly restore default Backrooms illumination
                RenderSettings.ambientLight = Color.Lerp(RenderSettings.ambientLight, defaultAmbientColor, Time.deltaTime * transitionSpeed);
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, defaultFogColor, Time.deltaTime * transitionSpeed);
                RenderSettings.fogDensity = Mathf.Lerp(RenderSettings.fogDensity, defaultFogDensity, Time.deltaTime * transitionSpeed);

                if (zoneAudioSource != null && zoneAudioSource.isPlaying)
                {
                    zoneAudioSource.volume = Mathf.MoveTowards(zoneAudioSource.volume, 0f, Time.deltaTime * 0.8f);
                    if (zoneAudioSource.volume <= 0.01f) zoneAudioSource.Stop();
                }
            }
            else
            {
                // Not active zone: fade out audio
                if (zoneAudioSource != null && zoneAudioSource.isPlaying)
                {
                    zoneAudioSource.volume = Mathf.MoveTowards(zoneAudioSource.volume, 0f, Time.deltaTime * 0.8f);
                    if (zoneAudioSource.volume <= 0.01f) zoneAudioSource.Stop();
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            switch (zoneType)
            {
                case DarknessZoneType.Normal:
                    Gizmos.color = new Color(1f, 1f, 0.4f, 0.25f);
                    break;
                case DarknessZoneType.PartialBlackout:
                    Gizmos.color = new Color(0.4f, 0.4f, 0.8f, 0.35f);
                    break;
                case DarknessZoneType.CompleteBlackout:
                    Gizmos.color = new Color(0.1f, 0.05f, 0.15f, 0.65f);
                    break;
            }
            Gizmos.DrawCube(Vector3.zero, boundsSize);
            Gizmos.DrawWireCube(Vector3.zero, boundsSize);
        }
    }
}

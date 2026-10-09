using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Interaction;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Managers
{
    public enum GamePhase
    {
        Phase1_Explore,     // 0:00 - 3:00: Initial exploration & scavenging
        Phase2_Tension,     // 3:00 - 5:00: Atmospheric dread & entity emergence
        Phase3_AccessItem,  // 4:00 - 7:00: Retrieve maintenance keycard
        Phase4_Maintenance, // 6:00 - 8:00: Activate breaker in maintenance room
        Phase5_Escape       // 8:00 - 10:00: Rush to powered emergency exit
    }

    /// <summary>
    /// Master Director orchestrating the 10-minute Backrooms survival horror experience.
    /// Controls pacing, environmental audio tension, progressive enemy stalking,
    /// objective sequencing, and escape completion.
    /// </summary>
    public class GameplayDirector : MonoBehaviour
    {
        public static GameplayDirector Instance { get; private set; }

        [Header("Game Timeline")]
        [SerializeField] private float elapsedTime = 0f;
        [SerializeField] private GamePhase currentPhase = GamePhase.Phase1_Explore;
        [SerializeField] private bool hasEscaped = false;

        [Header("State Flags")]
        [SerializeField] private bool hasAccessItem = false;
        [SerializeField] private bool isPowerRestored = false;

        [Header("Enemy & Progression References")]
        [SerializeField] private StalkerAI stalker;
        [SerializeField] private PowerSwitch maintenanceSwitch;
        [SerializeField] private EscapeExit emergencyExit;

        // Tension event timers
        private float tensionEventTimer = 25.0f;
        private int tensionEventIndex = 0;
        private bool hasTriggeredPhase2Announcement = false;

        public float ElapsedTime => elapsedTime;
        public GamePhase CurrentPhase => currentPhase;
        public bool HasAccessItem => hasAccessItem;
        public bool IsPowerRestored => isPowerRestored;
        public bool HasEscaped => hasEscaped;

        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(elapsedTime % 60f);
            return $"{minutes:00}:{seconds:00}";
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (stalker == null)
            {
                stalker = FindFirstObjectByType<StalkerAI>();
            }

            if (maintenanceSwitch == null)
            {
                maintenanceSwitch = FindFirstObjectByType<PowerSwitch>();
            }

            if (emergencyExit == null)
            {
                emergencyExit = FindFirstObjectByType<EscapeExit>();
            }

            // Phase 1: Initial Objective
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetObjective("OBJECTIVE: Find a way out.");
                HUDManager.Instance.UpdateTimer($"TIME: {GetFormattedTime()}");
            }

            // Keep stalker dormant/distant during early exploration
            if (stalker != null)
            {
                stalker.gameObject.SetActive(true);
                stalker.SetDormant(true);
            }
        }

        private void Update()
        {
            if (hasEscaped || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;

            elapsedTime += Time.deltaTime;

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateTimer($"TIME: {GetFormattedTime()}");
            }

            UpdateTimelineProgression();
        }

        private void UpdateTimelineProgression()
        {
            // Transition to Phase 2 at 3:00 (180s)
            if (elapsedTime >= 180f && currentPhase == GamePhase.Phase1_Explore)
            {
                currentPhase = GamePhase.Phase2_Tension;
                OnEnterPhase2Tension();
            }

            // Atmospheric tension events during Phase 2 and onwards
            if (currentPhase >= GamePhase.Phase2_Tension)
            {
                tensionEventTimer -= Time.deltaTime;
                if (tensionEventTimer <= 0f)
                {
                    tensionEventTimer = UnityEngine.Random.Range(20f, 35f);
                    TriggerEnvironmentalTensionCue();
                }
            }
        }

        private void OnEnterPhase2Tension()
        {
            if (hasTriggeredPhase2Announcement) return;
            hasTriggeredPhase2Announcement = true;

            // Transition ambient track to eerie tension music
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayTensionAmbience();
            }

            // Activate stalker for cautious distant patrolling
            if (stalker != null)
            {
                stalker.SetDormant(false);
            }

            // Trigger immediate eerie vocal / audio cue from the Entity SFX pack
            if (AudioManager.Instance != null)
            {
                AudioClip cue = AudioManager.Instance.HasEntityClips
                    ? AudioManager.Instance.GetRandomEntityClip()
                    : AudioManager.Instance.distantGroanClip;
                if (cue != null) AudioManager.Instance.Play2D(cue, 0.7f);
            }

            // Subtle momentary light flicker in facility
            StartCoroutine(FlickerRandomFacilityLights(2, 0.8f));
        }

        private void TriggerEnvironmentalTensionCue()
        {
            tensionEventIndex++;
            switch (tensionEventIndex % 3)
            {
                case 0:
                    // Distant heavy footsteps echoing
                    if (AudioManager.Instance != null && AudioManager.Instance.distantFootstepsClip != null)
                    {
                        AudioManager.Instance.Play2D(AudioManager.Instance.distantFootstepsClip, 0.65f);
                    }
                    break;

                case 1:
                    // Resonant hollow entity moan / distant roar
                    if (AudioManager.Instance != null)
                    {
                        AudioClip groan = AudioManager.Instance.HasEntityClips
                            ? AudioManager.Instance.GetRandomEntityClip()
                            : AudioManager.Instance.distantGroanClip;
                        if (groan != null) AudioManager.Instance.Play2D(groan, 0.65f);
                    }
                    break;

                case 2:
                    // Corridor light brownout / flicker
                    StartCoroutine(FlickerRandomFacilityLights(3, 1.2f));
                    break;
            }
        }

        public void OnAccessItemFound(string itemName)
        {
            if (hasAccessItem) return;
            hasAccessItem = true;

            if (currentPhase < GamePhase.Phase4_Maintenance)
            {
                currentPhase = GamePhase.Phase4_Maintenance;
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.9f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"ITEM FOUND: {itemName}");
                HUDManager.Instance.ShowObjectiveBanner("NEW OBJECTIVE", "Find the maintenance room.");
                HUDManager.Instance.SetObjective("OBJECTIVE: Find the maintenance room.");
            }
        }

        public void OnPowerRestored()
        {
            if (isPowerRestored) return;
            isPowerRestored = true;
            currentPhase = GamePhase.Phase5_Escape;

            // Transition ambient track to final tense escape music
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlayEscapeAmbience();
            }

            // Power surge audio
            if (AudioManager.Instance != null && AudioManager.Instance.powerRestoreClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.powerRestoreClip, 1.0f);
            }

            // Brownout flicker across entire facility
            StartCoroutine(FlickerAllFacilityLights(1.6f));

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("POWER RESTORED");
                HUDManager.Instance.ShowObjectiveBanner("NEW OBJECTIVE", "Find the emergency exit.");
                HUDManager.Instance.SetObjective("OBJECTIVE: Find the emergency exit.");
            }

            // Power up emergency exit
            if (emergencyExit != null)
            {
                emergencyExit.PowerUpExit();
            }

            // Entity becomes more aggressive in final escape phase
            if (stalker != null)
            {
                stalker.SetAggressiveMode(true);
            }
        }

        public void OnPlayerEscaped()
        {
            if (hasEscaped) return;
            hasEscaped = true;

            if (AudioManager.Instance != null && AudioManager.Instance.doorUnlockClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.doorUnlockClip, 1.0f);
            }

            string finalTime = GetFormattedTime();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowVictory(finalTime);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerVictory();
            }
        }

        private IEnumerator FlickerRandomFacilityLights(int count, float duration)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            List<Light> pointLights = new List<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Point && l.name.Contains("Light"))
                {
                    pointLights.Add(l);
                }
            }

            if (pointLights.Count == 0) yield break;

            List<Light> chosen = new List<Light>();
            for (int i = 0; i < count; i++)
            {
                chosen.Add(pointLights[UnityEngine.Random.Range(0, pointLights.Count)]);
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                bool state = (UnityEngine.Random.value > 0.45f);
                foreach (var l in chosen)
                {
                    if (l != null) l.enabled = state;
                }
                yield return new WaitForSeconds(0.08f);
            }

            foreach (var l in chosen)
            {
                if (l != null) l.enabled = true;
            }
        }

        private IEnumerator FlickerAllFacilityLights(float duration)
        {
            Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
            List<Light> ceilingLights = new List<Light>();
            foreach (var l in lights)
            {
                if (l.type == LightType.Point && l.name.Contains("Light"))
                {
                    ceilingLights.Add(l);
                }
            }

            float timer = 0f;
            while (timer < duration)
            {
                timer += Time.deltaTime;
                bool state = (UnityEngine.Random.value > 0.5f);
                foreach (var l in ceilingLights)
                {
                    if (l != null) l.enabled = state;
                }
                yield return new WaitForSeconds(0.09f);
            }

            foreach (var l in ceilingLights)
            {
                if (l != null) l.enabled = true;
            }
        }
    }
}

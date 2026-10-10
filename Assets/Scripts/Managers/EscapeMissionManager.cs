using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.Inventory;
using HorrorEscape.UI;

namespace HorrorEscape.Managers
{
    /// <summary>
    /// Master Manager for the 4-part Backrooms Escape Mission:
    /// 1. [ ] Find a gun
    /// 2. [ ] Find flashlight batteries
    /// 3. [ ] Find the escape key
    /// 4. [ ] Unlock the exit door and escape
    /// </summary>
    public class EscapeMissionManager : MonoBehaviour
    {
        public static EscapeMissionManager Instance { get; private set; }

        [Header("Mission Checklist State")]
        [SerializeField] private bool hasGun = false;
        [SerializeField] private bool hasBatteries = false;
        [SerializeField] private bool hasEscapeKey = false;
        [SerializeField] private bool hasEscaped = false;

        public bool HasGun => hasGun;
        public bool HasBatteries => hasBatteries;
        public bool HasEscapeKey => hasEscapeKey;
        public bool HasEscaped => hasEscaped;

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
            UpdateHUD();
        }

        private void Update()
        {
            // Continuous polling fallback to ensure state remains in sync
            if (!hasGun)
            {
                var combat = FindFirstObjectByType<PlayerCombat>();
                if (combat != null && (combat.HasUnlockedPistol || combat.HasUnlockedSMG))
                {
                    OnGunFound("Weapon Pickup");
                }
                else if (InventoryManager.Instance != null &&
                        (InventoryManager.Instance.HasItem(ItemType.Pistol) || InventoryManager.Instance.HasItem(ItemType.SMG)))
                {
                    OnGunFound("Weapon Pickup");
                }
            }

            if (!hasBatteries)
            {
                if (InventoryManager.Instance != null && InventoryManager.Instance.HasItem(ItemType.Battery))
                {
                    OnBatteryCollected();
                }
            }

            if (!hasEscapeKey)
            {
                if (GameManager.Instance != null &&
                   (GameManager.Instance.HasKey("EscapeKey") || GameManager.Instance.HasKey("MaintenanceKeycard")))
                {
                    OnKeyFound("Escape Key");
                }
            }
        }

        public void OnGunFound(string weaponName = "Gun")
        {
            if (hasGun) return;
            hasGun = true;

            PlayObjectiveAudio();
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"OBJECTIVE COMPLETE: Found a gun ({weaponName})");
            }
            UpdateHUD();
        }

        public void OnBatteryCollected()
        {
            if (hasBatteries) return;
            hasBatteries = true;

            PlayObjectiveAudio();
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("OBJECTIVE COMPLETE: Found flashlight batteries");
            }
            UpdateHUD();
        }

        public void OnKeyFound(string keyName = "Escape Key")
        {
            if (hasEscapeKey) return;
            hasEscapeKey = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKey("EscapeKey");
            }

            PlayObjectiveAudio();
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"OBJECTIVE COMPLETE: Found the {keyName}");
            }
            UpdateHUD();
        }

        public void OnPlayerEscaped()
        {
            if (hasEscaped) return;
            hasEscaped = true;

            PlayObjectiveAudio();
            UpdateHUD();

            if (GameplayDirector.Instance != null)
            {
                GameplayDirector.Instance.OnPlayerEscaped();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerVictory();
            }
        }

        public void UpdateHUD()
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.UpdateObjectiveChecklist(hasGun, hasBatteries, hasEscapeKey, hasEscaped);
            }
        }

        private void PlayObjectiveAudio()
        {
            if (AudioManager.Instance != null)
            {
                if (AudioManager.Instance.terminalBeepClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.terminalBeepClip, 0.45f);
                }
                else if (AudioManager.Instance.doorLatchClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.doorLatchClip, 0.6f);
                }
            }
        }
    }
}

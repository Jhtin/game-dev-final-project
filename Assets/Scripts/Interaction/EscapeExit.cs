using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Final escape exit door at the end of the Backrooms maze.
    /// Unlocks when emergency power is restored in the maintenance room.
    /// </summary>
    public class EscapeExit : MonoBehaviour, IInteractable
    {
        [Header("Exit Status")]
        [SerializeField] private bool isPowered = false;
        [SerializeField] private bool isOpen = false;

        [Header("Visual Components")]
        [SerializeField] private Light exitIndicatorLight;
        [SerializeField] private Transform doorHinge;
        [SerializeField] private float openAngle = 90.0f;

        private bool hasNotifiedExitFound = false;

        public bool IsPowered => isPowered;
        public bool IsOpen => isOpen;

        private void Start()
        {
            UpdateVisuals();
        }

        public void PowerUpExit()
        {
            isPowered = true;
            UpdateVisuals();

            if (HUDManager.Instance != null && !hasNotifiedExitFound)
            {
                HUDManager.Instance.ShowNotification("Emergency Exit Powered & Unlocked!");
            }
        }

        private void UpdateVisuals()
        {
            if (exitIndicatorLight != null)
            {
                // Red when offline, Bright Green/Amber when powered online
                exitIndicatorLight.color = isPowered ? new Color(0.2f, 1.0f, 0.3f) : new Color(1.0f, 0.15f, 0.15f);
                exitIndicatorLight.intensity = isPowered ? 2.5f : 1.2f;
            }
        }

        private bool CanUnlockExit()
        {
            if (EscapeMissionManager.Instance != null && EscapeMissionManager.Instance.HasEscapeKey) return true;
            if (GameManager.Instance != null && (GameManager.Instance.HasKey("EscapeKey") || GameManager.Instance.HasKey("MaintenanceKeycard"))) return true;
            if (isPowered || (GameplayDirector.Instance != null && GameplayDirector.Instance.IsPowerRestored)) return true;
            return false;
        }

        public string GetInteractionPrompt()
        {
            if (isOpen) return "[Open] Escape Threshold";

            if (!CanUnlockExit())
            {
                return "[Locked] The door is locked. Find the key.";
            }

            return "[E] Unlock Exit Door & Escape!";
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponent<PlayerInteraction>() != null)
            {
                if (CanUnlockExit() && !hasNotifiedExitFound)
                {
                    hasNotifiedExitFound = true;
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.ShowNotification("EXIT FOUND - UNLOCK TO ESCAPE");
                    }
                }
            }
        }

        public void Interact(PlayerInteraction player)
        {
            if (isOpen) return;

            if (CanUnlockExit())
            {
                StartCoroutine(OpenExitRoutine());
            }
            else
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("The door is locked. Find the key.");
                }
                if (AudioManager.Instance != null)
                {
                    AudioClip lockedSound = AudioManager.Instance.doorLockedClip ?? AudioManager.Instance.doorCloseClip;
                    if (lockedSound != null) AudioManager.Instance.PlayAtPosition(lockedSound, transform.position, 0.85f);
                }
            }
        }

        private IEnumerator OpenExitRoutine()
        {
            isOpen = true;

            // Play door unlock and swing open audio
            if (AudioManager.Instance != null)
            {
                if (AudioManager.Instance.doorUnlockClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.doorUnlockClip, transform.position, 0.9f);
                }
                AudioClip swingClip = AudioManager.Instance.gateOpenClip ?? AudioManager.Instance.gateClangClip ?? AudioManager.Instance.doorOpenClip;
                if (swingClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(swingClip, transform.position, 1.0f);
                }
            }

            // Animate door open
            if (doorHinge != null)
            {
                Quaternion startRot = doorHinge.localRotation;
                Quaternion targetRot = startRot * Quaternion.Euler(0f, openAngle, 0f);
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime * 1.5f;
                    doorHinge.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(0.4f);
            }

            yield return new WaitForSeconds(0.2f);

            // Trigger escape victory sequence
            if (EscapeMissionManager.Instance != null)
            {
                EscapeMissionManager.Instance.OnPlayerEscaped();
            }
            else if (GameplayDirector.Instance != null)
            {
                GameplayDirector.Instance.OnPlayerEscaped();
            }
            else if (GameManager.Instance != null)
            {
                GameManager.Instance.TriggerVictory();
            }
        }

        public void SetIndicatorLight(Light light)
        {
            exitIndicatorLight = light;
            UpdateVisuals();
        }

        public void SetDoorHinge(Transform hinge)
        {
            doorHinge = hinge;
        }
    }
}

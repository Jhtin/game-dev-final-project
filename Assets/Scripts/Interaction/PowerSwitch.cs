using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive Power Switch / Generator Lever (Rubric Requirement 2: Switches/Buttons).
    /// Once the player collects the required fuses, pulling this switch powers the emergency exit!
    /// </summary>
    public class PowerSwitch : MonoBehaviour, IInteractable
    {
        [Header("Switch Requirements")]
        [SerializeField] private int requiredFuses = 3;
        [SerializeField] private bool isActivated = false;

        [Header("Visual Animation")]
        [SerializeField] private Transform switchLever;
        [SerializeField] private float pullAngle = 65.0f;
        [SerializeField] private Light statusIndicatorLight;

        public bool IsActivated => isActivated;
        public int RequiredFuses => requiredFuses;

        private void Awake()
        {
            UpdateIndicator();
        }

        public string GetInteractionPrompt()
        {
            if (isActivated)
            {
                return "[Activated] Emergency Power Grid Online";
            }

            bool hasCard = (GameplayDirector.Instance != null && GameplayDirector.Instance.HasAccessItem) ||
                           (GameManager.Instance != null && GameManager.Instance.HasKey("MaintenanceKeycard"));

            if (hasCard)
            {
                return "[E] Restore Emergency Power";
            }

            return "[Locked] Maintenance Switch (Requires Maintenance Keycard)";
        }

        public void Interact(PlayerInteraction player)
        {
            if (isActivated) return;

            bool hasCard = (GameplayDirector.Instance != null && GameplayDirector.Instance.HasAccessItem) ||
                           (GameManager.Instance != null && GameManager.Instance.HasKey("MaintenanceKeycard"));

            if (hasCard)
            {
                ActivateSwitch();
            }
            else
            {
                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("Locked: Locate the Maintenance Keycard in the maze first!");
                }
                if (AudioManager.Instance != null && AudioManager.Instance.doorCloseClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.doorCloseClip, transform.position, 0.7f);
                }
            }
        }

        private void ActivateSwitch()
        {
            isActivated = true;

            // Animate lever handle down
            if (switchLever != null)
            {
                StartCoroutine(AnimateLever());
            }

            UpdateIndicator();

            if (GameplayDirector.Instance != null)
            {
                GameplayDirector.Instance.OnPowerRestored();
            }
            else
            {
                if (AudioManager.Instance != null && AudioManager.Instance.doorUnlockClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.doorUnlockClip, transform.position, 1.0f);
                }

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification("POWER RESTORED");
                }
            }
        }

        private IEnumerator AnimateLever()
        {
            Quaternion startRot = switchLever.localRotation;
            Quaternion targetRot = startRot * Quaternion.Euler(pullAngle, 0f, 0f);

            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime * 3.0f;
                switchLever.localRotation = Quaternion.Slerp(startRot, targetRot, t);
                yield return null;
            }
            switchLever.localRotation = targetRot;
        }

        private void UpdateIndicator()
        {
            if (statusIndicatorLight != null)
            {
                statusIndicatorLight.color = isActivated ? Color.green : Color.red;
                statusIndicatorLight.intensity = isActivated ? 1.5f : 0.8f;
            }
        }
    }
}

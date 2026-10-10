using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive Door that can be opened, closed, and unlocked with specific keys.
    /// Rotates smoothly on a hinge and updates NavMesh obstacles for AI navigation.
    /// </summary>
    public class Door : MonoBehaviour, IInteractable
    {
        [Header("Door State")]
        [SerializeField] private bool isOpen = false;
        [SerializeField] private bool isLocked = false;
        [SerializeField] private string requiredKeyId = "";
        [SerializeField] private string keyDisplayName = "Rusty Key";

        [Header("Rotation Settings")]
        [SerializeField] private Transform doorHinge;
        [SerializeField] private float openAngle = 90.0f;
        [SerializeField] private float smoothSpeed = 3.5f;

        private Quaternion closedRotation;
        private Quaternion openRotation;
        private Coroutine rotateCoroutine;
        private UnityEngine.AI.NavMeshObstacle navObstacle;

        private void Awake()
        {
            if (doorHinge == null)
            {
                doorHinge = transform;
            }

            closedRotation = doorHinge.localRotation;
            openRotation = Quaternion.Euler(doorHinge.localEulerAngles + new Vector3(0f, openAngle, 0f));

            navObstacle = GetComponent<UnityEngine.AI.NavMeshObstacle>();
            if (navObstacle != null)
            {
                navObstacle.carving = true;
                navObstacle.enabled = !isOpen;
            }
        }

        public string GetInteractionPrompt()
        {
            if (isLocked)
            {
                return $"[Locked] Requires {keyDisplayName}";
            }
            return isOpen ? "[E] Close Door" : "[E] Open Door";
        }

        public void Interact(PlayerInteraction player)
        {
            if (isLocked)
            {
                TryUnlock();
                return;
            }

            ToggleDoor();
        }

        private void TryUnlock()
        {
            if (GameManager.Instance != null && GameManager.Instance.HasKey(requiredKeyId))
            {
                isLocked = false;
                if (AudioManager.Instance != null && AudioManager.Instance.doorUnlockClip != null)
                {
                    AudioManager.Instance.PlayAtPosition(AudioManager.Instance.doorUnlockClip, transform.position);
                }

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification($"Unlocked door with {keyDisplayName}!");
                }

                ToggleDoor();
            }
            else
            {
                // Play locked rattle
                if (AudioManager.Instance != null)
                {
                    AudioClip rattle = AudioManager.Instance.doorLockedClip ?? AudioManager.Instance.doorCloseClip;
                    if (rattle != null)
                    {
                        AudioManager.Instance.PlayAtPosition(rattle, transform.position, 0.7f, 15f);
                    }
                }

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification($"Door is locked. Need {keyDisplayName}.");
                }
            }
        }

        public void ToggleDoor()
        {
            isOpen = !isOpen;

            if (navObstacle != null)
            {
                navObstacle.enabled = !isOpen;
            }

            // Audio
            if (AudioManager.Instance != null)
            {
                AudioClip clip = isOpen ? AudioManager.Instance.doorOpenClip : AudioManager.Instance.doorCloseClip;
                AudioManager.Instance.PlayAtPosition(clip, transform.position, 0.7f);
            }

            if (rotateCoroutine != null)
            {
                StopCoroutine(rotateCoroutine);
            }
            rotateCoroutine = StartCoroutine(AnimateDoor(isOpen ? openRotation : closedRotation));
        }

        private IEnumerator AnimateDoor(Quaternion targetRot)
        {
            while (Quaternion.Angle(doorHinge.localRotation, targetRot) > 0.5f)
            {
                doorHinge.localRotation = Quaternion.Slerp(doorHinge.localRotation, targetRot, Time.deltaTime * smoothSpeed);
                yield return null;
            }
            doorHinge.localRotation = targetRot;
        }
    }
}

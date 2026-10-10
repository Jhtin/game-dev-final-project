using System.Collections;
using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Enemy;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive Hiding Spot (Cabinet / Wardrobe / Locker).
    /// - Allows the player to open the door, step inside, close the door, and hide from the Stalker entity.
    /// - When hidden, the enemy cannot spot the player via vision, flashlight, proximity, or hearing.
    /// - If the enemy was chasing the player, it rushes to the spot outside the cabinet, searches in confusion,
    ///   gives up, and walks away on its patrol route.
    /// - Inside the cabinet, the player can peek through the front door slats/gap with constrained mouse look.
    /// - Features smooth door opening/closing animations, latch audio, and tension audio when the monster is near.
    /// </summary>
    public class HidingSpot : MonoBehaviour, IInteractable
    {
        [Header("Door & Hinge Settings")]
        [SerializeField] private Transform doorHinge;
        [SerializeField] private float openAngle = -95.0f;
        [SerializeField] private float smoothSpeed = 7.0f;

        [Header("Player Anchors")]
        [SerializeField] private Transform interiorAnchor;
        [SerializeField] private Transform exitAnchor;

        [Header("Audio")]
        [SerializeField] private AudioClip doorOpenClip;
        [SerializeField] private AudioClip doorCloseClip;
        [SerializeField] private float audioVolume = 0.8f;
        [SerializeField] private float audioRange = 12.0f;

        [Header("State")]
        [SerializeField] private bool isOccupied = false;
        [SerializeField] private bool isTransitioning = false;

        private Quaternion closedRotation = Quaternion.identity;
        private Quaternion openRotation;
        private Quaternion targetDoorRotation = Quaternion.identity;
        private FirstPersonController hiddenPlayer;
        private FlashlightController playerFlashlight;
        private bool flashlightWasOn = false;
        private AudioSource audioSource;
        private float tensionTimer = 0f;

        public bool IsOccupied => isOccupied;

        public void Configure(Transform hinge, Transform interior, Transform exit, float angle = -95f)
        {
            doorHinge = hinge;
            interiorAnchor = interior;
            exitAnchor = exit;
            openAngle = angle;
            closedRotation = Quaternion.identity;
            openRotation = Quaternion.Euler(0f, openAngle, 0f);
            targetDoorRotation = closedRotation;
            if (doorHinge != null)
            {
                doorHinge.localRotation = closedRotation;
            }
        }

        private void Awake()
        {
            if (doorHinge == null)
            {
                Transform foundHinge = transform.Find("DoorHinge");
                if (foundHinge != null) doorHinge = foundHinge;
                else doorHinge = transform;
            }

            closedRotation = Quaternion.identity;
            openRotation = Quaternion.Euler(0f, openAngle, 0f);
            targetDoorRotation = closedRotation;
            if (doorHinge != null)
            {
                doorHinge.localRotation = closedRotation;
            }

            if (interiorAnchor == null)
            {
                Transform foundInterior = transform.Find("InteriorAnchor");
                if (foundInterior != null) interiorAnchor = foundInterior;
            }

            if (exitAnchor == null)
            {
                Transform foundExit = transform.Find("ExitAnchor");
                if (foundExit != null) exitAnchor = foundExit;
            }

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.playOnAwake = false;
                audioSource.minDistance = 1.0f;
                audioSource.maxDistance = audioRange;
            }
        }

        private void Update()
        {
            // Smoothly swing door towards target rotation
            if (doorHinge != null && Quaternion.Angle(doorHinge.localRotation, targetDoorRotation) > 0.05f)
            {
                doorHinge.localRotation = Quaternion.Slerp(doorHinge.localRotation, targetDoorRotation, Time.deltaTime * smoothSpeed);
            }

            // If player is inside the cabinet:
            if (isOccupied && !isTransitioning)
            {
                // Press [E] to exit cabinet
                if (Input.GetKeyDown(KeyCode.E))
                {
                    ExitHiding();
                    return;
                }

                // Keep player position anchored firmly inside
                if (hiddenPlayer != null && interiorAnchor != null)
                {
                    hiddenPlayer.transform.position = interiorAnchor.position;
                }

                // Check distance to Stalker for tension audio / heartbeat
                tensionTimer += Time.deltaTime;
                if (tensionTimer >= 1.0f)
                {
                    tensionTimer = 0f;
                    CheckMonsterProximity();
                }
            }
        }

        private void CheckMonsterProximity()
        {
            StalkerAI stalker = FindFirstObjectByType<StalkerAI>();
            if (stalker != null && hiddenPlayer != null)
            {
                float dist = Vector3.Distance(transform.position, stalker.transform.position);
                if (dist <= 8.0f && AudioManager.Instance != null && AudioManager.Instance.heartbeatClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.heartbeatClip, 0.4f);
                }
            }
        }

        public string GetInteractionPrompt()
        {
            if (isTransitioning) return string.Empty;
            return isOccupied ? "[E] Exit Cabinet" : "[E] Hide in Cabinet";
        }

        public void Interact(PlayerInteraction player)
        {
            if (isTransitioning) return;

            if (!isOccupied)
            {
                EnterHiding(player);
            }
            else
            {
                ExitHiding();
            }
        }

        public void EnterHiding(PlayerInteraction player)
        {
            if (isOccupied || isTransitioning || player == null) return;
            StartCoroutine(EnterHidingRoutine(player));
        }

        public void ExitHiding()
        {
            if (!isOccupied || isTransitioning) return;
            StartCoroutine(ExitHidingRoutine());
        }

        private IEnumerator EnterHidingRoutine(PlayerInteraction player)
        {
            isTransitioning = true;
            hiddenPlayer = player.GetComponentInParent<FirstPersonController>();
            if (hiddenPlayer == null) hiddenPlayer = FindFirstObjectByType<FirstPersonController>();

            // Turn off flashlight so it doesn't give away the player inside
            playerFlashlight = hiddenPlayer != null ? hiddenPlayer.GetComponentInChildren<FlashlightController>() : null;
            if (playerFlashlight != null)
            {
                flashlightWasOn = playerFlashlight.IsOn;
                if (flashlightWasOn)
                {
                    playerFlashlight.ToggleFlashlight();
                }
            }

            // 1. Swing door open
            targetDoorRotation = openRotation;
            PlayDoorSound(doorOpenClip != null ? doorOpenClip : (AudioManager.Instance != null ? AudioManager.Instance.doorOpenClip : null));

            // 2. Smoothly transition player into cabinet
            Vector3 startPos = hiddenPlayer.transform.position;
            Vector3 targetPos = interiorAnchor != null ? interiorAnchor.position : transform.position;
            float elapsed = 0f;
            float duration = 0.45f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (hiddenPlayer != null)
                {
                    hiddenPlayer.transform.position = Vector3.Lerp(startPos, targetPos, t);
                }
                yield return null;
            }

            // 3. Lock movement and configure peephole look
            float facingYaw = interiorAnchor != null ? interiorAnchor.eulerAngles.y : transform.eulerAngles.y;
            if (hiddenPlayer != null)
            {
                hiddenPlayer.transform.position = targetPos;
                hiddenPlayer.SetHiding(true, facingYaw);
            }

            // 4. Swing door shut on its own so the player is completely hidden inside
            targetDoorRotation = closedRotation;
            PlayDoorSound(doorCloseClip != null ? doorCloseClip : (AudioManager.Instance != null ? AudioManager.Instance.doorCloseClip : null));

            // Wait briefly for the door to visibly swing shut and latch
            float shutTimer = 0f;
            while (shutTimer < 0.4f)
            {
                shutTimer += Time.deltaTime;
                yield return null;
            }
            if (doorHinge != null) doorHinge.localRotation = closedRotation;

            isOccupied = true;
            isTransitioning = false;

            NotifyEnemiesPlayerHid();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt("[E] Exit Cabinet", true);
                HUDManager.Instance.ShowNotification("Hidden inside cabinet. Entity lost you and fled!");
            }
        }

        private IEnumerator ExitHidingRoutine()
        {
            isTransitioning = true;

            // 1. Swing door open
            targetDoorRotation = openRotation;
            PlayDoorSound(doorOpenClip != null ? doorOpenClip : (AudioManager.Instance != null ? AudioManager.Instance.doorOpenClip : null));

            yield return new WaitForSeconds(0.2f);

            // 2. Smoothly step player outside cabinet
            Vector3 startPos = interiorAnchor != null ? interiorAnchor.position : transform.position;
            Vector3 targetPos = exitAnchor != null ? exitAnchor.position : transform.position + transform.forward * 1.2f;
            float elapsed = 0f;
            float duration = 0.4f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                if (hiddenPlayer != null)
                {
                    hiddenPlayer.transform.position = Vector3.Lerp(startPos, targetPos, t);
                }
                yield return null;
            }

            // 3. Restore player movement
            if (hiddenPlayer != null)
            {
                hiddenPlayer.transform.position = targetPos;
                hiddenPlayer.SetHiding(false);
            }

            // Restore flashlight state if it was on before
            if (playerFlashlight != null && flashlightWasOn && !playerFlashlight.IsOn)
            {
                playerFlashlight.ToggleFlashlight();
            }

            // 4. Swing door shut behind player
            targetDoorRotation = closedRotation;
            PlayDoorSound(doorCloseClip != null ? doorCloseClip : (AudioManager.Instance != null ? AudioManager.Instance.doorCloseClip : null));

            float exitShutTimer = 0f;
            while (exitShutTimer < 0.35f)
            {
                exitShutTimer += Time.deltaTime;
                yield return null;
            }
            if (doorHinge != null) doorHinge.localRotation = closedRotation;

            isOccupied = false;
            isTransitioning = false;
            hiddenPlayer = null;

            NotifyEnemiesPlayerExited();

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt(string.Empty, false);
            }
        }

        private void NotifyEnemiesPlayerHid()
        {
            StalkerAI[] stalkers = FindObjectsByType<StalkerAI>(FindObjectsSortMode.None);
            foreach (var stalker in stalkers)
            {
                if (stalker != null)
                {
                    stalker.OnPlayerEnteredHiding(transform.position);
                }
            }
        }

        private void NotifyEnemiesPlayerExited()
        {
            StalkerAI[] stalkers = FindObjectsByType<StalkerAI>(FindObjectsSortMode.None);
            foreach (var stalker in stalkers)
            {
                if (stalker != null)
                {
                    stalker.OnPlayerExitedHiding();
                }
            }
        }

        private void PlayDoorSound(AudioClip clip)
        {
            if (clip != null && audioSource != null)
            {
                audioSource.pitch = Random.Range(0.95f, 1.05f);
                audioSource.PlayOneShot(clip, audioVolume);
            }
            else if (AudioManager.Instance != null)
            {
                if (clip != null) AudioManager.Instance.PlayAtPosition(clip, transform.position, audioVolume, audioRange);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (interiorAnchor != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(interiorAnchor.position, 0.35f);
                Gizmos.DrawRay(interiorAnchor.position, interiorAnchor.forward * 0.8f);
            }

            if (exitAnchor != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(exitAnchor.position, 0.35f);
                Gizmos.DrawRay(exitAnchor.position, exitAnchor.forward * 0.8f);
            }
        }
    }
}

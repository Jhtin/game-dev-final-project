using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive pickup for the Tactical Submachine Gun (POLY - Lite Survival Collection).
    /// Placed on a workbench in a maintenance room, enabling full-auto defense for the player.
    /// </summary>
    public class SMGPickup : MonoBehaviour, IInteractable
    {
        [Header("SMG Pickup Settings")]
        [SerializeField] private int initialClipAmmo = 20;
        [SerializeField] private int initialReserveAmmo = 20;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = true;
        [SerializeField] private float rotateSpeed = 25.0f;
        [SerializeField] private float bobAmplitude = 0.04f;

        private Vector3 startPos;
        private bool isCollected = false;

        private void Start()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            if (bobAndRotate && !isCollected)
            {
                transform.Rotate(Vector3.up * (rotateSpeed * Time.deltaTime), Space.World);
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.0f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Take Tactical SMG";
        }

        public void Interact(PlayerInteraction player)
        {
            Collect(player.gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponent<PlayerInteraction>() != null)
            {
                Collect(other.gameObject);
            }
        }

        private void Collect(GameObject player)
        {
            if (isCollected) return;
            isCollected = true;

            PlayerCombat combat = player.GetComponent<PlayerCombat>();
            if (combat == null) combat = player.GetComponentInChildren<PlayerCombat>();

            if (combat != null)
            {
                combat.UnlockSMG(initialClipAmmo, initialReserveAmmo);
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.9f, 1.2f);
            }

            Destroy(gameObject);
        }
    }
}

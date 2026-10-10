using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Inventory;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive pickup for the 9mm Army Pistol (POLY - Lite Survival Collection).
    /// Placed in the Backrooms maze for the player to discover naturally.
    /// </summary>
    public class PistolPickup : MonoBehaviour, IInteractable
    {
        [Header("Pistol Ammo Settings")]
        [SerializeField] private int initialClipAmmo = 6;
        [SerializeField] private int initialReserveAmmo = 6;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 25.0f;
        [SerializeField] private float bobAmplitude = 0.04f;

        private Vector3 startPos;
        private bool isCollected = false;

        private void Start()
        {
            startPos = transform.position;
            if (GetComponent<GroundItemPhysics>() == null)
            {
                gameObject.AddComponent<GroundItemPhysics>();
            }
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
            return "Press E to Pick Up 9mm Pistol";
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

            PlayerCombat combat = player.GetComponent<PlayerCombat>() ?? player.GetComponentInChildren<PlayerCombat>();
            if (combat != null)
            {
                combat.UnlockPistol(initialClipAmmo, initialReserveAmmo);
            }

            // Register with InventoryManager
            if (InventoryManager.Instance != null)
            {
                InventoryManager.Instance.AddItem(
                    ItemType.Pistol,
                    "9mm Army Pistol",
                    "Compact semi-automatic sidearm. Emergency defense against entities.",
                    null,
                    1,
                    true,
                    false
                );
                InventoryManager.Instance.EquipItem(EquipSlot.Pistol);
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.9f, 1.2f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("DISCOVERED: 9mm Army Pistol! Equipped to defense hand.");
            }

            if (HorrorEscape.Managers.EscapeMissionManager.Instance != null)
            {
                HorrorEscape.Managers.EscapeMissionManager.Instance.OnGunFound("9mm Army Pistol");
            }

            Destroy(gameObject);
        }
    }
}

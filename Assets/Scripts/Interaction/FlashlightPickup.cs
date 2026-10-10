using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive pickup for discovery of a heavy industrial searchlight.
    /// Restores full battery power to the player flashlight and registers with inventory.
    /// </summary>
    public class FlashlightPickup : MonoBehaviour, IInteractable
    {
        [SerializeField] private float batteryRestore = 100f;
        private bool isCollected = false;

        private void Start()
        {
            if (GetComponent<GroundItemPhysics>() == null)
            {
                gameObject.AddComponent<GroundItemPhysics>();
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Take Industrial Flashlight";
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

            FlashlightController flash = player.GetComponentInChildren<FlashlightController>();
            if (flash != null)
            {
                flash.Recharge(batteryRestore);
            }

            if (HorrorEscape.Inventory.InventoryManager.Instance != null)
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.Flashlight,
                    "Industrial Flashlight",
                    "Heavy-duty searchlight with fresh dry cells.",
                    null,
                    1,
                    true,
                    false
                );
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.9f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification("DISCOVERED: Industrial Searchlight (Battery Fully Recharged!)");
            }

            Destroy(gameObject);
        }
    }
}

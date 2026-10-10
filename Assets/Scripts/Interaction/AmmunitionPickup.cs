using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Ammunition pickup item providing emergency 9mm defense rounds for the player gun.
    /// </summary>
    public class AmmunitionPickup : MonoBehaviour, IInteractable
    {
        [Header("Ammo Settings")]
        [SerializeField] private int ammoAmount = 6;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 40.0f;
        [SerializeField] private float bobAmplitude = 0.05f;

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
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.2f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Pick up Ammunition (6 Rounds)";
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
                combat.AddAmmo(ammoAmount);
            }

            if (HorrorEscape.Inventory.InventoryManager.Instance != null)
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.PistolAmmo,
                    "9mm Ammunition",
                    "Standard defense cartridges. Loaded into handgun magazine.",
                    null,
                    ammoAmount,
                    false,
                    true
                );
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.8f, 1.3f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Collected 9mm Ammunition (+{ammoAmount} rounds)");
            }

            Destroy(gameObject);
        }
    }
}

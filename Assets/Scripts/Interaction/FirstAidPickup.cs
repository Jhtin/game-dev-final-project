using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// First Aid Kit pickup from Survival Tools.
    /// Restores player health by a substantial amount.
    /// </summary>
    public class FirstAidPickup : MonoBehaviour, IInteractable
    {
        [Header("Heal Settings")]
        [SerializeField] private float healAmount = 50.0f;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 30.0f;
        [SerializeField] private float bobAmplitude = 0.04f;

        private Vector3 startPos;

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
            if (bobAndRotate)
            {
                transform.Rotate(Vector3.up * (rotateSpeed * Time.deltaTime), Space.World);
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.0f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Take First Aid Kit";
        }

        private bool isCollected = false;

        public void Interact(PlayerInteraction player)
        {
            if (isCollected) return;
            isCollected = true;

            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Heal(healAmount);
            }

            if (HorrorEscape.Inventory.InventoryManager.Instance != null)
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.FirstAid,
                    "First Aid Kit",
                    "Emergency sterile trauma dressings and antiseptics. Restores vital physical condition.",
                    null,
                    1,
                    false,
                    true
                );
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Used First Aid Kit (+{healAmount} HP)");
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.85f, 1.1f);
            }

            Destroy(gameObject);
        }
    }
}

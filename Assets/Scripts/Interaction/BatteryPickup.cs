using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Flashlight battery pickup that restores battery charge.
    /// </summary>
    public class BatteryPickup : MonoBehaviour, IInteractable
    {
        [Header("Battery Recharge Amount")]
        [SerializeField] private float rechargeAmount = 40.0f;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = true;
        [SerializeField] private float rotateSpeed = 40.0f;
        [SerializeField] private float bobAmplitude = 0.05f;

        private Vector3 startPos;

        private void Start()
        {
            startPos = transform.position;
        }

        private void Update()
        {
            if (bobAndRotate)
            {
                transform.Rotate(Vector3.up * (rotateSpeed * Time.deltaTime), Space.World);
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.2f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Pick up Flashlight Battery";
        }

        private bool isCollected = false;

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

            FlashlightController flashlight = player.GetComponentInChildren<FlashlightController>();
            if (flashlight != null)
            {
                flashlight.Recharge(rechargeAmount);
            }

            if (HorrorEscape.Inventory.InventoryManager.Instance != null)
            {
                HorrorEscape.Inventory.InventoryManager.Instance.AddItem(
                    HorrorEscape.Inventory.ItemType.Battery,
                    "Flashlight Battery",
                    "High-capacity industrial dry cell. Restores searchlight power.",
                    null,
                    1,
                    false,
                    true
                );
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.7f, 1.2f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNotification($"Battery Recharged (+{rechargeAmount}%)");
            }

            Destroy(gameObject);
        }
    }
}

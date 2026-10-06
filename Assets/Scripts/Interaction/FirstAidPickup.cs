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
        [SerializeField] private bool bobAndRotate = true;
        [SerializeField] private float rotateSpeed = 30.0f;
        [SerializeField] private float bobAmplitude = 0.04f;

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
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.0f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Take First Aid Kit";
        }

        public void Interact(PlayerInteraction player)
        {
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Heal(healAmount);
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

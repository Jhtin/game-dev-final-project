using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Calming Pills pickup from Survival Tools.
    /// Relieves paranoia, stabilizes heartbeat, and cures fear vignette.
    /// </summary>
    public class SanityPillsPickup : MonoBehaviour, IInteractable
    {
        [Header("Pills Settings")]
        [SerializeField] private float healAmount = 20.0f;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 35.0f;
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
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.1f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return "[E] Take Calming Pills";
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
                HUDManager.Instance.SetDangerVignette(0f);
                HUDManager.Instance.ShowNotification("Took Calming Pills (Sanity Restored)");
            }

            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.8f, 1.3f);
            }

            Destroy(gameObject);
        }
    }
}

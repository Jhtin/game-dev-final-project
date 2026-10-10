using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Player;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Iconic Backrooms Almond Water consumable.
    /// Drinking it restores stamina, relieves fear, and restores sanity!
    /// </summary>
    public class AlmondWaterPickup : MonoBehaviour, IInteractable
    {
        [Header("Almond Water Properties")]
        [SerializeField] private float sanityRestoration = 50.0f;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 35.0f;
        [SerializeField] private float bobAmplitude = 0.05f;

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
            return "[E] Drink Almond Water";
        }

        public void Interact(PlayerInteraction player)
        {
            // Heal player HP
            PlayerHealth health = player.GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.Heal(sanityRestoration);
            }

            // Reset danger vignette on HUD
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetDangerVignette(0f);
                HUDManager.Instance.ShowNotification("Drank Almond Water (+HP & Stamina restored!)");
            }

            // Play gulp / drink sound
            if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.9f, 0.85f);
            }

            Destroy(gameObject);
        }
    }
}

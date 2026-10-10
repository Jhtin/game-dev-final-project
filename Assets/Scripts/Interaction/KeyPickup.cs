using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.Managers;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Collectible Key or Quest Item (e.g. Fuse, Master Key) required to unlock doors or escape.
    /// </summary>
    public class KeyPickup : MonoBehaviour, IInteractable
    {
        [Header("Key Properties")]
        [SerializeField] private string keyId = "Fuse1";
        [SerializeField] private string keyDisplayName = "Power Grid Fuse";
        [SerializeField] private bool isObjectiveItem = true;

        [Header("Visual Effects")]
        [SerializeField] private bool bobAndRotate = false;
        [SerializeField] private float rotateSpeed = 45.0f;
        [SerializeField] private float bobAmplitude = 0.08f;

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
                transform.position = startPos + new Vector3(0f, Mathf.Sin(Time.time * 2.5f) * bobAmplitude, 0f);
            }
        }

        public string GetInteractionPrompt()
        {
            return $"[E] Take {keyDisplayName}";
        }

        public void Interact(PlayerInteraction player)
        {
            Collect();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null || other.GetComponent<PlayerInteraction>() != null)
            {
                Collect();
            }
        }

        private void Collect()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddKey(keyId);
                if (isObjectiveItem)
                {
                    GameManager.Instance.RegisterObjectiveProgress();
                }
            }

            if (GameplayDirector.Instance != null)
            {
                GameplayDirector.Instance.OnAccessItemFound(keyDisplayName);
            }
            else
            {
                if (AudioManager.Instance != null && AudioManager.Instance.itemPickupClip != null)
                {
                    AudioManager.Instance.Play2D(AudioManager.Instance.itemPickupClip, 0.8f);
                }

                if (HUDManager.Instance != null)
                {
                    HUDManager.Instance.ShowNotification($"Collected: {keyDisplayName}");
                }
            }

            if (HorrorEscape.Managers.EscapeMissionManager.Instance != null)
            {
                HorrorEscape.Managers.EscapeMissionManager.Instance.OnKeyFound(keyDisplayName);
            }

            Destroy(gameObject);
        }
    }
}

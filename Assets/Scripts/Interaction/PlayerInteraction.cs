using UnityEngine;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Raycasts from player's view center to detect and interact with world objects.
    /// Updates HUD crosshair and prompt text dynamically.
    /// </summary>
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private float interactDistance = 2.8f;
        [SerializeField] private LayerMask interactableLayers = ~0; // Default to everything
        [SerializeField] private Transform raycastOrigin;

        private IInteractable currentTarget;

        private void Awake()
        {
            if (raycastOrigin == null && Camera.main != null)
            {
                raycastOrigin = Camera.main.transform;
            }
        }

        private void Update()
        {
            DetectInteractable();
            HandleInteractionInput();
        }

        private void DetectInteractable()
        {
            if (raycastOrigin == null) return;

            Ray ray = new Ray(raycastOrigin.position, raycastOrigin.forward);
            if (Physics.Raycast(ray, out RaycastHit hit, interactDistance, interactableLayers, QueryTriggerInteraction.Collide))
            {
                IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
                if (interactable != null)
                {
                    currentTarget = interactable;
                    if (HUDManager.Instance != null)
                    {
                        HUDManager.Instance.SetInteractionPrompt(interactable.GetInteractionPrompt(), true);
                    }
                    return;
                }
            }

            // No interactable in focus
            currentTarget = null;
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.SetInteractionPrompt(string.Empty, false);
            }
        }

        private void HandleInteractionInput()
        {
            if (currentTarget != null && Input.GetKeyDown(KeyCode.E))
            {
                currentTarget.Interact(this);
            }
        }
    }
}

using UnityEngine;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interface for any world object the player can inspect, pick up, or activate.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Text prompt displayed on the player's HUD when looking at this object (e.g. "[E] Open Door").
        /// </summary>
        string GetInteractionPrompt();

        /// <summary>
        /// Action executed when the player presses the interaction key.
        /// </summary>
        void Interact(PlayerInteraction player);
    }
}

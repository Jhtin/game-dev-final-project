using UnityEngine;
using HorrorEscape.Audio;
using HorrorEscape.UI;

namespace HorrorEscape.Interaction
{
    /// <summary>
    /// Interactive Lore Document / Note that reveals story and puzzle clues.
    /// </summary>
    public class NotePickup : MonoBehaviour, IInteractable
    {
        [Header("Note Content")]
        [SerializeField] private string noteTitle = "Log: Night Shift, Oct 12";
        [TextArea(4, 10)]
        [SerializeField] private string noteBody = "If you're reading this, the main generator went down. The backup emergency exit requires all 3 power fuses to unlock. I hid one in the storage room and another in the medical ward. Don't run in the halls... it hears everything.";

        public string GetInteractionPrompt()
        {
            return "[E] Read Document";
        }

        public void Interact(PlayerInteraction player)
        {
            if (AudioManager.Instance != null && AudioManager.Instance.noteOpenClip != null)
            {
                AudioManager.Instance.Play2D(AudioManager.Instance.noteOpenClip, 0.8f);
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShowNote(noteTitle, noteBody);
            }
        }
    }
}

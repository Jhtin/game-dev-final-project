using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>Shows <see cref="frame"/> while its selectable (an input field, a slot) is selected.</summary>
    public class FocusFrame : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Graphic frame;

        public void OnSelect(BaseEventData e) => frame.enabled = true;

        public void OnDeselect(BaseEventData e) => frame.enabled = false;

        void OnDisable()
        {
            if (frame != null)
                frame.enabled = false;
        }
    }
}

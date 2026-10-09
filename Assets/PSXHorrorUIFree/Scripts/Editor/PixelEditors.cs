using UnityEditor;
using UnityEditor.UI;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>Unity's Button and Toggle inspectors, with the kit's own fields under them.</summary>
    [CustomEditor(typeof(PixelButton), true), CanEditMultipleObjects]
    class PixelButtonEditor : ButtonEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Fields.Draw(serializedObject, "label", "icon", "focus", "textNormal", "textHover", "textPressed", "textDisabled",
                "iconNormal", "iconHover", "iconPressed", "iconDisabled", "padding", "pressedPadding");
        }
    }

    [CustomEditor(typeof(PixelToggle), true), CanEditMultipleObjects]
    class PixelToggleEditor : ToggleEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            Fields.Draw(serializedObject, "label", "textNormal", "textHover");
        }
    }

    static class Fields
    {
        public static void Draw(SerializedObject so, params string[] names)
        {
            so.Update();
            EditorGUILayout.Space();
            foreach (var n in names)
                EditorGUILayout.PropertyField(so.FindProperty(n), true);
            so.ApplyModifiedProperties();
        }
    }
}

using UnityEngine;

namespace HeyHeyThere.PSXHorrorUIFree
{
    /// <summary>
    /// One theme's prefabs by name ("Button", "Window", "Slider" ...), for code that builds UI or swaps
    /// themes; <see cref="iconColor"/> is what the theme tints the white icons with.
    /// </summary>
    public class PixelTheme : ScriptableObject
    {
        public string[] names;
        public GameObject[] prefabs;
        public Color iconColor = Color.white;

        public GameObject Prefab(string name)
        {
            int i = System.Array.IndexOf(names, name);
            return i < 0 ? null : prefabs[i];
        }

        public bool Has(string name) => Prefab(name) != null;

        public GameObject Make(string name, Transform parent)
        {
            var prefab = Prefab(name);
            if (prefab == null)
                throw new System.ArgumentException($"{this.name} has no {name}");
            var go = Instantiate(prefab, parent, false);
            go.name = name;
            return go;
        }

        public T Make<T>(string name, Transform parent) where T : Component => Make(name, parent).GetComponent<T>();
    }
}

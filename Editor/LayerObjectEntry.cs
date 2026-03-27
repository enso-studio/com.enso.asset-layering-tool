using UnityEditor;
using UnityEngine;

namespace AssetLayeringTool.Editor
{
    public class LayerObjectEntry
    {
        public SpriteRenderer SpriteRenderer { get; private set; }

        public Realm Realm { get; private set; }
        public Color RealmColor { get; private set; }

        public string Name => SpriteRenderer.gameObject.name;
        public string SortingLayerName => SpriteRenderer.sortingLayerName;
        public int SortingOrder => SpriteRenderer.sortingOrder;
        public float Z => SpriteRenderer.transform.position.z;
        public GameObject GameObject => SpriteRenderer.gameObject;

        public LayerObjectEntry(SpriteRenderer renderer, Realm realm, Color realmColor)
        {
            SpriteRenderer = renderer;
            Realm = realm;
            RealmColor = realmColor;
        }

        public void Select()
        {
            if (SpriteRenderer.gameObject == null) return;

            Selection.activeGameObject = GameObject;
            EditorGUIUtility.PingObject(SpriteRenderer);
        }
    }
}
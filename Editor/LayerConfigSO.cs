using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace AssetLayeringTool.Editor
{
    [CreateAssetMenu(menuName = "Scriptable Objects/Asset Layering Tool/Layer Config", fileName = "SO_NewLayerConfig")]
    public class LayerConfigSO : ScriptableObject
    {
        [System.Serializable]
        public class LayerConfig
        {
            [SortingLayer, ReadOnly]
            public string layerName;
            public float depthValue;
        }

        private SpriteRenderer spriteRenderer;

        [TitleGroup("Realm Settings", "Insert the name of the parent objects in the hierarchy where you placed your assets")]
        [HorizontalGroup("Realm Settings/North")]
        public string northParentObjectName;
        [HorizontalGroup("Realm Settings/North"), LabelText("Color")]
        public Color northColor = new(0.2f, 0.6f, 1f);

        [HorizontalGroup("Realm Settings/South")]
        public string southParentObjectName;
        [HorizontalGroup("Realm Settings/South"), LabelText("Color")]
        public Color southColor = new(1f, 0.3f, 0.3f);

        [HorizontalGroup("Realm Settings/West")]
        public string westParentObjectName;
        [HorizontalGroup("Realm Settings/West"), LabelText("Color")]
        public Color westColor = new(1f, 0.8f, 0.2f);

        [HorizontalGroup("Realm Settings/East")]
        public string eastParentObjectName;
        [HorizontalGroup("Realm Settings/East"), LabelText("Color")]
        public Color eastColor = new(0.5f, 0.2f, 1.0f);

        [Title("Layer Configs")]
        public List<LayerConfig> layerConfigs = new();

        public float GetZValueForLayer(string layerName)
        {
            foreach (var mapping in layerConfigs)
            {
                if (mapping.layerName == layerName)
                    return mapping.depthValue;
            }
            return 0f;
        }

        public Color GetRealmColor(Realm realm)
        {
            return realm switch
            {
                Realm.North => northColor,
                Realm.East => eastColor,
                Realm.South => southColor,
                Realm.West => westColor,
                _ => Color.gray // Fallback Color
            };
        }

        [Button("Sync Layers from Project", ButtonSizes.Medium)]
        private void SyncLayersWithUnity()
        {
            var syncedList = new List<LayerConfig>();

            foreach (var layer in SortingLayer.layers)
            {
                var existingConfig = layerConfigs.Find(x => x.layerName == layer.name);
                if (existingConfig != null)
                {
                    syncedList.Add(existingConfig);
                }
                else
                {
                    syncedList.Add(new LayerConfig
                    {
                        layerName = layer.name,
                        depthValue = 0f
                    });
                }
            }

            layerConfigs = syncedList;

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
    }
}
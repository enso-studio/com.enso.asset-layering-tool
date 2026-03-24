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
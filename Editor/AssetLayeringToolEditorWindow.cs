using Sirenix.OdinInspector;
using UnityEngine;
using UnityEditor;
using Sirenix.OdinInspector.Editor;

namespace AssetLayeringTool.Editor
{
    public class LayerManagerWindow : OdinMenuEditorWindow
    {
        [SerializeField]
        private LayerConfigSO configManager;

        // Öffnet das Fenster über die obere Menüleiste in Unity
        [MenuItem("Tools/2D Layer Manager")]
        private static void OpenWindow()
        {
            GetWindow<LayerManagerWindow>("Layer Manager").Show();
        }

        protected override OdinMenuTree BuildMenuTree()
        {
            var tree = new OdinMenuTree();
            tree.Selection.SupportsMultiSelect = false;

            var layeringView = new AssetLayeringView(configManager);
            tree.Add("Asset Layering", layeringView, SdfIconType.LayersFill);

            tree.Add("Settings", configManager, SdfIconType.GearFill);

            return tree;
        }
    }
}
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AssetLayeringTool.Editor
{
    public class SortingLayerAttribute : PropertyAttribute { }

    [CustomPropertyDrawer(typeof(SortingLayerAttribute))]
    public class SortingLayerDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.String)
            {
                EditorGUI.LabelField(position, label.text, "Use [SortingLayer] with strings only");
                return;
            }

            string[] layerNames = GetAllSortingLayers();

            int currentIndex = Mathf.Max(0, System.Array.IndexOf(layerNames, property.stringValue));
            int newIndex = EditorGUI.Popup(position, label.text, currentIndex, layerNames);

            property.stringValue = layerNames[newIndex];
        }

        private string[] GetAllSortingLayers()
        {
            System.Type internalEditorUtilityType = typeof(UnityEditorInternal.InternalEditorUtility);
            PropertyInfo sortingLayersProperty = internalEditorUtilityType.GetProperty("sortingLayerNames", BindingFlags.Static | BindingFlags.NonPublic);
            return (string[])sortingLayersProperty.GetValue(null, null);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetLayeringTool.Editor
{
    public enum ColumnSortingMode { SortingOrder, GameObjectName, SpriteName }

    public class LayerColumn
    {
        public string Layer { get; private set; }

        private List<LayerObjectEntry> entriesList = new();
        private Vector2 scrollPosition;
        private Action<string, int> onDropRequested;
        private LayerConfigSO config;
        private ColumnSortingMode sortingMode = ColumnSortingMode.SortingOrder;

        public LayerColumn(string layerName, LayerConfigSO config, Action<string, int> onDropCallback)
        {
            Layer = layerName;
            this.config = config;
            onDropRequested = onDropCallback;
        }

        public void RefreshData(IEnumerable<LayerObjectEntry> newEntries)
        {
            entriesList = newEntries
                .Where(e => e.SortingLayerName == Layer)
                .OrderBy(e => e.SortingOrder)
                .ToList();

            ApplySorting();
        }

        private void ApplySorting()
        {
            switch (sortingMode)
            {
                case ColumnSortingMode.SortingOrder:
                    entriesList = entriesList.OrderBy(e => e.SortingOrder).ToList();
                    break;
                case ColumnSortingMode.GameObjectName:
                    entriesList = entriesList.OrderBy(e => e.Name).ThenBy(e => e.SortingOrder).ToList();
                    break;
                case ColumnSortingMode.SpriteName:
                    entriesList = entriesList.OrderBy(e => e.SpriteRenderer.sprite != null ? e.SpriteRenderer.sprite.name : "")
                        .ThenBy(e => e.SortingOrder).ToList();
                    break;
            }
        }

        private void ApplyZValue(float newZ)
        {
            var transformsToUndo = entriesList
                .Where(e => e.SpriteRenderer != null)
                .Select(e => e.SpriteRenderer.transform)
                .ToArray();

            if (transformsToUndo.Length > 0)
            {
                Undo.RecordObjects(transformsToUndo, "Apply Z-Values to Layer");

                foreach (var entry in entriesList)
                {
                    if (entry.SpriteRenderer == null) continue;

                    Vector3 pos = entry.GameObject.transform.position;
                    pos.z = newZ;
                    entry.GameObject.transform.position = pos;

                    EditorUtility.SetDirty(entry.GameObject.transform);
                }

                Debug.Log($"[{Layer}] Z-value ({newZ}) applied to {transformsToUndo.Length} objects.");
            }
        }

        public void DrawColumn()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(250f));

            // --- Header ---
            GUILayout.BeginVertical("box");

            GUILayout.Label(Layer, EditorStyles.boldLabel);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Z-Value:", GUILayout.Width(60));
            float currentZ = config.GetZValueForLayer(Layer);
            EditorGUI.BeginChangeCheck();
            float newZ = EditorGUILayout.FloatField(currentZ);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(config, "Changed Layer Z-Value");
                config.ChangeZValue(Layer, newZ);
            }

            var buttonContent = new GUIContent(" Apply", EditorGUIUtility.IconContent("Refresh").image, "Refreshes the z-value of the entries.");
            if (GUILayout.Button(buttonContent, GUILayout.Height(20f), GUILayout.Width(65f)))
            {
                ApplyZValue(newZ);
            }
            GUILayout.EndHorizontal();

            // Sorting
            GUILayout.BeginHorizontal();
            GUILayout.Label("Sort by:", GUILayout.Width(60));
            EditorGUI.BeginChangeCheck();
            sortingMode = (ColumnSortingMode)EditorGUILayout.EnumPopup(sortingMode);
            if (EditorGUI.EndChangeCheck())
            {
                ApplySorting();
            }
            GUILayout.EndHorizontal();

            GUILayout.EndVertical();
            // --- END HEADER ---

            GUILayout.Space(5f);

            // --- COLUMN SCROLL VIEW ---
            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var entry in entriesList.ToList())
            {
                if (entry.SpriteRenderer == null) continue;
                DrawDraggableItem(entry);
            }

            Rect emptySpaceRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.MinHeight(50));
            int lastOrder = entriesList.Count > 0 ? entriesList.Last().SortingOrder + 1 : 0;
            HandleDropZone(emptySpaceRect, lastOrder, isEndOfList: true);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            // --- END COLUMN SCROLL VIEW ---
        }

        private void DrawDraggableItem(LayerObjectEntry entry)
        {
            Event evt = Event.current;

            // --- SELECTION HIGHLIGHT ---
            Rect itemRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(44));
            bool isSelected = Selection.gameObjects.Contains(entry.GameObject);
            if (isSelected)
            {
                var selectedColor = new Color(0.243f, 0.373f, 0.588f, 1f);
                EditorGUI.DrawRect(itemRect, selectedColor);
            }
            else
            {
                GUI.Box(itemRect, "", "button");
            }

            // --- ICON ---
            Rect iconRect = new Rect(itemRect.x + 5, itemRect.y + 5, 34, 34);
            Rect iconBackgroundRect = new Rect(iconRect.x - 2, iconRect.y - 2, iconRect.width + 4, iconRect.height + 4);
            EditorGUI.DrawRect(iconBackgroundRect, new Color(0f, 0f, 0f, 0.4f));

            Texture2D spriteTexture = AssetPreview.GetAssetPreview(entry.SpriteRenderer.sprite);

            if (spriteTexture != null) {
                if (GUI.Button(iconBackgroundRect.Contains(evt.mousePosition)
                        ? iconBackgroundRect : iconRect, spriteTexture, GUIStyle.none)) { // GUIStyle.none get rid of Button-Outline
                    Selection.activeObject = entry.SpriteRenderer.sprite;
                    EditorGUIUtility.PingObject(entry.SpriteRenderer.sprite);
                }
            }

            // --- TEXT ---
            float textX = iconRect.xMax + 10;
            Rect nameRect = new Rect(textX, itemRect.y + 2, itemRect.width - 120, 20);
            GUI.Label(nameRect, entry.Name, EditorStyles.label);

            Rect spriteNameRect = new Rect(textX, itemRect.y + 18, itemRect.width - 120, 15);
            GUI.Label(spriteNameRect, entry.SpriteRenderer.sprite.name, EditorStyles.miniLabel);

            // --- REALM INDICATOR ---
            if (entry.Realm != Realm.None)
            {
                Rect circleRect = new Rect(itemRect.xMax - 75, itemRect.y + 12, 20, 20);

                if (evt.type == EventType.Repaint)
                {
                    Color oldColor = Handles.color;
                    Handles.color = entry.RealmColor;
                    Handles.DrawSolidDisc(circleRect.center, Vector3.forward, 10f);
                    Handles.color = oldColor;
                }

                GUIStyle letterStyle = new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(circleRect, entry.Realm.ToString().Substring(0, 1), letterStyle);
            }

            // --- SORTING ORDER ---
            Rect orderRect = new Rect(itemRect.xMax - 45, itemRect.y + 10, 40, 20);
            GUI.Box(orderRect, entry.SortingOrder.ToString(), EditorStyles.textField);

            // --- CLICK TO SELECT & DOUBLE CLICK TO FRAME ---
            if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition) && evt.button == 0)
            {
                if (evt.clickCount == 1) {
                    entry.Select();
                } else if (evt.clickCount == 2) {
                    SceneView.FrameLastActiveSceneView();
                }
                evt.Use();
            }
            // --- START DRAG & DROP ---
            else if (evt.type == EventType.MouseDrag && itemRect.Contains(evt.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { entry.GameObject };
                DragAndDrop.StartDrag("Dragging Renderer");
                evt.Use();
            }

            HandleDropZone(itemRect, entry.SortingOrder, isEndOfList: false);
        }

        private void HandleDropZone(Rect dropRect, int targetOrder, bool isEndOfList)
        {
            Event evt = Event.current;
            if (!dropRect.Contains(evt.mousePosition)) return;

            // Hover
            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Move;
                evt.Use();
            }
            // Indicator where to drop
            else if (evt.type == EventType.Repaint && DragAndDrop.visualMode == DragAndDropVisualMode.Move)
            {
                Rect indicatorRect = isEndOfList ? new Rect(dropRect.x, dropRect.y + 10, dropRect.width, 2) : new Rect(dropRect.x, dropRect.y, dropRect.width, 2);
                EditorGUI.DrawRect(indicatorRect, new Color(0.12f, 0.46f, 1f, 1f));
            }
            // Drag release
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                onDropRequested?.Invoke(Layer, targetOrder);
                evt.Use();
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetLayeringTool.Editor
{
    public class LayerColumn
    {
        public string Layer { get; private set; }

        private List<LayerObjectEntry> entriesList = new();
        private Vector2 scrollPosition;
        private Action<string, int> onDropRequested;

        private bool isDraggingOver = false;
        private bool isEndOfList = false;
        private int dropIndex = -1;

        public LayerColumn(string layerName, Action<string, int> onDropCallback)
        {
            Layer = layerName;
            onDropRequested = onDropCallback;
        }

        public void RefreshData(IEnumerable<SpriteRenderer> renderers)
        {
            entriesList.Clear();

            foreach (var renderer in renderers)
            {
                if (renderer.sortingLayerName == Layer)
                {
                    entriesList.Add(new LayerObjectEntry(renderer));
                }
            }

            entriesList = entriesList.OrderBy(e => e.SortingOrder).ToList();
        }

        public void DrawColumn()
        {
            GUILayout.BeginVertical("box", GUILayout.Width(250f));
            GUILayout.Label(Layer, EditorStyles.boldLabel);
            GUILayout.Space(5f);

            scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));

            foreach (var entry in entriesList.ToList())
            {
                DrawDraggableItem(entry);
            }

            Rect emptySpaceRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.ExpandHeight(true), GUILayout.MinHeight(50));
            int lastOrder = entriesList.Count > 0 ? entriesList.Last().SortingOrder + 1 : 0;
            HandleDropZone(emptySpaceRect, lastOrder, isEndOfList: true);

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawDraggableItem(LayerObjectEntry entry)
        {
            Rect itemRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(35));

            // Hintergrundbox
            GUI.Box(itemRect, "", "button");

            // Draw the gameObject Icon
            Rect iconRect = new Rect(itemRect.x + 5, itemRect.y + 5, 25, 25);
            Texture2D spriteTexture = AssetPreview.GetAssetPreview(entry.SpriteRenderer.sprite);
            if (spriteTexture != null)
            {
                GUI.DrawTexture(iconRect, spriteTexture, ScaleMode.ScaleToFit);
            }

            // Text links (Name) und rechts (Order)
            GUI.Label(new Rect(itemRect.x + iconRect.width + 8, itemRect.y - 5, itemRect.width - 60, itemRect.height), entry.Name);
            GUI.Label(new Rect(itemRect.x + iconRect.width + 8, itemRect.y + 10, itemRect.width - 60, itemRect.height), $"Sprite: {entry.SpriteRenderer.sprite.name}", EditorStyles.miniLabel);
            GUI.Label(new Rect(itemRect.xMax - 60, itemRect.y + 2, 55, itemRect.height), $"[{entry.SortingOrder}]", EditorStyles.miniLabel);

            Event evt = Event.current;

            if (itemRect.Contains(evt.mousePosition))
            {
                GUI.Box(itemRect, "", "box");
            }

            // Klick zum Auswählen
            if (evt.type == EventType.MouseDown && itemRect.Contains(evt.mousePosition))
            {
                entry.Select();
                evt.Use(); // Event konsumieren
            }
            // Maus ziehen -> Drag & Drop starten
            else if (evt.type == EventType.MouseDrag && itemRect.Contains(evt.mousePosition))
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { entry.GameObject };
                DragAndDrop.StartDrag("Dragging Renderer");
                evt.Use();
            }

            // Drop-Zone für genau dieses Element verarbeiten
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
            // Green indicator where to drop
            else if (evt.type == EventType.Repaint && DragAndDrop.visualMode == DragAndDropVisualMode.Move)
            {
                Rect indicatorRect = isEndOfList ? new Rect(dropRect.x, dropRect.y + 10, dropRect.width, 2) : new Rect(dropRect.x, dropRect.y, dropRect.width, 2);
                EditorGUI.DrawRect(indicatorRect, Color.cyan);
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

    public class AssetLayeringView
    {
        private LayerConfigSO config;
        private Vector2 horizontalScrollPos;

        private Dictionary<string, LayerColumn> columnsDictionary = new();

        private List<SpriteRenderer> cachedRenderers = new();

        public AssetLayeringView(LayerConfigSO config)
        {
            this.config = config;
            InitializeColumns();
            RefreshData();
            Undo.undoRedoPerformed += RefreshData;
        }

        private void InitializeColumns()
        {
            columnsDictionary.Clear();
            foreach (var layer in SortingLayer.layers)
            {
                columnsDictionary[layer.name] = new LayerColumn(layer.name, ApplyDrop);
            }
        }

        [Button("Refresh Scene Data", ButtonSizes.Large, Icon = SdfIconType.ArrowClockwise)]
        public void RefreshData()
        {
            cachedRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).ToList();

            foreach (var column in columnsDictionary.Values)
            {
                column.RefreshData(cachedRenderers);
            }
        }

        private void RefreshColumn(HashSet<string> layersToRefresh)
        {
            // Remove deleted references
            cachedRenderers.RemoveAll(r => r == null);

            foreach (string layer in layersToRefresh)
            {
                if (columnsDictionary.TryGetValue(layer, out LayerColumn column))
                {
                    column.RefreshData(cachedRenderers);
                }
            }
        }

        [OnInspectorGUI]
        private void DrawGrid()
        {
            GUILayout.Space(10);
            horizontalScrollPos = GUILayout.BeginScrollView(horizontalScrollPos);
            GUILayout.BeginHorizontal();

            foreach (var column in columnsDictionary.Values)
            {
                column.DrawColumn();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void ApplyDrop(string targetLayer, int targetOrder)
        {
            List<SpriteRenderer> draggedRenderers = new List<SpriteRenderer>();
            HashSet<string> affectedLayers = new() { targetLayer };

            foreach (var draggedObj in DragAndDrop.objectReferences)
            {
                if (draggedObj is GameObject go)
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        draggedRenderers.Add(sr);
                        affectedLayers.Add(sr.sortingLayerName);

                        if (!cachedRenderers.Contains(sr)) cachedRenderers.Add(sr);
                    }
                }
            }

            if (draggedRenderers.Count == 0) return;

            // Get all renderers that need to be shifted after the Drop
            List<SpriteRenderer> renderersToShift = new();
            foreach (var renderer in cachedRenderers)
            {
                if (renderer.sortingLayerName == targetLayer &&
                    renderer.sortingOrder >= targetOrder &&
                    !draggedRenderers.Contains(renderer))
                {
                    renderersToShift.Add(renderer);
                }
            }

            // Undo support
            List<Object> undoObjects = new();
            foreach (var sr in draggedRenderers) { undoObjects.Add(sr); undoObjects.Add(sr.transform); }
            foreach (var sr in renderersToShift) { undoObjects.Add(sr); }
            Undo.RecordObjects(undoObjects.ToArray(), "Insert and Shift Layering");

            // Paste dropped objects and adjust sorting order and Z-Value
            //TODO: Not counting correctly, need to only update if the next order isn't lower then the one before
            int shiftAmount = draggedRenderers.Count;
            foreach (var sr in renderersToShift)
            {
                sr.sortingOrder += shiftAmount;
                EditorUtility.SetDirty(sr);
            }

            float targetZ = config.GetZValueForLayer(targetLayer);
            int currentInsertOrder = targetOrder;

            foreach (var sr in draggedRenderers)
            {
                sr.sortingLayerName = targetLayer;
                sr.sortingOrder = currentInsertOrder;
                currentInsertOrder++;

                Vector3 pos = sr.transform.position;
                pos.z = targetZ;
                sr.transform.position = pos;

                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(sr.transform);
            }

            // Refresh so we see the updated view
            RefreshColumn(affectedLayers);
        }
    }
}
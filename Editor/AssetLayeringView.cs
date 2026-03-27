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

        public LayerColumn(string layerName, Action<string, int> onDropCallback)
        {
            Layer = layerName;
            onDropRequested = onDropCallback;
        }

        public void RefreshData(IEnumerable<LayerObjectEntry> newEntries)
        {
            // entriesList.Clear();
            //
            // foreach (var renderer in renderers)
            // {
            //     if (renderer.sortingLayerName == Layer)
            //     {
            //         entriesList.Add(new LayerObjectEntry(renderer));
            //     }
            // }
            //
            // entriesList = entriesList.OrderBy(e => e.SortingOrder).ToList();

            entriesList = newEntries
                .Where(e => e.SortingLayerName == Layer)
                .OrderBy(e => e.SortingOrder)
                .ToList();
        }

        public void DrawColumn()
        {
            GUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(250f));

            Rect headerRect = GUILayoutUtility.GetRect(0, 25, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(headerRect, new Color(0f, 0f, 0f, 0.6f));
            GUI.Label(new Rect(headerRect.x + 5, headerRect.y, headerRect.width - 70, headerRect.height), Layer, EditorStyles.boldLabel);
            GUILayout.Space(5f);

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
        }

        private void DrawDraggableItem(LayerObjectEntry entry)
        {
            Event evt = Event.current;

            // Etwas mehr Höhe für den modernen Look
            Rect itemRect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(44));
            GUI.Box(itemRect, "", "button");

            // Icon (linksbündig)
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

            // Texte (mittig zentriert zum Icon)
            float textX = iconRect.xMax + 10;
            Rect nameRect = new Rect(textX, itemRect.y + 2, itemRect.width - 120, 20);
            GUI.Label(nameRect, entry.Name, EditorStyles.label);

            Rect spriteNameRect = new Rect(textX, itemRect.y + 18, itemRect.width - 120, 15);
            GUI.Label(spriteNameRect, entry.SpriteRenderer.sprite.name, EditorStyles.miniLabel);

            // Small circle to indicate the realm
            if (entry.Realm != Realm.None)
            {
                Rect circleRect = new Rect(itemRect.xMax - 75, itemRect.y + 12, 20, 20);

                // Kreise dürfen nur im Repaint-Event gezeichnet werden
                if (evt.type == EventType.Repaint)
                {
                    Color oldColor = Handles.color;
                    Handles.color = entry.RealmColor;
                    Handles.DrawSolidDisc(circleRect.center, Vector3.forward, 10f);
                    Handles.color = oldColor;
                }

                // Den ersten Buchstaben des Realms in die Mitte des Kreises schreiben
                GUIStyle letterStyle = new GUIStyle(EditorStyles.whiteBoldLabel) { alignment = TextAnchor.MiddleCenter };
                GUI.Label(circleRect, entry.Realm.ToString().Substring(0, 1), letterStyle);
            }

            // Rechter Bereich: Die Zahl (Order) in einem schicken Feld
            Rect orderRect = new Rect(itemRect.xMax - 45, itemRect.y + 10, 40, 20);
            GUI.Box(orderRect, entry.SortingOrder.ToString(), EditorStyles.textField);

            // Den Focus-Button machen wir unsichtbar über das ganze Item oder als Icon
            if (Event.current.type == EventType.MouseDown && itemRect.Contains(Event.current.mousePosition) && Event.current.button == 0)
            {
                if (Event.current.clickCount == 1) {
                     Selection.activeGameObject = entry.GameObject;
                } else if (Event.current.clickCount == 2) {
                     SceneView.FrameLastActiveSceneView();
                }
                Event.current.Use();
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

    [Flags]
    public enum Realm
    {
        None = 0,
        North = 1 << 0,
        East = 1 << 1,
        South = 1 << 2,
        West = 1 << 3,
        All = ~0
    }

    [Serializable]
    public class AssetLayeringView
    {
        [OnValueChanged(nameof(RefreshData))]
        [SerializeField, EnumToggleButtons, HideLabel, Title("Filter Realms")]
        private Realm realmsToShow = Realm.All;

        private LayerConfigSO config;
        private Vector2 horizontalScrollPos;

        private Dictionary<string, LayerColumn> columnsDictionary = new();
        private List<SpriteRenderer> cachedRenderers = new();

        private Dictionary<SpriteRenderer, Realm> rendererRealmMap = new();

        public AssetLayeringView(LayerConfigSO config)
        {
            this.config = config;
            InitializeColumns();
            RefreshData();
            Undo.undoRedoPerformed += RefreshData;
        }

        private void FindAssetParents()
        {
            rendererRealmMap.Clear();
            AddRealmRenderers(config.northParentObjectName, Realm.North);
            AddRealmRenderers(config.eastParentObjectName, Realm.East);
            AddRealmRenderers(config.southParentObjectName, Realm.South);
            AddRealmRenderers(config.westParentObjectName, Realm.West);
        }

        private void AddRealmRenderers(string parentObjectName, Realm realm)
        {
            if (string.IsNullOrEmpty(parentObjectName))
            {
                Debug.LogWarning($"Parent object name for realm '{realm}' not assigned.");
                return;
            }

            var parentObject = GameObject.Find(parentObjectName);
            if (parentObject != null)
            {
                var renderers = parentObject.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var r in renderers)
                {
                    rendererRealmMap[r] = realm;
                }
            }
            else
            {
                Debug.LogWarning($"Parent '{parentObjectName}' not found in Scene.");
            }
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

            FindAssetParents();

            var filteredEntries = new List<LayerObjectEntry>();
            foreach (var renderer in cachedRenderers)
            {
                var realm = rendererRealmMap.GetValueOrDefault(renderer, Realm.None);

                if (realmsToShow.HasFlag(realm))
                {
                    var rColor = config.GetRealmColor(realm);
                    filteredEntries.Add(new LayerObjectEntry(renderer, realm, rColor));
                }
            }

            foreach (var column in columnsDictionary.Values)
            {
                column.RefreshData(filteredEntries);
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

            foreach (var draggedObj in DragAndDrop.objectReferences)
            {
                if (draggedObj is GameObject go)
                {
                    SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        draggedRenderers.Add(sr);

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
            RefreshData();
        }
    }
}
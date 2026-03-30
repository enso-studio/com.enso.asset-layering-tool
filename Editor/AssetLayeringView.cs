using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AssetLayeringTool.Editor
{
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
                columnsDictionary[layer.name] = new LayerColumn(layer.name, config, ApplyDrop);
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
            // horizontal scroll movement
            Event evt = Event.current;

            if (evt.type == EventType.ScrollWheel)
            {
                if (evt.modifiers == EventModifiers.Shift)
                {
                    horizontalScrollPos.x += evt.delta.x * 20f;
                    evt.Use();
                }
            }

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
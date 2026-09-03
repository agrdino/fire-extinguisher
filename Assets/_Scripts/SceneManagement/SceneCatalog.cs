using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.SceneManagement
{
    [CreateAssetMenu(fileName = "Scene Catalog", menuName = "Fire Extinguisher/Scene Catalog")]
    public sealed class SceneCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private SceneId _id;
            [SerializeField] private SceneReference _scene = new();

            public SceneId Id => _id;
            public SceneReference Scene => _scene;
        }

        [SerializeField] private List<Entry> _entries = new();

        public bool TryGetScenePath(SceneId id, out string path)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.Id != id || !entry.Scene.IsAssigned) continue;

                path = entry.Scene.Path;
                return true;
            }

            path = string.Empty;
            return false;
        }

        public bool TryGetSceneId(string path, out SceneId id)
        {
            foreach (Entry entry in _entries)
            {
                if (!entry.Scene.IsAssigned
                    || !string.Equals(entry.Scene.Path, path, StringComparison.OrdinalIgnoreCase))
                    continue;

                id = entry.Id;
                return true;
            }

            id = default;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            var assignedIds = new HashSet<SceneId>();
            foreach (Entry entry in _entries)
            {
                entry.Scene.RefreshPath();
                if (!assignedIds.Add(entry.Id))
                    Debug.LogError($"Scene Catalog contains more than one entry for {entry.Id}.", this);
            }
        }
#endif
    }
}

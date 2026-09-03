using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace _Scripts.SceneManagement
{
    [Serializable]
    public sealed class SceneReference
    {
#if UNITY_EDITOR
        [SerializeField] private SceneAsset _sceneAsset;
#endif
        [SerializeField, HideInInspector] private string _path;

        public string Path => _path;
        public bool IsAssigned => !string.IsNullOrWhiteSpace(_path);

#if UNITY_EDITOR
        public void RefreshPath()
        {
            _path = _sceneAsset != null
                ? AssetDatabase.GetAssetPath(_sceneAsset)
                : string.Empty;
        }
#endif
    }
}

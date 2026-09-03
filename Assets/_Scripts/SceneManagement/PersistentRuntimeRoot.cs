using UnityEngine;

namespace _Scripts.SceneManagement
{
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class PersistentRuntimeRoot : MonoBehaviour
    {
        private static PersistentRuntimeRoot _instance;

        public static PersistentRuntimeRoot Instance => _instance;
        public bool IsPrimaryInstance { get; private set; }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
                return;
            }

            _instance = this;
            IsPrimaryInstance = true;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            _instance = null;
            IsPrimaryInstance = false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (transform.parent != null)
                Debug.LogError("PersistentRuntimeRoot must be placed at the root of a scene.", this);
        }
#endif
    }
}

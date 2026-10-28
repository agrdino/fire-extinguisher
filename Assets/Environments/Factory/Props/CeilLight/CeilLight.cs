using UnityEngine;

namespace _Scripts.Environments.Factory
{
    [DisallowMultipleComponent]
    public sealed class CeilLight : MonoBehaviour
    {
        [Header("Visual State")]
        [SerializeField] private GameObject _emission;
        [SerializeField] private GameObject _nonEmission;

        public bool IsPowered { get; private set; } = true;

        private void Awake()
        {
            ApplyVisualState();
        }

        public void SetPowered(bool isPowered)
        {
            IsPowered = isPowered;
            ApplyVisualState();
        }

        private void ApplyVisualState()
        {
            if (_emission != null)
                _emission.SetActive(IsPowered);

            if (_nonEmission != null)
                _nonEmission.SetActive(!IsPowered);
        }

#if UNITY_EDITOR
        private void Reset()
        {
            Transform view = transform.Find("View");
            if (view == null) return;

            Transform emission = view.Find("Emission");
            Transform nonEmission = view.Find("Non Emission");
            _emission = emission != null ? emission.gameObject : null;
            _nonEmission = nonEmission != null ? nonEmission.gameObject : null;
        }

#endif
    }
}

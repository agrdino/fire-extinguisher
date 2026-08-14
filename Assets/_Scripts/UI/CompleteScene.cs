using _Scripts.Controller;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace _Scripts.UI
{
    public class CompleteScene : MonoBehaviour, IScene
    {
        [SerializeField] private Button _btnBack;
        [SerializeField, Min(0.5f)] private float _distanceFromViewer = 1.5f;

        private Transform _uiRoot;
        private Vector3 _initialUIPosition;
        private Quaternion _initialUIRotation;

        private void Awake()
        {
            if (GetComponentInParent<LazyFollow>() == null)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                _uiRoot = canvas != null ? canvas.transform : transform.parent;
                if (_uiRoot != null)
                {
                    _initialUIPosition = _uiRoot.position;
                    _initialUIRotation = _uiRoot.rotation;
                }
            }

            if (_btnBack != null) _btnBack.onClick.AddListener(OnClickBackButton);
        }

        private void OnDestroy()
        {
            if (_btnBack != null) _btnBack.onClick.RemoveListener(OnClickBackButton);
        }

        public void Show()
        {
            MoveUIInFrontOfViewer();
        }

        public void Hide()
        {
            if (_uiRoot != null) _uiRoot.SetPositionAndRotation(_initialUIPosition, _initialUIRotation);
        }

        private void OnClickBackButton()
        {
            ApplicationManager.Instance.SetState(ApplicationState.Start);
        }

        private void MoveUIInFrontOfViewer()
        {
            if (_uiRoot == null) return;

            Camera viewer = Camera.main;
            if (viewer == null) return;

            Vector3 forward = Vector3.ProjectOnPlane(viewer.transform.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude <= Mathf.Epsilon) forward = viewer.transform.forward;
            float distanceFromViewer = Mathf.Max(0.5f, _distanceFromViewer);
            Vector3 position = viewer.transform.position + forward * distanceFromViewer;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            _uiRoot.SetPositionAndRotation(position, rotation);
        }
    }
}

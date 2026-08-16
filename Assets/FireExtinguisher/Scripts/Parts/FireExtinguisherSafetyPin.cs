using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherSafetyPin : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _safetyPin;
        [SerializeField] private Transform _removedTarget;
        [SerializeField, Min(0f)] private float _moveDuration = 0.5f;

        private Vector3 _insertedLocalPosition;
        private Vector3 _moveStartLocalPosition;
        private float _moveElapsed;
        private bool _isMoving;
        private SafetyPinState _currentPinState;

        public Transform HintTarget => _safetyPin != null ? _safetyPin : transform;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _safetyPin = transform.Find("Safety Pin");
            _removedTarget = transform.Find("Remove Target");
        }

        private void Awake()
        {
            _insertedLocalPosition = _safetyPin.localPosition;
        }

        private void OnEnable()
        {
            _fireExtinguisher.OnStateChanged += FireExtinguisher_OnStateChanged;
            SynchronizeVisual(_fireExtinguisher.CurrentState);
        }

        private void OnDisable()
        {
            _fireExtinguisher.OnStateChanged -= FireExtinguisher_OnStateChanged;
            CancelMove();
        }

        private void Update()
        {
            if (!_isMoving) return;

            _moveElapsed += Time.deltaTime;
            float progress = _moveDuration <= 0f ? 1f : Mathf.Clamp01(_moveElapsed / _moveDuration);
            _safetyPin.localPosition = Vector3.Lerp(_moveStartLocalPosition, _removedTarget.localPosition, progress);
            if (progress >= 1f) CompletePull();
        }

        private void SynchronizeVisual(FireExtinguisherState state)
        {
            _currentPinState = state.SafetyPin;
            if (state.SafetyPin == SafetyPinState.Inserted) InsertPin();
            else SetRemovedVisual();
        }

        private void InsertPin()
        {
            CancelMove();
            _safetyPin.localPosition = _insertedLocalPosition;
            _safetyPin.gameObject.SetActive(true);
        }

        private void BeginPull()
        {
            _safetyPin.gameObject.SetActive(true);
            _moveStartLocalPosition = _safetyPin.localPosition;
            _moveElapsed = 0f;
            _isMoving = true;
            if (_moveDuration <= 0f) CompletePull();
        }

        private void CompletePull()
        {
            CancelMove();
            _safetyPin.localPosition = _removedTarget.localPosition;
            _safetyPin.gameObject.SetActive(false);
        }

        private void SetRemovedVisual()
        {
            CancelMove();
            _safetyPin.localPosition = _removedTarget.localPosition;
            _safetyPin.gameObject.SetActive(false);
        }

        private void CancelMove()
        {
            _isMoving = false;
            _moveElapsed = 0f;
        }

        private void FireExtinguisher_OnStateChanged(FireExtinguisherState state)
        {
            if (state.SafetyPin == _currentPinState) return;

            _currentPinState = state.SafetyPin;
            if (state.SafetyPin == SafetyPinState.Inserted) InsertPin();
            else BeginPull();
        }

    }
}

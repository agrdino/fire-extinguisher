using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherLever : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _lever;

        [SerializeField] private float _initialRotation = 0;
        [SerializeField] private float _maximumPressedRotation = 25f;
        [SerializeField, Min(0f)] private float _rotationSpeed = 90f;

        private LeverState _currentLeverState;
        private float _targetRotation;
        private bool _isRotating;

        private void Reset()
        {
            _fireExtinguisher = GetComponentInParent<FireExtinguisher>();
            _lever = transform.Find("Lever");
        }

        private void Awake()
        {
            _initialRotation = _lever.localEulerAngles.z;
        }

        private void OnEnable()
        {
            _fireExtinguisher.OnStateChanged += FireExtinguisher_OnStateChanged;
            SynchronizeRotation(_fireExtinguisher.CurrentState);
        }

        private void OnDisable()
        {
            _fireExtinguisher.OnStateChanged -= FireExtinguisher_OnStateChanged;
            _isRotating = false;
        }

        private void Update()
        {
            if (!_isRotating) return;

            float currentRotation = _lever.localEulerAngles.z;
            float nextRotation = Mathf.MoveTowardsAngle(currentRotation, _targetRotation, _rotationSpeed * Time.deltaTime);
            SetRotation(nextRotation);
            if (Mathf.Abs(Mathf.DeltaAngle(nextRotation, _targetRotation)) <= 0.01f) CompleteAnimation();
        }

        private void SynchronizeRotation(FireExtinguisherState state)
        {
            _currentLeverState = state.Lever;
            _targetRotation = GetTargetRotation(state.Lever);
            SetRotation(_targetRotation);
            _isRotating = false;
        }

        private void CompleteAnimation()
        {
            SetRotation(_targetRotation);
            _isRotating = false;
        }

        private float GetTargetRotation(LeverState state) => state == LeverState.Pressed ? _maximumPressedRotation : _initialRotation;

        private void SetRotation(float rotationZ)
        {
            Vector3 localEulerAngles = _lever.localEulerAngles;
            localEulerAngles.z = rotationZ;
            _lever.localEulerAngles = localEulerAngles;
        }

        private void FireExtinguisher_OnStateChanged(FireExtinguisherState state)
        {
            if (state.Lever == _currentLeverState) return;

            _currentLeverState = state.Lever;
            _targetRotation = GetTargetRotation(state.Lever);
            if (_rotationSpeed <= 0f) CompleteAnimation();
            else _isRotating = true;
        }

    }
}

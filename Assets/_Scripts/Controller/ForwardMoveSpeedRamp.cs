using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

namespace _Scripts.Controller
{
    [DefaultExecutionOrder(-10)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ContinuousMoveProvider))]
    public sealed class ForwardMoveSpeedRamp : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float _rampDuration = 4f;
        [SerializeField, Min(1f)] private float _maximumSpeedMultiplier = 2f;
        [SerializeField, Range(0f, 1f)] private float _forwardInputThreshold = 0.1f;

        private ContinuousMoveProvider _moveProvider;
        private float _baseMoveSpeed;
        private float _forwardMoveTime;

        private void Awake()
        {
            _moveProvider = GetComponent<ContinuousMoveProvider>();
            _baseMoveSpeed = _moveProvider.moveSpeed;
        }

        private void OnEnable()
        {
            if (_moveProvider == null) _moveProvider = GetComponent<ContinuousMoveProvider>();

            _baseMoveSpeed = _moveProvider.moveSpeed;
            _forwardMoveTime = 0f;
        }

        private void Update()
        {
            Vector2 moveInput = _moveProvider.leftHandMoveInput.ReadValue()
                + _moveProvider.rightHandMoveInput.ReadValue();

            if (moveInput.y <= _forwardInputThreshold)
            {
                ResetSpeed();
                return;
            }

            _forwardMoveTime += Time.deltaTime;
            float rampProgress = _rampDuration > 0f
                ? Mathf.Clamp01(_forwardMoveTime / _rampDuration)
                : 1f;
            float speedMultiplier = Mathf.Lerp(1f, _maximumSpeedMultiplier, rampProgress);
            _moveProvider.moveSpeed = _baseMoveSpeed * speedMultiplier;
        }

        private void OnDisable()
        {
            ResetSpeed();
        }

        private void ResetSpeed()
        {
            _forwardMoveTime = 0f;
            if (_moveProvider != null) _moveProvider.moveSpeed = _baseMoveSpeed;
        }
    }
}

using _Scripts.Controller;
using _Scripts.Fires;
using UnityEngine;

namespace _Scripts.Audio
{
    /// <summary>
    /// Selects the result voice from the fire type and recorded failure reasons.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainingResultVoicePresenter : MonoBehaviour
    {
        [SerializeField] private ApplicationManager _applicationManager;

        [Header("Completed")]
        [SerializeField] private VoiceCueReference _completedElectrical;
        [SerializeField] private VoiceCueReference _completedLiquid;

        [Header("Failed")]
        [SerializeField] private VoiceCueReference _extinguisherDepleted;
        [SerializeField] private VoiceCueReference _wrongExtinguisherElectrical;
        [SerializeField] private VoiceCueReference _wrongExtinguisherLiquid;
        [SerializeField] private VoiceCueReference _unknownFailure;

        private VoicePlaybackHandle _activeVoice;

        private void Awake()
        {
            if (_applicationManager == null)
                _applicationManager = ApplicationManager.Instance;
        }

        private void OnEnable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleStateChanged;
            _activeVoice.Stop();
        }

        private void HandleStateChanged(ApplicationState state)
        {
            _activeVoice.Stop();

            VoiceCueReference cue;
            switch (state)
            {
                case ApplicationState.Completed:
                    cue = IsLiquidFire() ? _completedLiquid : _completedElectrical;
                    break;

                case ApplicationState.Failed:
                    cue = ResolveFailureCue();
                    break;

                default:
                    return;
            }

            if (AudioManager.TryGetInstance(out AudioManager manager))
                _activeVoice = manager.PlayVoice(cue);
        }

        private VoiceCueReference ResolveFailureCue()
        {
            TrainingFailureReason reasons = _applicationManager != null
                ? _applicationManager.FailureReasons
                : TrainingFailureReason.None;

            if ((reasons & TrainingFailureReason.IncompatibleExtinguisherSelected) != 0)
            {
                return IsLiquidFire()
                    ? _wrongExtinguisherLiquid
                    : _wrongExtinguisherElectrical;
            }

            if ((reasons & TrainingFailureReason.ExtinguisherDepleted) != 0)
                return _extinguisherDepleted;

            return _unknownFailure;
        }

        private static bool IsLiquidFire()
        {
            FireController fireController = FireController.Instance;
            return fireController != null && fireController.CurrentFireType == FireType.Liquid;
        }
    }
}

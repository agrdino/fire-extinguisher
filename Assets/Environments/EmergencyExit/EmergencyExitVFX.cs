using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    public sealed class EmergencyExitVFX : MonoBehaviour
    {
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private GameObject _sparks;

        private EnvironmentController _EnvironmentController;
        private bool _hasDisplayedSparks;

        private void Reset()
        {
            Transform sparks = transform.Find("Sparks");
            _sparks = sparks != null ? sparks.gameObject : null;
        }

        private void OnEnable()
        {
            ResetEffect();
            _emergencyExit.OnPlayerReached += HandlePlayerReached;
            _EnvironmentController = EnvironmentController.Instance;
            _EnvironmentController.OnEnvironmentChanged += EnvironmentController_OnEnvironmentChanged;
        }

        private void OnDisable()
        {
            _emergencyExit.OnPlayerReached -= HandlePlayerReached;
            _EnvironmentController.OnEnvironmentChanged -= EnvironmentController_OnEnvironmentChanged;

            _EnvironmentController = null;
        }

        private void HandlePlayerReached()
        {
            if (_hasDisplayedSparks) return;

            _hasDisplayedSparks = true;
            _sparks.SetActive(true);
        }

        private void EnvironmentController_OnEnvironmentChanged(EnvironmentType _) => ResetEffect();

        private void ResetEffect()
        {
            _hasDisplayedSparks = false;
            _sparks.SetActive(false);
        }
    }
}

using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    public sealed class EmergencyExitVFX : MonoBehaviour
    {
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private GameObject _sparks;

        private EnviromentController _enviromentController;
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
            _enviromentController = EnviromentController.Instance;
            _enviromentController.OnEnviromentChanged += EnviromentController_OnEnviromentChanged;
        }

        private void OnDisable()
        {
            _emergencyExit.OnPlayerReached -= HandlePlayerReached;
            _enviromentController.OnEnviromentChanged -= EnviromentController_OnEnviromentChanged;

            _enviromentController = null;
        }

        private void HandlePlayerReached()
        {
            if (_hasDisplayedSparks) return;

            _hasDisplayedSparks = true;
            _sparks.SetActive(true);
        }

        private void EnviromentController_OnEnviromentChanged(EnviromentType _) => ResetEffect();

        private void ResetEffect()
        {
            _hasDisplayedSparks = false;
            _sparks.SetActive(false);
        }
    }
}

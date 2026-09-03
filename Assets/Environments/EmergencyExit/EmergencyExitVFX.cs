using UnityEngine;

namespace _Scripts.Controller
{
    [DisallowMultipleComponent]
    public sealed class EmergencyExitVFX : MonoBehaviour
    {
        [SerializeField] private EmergencyExit _emergencyExit;
        [SerializeField] private GameObject _sparks;

        private bool _hasDisplayedSparks;

        private void Reset()
        {
            Transform sparks = transform.Find("Sparks");
            _sparks = sparks != null ? sparks.gameObject : null;
        }

        private void OnEnable()
        {
            ResetEffect();
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached += HandlePlayerReached;
        }

        private void OnDisable()
        {
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached -= HandlePlayerReached;
        }

        private void HandlePlayerReached()
        {
            if (_hasDisplayedSparks || _sparks == null) return;

            _hasDisplayedSparks = true;
            _sparks.SetActive(true);
        }

        private void ResetEffect()
        {
            _hasDisplayedSparks = false;
            if (_sparks != null) _sparks.SetActive(false);
        }
    }
}

using _Scripts.ParticleSystemLerps;
using UnityEngine;

namespace _Scripts.Fires.Visualizes
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystemLerpGroup))]
    public sealed class FireVFX : MonoBehaviour
    {
        [SerializeField] private Fire _fire;
        [SerializeField] private ParticleSystemLerpGroup _fireEffect;

        [Header("Intensity To Blend")]
        [SerializeField, Range(0f, 1f)] private float _zeroBlendIntensityRatio = 0.2f;
        [SerializeField, Range(0f, 1f)] private float _fullBlendIntensityRatio = 0.8f;

        private void Reset()
        {
            _fire = GetComponentInParent<Fire>();
            _fireEffect = GetComponent<ParticleSystemLerpGroup>();
        }

        private void Awake()
        {
            if (_fire == null) _fire = GetComponentInParent<Fire>();
            if (_fireEffect == null) _fireEffect = GetComponent<ParticleSystemLerpGroup>();
        }

        private void OnEnable()
        {
            if (_fire == null || _fireEffect == null) return;

            _fire.OnIntensityChanged += Fire_OnIntensityChanged;
            UpdateBlend();
        }

        private void OnDisable()
        {
            if (_fire == null) return;
            _fire.OnIntensityChanged -= Fire_OnIntensityChanged;
        }

        private void UpdateBlend()
        {
            float blend = GetBlend(_fire.IntensityRatio);
            _fireEffect.SetBlend(blend);
        }

        private float GetBlend(float intensityRatio)
        {
            float fullBlendIntensityRatio = Mathf.Max(_zeroBlendIntensityRatio, _fullBlendIntensityRatio);
            if (Mathf.Approximately(_zeroBlendIntensityRatio, fullBlendIntensityRatio)) return intensityRatio >= fullBlendIntensityRatio ? 1f : 0f;
            return Mathf.InverseLerp(_zeroBlendIntensityRatio, fullBlendIntensityRatio, intensityRatio);
        }

        private void Fire_OnIntensityChanged(float currentIntensity) => UpdateBlend();

    }
}

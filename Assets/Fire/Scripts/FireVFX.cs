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

        [Header("Fire Light")]
        [SerializeField] private Light _fireLight;
        [SerializeField] private Color _fireLightColor = new Color(1f, 0.32f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float _lightIntensityPerBlend = 2f;
        [SerializeField, Min(0f)] private float _lightRange = 4f;
        [SerializeField, Min(1f)] private float _flareUpRangeMultiplier = 1.5f;

        private void Reset()
        {
            _fire = GetComponentInParent<Fire>();
            _fireEffect = GetComponent<ParticleSystemLerpGroup>();
        }

        private void Awake()
        {
            if (_fire == null) _fire = GetComponentInParent<Fire>();
            if (_fireEffect == null) _fireEffect = GetComponent<ParticleSystemLerpGroup>();
            InitializeLight();
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
            UpdateLight(blend);
        }

        private void InitializeLight()
        {
            if (_fireLight == null) _fireLight = GetComponent<Light>();
            if (_fireLight == null) _fireLight = gameObject.AddComponent<Light>();

            _fireLight.type = LightType.Point;
            _fireLight.color = _fireLightColor;
            _fireLight.shadows = LightShadows.None;
        }

        private void UpdateLight(float blend)
        {
            if (_fireLight == null) return;

            float visibleBlend = Mathf.Max(0f, blend);
            _fireLight.enabled = visibleBlend > 0f && !_fire.IsExtinguished;
            _fireLight.intensity = _lightIntensityPerBlend * visibleBlend;
            _fireLight.range = _lightRange * Mathf.Lerp(1f, _flareUpRangeMultiplier, _fire.FlareUpProgress);
        }

        private float GetBlend(float intensityRatio)
        {
            if (intensityRatio > 1f) return intensityRatio;

            float fullBlendIntensityRatio = Mathf.Max(_zeroBlendIntensityRatio, _fullBlendIntensityRatio);
            if (Mathf.Approximately(_zeroBlendIntensityRatio, fullBlendIntensityRatio)) return intensityRatio >= fullBlendIntensityRatio ? 1f : 0f;
            return Mathf.InverseLerp(_zeroBlendIntensityRatio, fullBlendIntensityRatio, intensityRatio);
        }

        private void Fire_OnIntensityChanged(float currentIntensity) => UpdateBlend();

    }
}

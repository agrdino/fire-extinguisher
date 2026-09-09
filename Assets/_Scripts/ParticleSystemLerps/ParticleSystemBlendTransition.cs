using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.ParticleSystemLerps
{
    [DisallowMultipleComponent]
    public sealed class ParticleSystemBlendTransition : MonoBehaviour
    {
        private enum TransitionState
        {
            Cleared,
            FadingIn,
            Visible,
            FadingOut
        }

        [SerializeField] private ParticleSystemLerpGroup _lerpGroup;
        [SerializeField] private List<ParticleSystem> _particleSystems = new();
        [SerializeField, Min(0f)] private float _fadeInDuration = 10f;
        [SerializeField, Min(0f)] private float _fadeOutDuration = 2f;

        private TransitionState _state = TransitionState.Cleared;
        private float _elapsed;
        private float _blend;
        private ParticleSystem.Particle[] _particleBuffer = new ParticleSystem.Particle[128];
        private readonly Dictionary<ulong, byte> _fadeOutStartAlphas = new();

        public bool IsVisible => _state != TransitionState.Cleared;

        private void Reset()
        {
            _lerpGroup = GetComponent<ParticleSystemLerpGroup>();
            _particleSystems.Clear();
            GetComponentsInChildren(true, _particleSystems);
        }

        private void Awake()
        {
            ClearImmediately();
        }

        private void Update()
        {
            switch (_state)
            {
                case TransitionState.FadingIn:
                    UpdateFadeIn();
                    break;

                case TransitionState.FadingOut:
                    UpdateFadeOut();
                    break;
            }
        }

        private void OnDisable()
        {
            ClearImmediately();
        }

        public void FadeInFromClear()
        {
            ClearImmediately();
            SetBlend(0f);

            for (var i = 0; i < _particleSystems.Count; i++)
            {
                if (_particleSystems[i] != null)
                    _particleSystems[i].Play(false);
            }

            _elapsed = 0f;
            _state = _fadeInDuration > 0f
                ? TransitionState.FadingIn
                : TransitionState.Visible;

            if (_state == TransitionState.Visible)
                SetBlend(1f);
        }

        public void FadeOutAndClear()
        {
            if (_state == TransitionState.Cleared)
                return;

            if (_fadeOutDuration <= 0f)
            {
                ClearImmediately();
                return;
            }

            for (var i = 0; i < _particleSystems.Count; i++)
            {
                if (_particleSystems[i] != null)
                    _particleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
            }

            CaptureExistingParticleAlphas();
            _elapsed = 0f;
            _state = TransitionState.FadingOut;
        }

        public void ClearImmediately()
        {
            _state = TransitionState.Cleared;
            _elapsed = 0f;
            _fadeOutStartAlphas.Clear();
            SetBlend(0f);

            for (var i = 0; i < _particleSystems.Count; i++)
            {
                if (_particleSystems[i] != null)
                    _particleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void UpdateFadeIn()
        {
            _elapsed += Time.deltaTime;
            SetBlend(Mathf.Clamp01(_elapsed / _fadeInDuration));

            if (_blend >= 1f)
                _state = TransitionState.Visible;
        }

        private void UpdateFadeOut()
        {
            _elapsed += Time.deltaTime;
            float visibility = 1f - Mathf.Clamp01(_elapsed / _fadeOutDuration);
            ApplyExistingParticleVisibility(visibility);

            if (visibility <= 0f)
                ClearImmediately();
        }

        private void SetBlend(float value)
        {
            _blend = Mathf.Clamp01(value);
            _lerpGroup?.SetBlend(_blend);
        }

        private void CaptureExistingParticleAlphas()
        {
            _fadeOutStartAlphas.Clear();

            for (var systemIndex = 0; systemIndex < _particleSystems.Count; systemIndex++)
            {
                ParticleSystem particleSystem = _particleSystems[systemIndex];
                if (particleSystem == null)
                    continue;

                int particleCount = GetParticles(particleSystem);
                for (var particleIndex = 0; particleIndex < particleCount; particleIndex++)
                {
                    ulong key = GetParticleKey(particleSystem, _particleBuffer[particleIndex]);
                    _fadeOutStartAlphas[key] = _particleBuffer[particleIndex].startColor.a;
                }
            }
        }

        private void ApplyExistingParticleVisibility(float visibility)
        {
            for (var systemIndex = 0; systemIndex < _particleSystems.Count; systemIndex++)
            {
                ParticleSystem particleSystem = _particleSystems[systemIndex];
                if (particleSystem == null)
                    continue;

                int particleCount = GetParticles(particleSystem);
                for (var particleIndex = 0; particleIndex < particleCount; particleIndex++)
                {
                    ulong key = GetParticleKey(particleSystem, _particleBuffer[particleIndex]);
                    if (!_fadeOutStartAlphas.TryGetValue(key, out byte startAlpha))
                        continue;

                    Color32 color = _particleBuffer[particleIndex].startColor;
                    color.a = (byte)Mathf.RoundToInt(startAlpha * visibility);
                    _particleBuffer[particleIndex].startColor = color;
                }

                particleSystem.SetParticles(_particleBuffer, particleCount);
            }
        }

        private int GetParticles(ParticleSystem particleSystem)
        {
            int requiredCapacity = Mathf.Max(1, particleSystem.particleCount);
            if (_particleBuffer.Length < requiredCapacity)
                _particleBuffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(requiredCapacity)];

            return particleSystem.GetParticles(_particleBuffer);
        }

        private static ulong GetParticleKey(
            ParticleSystem particleSystem,
            ParticleSystem.Particle particle)
        {
            return ((ulong)(uint)particleSystem.GetInstanceID() << 32) | particle.randomSeed;
        }
    }
}

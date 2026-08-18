using _Scripts.Audio;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FireExtinguisher), typeof(AudioSource))]
    public sealed class FireExtinguisherAudio : MonoBehaviour
    {
        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private AudioSource _firstAudioSource;
        [SerializeField] private AudioSource _secondAudioSource;
        [SerializeField] private AudioClip _sprayLoop;
        [SerializeField, Min(0f)] private float _overlapDuration = 0.06f;

        private void Reset()
        {
            _fireExtinguisher = GetComponent<FireExtinguisher>();
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0) _firstAudioSource = sources[0];
            if (sources.Length > 1) _secondAudioSource = sources[1];
            ConfigureAudioSources();
        }

        private void Awake()
        {
            if (_fireExtinguisher == null) _fireExtinguisher = GetComponent<FireExtinguisher>();
            ConfigureAudioSources();
        }

        private void OnEnable()
        {
            _fireExtinguisher.OnCanSprayChanged += FireExtinguisher_OnCanSprayChanged;
            UpdateSprayLoop(_fireExtinguisher.CanSpray);
        }

        private void OnDisable()
        {
            _fireExtinguisher.OnCanSprayChanged -= FireExtinguisher_OnCanSprayChanged;
            if (AudioManager.TryGetInstance(out AudioManager audioManager))
                audioManager.StopAlternatingLoop(_firstAudioSource);
            else
                StopAudioSources();
        }

        private void ConfigureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (_firstAudioSource == null && sources.Length > 0) _firstAudioSource = sources[0];
            if (_secondAudioSource == null && sources.Length > 1) _secondAudioSource = sources[1];

            ConfigureAudioSource(_firstAudioSource);
            ConfigureAudioSource(_secondAudioSource);
        }

        private void UpdateSprayLoop(bool canSpray)
        {
            if (!AudioManager.TryGetInstance(out AudioManager audioManager))
            {
                StopAudioSources();
                if (canSpray) Debug.LogError("No AudioManager exists in the active scene.", this);
                return;
            }

            if (canSpray)
                audioManager.PlayAlternatingLoop(
                    _firstAudioSource,
                    _secondAudioSource,
                    _sprayLoop,
                    _overlapDuration);
            else
                audioManager.StopAlternatingLoop(_firstAudioSource);
        }

        private static void ConfigureAudioSource(AudioSource source)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = false;
        }

        private void StopAudioSources()
        {
            if (_firstAudioSource != null) _firstAudioSource.Stop();
            if (_secondAudioSource != null) _secondAudioSource.Stop();
        }

        private void FireExtinguisher_OnCanSprayChanged(bool canSpray) => UpdateSprayLoop(canSpray);
    }
}

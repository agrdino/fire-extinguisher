using _Scripts.Audio;
using UnityEngine;

namespace _Scripts.Fires
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Fire), typeof(AudioSource))]
    public sealed class FireAudio : MonoBehaviour
    {
        [SerializeField] private Fire _fire;
        [SerializeField] private AudioSource _firstAudioSource;
        [SerializeField] private AudioSource _secondAudioSource;
        [SerializeField] private AudioClip _fireLoop;
        [SerializeField, Min(0f)] private float _overlapDuration = 0.1f;
        [SerializeField, Min(1f)] private float _flareUpVolumeMultiplier = 1.5f;

        private volatile float _fireLoopGain = 1f;

        private void Reset()
        {
            _fire = GetComponent<Fire>();
            AudioSource[] sources = GetComponents<AudioSource>();
            if (sources.Length > 0) _firstAudioSource = sources[0];
            if (sources.Length > 1) _secondAudioSource = sources[1];
            ConfigureAudioSources();
        }

        private void Awake()
        {
            if (_fire == null) _fire = GetComponent<Fire>();
            ConfigureAudioSources();
        }

        private void OnEnable()
        {
            _fire.OnIntensityChanged += Fire_OnIntensityChanged;
            UpdateFireAudio();
        }

        private void OnDisable()
        {
            _fire.OnIntensityChanged -= Fire_OnIntensityChanged;
            _fireLoopGain = 1f;
            StopFireLoop();
        }

        private void ConfigureAudioSources()
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            if (_firstAudioSource == null && sources.Length > 0) _firstAudioSource = sources[0];
            if (_secondAudioSource == null && sources.Length > 1) _secondAudioSource = sources[1];

            ConfigureAudioSource(_firstAudioSource);
            ConfigureAudioSource(_secondAudioSource);
        }

        private void UpdateFireLoop()
        {
            if (_fire.IsExtinguished)
            {
                StopFireLoop();
                return;
            }

            if (!AudioManager.TryGetInstance(out AudioManager audioManager))
            {
                StopAudioSources();
                Debug.LogError("No AudioManager exists in the active scene.", this);
                return;
            }

            audioManager.PlayAlternatingLoop(
                _firstAudioSource,
                _secondAudioSource,
                _fireLoop,
                _overlapDuration);
        }

        private void UpdateFireAudio()
        {
            _fireLoopGain = Mathf.Lerp(1f, _flareUpVolumeMultiplier, _fire.FlareUpProgress);
            UpdateFireLoop();
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _fireLoopGain;
            if (gain <= 1f) return;

            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }

        private void StopFireLoop()
        {
            if (AudioManager.TryGetInstance(out AudioManager audioManager))
                audioManager.StopAlternatingLoop(_firstAudioSource);
            else
                StopAudioSources();
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

        private void Fire_OnIntensityChanged(float currentIntensity) => UpdateFireAudio();
    }
}

using System;
using UnityEngine;
using UnityEngine.Audio;

namespace _Scripts.Audio
{
    /// <summary>
    /// Owns one non-spatial narration channel. Starting a new cue replaces the
    /// previous cue so instructional voices never overlap.
    /// </summary>
    [Serializable]
    public sealed class VoicePlaybackSystem
    {
        [SerializeField] private VoiceClipLibrary _library;
        [SerializeField] private string _defaultLocaleCode = "en";
        [SerializeField] private AudioMixerGroup _output;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;
        [SerializeField] private bool _logMissingCues = true;

        private AudioManager _owner;
        private AudioSource _source;
        private string _localeCode;
        private uint _generation;

        public string CurrentLocaleCode => _localeCode;

        internal void Initialize(AudioManager owner)
        {
            _owner = owner;
            _localeCode = string.IsNullOrWhiteSpace(_defaultLocaleCode)
                ? "en"
                : _defaultLocaleCode;

            Transform existing = owner.transform.Find("Localized Voice Source");
            GameObject sourceObject;
            if (existing != null)
            {
                sourceObject = existing.gameObject;
            }
            else
            {
                sourceObject = new GameObject("Localized Voice Source");
                sourceObject.transform.SetParent(owner.transform, false);
            }

            _source = sourceObject.GetComponent<AudioSource>();
            if (_source == null) _source = sourceObject.AddComponent<AudioSource>();
            ConfigureSource();
        }

        public void SetLocale(string localeCode)
        {
            _localeCode = string.IsNullOrWhiteSpace(localeCode) ? "en" : localeCode;
        }

        internal VoicePlaybackHandle Play(VoiceCueReference cue)
        {
            if (_owner == null || _source == null) return default;
            if (_library == null || !_library.TryResolve(cue, _localeCode, out AudioClip clip))
            {
                if (_logMissingCues && !cue.IsEmpty)
                {
                    Debug.LogWarning(
                        $"Voice cue '{cue.Id}' has no clip for '{_localeCode}' or English fallback.",
                        _owner);
                }
                return default;
            }

            _generation++;
            _source.Stop();
            _source.clip = clip;
            _source.Play();
            return new VoicePlaybackHandle(_owner, _generation);
        }

        internal bool IsPlaying(uint generation)
        {
            return generation == _generation && _source != null && _source.isPlaying;
        }

        internal void Stop(uint generation)
        {
            if (generation != _generation) return;
            Stop();
        }

        internal void Stop()
        {
            if (_source == null) return;
            _source.Stop();
            _source.clip = null;
        }

        private void ConfigureSource()
        {
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.dopplerLevel = 0f;
            _source.priority = 32;
            _source.volume = Mathf.Clamp01(_volume);
            _source.outputAudioMixerGroup = _output;
        }
    }
}

using UnityEngine;

namespace _Scripts.Audio
{
    /// <summary>
    /// Standalone Inspector component for playing any configured voice cue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceCuePlayer : MonoBehaviour
    {
        [SerializeField] private VoiceCueReference _cue;
        [SerializeField] private bool _playOnEnable = true;

        private VoicePlaybackHandle _activeVoice;

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            _activeVoice.Stop();
            if (AudioManager.TryGetInstance(out AudioManager manager))
                _activeVoice = manager.PlayVoice(_cue);
        }

        public void Stop()
        {
            _activeVoice.Stop();
            _activeVoice = default;
        }
    }
}

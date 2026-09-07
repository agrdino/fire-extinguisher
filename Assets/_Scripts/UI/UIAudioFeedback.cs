using UnityEngine;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class UIAudioFeedback : MonoBehaviour
    {
        private static UIAudioFeedback _instance;

        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _hoverClip;
        [SerializeField] private AudioClip _clickClip;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public static void PlayHover()
        {
            if (_instance != null) _instance.Play(_instance._hoverClip);
        }

        public static void PlayClick()
        {
            if (_instance != null) _instance.Play(_instance._clickClip);
        }

        private void Play(AudioClip clip)
        {
            if (_audioSource == null || clip == null) return;
            _audioSource.PlayOneShot(clip);
        }
    }
}

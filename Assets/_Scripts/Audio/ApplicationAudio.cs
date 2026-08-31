using System.Collections;
using System.Collections.Generic;
using _Scripts.Controller;
using _Scripts.Fires;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioManager), typeof(AudioSource))]
    public sealed class ApplicationAudio : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private AudioManager _audioManager;
        [SerializeField] private AudioSource _oneShotSource;
        [SerializeField] private AudioSource _escapeLoopSource;

        [Header("Clips")]
        [SerializeField] private AudioClip _completedClip;
        [SerializeField] private AudioClip _escapeClip;
        [SerializeField] private AudioClip _exitClip;
        [SerializeField] private AudioClip _failedClip;
        [SerializeField] private AudioClip _uiClickClip;

        private readonly List<Button> _buttons = new List<Button>();

        private ApplicationManager _applicationManager;
        private FireController _fireController;
        private EmergencyExit _emergencyExit;
        private Coroutine _completedRoutine;

        private void Awake()
        {
            if (_audioManager == null) _audioManager = GetComponent<AudioManager>();
            AudioSource[] sources = GetComponents<AudioSource>();
            if (_oneShotSource == null && sources.Length > 0) _oneShotSource = sources[0];
            if (_escapeLoopSource == null && sources.Length > 1) _escapeLoopSource = sources[1];

            _applicationManager = ApplicationManager.Instance;
            _fireController = FireController.Instance;
            _emergencyExit = _applicationManager != null ? _applicationManager.EmergencyExit : null;

            ConfigureSource(_oneShotSource);
            ConfigureSource(_escapeLoopSource);
        }

        private void OnEnable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged += ApplicationManager_OnStateChanged;
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached += EmergencyExit_OnPlayerReached;

            SubscribeToButtons();
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= ApplicationManager_OnStateChanged;
            if (_emergencyExit != null)
                _emergencyExit.OnPlayerReached -= EmergencyExit_OnPlayerReached;

            UnsubscribeFromButtons();
            CancelCompletedRoutine();
            StopEscapeLoop();
        }

        private void ApplicationManager_OnStateChanged(ApplicationState state)
        {
            CancelCompletedRoutine();
            if (state != ApplicationState.Escape) StopEscapeLoop();

            switch (state)
            {
                case ApplicationState.Escape:
                    if (IsFireStillBurning())
                        _audioManager.PlayLoop(_escapeLoopSource, _escapeClip);
                    break;

                case ApplicationState.Completed:
                    _completedRoutine = StartCoroutine(PlayCompletedAfterExit());
                    break;

                case ApplicationState.Failed:
                    _audioManager.PlayOneShot(_oneShotSource, _failedClip);
                    break;
            }
        }

        private bool IsFireStillBurning()
        {
            return _fireController != null &&
                   _fireController.ActiveFires.Count > 0 &&
                   !_fireController.AreAllFiresExtinguished();
        }

        private IEnumerator PlayCompletedAfterExit()
        {
            if (_exitClip != null && _exitClip.length > 0f)
                yield return new WaitForSecondsRealtime(_exitClip.length);

            _audioManager.PlayOneShot(_oneShotSource, _completedClip);
            _completedRoutine = null;
        }

        private void EmergencyExit_OnPlayerReached()
        {
            StopEscapeLoop();
            _audioManager.PlayOneShot(_oneShotSource, _exitClip);
        }

        private void StopEscapeLoop()
        {
            _audioManager.StopLoop(_escapeLoopSource);
        }

        private static void ConfigureSource(AudioSource source)
        {
            if (source == null) return;
            source.playOnAwake = false;
            source.loop = false;
        }

        private void SubscribeToButtons()
        {
            UnsubscribeFromButtons();

            Button[] buttons = FindObjectsByType<Button>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (Button button in buttons)
            {
                if (button == null) continue;
                button.onClick.AddListener(PlayUIClick);
                _buttons.Add(button);
            }
        }

        private void UnsubscribeFromButtons()
        {
            foreach (Button button in _buttons)
            {
                if (button != null) button.onClick.RemoveListener(PlayUIClick);
            }

            _buttons.Clear();
        }

        private void PlayUIClick()
        {
            _audioManager.PlayOneShot(_oneShotSource, _uiClickClip);
        }

        private void CancelCompletedRoutine()
        {
            if (_completedRoutine == null) return;

            StopCoroutine(_completedRoutine);
            _completedRoutine = null;
        }
    }
}

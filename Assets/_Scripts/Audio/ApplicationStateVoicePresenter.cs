using System;
using System.Collections.Generic;
using _Scripts.Controller;
using UnityEngine;

namespace _Scripts.Audio
{
    /// <summary>
    /// Maps application states to data-configured voice cue IDs.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ApplicationStateVoicePresenter : MonoBehaviour
    {
        [Serializable]
        private sealed class Binding
        {
            [SerializeField] private ApplicationState _state;
            [SerializeField] private VoiceCueReference _cue;

            public ApplicationState State => _state;
            public VoiceCueReference Cue => _cue;
        }

        [SerializeField] private ApplicationManager _applicationManager;
        [SerializeField] private List<Binding> _bindings = new List<Binding>();
        [SerializeField] private bool _playCurrentStateOnStart = true;

        private VoicePlaybackHandle _activeVoice;

        private void Awake()
        {
            if (_applicationManager == null)
                _applicationManager = ApplicationManager.Instance;
        }

        private void OnEnable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged += HandleStateChanged;
        }

        private void Start()
        {
            if (_playCurrentStateOnStart && _applicationManager != null)
                HandleStateChanged(_applicationManager.State);
        }

        private void OnDisable()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleStateChanged;
            _activeVoice.Stop();
        }

        private void HandleStateChanged(ApplicationState state)
        {
            _activeVoice.Stop();
            for (int index = 0; index < _bindings.Count; index++)
            {
                Binding binding = _bindings[index];
                if (binding == null || binding.State != state) continue;

                if (AudioManager.TryGetInstance(out AudioManager manager))
                    _activeVoice = manager.PlayVoice(binding.Cue);
                return;
            }
        }
    }
}

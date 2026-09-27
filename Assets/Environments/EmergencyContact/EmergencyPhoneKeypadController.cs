using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    public sealed class EmergencyPhoneKeypadController : MonoBehaviour
    {
        [SerializeField] private EmergencyContactDirectory _directory;
        [SerializeField] private EmergencyPhoneWorldView _view;
        [SerializeField] private EmergencyPhoneKey[] _keys = Array.Empty<EmergencyPhoneKey>();
        [SerializeField, Min(1)] private int _requiredLength = 4;
        [SerializeField, Range(1, 2)] private int _maximumVisibleContacts = 2;

        private readonly List<EmergencyContactEntry> _validContacts = new();
        private readonly List<EmergencyContactEntry> _activeContacts = new();
        private readonly StringBuilder _input = new();
        private bool _isSessionActive;

        public Transform HintTarget => _view != null ? _view.HintTarget : transform;
        public event Action<EmergencyContactEntry> ValidCallSubmitted;

        private void Awake()
        {
            if (_view == null) _view = GetComponentInChildren<EmergencyPhoneWorldView>(true);
            if (_keys == null || _keys.Length == 0) _keys = GetComponentsInChildren<EmergencyPhoneKey>(true);
            _view?.Initialize(this);
            EndSession();
        }

        public void BeginSession()
        {
            _isSessionActive = true;
            _input.Clear();
            SelectActiveContacts();
            _view?.SetVisible(true);
            _view?.SetContacts(_activeContacts);
            _view?.ShowReadyStatus();
            RefreshState();
        }

        public void EndSession()
        {
            _isSessionActive = false;
            _input.Clear();
            _activeContacts.Clear();
            SetKeysEnabled(false);
            _view?.SetVisible(false);
        }

        public bool TryAppendSymbol(string symbol)
        {
            if (!_isSessionActive || _input.Length >= _requiredLength || !IsAllowedSymbol(symbol)) return false;

            _input.Append(symbol);
            _view?.ShowReadyStatus();
            RefreshState();
            return true;
        }

        public void DeleteLast()
        {
            if (!_isSessionActive || _input.Length == 0) return;
            _input.Length--;
            _view?.ShowReadyStatus();
            RefreshState();
        }

        public void ClearInput()
        {
            if (!_isSessionActive || _input.Length == 0) return;
            _input.Clear();
            _view?.ShowReadyStatus();
            RefreshState();
        }

        public void Submit()
        {
            if (!_isSessionActive || _input.Length != _requiredLength) return;

            string submittedNumber = _input.ToString();
            foreach (EmergencyContactEntry contact in _activeContacts)
            {
                if (!string.Equals(contact.PhoneNumber, submittedNumber, StringComparison.Ordinal)) continue;

                _isSessionActive = false;
                SetKeysEnabled(false);
                _view?.SetActionAvailability(false, false);
                _view?.ShowConnectedStatus(contact);
                ValidCallSubmitted?.Invoke(contact);
                return;
            }

            _view?.ShowInvalidStatus();
        }

        public void Configure(
            EmergencyContactDirectory directory,
            EmergencyPhoneWorldView view,
            EmergencyPhoneKey[] keys)
        {
            _directory = directory;
            _view = view;
            _keys = keys ?? Array.Empty<EmergencyPhoneKey>();
        }

        private void SelectActiveContacts()
        {
            _validContacts.Clear();
            _activeContacts.Clear();
            _directory?.GetValidEntries(_requiredLength, _validContacts);

            for (int index = _validContacts.Count - 1; index > 0; index--)
            {
                int swapIndex = UnityEngine.Random.Range(0, index + 1);
                (_validContacts[index], _validContacts[swapIndex]) = (_validContacts[swapIndex], _validContacts[index]);
            }

            int count = Mathf.Min(_maximumVisibleContacts, _validContacts.Count);
            for (int index = 0; index < count; index++) _activeContacts.Add(_validContacts[index]);

            if (_activeContacts.Count == 0)
                Debug.LogError($"{name} has no valid {_requiredLength}-character emergency contacts configured.", this);
        }

        private void RefreshState()
        {
            bool canEdit = _isSessionActive && _input.Length > 0;
            bool canSubmit = _isSessionActive && _input.Length == _requiredLength && _activeContacts.Count > 0;
            bool canType = _isSessionActive && _input.Length < _requiredLength;

            _view?.RenderInput(_input.ToString(), _requiredLength);
            _view?.SetActionAvailability(canEdit, canSubmit);
            SetKeysEnabled(canType);
        }

        private void SetKeysEnabled(bool isEnabled)
        {
            if (_keys == null) return;
            foreach (EmergencyPhoneKey key in _keys) key?.SetInteractionEnabled(isEnabled);
        }

        private static bool IsAllowedSymbol(string symbol)
        {
            return symbol != null
                && symbol.Length == 1
                && (char.IsDigit(symbol[0]) || symbol[0] == '*' || symbol[0] == '#');
        }
    }
}

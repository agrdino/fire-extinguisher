using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    public sealed class EmergencyPhoneWorldView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _inputText;
        [SerializeField] private TMP_Text _contactsText;
        [SerializeField] private TMP_Text _statusText;
        [SerializeField] private Button _deleteButton;
        [SerializeField] private Button _clearButton;
        [SerializeField] private Button _submitButton;
        [SerializeField] private Color _neutralStatusColor = new(0.66f, 0.78f, 0.84f, 1f);
        [SerializeField] private Color _errorStatusColor = new(1f, 0.3f, 0.25f, 1f);
        [SerializeField] private Color _successStatusColor = new(0.2f, 1f, 0.6f, 1f);

        private EmergencyPhoneKeypadController _controller;

        public Transform HintTarget => _inputText != null ? _inputText.transform : transform;

        public void Initialize(EmergencyPhoneKeypadController controller)
        {
            Unbind();
            _controller = controller;
            if (_deleteButton != null) _deleteButton.onClick.AddListener(_controller.DeleteLast);
            if (_clearButton != null) _clearButton.onClick.AddListener(_controller.ClearInput);
            if (_submitButton != null) _submitButton.onClick.AddListener(_controller.Submit);
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void SetVisible(bool isVisible)
        {
            if (gameObject.activeSelf != isVisible) gameObject.SetActive(isVisible);
        }

        public void SetContacts(IReadOnlyList<EmergencyContactEntry> contacts)
        {
            if (_contactsText == null) return;

            var builder = new StringBuilder();
            for (int index = 0; index < contacts.Count; index++)
            {
                if (index > 0) builder.AppendLine().AppendLine();
                EmergencyContactEntry contact = contacts[index];
                builder.Append("<color=#00C8FF><b>")
                    .Append(contact.PhoneNumber)
                    .Append("</b></color>\n")
                    .Append(contact.DisplayName);
            }

            _contactsText.SetText(builder.ToString());
        }

        public void RenderInput(string input, int requiredLength)
        {
            if (_inputText == null) return;

            var builder = new StringBuilder(requiredLength * 2);
            for (int index = 0; index < requiredLength; index++)
            {
                if (index > 0) builder.Append(' ');
                builder.Append(index < input.Length ? input[index] : '_');
            }

            _inputText.SetText(builder.ToString());
        }

        public void SetActionAvailability(bool canEdit, bool canSubmit)
        {
            if (_deleteButton != null) _deleteButton.interactable = canEdit;
            if (_clearButton != null) _clearButton.interactable = canEdit;
            if (_submitButton != null) _submitButton.interactable = canSubmit;
        }

        public void ShowReadyStatus()
        {
            SetStatus("ENTER 4 CHARACTERS", _neutralStatusColor);
        }

        public void ShowInvalidStatus()
        {
            SetStatus("NUMBER NOT AVAILABLE", _errorStatusColor);
        }

        public void ShowConnectedStatus(EmergencyContactEntry contact)
        {
            SetStatus($"CONNECTING: {contact.DisplayName}", _successStatusColor);
        }

        public void Configure(
            TMP_Text inputText,
            TMP_Text contactsText,
            TMP_Text statusText,
            Button deleteButton,
            Button clearButton,
            Button submitButton)
        {
            _inputText = inputText;
            _contactsText = contactsText;
            _statusText = statusText;
            _deleteButton = deleteButton;
            _clearButton = clearButton;
            _submitButton = submitButton;
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;
            _statusText.color = color;
            _statusText.SetText(message);
        }

        private void Unbind()
        {
            if (_controller == null) return;
            if (_deleteButton != null) _deleteButton.onClick.RemoveListener(_controller.DeleteLast);
            if (_clearButton != null) _clearButton.onClick.RemoveListener(_controller.ClearInput);
            if (_submitButton != null) _submitButton.onClick.RemoveListener(_controller.Submit);
            _controller = null;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using _Scripts.Controller;
using _Scripts.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace _Scripts.Environments.EmergencyContact
{
    public enum HandheldEmergencyPhoneState
    {
        Hidden,
        Dialer,
        Calling,
        Ended
    }

    [DisallowMultipleComponent]
    public sealed class HandheldEmergencyPhoneController : MonoBehaviour
    {
        private const int RequiredPhoneNumberLength = 4;

        public static HandheldEmergencyPhoneController Instance { get; private set; }

        [Header("Contacts")]
        [SerializeField] private EmergencyContactDirectory _directory;

        [Header("View")]
        [SerializeField] private HandheldEmergencyPhoneView _viewPrefab;

        [Header("Hand Attachment")]
        [SerializeField] private Vector3 _localPosition = new(0f, 0.115f, 0.095f);
        [SerializeField] private Vector3 _localEulerAngles = new(58f, 0f, 0f);
        [SerializeField, Min(0.00001f)] private float _canvasScale = 0.00035f;
        [SerializeField, Range(0, 31)] private int _interfaceLayer = 8;

        [Header("Call")]
        [SerializeField, Min(1)] private int _minimumCallDuration = 4;
        [SerializeField, Min(1)] private int _maximumCallDuration = 6;
        [SerializeField, Min(0f)] private float _autoHideDelay = 5f;

        private readonly List<EmergencyContactEntry> _contacts = new();
        private InputAction _triggerAction;
        private InputAction _thumbButtonAction;
        private ApplicationManager _applicationManager;
        private Coroutine _callRoutine;
        private Coroutine _autoHideRoutine;
        private int _selectedContactIndex;
        private int _callDuration;

        private HandheldEmergencyPhoneView _view;
        private GameObject _phoneRoot;
        private GameObject _dialerScreen;
        private GameObject _callScreen;
        private GameObject _endedScreen;
        private Image _background;
        private TMP_Text _dialerNumberText;
        private TMP_Text _departmentText;
        private TMP_Text _callNumberText;
        private TMP_Text _timerText;
        private TMP_Text _endedDepartmentText;
        private TMP_Text _endedCallNumberText;

        public HandheldEmergencyPhoneState State { get; private set; } = HandheldEmergencyPhoneState.Hidden;
        public Transform HintTarget => _phoneRoot != null ? _phoneRoot.transform : transform;

        private EmergencyContactEntry SelectedContact =>
            _contacts.Count > 0 && _selectedContactIndex >= 0 && _selectedContactIndex < _contacts.Count
                ? _contacts[_selectedContactIndex]
                : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("More than one handheld emergency phone exists. Disabling the duplicate.", this);
                enabled = false;
                return;
            }

            Instance = this;
            CreateView();

            _triggerAction = new InputAction(
                "Emergency Phone Trigger",
                InputActionType.Button,
                "<XRController>{LeftHand}/trigger");
            _thumbButtonAction = new InputAction(
                "Emergency Phone Thumbstick Click",
                InputActionType.Button,
                "<XRController>{LeftHand}/thumbstickClicked");
        }

        private void OnEnable()
        {
            _triggerAction?.Enable();
            _thumbButtonAction?.Enable();
        }

        private void Start()
        {
            _applicationManager = ApplicationManager.Instance;
            if (_applicationManager == null)
            {
                Debug.LogError("The handheld emergency phone requires an ApplicationManager.", this);
                enabled = false;
                return;
            }

            _applicationManager.OnStateChanged += HandleApplicationStateChanged;
            HandleApplicationStateChanged(_applicationManager.State);
        }

        private void Update()
        {
            if (_triggerAction?.WasPressedThisFrame() == true)
                HandleTriggerButton();

            if (_thumbButtonAction?.WasPressedThisFrame() == true)
                HandleThumbButton();
        }

        private void OnDisable()
        {
            _triggerAction?.Disable();
            _thumbButtonAction?.Disable();
        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleApplicationStateChanged;

            _triggerAction?.Dispose();
            _thumbButtonAction?.Dispose();
            if (Instance == this) Instance = null;
        }

        private void HandleApplicationStateChanged(ApplicationState applicationState)
        {
            if (applicationState == ApplicationState.ContactEmergencyTeam)
            {
                BeginSession();
                return;
            }

            // Keep the completed-call screen visible during Escape until its timeout
            // expires or the player dismisses it with the trigger.
            if (applicationState == ApplicationState.Escape && State == HandheldEmergencyPhoneState.Ended)
                return;

            ResetSession();
        }

        private void BeginSession()
        {
            StopPhoneRoutines();
            _contacts.Clear();
            _directory?.GetValidEntries(RequiredPhoneNumberLength, _contacts);

            if (_contacts.Count == 0)
            {
                Debug.LogError("The handheld emergency phone has no valid contacts.", this);
                ResetSession();
                return;
            }

            _selectedContactIndex = Random.Range(0, _contacts.Count);
            State = HandheldEmergencyPhoneState.Dialer;
            _phoneRoot.SetActive(true);
            RenderDialer();
            IdleHintController.Instance?.NotifyActivity();
        }

        private void ResetSession()
        {
            StopPhoneRoutines();
            HidePhone();
        }

        private void HandleTriggerButton()
        {
            switch (State)
            {
                case HandheldEmergencyPhoneState.Hidden:
                    break;

                case HandheldEmergencyPhoneState.Dialer:
                    StartCall();
                    break;

                case HandheldEmergencyPhoneState.Calling:
                    // Calls cannot be interrupted.
                    break;

                case HandheldEmergencyPhoneState.Ended:
                    StopAutoHideRoutine();
                    HidePhone();
                    IdleHintController.Instance?.NotifyActivity();
                    break;
            }
        }

        private void HandleThumbButton()
        {
            if (State != HandheldEmergencyPhoneState.Dialer || _contacts.Count < 2) return;

            _selectedContactIndex = (_selectedContactIndex + 1) % _contacts.Count;
            RenderDialer();
            IdleHintController.Instance?.NotifyActivity();
        }

        private void StartCall()
        {
            if (SelectedContact == null) return;

            State = HandheldEmergencyPhoneState.Calling;
            _callDuration = Random.Range(
                Mathf.Min(_minimumCallDuration, _maximumCallDuration),
                Mathf.Max(_minimumCallDuration, _maximumCallDuration) + 1);
            RenderCalling(0);
            StopCallRoutine();
            _callRoutine = StartCoroutine(RunCall());
            IdleHintController.Instance?.NotifyActivity();
        }

        private IEnumerator RunCall()
        {
            float elapsed = 0f;
            int displayedSecond = -1;

            while (elapsed < _callDuration)
            {
                int nextDisplayedSecond = Mathf.Min(Mathf.FloorToInt(elapsed), _callDuration);
                if (nextDisplayedSecond != displayedSecond)
                {
                    displayedSecond = nextDisplayedSecond;
                    SetTimer(displayedSecond);
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            _callRoutine = null;
            State = HandheldEmergencyPhoneState.Ended;
            RenderEnded();
            IdleHintController.Instance?.NotifyActivity();
            _applicationManager?.CompleteEmergencyContact();
            _autoHideRoutine = StartCoroutine(HideAfterDelay());
        }

        private IEnumerator HideAfterDelay()
        {
            if (_autoHideDelay > 0f)
                yield return new WaitForSecondsRealtime(_autoHideDelay);

            _autoHideRoutine = null;
            if (State == HandheldEmergencyPhoneState.Ended)
                HidePhone();
        }

        private void StopCallRoutine()
        {
            if (_callRoutine == null) return;
            StopCoroutine(_callRoutine);
            _callRoutine = null;
        }

        private void StopAutoHideRoutine()
        {
            if (_autoHideRoutine == null) return;
            StopCoroutine(_autoHideRoutine);
            _autoHideRoutine = null;
        }

        private void StopPhoneRoutines()
        {
            StopCallRoutine();
            StopAutoHideRoutine();
        }

        private void HidePhone()
        {
            State = HandheldEmergencyPhoneState.Hidden;
            if (_phoneRoot != null) _phoneRoot.SetActive(false);
        }

        private void RenderDialer()
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            ShowScreen(_dialerScreen, _view.DialerBackgroundColor);
            _dialerNumberText.SetText(contact.PhoneNumber);
        }

        private void RenderCalling(int elapsedSeconds)
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            ShowScreen(_callScreen, _view.CallingBackgroundColor);
            _departmentText.SetText(contact.DisplayName);
            _callNumberText.SetText(contact.PhoneNumber);
            SetTimer(elapsedSeconds);
        }

        private void RenderEnded()
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            ShowScreen(_endedScreen, _view.EndedBackgroundColor);
            _endedDepartmentText.SetText(contact.DisplayName);
            _endedCallNumberText.SetText(contact.PhoneNumber);
        }

        private void ShowScreen(GameObject activeScreen, Color backgroundColor)
        {
            _background.color = backgroundColor;
            _dialerScreen.SetActive(activeScreen == _dialerScreen);
            _callScreen.SetActive(activeScreen == _callScreen);
            _endedScreen.SetActive(activeScreen == _endedScreen);
        }

        private void SetTimer(int totalSeconds)
        {
            int minutes = Mathf.Max(0, totalSeconds) / 60;
            int seconds = Mathf.Max(0, totalSeconds) % 60;
            _timerText.SetText($"{minutes:00}:{seconds:00}");
        }

        private void CreateView()
        {
            if (_viewPrefab == null)
            {
                Debug.LogError("The handheld emergency phone requires a view prefab.", this);
                enabled = false;
                return;
            }

            _view = Instantiate(_viewPrefab, transform);
            _view.name = _viewPrefab.name;
            _phoneRoot = _view.gameObject;
            _phoneRoot.transform.localPosition = _localPosition;
            _phoneRoot.transform.localRotation = Quaternion.Euler(_localEulerAngles);
            _phoneRoot.transform.localScale = Vector3.one * _canvasScale;

            _background = _view.Background;
            _dialerScreen = _view.DialerScreen;
            _callScreen = _view.CallScreen;
            _endedScreen = _view.EndedScreen;
            _dialerNumberText = _view.DialerNumberText;
            _departmentText = _view.DepartmentText;
            _callNumberText = _view.CallNumberText;
            _timerText = _view.TimerText;
            _endedDepartmentText = _view.EndedDepartmentText;
            _endedCallNumberText = _view.EndedCallNumberText;

            SetLayerRecursively(_phoneRoot, _interfaceLayer);
            _phoneRoot.SetActive(false);
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
                SetLayerRecursively(child.gameObject, layer);
        }
    }
}

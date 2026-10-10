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

        private static readonly Color DialerBackgroundColor = Color.white;
        private static readonly Color CallingBackgroundColor = new(0.035f, 0.36f, 0.82f, 1f);
        private static readonly Color PrimaryTextColor = new(0.055f, 0.075f, 0.11f, 1f);
        private static readonly Color SecondaryTextColor = new(0.35f, 0.39f, 0.45f, 1f);
        private static readonly Color CallButtonColor = new(0.1f, 0.72f, 0.35f, 1f);
        private static readonly Color HangUpButtonColor = new(0.9f, 0.08f, 0.08f, 1f);
        private static readonly Color DisabledButtonColor = new(0.72f, 0.75f, 0.79f, 1f);

        public static HandheldEmergencyPhoneController Instance { get; private set; }

        [Header("Contacts")]
        [SerializeField] private EmergencyContactDirectory _directory;

        [Header("View")]
        [SerializeField] private HandheldEmergencyPhoneView _viewPrefab;

        [Header("Hand Attachment")]
        [SerializeField] private Vector3 _localPosition = new(0.065f, 0.115f, 0.095f);
        [SerializeField] private Vector3 _localEulerAngles = new(58f, 0f, 0f);
        [SerializeField, Min(0.00001f)] private float _canvasScale = 0.00035f;
        [SerializeField, Range(0, 31)] private int _interfaceLayer = 8;

        [Header("Call")]
        [SerializeField, Min(1)] private int _minimumCallDuration = 4;
        [SerializeField, Min(1)] private int _maximumCallDuration = 6;

        private readonly List<EmergencyContactEntry> _contacts = new();
        private InputAction _primaryButtonAction;
        private InputAction _secondaryButtonAction;
        private ApplicationManager _applicationManager;
        private Coroutine _callRoutine;
        private int _selectedContactIndex;
        private int _callDuration;

        private HandheldEmergencyPhoneView _view;
        private GameObject _phoneRoot;
        private GameObject _dialerScreen;
        private GameObject _callScreen;
        private Image _background;
        private Image _hangUpButton;
        private TMP_Text _dialerNumberText;
        private TMP_Text _departmentText;
        private TMP_Text _callNumberText;
        private TMP_Text _timerText;

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

            _primaryButtonAction = new InputAction(
                "Emergency Phone X",
                InputActionType.Button,
                "<XRController>{LeftHand}/primaryButton");
            _secondaryButtonAction = new InputAction(
                "Emergency Phone Y",
                InputActionType.Button,
                "<XRController>{LeftHand}/secondaryButton");
        }

        private void OnEnable()
        {
            _primaryButtonAction?.Enable();
            _secondaryButtonAction?.Enable();
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
            if (_primaryButtonAction?.WasPressedThisFrame() == true)
                HandlePrimaryButton();

            if (_secondaryButtonAction?.WasPressedThisFrame() == true)
                HandleSecondaryButton();
        }

        private void OnDisable()
        {
            _primaryButtonAction?.Disable();
            _secondaryButtonAction?.Disable();
        }

        private void OnDestroy()
        {
            if (_applicationManager != null)
                _applicationManager.OnStateChanged -= HandleApplicationStateChanged;

            _primaryButtonAction?.Dispose();
            _secondaryButtonAction?.Dispose();
            if (Instance == this) Instance = null;
        }

        private void HandleApplicationStateChanged(ApplicationState applicationState)
        {
            if (applicationState == ApplicationState.ContactEmergencyTeam)
            {
                BeginSession();
                return;
            }

            // The completed-call screen may stay in the player's hand during Escape.
            // Hiding it is deliberately left to the next X press.
            if (applicationState == ApplicationState.Escape && State == HandheldEmergencyPhoneState.Ended)
                return;

            ResetSession();
        }

        private void BeginSession()
        {
            StopCallRoutine();
            _contacts.Clear();
            _directory?.GetValidEntries(RequiredPhoneNumberLength, _contacts);

            if (_contacts.Count == 0)
            {
                Debug.LogError("The handheld emergency phone has no valid contacts.", this);
                ResetSession();
                return;
            }

            _selectedContactIndex = Random.Range(0, _contacts.Count);
            State = HandheldEmergencyPhoneState.Hidden;
            _phoneRoot.SetActive(false);
            RenderDialer();
        }

        private void ResetSession()
        {
            StopCallRoutine();
            State = HandheldEmergencyPhoneState.Hidden;
            if (_phoneRoot != null) _phoneRoot.SetActive(false);
        }

        private void HandlePrimaryButton()
        {
            switch (State)
            {
                case HandheldEmergencyPhoneState.Hidden:
                    if (_applicationManager?.State == ApplicationState.ContactEmergencyTeam)
                        ShowDialer();
                    break;

                case HandheldEmergencyPhoneState.Dialer:
                    StartCall();
                    break;

                case HandheldEmergencyPhoneState.Calling:
                    // Calls cannot be interrupted.
                    break;

                case HandheldEmergencyPhoneState.Ended:
                    State = HandheldEmergencyPhoneState.Hidden;
                    _phoneRoot.SetActive(false);
                    IdleHintController.Instance?.NotifyActivity();
                    break;
            }
        }

        private void HandleSecondaryButton()
        {
            if (State != HandheldEmergencyPhoneState.Dialer || _contacts.Count < 2) return;

            _selectedContactIndex = (_selectedContactIndex + 1) % _contacts.Count;
            RenderDialer();
            IdleHintController.Instance?.NotifyActivity();
        }

        private void ShowDialer()
        {
            State = HandheldEmergencyPhoneState.Dialer;
            _phoneRoot.SetActive(true);
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
                    SetTimer(displayedSecond, Color.white);
                }

                yield return null;
                elapsed += Time.unscaledDeltaTime;
            }

            _callRoutine = null;
            State = HandheldEmergencyPhoneState.Ended;
            RenderEnded();
            IdleHintController.Instance?.NotifyActivity();
            _applicationManager?.CompleteEmergencyContact();
        }

        private void StopCallRoutine()
        {
            if (_callRoutine == null) return;
            StopCoroutine(_callRoutine);
            _callRoutine = null;
        }

        private void RenderDialer()
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            _background.color = DialerBackgroundColor;
            _dialerScreen.SetActive(true);
            _callScreen.SetActive(false);
            _dialerNumberText.color = PrimaryTextColor;
            _dialerNumberText.SetText(contact.PhoneNumber);
        }

        private void RenderCalling(int elapsedSeconds)
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            _background.color = CallingBackgroundColor;
            _dialerScreen.SetActive(false);
            _callScreen.SetActive(true);
            _departmentText.color = Color.white;
            _callNumberText.color = Color.white;
            _departmentText.SetText(contact.DisplayName);
            _callNumberText.SetText(contact.PhoneNumber);
            _hangUpButton.color = HangUpButtonColor;
            SetTimer(elapsedSeconds, Color.white);
        }

        private void RenderEnded()
        {
            EmergencyContactEntry contact = SelectedContact;
            if (contact == null) return;

            _background.color = DialerBackgroundColor;
            _dialerScreen.SetActive(false);
            _callScreen.SetActive(true);
            _departmentText.color = PrimaryTextColor;
            _callNumberText.color = SecondaryTextColor;
            _departmentText.SetText(contact.DisplayName);
            _callNumberText.SetText(contact.PhoneNumber);
            _hangUpButton.color = DisabledButtonColor;
            SetTimer(_callDuration, HangUpButtonColor);
        }

        private void SetTimer(int totalSeconds, Color color)
        {
            int minutes = Mathf.Max(0, totalSeconds) / 60;
            int seconds = Mathf.Max(0, totalSeconds) % 60;
            _timerText.color = color;
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
            _dialerNumberText = _view.DialerNumberText;
            _departmentText = _view.DepartmentText;
            _callNumberText = _view.CallNumberText;
            _timerText = _view.TimerText;
            _hangUpButton = _view.HangUpButton;

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

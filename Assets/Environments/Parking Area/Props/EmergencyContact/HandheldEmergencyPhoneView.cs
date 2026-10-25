using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    public sealed class HandheldEmergencyPhoneView : MonoBehaviour
    {
        [Header("Phase Visuals")]
        [SerializeField] private Image _background;
        [SerializeField] private GameObject _dialerScreen;
        [SerializeField] private GameObject _callScreen;
        [SerializeField] private GameObject _endedScreen;
        [SerializeField] private Color _dialerBackgroundColor = Color.white;
        [SerializeField] private Color _callingBackgroundColor = new(0f, 0.2465775f, 1f, 1f);
        [SerializeField] private Color _endedBackgroundColor = new(0f, 0.2465775f, 1f, 1f);

        [Header("Dialer")]
        [SerializeField] private TMP_Text _dialerNumberText;

        [Header("Call")]
        [SerializeField] private TMP_Text _departmentText;
        [SerializeField] private TMP_Text _callNumberText;
        [SerializeField] private TMP_Text _timerText;

        [Header("Ended")]
        [SerializeField] private TMP_Text _endedDepartmentText;
        [SerializeField] private TMP_Text _endedCallNumberText;

        public Image Background => _background;
        public GameObject DialerScreen => _dialerScreen;
        public GameObject CallScreen => _callScreen;
        public GameObject EndedScreen => _endedScreen;
        public Color DialerBackgroundColor => _dialerBackgroundColor;
        public Color CallingBackgroundColor => _callingBackgroundColor;
        public Color EndedBackgroundColor => _endedBackgroundColor;
        public TMP_Text DialerNumberText => _dialerNumberText;
        public TMP_Text DepartmentText => _departmentText;
        public TMP_Text CallNumberText => _callNumberText;
        public TMP_Text TimerText => _timerText;
        public TMP_Text EndedDepartmentText => _endedDepartmentText;
        public TMP_Text EndedCallNumberText => _endedCallNumberText;
    }
}

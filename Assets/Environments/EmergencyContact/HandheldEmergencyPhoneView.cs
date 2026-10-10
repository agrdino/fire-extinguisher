using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.Environments.EmergencyContact
{
    [DisallowMultipleComponent]
    public sealed class HandheldEmergencyPhoneView : MonoBehaviour
    {
        [SerializeField] private Image _background;
        [SerializeField] private GameObject _dialerScreen;
        [SerializeField] private GameObject _callScreen;
        [SerializeField] private TMP_Text _dialerNumberText;
        [SerializeField] private TMP_Text _departmentText;
        [SerializeField] private TMP_Text _callNumberText;
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private Image _hangUpButton;

        public Image Background => _background;
        public GameObject DialerScreen => _dialerScreen;
        public GameObject CallScreen => _callScreen;
        public TMP_Text DialerNumberText => _dialerNumberText;
        public TMP_Text DepartmentText => _departmentText;
        public TMP_Text CallNumberText => _callNumberText;
        public TMP_Text TimerText => _timerText;
        public Image HangUpButton => _hangUpButton;
    }
}

using System;
using _Scripts.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.UI
{
    public class FireExtinguisher : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _txtName;
        [SerializeField] private TextMeshProUGUI _txtDescription;
        [SerializeField] private Image _imgIcon;
        [SerializeField] private Button _btnSelect;
        
        public static event Action<int> onSelect;

        private int _id;

        private void Awake()
        {
            _btnSelect.onClick.AddListener(() => onSelect?.Invoke(_id));
        }

        public void ShowFireExtinguisher(FireExtinguisherConfig config)
        {
            _id = config.id;
            _txtName.SetText(config.name);
            _txtDescription.SetText(config.description);
            _imgIcon.sprite = config.icon;
        }
    }
}
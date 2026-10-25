using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Environments.EmergencyContact
{
    [Serializable]
    public sealed class EmergencyContactEntry
    {
        [SerializeField] private string _phoneNumber;
        [SerializeField] private string _displayName;

        public string PhoneNumber => _phoneNumber?.Trim() ?? string.Empty;
        public string DisplayName => _displayName?.Trim() ?? string.Empty;

        public bool IsValid(int requiredLength)
        {
            string number = PhoneNumber;
            if (number.Length != requiredLength || string.IsNullOrWhiteSpace(DisplayName)) return false;

            for (int index = 0; index < number.Length; index++)
            {
                char symbol = number[index];
                if (!char.IsDigit(symbol) && symbol != '*' && symbol != '#') return false;
            }

            return true;
        }
    }

    [CreateAssetMenu(fileName = "Emergency Contact Directory", menuName = "Fire Extinguisher/Emergency Contact Directory")]
    public sealed class EmergencyContactDirectory : ScriptableObject
    {
        [SerializeField] private List<EmergencyContactEntry> _entries = new();

        public IReadOnlyList<EmergencyContactEntry> Entries => _entries;

        public void GetValidEntries(int requiredLength, List<EmergencyContactEntry> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            results.Clear();

            foreach (EmergencyContactEntry entry in _entries)
            {
                if (entry != null && entry.IsValid(requiredLength)) results.Add(entry);
            }
        }
    }
}

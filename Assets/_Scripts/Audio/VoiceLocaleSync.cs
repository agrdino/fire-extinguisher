using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Scripts.Audio
{
    /// <summary>
    /// Keeps narration language aligned with Unity Localization.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoiceLocaleSync : MonoBehaviour
    {
        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
            HandleLocaleChanged(LocalizationSettings.SelectedLocale);
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
        }

        private static void HandleLocaleChanged(Locale locale)
        {
            AudioManager.Instance?.SetVoiceLocale(locale?.Identifier.Code ?? "en");
        }
    }
}

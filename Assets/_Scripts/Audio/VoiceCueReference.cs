using System;
using UnityEngine;

namespace _Scripts.Audio
{
    /// <summary>
    /// Serializable reference to a localized voice cue. Gameplay code does not own
    /// an AudioClip or know where the localized files are stored.
    /// </summary>
    [Serializable]
    public struct VoiceCueReference
    {
        [SerializeField] private string _id;

        public string Id => _id;
        public bool IsEmpty => string.IsNullOrWhiteSpace(_id);

        public VoiceCueReference(string id)
        {
            _id = id;
        }

        public override string ToString()
        {
            return _id ?? string.Empty;
        }
    }
}

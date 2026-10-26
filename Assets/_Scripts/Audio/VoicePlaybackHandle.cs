namespace _Scripts.Audio
{
    /// <summary>
    /// Ownership token for the narration channel. An old presenter cannot stop a
    /// newer cue that has already replaced its voice.
    /// </summary>
    public readonly struct VoicePlaybackHandle
    {
        private readonly AudioManager _owner;
        private readonly uint _generation;

        internal VoicePlaybackHandle(AudioManager owner, uint generation)
        {
            _owner = owner;
            _generation = generation;
        }

        public bool IsPlaying => _owner != null && _owner.IsVoicePlaying(_generation);

        public void Stop()
        {
            if (_owner != null) _owner.StopVoice(_generation);
        }
    }
}

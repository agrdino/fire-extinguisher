using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.Audio
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class AudioManager : MonoBehaviour
    {
        private sealed class AlternatingLoopState
        {
            public AudioSource FirstSource;
            public AudioSource SecondSource;
            public AudioClip Clip;
            public float OverlapDuration;
            public float FirstVolume;
            public float SecondVolume;
            public Coroutine Routine;
        }

        private static AudioManager _instance;

        private readonly Dictionary<AudioSource, AlternatingLoopState> _alternatingLoops =
            new Dictionary<AudioSource, AlternatingLoopState>();

        public static AudioManager Instance => _instance;

        public static bool TryGetInstance(out AudioManager audioManager)
        {
            audioManager = _instance;
            return audioManager != null;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogError("Only one AudioManager can be active at a time.", this);
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance != this) return;

            StopAllAlternatingLoops();
            _instance = null;
        }

        public void PlayAlternatingLoop(
            AudioSource firstSource,
            AudioSource secondSource,
            AudioClip clip,
            float overlapDuration)
        {
            if (firstSource == null || secondSource == null || clip == null) return;
            if (firstSource == secondSource)
            {
                Debug.LogError("An alternating loop requires two different AudioSources.", firstSource);
                return;
            }

            if (_alternatingLoops.TryGetValue(firstSource, out AlternatingLoopState activeLoop))
            {
                if (activeLoop.SecondSource == secondSource && activeLoop.Clip == clip) return;
                StopAlternatingLoop(firstSource);
            }

            PrepareSource(firstSource, clip);
            PrepareSource(secondSource, clip);

            var state = new AlternatingLoopState
            {
                FirstSource = firstSource,
                SecondSource = secondSource,
                Clip = clip,
                OverlapDuration = Mathf.Max(0f, overlapDuration),
                FirstVolume = firstSource.volume,
                SecondVolume = secondSource.volume
            };

            _alternatingLoops.Add(firstSource, state);
            state.Routine = StartCoroutine(RunAlternatingLoop(state));
        }

        public void StopAlternatingLoop(AudioSource firstSource)
        {
            if (firstSource == null) return;
            if (!_alternatingLoops.TryGetValue(firstSource, out AlternatingLoopState state)) return;

            _alternatingLoops.Remove(firstSource);
            if (state.Routine != null) StopCoroutine(state.Routine);
            StopAndRestore(state);
        }

        public void StopAllAlternatingLoops()
        {
            var activeLoops = new List<AlternatingLoopState>(_alternatingLoops.Values);
            _alternatingLoops.Clear();

            foreach (AlternatingLoopState state in activeLoops)
            {
                if (state.Routine != null) StopCoroutine(state.Routine);
                StopAndRestore(state);
            }
        }

        public void PlayOneShot(AudioSource source, AudioClip clip, float volumeScale = 1f)
        {
            if (source == null || clip == null) return;

            source.playOnAwake = false;
            source.loop = false;
            source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));
        }

        public void PlayLoop(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null) return;
            if (source.isPlaying && source.loop && source.clip == clip) return;

            source.Stop();
            source.playOnAwake = false;
            source.clip = clip;
            source.loop = true;
            source.Play();
        }

        public void StopLoop(AudioSource source)
        {
            if (source == null) return;

            source.Stop();
            source.loop = false;
        }

        private IEnumerator RunAlternatingLoop(AlternatingLoopState state)
        {
            AudioSource currentSource = state.FirstSource;
            AudioSource nextSource = state.SecondSource;
            float currentVolume = state.FirstVolume;
            float nextVolume = state.SecondVolume;

            currentSource.volume = currentVolume;
            nextSource.volume = 0f;
            currentSource.Play();
            double currentStartTime = AudioSettings.dspTime;

            while (IsActive(state))
            {
                float currentDuration = GetPlaybackDuration(currentSource, state.Clip);
                float nextDuration = GetPlaybackDuration(nextSource, state.Clip);
                float maximumOverlap = Mathf.Min(currentDuration, nextDuration) * 0.45f;
                float overlap = Mathf.Min(state.OverlapDuration, maximumOverlap);
                double currentEndTime = currentStartTime + currentDuration;
                double nextStartTime = currentEndTime - overlap;

                nextSource.Stop();
                PrepareSource(nextSource, state.Clip);
                nextSource.volume = 0f;
                nextSource.PlayScheduled(nextStartTime);

                while (IsActive(state) && AudioSettings.dspTime < nextStartTime)
                    yield return null;

                while (IsActive(state) && AudioSettings.dspTime < currentEndTime)
                {
                    float blend = overlap <= 0f
                        ? 1f
                        : Mathf.Clamp01((float)((AudioSettings.dspTime - nextStartTime) / overlap));

                    currentSource.volume = currentVolume * (1f - blend);
                    nextSource.volume = nextVolume * blend;
                    yield return null;
                }

                if (!IsActive(state)) yield break;

                currentSource.Stop();
                currentSource.volume = currentVolume;
                nextSource.volume = nextVolume;

                (currentSource, nextSource) = (nextSource, currentSource);
                (currentVolume, nextVolume) = (nextVolume, currentVolume);
                currentStartTime = nextStartTime;
            }
        }

        private bool IsActive(AlternatingLoopState state)
        {
            return _alternatingLoops.TryGetValue(state.FirstSource, out AlternatingLoopState activeState) &&
                   ReferenceEquals(activeState, state);
        }

        private static void PrepareSource(AudioSource source, AudioClip clip)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.clip = clip;
        }

        private static float GetPlaybackDuration(AudioSource source, AudioClip clip)
        {
            return clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
        }

        private static void StopAndRestore(AlternatingLoopState state)
        {
            if (state.FirstSource != null)
            {
                state.FirstSource.Stop();
                state.FirstSource.volume = state.FirstVolume;
            }

            if (state.SecondSource != null)
            {
                state.SecondSource.Stop();
                state.SecondSource.volume = state.SecondVolume;
            }
        }
    }
}

using UnityEngine;

namespace _Scripts.Audio
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioGainFilter : MonoBehaviour
    {
        private volatile float _gain = 1f;

        public float Gain
        {
            get => _gain;
            set => _gain = Mathf.Max(0f, value);
        }

        private void OnDisable() => _gain = 1f;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float gain = _gain;
            if (gain == 1f) return;

            for (int i = 0; i < data.Length; i++)
                data[i] *= gain;
        }
    }
}

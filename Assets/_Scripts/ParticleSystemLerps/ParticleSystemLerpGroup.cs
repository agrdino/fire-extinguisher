using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.ParticleSystemLerps
{
    [DisallowMultipleComponent]
    public sealed class ParticleSystemLerpGroup : MonoBehaviour
    {
        [SerializeField] private List<ParticleSystemLerper> _lerpers = new List<ParticleSystemLerper>();
        [SerializeField, Range(0f, 1f)] private float _blend;

        private float _lastAppliedBlend = float.NaN;

        private void Reset()
        {
            _lerpers.Clear();
            GetComponentsInChildren(true, _lerpers);
        }

        private void Awake()
        {
            ApplyBlend();
        }

        private void Update()
        {
            if (!Mathf.Approximately(_blend, _lastAppliedBlend))
                ApplyBlend();
        }

        public void SetBlend(float value)
        {
            _blend = Mathf.Clamp01(value);
            ApplyBlend();
        }

        private void ApplyBlend()
        {
            _lastAppliedBlend = _blend;

            for (var i = 0; i < _lerpers.Count; i++)
            {
                if (_lerpers[i] != null)
                    _lerpers[i].SetBlend(_blend);
            }
        }
    }
}

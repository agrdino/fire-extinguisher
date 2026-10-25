using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    [AddComponentMenu("Fire Extinguisher/Spray Pass Through")]
    [DisallowMultipleComponent]
    public sealed class SprayPassThrough : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Multiplier applied to the spray's final effect after it passes through this collider. Default: 1.")]
        private float _effectivenessMultiplier = 1f;

        public float EffectivenessMultiplier => Mathf.Max(0f, _effectivenessMultiplier);
    }
}

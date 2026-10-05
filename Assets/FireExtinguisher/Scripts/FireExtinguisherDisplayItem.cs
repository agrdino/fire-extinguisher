using System.Collections;
using System.Collections.Generic;
using _Scripts.Controller;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FireExtinguisherDisplayItem : HandRayInteractable
    {
        private const float FullyVisible = 0f;
        private const float FullyDissolved = 1f;

        [SerializeField] private FireExtinguisherType _type;
        [SerializeField] private FireExtinguisherStation _station;
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Collider _interactionCollider;

        [Header("Hover Highlight")]
        [SerializeField] private Material _hoverMaterial;

        [Header("Hover Label")]
        [SerializeField] private Canvas _hoverCanvas;
        [SerializeField] private TMP_Text _labelText;
        [SerializeField] private Material _alwaysOnTopUiMaterial;
        [SerializeField] private Material _alwaysOnTopTextMaterial;
        [SerializeField] private LocalizedString _label;

        [Header("Dissolve")]
        [SerializeField] private Material _dissolveMaterial;
        [SerializeField] private ParticleSystem _dissolveParticles;
        [SerializeField, Min(0f)] private float _particleDissolveBand = 0.05f;

        private DissolveRendererMaterials _transitionMaterials;
        private DissolveMeshParticleEmitter _particleEmitter;
        private Coroutine _transitionRoutine;
        private Renderer[] _highlightRenderers = System.Array.Empty<Renderer>();
        private Material[][] _originalHighlightMaterials;
        private bool _stationAllowsInteraction;
        private bool _isVisible = true;
        private bool _isHovered;

        public FireExtinguisherType Type => _type;
        public bool IsVisible => _isVisible;
        public override bool IsInteractionEnabled =>
            _stationAllowsInteraction && _isVisible && _transitionRoutine == null;

        private void Awake()
        {
            if (_interactionCollider == null) _interactionCollider = GetComponent<Collider>();
            if (_visualRoot == null) _visualRoot = transform;
            _highlightRenderers = GetMeshRenderers(_visualRoot);
            _transitionMaterials = new DissolveRendererMaterials(_dissolveMaterial);
            _particleEmitter = new DissolveMeshParticleEmitter(_dissolveParticles, _particleDissolveBand);
            ConfigureHoverCanvasRendering();
            RefreshLabel();
            ApplyInteractionState();
            SetHoverVisible(false);
        }

        private void OnEnable()
        {
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged += HandleLocaleChanged;
        }

        private void OnDisable()
        {
            UnityEngine.Localization.Settings.LocalizationSettings.SelectedLocaleChanged -= HandleLocaleChanged;
            CancelTransition();
            _isHovered = false;
            SetHoverVisible(false);
            SetMaterialHighlight(false);
        }

        private void LateUpdate()
        {
            if (!_isHovered || _hoverCanvas == null) return;

            Transform playerView = ApplicationManager.Instance?.PlayerView;
            if (playerView == null) return;
            Vector3 direction = _hoverCanvas.transform.position - playerView.position;
            if (direction.sqrMagnitude > 0.0001f)
                _hoverCanvas.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        public void SetStationInteractionEnabled(bool isEnabled)
        {
            _stationAllowsInteraction = isEnabled;
            if (!isEnabled) SetHovered(false);
            ApplyInteractionState();
        }

        public void SetVisibleImmediate(bool isVisible)
        {
            CancelTransition();
            _isVisible = isVisible;
            if (_visualRoot != null) _visualRoot.gameObject.SetActive(isVisible);
            if (!isVisible) SetHovered(false);
            ApplyInteractionState();
        }

        public void PlayDissolve(bool becomeVisible, float duration)
        {
            CancelTransition();
            _transitionRoutine = StartCoroutine(AnimateDissolve(becomeVisible, duration));
            ApplyInteractionState();
        }

        public override void SetHovered(bool isHovered)
        {
            bool nextValue = isHovered && IsInteractionEnabled;
            if (_isHovered == nextValue) return;

            _isHovered = nextValue;
            SetHoverVisible(_isHovered);
            SetMaterialHighlight(_isHovered);
        }

        public override bool TryActivate()
        {
            return IsInteractionEnabled && _station != null && _station.TrySelect(_type);
        }

        private IEnumerator AnimateDissolve(bool becomeVisible, float duration)
        {
            _isVisible = becomeVisible;
            SetHovered(false);
            if (_visualRoot == null)
            {
                _transitionRoutine = null;
                ApplyInteractionState();
                yield break;
            }

            if (becomeVisible) _visualRoot.gameObject.SetActive(true);
            Renderer[] renderers = GetMeshRenderers(_visualRoot);
            float from = becomeVisible ? FullyDissolved : FullyVisible;
            float to = becomeVisible ? FullyVisible : FullyDissolved;

            if (!_transitionMaterials.Apply(renderers, from) || duration <= 0f)
            {
                _transitionMaterials.Restore();
                _visualRoot.gameObject.SetActive(becomeVisible);
                _transitionRoutine = null;
                ApplyInteractionState();
                yield break;
            }

            _particleEmitter.Begin(renderers);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                float easedProgress = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * progress);
                float amount = Mathf.LerpUnclamped(from, to, easedProgress);
                _transitionMaterials.SetAmount(amount);
                UpdateParticles(renderers, amount);
                yield return null;
            }

            _transitionMaterials.SetAmount(to);
            _transitionMaterials.Restore();
            if (!becomeVisible) _visualRoot.gameObject.SetActive(false);
            StopParticles();
            _transitionRoutine = null;
            ApplyInteractionState();
        }

        private void CancelTransition()
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionMaterials?.Restore();
            StopParticles();
        }

        private void ApplyInteractionState()
        {
            if (_interactionCollider != null) _interactionCollider.enabled = IsInteractionEnabled;
        }

        private void SetHoverVisible(bool isVisible)
        {
            if (_hoverCanvas != null) _hoverCanvas.gameObject.SetActive(isVisible);
        }

        private void ConfigureHoverCanvasRendering()
        {
            if (_hoverCanvas == null) return;

            _hoverCanvas.overrideSorting = true;
            _hoverCanvas.sortingOrder = 1000;

            if (_alwaysOnTopUiMaterial != null)
            {
                Graphic[] graphics = _hoverCanvas.GetComponentsInChildren<Graphic>(true);
                for (int index = 0; index < graphics.Length; index++)
                {
                    Graphic graphic = graphics[index];
                    if (graphic != null && graphic != _labelText)
                        graphic.material = _alwaysOnTopUiMaterial;
                }
            }

            if (_labelText != null && _alwaysOnTopTextMaterial != null)
                _labelText.fontSharedMaterial = _alwaysOnTopTextMaterial;
        }

        private void SetMaterialHighlight(bool isHighlighted)
        {
            if (!isHighlighted)
            {
                RestoreHighlightMaterials();
                return;
            }

            if (_hoverMaterial == null || _originalHighlightMaterials != null) return;
            _originalHighlightMaterials = new Material[_highlightRenderers.Length][];
            for (int index = 0; index < _highlightRenderers.Length; index++)
            {
                Renderer renderer = _highlightRenderers[index];
                if (renderer == null) continue;

                Material[] original = renderer.sharedMaterials;
                _originalHighlightMaterials[index] = original;
                Material[] highlighted = new Material[original.Length + 1];
                System.Array.Copy(original, highlighted, original.Length);
                highlighted[original.Length] = _hoverMaterial;
                renderer.sharedMaterials = highlighted;
            }
        }

        private void RestoreHighlightMaterials()
        {
            if (_originalHighlightMaterials == null) return;
            for (int index = 0; index < _originalHighlightMaterials.Length; index++)
            {
                Renderer renderer = _highlightRenderers[index];
                if (renderer != null && _originalHighlightMaterials[index] != null)
                    renderer.sharedMaterials = _originalHighlightMaterials[index];
            }
            _originalHighlightMaterials = null;
        }

        private void RefreshLabel()
        {
            if (_labelText == null) return;
            if (_label == null || _label.IsEmpty)
            {
                string key = _type == FireExtinguisherType.CO2
                    ? "select.co2_name"
                    : "select.powder_name";
                _label = new LocalizedString("UI", key);
            }
            _labelText.SetText($"{_label.GetLocalizedString()}\n▼");
        }

        private void HandleLocaleChanged(UnityEngine.Localization.Locale locale) => RefreshLabel();

        private void PlayParticles()
        {
            _particleEmitter?.Begin(GetMeshRenderers(_visualRoot));
        }

        private void StopParticles()
        {
            _particleEmitter?.Stop(true);
        }

        private void UpdateParticles(Renderer[] renderers, float amount)
        {
            if (_particleEmitter == null || renderers == null || renderers.Length == 0) return;

            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null) continue;
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else bounds.Encapsulate(renderer.bounds);
            }

            if (!hasBounds) return;
            float minWorldY = bounds.min.y;
            float maxWorldY = bounds.max.y;
            if (DissolveRendererMaterials.TryGetSharedWorldYBounds(
                    out float sharedMinWorldY,
                    out float sharedMaxWorldY))
            {
                minWorldY = sharedMinWorldY;
                maxWorldY = sharedMaxWorldY;
            }

            _particleEmitter.Update(Mathf.Lerp(minWorldY, maxWorldY, Mathf.Clamp01(amount)));
        }

        private static Renderer[] GetMeshRenderers(Transform root)
        {
            if (root == null) return System.Array.Empty<Renderer>();

            Renderer[] allRenderers = root.GetComponentsInChildren<Renderer>(true);
            List<Renderer> meshRenderers = new(allRenderers.Length);
            for (int index = 0; index < allRenderers.Length; index++)
            {
                Renderer renderer = allRenderers[index];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    meshRenderers.Add(renderer);
            }
            return meshRenderers.ToArray();
        }
    }
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace _Scripts.UI
{
    /// <summary>
    /// Presentation-only view for the controller tutorial. The complete hierarchy lives in
    /// Guide Controller Tutorial.prefab so artists can adjust the controller, callout and copy
    /// directly in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuideControllerTutorialView : MonoBehaviour
    {
        public const int StepCount = 3;

        [Header("Controller Targets")]
        [SerializeField] private Transform _controllerVisual;
        [SerializeField] private Transform _triggerTarget;
        [SerializeField] private Transform _stickTarget;
        [SerializeField] private Transform _xButtonTarget;
        [SerializeField] private Transform _yButtonTarget;
        [SerializeField] private Vector3 _triggerViewEuler = new(22f, 95f, 12f);
        [SerializeField] private Vector3 _topViewEuler = new(22f, 150f, 18f);

        [Header("Callout")]
        [SerializeField] private CanvasGroup _calloutCanvasGroup;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private TMP_Text _stepText;
        [SerializeField] private TMP_Text _advanceHintText;
        [SerializeField] private Material _highlightMaterial;

        [Header("Localized Copy")]
        [SerializeField] private LocalizedString _triggerInstruction = new("UI", "guide.controller.interact");
        [SerializeField] private LocalizedString _stickInstruction = new("UI", "guide.controller.move");
        [SerializeField] private LocalizedString _buttonsInstruction = new("UI", "guide.controller.call");
        [SerializeField] private LocalizedString _waitInstruction = new("UI", "guide.controller.wait");
        [SerializeField] private LocalizedString _advanceInstruction = new("UI", "guide.controller.advance");
        [SerializeField] private LocalizedString _completeInstruction = new("UI", "guide.controller.complete");

        private readonly Dictionary<Renderer, Material[]> _originalMaterials = new();
        private readonly List<Renderer> _highlightedRenderers = new();
        private int _currentStep = -1;
        private bool _isComplete;
        private bool _canAdvance;

        private void Awake()
        {
            CacheOriginalMaterials(_triggerTarget);
            CacheOriginalMaterials(_stickTarget);
            CacheOriginalMaterials(_xButtonTarget);
            CacheOriginalMaterials(_yButtonTarget);
        }

        private void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        }

        private void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
            RestoreHighlights();
        }

        public void ShowStep(int stepIndex)
        {
            _isComplete = false;
            _currentStep = Mathf.Clamp(stepIndex, 0, StepCount - 1);
            _canAdvance = false;
            RestoreHighlights();

            if (_controllerVisual != null)
                _controllerVisual.localRotation = Quaternion.Euler(
                    _currentStep == 0 ? _triggerViewEuler : _topViewEuler);

            switch (_currentStep)
            {
                case 0:
                    Highlight(_triggerTarget);
                    break;
                case 1:
                    Highlight(_stickTarget);
                    break;
                default:
                    Highlight(_xButtonTarget);
                    Highlight(_yButtonTarget);
                    break;
            }

            if (_calloutCanvasGroup != null)
            {
                _calloutCanvasGroup.alpha = 1f;
                _calloutCanvasGroup.interactable = false;
                _calloutCanvasGroup.blocksRaycasts = false;
            }

            RefreshLocalizedCopy();
        }

        public void SetCanAdvance(bool canAdvance)
        {
            if (_isComplete || _canAdvance == canAdvance) return;
            _canAdvance = canAdvance;
            RefreshAdvanceHint();
        }

        public void ShowCompleted()
        {
            _isComplete = true;
            _canAdvance = false;
            RestoreHighlights();
            RefreshLocalizedCopy();
        }

        private void Highlight(Transform target)
        {
            if (target == null || _highlightMaterial == null) return;

            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
            {
                if (targetRenderer == null) continue;
                CacheOriginalMaterial(targetRenderer);

                Material[] materials = targetRenderer.sharedMaterials;
                for (int index = 0; index < materials.Length; index++)
                    materials[index] = _highlightMaterial;
                targetRenderer.sharedMaterials = materials;
                _highlightedRenderers.Add(targetRenderer);
            }
        }

        private void RestoreHighlights()
        {
            foreach (Renderer targetRenderer in _highlightedRenderers)
            {
                if (targetRenderer != null && _originalMaterials.TryGetValue(targetRenderer, out Material[] materials))
                    targetRenderer.sharedMaterials = materials;
            }

            _highlightedRenderers.Clear();
        }

        private void CacheOriginalMaterials(Transform target)
        {
            if (target == null) return;
            foreach (Renderer targetRenderer in target.GetComponentsInChildren<Renderer>(true))
                CacheOriginalMaterial(targetRenderer);
        }

        private void CacheOriginalMaterial(Renderer targetRenderer)
        {
            if (targetRenderer != null && !_originalMaterials.ContainsKey(targetRenderer))
                _originalMaterials.Add(targetRenderer, targetRenderer.sharedMaterials);
        }

        private void RefreshLocalizedCopy()
        {
            if (_isComplete)
            {
                if (_instructionText != null) _instructionText.SetText(_completeInstruction.GetLocalizedString());
                if (_stepText != null) _stepText.SetText($"{StepCount} / {StepCount}");
                if (_advanceHintText != null) _advanceHintText.SetText(string.Empty);
                return;
            }

            LocalizedString instruction = _currentStep switch
            {
                0 => _triggerInstruction,
                1 => _stickInstruction,
                _ => _buttonsInstruction
            };

            if (_instructionText != null) _instructionText.SetText(instruction.GetLocalizedString());
            if (_stepText != null) _stepText.SetText($"{_currentStep + 1} / {StepCount}");
            RefreshAdvanceHint();
        }

        private void RefreshAdvanceHint()
        {
            if (_advanceHintText == null || _isComplete) return;
            _advanceHintText.SetText((_canAdvance ? _advanceInstruction : _waitInstruction).GetLocalizedString());
        }

        private void OnSelectedLocaleChanged(Locale _)
        {
            RefreshLocalizedCopy();
        }
    }
}

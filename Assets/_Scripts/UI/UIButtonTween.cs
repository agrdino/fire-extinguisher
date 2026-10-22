using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.UI
{
    /// <summary>
    /// Drives the shared Reach button artwork. Every state reuses the same
    /// Background, Frame, Text and Glow graphics; only colors and glow visibility change.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UIButtonTween : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerDownHandler,
        IPointerUpHandler,
        ISelectHandler,
        IDeselectHandler,
        ISubmitHandler
    {
        private enum VisualState { Normal, Highlighted, Pressed, Disabled }

        [Header("Shared visuals")]
        [SerializeField] private Image _background;
        [SerializeField] private Image _frame;
        [SerializeField] private Image _glow;
        [SerializeField] private TMP_Text[] _textTargets;

        [Header("Normal")]
        [SerializeField] private Color _normalBackgroundColor = new Color(0.09803922f, 0.13725491f, 0.1764706f, 0.9f);
        [SerializeField] private Color _normalFrameColor = new Color(0f, 0.7843137f, 1f, 1f);
        [SerializeField] private Color _normalTextColor = Color.white;

        [Header("Highlighted")]
        [SerializeField] private Color _highlightedBackgroundColor = new Color(0.07843138f, 0.1764706f, 0.2313726f, 0.95f);
        [SerializeField] private Color _highlightedFrameColor = new Color(0f, 0.7843137f, 1f, 1f);
        [SerializeField] private Color _highlightedTextColor = new Color(0f, 0.7843137f, 1f, 1f);
        [SerializeField] private Color _highlightedGlowColor = new Color(0f, 0.7843137f, 1f, 0.15f);

        [Header("Selected / Pressed")]
        [SerializeField] private Color _pressedBackgroundColor = new Color(0.05098039f, 0.08235294f, 0.1137255f, 0.95f);
        [SerializeField] private Color _pressedFrameColor = new Color(1f, 0.4784314f, 0f, 1f);
        [SerializeField] private Color _pressedTextColor = Color.white;
        [SerializeField] private Color _pressedGlowColor = new Color(1f, 0.4784314f, 0f, 0.2f);

        [Header("Disabled")]
        [SerializeField] private Color _disabledBackgroundColor = new Color(0.09803922f, 0.13725491f, 0.1764706f, 0.28f);
        [SerializeField] private Color _disabledFrameColor = new Color(0.66f, 0.78f, 0.84f, 0.28f);
        [SerializeField] private Color _disabledTextColor = new Color(0.66f, 0.78f, 0.84f, 0.5f);

        [Header("Tween")]
        [SerializeField, Min(0f)] private float _duration = 0.1f;
        [SerializeField] private Ease _ease = Ease.OutQuad;
        [SerializeField, Range(1f, 1.05f)] private float _hoverScale = 1.012f;
        [SerializeField, Range(0.9f, 1f)] private float _pressedScale = 0.985f;

        private Button _button;
        private bool _pointerInside;
        private bool _pointerDown;
        private bool _selected;
        private bool _lastInteractable;
        private VisualState _currentState;
        private Vector3 _baseScale;
        private bool _hasBaseScale;

        public bool IsSelected => _selected;

        private void Awake()
        {
            CacheReferences();
            CacheBaseScale();
            ApplyCurrentState(true);
        }

        private void OnEnable()
        {
            CacheReferences();
            CacheBaseScale();
            _lastInteractable = _button != null && _button.interactable;
            if (_button != null) _button.onClick.AddListener(PlayClickSound);
            ApplyCurrentState(true);
        }

        private void OnDisable()
        {
            if (_button != null) _button.onClick.RemoveListener(PlayClickSound);
            KillTweens();
            _pointerInside = false;
            _pointerDown = false;
        }

        private void Update()
        {
            if (_button == null || _button.interactable == _lastInteractable) return;
            _lastInteractable = _button.interactable;
            ApplyCurrentState(false);
        }

        public void ConfigureVisuals(Image background, Image frame, Image glow, TMP_Text[] textTargets)
        {
            _background = background;
            _frame = frame;
            _glow = glow;
            _textTargets = textTargets;
        }

        public void SetSelected(bool value)
        {
            if (_selected == value) return;
            _selected = value;
            ApplyCurrentState(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (_button != null && _button.interactable) UIAudioFeedback.PlayHover();
            ApplyCurrentState(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerInside = false;
            _pointerDown = false;
            ApplyCurrentState(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_button == null || !_button.interactable) return;
            _pointerDown = true;
            ApplyCurrentState(false);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pointerDown = false;
            ApplyCurrentState(false);
        }

        public void OnSelect(BaseEventData eventData)
        {
            _pointerInside = true;
            ApplyCurrentState(false);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _pointerInside = false;
            _pointerDown = false;
            ApplyCurrentState(false);
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (_button == null || !_button.interactable) return;
            ApplyState(VisualState.Pressed, false);
            DOVirtual.DelayedCall(_duration, () => ApplyCurrentState(false), true).SetLink(gameObject);
        }

        private void CacheReferences()
        {
            if (_button == null) _button = GetComponent<Button>();
            if (_button != null) _button.transition = Selectable.Transition.None;

            if (_background == null) _background = FindImage("Background");
            if (_frame == null) _frame = FindImage("Frame");
            if (_glow == null) _glow = FindImage("Glow");
            if (_textTargets == null || _textTargets.Length == 0)
                _textTargets = GetComponentsInChildren<TMP_Text>(true);
        }

        private Image FindImage(string objectName)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
                if (image.name == objectName)
                    return image;
            return null;
        }

        private void ApplyCurrentState(bool instant)
        {
            if (_button == null) return;

            VisualState state;
            if (_selected) state = VisualState.Pressed;
            else if (!_button.interactable) state = VisualState.Disabled;
            else if (_pointerDown) state = VisualState.Pressed;
            else if (_pointerInside) state = VisualState.Highlighted;
            else state = VisualState.Normal;

            ApplyState(state, instant);
        }

        private void ApplyState(VisualState state, bool instant)
        {
            _currentState = state;
            ApplyGraphicColor(_background, GetBackgroundColor(state), instant);
            ApplyGraphicColor(_frame, GetFrameColor(state), instant);
            ApplyGlow(state, instant);
            ApplyScale(state, instant);

            Color textColor = GetTextColor(state);
            if (_textTargets == null) return;
            foreach (TMP_Text text in _textTargets)
                ApplyGraphicColor(text, textColor, instant);
        }

        private void ApplyGlow(VisualState state, bool instant)
        {
            if (_glow == null) return;

            bool visible = state == VisualState.Highlighted || state == VisualState.Pressed;
            Color target = state == VisualState.Pressed ? _pressedGlowColor : _highlightedGlowColor;
            if (!visible) target.a = 0f;

            _glow.DOKill();
            if (visible) _glow.enabled = true;

            if (instant || _duration <= 0f)
            {
                _glow.color = target;
                _glow.enabled = visible;
                return;
            }

            _glow.DOColor(target, _duration)
                .SetEase(_ease)
                .SetUpdate(true)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    if (!visible && _glow != null) _glow.enabled = false;
                });
        }

        private void ApplyScale(VisualState state, bool instant)
        {
            CacheBaseScale();
            transform.DOKill();
            float multiplier = state == VisualState.Highlighted
                ? _hoverScale
                : state == VisualState.Pressed ? _pressedScale : 1f;
            Vector3 target = _baseScale * multiplier;
            if (instant || _duration <= 0f) transform.localScale = target;
            else transform.DOScale(target, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
        }

        private void CacheBaseScale()
        {
            if (_hasBaseScale) return;
            _baseScale = transform.localScale;
            _hasBaseScale = true;
        }

        private void ApplyGraphicColor(Graphic graphic, Color color, bool instant)
        {
            if (graphic == null) return;
            graphic.DOKill();
            if (instant || _duration <= 0f) graphic.color = color;
            else graphic.DOColor(color, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
        }

        private Color GetBackgroundColor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Highlighted: return _highlightedBackgroundColor;
                case VisualState.Pressed: return _pressedBackgroundColor;
                case VisualState.Disabled: return _disabledBackgroundColor;
                default: return _normalBackgroundColor;
            }
        }

        private Color GetFrameColor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Highlighted: return _highlightedFrameColor;
                case VisualState.Pressed: return _pressedFrameColor;
                case VisualState.Disabled: return _disabledFrameColor;
                default: return _normalFrameColor;
            }
        }

        private Color GetTextColor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Highlighted: return _highlightedTextColor;
                case VisualState.Pressed: return _pressedTextColor;
                case VisualState.Disabled: return _disabledTextColor;
                default: return _normalTextColor;
            }
        }

        private void KillTweens()
        {
            transform.DOKill();
            if (_background != null) _background.DOKill();
            if (_frame != null) _frame.DOKill();
            if (_glow != null) _glow.DOKill();
            if (_textTargets == null) return;
            foreach (TMP_Text text in _textTargets)
                if (text != null) text.DOKill();
        }

        private void PlayClickSound()
        {
            if (_currentState != VisualState.Disabled) UIAudioFeedback.PlayClick();
        }
    }
}

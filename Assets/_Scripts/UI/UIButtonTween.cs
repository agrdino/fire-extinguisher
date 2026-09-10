using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.UI
{
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
        private enum VisualState { Normal, Hover, Pressed, Disabled }

        [Header("State visuals")]
        [SerializeField] private CanvasGroup _normalState;
        [SerializeField] private CanvasGroup _hoverState;
        [SerializeField] private CanvasGroup _pressedState;
        [SerializeField] private CanvasGroup _disabledState;

        [Header("Text colors")]
        [SerializeField] private TMP_Text[] _textTargets;
        [SerializeField] private Color _normalTextColor = Color.white;
        [SerializeField] private Color _hoverTextColor = Color.white;
        [SerializeField] private Color _pressedTextColor = new Color(0f, 0.7843137f, 1f, 1f);
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
            _lastInteractable = _button.interactable;
            _button.onClick.AddListener(PlayClickSound);
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

        public void SetSelected(bool value)
        {
            if (_selected == value) return;
            _selected = value;
            ApplyCurrentState(false);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerInside = true;
            if (_button != null && _button.interactable)
                UIAudioFeedback.PlayHover();
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
            DOVirtual.DelayedCall(_duration, () => ApplyCurrentState(false), true)
                .SetLink(gameObject);
        }

        private void CacheReferences()
        {
            if (_button == null) _button = GetComponent<Button>();
            _button.transition = Selectable.Transition.None;

            Transform states = transform.Find("Reach States");
            if (states != null)
            {
                if (_normalState == null) _normalState = FindGroup(states, "Normal");
                if (_hoverState == null) _hoverState = FindGroup(states, "Highlighted");
                if (_pressedState == null) _pressedState = FindGroup(states, "Selected");
                if (_disabledState == null) _disabledState = FindGroup(states, "Disabled");
            }

            if (_pressedState == null) _pressedState = _normalState;
            if (_textTargets == null || _textTargets.Length == 0)
                _textTargets = GetComponentsInChildren<TMP_Text>(true);
        }

        private static CanvasGroup FindGroup(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            return child != null ? child.GetComponent<CanvasGroup>() : null;
        }

        private void ApplyCurrentState(bool instant)
        {
            if (_button == null) return;

            VisualState state;
            if (_selected) state = VisualState.Pressed;
            else if (!_button.interactable) state = VisualState.Disabled;
            else if (_pointerDown) state = VisualState.Pressed;
            else if (_pointerInside) state = VisualState.Hover;
            else state = VisualState.Normal;

            ApplyState(state, instant);
        }

        private void ApplyState(VisualState state, bool instant)
        {
            _currentState = state;
            ApplyGroupAlphas(GetStateGroup(state), instant);
            ApplyScale(state, instant);

            Color color = GetTextColor(state);
            if (_textTargets == null) return;
            foreach (TMP_Text text in _textTargets)
            {
                if (text == null) continue;
                text.DOKill();
                if (instant || _duration <= 0f) text.color = color;
                else text.DOColor(color, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
            }
        }

        private CanvasGroup GetStateGroup(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover: return _hoverState != null ? _hoverState : _normalState;
                case VisualState.Pressed: return _pressedState != null ? _pressedState : _normalState;
                case VisualState.Disabled: return _disabledState != null ? _disabledState : _normalState;
                default: return _normalState;
            }
        }

        private void ApplyGroupAlphas(CanvasGroup activeGroup, bool instant)
        {
            CanvasGroup[] groups = { _normalState, _hoverState, _pressedState, _disabledState };
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                if (group == null) continue;

                bool alreadyHandled = false;
                for (int previous = 0; previous < i; previous++)
                {
                    if (groups[previous] != group) continue;
                    alreadyHandled = true;
                    break;
                }

                if (!alreadyHandled) SetGroupAlpha(group, group == activeGroup, instant);
            }
        }

        private void ApplyScale(VisualState state, bool instant)
        {
            CacheBaseScale();
            transform.DOKill();
            float multiplier = state == VisualState.Hover
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

        private void SetGroupAlpha(CanvasGroup group, bool visible, bool instant)
        {
            if (group == null) return;
            group.DOKill();
            float target = visible ? 1f : 0f;
            if (instant || _duration <= 0f) group.alpha = target;
            else group.DOFade(target, _duration).SetEase(_ease).SetUpdate(true).SetLink(gameObject);
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        private Color GetTextColor(VisualState state)
        {
            switch (state)
            {
                case VisualState.Hover: return _hoverTextColor;
                case VisualState.Pressed: return _pressedTextColor;
                case VisualState.Disabled: return _disabledTextColor;
                default: return _normalTextColor;
            }
        }

        private void KillTweens()
        {
            transform.DOKill();
            if (_normalState != null) _normalState.DOKill();
            if (_hoverState != null) _hoverState.DOKill();
            if (_pressedState != null) _pressedState.DOKill();
            if (_disabledState != null) _disabledState.DOKill();
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

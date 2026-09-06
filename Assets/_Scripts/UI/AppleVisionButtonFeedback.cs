using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class AppleVisionButtonFeedback : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IDeselectHandler
    {
        [SerializeField] private Graphic _hoverGraphic;
        [SerializeField, Range(1f, 1.08f)] private float _hoverScale = 1.025f;
        [SerializeField, Range(0.9f, 1f)] private float _pressedScale = 0.98f;
        [SerializeField, Min(1f)] private float _response = 18f;
        [SerializeField, Range(0f, 1f)] private float _hoverOpacity = 0.22f;

        private RectTransform _rectTransform;
        private Vector3 _baseScale;
        private bool _hovered;
        private bool _pressed;

        public void Configure(Graphic hoverGraphic)
        {
            _hoverGraphic = hoverGraphic;
            SetHoverOpacity(0f);
        }

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            _baseScale = _rectTransform != null ? _rectTransform.localScale : transform.localScale;
            SetHoverOpacity(0f);
        }

        private void OnEnable()
        {
            _hovered = false;
            _pressed = false;
        }

        private void Update()
        {
            if (_rectTransform == null) return;

            float multiplier = _pressed ? _pressedScale : _hovered ? _hoverScale : 1f;
            Vector3 targetScale = _baseScale * multiplier;
            float t = 1f - Mathf.Exp(-_response * Time.unscaledDeltaTime);
            _rectTransform.localScale = Vector3.Lerp(_rectTransform.localScale, targetScale, t);

            if (_hoverGraphic != null)
            {
                float targetOpacity = _hovered && !_pressed ? _hoverOpacity : 0f;
                Color color = _hoverGraphic.color;
                color.a = Mathf.Lerp(color.a, targetOpacity, t);
                _hoverGraphic.color = color;
            }
        }

        private void OnDisable()
        {
            if (_rectTransform != null) _rectTransform.localScale = _baseScale;
            SetHoverOpacity(0f);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hovered = true;
        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => _pressed = true;
        public void OnPointerUp(PointerEventData eventData) => _pressed = false;
        public void OnDeselect(BaseEventData eventData) => _pressed = false;

        private void SetHoverOpacity(float opacity)
        {
            if (_hoverGraphic == null) return;
            Color color = _hoverGraphic.color;
            color.a = opacity;
            _hoverGraphic.color = color;
        }
    }
}

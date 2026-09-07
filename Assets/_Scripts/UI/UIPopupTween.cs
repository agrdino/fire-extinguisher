using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIPopupTween : MonoBehaviour
    {
        [SerializeField] private bool _playOnEnable;
        [SerializeField, Min(0.01f)] private float _duration = 0.1667f;
        [SerializeField] private AnimationCurve _curve = new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(1f, 1f));
        [SerializeField] private UnityEvent _onShow = new UnityEvent();
        [SerializeField] private UnityEvent _onVisible = new UnityEvent();
        [SerializeField] private UnityEvent _onHide = new UnityEvent();

        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private Sequence _sequence;
        private bool _isVisible;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (_playOnEnable) PlayIn();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        public void Animate()
        {
            if (_isVisible) PlayOut();
            else PlayIn();
        }

        public void PlayIn()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            KillSequence();

            _isVisible = true;
            _rectTransform.localScale = Vector3.zero;
            _canvasGroup.alpha = 0f;
            _onShow.Invoke();

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Join(_rectTransform.DOScale(1f, _duration).SetEase(_curve));
            _sequence.Join(_canvasGroup.DOFade(1f, _duration).SetEase(_curve));
            _sequence.OnComplete(() => _onVisible.Invoke());
        }

        public void PlayOut()
        {
            ResolveReferences();
            KillSequence();
            _isVisible = false;
            _onHide.Invoke();

            if (!gameObject.activeInHierarchy)
            {
                _canvasGroup.alpha = 0f;
                _rectTransform.localScale = Vector3.zero;
                return;
            }

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Join(_rectTransform.DOScale(0f, _duration).SetEase(_curve));
            _sequence.Join(_canvasGroup.DOFade(0f, _duration).SetEase(_curve));
            _sequence.OnComplete(() => gameObject.SetActive(false));
        }

        private void ResolveReferences()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
        }

        private void KillSequence()
        {
            if (_sequence == null) return;
            _sequence.Kill();
            _sequence = null;
        }
    }
}

using DG.Tweening;
using UnityEngine;

namespace _Scripts.UI
{
    [DisallowMultipleComponent]
    public sealed class UIPanelTween : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _panel;
        [SerializeField] private RectTransform _content;

        private Sequence _sequence;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        public void PlayIn()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            KillSequence();

            if (_panel == null)
            {
                if (_content != null) _content.localScale = Vector3.one;
                return;
            }

            _panel.alpha = 0f;
            _panel.interactable = false;
            _panel.blocksRaycasts = false;
            if (_content != null) _content.localScale = Vector3.one * 1.2f;

            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Insert(0.1667f, _panel.DOFade(1f, 0.1666f).SetEase(Ease.Linear));
            if (_content != null)
            {
                _sequence.Insert(0.1667f, _content.DOScale(1.03f, 0.1666f).SetEase(Ease.OutQuad));
                _sequence.Insert(0.3333f, _content.DOScale(1f, 0.3334f).SetEase(Ease.OutQuad));
            }
            _sequence.InsertCallback(0.3333f, () =>
            {
                if (_panel == null) return;
                _panel.interactable = true;
                _panel.blocksRaycasts = true;
            });
        }

        public void PlayOutAndDeactivate()
        {
            ResolveReferences();
            KillSequence();
            if (!gameObject.activeInHierarchy || _panel == null)
            {
                gameObject.SetActive(false);
                return;
            }

            _panel.interactable = false;
            _panel.blocksRaycasts = false;
            _sequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
            _sequence.Insert(0f, _panel.DOFade(0f, 0.1667f).SetEase(Ease.Linear));
            if (_content != null)
                _sequence.Insert(0f, _content.DOScale(0.85f, 0.25f).SetEase(Ease.InQuad));
            _sequence.OnComplete(() => gameObject.SetActive(false));
        }

        private void ResolveReferences()
        {
            if (_panel == null) _panel = GetComponentInChildren<CanvasGroup>(true);
            if (_content != null) return;
            Transform[] children = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in children)
            {
                if (child.name != "Content") continue;
                _content = child as RectTransform;
                break;
            }
        }

        private void KillSequence()
        {
            if (_sequence == null) return;
            _sequence.Kill();
            _sequence = null;
        }
    }
}

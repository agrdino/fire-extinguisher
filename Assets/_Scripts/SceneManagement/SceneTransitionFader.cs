using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace _Scripts.SceneManagement
{
    [DisallowMultipleComponent]
    public sealed class SceneTransitionFader : MonoBehaviour
    {
        private const string OverlayName = "Scene Transition Fade";

        private GameObject _overlay;
        private CanvasGroup _canvasGroup;
        private Image _image;

        public void Configure(Camera targetCamera, Color color)
        {
            EnsureOverlay(targetCamera);
            _image.color = color;
        }

        public IEnumerator FadeToBlack(float duration)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            yield return FadeTo(0f, duration);
        }

        private IEnumerator FadeTo(float targetAlpha, float duration)
        {
            EnsureOverlay(Camera.main);
            _overlay.SetActive(true);

            float startAlpha = _canvasGroup.alpha;
            if (duration <= 0f || Mathf.Approximately(startAlpha, targetAlpha))
            {
                _canvasGroup.alpha = targetAlpha;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    _canvasGroup.alpha = Mathf.Lerp(
                        startAlpha,
                        targetAlpha,
                        Mathf.Clamp01(elapsed / duration));
                    yield return null;
                }

                _canvasGroup.alpha = targetAlpha;
            }

            if (targetAlpha <= 0f) _overlay.SetActive(false);
        }

        private void EnsureOverlay(Camera targetCamera)
        {
            if (_overlay != null)
            {
                Canvas existingCanvas = _overlay.GetComponent<Canvas>();
                if (targetCamera != null && existingCanvas.worldCamera != targetCamera)
                {
                    existingCanvas.worldCamera = targetCamera;
                    existingCanvas.planeDistance = GetPlaneDistance(targetCamera);
                }

                return;
            }

            _overlay = new GameObject(
                OverlayName,
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasGroup));
            _overlay.transform.SetParent(transform, false);

            Canvas canvas = _overlay.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = targetCamera;
            canvas.planeDistance = GetPlaneDistance(targetCamera);
            canvas.overrideSorting = true;
            canvas.sortingOrder = short.MaxValue;

            _canvasGroup = _overlay.GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            GameObject imageObject = new("Black", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(_overlay.transform, false);

            RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
            imageTransform.anchorMin = Vector2.zero;
            imageTransform.anchorMax = Vector2.one;
            imageTransform.offsetMin = Vector2.zero;
            imageTransform.offsetMax = Vector2.zero;

            _image = imageObject.GetComponent<Image>();
            _image.color = Color.black;
            _image.raycastTarget = false;

            _overlay.SetActive(false);
        }

        private static float GetPlaneDistance(Camera targetCamera)
        {
            return targetCamera == null
                ? 0.1f
                : Mathf.Max(0.1f, targetCamera.nearClipPlane + 0.01f);
        }
    }
}

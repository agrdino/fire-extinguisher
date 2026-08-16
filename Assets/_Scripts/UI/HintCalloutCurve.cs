using UnityEngine;
using UnityEngine.Rendering;

namespace _Scripts.UI
{
    /// <summary>
    /// View-aware callout curve adapted from the Unity VR Template Affordance Callout.
    /// The popup remains camera-followed while this curve connects it to a visible target.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LineRenderer))]
    public sealed class HintCalloutCurve : MonoBehaviour
    {
        private const int SegmentCount = 16;

        private readonly Vector3[] _popupCorners = new Vector3[4];
        private readonly Vector3[] _curvePoints = new Vector3[SegmentCount + 1];

        private LineRenderer _lineRenderer;
        private RectTransform _popupRect;
        private CanvasGroup _canvasGroup;
        private Camera _camera;
        private Transform _target;
        private Renderer[] _targetRenderers;
        private float _targetGap;
        private float _popupGap;
        private float _viewportMargin;
        private float _curveStrength;
        private Color _lineColor;
        private float _lastAlpha = -1f;
        private bool _requestedVisible;

        public void Initialize(
            RectTransform popupRect,
            CanvasGroup canvasGroup,
            Camera viewCamera,
            Material curveMaterial,
            Color lineColor,
            float lineWidth,
            float targetGap,
            float popupGap,
            float viewportMargin,
            float curveStrength)
        {
            _popupRect = popupRect;
            _canvasGroup = canvasGroup;
            _camera = viewCamera;
            _lineColor = lineColor;
            _targetGap = Mathf.Max(0f, targetGap);
            _popupGap = Mathf.Max(0f, popupGap);
            _viewportMargin = Mathf.Max(0f, viewportMargin);
            _curveStrength = Mathf.Max(0f, curveStrength);

            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.positionCount = SegmentCount + 1;
            _lineRenderer.widthMultiplier = Mathf.Max(0.0001f, lineWidth);
            _lineRenderer.alignment = LineAlignment.View;
            _lineRenderer.numCapVertices = 2;
            _lineRenderer.numCornerVertices = 2;
            _lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _lineRenderer.receiveShadows = false;
            _lineRenderer.lightProbeUsage = LightProbeUsage.Off;
            _lineRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _lineRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            if (curveMaterial != null) _lineRenderer.sharedMaterial = curveMaterial;

            SetLineAlpha(0f);
            _lineRenderer.enabled = false;
        }

        public void SetTarget(Transform target)
        {
            if (_target == target) return;

            _target = target;
            _targetRenderers = _target != null
                ? _target.GetComponentsInChildren<Renderer>(true)
                : null;
        }

        public void SetVisible(bool isVisible)
        {
            _requestedVisible = isVisible;
            if (!isVisible && _lineRenderer != null) _lineRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (_lineRenderer == null || _popupRect == null || _camera == null || _target == null)
            {
                if (_lineRenderer != null) _lineRenderer.enabled = false;
                return;
            }

            float alpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
            Vector3 targetPosition = GetTargetSurfacePoint(_popupRect.position);
            Vector3 viewportPosition = _camera.WorldToViewportPoint(targetPosition);
            bool targetIsInView = viewportPosition.z > _camera.nearClipPlane
                && viewportPosition.x >= -_viewportMargin
                && viewportPosition.x <= 1f + _viewportMargin
                && viewportPosition.y >= -_viewportMargin
                && viewportPosition.y <= 1f + _viewportMargin;

            bool shouldRender = _requestedVisible && alpha > 0.001f && targetIsInView;
            _lineRenderer.enabled = shouldRender;
            if (!shouldRender) return;

            Vector3 popupPoint = GetClosestPopupEdgePoint(targetPosition);
            Vector3 popupToTarget = targetPosition - popupPoint;
            if (popupToTarget.sqrMagnitude < 0.000001f)
            {
                _lineRenderer.enabled = false;
                return;
            }

            Vector3 direction = popupToTarget.normalized;
            float maximumGap = popupToTarget.magnitude * 0.4f;
            Vector3 start = popupPoint + direction * Mathf.Min(_popupGap, maximumGap);
            Vector3 end = targetPosition - direction * Mathf.Min(_targetGap, maximumGap);
            DrawCurve(start, end);
            SetLineAlpha(alpha);
        }

        private Vector3 GetClosestPopupEdgePoint(Vector3 targetPosition)
        {
            _popupRect.GetWorldCorners(_popupCorners);

            Vector3 bestPoint = _popupCorners[0];
            float bestDistance = float.PositiveInfinity;
            for (int index = 0; index < 4; index++)
            {
                Vector3 edgeStart = _popupCorners[index];
                Vector3 edgeEnd = _popupCorners[(index + 1) % 4];
                Vector3 candidate = ClosestPointOnSegment(edgeStart, edgeEnd, targetPosition);
                float distance = (candidate - targetPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestPoint = candidate;
            }

            return bestPoint;
        }

        private Vector3 GetTargetSurfacePoint(Vector3 popupPosition)
        {
            if (_target is RectTransform rectTransform)
                return GetClosestRectPoint(rectTransform, popupPosition);

            Vector3 bestPoint = _target.position;
            float bestDistance = (bestPoint - popupPosition).sqrMagnitude;
            if (_targetRenderers == null) return bestPoint;

            foreach (Renderer targetRenderer in _targetRenderers)
            {
                if (targetRenderer == null || !targetRenderer.gameObject.activeInHierarchy) continue;

                Vector3 candidate = targetRenderer.bounds.ClosestPoint(popupPosition);
                float distance = (candidate - popupPosition).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestPoint = candidate;
            }

            return bestPoint;
        }

        private static Vector3 GetClosestRectPoint(RectTransform rectTransform, Vector3 worldPoint)
        {
            Plane plane = new Plane(rectTransform.forward, rectTransform.position);
            Vector3 projected = plane.ClosestPointOnPlane(worldPoint);
            Vector3 local = rectTransform.InverseTransformPoint(projected);
            Rect rect = rectTransform.rect;
            local.x = Mathf.Clamp(local.x, rect.xMin, rect.xMax);
            local.y = Mathf.Clamp(local.y, rect.yMin, rect.yMax);
            local.z = 0f;
            return rectTransform.TransformPoint(local);
        }

        private void DrawCurve(Vector3 start, Vector3 end)
        {
            Vector3 delta = end - start;
            float distance = delta.magnitude;
            Vector3 planarDirection = Vector3.ProjectOnPlane(delta, _camera.transform.forward).normalized;
            if (planarDirection.sqrMagnitude < 0.0001f) planarDirection = _camera.transform.right;

            Vector3 bendDirection = Vector3.Cross(_camera.transform.forward, planarDirection).normalized;
            float bendSign = Vector3.Dot(bendDirection, _camera.transform.up) >= 0f ? 1f : -1f;
            Vector3 bend = bendDirection * bendSign * Mathf.Min(distance * _curveStrength, 0.25f);
            Vector3 control1 = start + delta * 0.3f + bend;
            Vector3 control2 = start + delta * 0.7f + bend;

            for (int index = 0; index <= SegmentCount; index++)
            {
                float t = index / (float)SegmentCount;
                _curvePoints[index] = CalculateCubicBezierPoint(t, start, control1, control2, end);
            }

            _lineRenderer.SetPositions(_curvePoints);
        }

        private void SetLineAlpha(float alpha)
        {
            if (Mathf.Abs(_lastAlpha - alpha) < 0.01f) return;
            _lastAlpha = alpha;

            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(_lineColor, 0f),
                    new GradientColorKey(_lineColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(alpha * 0.15f, 0f),
                    new GradientAlphaKey(alpha, 0.65f),
                    new GradientAlphaKey(alpha, 1f)
                });
            _lineRenderer.colorGradient = gradient;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 start, Vector3 end, Vector3 point)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon) return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }

        private static Vector3 CalculateCubicBezierPoint(
            float t,
            Vector3 start,
            Vector3 control1,
            Vector3 control2,
            Vector3 end)
        {
            float inverse = 1f - t;
            float inverseSquared = inverse * inverse;
            float tSquared = t * t;
            return inverseSquared * inverse * start
                + 3f * inverseSquared * t * control1
                + 3f * inverse * tSquared * control2
                + tSquared * t * end;
        }
    }
}

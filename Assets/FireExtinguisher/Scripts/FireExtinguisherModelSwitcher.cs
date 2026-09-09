using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace _Scripts.FireExtinguishers
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherModelSwitcher : MonoBehaviour
    {
        private const float FullyVisible = 0f;
        private const float FullyDissolved = 1f;

        [SerializeField] private FireExtinguisher _fireExtinguisher;
        [SerializeField] private Transform _unselectModel;
        [SerializeField] private Transform _co2Model;
        [SerializeField] private Transform _powderModel;

        [Header("Dissolve Transition")]
        [SerializeField] private Material _dissolveMaterial;
        [SerializeField, Min(0f)] private float _phaseDuration = 0.6f;

        [Header("Dissolve Particles")]
        [SerializeField] private ParticleSystem _dissolveParticles;
        [SerializeField, Min(0f)] private float _particleDissolveBand = 0.05f;

        private DissolveRendererMaterials _transitionMaterials;
        private Coroutine _transitionRoutine;
        private readonly List<ParticleMeshSurface> _particleMeshSurfaces = new();
        private readonly List<Renderer> _activeDissolveRenderers = new();
        private readonly HashSet<int> _cachedParticleRendererIds = new();
        private float _particleEmissionAccumulator;
        private bool _particlePrefabEmissionEnabled;
        private bool _hasParticlePrefabEmissionState;
        private FireExtinguisherType _visualType;
        private FireExtinguisherType _requestedType;

        public bool IsTransitioning { get; private set; }

        public event Action OnTransitionStarted;
        public event Action OnTransitionCompleted;

        private void OnEnable()
        {
            if (_fireExtinguisher == null) return;

            _transitionMaterials = new DissolveRendererMaterials(_dissolveMaterial);
            _visualType = _fireExtinguisher.ExtinguisherType;
            _requestedType = _visualType;
            SetModelImmediate(_visualType);

            _fireExtinguisher.OnTypeChanged += RequestModel;
        }

        private void OnDisable()
        {
            if (_fireExtinguisher != null)
                _fireExtinguisher.OnTypeChanged -= RequestModel;

            CancelTransition();

            if (_fireExtinguisher != null)
            {
                _visualType = _fireExtinguisher.ExtinguisherType;
                _requestedType = _visualType;
                SetModelImmediate(_visualType);
            }
        }

        private void RequestModel(FireExtinguisherType extinguisherType)
        {
            _requestedType = extinguisherType;
            if (IsTransitioning || _visualType == _requestedType) return;

            if (_dissolveMaterial == null || _phaseDuration <= 0f)
            {
                SetModelImmediate(_requestedType);
                _visualType = _requestedType;
                return;
            }

            IsTransitioning = true;
            OnTransitionStarted?.Invoke();
            _transitionRoutine = StartCoroutine(TransitionToRequestedModel());
        }

        private IEnumerator TransitionToRequestedModel()
        {
            while (_visualType != _requestedType)
            {
                Transform outgoingModel = GetModel(_visualType);
                if (outgoingModel != null)
                {
                    SetActive(outgoingModel, true);
                    if (_transitionMaterials.Apply(GetRenderers(outgoingModel), FullyVisible))
                        yield return AnimateDissolve(outgoingModel, FullyVisible, FullyDissolved);

                    _transitionMaterials.Restore();
                    SetActive(outgoingModel, false);
                }

                // Use the latest request at the midpoint so rapid input never reveals
                // a stale model before transitioning again.
                FireExtinguisherType incomingType = _requestedType;
                Transform incomingModel = GetModel(incomingType);
                _visualType = incomingType;

                if (incomingModel == null) continue;

                SetActive(incomingModel, true);
                if (_transitionMaterials.Apply(GetRenderers(incomingModel), FullyDissolved))
                    yield return AnimateDissolve(incomingModel, FullyDissolved, FullyVisible);

                _transitionMaterials.Restore();
            }

            _transitionRoutine = null;
            IsTransitioning = false;
            StopDissolveParticles(false);
            OnTransitionCompleted?.Invoke();
        }

        private IEnumerator AnimateDissolve(Transform model, float from, float to)
        {
            RefreshParticleMeshSurfaces(model);
            PlayDissolveParticles();
            UpdateDissolveParticlePosition(model, from);

            float elapsed = 0f;
            while (elapsed < _phaseDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(elapsed / _phaseDuration);
                float easedProgress = 0.5f - 0.5f * Mathf.Cos(Mathf.PI * progress);
                float amount = Mathf.LerpUnclamped(from, to, easedProgress);
                _transitionMaterials.SetAmount(amount);
                UpdateDissolveParticlePosition(model, amount);
                RefreshParticleMeshSurfaces(model);
                EmitParticlesFromMesh();
                yield return null;
            }

            _transitionMaterials.SetAmount(to);
            UpdateDissolveParticlePosition(model, to);
        }

        private void PlayDissolveParticles()
        {
            if (_dissolveParticles == null) return;

            ParticleSystem.EmissionModule emission = _dissolveParticles.emission;
            if (!_hasParticlePrefabEmissionState)
            {
                _particlePrefabEmissionEnabled = emission.enabled;
                _hasParticlePrefabEmissionState = true;
            }

            emission.enabled = false;

            if (_dissolveParticles.isPlaying) return;

            _particleEmissionAccumulator = 0f;
            _dissolveParticles.Play(true);
        }

        private void StopDissolveParticles(bool clear)
        {
            if (_dissolveParticles == null) return;

            _dissolveParticles.Stop(
                true,
                clear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);

            if (_hasParticlePrefabEmissionState)
            {
                ParticleSystem.EmissionModule emission = _dissolveParticles.emission;
                emission.enabled = _particlePrefabEmissionEnabled;
            }

            _particleMeshSurfaces.Clear();
            _activeDissolveRenderers.Clear();
            _cachedParticleRendererIds.Clear();
            _particleEmissionAccumulator = 0f;
        }

        private void UpdateDissolveParticlePosition(Transform model, float amount)
        {
            if (_dissolveParticles == null || !TryGetWorldBounds(model, out Bounds modelBounds)) return;

            float minWorldY = modelBounds.min.y;
            float maxWorldY = modelBounds.max.y;
            if (DissolveRendererMaterials.TryGetSharedWorldYBounds(
                    out float sharedMinWorldY,
                    out float sharedMaxWorldY))
            {
                minWorldY = sharedMinWorldY;
                maxWorldY = sharedMaxWorldY;
            }

            Vector3 position = modelBounds.center;
            position.y = Mathf.Lerp(minWorldY, maxWorldY, Mathf.Clamp01(amount));
            _dissolveParticles.transform.position = position;
        }

        private void RefreshParticleMeshSurfaces(Transform fallbackModel)
        {
            if (_dissolveParticles == null) return;

            DissolveRendererMaterials.GetActiveRenderers(_activeDissolveRenderers);
            Transform extinguisherRoot = _fireExtinguisher != null ? _fireExtinguisher.transform : transform;

            for (int index = _activeDissolveRenderers.Count - 1; index >= 0; index--)
            {
                Renderer renderer = _activeDissolveRenderers[index];
                if (renderer == null || !renderer.transform.IsChildOf(extinguisherRoot))
                    _activeDissolveRenderers.RemoveAt(index);
            }

            if (_activeDissolveRenderers.Count == 0 && fallbackModel != null)
                _activeDissolveRenderers.AddRange(GetRenderers(fallbackModel));

            bool renderersChanged = _cachedParticleRendererIds.Count != _activeDissolveRenderers.Count;
            if (!renderersChanged)
            {
                for (int index = 0; index < _activeDissolveRenderers.Count; index++)
                {
                    if (_cachedParticleRendererIds.Contains(_activeDissolveRenderers[index].GetInstanceID())) continue;

                    renderersChanged = true;
                    break;
                }
            }

            if (!renderersChanged) return;

            _particleMeshSurfaces.Clear();
            _cachedParticleRendererIds.Clear();

            for (int index = 0; index < _activeDissolveRenderers.Count; index++)
            {
                Renderer renderer = _activeDissolveRenderers[index];
                _cachedParticleRendererIds.Add(renderer.GetInstanceID());

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    Mesh bakedMesh = new Mesh { name = "Dissolve Particle Mesh (Runtime)" };
                    skinnedMeshRenderer.BakeMesh(bakedMesh);
                    AddParticleMeshSurface(renderer, skinnedMeshRenderer.transform, bakedMesh);
                    Destroy(bakedMesh);
                    continue;
                }

                if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter meshFilter))
                    AddParticleMeshSurface(renderer, meshFilter.transform, meshFilter.sharedMesh);
            }
        }

        private void AddParticleMeshSurface(Renderer renderer, Transform meshTransform, Mesh mesh)
        {
            if (renderer == null || meshTransform == null || mesh == null || !mesh.isReadable) return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length < 3) return;

            _particleMeshSurfaces.Add(new ParticleMeshSurface(renderer, meshTransform, vertices, triangles));
        }

        private void EmitParticlesFromMesh()
        {
            if (_dissolveParticles == null || _particleMeshSurfaces.Count == 0) return;

            ParticleSystem.MainModule main = _dissolveParticles.main;
            float normalizedTime = main.duration > 0f ? _dissolveParticles.time / main.duration : 0f;
            float emissionRate = _dissolveParticles.emission.rateOverTime.Evaluate(
                normalizedTime,
                UnityEngine.Random.value);
            _particleEmissionAccumulator += Mathf.Max(0f, emissionRate) * Time.unscaledDeltaTime;
            int particleCount = Mathf.Min(Mathf.FloorToInt(_particleEmissionAccumulator), 20);
            if (particleCount <= 0) return;

            _particleEmissionAccumulator -= particleCount;
            float dissolveWorldY = _dissolveParticles.transform.position.y;

            for (int index = 0; index < particleCount; index++)
            {
                float sampleWorldY = dissolveWorldY + UnityEngine.Random.Range(
                    -_particleDissolveBand,
                    _particleDissolveBand);
                if (!TryGetParticleMeshPoint(sampleWorldY, out Vector3 position)) continue;

                ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = UnityEngine.Random.onUnitSphere * main.startSpeed.Evaluate(
                        normalizedTime,
                        UnityEngine.Random.value)
                };
                _dissolveParticles.Emit(emitParams, 1);
            }
        }

        private bool TryGetParticleMeshPoint(float worldY, out Vector3 position)
        {
            int surfaceCount = _particleMeshSurfaces.Count;
            int startIndex = UnityEngine.Random.Range(0, surfaceCount);

            for (int offset = 0; offset < surfaceCount; offset++)
            {
                ParticleMeshSurface surface = _particleMeshSurfaces[(startIndex + offset) % surfaceCount];
                if (surface.TryGetPointAtWorldY(worldY, out position)) return true;
            }

            position = default;
            return false;
        }

        private void SetModelImmediate(FireExtinguisherType extinguisherType)
        {
            _transitionMaterials?.Restore();
            SetActive(_unselectModel, extinguisherType == FireExtinguisherType.Unselect);
            SetActive(_co2Model, extinguisherType == FireExtinguisherType.CO2);
            SetActive(_powderModel, extinguisherType == FireExtinguisherType.Powder);
        }

        private Transform GetModel(FireExtinguisherType extinguisherType)
        {
            return extinguisherType switch
            {
                FireExtinguisherType.CO2 => _co2Model,
                FireExtinguisherType.Powder => _powderModel,
                _ => _unselectModel
            };
        }

        private void CancelTransition()
        {
            bool wasTransitioning = IsTransitioning;
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
            }

            _transitionMaterials?.Restore();
            StopDissolveParticles(true);
            IsTransitioning = false;
            if (wasTransitioning) OnTransitionCompleted?.Invoke();
        }

        private static IEnumerable<Renderer> GetRenderers(Transform model)
        {
            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    yield return renderer;
            }
        }

        private static bool TryGetWorldBounds(Transform model, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in GetRenderers(model))
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return hasBounds;
        }

        private sealed class ParticleMeshSurface
        {
            private const float PlaneEpsilon = 0.0001f;

            private readonly Renderer _renderer;
            private readonly Transform _transform;
            private readonly Vector3[] _vertices;
            private readonly int[] _triangles;

            public ParticleMeshSurface(
                Renderer renderer,
                Transform meshTransform,
                Vector3[] vertices,
                int[] triangles)
            {
                _renderer = renderer;
                _transform = meshTransform;
                _vertices = vertices;
                _triangles = triangles;
            }

            public bool TryGetPointAtWorldY(float worldY, out Vector3 position)
            {
                position = default;
                if (_renderer == null || _transform == null) return false;

                Bounds bounds = _renderer.bounds;
                if (worldY < bounds.min.y - PlaneEpsilon || worldY > bounds.max.y + PlaneEpsilon)
                    return false;

                int triangleCount = _triangles.Length / 3;
                int startTriangle = UnityEngine.Random.Range(0, triangleCount);

                for (int offset = 0; offset < triangleCount; offset++)
                {
                    int triangleIndex = (startTriangle + offset) % triangleCount * 3;
                    Vector3 a = _transform.TransformPoint(_vertices[_triangles[triangleIndex]]);
                    Vector3 b = _transform.TransformPoint(_vertices[_triangles[triangleIndex + 1]]);
                    Vector3 c = _transform.TransformPoint(_vertices[_triangles[triangleIndex + 2]]);

                    if (TryIntersectTriangleWithWorldY(a, b, c, worldY, out position))
                        return true;
                }

                return false;
            }

            private static bool TryIntersectTriangleWithWorldY(
                Vector3 a,
                Vector3 b,
                Vector3 c,
                float worldY,
                out Vector3 position)
            {
                float aDistance = a.y - worldY;
                float bDistance = b.y - worldY;
                float cDistance = c.y - worldY;

                if (Mathf.Abs(aDistance) <= PlaneEpsilon
                    && Mathf.Abs(bDistance) <= PlaneEpsilon
                    && Mathf.Abs(cDistance) <= PlaneEpsilon)
                {
                    float first = Mathf.Sqrt(UnityEngine.Random.value);
                    float second = UnityEngine.Random.value;
                    position = (1f - first) * a
                               + first * (1f - second) * b
                               + first * second * c;
                    return true;
                }

                Vector3 firstPoint = default;
                Vector3 secondPoint = default;
                int pointCount = 0;

                AddEdgeIntersection(a, b, aDistance, bDistance, ref firstPoint, ref secondPoint, ref pointCount);
                AddEdgeIntersection(b, c, bDistance, cDistance, ref firstPoint, ref secondPoint, ref pointCount);
                AddEdgeIntersection(c, a, cDistance, aDistance, ref firstPoint, ref secondPoint, ref pointCount);

                if (pointCount == 0)
                {
                    position = default;
                    return false;
                }

                position = pointCount == 1
                    ? firstPoint
                    : Vector3.Lerp(firstPoint, secondPoint, UnityEngine.Random.value);
                return true;
            }

            private static void AddEdgeIntersection(
                Vector3 from,
                Vector3 to,
                float fromDistance,
                float toDistance,
                ref Vector3 firstPoint,
                ref Vector3 secondPoint,
                ref int pointCount)
            {
                bool fromOnPlane = Mathf.Abs(fromDistance) <= PlaneEpsilon;
                bool toOnPlane = Mathf.Abs(toDistance) <= PlaneEpsilon;

                if (fromOnPlane) AddUniquePoint(from, ref firstPoint, ref secondPoint, ref pointCount);
                if (toOnPlane) AddUniquePoint(to, ref firstPoint, ref secondPoint, ref pointCount);

                if (fromOnPlane || toOnPlane || Mathf.Sign(fromDistance) == Mathf.Sign(toDistance)) return;

                float interpolation = fromDistance / (fromDistance - toDistance);
                AddUniquePoint(
                    Vector3.LerpUnclamped(from, to, interpolation),
                    ref firstPoint,
                    ref secondPoint,
                    ref pointCount);
            }

            private static void AddUniquePoint(
                Vector3 point,
                ref Vector3 firstPoint,
                ref Vector3 secondPoint,
                ref int pointCount)
            {
                if (pointCount == 0)
                {
                    firstPoint = point;
                    pointCount = 1;
                    return;
                }

                if ((firstPoint - point).sqrMagnitude <= PlaneEpsilon * PlaneEpsilon || pointCount >= 2) return;

                secondPoint = point;
                pointCount = 2;
            }
        }

        private static void SetActive(Transform model, bool isActive)
        {
            if (model != null && model.gameObject.activeSelf != isActive)
                model.gameObject.SetActive(isActive);
        }
    }
}

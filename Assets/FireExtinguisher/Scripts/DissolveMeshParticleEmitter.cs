using System.Collections.Generic;
using UnityEngine;

namespace _Scripts.FireExtinguishers
{
    /// <summary>
    /// Emits dissolve particles on the actual mesh/plane intersection instead of
    /// from the particle prefab's point/line shape.
    /// </summary>
    internal sealed class DissolveMeshParticleEmitter
    {
        private readonly ParticleSystem _particles;
        private readonly float _dissolveBand;
        private readonly List<ParticleMeshSurface> _surfaces = new();
        private float _emissionAccumulator;
        private bool _prefabEmissionEnabled;
        private bool _hasPrefabEmissionState;

        public DissolveMeshParticleEmitter(ParticleSystem particles, float dissolveBand)
        {
            _particles = particles;
            _dissolveBand = Mathf.Max(0f, dissolveBand);
        }

        public void Begin(IEnumerable<Renderer> renderers)
        {
            if (_particles == null) return;

            CacheSurfaces(renderers);
            ParticleSystem.EmissionModule emission = _particles.emission;
            if (!_hasPrefabEmissionState)
            {
                _prefabEmissionEnabled = emission.enabled;
                _hasPrefabEmissionState = true;
            }

            // Manual surface emission replaces the prefab's shape emission.
            emission.enabled = false;
            _emissionAccumulator = 0f;
            _particles.gameObject.SetActive(true);
            _particles.Play(true);
        }

        public void Update(float dissolveWorldY)
        {
            if (_particles == null || _surfaces.Count == 0) return;

            Vector3 emitterPosition = _particles.transform.position;
            emitterPosition.y = dissolveWorldY;
            _particles.transform.position = emitterPosition;

            ParticleSystem.MainModule main = _particles.main;
            float normalizedTime = main.duration > 0f ? _particles.time / main.duration : 0f;
            float emissionRate = _particles.emission.rateOverTime.Evaluate(
                normalizedTime,
                Random.value);
            _emissionAccumulator += Mathf.Max(0f, emissionRate) * Time.unscaledDeltaTime;
            int particleCount = Mathf.Min(Mathf.FloorToInt(_emissionAccumulator), 20);
            if (particleCount <= 0) return;

            _emissionAccumulator -= particleCount;
            for (int index = 0; index < particleCount; index++)
            {
                float sampleWorldY = dissolveWorldY + Random.Range(-_dissolveBand, _dissolveBand);
                if (!TryGetSurfacePoint(sampleWorldY, out Vector3 position)) continue;

                ParticleSystem.EmitParams emitParams = new()
                {
                    position = position,
                    velocity = Random.onUnitSphere * main.startSpeed.Evaluate(normalizedTime, Random.value)
                };
                _particles.Emit(emitParams, 1);
            }
        }

        public void Stop(bool clear)
        {
            if (_particles == null) return;

            _particles.Stop(
                true,
                clear
                    ? ParticleSystemStopBehavior.StopEmittingAndClear
                    : ParticleSystemStopBehavior.StopEmitting);

            if (_hasPrefabEmissionState)
            {
                ParticleSystem.EmissionModule emission = _particles.emission;
                emission.enabled = _prefabEmissionEnabled;
            }

            _surfaces.Clear();
            _emissionAccumulator = 0f;
        }

        private void CacheSurfaces(IEnumerable<Renderer> renderers)
        {
            _surfaces.Clear();
            if (renderers == null) return;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null) continue;

                if (renderer is SkinnedMeshRenderer skinnedMeshRenderer)
                {
                    Mesh bakedMesh = new() { name = "Dissolve Particle Mesh (Runtime)" };
                    skinnedMeshRenderer.BakeMesh(bakedMesh);
                    AddSurface(renderer, skinnedMeshRenderer.transform, bakedMesh);
                    Object.Destroy(bakedMesh);
                }
                else if (renderer is MeshRenderer && renderer.TryGetComponent(out MeshFilter meshFilter))
                {
                    AddSurface(renderer, meshFilter.transform, meshFilter.sharedMesh);
                }
            }
        }

        private void AddSurface(Renderer renderer, Transform meshTransform, Mesh mesh)
        {
            if (mesh == null || !mesh.isReadable) return;

            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            if (vertices.Length == 0 || triangles.Length < 3) return;
            _surfaces.Add(new ParticleMeshSurface(renderer, meshTransform, vertices, triangles));
        }

        private bool TryGetSurfacePoint(float worldY, out Vector3 position)
        {
            int count = _surfaces.Count;
            int startIndex = Random.Range(0, count);
            for (int offset = 0; offset < count; offset++)
            {
                if (_surfaces[(startIndex + offset) % count].TryGetPointAtWorldY(worldY, out position))
                    return true;
            }

            position = default;
            return false;
        }

        private sealed class ParticleMeshSurface
        {
            private const float PlaneEpsilon = 0.0001f;

            private readonly Renderer _renderer;
            private readonly Transform _transform;
            private readonly Vector3[] _vertices;
            private readonly int[] _triangles;

            public ParticleMeshSurface(Renderer renderer, Transform meshTransform, Vector3[] vertices, int[] triangles)
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
                int startTriangle = Random.Range(0, triangleCount);
                for (int offset = 0; offset < triangleCount; offset++)
                {
                    int triangleIndex = (startTriangle + offset) % triangleCount * 3;
                    Vector3 a = _transform.TransformPoint(_vertices[_triangles[triangleIndex]]);
                    Vector3 b = _transform.TransformPoint(_vertices[_triangles[triangleIndex + 1]]);
                    Vector3 c = _transform.TransformPoint(_vertices[_triangles[triangleIndex + 2]]);
                    if (TryIntersectTriangle(a, b, c, worldY, out position)) return true;
                }
                return false;
            }

            private static bool TryIntersectTriangle(Vector3 a, Vector3 b, Vector3 c, float worldY, out Vector3 position)
            {
                float aDistance = a.y - worldY;
                float bDistance = b.y - worldY;
                float cDistance = c.y - worldY;

                if (Mathf.Abs(aDistance) <= PlaneEpsilon
                    && Mathf.Abs(bDistance) <= PlaneEpsilon
                    && Mathf.Abs(cDistance) <= PlaneEpsilon)
                {
                    float first = Mathf.Sqrt(Random.value);
                    float second = Random.value;
                    position = (1f - first) * a + first * (1f - second) * b + first * second * c;
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
                    : Vector3.Lerp(firstPoint, secondPoint, Random.value);
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
                AddUniquePoint(Vector3.LerpUnclamped(from, to, interpolation), ref firstPoint, ref secondPoint, ref pointCount);
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
    }
}

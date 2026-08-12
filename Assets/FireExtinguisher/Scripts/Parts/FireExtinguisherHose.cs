using UnityEngine;

namespace _Scripts.FireExtinguishers.Visualizes
{
    [DisallowMultipleComponent]
    public sealed class FireExtinguisherHose : MonoBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        [Header("References")]
        [SerializeField] private Transform _startAnchor;
        [SerializeField] private Transform _endAnchor;
        [SerializeField] private SkinnedMeshRenderer _hoseRenderer;
        [SerializeField] private Vector3 _endAnchorLocalOffset;

        [Header("Endpoint Axes")]
        [Tooltip("Local axis that points from the body into the hose.")]
        [SerializeField] private Vector3 _startTangentAxis = Vector3.right;
        [Tooltip("Local axis that points from the hose into the nozzle connector.")]
        [SerializeField] private Vector3 _endTangentAxis = Vector3.right;
        [SerializeField] private Vector3 _startUpAxis = Vector3.up;
        [SerializeField] private Vector3 _endUpAxis = Vector3.up;

        [Header("Shape")]
        [Tooltip("Hose length before it starts stretching. Set to 0 to derive it from the bind pose.")]
        [SerializeField, Min(0f)] private float _restLength = 0.325f;
        [Tooltip("Length of each endpoint tangent relative to the hose rest length.")]
        [SerializeField, Range(0f, 1f)] private float _tangentLengthRatio = 0.25f;
        [Tooltip("World-space direction in which spare hose length sags.")]
        [SerializeField] private Vector3 _sagDirection = Vector3.down;
        [Tooltip("Scales the sag needed to use the spare hose length. 1 keeps the requested length as closely as possible.")]
        [SerializeField, Min(0f)] private float _sagStrength = 1f;

        [Header("Motion")]
        [Tooltip("Smooths changes to the hose shape while keeping both endpoints attached.")]
        [SerializeField, Min(0f)] private float _shapeSmoothTime = 0.06f;

        [Header("Smooth Skin")]
        [Tooltip("Creates a denser runtime rig and reweights a cloned mesh. The source FBX is not modified.")]
        [SerializeField] private bool _rebuildSmoothSkin = true;
        [Tooltip("2 doubles the original segment count. An extra terminal bone is always added at the nozzle.")]
        [SerializeField, Range(1, 4)] private int _boneSubdivisions = 2;

        [Header("Quality")]
        [SerializeField, Range(12, 128)] private int _curveSamples = 48;
        [SerializeField, Range(4, 16)] private int _lengthSolveIterations = 10;

        private Transform[] _bones;
        private Quaternion[] _boneBindOffsets;
        private Transform _runtimeRigRoot;
        private Mesh _runtimeMesh;
        private Mesh _sourceMesh;
        private Transform[] _sourceBones;
        private Transform _sourceRootBone;
        private float[] _arcParameters;
        private float[] _arcLengths;

        private Vector3 _startTangentLocal;
        private Vector3 _endTangentLocal;
        private Vector3 _startNormalLocal;
        private Vector3 _endNormalLocal;

        private Vector3 _currentStartDerivative;
        private Vector3 _currentEndDerivative;
        private Vector3 _currentStartNormal;
        private Vector3 _currentEndNormal;

        private Vector3 _curveStart;
        private Vector3 _curveEnd;
        private Vector3 _curveStartDerivative;
        private Vector3 _curveEndDerivative;
        private Vector3 _curveSagDirection;
        private float _curveSagAmount;
        private float _currentCurveLength;
        private float _bindLength;
        private float _effectiveRestLength;
        private bool _bonesIncludeEndpoint;
        private bool _initialized;

        public float RestLength => _effectiveRestLength;
        public float CurrentCurveLength => _currentCurveLength;
        public int BoneCount => _bones != null ? _bones.Length : 0;
        public bool IsStretching => _initialized && Vector3.Distance(_curveStart, _curveEnd) > _effectiveRestLength;

        private void Reset()
        {
            _hoseRenderer = GetComponentInParent<SkinnedMeshRenderer>();
            if (_hoseRenderer == null)
            {
                Transform hose = transform.parent != null ? transform.parent.Find("Hose") : null;
                if (hose != null) _hoseRenderer = hose.GetComponent<SkinnedMeshRenderer>();
            }
        }

        private void OnEnable()
        {
            TryInitialize();
        }

        private void LateUpdate()
        {
            if (!TryInitialize()) return;

            UpdateCurve(Time.deltaTime);
            PoseBones();
        }

        private void OnDestroy()
        {
            RestoreSourceRig();
        }

        private void OnValidate()
        {
            _restLength = Mathf.Max(0f, _restLength);
            _shapeSmoothTime = Mathf.Max(0f, _shapeSmoothTime);
            _boneSubdivisions = Mathf.Clamp(_boneSubdivisions, 1, 4);
            _curveSamples = Mathf.Clamp(_curveSamples, 12, 128);
            _lengthSolveIterations = Mathf.Clamp(_lengthSolveIterations, 4, 16);

        }

        private bool TryInitialize()
        {
            if (_initialized) return true;
            if (_startAnchor == null || _endAnchor == null || _hoseRenderer == null) return false;

            if (_rebuildSmoothSkin && !BuildSmoothRuntimeRig()) return false;

            _bones = _hoseRenderer.bones;
            if (_bones == null || _bones.Length < 2) return false;

            _boneBindOffsets = new Quaternion[_bones.Length];
            EnsureArcLengthTables();

            Vector3 startDirection = GetBindDirection(0);
            Vector3 endDirection = GetBindDirection(_bones.Length - 1);
            Vector3 startNormal = GetSafeNormal(_bones[0].forward, startDirection);
            Vector3 endNormal = GetSafeNormal(_bones[_bones.Length - 1].forward, endDirection);

            _startTangentLocal = GetSafeAxis(_startTangentAxis, Vector3.right);
            _endTangentLocal = GetSafeAxis(_endTangentAxis, Vector3.right);
            _startNormalLocal = GetSafeNormal(_startUpAxis, _startTangentLocal);
            _endNormalLocal = GetSafeNormal(_endUpAxis, _endTangentLocal);
            startNormal = _startAnchor.TransformDirection(_startNormalLocal).normalized;
            endNormal = _endAnchor.TransformDirection(_endNormalLocal).normalized;

            for (int i = 0; i < _bones.Length; i++)
            {
                float normalizedDistance = GetBoneNormalizedDistance(i);
                Vector3 bindDirection = GetBindDirection(i);
                Vector3 bindNormal = InterpolateNormal(startNormal, endNormal, bindDirection, normalizedDistance);
                Quaternion bindFrame = Quaternion.LookRotation(bindDirection, bindNormal);
                _boneBindOffsets[i] = Quaternion.Inverse(bindFrame) * _bones[i].rotation;
            }

            _bindLength = EstimateBindLength();
            _effectiveRestLength = _restLength > 0f ? _restLength : _bindLength;
            _effectiveRestLength = Mathf.Max(_effectiveRestLength, 0.001f);
            _hoseRenderer.updateWhenOffscreen = true;

            InitializeSmoothedShape();
            UpdateCurve(0f);
            PoseBones();
            _initialized = true;
            return true;
        }

        private void InitializeSmoothedShape()
        {
            Vector3 endPosition = _endAnchor.TransformPoint(_endAnchorLocalOffset);
            float endpointDistance = Vector3.Distance(_startAnchor.position, endPosition);
            float targetLength = Mathf.Max(_effectiveRestLength, endpointDistance);
            float tangentLength = GetTangentLength(targetLength);

            _currentStartDerivative = _startAnchor.TransformDirection(_startTangentLocal).normalized * tangentLength;
            _currentEndDerivative = _endAnchor.TransformDirection(_endTangentLocal).normalized * tangentLength;
            _currentStartNormal = _startAnchor.TransformDirection(_startNormalLocal).normalized;
            _currentEndNormal = _endAnchor.TransformDirection(_endNormalLocal).normalized;
        }

        private void UpdateCurve(float deltaTime)
        {
            EnsureArcLengthTables();
            _effectiveRestLength = Mathf.Max(_restLength > 0f ? _restLength : _bindLength, 0.001f);
            _curveStart = _startAnchor.position;
            _curveEnd = _endAnchor.TransformPoint(_endAnchorLocalOffset);

            float endpointDistance = Vector3.Distance(_curveStart, _curveEnd);
            float targetLength = Mathf.Max(_effectiveRestLength, endpointDistance);
            float tangentLength = GetTangentLength(targetLength);

            Vector3 targetStartDerivative = _startAnchor.TransformDirection(_startTangentLocal).normalized * tangentLength;
            Vector3 targetEndDerivative = _endAnchor.TransformDirection(_endTangentLocal).normalized * tangentLength;
            Vector3 targetStartNormal = _startAnchor.TransformDirection(_startNormalLocal).normalized;
            Vector3 targetEndNormal = _endAnchor.TransformDirection(_endNormalLocal).normalized;

            float blend = GetDampingBlend(deltaTime, _shapeSmoothTime);
            _currentStartDerivative = Vector3.Lerp(_currentStartDerivative, targetStartDerivative, blend);
            _currentEndDerivative = Vector3.Lerp(_currentEndDerivative, targetEndDerivative, blend);
            _currentStartNormal = Vector3.Slerp(_currentStartNormal, targetStartNormal, blend).normalized;
            _currentEndNormal = Vector3.Slerp(_currentEndNormal, targetEndNormal, blend).normalized;

            _curveStartDerivative = _currentStartDerivative;
            _curveEndDerivative = _currentEndDerivative;
            _curveSagDirection = _sagDirection.sqrMagnitude > MinimumDirectionSqrMagnitude
                ? _sagDirection.normalized
                : Vector3.down;

            _curveSagAmount = SolveSagAmount(targetLength) * _sagStrength;
            BuildArcLengthTable(_curveSagAmount);
            _currentCurveLength = _arcLengths[_arcLengths.Length - 1];
        }

        private float SolveSagAmount(float targetLength)
        {
            float baseLength = MeasureCurveLength(0f);
            if (_sagStrength <= 0f || baseLength >= targetLength - 0.0001f) return 0f;

            float lower = 0f;
            float upper = Mathf.Max(targetLength - Vector3.Distance(_curveStart, _curveEnd), 0.01f);
            float upperLength = MeasureCurveLength(upper);

            for (int i = 0; i < 8 && upperLength < targetLength; i++)
            {
                upper *= 2f;
                upperLength = MeasureCurveLength(upper);
            }

            for (int i = 0; i < _lengthSolveIterations; i++)
            {
                float middle = (lower + upper) * 0.5f;
                if (MeasureCurveLength(middle) < targetLength) lower = middle;
                else upper = middle;
            }

            return (lower + upper) * 0.5f;
        }

        private float MeasureCurveLength(float sagAmount)
        {
            Vector3 previous = EvaluatePoint(0f, sagAmount);
            float length = 0f;

            for (int i = 1; i <= _curveSamples; i++)
            {
                float t = (float)i / _curveSamples;
                Vector3 point = EvaluatePoint(t, sagAmount);
                length += Vector3.Distance(previous, point);
                previous = point;
            }

            return length;
        }

        private void BuildArcLengthTable(float sagAmount)
        {
            _arcParameters[0] = 0f;
            _arcLengths[0] = 0f;
            Vector3 previous = EvaluatePoint(0f, sagAmount);

            for (int i = 1; i <= _curveSamples; i++)
            {
                float t = (float)i / _curveSamples;
                Vector3 point = EvaluatePoint(t, sagAmount);
                _arcParameters[i] = t;
                _arcLengths[i] = _arcLengths[i - 1] + Vector3.Distance(previous, point);
                previous = point;
            }
        }

        private void PoseBones()
        {
            if (_currentCurveLength <= 0f) return;

            for (int i = 0; i < _bones.Length; i++)
            {
                float normalizedDistance = GetBoneNormalizedDistance(i);
                float t = GetParameterAtDistance(_currentCurveLength * normalizedDistance);
                Vector3 position = EvaluatePoint(t, _curveSagAmount);
                Vector3 tangent = EvaluateDerivative(t, _curveSagAmount);
                if (tangent.sqrMagnitude <= MinimumDirectionSqrMagnitude) tangent = _curveEnd - _curveStart;
                tangent.Normalize();

                Vector3 normal = InterpolateNormal(
                    _currentStartNormal,
                    _currentEndNormal,
                    tangent,
                    normalizedDistance);
                Quaternion frame = Quaternion.LookRotation(tangent, normal);

                _bones[i].SetPositionAndRotation(position, frame * _boneBindOffsets[i]);
            }
        }

        private float GetParameterAtDistance(float distance)
        {
            int low = 0;
            int high = _arcLengths.Length - 1;

            while (low < high)
            {
                int middle = (low + high) / 2;
                if (_arcLengths[middle] < distance) low = middle + 1;
                else high = middle;
            }

            int upperIndex = Mathf.Clamp(low, 1, _arcLengths.Length - 1);
            int lowerIndex = upperIndex - 1;
            float segmentLength = _arcLengths[upperIndex] - _arcLengths[lowerIndex];
            float segmentProgress = segmentLength > 0.000001f
                ? (distance - _arcLengths[lowerIndex]) / segmentLength
                : 0f;

            return Mathf.Lerp(_arcParameters[lowerIndex], _arcParameters[upperIndex], segmentProgress);
        }

        private Vector3 EvaluatePoint(float t, float sagAmount)
        {
            Vector3 control0 = _curveStart;
            Vector3 control1 = _curveStart + _curveStartDerivative;
            Vector3 control2 = (_curveStart + _curveEnd) * 0.5f + _curveSagDirection * sagAmount;
            Vector3 control3 = _curveEnd - _curveEndDerivative;
            Vector3 control4 = _curveEnd;

            float oneMinusT = 1f - t;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float oneMinusT3 = oneMinusT2 * oneMinusT;
            float oneMinusT4 = oneMinusT3 * oneMinusT;
            float t2 = t * t;
            float t3 = t2 * t;
            float t4 = t3 * t;

            return oneMinusT4 * control0
                   + 4f * oneMinusT3 * t * control1
                   + 6f * oneMinusT2 * t2 * control2
                   + 4f * oneMinusT * t3 * control3
                   + t4 * control4;
        }

        private Vector3 EvaluateDerivative(float t, float sagAmount)
        {
            Vector3 control0 = _curveStart;
            Vector3 control1 = _curveStart + _curveStartDerivative;
            Vector3 control2 = (_curveStart + _curveEnd) * 0.5f + _curveSagDirection * sagAmount;
            Vector3 control3 = _curveEnd - _curveEndDerivative;
            Vector3 control4 = _curveEnd;

            float oneMinusT = 1f - t;
            float oneMinusT2 = oneMinusT * oneMinusT;
            float t2 = t * t;

            return 4f * (
                oneMinusT2 * oneMinusT * (control1 - control0)
                + 3f * oneMinusT2 * t * (control2 - control1)
                + 3f * oneMinusT * t2 * (control3 - control2)
                + t2 * t * (control4 - control3));
        }

        private bool BuildSmoothRuntimeRig()
        {
            if (_runtimeRigRoot != null) return true;

            _sourceBones = _hoseRenderer.bones;
            _sourceMesh = _hoseRenderer.sharedMesh;
            _sourceRootBone = _hoseRenderer.rootBone;
            if (_sourceMesh == null || _sourceBones == null || _sourceBones.Length < 2) return false;

            int sourceBoneCount = _sourceBones.Length;
            Vector3[] sourcePoints = new Vector3[sourceBoneCount + 1];
            float[] sourceDistances = new float[sourcePoints.Length];
            float sourceLength = 0f;

            for (int i = 0; i < sourceBoneCount; i++)
            {
                sourcePoints[i] = _sourceBones[i].position;
                if (i == 0) continue;

                sourceLength += Vector3.Distance(sourcePoints[i - 1], sourcePoints[i]);
                sourceDistances[i] = sourceLength;
            }

            float averageSegmentLength = sourceLength / (sourceBoneCount - 1);
            Vector3 finalDirection = sourcePoints[sourceBoneCount - 1] - sourcePoints[sourceBoneCount - 2];
            if (finalDirection.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                finalDirection = _sourceBones[sourceBoneCount - 1].up;
            sourcePoints[sourceBoneCount] = sourcePoints[sourceBoneCount - 1]
                                            + finalDirection.normalized * averageSegmentLength;
            sourceLength += averageSegmentLength;
            sourceDistances[sourceBoneCount] = sourceLength;

            int runtimeBoneCount = sourceBoneCount * _boneSubdivisions + 1;
            Transform[] runtimeBones = new Transform[runtimeBoneCount];
            GameObject rigObject = new GameObject($"Hose Runtime Rig ({runtimeBoneCount} Bones)");
            rigObject.hideFlags = HideFlags.DontSave;
            _runtimeRigRoot = rigObject.transform;
            _runtimeRigRoot.SetParent(transform, false);

            for (int i = 0; i < runtimeBoneCount; i++)
            {
                float normalizedDistance = (float)i / (runtimeBoneCount - 1);
                float distance = sourceLength * normalizedDistance;
                Vector3 position = SampleSourcePosition(sourcePoints, sourceDistances, distance);

                float sourceCoordinate = normalizedDistance * sourceBoneCount;
                int lowerSourceIndex = Mathf.Min(Mathf.FloorToInt(sourceCoordinate), sourceBoneCount - 1);
                int upperSourceIndex = Mathf.Min(lowerSourceIndex + 1, sourceBoneCount - 1);
                float sourceBlend = Mathf.Clamp01(sourceCoordinate - lowerSourceIndex);
                Quaternion rotation = Quaternion.Slerp(
                    _sourceBones[lowerSourceIndex].rotation,
                    _sourceBones[upperSourceIndex].rotation,
                    sourceBlend);

                GameObject boneObject = new GameObject($"Hose Bone {i:00}");
                boneObject.hideFlags = HideFlags.DontSave;
                Transform bone = boneObject.transform;
                bone.SetParent(_runtimeRigRoot, false);
                bone.SetPositionAndRotation(position, rotation);
                runtimeBones[i] = bone;
            }

            _runtimeMesh = Instantiate(_sourceMesh);
            _runtimeMesh.name = $"{_sourceMesh.name} (Smooth Runtime Skin)";
            _runtimeMesh.hideFlags = HideFlags.DontSave;
            _runtimeMesh.MarkDynamic();

            Vector3 bindStart = _hoseRenderer.transform.InverseTransformPoint(sourcePoints[0]);
            Vector3 bindEnd = _hoseRenderer.transform.InverseTransformPoint(sourcePoints[sourceBoneCount]);
            Vector3 bindAxis = bindEnd - bindStart;
            float bindAxisLengthSqr = bindAxis.sqrMagnitude;
            if (bindAxisLengthSqr <= MinimumDirectionSqrMagnitude)
            {
                RestoreSourceRig();
                return false;
            }

            Vector3[] vertices = _runtimeMesh.vertices;
            BoneWeight[] weights = new BoneWeight[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                float normalizedDistance = Mathf.Clamp01(
                    Vector3.Dot(vertices[i] - bindStart, bindAxis) / bindAxisLengthSqr);
                float boneCoordinate = normalizedDistance * (runtimeBoneCount - 1);
                int lowerBoneIndex = Mathf.Min(Mathf.FloorToInt(boneCoordinate), runtimeBoneCount - 1);
                int upperBoneIndex = Mathf.Min(lowerBoneIndex + 1, runtimeBoneCount - 1);
                float segmentBlend = boneCoordinate - lowerBoneIndex;
                segmentBlend = segmentBlend * segmentBlend * (3f - 2f * segmentBlend);

                weights[i] = new BoneWeight
                {
                    boneIndex0 = lowerBoneIndex,
                    weight0 = 1f - segmentBlend,
                    boneIndex1 = upperBoneIndex,
                    weight1 = segmentBlend
                };
            }

            Matrix4x4[] bindPoses = new Matrix4x4[runtimeBoneCount];
            Matrix4x4 rendererLocalToWorld = _hoseRenderer.transform.localToWorldMatrix;
            for (int i = 0; i < runtimeBoneCount; i++)
                bindPoses[i] = runtimeBones[i].worldToLocalMatrix * rendererLocalToWorld;

            _runtimeMesh.bindposes = bindPoses;
            _runtimeMesh.boneWeights = weights;
            _runtimeMesh.RecalculateBounds();
            _hoseRenderer.sharedMesh = _runtimeMesh;
            _hoseRenderer.bones = runtimeBones;
            _hoseRenderer.rootBone = runtimeBones[0];
            _bonesIncludeEndpoint = true;
            return true;
        }

        private void RestoreSourceRig()
        {
            if (_hoseRenderer != null && _sourceMesh != null)
            {
                _hoseRenderer.sharedMesh = _sourceMesh;
                _hoseRenderer.bones = _sourceBones;
                _hoseRenderer.rootBone = _sourceRootBone;
            }

            if (_runtimeRigRoot != null) Destroy(_runtimeRigRoot.gameObject);
            if (_runtimeMesh != null) Destroy(_runtimeMesh);
            _runtimeRigRoot = null;
            _runtimeMesh = null;
            _bonesIncludeEndpoint = false;
        }

        private static Vector3 SampleSourcePosition(
            Vector3[] points,
            float[] distances,
            float targetDistance)
        {
            for (int i = 1; i < points.Length; i++)
            {
                if (distances[i] < targetDistance) continue;

                float segmentLength = distances[i] - distances[i - 1];
                float segmentBlend = segmentLength > 0.000001f
                    ? (targetDistance - distances[i - 1]) / segmentLength
                    : 0f;
                return Vector3.Lerp(points[i - 1], points[i], segmentBlend);
            }

            return points[points.Length - 1];
        }

        private float GetBoneNormalizedDistance(int boneIndex)
        {
            int segmentCount = _bonesIncludeEndpoint ? _bones.Length - 1 : _bones.Length;
            return segmentCount > 0 ? (float)boneIndex / segmentCount : 0f;
        }

        private float GetTangentLength(float targetLength)
        {
            return Mathf.Min(targetLength, _effectiveRestLength) * _tangentLengthRatio;
        }

        private Vector3 GetBindDirection(int boneIndex)
        {
            Vector3 direction;
            if (boneIndex < _bones.Length - 1)
                direction = _bones[boneIndex + 1].position - _bones[boneIndex].position;
            else
                direction = _bones[boneIndex].position - _bones[boneIndex - 1].position;

            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                direction = _bones[boneIndex].up;

            return direction.normalized;
        }

        private float EstimateBindLength()
        {
            float distance = 0f;
            for (int i = 1; i < _bones.Length; i++)
                distance += Vector3.Distance(_bones[i - 1].position, _bones[i].position);

            if (_bonesIncludeEndpoint) return distance;

            float averageSegmentLength = distance / (_bones.Length - 1);
            return distance + averageSegmentLength;
        }

        private void EnsureArcLengthTables()
        {
            int tableSize = _curveSamples + 1;
            if (_arcParameters != null && _arcParameters.Length == tableSize) return;

            _arcParameters = new float[tableSize];
            _arcLengths = new float[tableSize];
        }

        private static Vector3 GetSafeNormal(Vector3 candidate, Vector3 tangent)
        {
            Vector3 normal = Vector3.ProjectOnPlane(candidate, tangent);
            if (normal.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                normal = Vector3.ProjectOnPlane(Vector3.up, tangent);
            if (normal.sqrMagnitude <= MinimumDirectionSqrMagnitude)
                normal = Vector3.ProjectOnPlane(Vector3.forward, tangent);
            return normal.normalized;
        }

        private static Vector3 GetSafeAxis(Vector3 candidate, Vector3 fallback)
        {
            return candidate.sqrMagnitude > MinimumDirectionSqrMagnitude
                ? candidate.normalized
                : fallback;
        }

        private static Vector3 InterpolateNormal(
            Vector3 startNormal,
            Vector3 endNormal,
            Vector3 tangent,
            float t)
        {
            Vector3 normal = Vector3.Slerp(startNormal, endNormal, t);
            return GetSafeNormal(normal, tangent);
        }

        private static float GetDampingBlend(float deltaTime, float smoothTime)
        {
            if (smoothTime <= 0f || deltaTime <= 0f) return 1f;
            return 1f - Mathf.Exp(-deltaTime / smoothTime);
        }
    }
}

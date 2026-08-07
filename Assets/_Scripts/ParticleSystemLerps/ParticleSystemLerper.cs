using UnityEngine;
using UnityEngine.Serialization;

namespace _Scripts.ParticleSystemLerps
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ParticleSystemLerper : MonoBehaviour
    {
    [Header("Samples")]
    [FormerlySerializedAs("sourceA"), SerializeField] private ParticleSystem _sourceA;
    [FormerlySerializedAs("sourceB"), SerializeField] private ParticleSystem _sourceB;

    private ParticleSystem _target;
    private Snapshot _cachedA;
    private Snapshot _cachedB;
    private bool _hasCache;

    private void Awake()
    {
        RebuildCache();
    }

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        _target = GetComponent<ParticleSystem>();
        _hasCache = false;

        if (_target == null || _sourceA == null || _sourceB == null)
            return;

        if (_sourceA == _sourceB || _sourceA == _target || _sourceB == _target)
            return;

        _cachedA = Snapshot.Capture(_sourceA);
        _cachedB = Snapshot.Capture(_sourceB);
        _hasCache = true;
    }

    public void SetBlend(float value)
    {
        var blend = Mathf.Clamp01(value);

        if (!_hasCache)
        {
            RebuildCache();
            if (!_hasCache)
                return;
        }

        ApplyMain(_cachedA.main, _cachedB.main, blend);
        ApplyEmission(_cachedA.emission, _cachedB.emission, blend);
        ApplyVelocity(_cachedA.velocity, _cachedB.velocity, blend);
        ApplyLimitVelocity(_cachedA.limitVelocity, _cachedB.limitVelocity, blend);
        ApplyForce(_cachedA.force, _cachedB.force, blend);
        ApplyColorOverLifetime(_cachedA.colorOverLifetime, _cachedB.colorOverLifetime, blend);
        ApplyColorBySpeed(_cachedA.colorBySpeed, _cachedB.colorBySpeed, blend);
        ApplySizeOverLifetime(_cachedA.sizeOverLifetime, _cachedB.sizeOverLifetime, blend);
        ApplySizeBySpeed(_cachedA.sizeBySpeed, _cachedB.sizeBySpeed, blend);
        ApplyRotationOverLifetime(_cachedA.rotationOverLifetime, _cachedB.rotationOverLifetime, blend);
        ApplyRotationBySpeed(_cachedA.rotationBySpeed, _cachedB.rotationBySpeed, blend);
        ApplyNoise(_cachedA.noise, _cachedB.noise, blend);
    }

    private void ApplyMain(MainSnapshot a, MainSnapshot b, float t)
    {
        var module = _target.main;

        // Unity does not allow duration changes while the system is playing.
        if (!_target.isPlaying)
            module.duration = Mathf.LerpUnclamped(a.duration, b.duration, t);

        if (a.loop == b.loop) module.loop = a.loop;
        if (a.prewarm == b.prewarm) module.prewarm = a.prewarm;
        if (a.playOnAwake == b.playOnAwake) module.playOnAwake = a.playOnAwake;
        if (a.useUnscaledTime == b.useUnscaledTime) module.useUnscaledTime = a.useUnscaledTime;
        if (a.maxParticles == b.maxParticles) module.maxParticles = a.maxParticles;
        if (a.simulationSpace == b.simulationSpace) module.simulationSpace = a.simulationSpace;
        if (a.customSimulationSpace == b.customSimulationSpace) module.customSimulationSpace = a.customSimulationSpace;
        if (a.scalingMode == b.scalingMode) module.scalingMode = a.scalingMode;
        if (a.stopAction == b.stopAction) module.stopAction = a.stopAction;
        if (a.cullingMode == b.cullingMode) module.cullingMode = a.cullingMode;
        if (a.emitterVelocityMode == b.emitterVelocityMode) module.emitterVelocityMode = a.emitterVelocityMode;
        if (a.gravitySource == b.gravitySource) module.gravitySource = a.gravitySource;

        module.simulationSpeed = Mathf.LerpUnclamped(a.simulationSpeed, b.simulationSpeed, t);
        module.flipRotation = Mathf.LerpUnclamped(a.flipRotation, b.flipRotation, t);

        SetCurve(ref module, MainCurve.StartDelay, a.startDelay, b.startDelay, t);
        SetCurve(ref module, MainCurve.StartLifetime, a.startLifetime, b.startLifetime, t);
        SetCurve(ref module, MainCurve.StartSpeed, a.startSpeed, b.startSpeed, t);
        SetCurve(ref module, MainCurve.GravityModifier, a.gravityModifier, b.gravityModifier, t);

        if (a.startSize3D == b.startSize3D)
        {
            module.startSize3D = a.startSize3D;
            SetCurve(ref module, MainCurve.StartSize, a.startSize, b.startSize, t);
            SetCurve(ref module, MainCurve.StartSizeX, a.startSizeX, b.startSizeX, t);
            SetCurve(ref module, MainCurve.StartSizeY, a.startSizeY, b.startSizeY, t);
            SetCurve(ref module, MainCurve.StartSizeZ, a.startSizeZ, b.startSizeZ, t);
        }

        if (a.startRotation3D == b.startRotation3D)
        {
            module.startRotation3D = a.startRotation3D;
            SetCurve(ref module, MainCurve.StartRotation, a.startRotation, b.startRotation, t);
            SetCurve(ref module, MainCurve.StartRotationX, a.startRotationX, b.startRotationX, t);
            SetCurve(ref module, MainCurve.StartRotationY, a.startRotationY, b.startRotationY, t);
            SetCurve(ref module, MainCurve.StartRotationZ, a.startRotationZ, b.startRotationZ, t);
        }

        if (TryLerp(a.startColor, b.startColor, t, out var startColor))
            module.startColor = startColor;

        if (a.ringBufferMode == b.ringBufferMode)
        {
            module.ringBufferMode = a.ringBufferMode;
            module.ringBufferLoopRange = Vector2.LerpUnclamped(a.ringBufferLoopRange, b.ringBufferLoopRange, t);
        }
    }

    private enum MainCurve
    {
        StartDelay,
        StartLifetime,
        StartSpeed,
        GravityModifier,
        StartSize,
        StartSizeX,
        StartSizeY,
        StartSizeZ,
        StartRotation,
        StartRotationX,
        StartRotationY,
        StartRotationZ
    }

    private static void SetCurve(
        ref ParticleSystem.MainModule module,
        MainCurve property,
        ParticleSystem.MinMaxCurve a,
        ParticleSystem.MinMaxCurve b,
        float t)
    {
        if (!TryLerp(a, b, t, out var value))
            return;

        switch (property)
        {
            case MainCurve.StartDelay: module.startDelay = value; break;
            case MainCurve.StartLifetime: module.startLifetime = value; break;
            case MainCurve.StartSpeed: module.startSpeed = value; break;
            case MainCurve.GravityModifier: module.gravityModifier = value; break;
            case MainCurve.StartSize: module.startSize = value; break;
            case MainCurve.StartSizeX: module.startSizeX = value; break;
            case MainCurve.StartSizeY: module.startSizeY = value; break;
            case MainCurve.StartSizeZ: module.startSizeZ = value; break;
            case MainCurve.StartRotation: module.startRotation = value; break;
            case MainCurve.StartRotationX: module.startRotationX = value; break;
            case MainCurve.StartRotationY: module.startRotationY = value; break;
            case MainCurve.StartRotationZ: module.startRotationZ = value; break;
        }
    }

    private void ApplyEmission(EmissionSnapshot a, EmissionSnapshot b, float t)
    {
        var module = _target.emission;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (TryLerp(a.rateOverTime, b.rateOverTime, t, out var rateTime)) module.rateOverTime = rateTime;
        if (TryLerp(a.rateOverDistance, b.rateOverDistance, t, out var rateDistance)) module.rateOverDistance = rateDistance;
        // Bursts are intentionally untouched.
    }

    private void ApplyVelocity(VelocitySnapshot a, VelocitySnapshot b, float t)
    {
        var module = _target.velocityOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (a.space == b.space) module.space = a.space;

        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
        if (TryLerp(a.orbitalX, b.orbitalX, t, out var orbitalX)) module.orbitalX = orbitalX;
        if (TryLerp(a.orbitalY, b.orbitalY, t, out var orbitalY)) module.orbitalY = orbitalY;
        if (TryLerp(a.orbitalZ, b.orbitalZ, t, out var orbitalZ)) module.orbitalZ = orbitalZ;
        if (TryLerp(a.orbitalOffsetX, b.orbitalOffsetX, t, out var offsetX)) module.orbitalOffsetX = offsetX;
        if (TryLerp(a.orbitalOffsetY, b.orbitalOffsetY, t, out var offsetY)) module.orbitalOffsetY = offsetY;
        if (TryLerp(a.orbitalOffsetZ, b.orbitalOffsetZ, t, out var offsetZ)) module.orbitalOffsetZ = offsetZ;
        if (TryLerp(a.radial, b.radial, t, out var radial)) module.radial = radial;
        if (TryLerp(a.speedModifier, b.speedModifier, t, out var speed)) module.speedModifier = speed;
    }

    private void ApplyLimitVelocity(LimitVelocitySnapshot a, LimitVelocitySnapshot b, float t)
    {
        var module = _target.limitVelocityOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (a.space == b.space) module.space = a.space;
        if (a.multiplyDragByParticleSize == b.multiplyDragByParticleSize)
            module.multiplyDragByParticleSize = a.multiplyDragByParticleSize;
        if (a.multiplyDragByParticleVelocity == b.multiplyDragByParticleVelocity)
            module.multiplyDragByParticleVelocity = a.multiplyDragByParticleVelocity;

        module.dampen = Mathf.LerpUnclamped(a.dampen, b.dampen, t);

        if (a.separateAxes == b.separateAxes)
        {
            module.separateAxes = a.separateAxes;
            if (TryLerp(a.limit, b.limit, t, out var limit)) module.limit = limit;
            if (TryLerp(a.limitX, b.limitX, t, out var limitX)) module.limitX = limitX;
            if (TryLerp(a.limitY, b.limitY, t, out var limitY)) module.limitY = limitY;
            if (TryLerp(a.limitZ, b.limitZ, t, out var limitZ)) module.limitZ = limitZ;
        }

        if (TryLerp(a.drag, b.drag, t, out var drag)) module.drag = drag;
    }

    private void ApplyForce(ForceSnapshot a, ForceSnapshot b, float t)
    {
        var module = _target.forceOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (a.space == b.space) module.space = a.space;
        if (a.randomized == b.randomized) module.randomized = a.randomized;
        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
    }

    private void ApplyColorOverLifetime(ColorSnapshot a, ColorSnapshot b, float t)
    {
        var module = _target.colorOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (TryLerp(a.color, b.color, t, out var color)) module.color = color;
    }

    private void ApplyColorBySpeed(ColorBySpeedSnapshot a, ColorBySpeedSnapshot b, float t)
    {
        var module = _target.colorBySpeed;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (TryLerp(a.color, b.color, t, out var color)) module.color = color;
        module.range = Vector2.LerpUnclamped(a.range, b.range, t);
    }

    private void ApplySizeOverLifetime(SizeSnapshot a, SizeSnapshot b, float t)
    {
        var module = _target.sizeOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;

        if (a.separateAxes != b.separateAxes)
            return;

        module.separateAxes = a.separateAxes;
        if (TryLerp(a.size, b.size, t, out var size)) module.size = size;
        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
    }

    private void ApplySizeBySpeed(SizeBySpeedSnapshot a, SizeBySpeedSnapshot b, float t)
    {
        var module = _target.sizeBySpeed;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        module.range = Vector2.LerpUnclamped(a.range, b.range, t);

        if (a.separateAxes != b.separateAxes)
            return;

        module.separateAxes = a.separateAxes;
        if (TryLerp(a.size, b.size, t, out var size)) module.size = size;
        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
    }

    private void ApplyRotationOverLifetime(RotationSnapshot a, RotationSnapshot b, float t)
    {
        var module = _target.rotationOverLifetime;
        if (a.enabled == b.enabled) module.enabled = a.enabled;

        if (a.separateAxes != b.separateAxes)
            return;

        module.separateAxes = a.separateAxes;
        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
    }

    private void ApplyRotationBySpeed(RotationBySpeedSnapshot a, RotationBySpeedSnapshot b, float t)
    {
        var module = _target.rotationBySpeed;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        module.range = Vector2.LerpUnclamped(a.range, b.range, t);

        if (a.separateAxes != b.separateAxes)
            return;

        module.separateAxes = a.separateAxes;
        if (TryLerp(a.x, b.x, t, out var x)) module.x = x;
        if (TryLerp(a.y, b.y, t, out var y)) module.y = y;
        if (TryLerp(a.z, b.z, t, out var z)) module.z = z;
    }

    private void ApplyNoise(NoiseSnapshot a, NoiseSnapshot b, float t)
    {
        var module = _target.noise;
        if (a.enabled == b.enabled) module.enabled = a.enabled;
        if (a.damping == b.damping) module.damping = a.damping;
        if (a.quality == b.quality) module.quality = a.quality;
        if (a.remapEnabled == b.remapEnabled) module.remapEnabled = a.remapEnabled;
        if (a.octaveCount == b.octaveCount) module.octaveCount = a.octaveCount;

        module.frequency = Mathf.LerpUnclamped(a.frequency, b.frequency, t);
        module.octaveMultiplier = Mathf.LerpUnclamped(a.octaveMultiplier, b.octaveMultiplier, t);
        module.octaveScale = Mathf.LerpUnclamped(a.octaveScale, b.octaveScale, t);
        if (TryLerp(a.positionAmount, b.positionAmount, t, out var positionAmount))
            module.positionAmount = positionAmount;
        if (TryLerp(a.rotationAmount, b.rotationAmount, t, out var rotationAmount))
            module.rotationAmount = rotationAmount;
        if (TryLerp(a.sizeAmount, b.sizeAmount, t, out var sizeAmount))
            module.sizeAmount = sizeAmount;

        if (a.separateAxes == b.separateAxes)
        {
            module.separateAxes = a.separateAxes;
            if (TryLerp(a.strength, b.strength, t, out var strength)) module.strength = strength;
            if (TryLerp(a.strengthX, b.strengthX, t, out var strengthX)) module.strengthX = strengthX;
            if (TryLerp(a.strengthY, b.strengthY, t, out var strengthY)) module.strengthY = strengthY;
            if (TryLerp(a.strengthZ, b.strengthZ, t, out var strengthZ)) module.strengthZ = strengthZ;
        }

        if (TryLerp(a.scrollSpeed, b.scrollSpeed, t, out var scrollSpeed)) module.scrollSpeed = scrollSpeed;
        if (TryLerp(a.remap, b.remap, t, out var remap)) module.remap = remap;
        if (TryLerp(a.remapX, b.remapX, t, out var remapX)) module.remapX = remapX;
        if (TryLerp(a.remapY, b.remapY, t, out var remapY)) module.remapY = remapY;
        if (TryLerp(a.remapZ, b.remapZ, t, out var remapZ)) module.remapZ = remapZ;
    }

    private static bool TryLerp(
        ParticleSystem.MinMaxCurve a,
        ParticleSystem.MinMaxCurve b,
        float t,
        out ParticleSystem.MinMaxCurve result)
    {
        result = default;

        if (a.mode != b.mode)
            return false;

        switch (a.mode)
        {
            case ParticleSystemCurveMode.Constant:
                result = new ParticleSystem.MinMaxCurve(
                    Mathf.LerpUnclamped(a.constant, b.constant, t));
                return true;

            case ParticleSystemCurveMode.TwoConstants:
                result = new ParticleSystem.MinMaxCurve(
                    Mathf.LerpUnclamped(a.constantMin, b.constantMin, t),
                    Mathf.LerpUnclamped(a.constantMax, b.constantMax, t));
                return true;

            default:
                return false;
        }
    }

    private static bool TryLerp(
        ParticleSystem.MinMaxGradient a,
        ParticleSystem.MinMaxGradient b,
        float t,
        out ParticleSystem.MinMaxGradient result)
    {
        result = default;

        if (a.mode != b.mode)
            return false;

        switch (a.mode)
        {
            case ParticleSystemGradientMode.Color:
                result = new ParticleSystem.MinMaxGradient(
                    Color.LerpUnclamped(a.color, b.color, t));
                return true;

            case ParticleSystemGradientMode.TwoColors:
                result = new ParticleSystem.MinMaxGradient(
                    Color.LerpUnclamped(a.colorMin, b.colorMin, t),
                    Color.LerpUnclamped(a.colorMax, b.colorMax, t));
                return true;

            default:
                return false;
        }
    }

    }
}

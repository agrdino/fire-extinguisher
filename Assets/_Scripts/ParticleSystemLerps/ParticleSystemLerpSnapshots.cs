using UnityEngine;

namespace _Scripts.ParticleSystemLerps
{
    internal sealed class Snapshot
    {
        public MainSnapshot main;
        public EmissionSnapshot emission;
        public VelocitySnapshot velocity;
        public LimitVelocitySnapshot limitVelocity;
        public ForceSnapshot force;
        public ColorSnapshot colorOverLifetime;
        public ColorBySpeedSnapshot colorBySpeed;
        public SizeSnapshot sizeOverLifetime;
        public SizeBySpeedSnapshot sizeBySpeed;
        public RotationSnapshot rotationOverLifetime;
        public RotationBySpeedSnapshot rotationBySpeed;
        public NoiseSnapshot noise;

        public static Snapshot Capture(ParticleSystem system)
        {
            return new Snapshot
            {
                main = MainSnapshot.Capture(system.main),
                emission = EmissionSnapshot.Capture(system.emission),
                velocity = VelocitySnapshot.Capture(system.velocityOverLifetime),
                limitVelocity = LimitVelocitySnapshot.Capture(system.limitVelocityOverLifetime),
                force = ForceSnapshot.Capture(system.forceOverLifetime),
                colorOverLifetime = ColorSnapshot.Capture(system.colorOverLifetime),
                colorBySpeed = ColorBySpeedSnapshot.Capture(system.colorBySpeed),
                sizeOverLifetime = SizeSnapshot.Capture(system.sizeOverLifetime),
                sizeBySpeed = SizeBySpeedSnapshot.Capture(system.sizeBySpeed),
                rotationOverLifetime = RotationSnapshot.Capture(system.rotationOverLifetime),
                rotationBySpeed = RotationBySpeedSnapshot.Capture(system.rotationBySpeed),
                noise = NoiseSnapshot.Capture(system.noise)
            };
        }
    }

    internal sealed class MainSnapshot
    {
        public float duration;
        public bool loop, prewarm, playOnAwake, useUnscaledTime, startSize3D, startRotation3D;
        public int maxParticles;
        public float simulationSpeed, flipRotation;
        public ParticleSystemSimulationSpace simulationSpace;
        public Transform customSimulationSpace;
        public ParticleSystemScalingMode scalingMode;
        public ParticleSystemStopAction stopAction;
        public ParticleSystemCullingMode cullingMode;
        public ParticleSystemEmitterVelocityMode emitterVelocityMode;
        public ParticleSystemGravitySource gravitySource;
        public ParticleSystemRingBufferMode ringBufferMode;
        public Vector2 ringBufferLoopRange;
        public ParticleSystem.MinMaxCurve startDelay, startLifetime, startSpeed, gravityModifier;
        public ParticleSystem.MinMaxCurve startSize, startSizeX, startSizeY, startSizeZ;
        public ParticleSystem.MinMaxCurve startRotation, startRotationX, startRotationY, startRotationZ;
        public ParticleSystem.MinMaxGradient startColor;

        public static MainSnapshot Capture(ParticleSystem.MainModule module)
        {
            return new MainSnapshot
            {
                duration = module.duration,
                loop = module.loop,
                prewarm = module.prewarm,
                playOnAwake = module.playOnAwake,
                useUnscaledTime = module.useUnscaledTime,
                maxParticles = module.maxParticles,
                simulationSpeed = module.simulationSpeed,
                simulationSpace = module.simulationSpace,
                customSimulationSpace = module.customSimulationSpace,
                scalingMode = module.scalingMode,
                stopAction = module.stopAction,
                cullingMode = module.cullingMode,
                emitterVelocityMode = module.emitterVelocityMode,
                gravitySource = module.gravitySource,
                flipRotation = module.flipRotation,
                startDelay = module.startDelay,
                startLifetime = module.startLifetime,
                startSpeed = module.startSpeed,
                gravityModifier = module.gravityModifier,
                startSize3D = module.startSize3D,
                startSize = module.startSize,
                startSizeX = module.startSizeX,
                startSizeY = module.startSizeY,
                startSizeZ = module.startSizeZ,
                startRotation3D = module.startRotation3D,
                startRotation = module.startRotation,
                startRotationX = module.startRotationX,
                startRotationY = module.startRotationY,
                startRotationZ = module.startRotationZ,
                startColor = module.startColor,
                ringBufferMode = module.ringBufferMode,
                ringBufferLoopRange = module.ringBufferLoopRange
            };
        }
    }

    internal sealed class EmissionSnapshot
    {
        public bool enabled;
        public ParticleSystem.MinMaxCurve rateOverTime, rateOverDistance;

        public static EmissionSnapshot Capture(ParticleSystem.EmissionModule module) => new EmissionSnapshot
        {
            enabled = module.enabled,
            rateOverTime = module.rateOverTime,
            rateOverDistance = module.rateOverDistance
        };
    }

    internal sealed class VelocitySnapshot
    {
        public bool enabled;
        public ParticleSystemSimulationSpace space;
        public ParticleSystem.MinMaxCurve x, y, z, orbitalX, orbitalY, orbitalZ;
        public ParticleSystem.MinMaxCurve orbitalOffsetX, orbitalOffsetY, orbitalOffsetZ, radial, speedModifier;

        public static VelocitySnapshot Capture(ParticleSystem.VelocityOverLifetimeModule module) => new VelocitySnapshot
        {
            enabled = module.enabled,
            space = module.space,
            x = module.x,
            y = module.y,
            z = module.z,
            orbitalX = module.orbitalX,
            orbitalY = module.orbitalY,
            orbitalZ = module.orbitalZ,
            orbitalOffsetX = module.orbitalOffsetX,
            orbitalOffsetY = module.orbitalOffsetY,
            orbitalOffsetZ = module.orbitalOffsetZ,
            radial = module.radial,
            speedModifier = module.speedModifier
        };
    }

    internal sealed class LimitVelocitySnapshot
    {
        public bool enabled, separateAxes, multiplyDragByParticleSize, multiplyDragByParticleVelocity;
        public ParticleSystemSimulationSpace space;
        public float dampen;
        public ParticleSystem.MinMaxCurve limit, limitX, limitY, limitZ, drag;

        public static LimitVelocitySnapshot Capture(ParticleSystem.LimitVelocityOverLifetimeModule module) =>
            new LimitVelocitySnapshot
            {
                enabled = module.enabled,
                separateAxes = module.separateAxes,
                space = module.space,
                dampen = module.dampen,
                multiplyDragByParticleSize = module.multiplyDragByParticleSize,
                multiplyDragByParticleVelocity = module.multiplyDragByParticleVelocity,
                limit = module.limit,
                limitX = module.limitX,
                limitY = module.limitY,
                limitZ = module.limitZ,
                drag = module.drag
            };
    }

    internal sealed class ForceSnapshot
    {
        public bool enabled, randomized;
        public ParticleSystemSimulationSpace space;
        public ParticleSystem.MinMaxCurve x, y, z;

        public static ForceSnapshot Capture(ParticleSystem.ForceOverLifetimeModule module) => new ForceSnapshot
        {
            enabled = module.enabled,
            randomized = module.randomized,
            space = module.space,
            x = module.x,
            y = module.y,
            z = module.z
        };
    }

    internal sealed class ColorSnapshot
    {
        public bool enabled;
        public ParticleSystem.MinMaxGradient color;

        public static ColorSnapshot Capture(ParticleSystem.ColorOverLifetimeModule module) => new ColorSnapshot
        {
            enabled = module.enabled,
            color = module.color
        };
    }

    internal sealed class ColorBySpeedSnapshot
    {
        public bool enabled;
        public Vector2 range;
        public ParticleSystem.MinMaxGradient color;

        public static ColorBySpeedSnapshot Capture(ParticleSystem.ColorBySpeedModule module) => new ColorBySpeedSnapshot
        {
            enabled = module.enabled,
            range = module.range,
            color = module.color
        };
    }

    internal sealed class SizeSnapshot
    {
        public bool enabled, separateAxes;
        public ParticleSystem.MinMaxCurve size, x, y, z;

        public static SizeSnapshot Capture(ParticleSystem.SizeOverLifetimeModule module) => new SizeSnapshot
        {
            enabled = module.enabled,
            separateAxes = module.separateAxes,
            size = module.size,
            x = module.x,
            y = module.y,
            z = module.z
        };
    }

    internal sealed class SizeBySpeedSnapshot
    {
        public bool enabled, separateAxes;
        public Vector2 range;
        public ParticleSystem.MinMaxCurve size, x, y, z;

        public static SizeBySpeedSnapshot Capture(ParticleSystem.SizeBySpeedModule module) => new SizeBySpeedSnapshot
        {
            enabled = module.enabled,
            separateAxes = module.separateAxes,
            range = module.range,
            size = module.size,
            x = module.x,
            y = module.y,
            z = module.z
        };
    }

    internal sealed class RotationSnapshot
    {
        public bool enabled, separateAxes;
        public ParticleSystem.MinMaxCurve x, y, z;

        public static RotationSnapshot Capture(ParticleSystem.RotationOverLifetimeModule module) => new RotationSnapshot
        {
            enabled = module.enabled,
            separateAxes = module.separateAxes,
            x = module.x,
            y = module.y,
            z = module.z
        };
    }

    internal sealed class RotationBySpeedSnapshot
    {
        public bool enabled, separateAxes;
        public Vector2 range;
        public ParticleSystem.MinMaxCurve x, y, z;

        public static RotationBySpeedSnapshot Capture(ParticleSystem.RotationBySpeedModule module) => new RotationBySpeedSnapshot
        {
            enabled = module.enabled,
            separateAxes = module.separateAxes,
            range = module.range,
            x = module.x,
            y = module.y,
            z = module.z
        };
    }

    internal sealed class NoiseSnapshot
    {
        public bool enabled, separateAxes, damping, remapEnabled;
        public ParticleSystemNoiseQuality quality;
        public int octaveCount;
        public float frequency, octaveMultiplier, octaveScale;
        public ParticleSystem.MinMaxCurve strength, strengthX, strengthY, strengthZ;
        public ParticleSystem.MinMaxCurve scrollSpeed, remap, remapX, remapY, remapZ;
        public ParticleSystem.MinMaxCurve positionAmount, rotationAmount, sizeAmount;

        public static NoiseSnapshot Capture(ParticleSystem.NoiseModule module) => new NoiseSnapshot
        {
            enabled = module.enabled,
            separateAxes = module.separateAxes,
            damping = module.damping,
            remapEnabled = module.remapEnabled,
            quality = module.quality,
            octaveCount = module.octaveCount,
            frequency = module.frequency,
            octaveMultiplier = module.octaveMultiplier,
            octaveScale = module.octaveScale,
            positionAmount = module.positionAmount,
            rotationAmount = module.rotationAmount,
            sizeAmount = module.sizeAmount,
            strength = module.strength,
            strengthX = module.strengthX,
            strengthY = module.strengthY,
            strengthZ = module.strengthZ,
            scrollSpeed = module.scrollSpeed,
            remap = module.remap,
            remapX = module.remapX,
            remapY = module.remapY,
            remapZ = module.remapZ
        };
    }
}

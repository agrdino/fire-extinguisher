Shader "Fire Extinguisher/VFX/Fire Alarm Light Soft Particle"
{
    Properties
    {
        _Color("Color", Color) = (1, 0, 0, 1)
        _Power("Power", Range(0, 5)) = 3
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clipping", Float) = 1
        _Alpha_Clipping("Alpha Clip Threshold", Range(0, 1)) = 0
        [Toggle(_SOFTPARTICLES_ON)] _SoftParticlesEnabled("Soft Particles", Float) = 1
        _SoftParticlesNearFadeDistance("Soft Particles Near Fade", Float) = 0
        _SoftParticlesFarFadeDistance("Soft Particles Far Fade", Float) = 0.5
        [HideInInspector] _SrcBlend("Source Blend", Float) = 5
        [HideInInspector] _DstBlend("Destination Blend", Float) = 10
        [HideInInspector] _SrcBlendAlpha("Source Blend Alpha", Float) = 1
        [HideInInspector] _DstBlendAlpha("Destination Blend Alpha", Float) = 10
        [HideInInspector] _ZWrite("ZWrite", Float) = 0
        [HideInInspector] _ZTest("ZTest", Float) = 4
        [HideInInspector] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        Pass
        {
            Name "Forward Unlit"
            Tags { "LightMode" = "UniversalForwardOnly" }
            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SOFTPARTICLES_ON
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Power;
                half _Alpha_Clipping;
                half _SoftParticlesNearFadeDistance;
                half _SoftParticlesFarFadeDistance;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float eyeDepth : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                const float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.eyeDepth = -TransformWorldToView(positionWS).z;
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half alpha = pow(saturate(input.uv.x), _Power) * _Color.a;
                #if defined(_SOFTPARTICLES_ON)
                    const float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                    const float rawSceneDepth = SampleSceneDepth(screenUV);
                    const float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                    const float fadeRange = max(_SoftParticlesFarFadeDistance - _SoftParticlesNearFadeDistance, 0.0001);
                    const half depthFade = saturate((sceneEyeDepth - input.eyeDepth - _SoftParticlesNearFadeDistance) / fadeRange);
                    alpha *= depthFade;
                #endif
                #if defined(_ALPHATEST_ON)
                    clip(alpha - _Alpha_Clipping);
                #endif
                return half4(_Color.rgb, alpha);
            }
            ENDHLSL
        }
    }
}

// The derez (OVERLOAD GDD 17, 20 MMF_KO, 22).
//
// Written by hand against URP rather than using the Ultimate 10+ "Dissolve" shader, which is a
// built-in-pipeline surface shader (#pragma surface surf Standard) and renders magenta under URP.
// Hovl's DissolveNoise is URP-safe but is a particle shader, not something you can put on a
// skinned character. This is the smallest thing that does the job on a Synty runner.
//
// Lit with a single lambert term from the main light so the runner does not visibly pop to a
// different brightness the instant the effect starts - the dissolve should read as the runner
// coming apart, not as a material swap.

Shader "Overload/RunnerDerez"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Colour", Color) = (1,1,1,1)

        _NoiseMap ("Dissolve Noise", 2D) = "white" {}
        _NoiseScale ("Noise Scale", Float) = 5

        _Dissolve ("Dissolve", Range(0,1)) = 0
        _EdgeWidth ("Edge Width", Range(0.001, 0.4)) = 0.10
        [HDR] _EdgeColor ("Edge Colour", Color) = (0.6, 3.0, 5.0, 1)

        _Ambient ("Ambient Floor", Range(0,1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 positionWS  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap);  SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _EdgeColor;
                float  _Dissolve;
                float  _EdgeWidth;
                float  _NoiseScale;
                float  _Ambient;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = nrm.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Object-space-ish world noise, so the burn pattern does not swim with the UVs of a
                // Synty atlas where unrelated body parts share texture space.
                float2 noiseUV = IN.positionWS.xz * _NoiseScale * 0.1 + IN.positionWS.y * 0.07;
                half noise = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, noiseUV).r;

                // Pushed slightly past 1 so a full dissolve clears every last texel.
                half threshold = _Dissolve * (1.0 + _EdgeWidth);
                clip(noise - threshold);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                Light mainLight = GetMainLight();
                half ndotl = saturate(dot(normalize(IN.normalWS), mainLight.direction));
                half3 lit = albedo.rgb * (mainLight.color * ndotl + _Ambient);

                // The burning rim: everything within EdgeWidth of being clipped glows.
                half edge = 1.0 - saturate((noise - threshold) / max(_EdgeWidth, 1e-4));
                lit = lerp(lit, _EdgeColor.rgb, edge * step(0.0001, _Dissolve));

                return half4(lit, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}

Shader "FPSDemo/Enemy/S_ZombieHairMobile"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 0.65
        _MaskMap("Enemy Mask RGBA", 2D) = "white" {}
        _SpecGlossMap("Specular Smoothness", 2D) = "white" {}

        _ShadowTint("Cold Shadow Tint", Color) = (0.14, 0.16, 0.16, 1)
        _DirtTint("Dirt Tint", Color) = (0.16, 0.13, 0.10, 1)
        _ColdRimColor("Cold Rim Color", Color) = (0.58, 0.72, 0.82, 1)

        _Desaturation("Hair Desaturation", Range(0, 1)) = 0.10
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.10
        _Wetness("Wetness", Range(0, 1)) = 0.03
        _CavityDarkness("Strand Darkness", Range(0, 1)) = 0.16
        _HeadContrastBoost("Readability Boost", Range(0, 1)) = 0.06
        _RimDamp("Damp Rim", Range(0, 1)) = 0.10
        _PromoContrast("Hair Contrast", Range(0, 1)) = 0.08
        _ColdRimStrength("Cold Rim Strength", Range(0, 2)) = 0.30
        _CavityInk("Cavity Ink", Range(0, 1)) = 0.00
        _MinVisibility("Minimum Visibility", Range(0, 1)) = 0.56
        _AmbientLift("Ambient Lift", Range(0, 1)) = 0.55
        _Smoothness("Base Smoothness", Range(0, 1)) = 0.12
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.06
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MaskMap);
            SAMPLER(sampler_MaskMap);
            TEXTURE2D(_SpecGlossMap);
            SAMPLER(sampler_SpecGlossMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _ColdRimColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _ColdRimStrength;
                half _CavityInk;
                half _MinVisibility;
                half _AmbientLift;
                half _Smoothness;
                half _SpecularStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            half3 ApplyHairGrade(half3 color, half cavity)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half3 hair = lerp(color, luma.xxx, _Desaturation * 0.18h);
                half dirtWeight = saturate(cavity * _DirtAmount);
                hair *= lerp(1.08h, 0.90h, dirtWeight);
                hair = lerp(hair, hair * (_DirtTint.rgb * 1.70h), dirtWeight * 0.08h);
                hair = max(hair, luma.xxx * 0.42h + 0.025h);
                hair = lerp(hair, hair * 1.08h + 0.025h, _HeadContrastBoost);
                return saturate((hair - 0.5h) * (1.0h + _PromoContrast * 0.12h) + 0.5h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half4 specGloss = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, input.uv);

                half occlusion = saturate(mask.r);
                half wetMask = saturate(mask.g);
                half smoothMask = saturate(mask.a);
                half cavity = 1.0h - occlusion;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrappedLight = saturate(ndl * 0.58h + 0.42h);
                half shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half safeShadow = max(shadowAtten, 0.62h);
                half lightPresence = saturate(max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b) * 8.0h);
                half3 safeLightColor = lerp(half3(0.70h, 0.76h, 0.80h), mainLight.color.rgb, lightPresence);

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half wetness = saturate(wetMask * _Wetness + smoothMask * 0.03h);
                half specPower = lerp(20.0h, 46.0h, wetness);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * (_SpecularStrength + wetness * 0.24h + specGloss.r * 0.025h);

                half3 graded = ApplyHairGrade(baseSample.rgb, cavity);
                graded *= lerp(1.0h, occlusion, _CavityDarkness * 0.18h);

                half visibility = saturate(_MinVisibility + wrappedLight * safeShadow * (1.0h - _MinVisibility));
                half3 coldShadow = graded * lerp(half3(0.84h, 0.86h, 0.84h), _ShadowTint.rgb, 0.14h);
                half3 litColor = lerp(coldShadow, graded * safeLightColor, visibility);
                litColor = max(litColor, graded * _MinVisibility);
                litColor += graded * _AmbientLift * 0.58h;
                litColor += SampleSH(normalWS) * graded * 0.48h;
                litColor += spec * safeLightColor * safeShadow;

                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.2h);
                litColor += rim * _ColdRimStrength * 0.34h * _ColdRimColor.rgb;
                litColor += pow(1.0h - saturate(dot(normalWS, viewDirWS)), 5.0h) * _ColdRimStrength * 0.10h * _ColdRimColor.rgb;
                litColor = saturate((litColor - 0.5h) * (1.0h + _PromoContrast * 0.08h) + 0.5h);
                half3 foggedColor = MixFog(litColor, input.fogFactor);
                litColor = lerp(litColor, foggedColor, 0.62h);

                return half4(saturate(litColor), baseSample.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _ColdRimColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _ColdRimStrength;
                half _CavityInk;
                half _MinVisibility;
                half _AmbientLift;
                half _Smoothness;
                half _SpecularStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
#if UNITY_REVERSED_Z
                output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#else
                output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE);
#endif
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}

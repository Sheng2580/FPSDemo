Shader "FPSDemo/Enemy/S_ZombieClothMobile"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 0.8
        _MaskMap("Enemy Mask RGBA", 2D) = "white" {}
        _SpecGlossMap("Specular Smoothness", 2D) = "white" {}

        _BloodTint("Blood Tint", Color) = (0.16, 0.03, 0.02, 1)
        _ShadowTint("Cold Shadow Tint", Color) = (0.18, 0.21, 0.20, 1)
        _DirtTint("Dirt Tint", Color) = (0.18, 0.15, 0.11, 1)
        _GroundTint("Ground Dirt Tint", Color) = (0.12, 0.17, 0.15, 1)
        _CharacterFillColor("Character Soft Fill", Color) = (0.48, 0.60, 0.60, 1)
        _ColdRimColor("Cold Rim Color", Color) = (0.66, 0.82, 0.92, 1)
        [HDR] _WoundGlowColor("Wound Glow Color", Color) = (0.75, 0.08, 0.025, 1)

        _Desaturation("Cloth Desaturation", Range(0, 1)) = 0.18
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.22
        _BloodAmount("Blood Darken", Range(0, 1)) = 0.10
        _Wetness("Wetness", Range(0, 1)) = 0.04
        _CavityDarkness("Fold Darkness", Range(0, 1)) = 0.22
        _HeadContrastBoost("Readability Boost", Range(0, 1)) = 0.08
        _RimDamp("Damp Rim", Range(0, 1)) = 0.10
        _PromoContrast("Cloth Contrast", Range(0, 1)) = 0.10
        _ColdRimStrength("Cold Rim Strength", Range(0, 2)) = 0.22
        _WoundRedBoost("Blood Red Boost", Range(0, 1)) = 0.06
        _WoundGlowIntensity("Wound Glow Intensity", Range(0, 4)) = 0.05
        _WoundGlowSpread("Wound Glow Spread", Range(0, 1)) = 0.18
        _CavityInk("Cavity Ink", Range(0, 1)) = 0.02
        _GroundDirtStrength("Ground Dirt Strength", Range(0, 1)) = 0.42
        _GroundShadowStrength("Ground Shadow Strength", Range(0, 1)) = 0.24
        _ContactShadowStrength("Soft Contact Shadow", Range(0, 1)) = 0.14
        _GroundBlendHeight("Ground Blend Height", Range(0.1, 2)) = 0.85
        _MinVisibility("Minimum Visibility", Range(0, 1)) = 0.50
        _AmbientLift("Ambient Lift", Range(0, 1)) = 0.55
        _CharacterFillStrength("Character Fill Strength", Range(0, 1)) = 0.18
        _FocusLift("Head Chest Focus Lift", Range(0, 1)) = 0.18
        _Smoothness("Base Smoothness", Range(0, 1)) = 0.10
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.035
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
                half4 _BloodTint;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _GroundTint;
                half4 _CharacterFillColor;
                half4 _ColdRimColor;
                half4 _WoundGlowColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _BloodAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _ColdRimStrength;
                half _WoundRedBoost;
                half _WoundGlowIntensity;
                half _WoundGlowSpread;
                half _CavityInk;
                half _GroundDirtStrength;
                half _GroundShadowStrength;
                half _ContactShadowStrength;
                half _GroundBlendHeight;
                half _MinVisibility;
                half _AmbientLift;
                half _CharacterFillStrength;
                half _FocusLift;
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
                half groundMask : TEXCOORD5;
                half focusMask : TEXCOORD6;
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
                half rootY = (half)TransformObjectToWorld(float3(0.0, 0.0, 0.0)).y;
                half heightFromRoot = max(0.0h, (half)positionInputs.positionWS.y - rootY);
                output.groundMask = saturate(1.0h - heightFromRoot / max(_GroundBlendHeight, 0.05h));
                output.focusMask = smoothstep(0.34h, 1.34h, heightFromRoot);
                return output;
            }

            half3 ApplyClothGrade(half3 color, half cavity, half wetMask, half bloodMask)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half3 cloth = lerp(color, luma.xxx, _Desaturation * 0.22h);

                half dirtWeight = saturate(cavity * _DirtAmount);
                cloth *= lerp(1.04h, 0.82h, dirtWeight);
                cloth = lerp(cloth, cloth * (_DirtTint.rgb * 2.15h), dirtWeight * 0.12h);

                half bloodWeight = saturate(bloodMask * (_BloodAmount + _WoundRedBoost * 0.22h));
                cloth = lerp(cloth, cloth * 0.58h + _BloodTint.rgb * 0.38h, bloodWeight * 0.32h);
                cloth = saturate((cloth - 0.5h) * (1.0h + _PromoContrast * 0.16h) + 0.5h);
                return cloth;
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
                half redBias = saturate(baseSample.r * 1.12h - baseSample.g * 0.86h - baseSample.b * 0.34h);
                half bloodMask = saturate(wetMask * 0.45h + redBias * 0.18h + specGloss.r * wetMask * 0.15h);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrappedLight = saturate(ndl * 0.66h + 0.34h);
                half shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half safeShadow = max(shadowAtten, 0.58h);
                half lightPresence = saturate(max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b) * 8.0h);
                half3 safeLightColor = lerp(half3(0.74h, 0.80h, 0.84h), mainLight.color.rgb, lightPresence);

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half wetness = saturate(wetMask * _Wetness + smoothMask * 0.04h);
                half specPower = lerp(18.0h, 38.0h, wetness);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * (_SpecularStrength + wetness * 0.22h);

                half3 graded = ApplyClothGrade(baseSample.rgb, cavity, wetMask, bloodMask);
                graded *= lerp(1.0h, occlusion, _CavityDarkness * 0.26h);
                graded *= 1.0h - saturate(cavity * _CavityInk * 0.08h);
                graded = lerp(graded, graded * 1.06h + 0.022h, saturate(_HeadContrastBoost + input.focusMask * _FocusLift));

                half visibility = saturate(_MinVisibility + wrappedLight * safeShadow * (1.0h - _MinVisibility));
                half3 coldShadow = graded * lerp(half3(0.82h, 0.86h, 0.84h), _ShadowTint.rgb, 0.12h);
                half3 litColor = lerp(coldShadow, graded * safeLightColor, visibility);
                litColor = max(litColor, graded * _MinVisibility);
                litColor += graded * _AmbientLift * 0.55h;
                litColor += SampleSH(normalWS) * graded * 0.45h;
                litColor += spec * safeLightColor * safeShadow;
                half cameraFill = smoothstep(-0.10h, 0.78h, dot(normalWS, viewDirWS));
                half fillFocus = saturate(0.45h + input.focusMask * 0.55h);
                litColor += (graded * 0.52h + _CharacterFillColor.rgb * 0.48h) * cameraFill * fillFocus * _CharacterFillStrength;

                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.5h);
                litColor += rim * _ColdRimStrength * 0.30h * _ColdRimColor.rgb;
                litColor += pow(1.0h - saturate(dot(normalWS, viewDirWS)), 5.0h) * _ColdRimStrength * 0.12h * _ColdRimColor.rgb;
                half groundBlend = saturate(input.groundMask * _GroundDirtStrength);
                litColor = lerp(litColor, litColor * (_GroundTint.rgb * 1.60h), groundBlend);
                half contactShadow = smoothstep(0.64h, 1.0h, input.groundMask);
                litColor *= 1.0h - contactShadow * _ContactShadowStrength * 0.55h;
                litColor *= 1.0h - input.groundMask * _GroundShadowStrength * 0.18h;

                half glowMask = smoothstep(0.45h, 0.92h, bloodMask) * _WoundGlowSpread;
                litColor += _WoundGlowColor.rgb * glowMask * _WoundGlowIntensity;
                litColor = saturate((litColor - 0.5h) * (1.0h + _PromoContrast * 0.10h) + 0.5h);
                half3 foggedColor = MixFog(litColor, input.fogFactor);
                litColor = lerp(litColor, foggedColor, 0.60h);

                return half4(min(litColor, half3(2.0h, 2.0h, 2.0h)), baseSample.a);
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
                half4 _BloodTint;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _GroundTint;
                half4 _CharacterFillColor;
                half4 _ColdRimColor;
                half4 _WoundGlowColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _BloodAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _ColdRimStrength;
                half _WoundRedBoost;
                half _WoundGlowIntensity;
                half _WoundGlowSpread;
                half _CavityInk;
                half _GroundDirtStrength;
                half _GroundShadowStrength;
                half _ContactShadowStrength;
                half _GroundBlendHeight;
                half _MinVisibility;
                half _AmbientLift;
                half _CharacterFillStrength;
                half _FocusLift;
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

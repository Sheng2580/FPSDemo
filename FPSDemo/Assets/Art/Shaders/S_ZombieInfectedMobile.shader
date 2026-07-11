Shader "FPSDemo/Enemy/S_ZombieInfectedMobile"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1
        _MaskMap("Enemy Mask RGBA", 2D) = "white" {}
        _SpecGlossMap("Specular Smoothness", 2D) = "white" {}

        _InfectionTint("Infection Tint", Color) = (0.43, 0.50, 0.35, 1)
        _BloodTint("Blood Tint", Color) = (0.19, 0.035, 0.025, 1)
        _ShadowTint("Cold Shadow Tint", Color) = (0.18, 0.23, 0.20, 1)
        _DirtTint("Dirt Tint", Color) = (0.18, 0.15, 0.11, 1)
        _GroundTint("Ground Dirt Tint", Color) = (0.12, 0.17, 0.15, 1)
        _CharacterFillColor("Character Soft Fill", Color) = (0.52, 0.64, 0.62, 1)

        _Desaturation("Sick Desaturation", Range(0, 1)) = 0.35
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.45
        _InfectionAmount("Infection Amount", Range(0, 1)) = 0.35
        _BloodAmount("Blood Darken", Range(0, 1)) = 0.25
        _Wetness("Wetness", Range(0, 1)) = 0.28
        _CavityDarkness("Cavity Darkness", Range(0, 1)) = 0.55
        _HeadContrastBoost("Readability Boost", Range(0, 1)) = 0.16
        _RimDamp("Damp Rim", Range(0, 1)) = 0.12
        _PromoContrast("Promo Contrast", Range(0, 1)) = 0.35
        _PromoPale("Promo Pale Skin", Range(0, 1)) = 0.35
        _ColdRimColor("Cold Rim Color", Color) = (0.72, 0.92, 1, 1)
        _ColdRimStrength("Cold Rim Strength", Range(0, 2)) = 0.55
        _WoundRedBoost("Wound Red Boost", Range(0, 1)) = 0.35
        [HDR] _WoundGlowColor("Wound Glow Color", Color) = (1.35, 0.18, 0.04, 1)
        _WoundGlowIntensity("Wound Glow Intensity", Range(0, 4)) = 0.75
        _WoundGlowSpread("Wound Glow Spread", Range(0, 1)) = 0.35
        [HDR] _EyeGlowColor("Eye Glow Color", Color) = (0.55, 1.05, 0.78, 1)
        _EyeGlowIntensity("Eye Glow Intensity", Range(0, 4)) = 0.85
        _EyeGlowThreshold("Eye Glow Threshold", Range(0, 1)) = 0.52
        _EyeGlowSpread("Eye Glow Spread", Range(0, 1)) = 0.32
        _CavityInk("Cavity Ink", Range(0, 1)) = 0.30
        _GroundDirtStrength("Ground Dirt Strength", Range(0, 1)) = 0.34
        _GroundShadowStrength("Ground Shadow Strength", Range(0, 1)) = 0.24
        _ContactShadowStrength("Soft Contact Shadow", Range(0, 1)) = 0.18
        _GroundBlendHeight("Ground Blend Height", Range(0.1, 2)) = 0.75
        _MinVisibility("Minimum Visibility", Range(0, 1)) = 0.42
        _AmbientLift("Ambient Lift", Range(0, 1)) = 0.32
        _CharacterFillStrength("Character Fill Strength", Range(0, 1)) = 0.22
        _FocusLift("Head Chest Focus Lift", Range(0, 1)) = 0.22
        _Smoothness("Base Smoothness", Range(0, 1)) = 0.25
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.18
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
                half4 _InfectionTint;
                half4 _BloodTint;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _GroundTint;
                half4 _CharacterFillColor;
                half4 _ColdRimColor;
                half4 _WoundGlowColor;
                half4 _EyeGlowColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _InfectionAmount;
                half _BloodAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _PromoPale;
                half _ColdRimStrength;
                half _WoundRedBoost;
                half _WoundGlowIntensity;
                half _WoundGlowSpread;
                half _EyeGlowIntensity;
                half _EyeGlowThreshold;
                half _EyeGlowSpread;
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
                output.focusMask = smoothstep(0.38h, 1.42h, heightFromRoot);
                return output;
            }

            half3 ApplyEnemyColorGrade(half3 color, half dirt, half infection, half blood)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half3 sickBase = lerp(color, luma.xxx, _Desaturation * 0.48h);
                half3 paleSkin = color * half3(0.92h, 0.96h, 0.86h) + 0.055h;
                sickBase = lerp(sickBase, paleSkin, saturate(_PromoPale * 0.48h));

                half dirtWeight = saturate(dirt * _DirtAmount);
                sickBase *= lerp(1.0h, 0.74h, dirtWeight * 0.62h);
                sickBase = lerp(sickBase, sickBase * (_DirtTint.rgb * 2.05h), dirtWeight * 0.20h);

                half infectionWeight = saturate(infection * _InfectionAmount * 0.45h);
                sickBase = lerp(sickBase, sickBase * 0.78h + _InfectionTint.rgb * 0.32h, infectionWeight);

                half bloodWeight = saturate(blood * (_BloodAmount + _WoundRedBoost * 0.32h));
                sickBase = lerp(sickBase, sickBase * 0.45h + _BloodTint.rgb * 0.70h, bloodWeight);
                sickBase = saturate((sickBase - 0.5h) * (1.0h + _PromoContrast * 0.32h) + 0.5h);
                return sickBase;
            }

            half BuildWoundGlowMask(half3 baseColor, half wetMask, half bloodMask, half infectionMask, half cavity)
            {
                half redBias = saturate(baseColor.r * 1.35h - baseColor.g * 0.82h - baseColor.b * 0.42h);
                half woundBody = saturate(bloodMask * 0.92h + wetMask * 0.35h + redBias * 0.42h + infectionMask * 0.12h);
                woundBody *= saturate(0.38h + cavity * 0.85h);
                woundBody = smoothstep(0.20h, 0.82h, woundBody);

                half innerGlow = woundBody * woundBody;
                half softEdge = woundBody * _WoundGlowSpread * 0.45h;
                return saturate(innerGlow + softEdge);
            }

            half BuildEyeGlowMask(half3 baseColor, half wetMask, half smoothMask, half bloodMask, half infectionMask, half cavity)
            {
                half maxChannel = max(max(baseColor.r, baseColor.g), baseColor.b);
                half minChannel = min(min(baseColor.r, baseColor.g), baseColor.b);
                half saturation = saturate((maxChannel - minChannel) / max(maxChannel, 0.06h));
                half luma = dot(baseColor, half3(0.299h, 0.587h, 0.114h));

                half paleEye = smoothstep(_EyeGlowThreshold, 0.96h, luma);
                paleEye *= 1.0h - smoothstep(0.28h, 0.72h, saturation);

                half warmEye = baseColor.g * 0.80h + baseColor.r * 0.48h - baseColor.b * 0.22h;
                warmEye = smoothstep(_EyeGlowThreshold * 0.78h, 0.90h, warmEye);

                half redReject = saturate(baseColor.r * 1.18h - baseColor.g * 0.82h - baseColor.b * 0.28h);
                half wetGate = saturate(wetMask * 1.15h + smoothMask * 0.82h + infectionMask * 0.22h + 0.08h);
                half woundReject = 1.0h - saturate(bloodMask * 1.22h + cavity * 0.12h + redReject * 0.58h);
                half eyeBody = saturate((paleEye + warmEye * 0.55h) * wetGate * woundReject);
                half eyeCore = smoothstep(0.16h, 0.70h, eyeBody);
                return saturate(eyeCore + eyeCore * _EyeGlowSpread * 0.52h);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, input.uv);
                half4 specGloss = SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, input.uv);

                half occlusion = saturate(mask.r);
                half wetMask = saturate(mask.g);
                half infectionMask = saturate(mask.b);
                half smoothMask = saturate(mask.a);
                half redBias = saturate(baseSample.r * 1.24h - baseSample.g * 0.82h - baseSample.b * 0.38h);
                half bloodMask = saturate(wetMask * 0.58h + redBias * 0.36h + specGloss.r * wetMask * 0.25h);
                half cavity = 1.0h - occlusion;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrappedLight = saturate(ndl * 0.72h + 0.28h);
                half shadowAtten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;
                half safeShadow = max(shadowAtten, 0.52h);
                half lightPresence = saturate(max(max(mainLight.color.r, mainLight.color.g), mainLight.color.b) * 8.0h);
                half3 safeLightColor = lerp(half3(0.72h, 0.78h, 0.84h), mainLight.color.rgb, lightPresence);

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specPower = lerp(16.0h, 48.0h, saturate(_Wetness + smoothMask));
                half wetness = saturate((wetMask * _Wetness) + smoothMask * 0.18h);
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * (_SpecularStrength + wetness * 0.45h);

                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), 3.0h) * _RimDamp * wetness;
                half3 graded = ApplyEnemyColorGrade(baseSample.rgb, cavity, infectionMask, bloodMask);
                graded *= lerp(1.0h, occlusion, _CavityDarkness * 0.34h);
                graded *= 1.0h - saturate(cavity * _CavityInk * 0.18h);
                graded = lerp(graded, graded * 1.08h + 0.028h, saturate(_HeadContrastBoost + input.focusMask * _FocusLift));
                half woundGlowMask = BuildWoundGlowMask(baseSample.rgb, wetMask, bloodMask, infectionMask, cavity);
                half woundGlowEnergy = _WoundGlowIntensity * saturate(_BloodAmount * 2.0h + _Wetness * 0.7h);
                half eyeGlowMask = BuildEyeGlowMask(baseSample.rgb, wetMask, smoothMask, bloodMask, infectionMask, cavity);
                half eyeGlowEnergy = _EyeGlowIntensity * saturate(0.68h + infectionMask * 0.36h + wetMask * 0.24h);

                half visibility = saturate(_MinVisibility + wrappedLight * safeShadow * (1.0h - _MinVisibility));
                half3 coldShadow = graded * lerp(half3(0.76h, 0.80h, 0.78h), _ShadowTint.rgb, 0.22h);
                half3 litColor = lerp(coldShadow, graded * safeLightColor, visibility);
                litColor = max(litColor, graded * _MinVisibility);
                litColor += graded * _AmbientLift * 0.52h;
                litColor += spec * safeLightColor * safeShadow;
                half cameraFill = smoothstep(-0.12h, 0.78h, dot(normalWS, viewDirWS));
                half fillFocus = saturate(0.42h + input.focusMask * 0.58h);
                litColor += (graded * 0.46h + _CharacterFillColor.rgb * 0.54h) * cameraFill * fillFocus * _CharacterFillStrength;
                litColor += rim * lerp(_BloodTint.rgb, _ColdRimColor.rgb, saturate(_ColdRimStrength * 0.75h));
                litColor += pow(1.0h - saturate(dot(normalWS, viewDirWS)), 4.0h) * _ColdRimStrength * 0.44h * _ColdRimColor.rgb * saturate(wrappedLight + 0.32h);
                litColor += pow(1.0h - saturate(dot(normalWS, viewDirWS)), 6.0h) * _ColdRimStrength * 0.18h * _ColdRimColor.rgb;
                litColor += SampleSH(normalWS) * graded * 0.45h;
                half groundBlend = saturate(input.groundMask * _GroundDirtStrength);
                litColor = lerp(litColor, litColor * (_GroundTint.rgb * 1.55h), groundBlend);
                half contactShadow = smoothstep(0.62h, 1.0h, input.groundMask);
                litColor *= 1.0h - contactShadow * _ContactShadowStrength * 0.55h;
                litColor *= 1.0h - input.groundMask * _GroundShadowStrength * 0.18h;
                litColor = saturate((litColor - 0.5h) * (1.0h + _PromoContrast * 0.18h) + 0.5h);
                litColor += _WoundGlowColor.rgb * woundGlowMask * woundGlowEnergy;
                litColor += _EyeGlowColor.rgb * eyeGlowMask * eyeGlowEnergy;
                half3 foggedColor = MixFog(litColor, input.fogFactor);
                litColor = lerp(litColor, foggedColor, 0.58h);

                return half4(min(litColor, half3(4.0h, 4.0h, 4.0h)), baseSample.a);
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
                half4 _InfectionTint;
                half4 _BloodTint;
                half4 _ShadowTint;
                half4 _DirtTint;
                half4 _GroundTint;
                half4 _CharacterFillColor;
                half4 _ColdRimColor;
                half4 _WoundGlowColor;
                half4 _EyeGlowColor;
                half _BumpScale;
                half _Desaturation;
                half _DirtAmount;
                half _InfectionAmount;
                half _BloodAmount;
                half _Wetness;
                half _CavityDarkness;
                half _HeadContrastBoost;
                half _RimDamp;
                half _PromoContrast;
                half _PromoPale;
                half _ColdRimStrength;
                half _WoundRedBoost;
                half _WoundGlowIntensity;
                half _WoundGlowSpread;
                half _EyeGlowIntensity;
                half _EyeGlowThreshold;
                half _EyeGlowSpread;
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

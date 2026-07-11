Shader "FPSDemo/Scene/S_SceneGrimyMobile"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _BaseColorMap("Base Color Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [Normal] _NormalMap("Normal Map", 2D) = "bump" {}
        _BumpScale("Normal Strength", Range(0, 2)) = 1
        _ORMH("ORMH / Mask", 2D) = "white" {}
        _OcclusionMap("Occlusion Map", 2D) = "white" {}

        _SceneShadowTint("Shared Shadow Tint", Color) = (0.12, 0.18, 0.16, 1)
        _SceneFogTint("Shared Fog Tint", Color) = (0.30, 0.20, 0.23, 1)
        _DirtTint("Dirt Tint", Color) = (0.17, 0.13, 0.10, 1)
        _WetTint("Wet Tint", Color) = (0.18, 0.24, 0.22, 1)

        _Desaturation("Scene Desaturation", Range(0, 1)) = 0.18
        _Contrast("Scene Contrast", Range(0, 1)) = 0.16
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.28
        _Wetness("Wetness", Range(0, 1)) = 0.18
        _CavityDarkness("Cavity Darkness", Range(0, 1)) = 0.28
        _ShadowBlend("Shadow Color Blend", Range(0, 1)) = 0.32
        _FogColorBlend("Fog Color Blend", Range(0, 1)) = 0.16
        _LightmapStrength("Lightmap Strength", Range(0, 2)) = 1.05
        _AmbientFloor("Ambient Floor", Range(0, 1)) = 0.18
        _VisibilityLift("Minimum Scene Visibility", Range(0, 1)) = 0.22
        _SpecularStrength("Specular Strength", Range(0, 1)) = 0.08
        _Smoothness("Smoothness", Range(0, 1)) = 0.26
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
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);
            TEXTURE2D(_NormalMap);
            SAMPLER(sampler_NormalMap);
            TEXTURE2D(_ORMH);
            SAMPLER(sampler_ORMH);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SceneShadowTint;
                half4 _SceneFogTint;
                half4 _DirtTint;
                half4 _WetTint;
                half _BumpScale;
                half _Desaturation;
                half _Contrast;
                half _DirtAmount;
                half _Wetness;
                half _CavityDarkness;
                half _ShadowBlend;
                half _FogColorBlend;
                half _LightmapStrength;
                half _AmbientFloor;
                half _VisibilityLift;
                half _SpecularStrength;
                half _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half3 normalOS : NORMAL;
                half4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half4 tangentWS : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
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
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            half3 GradeSceneBase(half3 color, half cavity, half wetMask)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half3 graded = lerp(color, luma.xxx, _Desaturation);
                graded = saturate((graded - 0.5h) * (1.0h + _Contrast * 0.45h) + 0.5h);

                half dirt = saturate(cavity * _DirtAmount + (1.0h - luma) * _DirtAmount * 0.22h);
                graded = lerp(graded, graded * (_DirtTint.rgb * 1.55h), dirt);
                graded = lerp(graded, graded * (_WetTint.rgb * 1.24h), wetMask * _Wetness);
                graded *= 1.0h - saturate(cavity * _CavityDarkness * 0.38h);
                return graded;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseColorMap = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv).rgb;
                half baseHasColor = step(0.02h, dot(baseSample.rgb, half3(0.333h, 0.333h, 0.333h)));
                half3 albedo = lerp(baseColorMap, baseSample.rgb, baseHasColor) * _BaseColor.rgb;

                half4 mask = SAMPLE_TEXTURE2D(_ORMH, sampler_ORMH, input.uv);
                half occlusion = saturate(mask.r * SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, input.uv).r);
                half wetMask = saturate(mask.a + (1.0h - mask.g) * 0.22h);
                half cavity = 1.0h - occlusion;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv), _BumpScale);
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS)));

                half3 bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, normalWS);
                bakedGI = max(bakedGI * _LightmapStrength, _AmbientFloor.xxx);
                half lightAmount = saturate(max(max(bakedGI.r, bakedGI.g), bakedGI.b));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(normalWS, mainLight.direction));
                half wrappedLight = saturate(ndl * 0.62h + 0.18h);

                half3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half specPower = lerp(18.0h, 56.0h, saturate(_Smoothness + wetMask * _Wetness));
                half spec = pow(saturate(dot(normalWS, halfDir)), specPower) * (_SpecularStrength + wetMask * _Wetness * 0.18h);

                half3 graded = GradeSceneBase(albedo, cavity, wetMask);
                half shadowWeight = saturate((1.0h - lightAmount) * _ShadowBlend + cavity * _ShadowBlend * 0.35h);
                half3 shadowColor = graded * lerp(half3(0.46h, 0.52h, 0.48h), _SceneShadowTint.rgb * 1.72h, 0.42h);
                half3 litColor = lerp(graded * bakedGI, shadowColor, shadowWeight);
                litColor += graded * mainLight.color.rgb * wrappedLight * mainLight.distanceAttenuation * mainLight.shadowAttenuation * 0.12h;
                litColor += spec * mainLight.color.rgb * saturate(mainLight.shadowAttenuation + 0.35h);
                litColor = max(litColor, graded * _VisibilityLift);

                half fogWash = saturate(input.fogFactor * _FogColorBlend + cavity * 0.06h);
                litColor = lerp(litColor, litColor * 0.68h + _SceneFogTint.rgb * 0.32h, fogWash);
                litColor = MixFog(litColor, input.fogFactor);
                return half4(min(litColor, half3(2.0h, 2.0h, 2.0h)), 1);
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

        Pass
        {
            Name "Meta"
            Tags { "LightMode" = "Meta" }

            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex UniversalVertexMeta
            #pragma fragment FragMeta

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _SceneShadowTint;
                half4 _SceneFogTint;
                half4 _DirtTint;
                half4 _WetTint;
                half _BumpScale;
                half _Desaturation;
                half _Contrast;
                half _DirtAmount;
                half _Wetness;
                half _CavityDarkness;
                half _ShadowBlend;
                half _FogColorBlend;
                half _LightmapStrength;
                half _AmbientFloor;
                half _VisibilityLift;
                half _SpecularStrength;
                half _Smoothness;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 FragMeta(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half3 baseColorMap = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv).rgb;
                half baseHasColor = step(0.02h, dot(baseSample.rgb, half3(0.333h, 0.333h, 0.333h)));

                MetaInput metaInput;
                metaInput.Albedo = lerp(baseColorMap, baseSample.rgb, baseHasColor) * _BaseColor.rgb;
                metaInput.Emission = 0;
                return UniversalFragmentMeta(input, metaInput);
            }
            ENDHLSL
        }
    }
}

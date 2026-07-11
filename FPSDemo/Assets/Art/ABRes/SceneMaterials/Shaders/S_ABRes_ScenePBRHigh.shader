Shader "FPSDemo/Scene/S_ABRes_ScenePBRHigh"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        _BaseColorMap("Base Color Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _Tint("Art Tint", Color) = (1, 1, 1, 1)
        [Normal] _BumpMap("Normal Map", 2D) = "bump" {}
        _NormalStrength("Normal Strength", Range(0, 4)) = 1
        _ORMH("RMA Map (R Roughness, G Metallic, B AO)", 2D) = "white" {}
        _OcclusionMap("Occlusion Map", 2D) = "white" {}
        _AOIntensity("AO Intensity", Range(0, 2)) = 1
        _Metallic("Metallic", Range(0, 1)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.45
        _Saturation("Saturation", Range(0, 2)) = 1
        _Contrast("Contrast", Range(0, 2)) = 1
        _Wetness("Wetness", Range(0, 1)) = 0.08
        _WetTint("Wet Tint", Color) = (0.18, 0.22, 0.20, 1)
        _DirtAmount("Dirt Amount", Range(0, 1)) = 0.12
        _DirtTint("Dirt Tint", Color) = (0.20, 0.16, 0.11, 1)
        _ShadowLift("Shadow Visibility Lift", Range(0, 1)) = 0.34
        _ShadowFloor("Shadow Visibility Floor", Range(0, 0.25)) = 0.09
        _ShadowTint("Shadow Fill Tint", Color) = (0.32, 0.38, 0.34, 1)
        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        _EmissionStrength("Emission Strength", Range(0, 8)) = 0
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
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BaseColorMap);
            SAMPLER(sampler_BaseColorMap);
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ORMH);
            SAMPLER(sampler_ORMH);
            TEXTURE2D(_OcclusionMap);
            SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Tint;
                half4 _WetTint;
                half4 _DirtTint;
                half4 _ShadowTint;
                half4 _EmissionColor;
                half _NormalStrength;
                half _AOIntensity;
                half _Metallic;
                half _Smoothness;
                half _Saturation;
                half _Contrast;
                half _Wetness;
                half _DirtAmount;
                half _ShadowLift;
                half _ShadowFloor;
                half _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 staticLightmapUV : TEXCOORD1;
                float2 dynamicLightmapUV : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half fogFactor : TEXCOORD4;
                half3 vertexLighting : TEXCOORD5;
                float4 shadowCoord : TEXCOORD6;
                DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
            #ifdef DYNAMICLIGHTMAP_ON
                float2 dynamicLightmapUV : TEXCOORD8;
            #endif
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            half3 ApplySceneMaterialGrade(half3 color, half occlusion, half wetMask)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                color = lerp(luma.xxx, color, _Saturation);
                color = saturate((color - 0.5h) * _Contrast + 0.5h);

                half cavity = saturate(1.0h - occlusion);
                half dirt = saturate(cavity * _DirtAmount);
                color = lerp(color, color * (_DirtTint.rgb * 1.65h), dirt);
                color = lerp(color, color * 0.82h + _WetTint.rgb * 0.18h, wetMask * _Wetness);
                return max(color, half3(0.015h, 0.015h, 0.015h));
            }

            half3 ApplyShadowVisibilityFloor(half3 color, half3 albedo)
            {
                half luma = dot(color, half3(0.299h, 0.587h, 0.114h));
                half darkWeight = saturate((0.55h - luma) * 2.35h);
                half3 tintedFill = albedo * _ShadowTint.rgb * _ShadowLift;
                half3 detailLift = color + _ShadowTint.rgb * (_ShadowLift * 0.12h);
                half3 absoluteFill = _ShadowTint.rgb * _ShadowFloor;
                half3 lifted = max(detailLift, max(tintedFill, absoluteFill));
                return lerp(color, lifted, darkWeight);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.positionWS = positionInputs.positionWS;
                output.positionCS = positionInputs.positionCS;
                output.normalWS = normalInputs.normalWS;
                output.tangentWS = half4(normalInputs.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                output.vertexLighting = VertexLighting(positionInputs.positionWS, normalInputs.normalWS);
                output.shadowCoord = GetShadowCoord(positionInputs);
                OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
            #ifdef DYNAMICLIGHTMAP_ON
                output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
            #endif
                OUTPUT_SH(output.normalWS, output.vertexSH);
                return output;
            }

            void InitializePBRInputData(Varyings input, half3 normalTS, out InputData inputData)
            {
                inputData = (InputData)0;
                half3 bitangentWS = cross(input.normalWS, input.tangentWS.xyz) * input.tangentWS.w;
                half3x3 tangentToWorld = half3x3(input.tangentWS.xyz, bitangentWS, input.normalWS.xyz);

                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.tangentToWorld = tangentToWorld;
                inputData.normalWS = NormalizeNormalPerPixel(TransformTangentToWorld(normalTS, tangentToWorld));
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = input.shadowCoord;
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.vertexLighting = input.vertexLighting;
            #ifdef DYNAMICLIGHTMAP_ON
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.dynamicLightmapUV, input.vertexSH, inputData.normalWS);
            #else
                inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
            #endif
                inputData.bakedGI = max(inputData.bakedGI, _ShadowTint.rgb * (_ShadowLift * 0.34h));
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColorSample = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv);
                half useBaseMap = step(0.02h, dot(baseSample.rgb, half3(0.333h, 0.333h, 0.333h)));
                half3 albedo = lerp(baseColorSample.rgb, baseSample.rgb, useBaseMap) * _BaseColor.rgb * _Tint.rgb;

                half4 mask = SAMPLE_TEXTURE2D(_ORMH, sampler_ORMH, input.uv);
                half occlusion = lerp(1.0h, saturate(mask.b), saturate(_AOIntensity));
                half roughness = saturate(mask.r);
                half metalMask = saturate(mask.g);
                half wetMask = saturate((1.0h - roughness) * 0.55h);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _NormalStrength);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = ApplySceneMaterialGrade(albedo, occlusion, wetMask);
                surfaceData.metallic = saturate(metalMask * _Metallic);
                surfaceData.specular = half3(0.04h, 0.04h, 0.04h);
                surfaceData.smoothness = saturate((1.0h - roughness) * _Smoothness + wetMask * _Wetness * 0.35h);
                surfaceData.normalTS = normalTS;
                surfaceData.emission = _EmissionColor.rgb * _EmissionStrength;
                surfaceData.occlusion = occlusion;
                surfaceData.alpha = 1.0h;
                surfaceData.clearCoatMask = 0.0h;
                surfaceData.clearCoatSmoothness = 0.0h;

                InputData inputData;
                InitializePBRInputData(input, normalTS, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = ApplyShadowVisibilityFloor(color.rgb, surfaceData.albedo);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0h;
                return color;
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
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Tint;
                half4 _WetTint;
                half4 _DirtTint;
                half4 _ShadowTint;
                half4 _EmissionColor;
                half _NormalStrength;
                half _AOIntensity;
                half _Metallic;
                half _Smoothness;
                half _Saturation;
                half _Contrast;
                half _Wetness;
                half _DirtAmount;
                half _ShadowLift;
                half _ShadowFloor;
                half _EmissionStrength;
            CBUFFER_END
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthOnlyVertex
            #pragma fragment DepthOnlyFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Tint;
                half4 _WetTint;
                half4 _DirtTint;
                half4 _ShadowTint;
                half4 _EmissionColor;
                half _NormalStrength;
                half _AOIntensity;
                half _Metallic;
                half _Smoothness;
                half _Saturation;
                half _Contrast;
                half _Wetness;
                half _DirtAmount;
                half _ShadowLift;
                half _ShadowFloor;
                half _EmissionStrength;
            CBUFFER_END
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _Tint;
                half4 _WetTint;
                half4 _DirtTint;
                half4 _ShadowTint;
                half4 _EmissionColor;
                half _NormalStrength;
                half _AOIntensity;
                half _Metallic;
                half _Smoothness;
                half _Saturation;
                half _Contrast;
                half _Wetness;
                half _DirtAmount;
                half _ShadowLift;
                half _ShadowFloor;
                half _EmissionStrength;
            CBUFFER_END
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"
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
                half4 _Tint;
                half4 _WetTint;
                half4 _DirtTint;
                half4 _ShadowTint;
                half4 _EmissionColor;
                half _NormalStrength;
                half _AOIntensity;
                half _Metallic;
                half _Smoothness;
                half _Saturation;
                half _Contrast;
                half _Wetness;
                half _DirtAmount;
                half _ShadowLift;
                half _ShadowFloor;
                half _EmissionStrength;
            CBUFFER_END

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UniversalMetaPass.hlsl"

            half4 FragMeta(Varyings input) : SV_Target
            {
                half4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColorSample = SAMPLE_TEXTURE2D(_BaseColorMap, sampler_BaseColorMap, input.uv);
                half useBaseMap = step(0.02h, dot(baseSample.rgb, half3(0.333h, 0.333h, 0.333h)));

                MetaInput metaInput;
                metaInput.Albedo = lerp(baseColorSample.rgb, baseSample.rgb, useBaseMap) * _BaseColor.rgb * _Tint.rgb;
                metaInput.Emission = _EmissionColor.rgb * _EmissionStrength;
                return UniversalFragmentMeta(input, metaInput);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}

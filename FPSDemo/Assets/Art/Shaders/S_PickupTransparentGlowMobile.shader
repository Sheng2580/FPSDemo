Shader "FPSDemo/Pickup/TransparentGlowMobile"
{
    Properties
    {
        [MainTexture] _BaseMap ("主贴图", 2D) = "white" {}
        [MainColor] _BaseColor ("基础颜色", Color) = (1, 1, 1, 1)
        _EmissionColor ("自发光颜色", Color) = (0.5, 0.5, 0.5, 1)
        _EmissionStrength ("自发光强度", Range(0, 4)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                half _EmissionStrength;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half alpha = saturate(tex.a * _BaseColor.a * input.color.a);
                half3 color = tex.rgb * _BaseColor.rgb * input.color.rgb;
                color += _EmissionColor.rgb * _EmissionStrength * alpha;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}

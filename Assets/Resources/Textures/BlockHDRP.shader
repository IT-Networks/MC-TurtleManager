Shader "HDRP/Block"
{
    Properties
    {
        // Texturen
        _TopTex ("Top Texture", 2D) = "white" {}
        _BottomTex ("Bottom Texture", 2D) = "white" {}
        _FrontTex ("Front Texture (+Z)", 2D) = "white" {}
        _BackTex ("Back Texture (-Z)", 2D) = "white" {}
        _LeftTex ("Left Texture (-X)", 2D) = "white" {}
        _RightTex ("Right Texture (+X)", 2D) = "white" {}

        // Farbüberlagerungen
        _TopColor ("Top Color", Color) = (1,1,1,1)
        _BottomColor ("Bottom Color", Color) = (1,1,1,1)
        _FrontColor ("Front Color", Color) = (1,1,1,1)
        _BackColor ("Back Color", Color) = (1,1,1,1)
        _LeftColor ("Left Color", Color) = (1,1,1,1)
        _RightColor ("Right Color", Color) = (1,1,1,1)

        // Material Properties
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        // Main Forward Pass
        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #pragma vertex Vert
            #pragma fragment Frag

            // Core HDRP includes
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_TopTex);
            SAMPLER(sampler_TopTex);
            TEXTURE2D(_BottomTex);
            SAMPLER(sampler_BottomTex);
            TEXTURE2D(_FrontTex);
            SAMPLER(sampler_FrontTex);
            TEXTURE2D(_BackTex);
            SAMPLER(sampler_BackTex);
            TEXTURE2D(_LeftTex);
            SAMPLER(sampler_LeftTex);
            TEXTURE2D(_RightTex);
            SAMPLER(sampler_RightTex);

            CBUFFER_START(UnityPerMaterial)
            float4 _TopColor;
            float4 _BottomColor;
            float4 _FrontColor;
            float4 _BackColor;
            float4 _LeftColor;
            float4 _RightColor;
            float _Smoothness;
            float _Metallic;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;

                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);

                // Determine dominant axis
                float3 absNormal = abs(normalWS);
                float3 majorAxis = step(absNormal.yzx, absNormal.xyz) * step(absNormal.zxy, absNormal.xyz);

                float topMask    = majorAxis.y * step(0, normalWS.y);
                float bottomMask = majorAxis.y * step(0, -normalWS.y);
                float frontMask  = majorAxis.z * step(0, normalWS.z);
                float backMask   = majorAxis.z * step(0, -normalWS.z);
                float rightMask  = majorAxis.x * step(0, normalWS.x);
                float leftMask   = majorAxis.x * step(0, -normalWS.x);

                // Sample textures
                float4 colTop    = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, input.uv) * _TopColor;
                float4 colBottom = SAMPLE_TEXTURE2D(_BottomTex, sampler_BottomTex, input.uv) * _BottomColor;
                float4 colFront  = SAMPLE_TEXTURE2D(_FrontTex, sampler_FrontTex, input.uv) * _FrontColor;
                float4 colBack   = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, input.uv) * _BackColor;
                float4 colRight  = SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, input.uv) * _RightColor;
                float4 colLeft   = SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, input.uv) * _LeftColor;

                // Blend based on normal
                float4 baseColor = colTop * topMask +
                                  colBottom * bottomMask +
                                  colFront * frontMask +
                                  colBack * backMask +
                                  colRight * rightMask +
                                  colLeft * leftMask;

                // Use main directional light
                float3 lightDir = _MainLightPosition.xyz;
                float NdotL = saturate(dot(normalWS, lightDir));
                float3 lighting = lerp(0.3, 1.0, NdotL); // Ambient + diffuse

                baseColor.rgb *= lighting;

                return baseColor;
            }
            ENDHLSL
        }

        // Shadow Caster Pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        // DepthOnly Pass
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

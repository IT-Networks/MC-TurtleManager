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
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "HDRenderPipeline"
            "RenderType" = "HDUnlitShader"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Back
            ZWrite On
            ZTest LEqual
            Blend One Zero

            HLSLPROGRAM
            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float2 uv : TEXCOORD1;
            };

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

            float4 _TopColor;
            float4 _BottomColor;
            float4 _FrontColor;
            float4 _BackColor;
            float4 _LeftColor;
            float4 _RightColor;

            v2f vert(appdata v)
            {
                v2f o;

                float3 positionWS = TransformObjectToWorld(v.vertex.xyz);
                o.pos = TransformWorldToHClip(positionWS);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.uv = v.uv;

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);

                // Determine which face we're looking at
                float3 absN = abs(n);
                float3 majorAxis = step(absN.yzx, absN.xyz) * step(absN.zxy, absN.xyz);

                float topMask = majorAxis.y * step(0, n.y);
                float bottomMask = majorAxis.y * step(0, -n.y);
                float frontMask = majorAxis.z * step(0, n.z);
                float backMask = majorAxis.z * step(0, -n.z);
                float rightMask = majorAxis.x * step(0, n.x);
                float leftMask = majorAxis.x * step(0, -n.x);

                // Sample textures
                float4 colTop = SAMPLE_TEXTURE2D(_TopTex, sampler_TopTex, i.uv) * _TopColor;
                float4 colBottom = SAMPLE_TEXTURE2D(_BottomTex, sampler_BottomTex, i.uv) * _BottomColor;
                float4 colFront = SAMPLE_TEXTURE2D(_FrontTex, sampler_FrontTex, i.uv) * _FrontColor;
                float4 colBack = SAMPLE_TEXTURE2D(_BackTex, sampler_BackTex, i.uv) * _BackColor;
                float4 colRight = SAMPLE_TEXTURE2D(_RightTex, sampler_RightTex, i.uv) * _RightColor;
                float4 colLeft = SAMPLE_TEXTURE2D(_LeftTex, sampler_LeftTex, i.uv) * _LeftColor;

                // Blend
                float4 color = colTop * topMask +
                              colBottom * bottomMask +
                              colFront * frontMask +
                              colBack * backMask +
                              colRight * rightMask +
                              colLeft * leftMask;

                // Simple lighting
                float3 lightDir = normalize(float3(0.5, 1.0, 0.3));
                float ndl = saturate(dot(n, lightDir));
                float3 lighting = lerp(0.3, 1.0, ndl);

                color.rgb *= lighting;

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}

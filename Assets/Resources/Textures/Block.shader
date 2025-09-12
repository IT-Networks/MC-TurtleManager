Shader "Custom/Block"
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

        // Transparenz-Steuerung
        [Toggle] _UseTransparency ("Use Transparency", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200

        Pass
        {
            ZWrite On
            ZTest LEqual
            Blend One Zero
            Cull Back
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _TopTex;
            sampler2D _BottomTex;
            sampler2D _FrontTex;
            sampler2D _BackTex;
            sampler2D _LeftTex;
            sampler2D _RightTex;

            fixed4 _TopColor;
            fixed4 _BottomColor;
            fixed4 _FrontColor;
            fixed4 _BackColor;
            fixed4 _LeftColor;
            fixed4 _RightColor;

            float _UseTransparency;

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

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);

                // Dominante Achse finden (genau eine Seite wird 1)
                float3 an = abs(n);
                float3 major = step(an.yzx, an.xyz) * step(an.zxy, an.xyz);

                float topMask    = major.y * step(0, n.y);
                float bottomMask = major.y * step(0, -n.y);
                float frontMask  = major.z * step(0, n.z);
                float backMask   = major.z * step(0, -n.z);
                float rightMask  = major.x * step(0, n.x);
                float leftMask   = major.x * step(0, -n.x);

                // Texturen (Mesh-UV verwenden statt worldPos-Projektion)
                fixed4 colTop    = tex2D(_TopTex,    i.uv) * _TopColor;
                fixed4 colBottom = tex2D(_BottomTex, i.uv) * _BottomColor;
                fixed4 colFront  = tex2D(_FrontTex,  i.uv) * _FrontColor;
                fixed4 colBack   = tex2D(_BackTex,   i.uv) * _BackColor;
                fixed4 colRight  = tex2D(_RightTex,  i.uv) * _RightColor;
                fixed4 colLeft   = tex2D(_LeftTex,   i.uv) * _LeftColor;

                // Mischung
                fixed4 col = colTop * topMask +
                             colBottom * bottomMask +
                             colFront * frontMask +
                             colBack * backMask +
                             colRight * rightMask +
                             colLeft * leftMask;

                // Transparenz-Option
                if (_UseTransparency > 0.5)
                {
                    // Blending aktivieren (Alpha aus Textur oder Color nutzen)
                    col.a = col.a;
                }
                else
                {
                    // Alpha auf 1 setzen für Opaque
                    col.a = 1.0;
                }

                return col;
            }
            ENDCG
        }
    }
    
    // Fallback für ältere Hardware
    Fallback "Legacy Shaders/Diffuse"
}
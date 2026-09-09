Shader "MainGame/UI/RoboticCRTOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(CRT Scanlines)]
        _ScanlineDensity ("Scanline Density", Float) = 540.0
        _ScanlineOpacity ("Scanline Opacity", Range(0, 0.1)) = 0.025
        _VignetteStrength ("Vignette Strength", Range(0, 0.3)) = 0.06

        [Header(Transition Glitch)]
        _GlitchIntensity ("Glitch Intensity", Range(0, 1)) = 0.0

        [Header(Stencil and Masking)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent+10"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float _ScanlineDensity;
            float _ScanlineOpacity;
            float _VignetteStrength;
            float _GlitchIntensity;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.worldPos = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Momentary transition micro-jitter (1-2 frames during screen switch)
                float glitchFlash = 0.0;
                if (_GlitchIntensity > 0.001)
                {
                    float slice = floor(uv.y * 48.0);
                    float j = sin(slice * 311.7 + _Time.y * 90.0);
                    if (j > 0.65)
                    {
                        glitchFlash = (j - 0.65) * 0.35 * _GlitchIntensity;
                    }
                }

                // 1. Ultra-subtle alternating 1px scanlines (almost invisible, retro monitor texture)
                float scan = sin(uv.y * _ScanlineDensity * 3.14159);
                float scanDarkening = saturate((scan + 1.0) * 0.5) * _ScanlineOpacity;

                // 2. Subtle corner vignette for depth
                float2 distFromCenter = abs(uv - 0.5) * 2.0;
                float vignette = dot(distFromCenter, distFromCenter) * _VignetteStrength;

                float totalAlpha = saturate(scanDarkening + vignette + glitchFlash * 0.4);
                fixed3 col = lerp(fixed3(0.02, 0.04, 0.08), fixed3(0.35, 0.85, 1.0), saturate(glitchFlash * 2.0));

                // Pure dark pixel tint with calculated subtle alpha
                return fixed4(col, totalAlpha * IN.color.a);
            }
            ENDCG
        }
    }
}

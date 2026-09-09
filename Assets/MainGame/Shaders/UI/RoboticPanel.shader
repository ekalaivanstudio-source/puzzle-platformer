Shader "MainGame/UI/RoboticPanel"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Robotic Edge)]
        _EdgeColor ("Edge Color", Color) = (0.35, 0.78, 1.0, 1.0)
        _EdgeIntensity ("Edge Intensity", Range(0, 4)) = 1.0
        _EdgeWidth ("Edge Width (pixels)", Range(0.5, 6.0)) = 2.0
        _InnerShadowDarkness ("Inner Shadow Darkness", Range(0, 1)) = 0.20
        _InnerShadowWidth ("Inner Shadow Width (pixels)", Range(1.0, 60.0)) = 24.0

        [Header(Robotic Power and Activation)]
        _Activation ("Activation Progress", Range(0, 1)) = 1.0
        _EdgeSweepProgress ("Edge Sweep Progress", Range(-0.5, 1.5)) = -0.5
        _EdgeSweepWidth ("Edge Sweep Width", Range(0.01, 0.5)) = 0.15

        [Header(Digital Scan and Shimmer)]
        _ScanIntensity ("Scan Intensity", Range(0, 2)) = 0.0
        _ScanSpeed ("Scan Speed", Float) = 2.0
        _ScanFrequency ("Scan Frequency", Float) = 25.0
        _NoiseAmount ("Noise Amount", Range(0, 1)) = 0.0
        _GlitchAmount ("Glitch Amount", Range(0, 1)) = 0.0

        [Header(Stencil and Masking)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 uv1      : TEXCOORD1; // uv1.xy = normalized rect UV (0..1), uv1.zw = pixel size (width, height)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 rectUV        : TEXCOORD1; // xy: norm UV, zw: pixel dimensions
                float4 worldPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            fixed4 _EdgeColor;
            float _EdgeIntensity;
            float _EdgeWidth;
            float _InnerShadowDarkness;
            float _InnerShadowWidth;

            float _Activation;
            float _EdgeSweepProgress;
            float _EdgeSweepWidth;

            float _ScanIntensity;
            float _ScanSpeed;
            float _ScanFrequency;
            float _NoiseAmount;
            float _GlitchAmount;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;

                // If RoboticUIMeshModifier injected uv1, use it. Otherwise fallback to texcoord and 100x100.
                bool hasCustomUV1 = (IN.uv1.z > 1.0 && IN.uv1.w > 1.0);
                OUT.rectUV = hasCustomUV1 ? IN.uv1 : float4(IN.texcoord, 100.0, 100.0);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 normUV = IN.rectUV.xy;
                float2 pixelSize = IN.rectUV.zw;

                // 1. Controlled pixel-art horizontal glitch slice offset (signal stabilization)
                if (_GlitchAmount > 0.001)
                {
                    float slice = floor(normUV.y * 48.0);
                    float sliceNoise = sin(slice * 137.5 + _Time.y * 75.0);
                    if (sliceNoise > 0.65)
                    {
                        normUV.x += (sliceNoise - 0.65) * 0.035 * _GlitchAmount;
                    }
                }

                // Sample base texture with uGUI standard tint
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // 2. Physical edge distance in pixels
                float2 distInPixels = min(normUV, 1.0 - normUV) * pixelSize;
                float minEdgeDist = min(distInPixels.x, distInPixels.y);

                // 3. Crisp robotic edge outline
                float edgeOutline = saturate(_EdgeWidth - minEdgeDist + 0.5);

                // 4. Subtle inner shadow for physical terminal depth
                if (_InnerShadowDarkness > 0.001)
                {
                    float innerShadowDist = saturate(minEdgeDist / max(_InnerShadowWidth, 1.0));
                    float shadowMultiplier = lerp(1.0 - _InnerShadowDarkness, 1.0, innerShadowDist);
                    color.rgb *= shadowMultiplier;
                }

                // 5. Edge Sweep highlight (travels along edge during power-up or focus)
                float edgeSweep = 0.0;
                if (_EdgeSweepProgress > -0.4 && _EdgeSweepProgress < 1.4)
                {
                    float sweepDist = abs(normUV.x - _EdgeSweepProgress);
                    edgeSweep = saturate(1.0 - sweepDist / max(_EdgeSweepWidth, 0.001));
                }

                // 6. Digital Scan shimmer (only when explicitly enabled, e.g. robot inspection)
                float scanShimmer = 0.0;
                if (_ScanIntensity > 0.001)
                {
                    float scanPhase = normUV.y * _ScanFrequency - _Time.y * _ScanSpeed * 6.28318;
                    float scanWave = saturate(sin(scanPhase) * 0.5 + 0.5);
                    scanShimmer = pow(scanWave, 6.0) * _ScanIntensity;
                }

                // 7. Subtle digital grain noise
                if (_NoiseAmount > 0.001)
                {
                    float grain = frac(sin(dot(normUV + frac(_Time.y * 10.0), float2(12.9898, 78.233))) * 43758.5453);
                    color.rgb += (grain - 0.5) * _NoiseAmount * 0.15;
                }

                // 8. Combine Edge Illumination and Shimmer with Activation Progress
                float totalEdgeFactor = edgeOutline * (_EdgeIntensity + edgeSweep * 1.5) * _Activation;
                color.rgb += _EdgeColor.rgb * totalEdgeFactor * color.a;

                if (scanShimmer > 0.001)
                {
                    color.rgb += _EdgeColor.rgb * (scanShimmer * _Activation * color.a);
                }

                return color;
            }
            ENDCG
        }
    }
}

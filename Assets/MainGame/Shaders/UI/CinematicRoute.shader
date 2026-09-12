Shader "MainGame/UI/CinematicRoute"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Route Energy Pulse)]
        _PulseProgress ("Pulse Progress (0 to 1)", Range(0, 1)) = 0.0
        _PulseLength ("Pulse Length", Range(0.05, 1.0)) = 0.35
        _PulseHeadWidth ("Pulse Head Width", Range(0.01, 0.2)) = 0.06
        _HeadColor ("Pulse Head Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _TailColor ("Pulse Tail Color", Color) = (1.0, 0.85, 0.2, 1.0)
        _BaseColor ("Inactive Base Color", Color) = (0.2, 0.25, 0.35, 0.5)
        _PulseIntensity ("Pulse Intensity", Range(0, 5)) = 2.0
        _IsActive ("Is Route Active (0 or 1)", Float) = 0.0

        [Header(Pixel Art Quantization)]
        _QuantizeSteps ("Brightness Steps", Float) = 8.0
        _SegmentDashCount ("Dash Count (0 for solid)", Float) = 0.0

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
            Name "RouteFlow"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _PulseProgress;
            float _PulseLength;
            float _PulseHeadWidth;
            fixed4 _HeadColor;
            fixed4 _TailColor;
            fixed4 _BaseColor;
            float _PulseIntensity;
            float _IsActive;
            float _QuantizeSteps;
            float _SegmentDashCount;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 texCol = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd);

                #ifdef UNITY_UI_CLIP_RECT
                texCol.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(texCol.a - 0.001);
                #endif

                float u = IN.texcoord.x;

                // Optional dashed pixel segments
                if (_SegmentDashCount > 0.0)
                {
                    float dashPattern = frac(u * _SegmentDashCount);
                    if (dashPattern < 0.25)
                    {
                        clip(-1.0);
                    }
                }

                if (_IsActive < 0.5)
                {
                    // Inactive dormant route
                    fixed4 inactiveOut = _BaseColor * IN.color;
                    inactiveOut.a *= texCol.a;
                    return inactiveOut;
                }

                // Active route with traveling pulse: (Node A -> Node B)
                // Head is at _PulseProgress, tail trails behind it (u < _PulseProgress)
                float distFromHead = _PulseProgress - u;
                float pulseFactor = 0.0;

                if (distFromHead >= 0.0 && distFromHead <= _PulseLength)
                {
                    float normDist = distFromHead / _PulseLength; // 0 at head, 1 at tail
                    // Strong head, exponential falloff
                    pulseFactor = pow(1.0 - normDist, 2.0);

                    // Extra intense head tip
                    if (distFromHead <= _PulseHeadWidth)
                    {
                        float headRatio = 1.0 - (distFromHead / _PulseHeadWidth);
                        pulseFactor += headRatio * 0.8;
                    }
                }

                // Quantize brightness for crisp pixel-art styling
                if (_QuantizeSteps > 0.0)
                {
                    pulseFactor = floor(pulseFactor * _QuantizeSteps) / _QuantizeSteps;
                }

                // Blend between tail color, head color, and base active path
                fixed4 routeColor = _TailColor;
                if (distFromHead >= 0.0 && distFromHead <= _PulseHeadWidth)
                {
                    float headBlend = 1.0 - (distFromHead / _PulseHeadWidth);
                    routeColor = lerp(_TailColor, _HeadColor, headBlend);
                }

                fixed4 finalCol = lerp(_BaseColor, routeColor * (1.0 + pulseFactor * _PulseIntensity), saturate(pulseFactor + 0.2));
                finalCol *= IN.color;
                finalCol.a *= texCol.a;

                return finalCol;
            }
            ENDCG
        }
    }
}

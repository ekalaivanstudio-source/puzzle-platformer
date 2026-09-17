Shader "MainGame/UI/CinematicPixelBackground"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(Pixel Art Grid Settings)]
        _PixelGridStep ("Pixel Grid Step (Texels)", Float) = 512.0
        _SnapDistortionToGrid ("Snap Distortion To Grid (0 or 1)", Float) = 1.0

        [Header(Subtle Wave Displacement)]
        _DistortionStrength ("Distortion Strength", Range(0, 0.05)) = 0.0
        _DistortionSpeed ("Distortion Speed", Float) = 1.0
        _DistortionFrequency ("Distortion Frequency", Float) = 8.0

        [Header(Stepped Animated Noise)]
        _NoiseStrength ("Noise Strength", Range(0, 0.2)) = 0.0
        _NoiseScale ("Noise Scale", Float) = 64.0
        _NoiseSpeed ("Noise Speed", Float) = 4.0

        [Header(Brightness Breathing and Color Pulse)]
        _BrightnessPulse ("Brightness Pulse", Range(0, 1.0)) = 0.0
        _PulseColor ("Pulse Color", Color) = (1, 0.3, 0.3, 1)
        _PulseSpeed ("Pulse Speed", Float) = 1.5
        _PulseIntensity ("Pulse Additive Intensity", Range(0, 2)) = 0.0

        [Header(Stepped Glitch Blocks)]
        _GlitchStrength ("Glitch Strength", Range(0, 0.1)) = 0.0
        _GlitchBlockSize ("Glitch Block Size", Float) = 24.0
        _GlitchRate ("Glitch Rate (Hz)", Float) = 12.0

        [Header(Scanline Distortion Band)]
        _ScanStrength ("Scan Distortion Strength", Range(0, 0.05)) = 0.0
        _ScanWidth ("Scan Width", Range(0.01, 0.3)) = 0.06
        _ScanSpeed ("Scan Speed", Float) = 0.8

        [Header(Animated Texture Offset)]
        _TextureOffsetSpeed ("Texture Offset Speed (XY)", Vector) = (0, 0, 0, 0)
        _TextureOffsetStep ("Texture Offset Step Size", Float) = 0.0

        [Header(Energy and Edge Accent)]
        _EnergyStrength ("Energy Glow Strength", Range(0, 2.0)) = 0.0
        _EdgeIntensity ("Edge Accent Intensity", Range(0, 2.0)) = 0.0
        _EdgeColor ("Edge Color", Color) = (0.35, 0.85, 1.0, 1)

        [Header(Effect Mode)]
        _EffectMode ("Effect Mode (0:Bg, 1:Char, 2:Title, 3:Robot, 4:Energy, 5:Glitch)", Float) = 0.0

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _PixelGridStep;
            float _SnapDistortionToGrid;
            float _DistortionStrength;
            float _DistortionSpeed;
            float _DistortionFrequency;

            float _NoiseStrength;
            float _NoiseScale;
            float _NoiseSpeed;

            float _BrightnessPulse;
            fixed4 _PulseColor;
            float _PulseSpeed;
            float _PulseIntensity;

            float _GlitchStrength;
            float _GlitchBlockSize;
            float _GlitchRate;

            float _ScanStrength;
            float _ScanWidth;
            float _ScanSpeed;

            float4 _TextureOffsetSpeed;
            float _TextureOffsetStep;

            float _EnergyStrength;
            float _EdgeIntensity;
            fixed4 _EdgeColor;

            float _EffectMode;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Pseudo-random 2D hash
            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float time = _Time.y;

                // ─── 1. OPTIONAL TEXTURE OFFSET (STEPPED OR CONTINUOUS) ─────────
                if (abs(_TextureOffsetSpeed.x) > 0.0001 || abs(_TextureOffsetSpeed.y) > 0.0001)
                {
                    float2 rawOffset = _TextureOffsetSpeed.xy * time;
                    if (_TextureOffsetStep > 0.0001)
                    {
                        rawOffset = floor(rawOffset / _TextureOffsetStep) * _TextureOffsetStep;
                    }
                    uv += rawOffset;
                }

                // ─── 2. STEPPED GLITCH BLOCKS ──────────────────────────────────
                if (_GlitchStrength > 0.0005)
                {
                    float glitchTime = floor(time * max(_GlitchRate, 1.0));
                    float blockY = floor(uv.y * max(_GlitchBlockSize, 1.0));
                    float glitchRand = Hash21(float2(blockY, glitchTime));

                    // Sparse activation (only top ~12% of slices jitter)
                    if (glitchRand > 0.88)
                    {
                        float xShift = (glitchRand - 0.88) / 0.12 * _GlitchStrength * ((glitchRand > 0.94) ? 1.0 : -1.0);
                        if (_SnapDistortionToGrid > 0.5 && _PixelGridStep > 1.0)
                        {
                            xShift = floor(xShift * _PixelGridStep) / _PixelGridStep;
                        }
                        uv.x += xShift;
                    }
                }

                // ─── 3. SCANLINE DISTORTION BAND ───────────────────────────────
                if (_ScanStrength > 0.0005)
                {
                    float scanPos = frac(time * _ScanSpeed);
                    float distFromScan = abs(uv.y - scanPos);
                    if (distFromScan < _ScanWidth)
                    {
                        float scanWave = (1.0 - (distFromScan / _ScanWidth));
                        float scanShift = scanWave * _ScanStrength;
                        if (_SnapDistortionToGrid > 0.5 && _PixelGridStep > 1.0)
                        {
                            scanShift = floor(scanShift * _PixelGridStep) / _PixelGridStep;
                        }
                        uv.x += scanShift;
                    }
                }

                // ─── 4. SUBTLE WAVE DISPLACEMENT (CONTROLLED DIRECTIONAL) ──────
                if (_DistortionStrength > 0.0005)
                {
                    float wave = sin(uv.y * _DistortionFrequency + time * _DistortionSpeed);
                    float waveShift = wave * _DistortionStrength;
                    if (_SnapDistortionToGrid > 0.5 && _PixelGridStep > 1.0)
                    {
                        waveShift = floor(waveShift * _PixelGridStep) / _PixelGridStep;
                    }
                    uv.x += waveShift;
                }

                // ─── 5. PIXEL-GRID SNAPPING (PROTECT PIXEL-ART CRISPNESS) ──────
                float2 sampleUV = uv;
                if (_SnapDistortionToGrid > 0.5 && _PixelGridStep > 1.0)
                {
                    sampleUV = floor(uv * _PixelGridStep) / _PixelGridStep;
                }

                // ─── 6. SAMPLE BASE TEXTURE ────────────────────────────────────
                half4 color = (tex2D(_MainTex, sampleUV) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // If pixel is transparent, skip heavy additive styling
                if (color.a < 0.01)
                {
                    return color;
                }

                // ─── 7. STEPPED ANIMATED PIXEL NOISE ───────────────────────────
                if (_NoiseStrength > 0.0005)
                {
                    float noiseTime = floor(time * max(_NoiseSpeed, 1.0));
                    float2 noiseGrid = floor(uv * max(_NoiseScale, 4.0));
                    float nVal = Hash21(noiseGrid + float2(noiseTime * 17.3, noiseTime * 31.7));
                    float noiseOffset = (nVal - 0.5) * _NoiseStrength;
                    color.rgb = saturate(color.rgb + noiseOffset);
                }

                // ─── 8. BRIGHTNESS BREATHING AND COLOR PULSE ───────────────────
                if (_BrightnessPulse > 0.0005 || _PulseIntensity > 0.0005)
                {
                    float breath = sin(time * _PulseSpeed) * 0.5 + 0.5;
                    float brightMod = 1.0 + (breath * _BrightnessPulse);
                    color.rgb *= brightMod;

                    if (_PulseIntensity > 0.0005)
                    {
                        half3 pulseAdd = _PulseColor.rgb * (breath * _PulseIntensity * color.a);
                        color.rgb += pulseAdd;
                    }
                }

                // ─── 9. ENERGY / EDGE ACCENT GLOW ──────────────────────────────
                if (_EnergyStrength > 0.0005)
                {
                    float energyWave = sin(time * 3.5 + uv.y * 12.0) * 0.5 + 0.5;
                    color.rgb += _PulseColor.rgb * (energyWave * _EnergyStrength * 0.35 * color.a);
                }

                if (_EdgeIntensity > 0.0005)
                {
                    // Fast 2-tap alpha edge detection in pixel grid
                    float2 stepOffset = (_PixelGridStep > 1.0) ? (1.0 / _PixelGridStep) : _MainTex_TexelSize.xy;
                    half aR = tex2D(_MainTex, sampleUV + float2(stepOffset.x, 0)).a;
                    half aL = tex2D(_MainTex, sampleUV - float2(stepOffset.x, 0)).a;
                    half aU = tex2D(_MainTex, sampleUV + float2(0, stepOffset.y)).a;
                    half aD = tex2D(_MainTex, sampleUV - float2(0, stepOffset.y)).a;
                    float isEdge = saturate((4.0 * color.a) - (aR + aL + aU + aD));

                    if (isEdge > 0.1)
                    {
                        color.rgb += _EdgeColor.rgb * (isEdge * _EdgeIntensity);
                    }
                }

                return color;
            }
            ENDCG
        }
    }
    FallBack "UI/Default"
}

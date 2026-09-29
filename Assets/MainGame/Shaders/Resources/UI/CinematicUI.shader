Shader "MainGame/UI/CinematicUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        [Header(MultiDirectional Energy Border)]
        _BorderColor ("Border Color", Color) = (0.35, 0.85, 1.0, 1.0)
        _BorderIntensity ("Border Base Intensity", Range(0, 5)) = 0.0
        _BorderWidth ("Border Width (Pixels)", Range(0.5, 12.0)) = 2.5
        _BorderSoftness ("Border Softness (Pixels)", Range(0.01, 4.0)) = 0.5
        _PulseProgress ("Pulse Progress", Range(0, 1)) = 0.0
        _PulseWidth ("Pulse Width", Range(0.02, 1.0)) = 0.25
        _PulseFalloff ("Pulse Falloff", Range(0.5, 5.0)) = 2.0
        _FlowSpeed ("Flow Speed", Float) = 1.0
        _Clockwise ("Clockwise (1 or -1)", Float) = 1.0
        _EnergyActive ("Energy Active", Float) = 0.0
        _BorderPulseIntensity ("Border Pulse Intensity", Range(0, 5)) = 2.5
        _BorderDirectionMode ("Direction Mode (0:Perimeter, 1:LeftRight, 2:RightLeft, 3:TopBottom, 4:BottomTop)", Float) = 0.0

        [Header(Electrical Flow)]
        _ElecNoiseAmount ("Electrical Noise Amount", Range(0, 1)) = 0.65
        _ElecFreq ("Electrical Frequency", Float) = 48.0
        _ElecJitterSpeed ("Electrical Jitter Speed", Float) = 30.0

        [Header(Surface Energy Sweep)]
        _SweepColor ("Sweep Color", Color) = (0.85, 0.96, 1.0, 1.0)
        _SweepIntensity ("Sweep Intensity", Range(0, 5)) = 0.0
        _SweepProgress ("Sweep Progress", Range(-0.4, 1.4)) = -0.4
        _SweepWidth ("Sweep Width", Range(0.01, 0.5)) = 0.14
        _SweepAngle ("Sweep Angle (Degrees)", Float) = 45.0

        [Header(Digital Distortion and Glitch)]
        _GlitchIntensity ("Glitch Intensity", Range(0, 2)) = 0.0
        _GlitchSlices ("Glitch Slices", Float) = 32.0
        _RgbOffset ("RGB Offset", Range(0, 0.08)) = 0.015

        [Header(Shockwave Distortion)]
        _ShockwaveProgress ("Shockwave Progress", Range(0, 1)) = 0.0
        _ShockwaveOrigin ("Shockwave Origin (UV)", Vector) = (0.5, 0.5, 0, 0)
        _ShockwaveStrength ("Shockwave Strength", Range(0, 0.2)) = 0.0
        _ShockwaveThickness ("Shockwave Thickness", Range(0.01, 0.3)) = 0.08

        [Header(Radial Energy Pulse)]
        _RadialPulseRadius ("Radial Pulse Radius", Range(0, 2.0)) = 0.0
        _RadialPulseCenter ("Radial Pulse Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _RadialPulseThickness ("Radial Pulse Thickness", Range(0.01, 0.3)) = 0.06
        _RadialPulseIntensity ("Radial Pulse Intensity", Range(0, 4)) = 0.0
        _RadialPulseColor ("Radial Pulse Color", Color) = (0.4, 0.9, 1.0, 1.0)
        _RadialPulseDistort ("Radial Pulse Distortion", Range(0, 0.1)) = 0.0

        [Header(Surface Pixel Reconstruction)]
        _ReconstructProgress ("Reconstruction Progress (0:Dissolved, 1:Solid)", Range(0, 1)) = 1.0
        _ReconstructColor ("Reconstruct Edge Color", Color) = (0.35, 0.9, 1.0, 1.0)
        _ReconstructBlockiness ("Reconstruct Blockiness", Float) = 28.0
        _ReconstructEdgeWidth ("Reconstruct Edge Width", Range(0.01, 0.2)) = 0.08

        [Header(Surface Pixel Dissolve)]
        _DissolveProgress ("Dissolve Progress", Range(0, 1)) = 0.0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.01, 0.2)) = 0.06
        _DissolveEdgeColor ("Dissolve Edge Color", Color) = (0.35, 0.9, 1.0, 1.0)
        _DissolveBlockiness ("Dissolve Blockiness", Float) = 24.0

        [Header(Surface Noise Reveal)]
        _NoiseRevealProgress ("Noise Reveal Progress (0:Hidden, 1:Revealed)", Range(0, 1)) = 1.0
        _NoiseRevealEdgeWidth ("Noise Reveal Edge Width", Range(0.01, 0.2)) = 0.06
        _NoiseRevealColor ("Noise Reveal Edge Color", Color) = (0.35, 0.85, 1.0, 1.0)
        _NoiseRevealScale ("Noise Reveal Scale", Float) = 36.0

        [Header(Holographic Flicker and Scanlines)]
        _HoloFlicker ("Holo Scanline Flicker", Range(0, 1)) = 0.0
        _ScanDistortIntensity ("Scan Distortion Intensity", Range(0, 1)) = 0.0
        _ScanDistortY ("Scan Distortion Y Position", Range(0, 1)) = 0.5
        _ScanDistortWidth ("Scan Distortion Width", Range(0.01, 0.2)) = 0.05

        [Header(Data Stream Overlay)]
        _DataStreamIntensity ("Data Stream Intensity", Range(0, 2)) = 0.0
        _DataStreamSpeed ("Data Stream Speed", Float) = 4.0
        _DataStreamDensity ("Data Stream Density", Float) = 20.0
        _DataStreamColor ("Data Stream Color", Color) = (0.4, 0.85, 1.0, 1.0)

        [Header(Segment Flow Slider Signal)]
        _SegmentFlowProgress ("Segment Flow Progress", Range(0, 1)) = 0.0
        _SegmentFlowIntensity ("Segment Flow Intensity", Range(0, 3)) = 0.0
        _SegmentFlowCount ("Segment Count", Float) = 10.0
        _SegmentFlowColor ("Segment Flow Color", Color) = (0.35, 0.85, 1.0, 1.0)

        [Header(Impact Flash and Brightness)]
        _FlashColor ("Flash Color", Color) = (1, 1, 1, 1)
        _FlashIntensity ("Flash Intensity", Range(0, 4)) = 0.0
        _SurfaceBrightness ("Surface Luminance Boost", Range(0, 2)) = 0.0

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
            Name "CinematicSurface"
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
                float4 uv1      : TEXCOORD1; // uv1.xy = normUV [0..1], uv1.zw = pixel dimensions (width, height)
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
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            // Energy Border
            fixed4 _BorderColor;
            float _BorderIntensity;
            float _BorderWidth;
            float _BorderSoftness;
            float _PulseProgress;
            float _PulseWidth;
            float _PulseFalloff;
            float _FlowSpeed;
            float _Clockwise;
            float _EnergyActive;
            float _BorderPulseIntensity;
            float _BorderDirectionMode;

            // Electrical Flow
            float _ElecNoiseAmount;
            float _ElecFreq;
            float _ElecJitterSpeed;

            // Surface Power Sweep
            fixed4 _SweepColor;
            float _SweepIntensity;
            float _SweepProgress;
            float _SweepWidth;
            float _SweepAngle;

            // Digital Distortion & Glitch
            float _GlitchIntensity;
            float _GlitchSlices;
            float _RgbOffset;

            // Shockwave Distortion
            float _ShockwaveProgress;
            float4 _ShockwaveOrigin;
            float _ShockwaveStrength;
            float _ShockwaveThickness;

            // Radial Energy Pulse
            float _RadialPulseRadius;
            float4 _RadialPulseCenter;
            float _RadialPulseThickness;
            float _RadialPulseIntensity;
            fixed4 _RadialPulseColor;
            float _RadialPulseDistort;

            // Surface Reconstruction
            float _ReconstructProgress;
            fixed4 _ReconstructColor;
            float _ReconstructBlockiness;
            float _ReconstructEdgeWidth;

            // Surface Dissolve
            float _DissolveProgress;
            float _DissolveEdgeWidth;
            fixed4 _DissolveEdgeColor;
            float _DissolveBlockiness;

            // Surface Noise Reveal
            float _NoiseRevealProgress;
            float _NoiseRevealEdgeWidth;
            fixed4 _NoiseRevealColor;
            float _NoiseRevealScale;

            // Holographic & Scan
            float _HoloFlicker;
            float _ScanDistortIntensity;
            float _ScanDistortY;
            float _ScanDistortWidth;

            // Data Stream
            float _DataStreamIntensity;
            float _DataStreamSpeed;
            float _DataStreamDensity;
            fixed4 _DataStreamColor;

            // Segment Flow
            float _SegmentFlowProgress;
            float _SegmentFlowIntensity;
            float _SegmentFlowCount;
            fixed4 _SegmentFlowColor;

            // Flash & Brightness
            fixed4 _FlashColor;
            float _FlashIntensity;
            float _SurfaceBrightness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;

                bool hasCustomUV1 = (IN.uv1.z > 1.0 && IN.uv1.w > 1.0);
                OUT.rectUV = hasCustomUV1 ? IN.uv1 : float4(IN.texcoord, 100.0, 100.0);

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 normUV = IN.rectUV.xy;
                float2 pixelSize = IN.rectUV.zw;
                float2 texCoord = IN.texcoord;

                // ─── 1. SHOCKWAVE SURFACE DISTORTION ─────────────────────────
                if (_ShockwaveStrength > 0.001 && _ShockwaveProgress > 0.001 && _ShockwaveProgress < 1.0)
                {
                    float2 swDiff = (normUV - _ShockwaveOrigin.xy) * float2(pixelSize.x / max(pixelSize.y, 1.0), 1.0);
                    float swDist = length(swDiff);
                    float swTarget = _ShockwaveProgress * 1.4;
                    float ringDist = abs(swDist - swTarget);

                    if (ringDist < _ShockwaveThickness)
                    {
                        float swT = 1.0 - (ringDist / max(_ShockwaveThickness, 0.001));
                        float swWave = sin(swT * 3.14159) * _ShockwaveStrength * (1.0 - _ShockwaveProgress);
                        float2 swDir = (swDist > 0.001) ? (swDiff / swDist) : float2(0, 1);
                        texCoord += swDir * swWave;
                        normUV += swDir * swWave;
                    }
                }

                // ─── 2. RADIAL PULSE DISTORTION ──────────────────────────────
                if (_RadialPulseDistort > 0.001 && _RadialPulseIntensity > 0.001 && _RadialPulseRadius > 0.001)
                {
                    float2 rDiff = (normUV - _RadialPulseCenter.xy) * float2(pixelSize.x / max(pixelSize.y, 1.0), 1.0);
                    float rDist = length(rDiff);
                    float rRing = abs(rDist - _RadialPulseRadius);
                    if (rRing < _RadialPulseThickness)
                    {
                        float rD = (1.0 - (rRing / max(_RadialPulseThickness, 0.001))) * _RadialPulseDistort;
                        float2 rDir = (rDist > 0.001) ? (rDiff / rDist) : float2(0, 1);
                        texCoord += rDir * rD;
                    }
                }

                // ─── 3. DIGITAL GLITCH & SLICE TEARING ───────────────────────
                if (_GlitchIntensity > 0.001)
                {
                    float sliceY = floor(normUV.y * max(_GlitchSlices, 1.0));
                    float sliceNoise = frac(sin(sliceY * 91.34 + floor(_Time.y * 24.0) * 47.12) * 43758.5453);
                    if (sliceNoise > 0.65)
                    {
                        float xOffset = (sliceNoise - 0.65) * 0.06 * _GlitchIntensity;
                        normUV.x = saturate(normUV.x + xOffset);
                        texCoord.x = saturate(texCoord.x + xOffset);
                    }
                }

                // ─── 4. SCAN DISTORTION BAND ─────────────────────────────────
                if (_ScanDistortIntensity > 0.001)
                {
                    float distFromScan = abs(normUV.y - _ScanDistortY);
                    if (distFromScan < _ScanDistortWidth)
                    {
                        float scanWave = (1.0 - (distFromScan / _ScanDistortWidth)) * _ScanDistortIntensity * 0.03;
                        texCoord.x += scanWave;
                        normUV.x += scanWave;
                    }
                }

                // ─── 5. SAMPLE BASE TEXTURE (WITH OPTIONAL CHROMATIC RGB SPLIT) ─
                half4 color;
                if (_GlitchIntensity > 0.001 && _RgbOffset > 0.0001)
                {
                    float offset = _RgbOffset * _GlitchIntensity;
                    half4 colR = tex2D(_MainTex, texCoord + float2(offset, 0.0));
                    half4 colG = tex2D(_MainTex, texCoord);
                    half4 colB = tex2D(_MainTex, texCoord - float2(offset, 0.0));
                    color = (half4(colR.r, colG.g, colB.b, colG.a) + _TextureSampleAdd) * IN.color;
                }
                else
                {
                    color = (tex2D(_MainTex, texCoord) + _TextureSampleAdd) * IN.color;
                }

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // ─── 6. SURFACE PIXEL RECONSTRUCTION (0 = Dissolved, 1 = Formed) ─
                if (_ReconstructProgress < 0.999)
                {
                    float2 blockUV = floor(normUV * max(_ReconstructBlockiness, 1.0)) / max(_ReconstructBlockiness, 1.0);
                    float blockNoise = frac(sin(dot(blockUV, float2(12.9898, 78.233))) * 43758.5453);
                    float threshold = blockNoise * 0.40 + (1.0 - normUV.y) * 0.60;

                    if (threshold > _ReconstructProgress)
                    {
                        float edgeDist = threshold - _ReconstructProgress;
                        if (edgeDist < _ReconstructEdgeWidth)
                        {
                            float edgeFade = 1.0 - (edgeDist / max(_ReconstructEdgeWidth, 0.001));
                            color.rgb = lerp(color.rgb, _ReconstructColor.rgb * 2.2, edgeFade);
                            color.a = max(color.a, edgeFade);
                        }
                        else
                        {
                            clip(-1.0);
                        }
                    }
                }

                // ─── 7. SURFACE PIXEL DISSOLVE (0 = Solid, 1 = Dissolved) ─────
                if (_DissolveProgress > 0.001)
                {
                    float2 blockUV = floor(normUV * max(_DissolveBlockiness, 1.0)) / max(_DissolveBlockiness, 1.0);
                    float blockNoise = frac(sin(dot(blockUV, float2(12.9898, 78.233))) * 43758.5453);
                    float threshold = blockNoise * 0.35 + normUV.x * 0.65;

                    if (threshold < _DissolveProgress)
                    {
                        float edgeDist = abs(threshold - _DissolveProgress);
                        if (edgeDist < _DissolveEdgeWidth)
                        {
                            float edgeFade = 1.0 - (edgeDist / max(_DissolveEdgeWidth, 0.001));
                            color.rgb += _DissolveEdgeColor.rgb * (edgeFade * 2.5);
                        }
                        else
                        {
                            clip(-1.0);
                        }
                    }
                }

                // ─── 8. SURFACE NOISE REVEAL (MAP / NETWORK) ──────────────────
                if (_NoiseRevealProgress < 0.999)
                {
                    float2 noiseCoord = floor(normUV * max(_NoiseRevealScale, 1.0)) / max(_NoiseRevealScale, 1.0);
                    float nVal = frac(sin(dot(noiseCoord, float2(37.119, 93.817))) * 28913.123);
                    float nThreshold = nVal * 0.45 + (1.0 - normUV.x) * 0.55;

                    if (nThreshold > _NoiseRevealProgress)
                    {
                        float edgeDist = nThreshold - _NoiseRevealProgress;
                        if (edgeDist < _NoiseRevealEdgeWidth)
                        {
                            float edgeFade = 1.0 - (edgeDist / max(_NoiseRevealEdgeWidth, 0.001));
                            color.rgb += _NoiseRevealColor.rgb * (edgeFade * 2.0);
                        }
                        else
                        {
                            clip(-1.0);
                        }
                    }
                }

                // ─── 9. HOLOGRAPHIC SCANLINES & FLICKER ───────────────────────
                if (_HoloFlicker > 0.001)
                {
                    float scanline = sin(normUV.y * pixelSize.y * 3.14159 * 0.5) * 0.5 + 0.5;
                    float flicker = frac(sin(floor(_Time.y * 20.0) * 19.33) * 43758.54);
                    float holoMod = lerp(1.0, 0.85 + 0.15 * scanline + 0.08 * flicker, _HoloFlicker);
                    color.rgb *= holoMod;
                }

                // ─── 10. DATA STREAM OVERLAY (CREDITS / TELEMETRY) ───────────
                if (_DataStreamIntensity > 0.001)
                {
                    float rowY = floor(normUV.y * max(_DataStreamDensity, 1.0));
                    float rowSpeed = frac(sin(rowY * 17.13) * 43758.54) * 0.5 + 0.5;
                    float streamX = frac(normUV.x * 2.0 - _Time.y * _DataStreamSpeed * rowSpeed);
                    float streamNoise = frac(sin(floor(normUV.x * 16.0 - _Time.y * _DataStreamSpeed * 8.0) + rowY * 31.7) * 43758.54);

                    if (streamNoise > 0.72)
                    {
                        float streamBright = (streamNoise - 0.72) / 0.28 * _DataStreamIntensity;
                        color.rgb += _DataStreamColor.rgb * streamBright * color.a;
                    }
                }

                // ─── 11. SEGMENT FLOW (SETTINGS CONTROLS) ────────────────────
                if (_SegmentFlowIntensity > 0.001)
                {
                    float segStep = floor(normUV.x * max(_SegmentFlowCount, 1.0)) / max(_SegmentFlowCount, 1.0);
                    float segDist = abs(segStep - _SegmentFlowProgress);
                    if (segDist < (1.0 / max(_SegmentFlowCount, 1.0)))
                    {
                        float segPulse = (1.0 - segDist * _SegmentFlowCount) * _SegmentFlowIntensity;
                        color.rgb += _SegmentFlowColor.rgb * segPulse * color.a;
                    }
                }

                // ─── 12. RADIAL ENERGY PULSE (SURFACE & GLOW) ─────────────────
                if (_RadialPulseIntensity > 0.001 && _RadialPulseRadius > 0.001)
                {
                    float2 rOffset = (normUV - _RadialPulseCenter.xy) * float2(pixelSize.x / max(pixelSize.y, 1.0), 1.0);
                    float rDist = length(rOffset);
                    float ringDist = abs(rDist - _RadialPulseRadius);

                    if (ringDist < _RadialPulseThickness)
                    {
                        float rT = 1.0 - (ringDist / max(_RadialPulseThickness, 0.001));
                        float rVal = pow(rT, 2.0) * _RadialPulseIntensity;
                        color.rgb += _RadialPulseColor.rgb * rVal * color.a;
                    }
                }

                // ─── 13. SURFACE POWER / SCAN SWEEP ───────────────────────────
                if (_SweepIntensity > 0.001 && _SweepProgress > -0.35 && _SweepProgress < 1.35)
                {
                    float rad = _SweepAngle * 0.01745329;
                    float2 sweepDir = float2(cos(rad), sin(rad));
                    float sweepCoord = dot(normUV, sweepDir) / max(abs(sweepDir.x) + abs(sweepDir.y), 0.001);
                    float distToSweep = abs(sweepCoord - _SweepProgress);

                    if (distToSweep < _SweepWidth)
                    {
                        float sT = 1.0 - (distToSweep / max(_SweepWidth, 0.001));
                        float sweepVal = pow(sT, 2.2) * _SweepIntensity;
                        color.rgb += _SweepColor.rgb * sweepVal * color.a;
                    }
                }

                // ─── 14. MULTI-DIRECTIONAL ENERGY BORDER ──────────────────────
                float dB = normUV.y * pixelSize.y;
                float dT = (1.0 - normUV.y) * pixelSize.y;
                float dL = normUV.x * pixelSize.x;
                float dR = (1.0 - normUV.x) * pixelSize.x;
                float minEdgeDist = min(min(dL, dR), min(dB, dT));

                float edgeMask = saturate((_BorderWidth - minEdgeDist) / max(_BorderSoftness, 0.01));
                if (edgeMask > 0.001)
                {
                    float P = 0.0;
                    int dirMode = (int)_BorderDirectionMode;

                    if (dirMode == 1) // Left-to-Right
                    {
                        P = normUV.x;
                    }
                    else if (dirMode == 2) // Right-to-Left
                    {
                        P = 1.0 - normUV.x;
                    }
                    else if (dirMode == 3) // Top-to-Bottom
                    {
                        P = 1.0 - normUV.y;
                    }
                    else if (dirMode == 4) // Bottom-to-Top
                    {
                        P = normUV.y;
                    }
                    else // 0 = Perimeter Rectangle
                    {
                        float w = pixelSize.x;
                        float h = pixelSize.y;
                        float s = 0.0;
                        if (dB <= dT && dB <= dL && dB <= dR) s = normUV.x * w;
                        else if (dR <= dL && dR <= dT) s = w + normUV.y * h;
                        else if (dT <= dL) s = w + h + (1.0 - normUV.x) * w;
                        else s = 2.0 * w + h + (1.0 - normUV.y) * h;

                        float totalPerimeter = max(2.0 * (w + h), 1.0);
                        P = s / totalPerimeter;
                        if (_Clockwise < 0.0) P = 1.0 - P;
                    }

                    float currentProgress = (_EnergyActive > 0.5) 
                        ? frac(_Time.y * _FlowSpeed) 
                        : _PulseProgress;

                    float pDist = frac(P - currentProgress + 1.0);
                    float pulseVal = 0.0;

                    if (pDist < _PulseWidth && _PulseWidth > 0.001)
                    {
                        float trail = 1.0 - (pDist / _PulseWidth);
                        pulseVal = pow(trail, _PulseFalloff) * _BorderPulseIntensity;

                        if (_ElecNoiseAmount > 0.001)
                        {
                            float stepP = floor(P * max(_ElecFreq, 1.0));
                            float stepT = floor(_Time.y * max(_ElecJitterSpeed, 1.0));
                            float eNoise = frac(sin(stepP * 12.9898 + stepT * 78.233) * 43758.5453);
                            float eMod = (eNoise > 0.30) ? (0.75 + 0.5 * frac(eNoise * 10.0)) : 0.15;
                            pulseVal *= lerp(1.0, eMod, _ElecNoiseAmount);
                        }
                    }

                    float totalBorder = edgeMask * (_BorderIntensity + pulseVal);
                    color.rgb += _BorderColor.rgb * totalBorder * color.a;
                }

                // ─── 15. IMPACT FLASH & SURFACE LUMINANCE BOOST ───────────────
                if (_FlashIntensity > 0.001)
                {
                    color.rgb += _FlashColor.rgb * _FlashIntensity * color.a;
                }

                if (_SurfaceBrightness > 0.001)
                {
                    color.rgb += color.rgb * _SurfaceBrightness;
                }

                return color;
            }
            ENDCG
        }
    }
}

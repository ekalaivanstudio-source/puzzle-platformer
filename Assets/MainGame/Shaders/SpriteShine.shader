// A sprite shader that sweeps a soft highlight band across the sprite on a timer.
// Written against URP's own Sprite-Unlit-Default (same includes, same single
// untagged pass) so it renders under BOTH renderers this project ships with:
// the 2D Renderer (default Graphics asset) and the Universal forward renderers
// the Mobile/PC quality levels override with.
//
// The sweep is evaluated in OBJECT space, not UV space, because sprites taken
// from a sheet (the bricks come out of the dungeon tile set) occupy a small
// sub-rect of the atlas — their UVs are nowhere near 0..1 and a UV-space band
// would land off the sprite entirely.
Shader "MainGame/Sprite Shine"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        [Header(Shine)]
        [HDR] _ShineColor ("Shine Colour", Color) = (1, 1, 1, 1)
        _ShineIntensity ("Intensity", Range(0, 4)) = 1
        _ShineWidth ("Band Width", Range(0.01, 1)) = 0.18
        _ShineSoftness ("Band Softness", Range(0, 1)) = 0.45
        _ShineAngle ("Angle (degrees)", Range(-180, 180)) = 35
        _ShineSize ("Sweep Size (object units)", Range(0.1, 10)) = 1.4
        _ShineDuration ("Sweep Duration (sec)", Range(0.05, 5)) = 0.55
        _ShineInterval ("Gap Between Sweeps (sec)", Range(0, 20)) = 2.5
        _ShinePhase ("Phase Offset (sec)", Float) = 0

        // Legacy properties, mirrored from Sprite-Unlit-Default so a material using
        // this shader still falls back gracefully to the legacy sprite shader.
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        // No LightMode tag: this resolves to SRPDefaultUnlit, which both the 2D
        // Renderer and the Universal forward renderer draw — exactly once each.
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex ShineVertex
            #pragma fragment ShineFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color     : COLOR;
                float2 positionOS : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _ShineColor;
                float _ShineIntensity;
                float _ShineWidth;
                float _ShineSoftness;
                float _ShineAngle;
                float _ShineSize;
                float _ShineDuration;
                float _ShineInterval;
                float _ShinePhase;
            CBUFFER_END

            // Strength of the highlight at this point of the sprite, 0..1.
            // positionOS is the sprite's local XY (centred on its pivot).
            half ShineBand(float2 positionOS)
            {
                // Project onto the sweep axis and normalise so ~0..1 spans the sprite.
                float angleRad = _ShineAngle * (PI / 180.0);
                float2 axis = float2(cos(angleRad), sin(angleRad));
                float projection = dot(positionOS, axis) / max(_ShineSize, 1e-3) + 0.5;

                // One period = the sweep itself, then a gap with the band parked
                // just past the far edge (where it contributes nothing).
                float period = max(_ShineDuration + _ShineInterval, 1e-3);
                float elapsed = frac((_Time.y + _ShinePhase) / period) * period;
                float progress = saturate(elapsed / max(_ShineDuration, 1e-3));

                float width = max(_ShineWidth, 1e-4);
                float head = lerp(-width, 1.0 + width, progress);

                half band = saturate(1.0 - abs(projection - head) / width);

                // Softness reshapes the falloff: 0 is a hard-edged streak, 1 a wide bloom.
                return pow(band, lerp(6.0, 0.6, saturate(_ShineSoftness)));
            }

            Varyings ShineVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.positionOS = input.positionOS.xy;
                return o;
            }

            half4 ShineFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);

                // Masked by the sprite's own alpha so the highlight stays inside the
                // silhouette instead of lighting up the transparent quad around it.
                color.rgb += _ShineColor.rgb * (_ShineIntensity * ShineBand(input.positionOS) * color.a);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}

// The sprite shader behind the faded markers AttemptGhostService leaves at the spot where
// each attempt ended. It desaturates the sprite to grey and dims it; the opacity itself
// comes from the SpriteRenderer's own colour, so a single shared material serves every
// ghost in the level and they all still batch together.
//
// Written against URP's own Sprite-Unlit-Default (same includes, same single untagged
// pass) exactly like MainGame/Sprite Shine next door, so it renders under BOTH renderers
// this project ships with: the 2D Renderer (default Graphics asset) and the Universal
// forward renderers the Mobile/PC quality levels override with.
//
// It lives under a Resources folder on purpose. No material asset in the project points at
// it — AttemptGhostService builds its material at runtime — so anywhere else the shader
// would be stripped out of player builds and every ghost would come out magenta.
Shader "MainGame/Sprite Ghost"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        [MaterialToggle] _ZWrite ("ZWrite", Float) = 0

        [Header(Ghost)]
        _Desaturation ("Desaturation", Range(0, 1)) = 1
        _Brightness ("Brightness", Range(0, 2)) = 0.85

        [Header(Hover Outline)]
        [HDR] _OutlineColor ("Outline Colour", Color) = (1, 1, 1, 0)
        _OutlineWidth ("Outline Width (texels)", Range(0, 4)) = 1
        // Set from C# per hovered marker, NOT relied on from Unity's own _MainTex_TexelSize:
        // that one comes back unset through this pass, which makes every neighbour sample
        // land a whole UV away and lights the entire silhouette instead of its rim.
        _OutlineTexel ("Outline Texel Size (set from code)", Vector) = (0, 0, 0, 0)

        // Legacy properties, mirrored from Sprite-Unlit-Default so a material using this
        // shader still falls back gracefully to the legacy sprite shader.
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

        // No LightMode tag: this resolves to SRPDefaultUnlit, which both the 2D Renderer
        // and the Universal forward renderer draw — exactly once each.
        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex GhostVertex
            #pragma fragment GhostFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Desaturation;
                float _Brightness;
                half4 _OutlineColor;
                float _OutlineWidth;
                float4 _OutlineTexel;
            CBUFFER_END

            // Lights up the INSIDE edge of the sprite's silhouette: a texel that is opaque
            // while one of its neighbours is not.
            //
            // Inside rather than outside on purpose. These sprites are imported with tight
            // meshes, so the quad is trimmed to the opaque pixels and there is no margin
            // outside the silhouette to draw into — an outer glow would simply be clipped
            // away. An inner border needs no room at all and reads just as clearly as a
            // hover state.
            half InnerEdge(float2 uv)
            {
                // No texel size means no outline, rather than an outline a whole UV wide
                // that swallows the sprite.
                if (_OutlineTexel.x <= 0 || _OutlineTexel.y <= 0) return 0;

                float2 step = _OutlineTexel.xy * _OutlineWidth;

                half own = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;

                half neighbour = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(step.x, 0)).a;
                neighbour = min(neighbour, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(step.x, 0)).a);
                neighbour = min(neighbour, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, step.y)).a);
                neighbour = min(neighbour, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, step.y)).a);

                return saturate(own - neighbour);
            }

            Varyings GhostVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 GhostFragment(Varyings input) : SV_Target
            {
                half4 color = CommonUnlitFragment(input, input.color);

                // Rec. 601 luminance: the sprite's own colours go, its shape stays. Dimmed
                // afterwards so the marker sits behind the live body rather than competing
                // with it, and multiplied straight onto rgb — the alpha the renderer's
                // colour carries is what actually makes it see-through.
                half luminance = dot(color.rgb, half3(0.299, 0.587, 0.114));
                color.rgb = lerp(color.rgb, luminance.xxx, saturate(_Desaturation)) * _Brightness;

                // The hover border. Its alpha deliberately IGNORES the ghost's own opacity
                // and overrides it, so the outline reads at full strength around a body
                // that is still sitting at a third of an alpha behind it — which is what
                // makes a hovered marker pick itself out of a row of faded ones.
                half edge = InnerEdge(input.uv) * _OutlineColor.a;
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, edge);
                color.a = max(color.a, edge);

                return color;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}

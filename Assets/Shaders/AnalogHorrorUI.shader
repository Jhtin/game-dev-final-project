Shader "UI/AnalogHorrorUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.08
        _ScanlineCount ("Scanline Count", Float) = 480.0
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.04
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.45
        _VignetteSmoothness ("Vignette Smoothness", Range(0.1, 1)) = 0.55
        _VignetteColor ("Vignette Color", Color) = (0.04, 0.03, 0.02, 1.0)
        _FlickerIntensity ("Flicker Intensity", Range(0, 1)) = 0.02
        _Distortion ("Distortion / Jitter", Range(0, 0.05)) = 0.0

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
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord  : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float _ScanlineIntensity;
            float _ScanlineCount;
            float _NoiseIntensity;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            fixed4 _VignetteColor;
            float _FlickerIntensity;
            float _Distortion;

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

            // Pseudo-random noise function
            float hash12(float2 p)
            {
                float3 p3  = frac(float3(p.xyx) * .1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;

                // Subtle horizontal line jitter/distortion during interference
                if (_Distortion > 0.0001)
                {
                    float lineJitter = sin(uv.y * 80.0 + _Time.y * 30.0) * _Distortion;
                    uv.x += lineJitter * step(0.92, hash12(float2(_Time.y, uv.y * 20.0)));
                }

                // 1. Organic Vignette Calculation (Darkening edges)
                float2 centeredUV = abs(uv - 0.5) * 2.0;
                float dist = length(centeredUV);
                float vig = smoothstep(1.0 - _VignetteSmoothness, 1.25, dist) * _VignetteIntensity;

                // 2. High-Frequency Scanlines
                float scanline = sin(uv.y * _ScanlineCount * 3.14159 * 2.0);
                float scanDark = (scanline * 0.5 + 0.5) * _ScanlineIntensity;

                // 3. Animated Film Grain / Noise
                float2 noiseCoord = uv * 320.0 + float2(_Time.y * 85.0, _Time.y * 123.0);
                float n = hash12(noiseCoord);
                float grainAlpha = n * _NoiseIntensity;

                // 4. Subtle 60Hz/Fluorescent Brightness Flicker
                float flicker = (sin(_Time.y * 60.0) * 0.5 + 0.5) * _FlickerIntensity;

                // Combine into composite analog overlay
                fixed4 outColor = _VignetteColor;
                float totalAlpha = saturate(vig + scanDark + grainAlpha + flicker);

                // Modulate by vertex color
                outColor.a = totalAlpha * IN.color.a;

                #ifdef UNITY_UI_CLIP_RECT
                outColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (outColor.a - 0.001);
                #endif

                return outColor;
            }
        ENDCG
        }
    }
}

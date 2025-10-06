﻿Shader "UI/ButtonHighlight"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        // 高亮属性
        _HighlightColor ("Highlight Color", Color) = (1, 0.8, 0, 1)
        _HighlightIntensity ("Highlight Intensity", Range(0, 2)) = 1.0
        _GlowSize ("Glow Size", Range(0, 0.5)) = 0.1
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.5
        _BorderWidth ("Border Width", Range(0, 0.2)) = 0.05
        
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
            
            // 高亮参数
            fixed4 _HighlightColor;
            float _HighlightIntensity;
            float _GlowSize;
            float _PulseSpeed;
            float _BorderWidth;

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
                // 采样原始纹理
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                // 计算到边缘的距离
                float2 center = IN.texcoord - 0.5;
                float dist = length(center);
                
                // 边框效果
                float borderOuter = 0.5;
                float borderInner = 0.5 - _BorderWidth;
                float border = smoothstep(borderInner - 0.02, borderInner, dist) * 
                              (1.0 - smoothstep(borderOuter - 0.02, borderOuter, dist));
                
                // 边缘发光
                float edge = 1.0 - smoothstep(0.5 - _GlowSize, 0.5, dist);
                
                // 脉冲效果
                float pulse = (sin(_Time.y * _PulseSpeed) * 0.5 + 0.5) * 0.3 + 0.7;
                
                // 综合发光强度
                float glowStrength = (edge * 0.5 + border * 1.5) * _HighlightIntensity * pulse;
                
                // 应用高亮
                color.rgb += _HighlightColor.rgb * glowStrength;
                
                // 整体提亮
                color.rgb += _HighlightColor.rgb * _HighlightIntensity * 0.15;

                return color;
            }
        ENDCG
        }
    }
}
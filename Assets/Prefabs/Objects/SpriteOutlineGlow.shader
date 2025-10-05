Shader "Custom/SpriteOutlineGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0, 10)) = 2
        _OutlineFadeSpeed ("Outline Fade Speed", Range(0, 10)) = 2
        _OutlineFadeMin ("Outline Fade Min", Range(0, 1)) = 0.2
        _OutlineFadeMax ("Outline Fade Max", Range(0, 1)) = 1.0
        
        [Header(Glow)]
        _GlowColor ("Glow Color", Color) = (1,1,0,1)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2
        _GlowSpeed ("Glow Speed", Range(0, 10)) = 1
        
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

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
            };

            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidth;
            float _OutlineFadeSpeed;
            float _OutlineFadeMin;
            float _OutlineFadeMax;
            fixed4 _GlowColor;
            float _GlowIntensity;
            float _GlowSpeed;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float4 _MainTex_TexelSize;

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);
                return color;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                
                // 计算描边渐隐闪烁
                float outlineFade = sin(_Time.y * _OutlineFadeSpeed) * 0.5 + 0.5;
                outlineFade = lerp(_OutlineFadeMin, _OutlineFadeMax, outlineFade);
                outlineFade = pow(outlineFade, 2); // 让过渡更平滑
                
                // 计算发光动画
                float glow = sin(_Time.y * _GlowSpeed) * 0.5 + 0.5;
                glow = pow(glow, 2);
                
                // 描边检测
                float outline = 0;
                float2 pixelSize = _MainTex_TexelSize.xy * _OutlineWidth;
                
                // 8方向采样检测边缘
                outline += SampleSpriteTexture(IN.texcoord + float2(pixelSize.x, 0)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(-pixelSize.x, 0)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(0, pixelSize.y)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(0, -pixelSize.y)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(pixelSize.x, pixelSize.y)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(-pixelSize.x, pixelSize.y)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(pixelSize.x, -pixelSize.y)).a;
                outline += SampleSpriteTexture(IN.texcoord + float2(-pixelSize.x, -pixelSize.y)).a;
                
                outline = step(0.1, outline);
                outline *= (1 - c.a); // 只在透明区域显示描边
                outline *= outlineFade; // 应用渐隐闪烁效果
                
                // 组合颜色
                fixed4 outlineColor = _OutlineColor * outline;
                fixed4 glowColor = _GlowColor * outline * glow * _GlowIntensity;
                
                // 最终颜色
                c.rgb = c.rgb * c.a + outlineColor.rgb + glowColor.rgb;
                c.a = saturate(c.a + outline);
                
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
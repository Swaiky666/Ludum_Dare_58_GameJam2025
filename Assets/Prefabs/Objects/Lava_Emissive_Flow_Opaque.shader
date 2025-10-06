Shader "Custom/Lava_Emissive_Flow_Opaque"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _Color ("Albedo Tint", Color) = (1,1,1,1)

        _NormalMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Range(0, 2)) = 1

        _Metallic ("Metallic", Range(0,1)) = 0
        _Smoothness ("Smoothness", Range(0,1)) = 0.5

        // Emission controls
        [NoScaleOffset]_EmissionMap ("Emission Mask (R)", 2D) = "black" {}
        [HDR]_EmissionColor ("Emission Color (HDR)", Color) = (1, 0.35, 0.0, 1)
        _EmissionStrength ("Emission Strength", Range(0, 50)) = 8

        // Optional: use MainTex brightness as an emission mask (useful for lava)
        _UseLuminanceAsMask ("Use Albedo Brightness As Mask", Float) = 1
        _LuminancePower ("Luminance Power", Range(0, 8)) = 1.5

        // Flow / Distortion
        _DistortTex ("Distortion (RG)", 2D) = "gray" {}
        _DistortStrength ("Distort Strength (UV units)", Range(0, 1)) = 0.1
        _FlowSpeed ("Flow Speed", Range(-4, 4)) = 1.0
        _MainTexFlowDir ("MainTex Flow Dir (XY)", Vector) = (1,0,0,0)
        _EmissionFlowDir ("Emission Flow Dir (XY)", Vector) = (1,0,0,0)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _Color;

        sampler2D _NormalMap;
        half _BumpScale;

        half _Metallic;
        half _Smoothness;

        sampler2D _EmissionMap;
        fixed4 _EmissionColor;
        half _EmissionStrength;
        half _UseLuminanceAsMask;
        half _LuminancePower;

        sampler2D _DistortTex;
        half _DistortStrength;
        half _FlowSpeed;
        float4 _MainTexFlowDir;
        float4 _EmissionFlowDir;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float2 uv_EmissionMap;
            float2 uv_DistortTex;
        };

        inline half MyLuminance(half3 c)
        {
            // Standard perceptual luminance
            return dot(c, half3(0.2126, 0.7152, 0.0722));
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Distortion sample (RG centered around 0)
            float2 noise = tex2D(_DistortTex, IN.uv_DistortTex + _Time.y * _FlowSpeed * 0.05).rg - 0.5;
            float2 distort = noise * _DistortStrength;

            // Flow UVs
            float2 flowMain = IN.uv_MainTex + (_MainTexFlowDir.xy * _Time.y * _FlowSpeed * 0.05) + distort;
            float2 flowEmit = IN.uv_EmissionMap + (_EmissionFlowDir.xy * _Time.y * _FlowSpeed * 0.05) + distort;

            fixed4 albedo = tex2D(_MainTex, flowMain) * _Color;
            o.Albedo = albedo.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;

            // Normal
            fixed4 nrm = tex2D(_NormalMap, IN.uv_NormalMap + distort);
            o.Normal = UnpackScaleNormal(nrm, _BumpScale);

            // Emission mask: combine explicit emission map and optional luminance from albedo
            half maskFromMap = tex2D(_EmissionMap, flowEmit).r;
            half maskFromLum = pow( saturate( MyLuminance(albedo.rgb) ), _LuminancePower );
            // If _UseLuminanceAsMask > 0.5, include luminance; otherwise rely on map only
            half mask = max(maskFromMap, step(0.5h, _UseLuminanceAsMask) * maskFromLum);

            // Final emission
            half3 emission = _EmissionColor.rgb * (mask * _EmissionStrength);
            o.Emission = emission;

            o.Alpha = 1;
        }
        ENDCG
    }

    FallBack "Standard"
}

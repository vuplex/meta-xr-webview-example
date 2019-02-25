//
// OvrAvatar PC single component shader
//
// This is a Unity Surface shader implementation for our 1.5 skin shaded avatar look.
// The benefit of using this version is that it uses the full Unity PBR lighting under the hood.
// The Mobile shader is strongly recommended for use on mobile platforms for performance.
//
// Notes:
// - Use Mobile shader if you need mouth vertex movement.

Shader "OvrAvatar/Avatar_PC_SingleComponent"
{
    Properties
    {
        [NoScaleOffset] _MainTex("Color (RGB)", 2D) = "white" {}
        [NoScaleOffset] _NormalMap("Normal Map", 2D) = "bump" {}
        [NoScaleOffset] _RoughnessMap("Roughness Map", 2D) = "black" {}

        _BaseColor("Color Tint", Color) = (0.95,0.77,0.63)
        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        _DiffuseIntensity("Diffuse Intensity", Range(0.0,1.0)) = 0.3
        _RimIntensity("Rim Intensity", Range(0.0,10.0)) = 5.0
    }
        SubShader
    {
        Tags{ "Queue" = "Transparent" "RenderType" = "Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        // Render the back facing parts of the object then set on backface culling.
        // This fixes broken faces with convex meshes when using the alpha path.
        Pass
        {
            Color(0,0,0,0)
        }

        CGPROGRAM
#pragma surface surf Standard alpha:fade
#pragma target 3.0
#pragma fragmentoption ARB_precision_hint_fastest
        // Set this shader keyword if you are using Linear color space
#pragma multi_compile COLORSPACE_LINEAR_OFF COLORSPACE_LINEAR_ON
        // (Optional) Set this shader keyword if your scene only has one light
#pragma multi_compile SINGLE_LIGHT_OFF SINGLE_LIGHT_ON
#include "UnityCG.cginc"
        sampler2D _MainTex;
        sampler2D _NormalMap;
        sampler2D _RoughnessMap;

        float4 _BaseColor;
        float _Dimmer;
        float _Alpha;

        float _DiffuseIntensity;
        float _RimIntensity;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_NormalMap;
            float2 uv_RoughnessMap;
            float3 viewDir;
            float3 worldNormal; INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            // Unpack normal map
            o.Normal = tex2D(_NormalMap, IN.uv_NormalMap) * 2 - 1;

            // Diffuse texture sample
            float4 col = tex2D(_MainTex, IN.uv_MainTex);

            // Convert _BaseColor to gamma color space if we are in linear
            // Albedo texture is already in correct color space
    #if !defined(UNITY_COLORSPACE_GAMMA)
            _BaseColor.rgb = LinearToGammaSpace(_BaseColor.rgb);
    #endif
            // Adjust color tint with NdotL
            float NdotL = saturate(dot(WorldNormalVector(IN, o.Normal), _WorldSpaceLightPos0.xyz));
            _BaseColor.rgb += _DiffuseIntensity * NdotL;
            // Multiply in color tint
            o.Albedo = col.rgb * _BaseColor;
            // Rim term
            float VdotN = saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Albedo += pow(1.0 - VdotN, _RimIntensity) * NdotL * _LightColor0;

            // Sample roughness map and set smoothness and metallic
            float4 roughnessSample = tex2D(_RoughnessMap, IN.uv_RoughnessMap);
            o.Smoothness = roughnessSample.a;
            o.Metallic = roughnessSample.r;

            // Global dimmer
            o.Albedo *= _Dimmer;
            // Global alpha
            o.Alpha = col.a * _Alpha;


            // Convert back to linear color space if we are in linear
    #if !defined(UNITY_COLORSPACE_GAMMA)
            o.Albedo = GammaToLinearSpace(o.Albedo);
    #endif
        }
        ENDCG
    }
}
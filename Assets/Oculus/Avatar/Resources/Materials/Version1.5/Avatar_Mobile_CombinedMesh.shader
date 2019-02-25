//
// *** OvrAvatar Mobile Combined Mesh shader ***
// *** Texture array approach for rendering a combined mesh avatar ***
//
// This is a Unity vertex-fragnment shader implementation for our 1.5 skin shaded avatar look.
// The benefit of using this version is performance as it bypasses the PBR lighting model and
// so is generally recommended for use on mobile.
//
// This is the texture array version of the shader, which will draw all pre-combined
// components in one draw call. This is coupled with OvrAvatarMaterialManager to populate the
// shader properties.
//
// Shader keywords:
// - SECONDARY_LIGHT_ON SECONDARY_LIGHT_OFF
//   Enable SECONDARY_LIGHT_ON for a second "light" as expressed by _SecondaryLightDirection
//   and _SecondaryLightColor to influence the standard rim effect. This is designed for use in video watching
//   experiences to sample the screen color and apply this to the rim term.
// - NO_BACKLIGHT_ON NO_BACKLIGHT_OFF
//   This effect is active by default: NO_BACKLIGHT_OFF is the default and enables the effect. Enable NO_BACKLIGHT_ON
//   to disable illumination from the rear of the main light direction. This mobile shader supports one directional
//   light. This can cause the un-illuminated side of the avatar to lose definition.
//
// Notes:
// - The primary light in your scene will be used to calculate lighting.
// - We don't have a mouth bone, but the vertex shader will animate the vertices around the mouth
//   area according to the _Voice value. This should be set according to local microphone value
//   range between 0-1.

Shader "OvrAvatar/Avatar_Mobile_CombinedMesh"
{
    Properties
    {
        _MainTex("Main Texture Array", 2DArray) = "white" {}
        _NormalMap("Normal Map Array", 2DArray) = "bump" {}
        _RoughnessMap("Roughness Map Array", 2DArray) = "black" {}

        _Dimmer("Dimmer", Range(0.0,1.0)) = 1.0
        _Alpha("Alpha", Range(0.0,1.0)) = 1.0

        _Voice("Voice", Range(0.0,1.0)) = 1.0
        [HideInInspector] _MouthPosition("Mouth position", Vector) = (0,0,0,1)
        [HideInInspector] _MouthDirection("Mouth direction", Vector) = (0,0,0,1)
        [HideInInspector] _MouthEffectDistance("Mouth Effect Distance", Float) = 0.03
        [HideInInspector] _MouthEffectScale("Mouth Effect Scaler", Float) = 1

        // Index into the texture array needs an offset for precision
        _Slices("Texture Array Slices", int) = 4.97
    }

    SubShader
    {
        Pass
        {
            Tags
            {
                "LightMode" = "ForwardBase" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True"
            }
            LOD 100
            ZWrite On
            ZTest LEqual
            Cull Back
            ColorMask RGB
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
        #pragma vertex vert
        #pragma fragment frag
        #pragma target 3.5
        #pragma fragmentoption ARB_precision_hint_fastest
        #pragma multi_compile SECONDARY_LIGHT_OFF SECONDARY_LIGHT_ON
        #pragma multi_compile BACKLIGHT_OFF BACKLIGHT_ON
        #include "UnityCG.cginc"
        #include "UnityLightingCommon.cginc"

            UNITY_DECLARE_TEX2DARRAY(_MainTex);
            UNITY_DECLARE_TEX2DARRAY(_NormalMap);
            uniform float4 _NormalMap_ST;
            UNITY_DECLARE_TEX2DARRAY(_RoughnessMap);

            uniform int _Slices;

            uniform float4 _BaseColor[5];
            uniform float _DiffuseIntensity[5];
            uniform float _RimIntensity[5];
            uniform float _BacklightIntensity[5];
            uniform float _ReflectionIntensity[5];

            uniform float3 _SecondaryLightDirection;
            uniform float4 _SecondaryLightColor;

            uniform float _Dimmer;
            uniform float _Alpha;

            uniform float _Voice;
            uniform float4 _MouthPosition;
            uniform float4 _MouthDirection;
            uniform float _MouthEffectDistance;
            uniform float _MouthEffectScale;

            static const fixed MOUTH_ZSCALE = 0.5f;
            static const fixed MOUTH_DROPOFF = 0.01f;

            struct appdata
            {
                float4 vertex: POSITION;
                float3 normal: NORMAL;
                float4 tangent: TANGENT;
                float2 texcoord: TEXCOORD0;
                float4 vertexColor : COLOR0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 uv : TEXCOORD0;
                float4 posWorld: TEXCOORD1;
                float3 normalDir: TEXCOORD2;
                float3 tangentDir: TEXCOORD3;
                float3 bitangentDir: TEXCOORD4;
            };

            v2f vert(appdata v)
            {
                v2f o;

                // Mouth vertex animation with voip
                float4 worldVert = mul(unity_ObjectToWorld, v.vertex);;
                float3 delta = _MouthPosition - worldVert;
                delta.z *= MOUTH_ZSCALE;
                float dist = length(delta);
                float scaledMouthDropoff = _MouthEffectScale * MOUTH_DROPOFF;
                float scaledMouthEffect = _MouthEffectScale * _MouthEffectDistance;
                float displacement = _Voice * smoothstep(scaledMouthEffect + scaledMouthDropoff, scaledMouthEffect, dist);
                worldVert.xyz -= _MouthDirection * displacement;
                v.vertex = mul(unity_WorldToObject, worldVert);

                // Calculate tangents for normal mapping
                o.normalDir = normalize(UnityObjectToWorldNormal(v.normal));
                o.tangentDir = normalize(mul(unity_ObjectToWorld, half4(v.tangent.xyz, 0.0)).xyz);
                o.bitangentDir = normalize(cross(o.normalDir, o.tangentDir) * v.tangent.w);

                o.posWorld = worldVert;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv.xy = v.texcoord;
                o.uv.z = v.vertexColor.x * _Slices;
                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                // Light direction
                float3 lightDirection = _WorldSpaceLightPos0.xyz;

                // Unpack normal map
                float3 transformedNormalUV = i.uv;
                transformedNormalUV.xy = float2(TRANSFORM_TEX(i.uv.xy, _NormalMap));
                float3 normalMap = UNITY_SAMPLE_TEX2DARRAY(_NormalMap, transformedNormalUV).rgb * 2 - 1;

                // Calculate normal
                float3x3 tangentTransform = float3x3(i.tangentDir, i.bitangentDir, i.normalDir);
                float3 normalDirection = normalize(mul(normalMap.rgb, tangentTransform));
                float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.posWorld.xyz);

                // Apply view, normal, and lighting dependent terms
                float VdotN = saturate(dot(viewDirection, normalDirection));
                float NdotL = saturate(dot(normalDirection, lightDirection));

                float LightColorNdotL = NdotL * _LightColor0;

                // Sample the default reflection cubemap using the reflection vector
                float3 worldReflection = reflect(-viewDirection, normalDirection);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, worldReflection);
                // Decode cubemap data into actual color
                half3 reflectionColor = DecodeHDR(skyData, unity_SpecCube0_HDR);

                // Calculate color for each component
                float4 col;
                // Get index into texture array
                int componentIndex = floor(i.uv.z + 0.5);
                // Diffuse texture sample
                col = UNITY_SAMPLE_TEX2DARRAY(_MainTex, i.uv);
                // Multiply in color tint (don't need to deal with gamma/linear here as conversion already done)
                col.rgb *= _BaseColor[componentIndex];
                // Main light
                col.rgb += _DiffuseIntensity[componentIndex] * LightColorNdotL;
        #ifdef NO_BACKLIGHT_ON
                //NO_BACKLIGHT_ON disables the rear illumination
        #else
                // Illuminate from behind
                float3 reverseLightDirection = lightDirection * -1;
                float NdotInvL = saturate(dot(normalDirection, normalize(reverseLightDirection)));
                col.rgb += (_DiffuseIntensity[componentIndex] * _BacklightIntensity[componentIndex]) * NdotInvL * _LightColor0;
        #endif
                // Rim term
        #ifdef SECONDARY_LIGHT_ON
                // Secondary light proxy (direction and color) passed into the rim term
                NdotL = saturate(dot(normalDirection, _SecondaryLightDirection));
                col.rgb += pow(1.0 - VdotN, _RimIntensity[componentIndex]) * NdotL * _SecondaryLightColor;
        #else
                col.rgb += pow(1.0 - VdotN, _RimIntensity[componentIndex]) * LightColorNdotL;
        #endif
                // Reflection
                col.rgb += reflectionColor * UNITY_SAMPLE_TEX2DARRAY(_RoughnessMap, i.uv).a * _ReflectionIntensity[componentIndex];

                // Global dimmer
                col.rgb *= _Dimmer;
                // Global alpha
                col.a *= _Alpha;
        #if !defined(UNITY_COLORSPACE_GAMMA)
                col.rgb = GammaToLinearSpace(col.rgb);
        #endif
                // Return clamped final color
                return saturate(col);
            }
            ENDCG
        }
    }
}

Shader "SqueakSqueak/Coin Surface"
{
    Properties
    {
        _BaseColor("Clean metal color", Color) = (0.78,0.56,0.18,1)
        _Metallic("Clean metallic", Range(0,1)) = 0.8
        _Smoothness("Clean smoothness", Range(0,1)) = 0.35
        _DirtColor("Dirt color", Color) = (0.16,0.075,0.03,1)
        _DirtySmoothness("Dirty smoothness", Range(0,1)) = 0.1
        _MaximumSmoothness("Polished smoothness", Range(0,1)) = 0.9
        _DetailContrast("Minted pattern contrast", Range(0,0.5)) = 0.22
        _ReliefStrength("Visual engraving normal strength", Range(0,0.1)) = 0.035
        [NoScaleOffset] _FrontSurfaceMask("Front: R dirt, G polish, A valid", 2D) = "black" {}
        [NoScaleOffset] _BackSurfaceMask("Back: R dirt, G polish, A valid", 2D) = "black" {}
        _SurfaceBounds("Local center xyz / radius w", Vector) = (0,0,0,0.5)
        _SurfaceHalfHeight("Local half height", Float) = 1
        [HideInInspector] _SurfaceDebug("Validation channel view", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DirtColor;
            float4 _SurfaceBounds;
            float _SurfaceHalfHeight;
            half _Metallic;
            half _Smoothness;
            half _DirtySmoothness;
            half _MaximumSmoothness;
            half _DetailContrast;
            half _ReliefStrength;
            half _SurfaceDebug;
        CBUFFER_END
        TEXTURE2D(_FrontSurfaceMask); SAMPLER(sampler_FrontSurfaceMask);
        TEXTURE2D(_BackSurfaceMask); SAMPLER(sampler_BackSurfaceMask);
        float3 _LightDirection;
        float3 _LightPosition;
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 positionOS : TEXCOORD2;
            float3 normalOS : TEXCOORD3;
            half fog : TEXCOORD4;
            float4 shadowCoord : TEXCOORD5;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings SurfaceVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
            output.positionCS = positions.positionCS;
            output.positionWS = positions.positionWS;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.positionOS = input.positionOS.xyz;
            output.normalOS = input.normalOS;
            output.fog = ComputeFogFactor(positions.positionCS.z);
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(positions);
            #endif
            return output;
        }
        // CoinSurfaceMapper와 같은 로컬 +X/+Z 좌표입니다. 뒷면도 반전하지 않습니다.
        half4 ReadSurface(Varyings input, out half face)
        {
            float3 local = input.positionOS - _SurfaceBounds.xyz;
            float epsilon = _SurfaceHalfHeight * 0.001;
            bool front = input.normalOS.y >= 0.999 && abs(local.y - _SurfaceHalfHeight) <= epsilon;
            bool back = input.normalOS.y <= -0.999 && abs(local.y + _SurfaceHalfHeight) <= epsilon;
            face = front ? 1 : (back ? 0 : 0.5);
            if (!front && !back) return half4(0,0,0,0);
            float2 uv = local.xz / (2 * _SurfaceBounds.w) + 0.5;
            half4 mask = front ? SAMPLE_TEXTURE2D(_FrontSurfaceMask, sampler_FrontSurfaceMask, uv) :
                SAMPLE_TEXTURE2D(_BackSurfaceMask, sampler_BackSurfaceMask, uv);
            mask.rg *= mask.a;
            return mask;
        }
        // 자체 절차적 문양입니다. 지오메트리/Collider/마스크 좌표는 변경하지 않습니다.
        float MintedHeight(float2 q, half face)
        {
            float radius = length(q);
            float angle = radius > 0.0001 ? atan2(q.y, q.x) : 0;
            float rim = 1 - smoothstep(0.018, 0.043, abs(radius - 0.92));
            float groove = 1 - smoothstep(0.004, 0.014, abs(radius - 0.72));
            float beadAngle = round(angle * 32 / TWO_PI) * TWO_PI / 32;
            float beads = 1 - smoothstep(0.019, 0.034, length(q - float2(cos(beadAngle), sin(beadAngle)) * 0.815));
            float emblem;
            if (face > 0.5)
            {
                float core = 1 - smoothstep(0.17, 0.205, radius);
                float coreRing = 1 - smoothstep(0.006, 0.018, abs(radius - 0.24));
                float rays = (1 - smoothstep(0.014, 0.04, abs(sin(angle * 4)) * radius)) *
                    smoothstep(0.28, 0.32, radius) * (1 - smoothstep(0.47, 0.52, radius));
                emblem = core * 0.7 + coreRing * 0.5 + rays * 0.65;
            }
            else
            {
                float diamond = abs(q.x) * 1.9 + abs(q.y) * 1.5;
                float seal = 1 - smoothstep(0.018, 0.055, abs(diamond - 0.78));
                float waveDistance = min(abs(q.y - sin(q.x * 7) * 0.055 - 0.17),
                    min(abs(q.y - sin(q.x * 7) * 0.055), abs(q.y - sin(q.x * 7) * 0.055 + 0.17)));
                float waves = (1 - smoothstep(0.01, 0.028, waveDistance)) *
                    (1 - smoothstep(0.29, 0.33, abs(q.x))) * (1 - smoothstep(0.63, 0.7, diamond));
                emblem = seal * 0.7 + waves * 0.65;
            }
            return rim * 0.85 + beads * 0.6 - groove * 0.35 + emblem;
        }
        half3 DecoratedNormal(Varyings input, half4 mask, half face, out float height)
        {
            half3 normal = NormalizeNormalPerPixel(input.normalWS);
            height = 0;
            if (face == 0.5) return normal;
            float2 q = (input.positionOS.xz - _SurfaceBounds.xz) / _SurfaceBounds.w;
            height = MintedHeight(q, face);
            const float stepSize = 0.008;
            float2 gradient = float2(MintedHeight(q + float2(stepSize, 0), face) - height,
                MintedHeight(q + float2(0, stepSize), face) - height) / stepSize;
            half strength = _ReliefStrength * lerp(0.3, 1, 1 - mask.r);
            half3 tangent = normalize(TransformObjectToWorldDir(float3(1,0,0)));
            half3 bitangent = normalize(TransformObjectToWorldDir(float3(0,0,1)));
            return normalize(normal - (tangent * gradient.x + bitangent * gradient.y) * strength);
        }
        half4 SurfaceFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half face;
            half4 mask = ReadSurface(input, face);
            // Editor GPU 매핑 검사용. 같은 샘플 경로만 표시하며 런타임 기본값은 0입니다.
            if (_SurfaceDebug > 0.5) return half4(mask.r, mask.g, face, 1);
            half clean = 1 - mask.r;
            float height;
            half3 normal = DecoratedNormal(input, mask, face, height);
            half3 metalColor = _BaseColor.rgb * (1 + (height - 0.25) * _DetailContrast);
            if (face == 0.5)
            {
                float2 q = input.positionOS.xz - _SurfaceBounds.xz;
                float angle = atan2(q.y, q.x);
                float reeding = sin(angle * 80);
                float visibility = 1 - smoothstep(0.5, 1.5, fwidth(angle) * 80);
                metalColor *= 0.85 + reeding * visibility * 0.08;
            }
            SurfaceData surface = (SurfaceData)0;
            surface.albedo = lerp(_DirtColor.rgb, metalColor, clean);
            surface.metallic = lerp(_Metallic * 0.35, _Metallic, clean);
            surface.smoothness = lerp(lerp(_DirtySmoothness, _Smoothness, clean), _MaximumSmoothness, mask.g);
            surface.normalTS = half3(0,0,1);
            surface.occlusion = 1;
            surface.alpha = 1;
            InputData lighting = (InputData)0;
            lighting.positionWS = input.positionWS;
            lighting.normalWS = normal;
            lighting.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
            // Cascade 경계는 fragment 월드 위치로 선택합니다(URP Lit과 동일한 계약).
            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                lighting.shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                lighting.shadowCoord = TransformWorldToShadowCoord(lighting.positionWS);
            #else
                lighting.shadowCoord = float4(0,0,0,0);
            #endif
            lighting.fogCoord = input.fog;
            lighting.vertexLighting = VertexLighting(input.positionWS, lighting.normalWS);
            lighting.bakedGI = SampleSH(lighting.normalWS);
            lighting.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
            lighting.shadowMask = half4(1,1,1,1);
            half4 color = UniversalFragmentPBR(lighting, surface);
            color.rgb = MixFog(color.rgb, input.fog);
            return color;
        }
        half4 DepthFragment(Varyings input) : SV_Target { return 0; }
        Varyings ShadowVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirection = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirection = _LightDirection;
            #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirection)));
            return output;
        }
        half4 NormalsFragment(Varyings input) : SV_Target
        {
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
            half face;
            half4 mask = ReadSurface(input, face);
            float height;
            float3 normal = DecoratedNormal(input, mask, face, height);
            #if defined(_GBUFFER_NORMALS_OCT)
                float2 octNormal = saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5);
                return half4(PackFloat2To888(octNormal), 0);
            #else
                return half4(normal, 0);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "CoinSurfaceForward"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment SurfaceFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            ENDHLSL
        }
        Pass
        {
            Name "CoinSurfaceShadow"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "CoinSurfaceDepth"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
        Pass
        {
            Name "CoinSurfaceNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex SurfaceVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }
    FallBack Off
}

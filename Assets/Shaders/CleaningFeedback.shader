Shader "SqueakSqueak/Cleaning Feedback"
{
    Properties
    {
        _BaseColor("Tint", Color) = (1,1,1,1)
        _ParticleShape("Soft round particle (lines: 0)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ParticleShape;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vertex(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color * _BaseColor; output.uv = input.uv;
                return output;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                half4 color = input.color;
                if (_ParticleShape > 0.5) color.a *= 1 - smoothstep(0.25, 0.5, length(input.uv - 0.5));
                return color;
            }
            ENDHLSL
        }
    }
    FallBack Off
}

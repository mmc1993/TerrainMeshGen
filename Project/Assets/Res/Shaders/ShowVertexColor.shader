Shader "L/Debug/ShowVertexColor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

    }
    SubShader
    {
        Tags {
            "RenderType"="Transparent"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DBuffer.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/Editor/ShaderGraph/Includes/ShaderPass.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"

        struct Attributes
        {
            float3 positionOS : POSITION;
            float4 color : COLOR;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float4 color : COLOR;
        };

        sampler2D _MainTex;
        float4 _MainTex_ST;

        Varyings vert (Attributes input)
        {
            Varyings o;
            o.positionCS = TransformObjectToHClip(input.positionOS);
            o.positionWS = TransformObjectToWorld(input.positionOS);
            o.color = input.color;
            return o;
        }

        float4 frag (Varyings input) : SV_Target
        {
            return input.color;
        }
        ENDHLSL

        Pass
        {
            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            ENDHLSL
        }
    }
}

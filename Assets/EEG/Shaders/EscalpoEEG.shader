// Escalpo do EEG: cor por vértice (a topografia, gravada em sRGB), luz difusa simples da luz principal e
// transparência para o cérebro aparecer por dentro. Só as faces voltadas para quem olha, sem escrita de
// profundidade, na fila depois do material fantasma da anatomia. Compatível com instancing estéreo do Quest.
Shader "VRSNC/EscalpoEEG"
{
    Properties
    {
        _Opacidade ("Opacidade", Range(0, 1)) = 0.5
        _Ambiente ("Luz ambiente", Range(0, 1)) = 0.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+100" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Escalpo"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vertice
            #pragma fragment Fragmento
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Opacidade;
                half _Ambiente;
            CBUFFER_END

            struct Entrada
            {
                float4 posicao : POSITION;
                float3 normal : NORMAL;
                half4 cor : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Saida
            {
                float4 posicao : SV_POSITION;
                half3 cor : COLOR;
                float3 normal : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Saida Vertice(Entrada entrada)
            {
                Saida saida;
                UNITY_SETUP_INSTANCE_ID(entrada);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(saida);
                saida.posicao = TransformObjectToHClip(entrada.posicao.xyz);
                saida.normal = TransformObjectToWorldNormal(entrada.normal);
                half3 cor = entrada.cor.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                cor = SRGBToLinear(cor);
                #endif
                saida.cor = cor;
                return saida;
            }

            half4 Fragmento(Saida entrada) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(entrada);
                Light luz = GetMainLight();
                half difusa = saturate(dot(normalize(entrada.normal), luz.direction));
                half3 cor = entrada.cor * (_Ambiente + (1.0h - _Ambiente) * difusa * luz.color);
                return half4(cor, _Opacidade);
            }
            ENDHLSL
        }
    }
}

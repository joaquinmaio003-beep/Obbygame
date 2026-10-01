Shader "Obby/Calor"
{
    // AIRE CALIENTE: deforma lo que se ve detras con ondas que suben, como el aire sobre el fuego.
    // Lee la imagen de la pantalla que el renderer 2D copia al terminar la capa Default
    // (_CameraSortingLayerTexture), por eso el objeto tiene que ir en la capa de dibujo "Calor",
    // que se dibuja despues. Lo configura solo Assets/Editor/ConfigurarEfectos.cs.
    // La forma y la fuerza salen del alpha del sprite (un degradado suave) y del color.
    Properties
    {
        [PerRendererData] _MainTex ("Mascara", 2D) = "white" {}
        _Fuerza ("Fuerza (fraccion de pantalla)", Float) = 0.004
        _Velocidad ("Velocidad", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_CameraSortingLayerTexture);
            SAMPLER(sampler_CameraSortingLayerTexture);

            CBUFFER_START(UnityPerMaterial)
                float _Fuerza;
                float _Velocidad;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                SetUpSpriteInstanceProperties();
                v.positionOS = UnityFlipSprite(v.positionOS, unity_SpriteProps.xy);
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * unity_SpriteColor;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half m = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                float t = _Time.y * _Velocidad;

                // ondas que suben (uv.y - t) y se tuercen un poco de costado
                float2 onda;
                onda.x = sin(i.uv.y * 18.0 - t * 7.0 + sin(i.uv.x * 9.0 + t * 2.0) * 1.5);
                onda.y = 0.5 * cos(i.uv.y * 11.0 - t * 5.0 + i.uv.x * 6.0);

                float2 pantalla = GetNormalizedScreenSpaceUV(i.positionCS);
                half4 c = SAMPLE_TEXTURE2D(_CameraSortingLayerTexture, sampler_CameraSortingLayerTexture,
                                           pantalla + onda * (_Fuerza * m));
                c.a = m;
                return c;
            }
            ENDHLSL
        }
    }
}

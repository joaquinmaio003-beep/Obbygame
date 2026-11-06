Shader "Obby/SpriteAdditive"
{
    // Mezcla ADITIVA: el sprite suma luz sobre lo que hay detras (no lo tapa).
    // Para rayos de sol, destellos y brillos.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _FadeBottom ("Fade abajo", Range(0,1)) = 0
        _FadeSides ("Fade costados", Range(0,1)) = 0
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
        Blend One One   // aditivo (premultiplicado en el fragment)

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
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

            sampler2D _MainTex;
            fixed4 _Color;
            float _FadeBottom;   // 0 = sin fade; 1 = se apaga a lo largo de todo el sprite
            float _FadeSides;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;
                // se desvanece hacia la punta de abajo y hacia los costados (0 = no cambia nada)
                if (_FadeBottom > 0) c.a *= smoothstep(0, _FadeBottom, IN.texcoord.y);
                if (_FadeSides > 0)  c.a *= smoothstep(0, _FadeSides, 1 - abs(IN.texcoord.x * 2 - 1));
                c.rgb *= c.a;   // premultiplicado: bordes suaves sin halo
                return c;
            }
            ENDCG
        }
    }
}

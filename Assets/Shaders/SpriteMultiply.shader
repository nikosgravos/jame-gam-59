// Multiplies the sprite's colour into whatever is already on screen behind it, with optional blur.
// Transparent parts of the sprite leave the background untouched.
Shader "Custom/Sprite Multiply"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 1
        _Blur ("Blur (texels)", Range(0, 16)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

        Cull Off
        ZWrite Off
        Blend DstColor Zero   // result = source * background

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _Strength;
            float _Blur;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;   // SpriteRenderer colour
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            // 5x5 gaussian blur. Colour is weighted by alpha so transparent pixels don't darken the edges.
            float4 SampleBlurred(float2 uv)
            {
                float2 step = _Blur * 0.5 * _MainTex_TexelSize.xy;
                float3 rgb = 0;
                float alpha = 0;
                float total = 0;

                for (int x = -2; x <= 2; x++)
                {
                    for (int y = -2; y <= 2; y++)
                    {
                        float w = exp(-(x * x + y * y) * 0.5);
                        float4 s = tex2D(_MainTex, uv + float2(x, y) * step);
                        rgb += s.rgb * s.a * w;
                        alpha += s.a * w;
                        total += w;
                    }
                }
                return float4(rgb / max(alpha, 0.0001), alpha / total);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 sampled = _Blur > 0.001 ? SampleBlurred(i.uv) : tex2D(_MainTex, i.uv);
                fixed4 tex = sampled * i.color;
                // white multiplies to "no change", so fade towards white where transparent or weak
                fixed3 rgb = lerp(fixed3(1, 1, 1), tex.rgb, tex.a * _Strength);
                return fixed4(rgb, 1);
            }
            ENDCG
        }
    }
}

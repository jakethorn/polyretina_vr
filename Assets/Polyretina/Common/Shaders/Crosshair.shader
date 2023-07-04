Shader "LNE/Crosshair"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _target_pixel ("Target Pixel", Vector) = (0, 0, 0, 0)
        _size ("Crosshair Size", Float) = 50.0
        _colour ("Crosshair Colour", Color) = (1, 0, 0, 1)
    }

    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float2 _target_pixel;
            float _size;
            float4 _colour;

            float4 frag(v2f i) : SV_Target
            {
                float girth = _size / 10.0;

                float4 col = tex2D(_MainTex, i.uv);
                float2 current_pixel = i.uv * _ScreenParams.xy;

                if (current_pixel.x < _target_pixel.x + girth && current_pixel.x > _target_pixel.x - girth)
                {
                    if (current_pixel.y < _target_pixel.y + _size && current_pixel.y > _target_pixel.y - _size)
                    {
                        col.xyz = _colour;
                    }
                }

                if (current_pixel.y < _target_pixel.y + girth && current_pixel.y > _target_pixel.y - girth)
                {
                    if (current_pixel.x < _target_pixel.x + _size && current_pixel.x > _target_pixel.x - _size)
                    {
                        col.xyz = _colour;
                    }
                }

                return col;
            }
            ENDCG
        }
    }
}

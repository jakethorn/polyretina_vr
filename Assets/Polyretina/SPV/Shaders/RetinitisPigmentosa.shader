Shader "LNE/Retinitis Pigmentosa"
{
    Properties
    {
        _MainTex("Base (RGB)", 2D) = "white" {}
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment horizontal_blur

            #pragma shader_feature_local TAP_1 TAP_3 TAP_5 TAP_9 TAP_13

            #define FIRST_PASS

            #include "Vert.cginc"
            #include "Blur.cginc"
            ENDCG
        }

        GrabPass { }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment vertical_blur

            #pragma shader_feature_local TAP_1 TAP_3 TAP_5 TAP_9 TAP_13

            #include "Vert.cginc"
            #include "Blur.cginc"
            ENDCG
        }

        GrabPass { }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "vert.cginc"
            #include "Coordinates.cginc"

            sampler2D _GrabTexture;
            sampler2D _ProsTex;
            float2 _eye_gaze;
            float2 _headset_diameter;
            float _natural_vision_width;
            float _natural_vision_saturation;
            float _natural_vision_brightness;
            float _natural_vision_contrast;

            static const float SATURATION_LIMIT = 50;

            float4 frag(float2 uv : TEXCOORD0) : SV_Target
            {
                float2 uv_pros = float2(uv.x, 1 - uv.y);

                _natural_vision_width /= 2;
                _natural_vision_saturation = max(_natural_vision_saturation, 1 / SATURATION_LIMIT);

                float distance_to_fovea = length(pixel_to_angle(uv_pros - _eye_gaze, _headset_diameter));
                float b = (_natural_vision_width - distance_to_fovea) / _natural_vision_width;
                b *= _natural_vision_saturation * SATURATION_LIMIT;
                b = saturate(b);
                b *= _natural_vision_brightness;

                return tex2D(_GrabTexture, uv) * b.xxxx + tex2D(_ProsTex, uv_pros);
            }
            ENDCG
        }
    }
}

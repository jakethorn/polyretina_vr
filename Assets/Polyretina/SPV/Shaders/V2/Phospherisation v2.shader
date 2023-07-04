Shader "LNE/Phospherisation v2"
{
	Properties
	{
		_MainTex ("Base (RGB)", 2D) = "white" {}
	}

	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment phos_w_fade_mrt

			#pragma shader_feature_local USE_FADING
			#pragma shader_feature_local OUTLINE

			#define FIRST_PASS

			#include "../Vert.cginc"
			#include "../Phospherisation.cginc"
			ENDCG
		}

		GrabPass { }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment horizontal_blur3

			#include "../Vert.cginc"
			#include "../Blur.cginc"
			ENDCG
		}

		GrabPass { }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment vertical_blur3

			#include "../Vert.cginc"
			#include "../Blur.cginc"
			ENDCG
		}
	}
}

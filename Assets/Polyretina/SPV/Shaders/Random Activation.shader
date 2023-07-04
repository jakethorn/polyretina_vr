Shader "LNE/Random Activation"
{
	SubShader
	{
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#define FIRST_PASS

			#include "Vert.cginc"

			sampler2D electrodes;
			float threshold;
			float brightness;
			int interrupt;

			float rand(float seed_x, float seed_y, float seed_z)
			{
				return frac(sin(dot(float3(seed_x, seed_y, seed_z), float3(12.9898, 78.233, 45.5432))) * 43758.5453);
			}

			float interruptf()
			{
				if (interrupt == 1)
				{
					return step(fmod(_Time.y, 1.0), 0.6);
				}
				else
				{
					return 1;
				}
			}

			float4 frag(float2 uv : TEXCOORD0) : SV_TARGET
			{
				float4 data = tex2D(electrodes, uv);
				float intensity = rand(data.b, data.a, _Time.y) > threshold;

				intensity *= brightness * interruptf();

				return float4(intensity.xxx, 1);
			}
			ENDCG
		}
	}
}

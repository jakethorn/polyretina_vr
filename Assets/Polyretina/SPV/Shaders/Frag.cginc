#ifndef FRAG_CGINC
#define FRAG_CGINC

    #include "UnityCG.cginc"

#ifdef FIRST_PASS
	#define TEX _MainTex
#else
	#ifndef GRAB_PASS
	#define GRAB_PASS
	#endif
	#define TEX _GrabTexture
#endif

	sampler2D TEX;

	float4 frag(float2 uv : TEXCOORD0) : SV_TARGET
	{
		return tex2D(TEX, uv);
	}

	float4 frag_inv(float2 uv : TEXCOORD0) : SV_TARGET
	{
		return float4(float3(1, 1, 1) - tex2D(TEX, uv).rgb, 1);
	}

#endif

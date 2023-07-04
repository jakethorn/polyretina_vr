#ifndef TAIL_DISTORTION_CGINC
#define TAIL_DISTORTION_CGINC

	#include "Coordinates.cginc"
	#include "Functions.cginc"

	/*
	 * Properties
	 */

#ifdef RIGHT_EYE
#undef LEFT_EYE
#endif

#ifdef FIRST_PASS
	#define TEX _MainTex
#else
	#ifndef GRAB_PASS
	#define GRAB_PASS
	#endif
	#define TEX _GrabTexture
#endif

	sampler2D TEX;

	sampler2D _axon_tex;
	float2 _eye_gaze;
	float2 _headset_diameter;
	float _polyretina_radius;
	float _decay_const;

	/*
	 * Constants
	 */

	static const float	TAIL_LENGTH = 15;

#if defined(LOW_QUALITY)
	static const int	TAIL_PRECISION = 50;
#elif defined(MEDIUM_QUALITY)
	static const int	TAIL_PRECISION = 100;
#else
	static const int	TAIL_PRECISION = 150;
	#ifndef HIGH_QUALITY
	#define HIGH_QUALITY
	#endif
#endif

	static const float	TAIL_INCREMENT = TAIL_LENGTH / TAIL_PRECISION;

	static const float	FOV_BUFFER = 2000.0;
	static const float2 FOV_BUFFER_OFFSET = float2(500.0, 0.0);

	/*
	 * Functions
	 */

	// clipping ends the processing of a pixel, thus increasing efficiency
	// we can safely clip a lot of the screen because the polyretina will always be in a determined spot with a determined size and all other pixels are black
	// however, we cannot simply clip outside of the polyretina because of the axonal tails
	// this function clips outside of the polyretina + the range of the axonal tails using some tested numbers.
	void clip_polyretina(float2 uv)
	{
		float clip_radius = _polyretina_radius + FOV_BUFFER;
		float2 pixel = pixel_to_retina(uv, _headset_diameter);
		float pixel_dist = distance(pixel, FOV_BUFFER_OFFSET);
		
		// clips pixel if given a negative number (i.e., if the distance to the pixel is greater than the polyretina radius)
		clip(clip_radius - pixel_dist);
	}

	// The following functions make us of equations from:
	// Jansonius et al, 2009, "A mathematical description of nerve fiber bundle trajectories and their variability in the human retina"
	// Beyeler et al 2018, "A model of ganglion axon pathways accounts for percepts elicited by retinal implants"
	
	float calculate_phi(float phi0, float rho, float b, float c)
	{
		return phi0 + b * pow(rho - 4.0, c);
	}

	float2 invert_x(float2 vec)
	{
		return float2(1 - vec.x, vec.y);
	}

	float2 invert_y(float2 vec)
	{
		return float2(vec.x, 1 - vec.y);
	}

	float calculate_tail(float4 data, float2 uv)
	{
		float phi0 = data.r;
		float rho = data.g;
		float b = data.b;
		float c = data.a;

		float2 angle = pixel_to_angle(uv, _headset_diameter);	// angle at current pixel
		float inv_decay_const = 1.0 / _decay_const;				// inverse of decay const is more efficient

		float output = 0.0;
		for (int i = 0; i < TAIL_PRECISION; ++i)
		{
			float phi = calculate_phi(phi0, rho, b, c);
			float2 tail = polar_to_pixel(rho, phi, _headset_diameter);	// uv coordinate of a pixel on the tail (relative to rho)

			float2 tail_eye_gaze = tail + _eye_gaze;

#ifdef LEFT_EYE
			// invert x-axis if we are targeting the left eye
			tail_eye_gaze = invert_x(tail_eye_gaze);
#endif

#ifdef RT_TARGET
			// grab passes work differently when rendering to an RT, so uv coordinates are inverted
			tail_eye_gaze = invert_y(tail_eye_gaze);
#endif

			float luminance = tex2D(TEX, tail_eye_gaze).g;

			float dist = distance(angle, pixel_to_angle(tail, _headset_diameter));	// distance from current pixel to tail pixel
			float sensitivity = exp(-dist * inv_decay_const);
			float activation = min(luminance, luminance * sensitivity); // min stops the tail from being brigher than the phosphene that created it
			output = max(output, activation);

			rho -= TAIL_INCREMENT;
		}

		return output;
	}

	/*
	 * Frag
	 */

	float4 tail_distortion(float2 uv : TEXCOORD0) : SV_TARGET
	{
		float2 tail_uv = uv;

#ifdef RT_TARGET
		tail_uv = invert_y(tail_uv);
#endif

		// clip all pixels if decay const is basically zero
		clip(_decay_const - 0.00001);

#ifdef LEFT_EYE
		// invert x-axis if we are targeting the left eye
		tail_uv = invert_x(tail_uv);
		_eye_gaze.x = -_eye_gaze.x;
#endif

#ifdef GRAB_PASS
		_eye_gaze.y = -_eye_gaze.y;
#endif

		float2 eye_uv = tail_uv - _eye_gaze;

		// clip pixels far outside of the polyretina to increase efficiency
		clip_polyretina(eye_uv);

		float phosphene = tex2D(TEX, uv).g;

		float4 data = tex2D(_axon_tex, eye_uv);
		float tail = calculate_tail(data, eye_uv);
		float outside_od = step(4.0, data.g);
		float luminance = max(phosphene, tail) * outside_od;

		// final output
		float4 output = float4(luminance.xxx, 1.0);

#ifdef OUTLINE
		// draw outline
		output += (outline_polyretina(eye_uv, _headset_diameter, _polyretina_radius) +
			outline_polyretina(eye_uv, _headset_diameter, FOV_BUFFER_OFFSET, _polyretina_radius + (FOV_BUFFER - 20)) +
			outline_optic_disc(data.g));
#endif

		return output;
	}

#endif

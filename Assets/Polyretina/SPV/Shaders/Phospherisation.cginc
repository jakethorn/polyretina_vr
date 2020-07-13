#ifndef PHOSPHERISATION_CGINC
#define PHOSPHERISATION_CGINC

	#include "UnityCG.cginc"
	#include "Coordinates.cginc"
	#include "Functions.cginc"

	/*
	 * Properties
	 */

#ifdef FIRST_PASS
	#define TEX _MainTex
#else
	#ifndef GRAB_PASS
	#define GRAB_PASS
	#endif
	#define TEX _GrabTexture
#endif

	sampler2D TEX;

	sampler2D _electrode_tex;
	float2 _eye_gaze;
	float2 _headset_diameter;
	float _electrode_radius;
	float _polyretina_radius;
	float _broken_chance;
	float _size_variance;
	float _intensity_variance;
	float _brightness;
	int _pulse;
	int _luminance_levels;

	/*
	 * Functions
	 */

	float rand(float seed_x, float seed_y, float seed_z)
	{
		return frac(sin(dot(float3(seed_x, seed_y, seed_z), float3(12.9898, 78.233, 45.5432))) * 43758.5453);
	}
	
	void calc_luminance(float2 eye_uv, out bool is_electrode, out bool electrode_is_on, out float luminance)
	{
		// electrode position
		float4 data = tex2D(_electrode_tex, eye_uv);
		float2 position = data.rg;
		
		// input luminance
		float3 input = tex2D(TEX, retina_to_pixel(position, _headset_diameter) + _eye_gaze).rgb;
		
		// electrode size, intensity and if broken
		float broken = step(data.b, _broken_chance);
		float size = 1.0 + lerp(-_size_variance, _size_variance, data.a);
		float intensity = 1.0 - lerp(0.0, _intensity_variance, rand(data.b, data.a, _Time.y));

		// distances
		float distance_to_electrode = distance(pixel_to_retina(eye_uv, _headset_diameter), position);
		float distance_to_fovea = length(position);
		
		// pixel is an electrode if... (out param)
		is_electrode =	step(distance_to_electrode, _electrode_radius * size)			*	// inside an electrode
						step(distance_to_fovea, _polyretina_radius - _electrode_radius)	*	// inside the polyretina
						(1 - broken);														// electrode is not broken
		
		// electrode luminance (out param)
		luminance = Luminance(input);														// base luminancy
		luminance = round(luminance * (_luminance_levels - 1)) / (_luminance_levels - 1);	// set to an interval of levels
		luminance *= is_electrode * _brightness * intensity;								// adjust luminance

		// electrode is on (out param)
		electrode_is_on = luminance > .001;

		// only when there is a pulse
		luminance *= _pulse;
	}

	float calc_luminance(float2 eye_uv)
	{
		bool is_electrode;
		bool electrode_is_on;
		float luminance;

		calc_luminance(eye_uv, is_electrode, electrode_is_on, luminance);
		return luminance;
	}

	/*
	 * Frag (without fading)
	 */

	float4 phospherisation(float2 uv : TEXCOORD0) : SV_TARGET
	{
#ifdef GRAB_PASS
		_eye_gaze.y = -_eye_gaze.y;
#endif

		float2 eye_uv = uv - _eye_gaze;
		float luminance = calc_luminance(eye_uv);
		float4 output = float4(luminance.xxx, 1.0);

#ifdef OUTLINE
		output += outline_polyretina(eye_uv, _headset_diameter, _polyretina_radius);
#endif

		return output;
	}

	/*
	 * Fading
	 */
	 
	/*
	 * Properties
	 */

	sampler2D _fade_tex;
	
	/*
	 * Constants
	 */

	static const float _fast_decay_frames = 3;	// .2 is for the 1/ 5 pulse rate
	static const float _fast_decay_rate = .175;
	static const float _slow_decay_rate = .00167;
	static const float _decay_exponent = 2;

	static const float _recovery_time = 30.0;
	static const float _recovery_exponent = 1.667;
	static const float _recovery_threshold = .95;
	
	/*
	 * Functions
	 */

	bool pulse_is_on()
	{
		return _pulse == 1;
	}

	float delta_time()
	{
		return unity_DeltaTime.r;
	}

	float decay(float brightness, float rate)
	{
		float inv_brightness = 1 - brightness;
		float exponential_rate = 1 + inv_brightness * _decay_exponent;

		return brightness - rate * exponential_rate;
	}

	float pythagoras(float r, float x)
	{
		return sqrt((r * r) - (x * x));
	}

	float recover(float off_time, float starting_brightness)
	{
		starting_brightness = pow(starting_brightness, 1 / _recovery_exponent);
		
		// calculate starting x (time) value
		float start_y = starting_brightness * _recovery_time;
		float start_x = pythagoras(_recovery_time, start_y);
		
		// calculate y (brightness) value
		float inv_time = start_x - off_time;
		if (inv_time > 0)
		{
			float y = pythagoras(_recovery_time, inv_time) / _recovery_time;
			y = pow(y, _recovery_exponent);

			return y;
		}
		else
		{
			return 1;
		}
	}

	float4 update_fade(bool electrode_is_on, float4 fade_data)
	{
		float brightness = fade_data.r;
		float on_frames = fade_data.g;
		float off_time = fade_data.b;
		float starting_brightness = fade_data.a;

		if (electrode_is_on == false)
		{
			on_frames = .5;
		}

		if (electrode_is_on && pulse_is_on())
		{
			if (on_frames < _fast_decay_frames)
			{
				brightness = decay(brightness, _fast_decay_rate);
			}
			else if (brightness > _recovery_threshold)
			{
				on_frames = .5;
				brightness = decay(brightness, _fast_decay_rate);
			}
			else
			{
				brightness = decay(brightness, _slow_decay_rate);
			}

			starting_brightness = clamp(brightness, .001, 1);

			on_frames += 1;
			off_time = 0;
		}
		else
		{
			brightness = recover(off_time, starting_brightness);
			off_time += delta_time();
		}

		brightness = clamp(brightness, .001, 1);

		return float4(brightness, on_frames, off_time, starting_brightness);
	}

	/*
	 * Frag (with fading)
	 */

	struct MRT
	{
		float4 phos : SV_TARGET0;
		float4 fade : SV_TARGET1;
	};

	MRT phos_w_fade_mrt(float2 uv : TEXCOORD0)
	{
#ifdef GRAB_PASS
		_eye_gaze.y = -_eye_gaze.y;
#endif

		float2 eye_uv = uv - _eye_gaze;
		
		bool is_electrode;
		bool electrode_is_on;
		float luminance;

		calc_luminance(eye_uv, is_electrode, electrode_is_on, luminance);
		float4 fade_data = tex2D(_fade_tex, uv);
		luminance *= fade_data.r;				
		
		MRT mrt;
		mrt.phos = float4(luminance.xxx, 1.0);
		mrt.fade = update_fade(electrode_is_on, fade_data);

#ifdef OUTLINE
		mrt.phos += outline_polyretina(eye_uv, _headset_diameter, _polyretina_radius);
#endif

		return mrt;
	}
#endif

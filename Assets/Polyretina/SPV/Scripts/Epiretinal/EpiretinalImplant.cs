﻿using UnityEngine;

namespace LNE.ProstheticVision
{
	using PostProcessing;

	using SP = ShaderProperties;

	[CreateAssetMenu(fileName = "Epiretinal Implant", menuName = "LNE/Implants/Epiretinal Implant")]
	public class EpiretinalImplant : Implant
	{
		/*
		 * Public fields
		 */

		[Header("Image Processing")]
		public HeadsetModel headset = HeadsetModel.VivePro;
		public StereoTargetEyeMask targetEye = StereoTargetEyeMask.Right;
		public bool overrideCameraFOV = true;
		public bool overrideRefreshRate = true;

		[Header("Model")]
		public ElectrodePattern pattern = ElectrodePattern.POLYRETINA;
		public ElectrodeLayout layout = ElectrodeLayout._80x150;
		public float fieldOfView =	46.3f;
		public int onFrames = 1;
		public int offFrames = 4;

		[Range(2, 16)]
		public int luminanceLevels = 2;

		[Range(0, 1)]
		public float brightness = 1;

		[Header("Variance")]
		[Range(0, 1)]
		public float brokenChance = .1f;
		[Range(0, 1)]
		public float sizeVariance = .3f;
		[Range(0, 1)]
		public float intensityVariance = .5f;

		[Header("Tail Distortion")]
		public Strength tailQuality = Strength.High;
		[Range(0, 3)]
		public float tailLength = 2;

		[Header("Fading")]
		public bool useFading;

		[Header("Preprocessed Data")]
		public EpiretinalData epiretinalData;

		[Header("Eye Tracking")]
		public EyeGaze.Source eyeGazeSource = EyeGaze.Source.EyeTracking;

		[Header("Debugging")]
		public bool outlineDevice = false;

		/*
		 * Private fields
		 */

		private DoubleBufferedRenderTexture fadeRT;
		private Material phosMRT;
		private Material tailBlr;

		// used to detect changes in these enums in the update function to efficiently upload texture data to the GPU
		private HeadsetModel lastHeadset;
		private ElectrodePattern lastPattern;
		private ElectrodeLayout lastLayout;

		/*
		 * Private properties
		 */

		// Pulse cycle is: 10ms on, 40ms off
		// The Vive Pro refresh rate is 90Hz, which is about 11ms
		// Until we find a headset with a 100Hz+ refresh rate, just treat each frame as 10ms (although its not)
		public bool Pulse => Time.frameCount % (onFrames + offFrames) < onFrames;

		public DoubleBufferedRenderTexture FadeRT => fadeRT;

		/*
		 * Inherited methods
		 */

		public override void Start()
		{
			// load shaders
			phosMRT = new Material(Shader.Find("LNE/Phospherisation (MRT)"));
			tailBlr = new Material(Shader.Find("LNE/Tail Distortion (w/ Blur)"));

			if (phosMRT == null || tailBlr == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			// upload electrode/axon textures to the GPU
			phosMRT.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));
			tailBlr.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));

			// create texture for the fading data
			fadeRT = new DoubleBufferedRenderTexture(headset.GetWidth(), headset.GetHeight());
			fadeRT.Initialise(new Color(1, .5f, 0, 1));

			// overrides
			if (overrideCameraFOV)
			{
				Prosthesis.Instance.Camera.fieldOfView = headset.GetFieldOfView(Axis.Vertical);
			}

			if (overrideRefreshRate)
			{
				Application.targetFrameRate = headset.GetRefreshRate();
			}

			// cache texture-related variables
			lastHeadset = headset;
			lastPattern = pattern;
			lastLayout = layout;

			// initialise eye gaze tracking
			EyeGaze.Initialise(eyeGazeSource, headset);
		}

		public override void Update()
		{
			if (phosMRT == null || tailBlr == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			/*
			 * Set shader propertes
			 */

			//
			// textures are only updated when necessary for efficiencies sake
			//

			// axon texture
			if (headset != lastHeadset)
			{
				tailBlr.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));

				if (overrideCameraFOV)
				{
					Prosthesis.Instance.Camera.fieldOfView = headset.GetFieldOfView(Axis.Vertical);
				}

				if (overrideRefreshRate)
				{
					Application.targetFrameRate = headset.GetRefreshRate();
				}

				lastHeadset = headset;
			}

			// phosphene texture
			if (headset != lastHeadset || pattern != lastPattern || layout != lastLayout)
			{
				phosMRT.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));

				lastHeadset = headset;
				lastPattern = pattern;
				lastLayout = layout;
			}

			//
			// everything else is updated every frame
			// there is already per-frame data that needs to be uploaded to graphics card anyway (e.g., eye gaze, pulse), 
			//	so adding a few more floats probably isn't a big performance hit (probably, definitely not tested)
			//

			// headset diameter
			var headsetDiameter = headset.GetRetinalDiameter();
			phosMRT.SetVector(SP.headsetDiameter, headsetDiameter);
			tailBlr.SetVector(SP.headsetDiameter, headsetDiameter);

			// electrode radius
			phosMRT.SetFloat(SP.electrodeRadius, layout.GetRadius(LayoutUsage.Theoretical));

			// implant radius
			var implantRadius = CoordinateSystem.FovToRetinalRadius(fieldOfView);
			phosMRT.SetFloat(SP.polyretinaRadius, implantRadius);
			tailBlr.SetFloat(SP.polyretinaRadius, implantRadius);

			// pulse
			phosMRT.SetInt(SP.pulse, Pulse ? 1 : 0);

			// levels
			phosMRT.SetInt(SP.luminanceLevels, luminanceLevels);

			// brightness
			phosMRT.SetFloat(SP.brightness, brightness);

			// variance
			phosMRT.SetFloat(SP.sizeVariance, sizeVariance);
			phosMRT.SetFloat(SP.intensityVariance, intensityVariance);
			phosMRT.SetFloat(SP.brokenChance, brokenChance);

			// decay constant
			tailBlr.SetFloat(SP.decayConst, tailLength);

			// eye gaze
			var eyeGaze = EyeGaze.Get(eyeGazeSource, headset);
			phosMRT.SetVector(SP.eyeGaze, eyeGaze);
			tailBlr.SetVector(SP.eyeGaze, eyeGaze);

			// keywords
			UpdateKeyword("USE_FADING", useFading);
			UpdateKeyword("RT_TARGET", Prosthesis.Instance.Camera.targetTexture != null);
			UpdateKeyword("OUTLINE", outlineDevice);
			UpdateTailQuality();

			// fading (can safely be uploaded every frame because it is just a RenderTexture pointer)
			phosMRT.SetTexture(SP.fadeTexture, fadeRT.Back);

			phosMRT.SetVector("_eye_gaze_delta", EyeGaze.GetDelta(eyeGazeSource, headset));
		}

		public override void GetDimensions(out int width, out int height)
		{
			width = headset.GetWidth();
			height = headset.GetHeight();
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			if (phosMRT == null || tailBlr == null)
			{
				Debug.LogError($"{name} does not have a material.");
				Graphics.Blit(source, destination);
				return;
			}

			if (on)
			{
				var tempRT = RenderTexture.GetTemporary(headset.GetWidth(), headset.GetHeight());
				Graphics.SetRenderTarget(new[] { tempRT.colorBuffer, fadeRT.Front.colorBuffer }, tempRT.depthBuffer);
				Graphics.Blit(source, phosMRT);
				Graphics.Blit(tempRT, destination, tailBlr);
				RenderTexture.ReleaseTemporary(tempRT);

				fadeRT.Swap();
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		private void UpdateKeyword(string keyword, bool condition)
		{
			if (condition && !phosMRT.IsKeywordEnabled(keyword))
			{
				phosMRT.EnableKeyword(keyword);
			}
			else if (!condition && phosMRT.IsKeywordEnabled(keyword))
			{
				phosMRT.DisableKeyword(keyword);
			}

			if (condition && !tailBlr.IsKeywordEnabled(keyword))
			{
				tailBlr.EnableKeyword(keyword);
			}
			else if (!condition && tailBlr.IsKeywordEnabled(keyword))
			{
				tailBlr.DisableKeyword(keyword);
			}
		}

		private void UpdateTailQuality()
		{
			if (tailQuality == Strength.High && tailBlr.IsKeywordEnabled("HIGH_QUALITY") == false)
			{
				tailBlr.EnableKeyword("HIGH_QUALITY");
				tailBlr.DisableKeyword("MEDIUM_QUALITY");
				tailBlr.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Medium && tailBlr.IsKeywordEnabled("MEDIUM_QUALITY") == false)
			{
				tailBlr.DisableKeyword("HIGH_QUALITY");
				tailBlr.EnableKeyword("MEDIUM_QUALITY");
				tailBlr.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Low && tailBlr.IsKeywordEnabled("LOW_QUALITY") == false)
			{
				tailBlr.DisableKeyword("HIGH_QUALITY");
				tailBlr.DisableKeyword("MEDIUM_QUALITY");
				tailBlr.EnableKeyword("LOW_QUALITY");
			}
		}
	}
}

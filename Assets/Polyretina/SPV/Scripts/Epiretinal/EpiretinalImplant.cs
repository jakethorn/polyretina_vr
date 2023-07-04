using UnityEngine;

namespace LNE.ProstheticVision
{
	using LNE.UI.Attributes;

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
		public ElectrodeLayout layout = ElectrodeLayout._80x120;
		public float fieldOfView =	45f;
		public int onFrames = 1;
		public int offFrames = 17;

		[Range(2, 16)]
		public int luminanceLevels = 2;

		[Range(0, 1)]
		public float luminanceBoost = 0;

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

		[Header("    Decay")]
		[CustomLabel(label = "    τ1")]
		public float decayT1 = .1f;
		[CustomLabel(label = "    τ2")]
		public float decayT2 = .3f;
		[CustomLabel(label = "    Threshold")]
		public float decayThreshold = .5f;

		[Header("    Recovery")]
		[CustomLabel(label = "    Delay")]
		public float recoveryDelay = 2;
		[CustomLabel(label = "    Time")]
		public float recoveryTime = 2;
		[CustomLabel(label = "    Exponent")]
		public float recoveryExponent = 3;

		[Header("Preprocessed Data")]
		public EpiretinalData epiretinalData;

		[Header("Eye Tracking")]
		public EyeGaze.Source eyeGazeSource = EyeGaze.Source.EyeTracking;

		[Header("Debugging")]
		public bool outlineDevice = false;

		/*
		 * Private fields
		 */

		protected DoubleBufferedRenderTexture fadeRT;
		protected Material phos;
		protected Material tail;

		// used to detect changes in these enums in the update function to efficiently upload texture data to the GPU
		protected HeadsetModel lastHeadset;
		protected ElectrodePattern lastPattern;
		protected ElectrodeLayout lastLayout;

		/*
		 * Public properties
		 */

		// Pulse cycle is: 10ms on, 40ms off
		// The Vive Pro refresh rate is 90Hz, which is about 11ms
		// Until we find a headset with a 100Hz+ refresh rate, just treat each frame as 10ms (although its not)
		public bool Pulse => Time.frameCount % (onFrames + offFrames) < onFrames;

		public DoubleBufferedRenderTexture FadeRT => fadeRT;

		/*
		 * Public methods
		 */

		public override void Start()
		{
			Initialise("LNE/Phospherisation (MRT)", "LNE/Tail Distortion (w/ Blur)");
		}

		public override void Update()
		{
			if (phos == null || tail == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			UpdatePerFrameData();

//#if UNITY_EDITOR
			// update this every frame only while in the editor
			UpdatePerChangeData();
//#endif
		}

		public override void GetDimensions(out int width, out int height)
		{
			width = headset.GetWidth();
			height = headset.GetHeight();
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			if (phos == null || tail == null)
			{
				Debug.LogError($"{name} does not have a material.");
				Graphics.Blit(source, destination);
				return;
			}

			if (on)
			{
				UpdateKeyword("RT_TARGET", destination != null);

				var tempRT = RenderTexture.GetTemporary(headset.GetWidth(), headset.GetHeight());
				Graphics.SetRenderTarget(new[] { tempRT.colorBuffer, fadeRT.Front.colorBuffer }, tempRT.depthBuffer);
				Graphics.Blit(source, phos);
				Graphics.Blit(tempRT, destination, tail);
				RenderTexture.ReleaseTemporary(tempRT);

				fadeRT.Swap();
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		public void ResetFadingParameters()
		{
			fadeRT.Initialise(new Color(1, 0, 0, 0));
		}

		public void SwapTargetEye()
		{
			if (targetEye == StereoTargetEyeMask.Left)
			{
				targetEye = StereoTargetEyeMask.Right;
			}
			else
			{
				targetEye = StereoTargetEyeMask.Left;
			}
		}

		/*
		 * Protected methods
		 */

		protected void Initialise(string phosShader, string tailShader)
        {
			// load shaders
			phos = new Material(Shader.Find(phosShader));
			tail = new Material(Shader.Find(tailShader));

			if (phos == null || tail == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			// upload electrode/axon textures to the GPU
			phos.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));
			tail.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));

			// create texture for the fading data
			fadeRT = new DoubleBufferedRenderTexture(headset.GetWidth(), headset.GetHeight());
			fadeRT.Initialise(new Color(1, 0, 0, 0));

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

			// initialise camera target eye
			UpdateCameraTargetEye();
		}

		protected void UpdateKeyword(string keyword, bool condition)
		{
			if (condition && !phos.IsKeywordEnabled(keyword))
			{
				phos.EnableKeyword(keyword);
			}
			else if (!condition && phos.IsKeywordEnabled(keyword))
			{
				phos.DisableKeyword(keyword);
			}

			if (condition && !tail.IsKeywordEnabled(keyword))
			{
				tail.EnableKeyword(keyword);
			}
			else if (!condition && tail.IsKeywordEnabled(keyword))
			{
				tail.DisableKeyword(keyword);
			}
		}

		/*
		 * Private methods
		 */

		private void UpdatePerFrameData()
		{
			// pulse
			phos.SetInt(SP.pulse, Pulse ? 1 : 0);

			if (useFading)
			{
				// fading (can safely be uploaded every frame because it is just a RenderTexture pointer)
				phos.SetTexture(SP.fadeTexture, fadeRT.Back);
			}

			// eye gaze
			var eyeGaze = EyeGaze.Get(eyeGazeSource, headset);
			phos.SetVector(SP.eyeGaze, eyeGaze);
			tail.SetVector(SP.eyeGaze, eyeGaze);

			// eye gaze delta
			phos.SetVector(SP.eyeGazeDelta, EyeGaze.GetDelta(eyeGazeSource, headset));
		}

		private void UpdatePerChangeData()
		{
			//
			// textures are only updated when necessary for efficiencies sake
			//

			// axon texture
			if (headset != lastHeadset)
			{
				tail.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));

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
				phos.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));

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
			phos.SetVector(SP.headsetDiameter, headsetDiameter);
			tail.SetVector(SP.headsetDiameter, headsetDiameter);

			// electrode radius
			phos.SetFloat(SP.electrodeRadius, layout.GetRadius(LayoutUsage.Anatomical));

			// implant radius
			var implantRadius = CoordinateSystem.FovToRetinalRadius(fieldOfView);
			phos.SetFloat(SP.polyretinaRadius, implantRadius);
			tail.SetFloat(SP.polyretinaRadius, implantRadius);

			// brightness
			phos.SetFloat(SP.brightness, brightness);

			// luminance levels
			phos.SetInt(SP.luminanceLevels, luminanceLevels);

			// luminance boost
			var range = 1 - (1f / luminanceLevels);
			phos.SetFloat(SP.luminanceBoost, luminanceBoost * range);

			// variance
			phos.SetFloat(SP.sizeVariance, sizeVariance);
			phos.SetFloat(SP.intensityVariance, intensityVariance);
			phos.SetFloat(SP.brokenChance, brokenChance);

			// decay constant
			tail.SetFloat(SP.decayConst, tailLength);

			// update decay/recovery fading parameters
			UpdateDecayParameters(decayT1, decayT2, decayThreshold);
			UpdateRecoveryParameters(recoveryDelay, recoveryTime, recoveryExponent);

			// keywords
			UpdateKeyword("USE_FADING", useFading);
			UpdateKeyword("RT_TARGET", Prosthesis.Instance.Camera.targetTexture != null);
			UpdateKeyword("OUTLINE", outlineDevice);
			UpdateTailQuality();
			UpdateTargetEye();
		}

		private void UpdateDecayParameters(float fastTime, float slowTime, float threshold)
		{
			phos.SetFloat(SP.fastDecayTime, fastTime);
			phos.SetFloat(SP.slowDecayTime, slowTime);
			phos.SetFloat(SP.decayThreshold, threshold);

			phos.SetFloat(SP.fastDecayRate, (1 / fastTime) * (1 - threshold));
			phos.SetFloat(SP.slowDecayRate, (1 / slowTime) * threshold);
		}

		private void UpdateRecoveryParameters(float delay, float time, float exponent)
		{
			phos.SetFloat(SP.recoveryDelay, delay);
			phos.SetFloat(SP.recoveryTime, time);
			phos.SetFloat(SP.recoveryExponent, exponent);
		}

		private void UpdateTailQuality()
		{
			if (tailQuality == Strength.High && tail.IsKeywordEnabled("HIGH_QUALITY") == false)
			{
				tail.EnableKeyword("HIGH_QUALITY");
				tail.DisableKeyword("MEDIUM_QUALITY");
				tail.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Medium && tail.IsKeywordEnabled("MEDIUM_QUALITY") == false)
			{
				tail.DisableKeyword("HIGH_QUALITY");
				tail.EnableKeyword("MEDIUM_QUALITY");
				tail.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Low && tail.IsKeywordEnabled("LOW_QUALITY") == false)
			{
				tail.DisableKeyword("HIGH_QUALITY");
				tail.DisableKeyword("MEDIUM_QUALITY");
				tail.EnableKeyword("LOW_QUALITY");
			}
		}

		private void UpdateTargetEye()
		{
			if (targetEye == StereoTargetEyeMask.Left && tail.IsKeywordEnabled("LEFT_EYE") == false)
			{
				tail.EnableKeyword("LEFT_EYE");
				tail.DisableKeyword("RIGHT_EYE");

				UpdateCameraTargetEye();
			}
			else if (targetEye == StereoTargetEyeMask.Right && tail.IsKeywordEnabled("RIGHT_EYE") == false)
			{
				tail.DisableKeyword("LEFT_EYE");
				tail.EnableKeyword("RIGHT_EYE");

				UpdateCameraTargetEye();
			}
		}

		private void UpdateCameraTargetEye()
		{
			Prosthesis.Instance.Camera.stereoTargetEye = targetEye;
			var otherEye = GameObject.Find("Black Eye");
			if (otherEye != null)
			{
				var otherTargetEye = targetEye == StereoTargetEyeMask.Right ? StereoTargetEyeMask.Left : StereoTargetEyeMask.Right;
				otherEye.GetComponent<Camera>().stereoTargetEye = otherTargetEye;
			}
			else
			{
				Debug.LogWarning("Cannot find other eye!");
			}
		}
	}
}

using UnityEngine;

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
		public Shader shader = null;
		public bool overrideCameraFOV = true;

		[Header("Model")]
		public ElectrodePattern pattern = ElectrodePattern.Polyretina2019;
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

		// new >>
		[Header("Fading")]
		public bool useFading;
		// new >>

		[Header("Preprocessed Data")]
		public EpiretinalData epiretinalData;

		[Header("Debugging")]
		public EyeGaze.Source eyeGazeSource = EyeGaze.Source.EyeTracking;
		public bool outlineDevice = false;

		/*
		 * Private fields
		 */

		private HeadsetModel lastHeadset;
		private ElectrodePattern lastPattern;
		private ElectrodeLayout lastLayout;
		private Material material;

		// new >>
		private DoubleBufferedRenderTexture fadeRT;
		private Material phosMRT;
		private Material tailBlr;
		// new >>

		/*
		 * Private properties
		 */

		// Pulse cycle is: 10ms on, 40ms off
		// The Vive Pro refresh rate is 90Hz, which is about 11ms
		// Until we find a headset with a 100Hz+ refresh rate, just treat each frame as 10ms (although its not)
		public bool Pulse => Time.frameCount % (onFrames + offFrames) < onFrames;

		// new >>
		public DoubleBufferedRenderTexture FadeRT => fadeRT;
		// new >>

		/*
		 * Inherited methods
		 */

		public override void Start()
		{
			if (shader == null && material == null)
			{
				Debug.LogError($"{name} does not have a shader.");
				return;
			}

			// cache texture-related variables
			lastHeadset = headset;
			lastPattern = pattern;
			lastLayout = layout;

			// material
			material = new Material(shader);

			// phosphene texture
			material.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));

			// axon texture
			material.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));

			if (overrideCameraFOV)
			{
				Prosthesis.Instance.Camera.fieldOfView = headset.GetFieldOfView(Axis.Vertical);
			}

			// new >>
			fadeRT = new DoubleBufferedRenderTexture(headset.GetWidth(), headset.GetHeight());
			fadeRT.Initialise(new Color(1, 0, 0, 1));

			phosMRT = new Material(Shader.Find("LNE/Phospherisation (MRT)"));
			tailBlr = new Material(Shader.Find("LNE/Tail Distortion (w/ Blur)"));

			phosMRT.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));
			tailBlr.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));
			// new >>

			// new >>
			// fading doesn't start immediately, this is a quick fix
			brightness = 0;
			Threading.CallbackManager.InvokeOnce(1, () => { brightness = 1; });
			// new >>
		}

		public override void Update()
		{
			// new >>
			if (useFading)
			{
				Update_MRT();
				return;
			}
			// new >>

			if (material == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			if (headset != lastHeadset && overrideCameraFOV)
			{
				Prosthesis.Instance.Camera.fieldOfView = headset.GetFieldOfView(Axis.Vertical);
			}

			/*
			 * Set shader propertes
			 */

			//
			// textures are only performed when necessary
			//

			// axon texture
			if (headset != lastHeadset)
			{
				material.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));
			}

			// phosphene texture
			if (headset != lastHeadset || pattern != lastPattern || layout != lastLayout)
			{
				material.SetTexture(SP.electrodeTexture, epiretinalData.GetPhospheneTexture(headset, pattern, layout));

				lastHeadset = headset;
				lastPattern = pattern;
				lastLayout = layout;
			}

			//
			// everything else is updated every frame
			// there is already per-frame data that needs to be uploaded to graphics card anyway (e.g., eye gaze), 
			//	so adding a few more floats probably isn't a big performance hit (probably, definitely not tested)
			//

			// headset diameter
			material.SetVector(SP.headsetDiameter, headset.GetRetinalDiameter());

			// electrode radius
			material.SetFloat(SP.electrodeRadius, layout.GetRadius(LayoutUsage.Theoretical));

			// implant radius
			var implantRadius = CoordinateSystem.FovToRetinalRadius(fieldOfView);
			material.SetFloat(SP.polyretinaRadius, implantRadius);

			// pulse
			material.SetInt(SP.pulse, Pulse ? 1 : 0);

			// levels
			material.SetInt(SP.luminanceLevels, luminanceLevels);

			// brightness
			material.SetFloat(SP.brightness, brightness);

			// variance
			material.SetFloat(SP.sizeVariance, sizeVariance);
			material.SetFloat(SP.intensityVariance, intensityVariance);
			material.SetFloat(SP.brokenChance, brokenChance);

			// decay constant
			material.SetFloat(SP.decayConst, tailLength);

			// eye gaze
			material.SetVector(SP.eyeGaze, EyeGaze.Get(eyeGazeSource, headset));

			UpdateKeyword("RT_TARGET", Prosthesis.Instance.Camera.targetTexture != null);
			UpdateKeyword("OUTLINE", outlineDevice);
			UpdateTailQuality();
		}

		public override void GetDimensions(out int width, out int height)
		{
			width = headset.GetWidth();
			height = headset.GetHeight();
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			// new >>
			if (useFading)
			{
				OnRenderImage_MRT(source, destination);
				return;
			}
			// new >>

			if (material == null)
			{
				Debug.LogError($"{name} does not have a material.");
				Graphics.Blit(source, destination);
				return;
			}

			if (on)
			{
				Graphics.Blit(source, destination, material);
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		private void UpdateKeyword(string keyword, bool condition)
		{
			if (condition && !material.IsKeywordEnabled(keyword))
			{
				material.EnableKeyword(keyword);
			}
			else if (!condition && material.IsKeywordEnabled(keyword))
			{
				material.DisableKeyword(keyword);
			}
		}

		private void UpdateTailQuality()
		{
			if (tailQuality == Strength.High && material.IsKeywordEnabled("HIGH_QUALITY") == false)
			{
				material.EnableKeyword("HIGH_QUALITY");
				material.DisableKeyword("MEDIUM_QUALITY");
				material.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Medium && material.IsKeywordEnabled("MEDIUM_QUALITY") == false)
			{
				material.DisableKeyword("HIGH_QUALITY");
				material.EnableKeyword("MEDIUM_QUALITY");
				material.DisableKeyword("LOW_QUALITY");
			}
			else if (tailQuality == Strength.Low && material.IsKeywordEnabled("LOW_QUALITY") == false)
			{
				material.DisableKeyword("HIGH_QUALITY");
				material.DisableKeyword("MEDIUM_QUALITY");
				material.EnableKeyword("LOW_QUALITY");
			}
		}

		private void Update_MRT()
		{
			if (phosMRT == null || tailBlr == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			if (headset != lastHeadset && overrideCameraFOV)
			{
				Prosthesis.Instance.Camera.fieldOfView = headset.GetFieldOfView(Axis.Vertical);
			}

			/*
			 * Set shader propertes
			 */

			//
			// textures are only performed when necessary
			//

			// axon texture
			if (headset != lastHeadset)
			{
				tailBlr.SetTexture(SP.axonTexture, epiretinalData.GetAxonTexture(headset));
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
			// there is already per-frame data that needs to be uploaded to graphics card anyway (e.g., eye gaze), 
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

			UpdateKeyword_MRT("RT_TARGET", Prosthesis.Instance.Camera.targetTexture != null);
			UpdateKeyword_MRT("OUTLINE", outlineDevice);
			UpdateTailQuality_MRT();

			phosMRT.SetTexture(SP.fadeTexture, fadeRT.Back);
		}

		private void OnRenderImage_MRT(Texture source, RenderTexture destination)
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

		private void UpdateKeyword_MRT(string keyword, bool condition)
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

		private void UpdateTailQuality_MRT()
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

﻿using LNE.UI.Attributes;
using UnityEngine;

namespace LNE.ProstheticVision
{
	[CreateAssetMenu(fileName = "Saccade Simulator", menuName = "LNE/External Processor/Saccade Simulator")]
	public class SaccadeSimulator : EdgeDetector
	{
		public enum SaccadeType { None = 1, Left = 2, LeftAndRight = 3, Hexagonal = 7 }

		/*
		 * Public fields
		 */

		public bool useEdgeDetection;

		[Header("Saccade")]

		[CustomLabel(label = "Shader")]
		public Shader saccadeShader = null;

		[CustomLabel(label = "Type")]
		public SaccadeType saccadeType = SaccadeType.None;

		[CustomLabel(label = "Frequency")]
		public int saccadeFrequency = 1;

		/*
		 * Private fields
		 */

		private Material saccadeMaterial = null;

		private int frameCount = 0;

		/*
		 * Private properties
		 */

		private EpiretinalImplant Implant => Prosthesis.Instance.Implant as EpiretinalImplant;

		/*
		 * Edge Detector overrides
		 */

		public override void Start()
		{
			base.Start();

			if (saccadeShader == null)
			{
				Debug.LogError($"{name} does not have a shader.");
				return;
			}

			saccadeMaterial = new Material(saccadeShader);
		}

		public override void Update()
		{
			base.Update();

			// update material properties
			saccadeMaterial.SetInt("_type", (int)saccadeType);
			saccadeMaterial.SetInt("_frequency", saccadeFrequency);
			saccadeMaterial.SetInt("_frame_count", frameCount++);

			saccadeMaterial.SetVector("_headset_diameter", Implant.headset.GetRetinalDiameter());
			saccadeMaterial.SetFloat("_electrode_pitch", Implant.layout.GetSpacing(LayoutUsage.Anatomical));
			saccadeMaterial.SetInt("_pulse_frequency", Implant.onFrames + Implant.offFrames);
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			var tempRT = RenderTexture.GetTemporary(source.width, source.height);

			var cachedOn = on;
			on = useEdgeDetection;
			base.OnRenderImage(source, tempRT);
			on = cachedOn;

			if (saccadeMaterial == null)
			{
				Debug.LogError($"{name} does not have a material.");
				Graphics.Blit(tempRT, destination);
				return;
			}

			if (on)
			{
				Graphics.Blit(tempRT, destination, saccadeMaterial);
			}
			else
			{
				Graphics.Blit(tempRT, destination);
			}

			RenderTexture.ReleaseTemporary(tempRT);
		}
	}
}

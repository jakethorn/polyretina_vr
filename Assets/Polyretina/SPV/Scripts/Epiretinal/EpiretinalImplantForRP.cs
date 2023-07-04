using UnityEngine;

namespace LNE.ProstheticVision
{
	using LNE.UI.Attributes;

	using SP = ShaderProperties;

	[CreateAssetMenu(fileName = "Epiretinal Implant for RP", menuName = "LNE/Implants/Epiretinal Implant for RP")]
	public class EpiretinalImplantForRP : EpiretinalImplant
	{
		[Header("Natural Vision")]

		[Range(0, 110)]
		public float width = 10;

		[Range(0, 1)]
		public float saturation = 0.1f;

		[Range(0, 1)]
		public float _brightness = 1;	// preceeding _ is necessary because of EpiretinalImplant.brightness

		[Range(0, 1)]
		public float contrast = 1;

		public Strength acuity = Strength.High;

		[Space, Range(0, 110)]
		public float implantBuffer = 1;

		private Material rpMat = null;

		public override void Start()
		{
			base.Start();
			UpdateKeyword("FOR_RP", true);

			rpMat = new Material(Shader.Find("LNE/Retinitis Pigmentosa"));
		}

		public override void Update()
		{
			base.Update();

			var nvw = CoordinateSystem.FovToRetinalRadius(width + implantBuffer);
			phos.SetFloat("_natural_vision_width", nvw);
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			GetDimensions(out var width, out var height);
			var tempRT = RenderTexture.GetTemporary(width, height);

			base.OnRenderImage(source, tempRT);

			rpMat.SetTexture("_ProsTex", tempRT);
			rpMat.SetVector(SP.eyeGaze, EyeGaze.Get(eyeGazeSource, headset));
			rpMat.SetVector(SP.headsetDiameter, headset.GetRetinalDiameter());
			rpMat.SetFloat("_natural_vision_width", this.width);
			rpMat.SetFloat("_natural_vision_saturation", saturation);
			rpMat.SetFloat("_natural_vision_brightness", _brightness);
			UpdateAcuity();
			Graphics.Blit(Prosthesis.Source, destination, rpMat);

			RenderTexture.ReleaseTemporary(tempRT);
		}

		private void UpdateAcuity()
		{
			if (acuity == Strength.High && rpMat.IsKeywordEnabled("TAP_1") == false)
			{
				rpMat.EnableKeyword("TAP_1");
				rpMat.DisableKeyword("TAP_5");
				rpMat.DisableKeyword("TAP_13");
			}
			else if (acuity == Strength.Medium && rpMat.IsKeywordEnabled("TAP_5") == false)
			{
				rpMat.DisableKeyword("TAP_1");
				rpMat.EnableKeyword("TAP_5");
				rpMat.DisableKeyword("TAP_13");
			}
			else if (acuity == Strength.Low && rpMat.IsKeywordEnabled("TAP_13") == false)
			{
				rpMat.DisableKeyword("TAP_1");
				rpMat.DisableKeyword("TAP_5");
				rpMat.EnableKeyword("TAP_13");
			}
		}
	}
}

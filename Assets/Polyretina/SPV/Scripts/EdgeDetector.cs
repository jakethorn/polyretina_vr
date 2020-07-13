using UnityEngine;

namespace LNE.ProstheticVision
{
	using SP = ShaderProperties;

	[CreateAssetMenu(fileName = "Edge Detector", menuName = "LNE/External Processor/Edge Detector")]
	public class EdgeDetector : ExternalProcessor
	{
		/*
		 * Public fields
		 */

		[Space]
		public Shader shader = null;

		public Strength sensitivity = Strength.Medium;

		[Range(.1f, 20)]
		public float contrast = 1;
		[Range(-.5f, .5f)]
		public float brightness = 0;
		[Range(-10, 10)]
		public float saturation = 2;

		[Range(0, 6)]
		public int thickness = 0;

		[Range(0, 1)]
		public float threshold = .5f;

		/*
		 * Public properties
		 */

		public Material Material { get; set; }

		/*
		 * ImageRenderer overrides
		 */

		public override void Start()
		{
			if (shader == null)
			{
				Debug.LogError($"{name} does not have a shader.");
				return;
			}

			Material = new Material(shader);
		}

		public override void Update()
		{
			if (Material == null)
			{
				Debug.LogError($"{name} does not have a material.");
				return;
			}

			UpdateSensitivity();

			Material.SetFloat(SP.edgeContrast, contrast);
			Material.SetFloat(SP.edgeBrightness, brightness);
			Material.SetFloat(SP.edgeSaturation, saturation);

			UpdateThickness();
			Material.SetFloat(SP.edgeThreshold, threshold);
		}

		public override void GetDimensions(out int width, out int height)
		{
			throw new System.Exception("Edge Detector does not have dimensions.");
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			if (Material == null)
			{
				Debug.LogError($"{name} does not have a material.");
				Graphics.Blit(source, destination);
				return;
			}

			if (on)
			{
				Graphics.Blit(source, destination, Material);
			}
			else
			{
				Graphics.Blit(source, destination);
			}
		}

		public void UpdateThickness()
		{
			if (Material.IsKeywordEnabled($"THICKNESS_{thickness}") == false)
			{
				SetThickness(thickness);
			}
		}

		public void UpdateSensitivity()
		{
			if (sensitivity == Strength.High && (Material.IsKeywordEnabled("TAP_5") || Material.IsKeywordEnabled("TAP_13")))
			{
				Material.DisableKeyword("TAP_5");
				Material.DisableKeyword("TAP_13");
			}
			else if (sensitivity == Strength.Medium && Material.IsKeywordEnabled("TAP_5") == false)
			{
				Material.EnableKeyword("TAP_5");
				Material.DisableKeyword("TAP_13");
			}
			else if (sensitivity == Strength.Low && Material.IsKeywordEnabled("TAP_13") == false)
			{
				Material.DisableKeyword("TAP_5");
				Material.EnableKeyword("TAP_13");
			}
		}

		/*
		 * Private methods
		 */

		private void SetThickness(int thickness)
		{
			Material.EnableKeyword($"THICKNESS_{thickness}");
			for (int i = 0; i <= 6; i++)
			{
				if (i != thickness)
				{
					Material.DisableKeyword($"THICKNESS_{i}");
				}
			}
		}
	}
}

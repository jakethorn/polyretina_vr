using UnityEngine;

namespace LNE.ProstheticVision
{
	using LNE.UI.Attributes;

	public class Prosthesis : Singleton<Prosthesis>
	{
		/*
		 * Editor fields
		 */

		[SerializeField, LinkWithProperty]
		private ExternalProcessor _externalProcessor;

		[SerializeField, LinkWithProperty]
		private Implant _implant;

		/*
		 * Public properties
		 */

		public ExternalProcessor ExternalProcessor
		{
			get => _externalProcessor;
			set => _externalProcessor = ImageRenderer.Initialise(this, value);
		}

		public Implant Implant
		{
			get => _implant;
			set => _implant = ImageRenderer.Initialise(this, value);
		}

		public Camera Camera => GetComponent<Camera>();

		public RenderTexture Source { get; private set; } = null;

		/*
		 * Unity callbacks
		 */

		void Start()
		{
			_externalProcessor = ImageRenderer.Initialise(this, _externalProcessor);
			_implant = ImageRenderer.Initialise(this, _implant);
		}

		void Update()
		{
			if (ExternalProcessor != null) 
				ExternalProcessor.Update();

			if (Implant != null) 
				Implant.Update();
		}

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			Source = source;

			if (ExternalProcessor != null && Implant != null)
			{
				Implant.GetDimensions(out var width, out var height);
				var tempRT = RenderTexture.GetTemporary(width, height);

				// render image using the external processor and implant
				ExternalProcessor.OnRenderImage(source, tempRT);
				Implant.OnRenderImage(tempRT, destination);

				RenderTexture.ReleaseTemporary(tempRT);
			}
			else if (ExternalProcessor != null && Implant == null)
			{
				ExternalProcessor.OnRenderImage(source, destination);
			}
			else if (ExternalProcessor == null && Implant != null)
			{
				Implant.OnRenderImage(source, destination);
			}
			else if (ExternalProcessor == null && Implant == null)
			{
				Graphics.Blit(source, destination);
			}

			Source = null;
		}
	}
}

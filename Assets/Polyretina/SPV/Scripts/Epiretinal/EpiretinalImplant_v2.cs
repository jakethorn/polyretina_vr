using UnityEngine;

namespace LNE.ProstheticVision
{
	[CreateAssetMenu(fileName = "Epiretinal Implant v2", menuName = "LNE/Implants/Epiretinal Implant v2")]
	public class EpiretinalImplant_v2 : EpiretinalImplant
	{
		/*
		 * Public fields
		 */

		[Header("Amplitude")]
		[Range(1, 6)]
		public int amplitude = 1;

		/*
		 * Private fields
		 */

		/*
		 * Public properties
		 */

		/*
		 * Public methods
		 */

		public override void Start()
		{
			Initialise("LNE/Phospherisation v2", "LNE/Tail Distortion v2");
		}

		public override void Update()
		{
			base.Update();

			// send amplitude data to the graphics card
		}
    }
}

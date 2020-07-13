#pragma warning disable 649

using UnityEngine;

namespace LNE
{
	public class RuntimeRefreshRate : MonoBehaviour
	{
		[SerializeField]
		private int _refreshRate;

		//[RuntimeInitializeOnLoadMethod]
		//void SetRefreshRate()
		//{
		//	Application.targetFrameRate = 90;
		//}

		void Awake()
		{
			Application.targetFrameRate = _refreshRate;
		}
	}
}

using System.Collections;
using System.Collections.Generic;
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

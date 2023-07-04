using UnityEngine;

namespace LNE
{
	/// <summary>
	/// Singleton pattern
	/// </summary>
	public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
	{
		private static T instance;

		public static T Instance
		{
			get
			{
				if (instance == null)
				{
					instance = GetInstance();
				}

				return instance;
			}
		}

		private static T GetInstance()
		{
			var objects = UnityApp.FindAllObjectsOfType<T>();
			if (objects.Length == 0)
			{
				return default;
			}
			else if (objects.Length > 1)
			{
				return objects[0];
			}
			else
			{
				return objects[0];
			}
		}
	}
}

using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

#if VIVE_PRO_EYE
using ViveSR.anipal.Eye;
#endif

namespace LNE.ProstheticVision
{
	using VectorExts;

	public static class EyeGaze
	{
		public enum Source { None, EyeTracking, Mouse, Custom }

#if VIVE_PRO_EYE
		private static EyeData_v2 eyeData = new EyeData_v2();
#endif

		private static Vector2 lastPos;
		private static Vector2 lastMousePos;

		public static Vector2 Offset { get; set; }

		public static Vector2 VivePro
		{
			get
			{
#if VIVE_PRO_EYE
				// looking camera
				var camera = Prosthesis.Instance.Camera;

				// looking plane (placed infront of camera)
				var plane = new Plane(-camera.transform.forward, camera.transform.forward);

				// looking direction (start from camera with camera normal as vec3(0, 0, 1))
				var eyeGaze =	camera.stereoTargetEye == StereoTargetEyeMask.Right		?
								eyeData.verbose_data.right.gaze_direction_normalized	:
								eyeData.verbose_data.left.gaze_direction_normalized		;

				eyeGaze.x *= -1;

				var direction = new Ray(camera.transform.position, camera.transform.rotation * eyeGaze);

				// looking position
				var position = AuxMath.Intersection(plane, direction);

				// position in screen space
				var screenPoint = camera.WorldToScreenPoint(position)
										.DivideXY(camera.pixelWidth, camera.pixelHeight)
										.SubtractXY(0.5f)
										.SubtractXY(Offset);

				return screenPoint;
#else
				return Vector2.zero;
#endif
			}
		}

		public static Vector2 Fove
		{
			get
			{
#if FOVE
				if (FoveInterface.IsEnabled() == false)
					return Vector2.zero;

				// looking camera
				var camera = Prosthesis.Instance.Camera;

				// looking plane (placed infront of camera)
				var plane = new Plane(-camera.transform.forward, camera.transform.forward);

				// looking direction (start from camera with camera normal as vec3(0, 0, 1))
				var direction = new Ray(camera.transform.position, camera.transform.rotation * FoveInterface.GetRightEyeVector());

				// looking position
				var position = AuxMath.Intersection(plane, direction);

				// position in screen space
				var screenPoint = camera.WorldToScreenPoint(position)
										.DivideXY(camera.pixelWidth, camera.pixelHeight)
										.SubtractXY(0.5f);

				return screenPoint;
#else
				return Vector2.zero;
#endif
			}
		}

		public static Vector2 Screen
		{
			get
			{
				if (Input.GetMouseButton(1))
				{
					var camera = Prosthesis.Instance?.Camera ?? Camera.main;
					lastMousePos = Input.mousePosition.DivideXY(camera.pixelWidth, camera.pixelHeight).SubtractXY(0.5f);
					return lastMousePos;
				}
				else
				{
					return lastMousePos;
				}
			}
		}

		public static Vector2 Custom { get; set; }

		public static Vector2 LastPosition
		{
			get => lastPos;
			set => lastPos = value;
		}

		public static void Initialise(HeadsetModel headset)
		{
			LastPosition = Get(headset);
		}

		public static void Initialise(Source source, HeadsetModel headset = HeadsetModel.None60)
		{
			LastPosition = Get(source, headset);
		}

		public static Vector2 Get(HeadsetModel headset)
		{
			switch (headset)
			{
				case HeadsetModel.VivePro:	return VivePro;
				case HeadsetModel.Fove:		return Fove;
				default:					return Vector2.zero;
			}
		}

		public static Vector2 Get(Source source, HeadsetModel headset = HeadsetModel.None60)
		{
			switch (source)
			{
				case Source.EyeTracking:	return Get(headset);
				case Source.Mouse:			return Screen;
				case Source.Custom:			return Custom;
				case Source.None:			return Vector2.zero;
				default:					return Vector2.zero;
			}
		}

		public static Vector2 GetDelta(HeadsetModel headset)
		{
			var position = Get(headset);
			var delta = LastPosition - position;
			LastPosition = position;

			return delta;
		}

		public static Vector2 GetDelta(Source source, HeadsetModel headset = HeadsetModel.None60)
		{
			var position = Get(source, headset);
			var delta = LastPosition - position;
			LastPosition = position;

			return delta;
		}

		public static float GetPupilDilation()
		{
#if VIVE_PRO_EYE
			return eyeData.verbose_data.right.pupil_diameter_mm;
#else
			return 0;
#endif
		}

		/*
		 * Threaded stuff
		 */

#if VIVE_PRO_EYE
		[RuntimeInitializeOnLoadMethod]
		static void Init()
		{
			UnityApp.StartCoroutine(InitEyeTracking());
		}

		static IEnumerator InitEyeTracking()
		{
			while (SRanipal_Eye_Framework.Status != SRanipal_Eye_Framework.FrameworkStatus.WORKING)
			{
				yield return null;
			}

			SRanipal_Eye_API.RegisterEyeDataCallback_v2(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye_v2.CallbackBasic)EyeCallback));
		}

		static void EyeCallback(ref EyeData_v2 data)
		{
			eyeData = data;
		}
#endif

		/*
		 * Untested
		 */

		public static Vector2 GetVivePro(StereoTargetEyeMask eye)
		{
#if VIVE_PRO_EYE
			// looking camera
			var camera = Prosthesis.Instance.Camera;

			// looking plane (placed infront of camera)
			var plane = new Plane(-camera.transform.forward, camera.transform.forward);

			// looking direction (start from camera with camera normal as vec3(0, 0, 1))
			var eyeGaze = eye == StereoTargetEyeMask.Right ?
							eyeData.verbose_data.right.gaze_direction_normalized :
							eyeData.verbose_data.left.gaze_direction_normalized;

			eyeGaze.x *= -1;

			var direction = new Ray(camera.transform.position, camera.transform.rotation * eyeGaze);

			// looking position
			var position = AuxMath.Intersection(plane, direction);

			// position in screen space
			var screenPoint = camera.WorldToScreenPoint(position)
									.DivideXY(camera.pixelWidth, camera.pixelHeight)
									.SubtractXY(0.5f)
									.SubtractXY(Offset);

			return screenPoint;
#else
			return Vector2.zero;
#endif
		}

		public static float GetPupilDilation(StereoTargetEyeMask eye)
		{
			return Random.value * 4 + 2;

#if VIVE_PRO_EYE
			return eye == StereoTargetEyeMask.Right ?
				eyeData.verbose_data.right.pupil_diameter_mm :
				eyeData.verbose_data.left.pupil_diameter_mm;
#else
			return 0;
#endif
		}

	}
}

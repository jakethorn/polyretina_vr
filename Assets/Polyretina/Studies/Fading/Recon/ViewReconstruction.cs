using UnityEngine;

namespace LNE.Studies.FadingV2
{
	using IO;
	using UI.Attributes;
	using PostProcessing;
	using ProstheticVision;

	public class ViewReconstruction : MonoBehaviour
	{
#pragma warning disable 649
		[SerializeField, Path(isFile = true)]
		private string rotPath;

		[SerializeField]
		private Transform target;

		[SerializeField]
		private Vector3 positionOffset;

		[Space]

		[SerializeField, Path(isFile = true)]
		private string eyePath;

		[SerializeField]
		private PostProcessEffect crosshair;

		[Space]

		[SerializeField]
		private int speed = 1;
#pragma warning restore 649

		private CSV rotCsv;
		private CSV eyeCsv;

		private int firstId = 0;
		private int frameId = 0;
		private int finalId = 0;
		private bool playing = false;

		private int frameCount => rotCsv.Height - 2;

		private int trialId => GetTrialId(firstId + frameId);

		void Start()
		{
			rotCsv = new CSV();
			rotCsv.LoadWStream(rotPath);

			eyeCsv = new CSV();
			eyeCsv.LoadWStream(eyePath);

			Play(0);
		}

		void FixedUpdate()
		{
			if (playing)
			{
				UpdateTransform();
				UpdateGaze();

				frameId += speed;

				var frame = Mathf.Clamp(firstId + frameId, 0, frameCount);

				if (GetTrialId(frame) != GetTrialId(frame + 1))
				{
					Pause();
				}
			}
		}

		void OnGUI()
		{
			var style = new GUIStyle(GUI.skin.label);
			style.fontSize = 100;

			GUILayout.Label($"Trial: {trialId}", style);

			var nowTime = GetTime(firstId + frameId) - GetTime(firstId);
			var endTime = GetTime(finalId) - GetTime(firstId);
			GUILayout.Label(nowTime.ToString("N2") + " / " + endTime.ToString("N2"), style);
		}

		public void Play(int trial)
		{
			UpdateTrial(trial);
			UpdateTransform();
		}

		public void PlayPrevious()
		{
			Play(trialId - 1);
		}

		public void PlayNext()
		{
			Play(trialId + 1);
		}

		public void Play()
		{
			playing = true;
		}

		public void Pause()
		{
			playing = false;
		}

		public void PlayPause()
		{
			playing = !playing;
		}

		public void Skip(int amount)
		{
			frameId += amount;
			UpdateTransform();
		}

		private int GetTrialId(int frame)
		{
			return int.Parse(rotCsv.GetCell("trial", frame + 1));
		}

		private float GetTime(int frame)
		{
			return float.Parse(rotCsv.GetCell("time", frame + 1));
		}

		private Vector3 GetPosition(int frame)
		{
			return new Vector3(
				float.Parse(rotCsv.GetCell("px", frame + 1)),
				float.Parse(rotCsv.GetCell("py", frame + 1)),
				float.Parse(rotCsv.GetCell("pz", frame + 1))
			);
		}

		private Quaternion GetRotation(int frame)
		{
			return new Quaternion(
				float.Parse(rotCsv.GetCell("rx", frame + 1)),
				float.Parse(rotCsv.GetCell("ry", frame + 1)),
				float.Parse(rotCsv.GetCell("rz", frame + 1)),
				float.Parse(rotCsv.GetCell("rw", frame + 1))
			);
		}

		private Vector2 GetEyeGaze(int frame)
		{
			return new Vector2(
				float.Parse(eyeCsv.GetCell("x", frame + 1)),
				float.Parse(eyeCsv.GetCell("y", frame + 1))
			);
		}

		private void UpdateTrial(int tid)
		{
			// firstId
			for (int i = 0; i < frameCount; i++)
			{
				if (GetTrialId(i) == tid)
				{
					firstId = i;
					break;
				}
			}

			// frameId
			frameId = 0;

			// finalId
			for (int i = 0; i < frameCount; i++)
			{
				if (GetTrialId(i) > tid)
				{
					finalId = i - 1;
					break;
				}

				if (i == frameCount - 1)
				{
					finalId = frameCount - 1;
				}
			}

			playing = false;
		}

		private void UpdateTransform()
		{
			target.position = GetPosition(firstId + frameId) - positionOffset;
			target.rotation = GetRotation(firstId + frameId);
		}

		private void UpdateGaze()
		{
			var eyeGaze = (GetEyeGaze(firstId + frameId) + new Vector2(0.5f, 0.5f)) * new Vector2(2036, 2260);
			crosshair.SetVector("_target_pixel", eyeGaze);

			// also assign vector to implant
			EyeGaze.Custom = GetEyeGaze(firstId + frameId);
		}
	}
}

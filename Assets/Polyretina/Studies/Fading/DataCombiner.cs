using UnityEngine;

namespace LNE.Studies.FadingV2
{
	using ArrayExts;
	using IO;
	using UI.Attributes;

	public class DataCombiner : MonoBehaviour
	{
		[SerializeField, Path]
		private string _path = "";

		void OnApplicationQuit()
		{
			var study = FindObjectOfType<FadingStudy2>();

			var stdCsv = study.csv;
			var rotCsv = FindObjectOfType<RotationRecorder>().csv;
			var eyeCsv = FindObjectOfType<EyeRecorder>().csv;

			CombineData(stdCsv, rotCsv, eyeCsv, _path, study.participant);
		}

		public static void CombineData(CSV stdCsv, CSV rotCsv, CSV eyeCsv, string path, string filename)
		{
			/*
			 * File 1
			 */

			var file1 = new CSV();
			file1.AppendRow("participant", "session", "trialId", "condition", "time", "posX", "posY", "posZ", "deltaPos", "rotX", "rotY", "rotZ", "rotW", "deltaRot", "eyeX", "eyeY", "deltaEye");

			var frameCount = rotCsv.Height - 2;
			for (int i = 1; i < frameCount; i++)
			{
				var participant = rotCsv.GetCell("participant", i);
				var session = rotCsv.GetCell<int>("session", i);
				var trialId = rotCsv.GetCell<int>("trial", i);
				var condition = GetCondition(stdCsv, trialId);

				var time = rotCsv.GetCell<float>("time", i);

				var posX = rotCsv.GetCell<float>("px", i);
				var posY = rotCsv.GetCell<float>("py", i);
				var posZ = rotCsv.GetCell<float>("pz", i);
				var posD = GetPositionDelta(rotCsv, i);

				var rotX = rotCsv.GetCell<float>("rx", i);
				var rotY = rotCsv.GetCell<float>("ry", i);
				var rotZ = rotCsv.GetCell<float>("rz", i);
				var rotW = rotCsv.GetCell<float>("rw", i);
				var rotD = GetRotationDelta(rotCsv, i);

				var eyeX = eyeCsv.GetCell<float>("x", i);
				var eyeY = eyeCsv.GetCell<float>("y", i);
				var eyeD = GetEyeGazeDelta(eyeCsv, i);

				file1.AppendRow(participant, session, trialId, condition, time, posX, posY, posZ, posD, rotX, rotY, rotZ, rotW, rotD, eyeX, eyeY, eyeD);
			}

			file1.SaveWStream(path + $"{filename}_file1.csv");

			/*
			 * File 2
			 */

			var file2 = new CSV();
			file2.AppendRow("participant", "session", "trialId", "condition", "startTime", "endTime", "timeTaken", "success", "totalRot");

			var trialCount = 50;
			for (int i = 0; i < trialCount; i++)
			{
				try
				{
					var cell = i + 1; // skip the header

					var participant = stdCsv.GetCell("participant", cell);
					var session = stdCsv.GetCell<int>("session", cell);
					var trialId = stdCsv.GetCell<int>("trial", cell);
					var condition = stdCsv.GetCell("strategy", cell);
					var startTime = stdCsv.GetCell<float>("start time", cell);
					var endTime = stdCsv.GetCell<float>("end time", cell);
					var timeTaken = stdCsv.GetCell<float>("time taken", cell);
					var success = stdCsv.GetCell("success", cell);
					var totalRot = GetTotalRotation(file1, trialId);

					file2.AppendRow(participant, session, trialId, condition, startTime, endTime, timeTaken, success, totalRot);
				}
				catch
				{
					break;
				}
			}

			file2.SaveWStream(path + $"{filename}_file2.csv");
		}

		private static string GetCondition(CSV stdCsv, int trialId)
		{
			var strategies = stdCsv.GetColumn("strategy", false);
			if (strategies.Length > trialId)
			{
				return strategies[trialId];
			}
			else
			{
				return "data not saved";
			}

			//return stdCsv.GetColumn("strategy", false)[trialId];
		}

		private static float GetPositionDelta(CSV rotCsv, int frameId)
		{
			if (IsFirstFrame(rotCsv, frameId))
				return 0;

			var prevPos = new Vector3(
				rotCsv.GetCell<float>("px", frameId - 1),
				rotCsv.GetCell<float>("py", frameId - 1),
				rotCsv.GetCell<float>("pz", frameId - 1)
			);

			var currPos = new Vector3(
				rotCsv.GetCell<float>("px", frameId),
				rotCsv.GetCell<float>("py", frameId),
				rotCsv.GetCell<float>("pz", frameId)
			);

			return Vector3.Distance(prevPos, currPos);
		}

		private static float GetRotationDelta(CSV rotCsv, int frameId)
		{
			if (IsFirstFrame(rotCsv, frameId))
				return 0;

			var prevRot = new Quaternion(
				rotCsv.GetCell<float>("rx", frameId - 1),
				rotCsv.GetCell<float>("ry", frameId - 1),
				rotCsv.GetCell<float>("rz", frameId - 1),
				rotCsv.GetCell<float>("rw", frameId - 1)
			);

			var currRot = new Quaternion(
				rotCsv.GetCell<float>("rx", frameId),
				rotCsv.GetCell<float>("ry", frameId),
				rotCsv.GetCell<float>("rz", frameId),
				rotCsv.GetCell<float>("rw", frameId)
			);

			return Quaternion.Angle(prevRot, currRot);
		}

		private static float GetEyeGazeDelta(CSV eyeCsv, int frameId)
		{
			if (IsFirstFrame(eyeCsv, frameId))
				return 0;

			var prevEye = new Vector2(
				eyeCsv.GetCell<float>("x", frameId - 1),
				eyeCsv.GetCell<float>("y", frameId - 1)
			);

			var currEye = new Vector2(
				eyeCsv.GetCell<float>("x", frameId),
				eyeCsv.GetCell<float>("y", frameId)
			);

			return Vector2.Distance(prevEye, currEye);
		}

		private static bool IsFirstFrame(CSV csv, int frameId)
		{
			// first frame of the whole data set
			if (frameId == 1)
				return true;

			var prevTrialId = csv.GetCell<int>("trial", frameId - 1);
			var currTrialId = csv.GetCell<int>("trial", frameId);
			return prevTrialId != currTrialId;
		}

		private static float GetTotalRotation(CSV file1, int trialId)
		{
			var trialIds = file1.GetColumn<int>("trialId", false);
			var rotDs = file1.GetColumn<float>("deltaRot", false);

			return rotDs.Where((i, _) => trialIds[i] == trialId)
						.Converge((a, b) => a + b);
		}
	}
}

/*

file 1:
participant    trialId    condition    time    rot    deltaRot    eyeGaze    deltaEyeGaze    

length = number of frames


file 2:
participant    trialId    condition    startTime    endTime    timeTaken    success    totalRot

length = number of trials

*/

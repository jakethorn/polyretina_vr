#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace LNE.Studies.FadingV2
{
	using ArrayExts;
	using IO;

	public class FadingDataHelper
	{
		/*
		 * Combine files
		 */

		[MenuItem("Polyretina/.../Combine Files")]
		static void CombineFiles()
		{
			var csv = new CSV();

			for (var path = SelectFile(); path != ""; path = SelectFile())
			{
				AppendCSV(csv, path);
			}

			var savePath = EditorUtility.SaveFilePanel("Save file...", "", "combined", "csv");
			if (savePath != "")
			{
				csv.SaveWStream(savePath);
			}
		}

		private static string SelectFile()
		{
			return EditorUtility.OpenFilePanel("Select file...", "", "csv");
		}

		private static void AppendCSV(CSV csv, string path)
		{
			csv.LoadWStream(path, csv.Height == 0);
		}

		/*
		 * Calculate Rotation
		 */

		[MenuItem("Polyretina/.../Calculate Rotation")]
		static void CalculateRotation()
		{
			var path = EditorUtility.OpenFilePanel("Select json file", "", "csv");
			var csv = new CSV();
			csv.LoadWStream(path);

			var xs = csv.GetColumn("rx", false);
			var ys = csv.GetColumn("ry", false);
			var zs = csv.GetColumn("rz", false);
			var ws = csv.GetColumn("rw", false);

			var ds = new List<object>();
			ds.Add("diff");

			for (int i = 0; i + 1 < xs.Length && float.TryParse(xs[i + 1], out _); i++)
			{
				var a = new Quaternion(
					float.Parse(xs[i]),
					float.Parse(ys[i]),
					float.Parse(zs[i]),
					float.Parse(ws[i])
				);

				var b = new Quaternion(
					float.Parse(xs[i + 1]),
					float.Parse(ys[i + 1]),
					float.Parse(zs[i + 1]),
					float.Parse(ws[i + 1])
				);

				var d = Quaternion.Angle(a, b);
				ds.Add(d);
			}

			Debug.Log(csv.Width);

			csv.AppendColumn(ds.ToArray());
			csv.SaveWStream(path.Replace(".csv", "-diff.csv"));
		}

		/*
		 * Check Words
		 */

		private const int NUM_LETTERS = 6;
		private const int NUM_TRIALS = 50;

		[MenuItem("Polyretina/.../Check Words")]
		static void CheckWords()
		{
			var path = EditorUtility.OpenFilePanel("Select word file", "", "txt");
			var text = File.ReadAllText(path);
			var allWords = text.Split('\n')
							.Apply((word) => word.Trim('\r'))
							.Where((word) => word.Length == NUM_LETTERS);

			var words = new List<string>(allWords);

			Debug.Log(words.Count);

			/*
			RemoveWords(words, allWords, "Adele1".AsUid());
			RemoveWords(words, allWords, "Adele2".AsUid());
			RemoveWords(words, allWords, "Adele3".AsUid());

			Debug.Log(words.Count);

			// fourth session
			var fourthWords = "";
			foreach (var word in words.ToArray().Randomise(1).Subarray(0, 150))
			{
				fourthWords += word + "\n";
			}

			File.WriteAllText(path.Replace(".txt", "_Adele4.txt"), fourthWords);

			// fifth session
			var fifthWords = "";
			foreach (var word in words.ToArray().Randomise(1).Subarray(150, 150))
			{
				fifthWords += word + "\n";
			}

			File.WriteAllText(path.Replace(".txt", "_Adele5.txt"), fifthWords);

			// sixth session
			var sixthWords = "";
			foreach (var word in words.ToArray().Randomise(1).Subarray(300, 56))
			{
				sixthWords += word + "\n";
			}

			// add words that were in Adele1, but not Adele2 and Adele3

			File.WriteAllText(path.Replace(".txt", "_Adele6.txt"), sixthWords);
			*/
		}

		private static void RemoveWords(List<string> words, string[] allWords, int seed)
		{
			for (int i = 0; i < NUM_TRIALS; i++)
			{
				var randomWords = allWords.Randomise(seed - i).Subarray(0, 3);

				foreach (var word in randomWords)
				{
					words.Remove(word);
				}
			}
		}

		/*
		 * Combine Data
		 */

		[MenuItem("Polyretina/.../Combine Data")]
		static void CombineData()
		{
			var stdCsv = Open("Select study csv file");
			var rotCsv = Open("Select rotation csv file");
			var eyeCsv = Open("Select eye gaze csv file");

			var path = Path.GetDirectoryName(stdCsv.SourcePath) + "/";
			DataCombiner.CombineData(stdCsv, rotCsv, eyeCsv, path, "combined");
		}

		/*
		 * Add distance from centre
		 */

		[MenuItem("Polyretina/.../Add Angles")]
		static void AddAngles()
		{
			var csv = Open("Select rotation csv file");
			csv.SetCell(csv.Width, 0, "angle");
			csv.SetCell(csv.Width, 0, "angleX");
			csv.SetCell(csv.Width, 0, "angleY");

			var startingRot = default(Quaternion);
			var n = csv.Height - 2;
			for (int i = 0; i < n; i++)
			{
				// +1 to skip the header
				var frame = i + 1;

				if (IsFirstFrame(csv, frame))
				{
					startingRot = GetRotation(csv, frame);
				}

				var angle = Quaternion.Angle(startingRot, GetRotation(csv, frame));
				csv.SetCell(csv.Width - 3, frame, angle);

				var quaternion = Quaternion.Inverse(startingRot) * GetRotation(csv, frame);
				csv.SetCell(csv.Width - 2, frame, CenterAroundZero(quaternion.eulerAngles.x));
				csv.SetCell(csv.Width - 1, frame, CenterAroundZero(quaternion.eulerAngles.y));
			}

			csv.SaveWStream(csv.SourcePath.Replace(".csv", "_wangles.csv"));
		}

		/*
		 * Fix condition data
		 */ 

		[MenuItem("Polyretina/.../Fix Condition Data")]
		static void FixConditionData()
		{
			var results = Open("Select results file...");
			var rotation = Open("Select rotation file...");

			var resSessions = results.GetColumn("session", true);
			var resTrialIds = results.GetColumn("trialId", true);

			for (int i = 1; i < rotation.Height; i++)
			{
				var session = rotation.GetCell("session", i);
				var trialId = rotation.GetCell("trialId", i);

				var indicesOfSession = resSessions.IndicesOf(session);
				var indicesOfTrialId = resTrialIds.IndicesOf(trialId);
				try
				{
					var index = FindMatchingIndex(indicesOfSession, indicesOfTrialId);
					var condition = results.GetCell("condition", index);
					rotation.SetCell("condition", i, condition);
				}
				catch
				{
					Debug.Log("uh oh");
				}

				//var indexOfTrialId = resTrialIds.IndexOf(trialId);
				//var condition = results.GetCell("condition", indexOfTrialId);
			}

			rotation.SaveWStream(rotation.SourcePath.Replace(".csv", "-fixed.csv"));
		}

		private static int FindMatchingIndex(int[] js, int[] ks)
		{
			foreach (var j in js)
			{
				foreach (var k in ks)
				{
					if (j == k)
						return j;
				}
			}

			throw new System.Exception();
		}

		private static CSV Open(string prompt)
		{
			var path = EditorUtility.OpenFilePanel(prompt, "", "csv");
			var csv = new CSV();
			csv.LoadWStream(path);

			return csv;
		}

		private static bool IsFirstFrame(CSV csv, int frame)
		{
			// first frame of the whole data set
			if (frame == 1)
				return true;

			var prevTrialId = csv.GetCell<int>("trialId", frame - 1);
			var currTrialId = csv.GetCell<int>("trialId", frame);
			return prevTrialId != currTrialId;
		}

		private static Quaternion GetRotation(CSV csv, int frame)
		{
			return new Quaternion(
				csv.GetCell<float>("rotX", frame),
				csv.GetCell<float>("rotY", frame),
				csv.GetCell<float>("rotZ", frame),
				csv.GetCell<float>("rotW", frame)
			);
		}

		private static float CenterAroundZero(float angle)
		{
			while (angle > 180)
			{
				angle -= 360;
			}

			while (angle < -180)
			{
				angle += 360;
			}

			return angle;
		}
	}
}
#endif

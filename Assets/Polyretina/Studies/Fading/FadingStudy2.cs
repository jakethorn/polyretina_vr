using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace LNE.Studies.FadingV2
{
	using IO;
	using ProstheticVision;
	using StringExts;
	using UI.Attributes;

	using static ArrayExts.ArrayExtensions;

	[Flags]
	public enum Strategy { None = 1, Saccade = 2, Random = 4, Interrupt = 8, EyeTracking = 16, NoFading = 32 }

	public class FadingStudy2 : MonoBehaviour
	{
		[Path]
		public string path;


		public string participant;
		public int session;

		public int startingTrial;
		public int trialsPerCondition;

		[Space]

		public AudioSource speakers;
		public AudioClip startSound;
		public AudioClip successSound;
		public AudioClip failedSound;

		[Space]

		public TextAsset _words;
		public int wordLength;
		public int rows;
		public int columns;

		[Space]
		public bool randomiseWords;

		private TrialData_Conditions _trial;
		private int _seed;
		private int _trialId;
		private bool _trialStarted;
		private Strategy[] _strategies;

		private CSV _csv;

		public int trialId => _trialStarted ? _trialId : -1;

		public CSV csv => _csv;

		private SaccadeSimulator simulator => Prosthesis.Instance.ExternalProcessor as SaccadeSimulator;
		private EpiretinalImplant implant => Prosthesis.Instance.Implant as EpiretinalImplant;

		private string[] randomWords
		{
			get
			{
				return _words
						.text
						.Split('\n')
						.Apply((word) => word.Trim('\r'))
						.Where((word) => word.Length == wordLength)
						.Randomise(_seed)
						.Subarray(rows * columns * _trialId, rows * columns);

				//if (randomiseWords)
				//{
				//	allWords = allWords.Randomise(_seed - _trialId);
				//	return allWords.Subarray(0, rows * columns);
				//}
				//else
				//{
				//	return allWords.Subarray(rows * columns * _trialId, rows * columns);
				//}
			}
		}

		void Awake()
		{
			_seed = (participant + session.ToString()).AsUid();
			_trialId = startingTrial;
			_trialStarted = false;

			var strategyCombos = new[]
			{
				Strategy.None,
				Strategy.Saccade,
				Strategy.Random,
				Strategy.Interrupt,
				Strategy.Saccade | Strategy.Random,
				Strategy.Saccade | Strategy.Interrupt,
				Strategy.Random | Strategy.Interrupt,
				Strategy.Saccade | Strategy.Random | Strategy.Interrupt,
				Strategy.None | Strategy.EyeTracking,
				Strategy.None | Strategy.NoFading
			};

			//_strategies = CreateArray(strategyCombos, trialsPerCondition, (s) => s).Randomise(_seed);

			// new semi-random design >>
			_strategies = new Strategy[trialsPerCondition * strategyCombos.Length];

			for (int i = 0; i < trialsPerCondition; i++)
			{
				var strategiesRandomised = strategyCombos.Randomise(_seed + i);

				for (int j = 0; j < strategiesRandomised.Length; j++)
				{
					_strategies[i * strategiesRandomised.Length + j] = strategiesRandomised[j];
				}
			}
			// <<

			_csv = new CSV();
			_csv.AppendRow("participant", "session", "trial", "strategy", "start time", "end time", "time taken", "success");

			HideWords();
		}

		void OnApplicationQuit()
		{
			_csv.SaveWStream(path + $"Fad_Cnd_Res_411_{participant}{session}.csv");
		}

		public void StartTrial()
		{
			// safety check
			if (_trialStarted)
				return;

			_trialStarted = true;

			FindObjectOfType<VR.HeadsetRecenterer>().Recenter();

			// create trial
			_trial = new TrialData_Conditions
			{
				participant = participant,
				session = session,
				trialId = _trialId,
				strategy = _strategies[_trialId],
				startTime = Time.time
			};

			// misc stuff
			SetStrategyParameters();
			ShowWords();

			if (_trial.strategy.HasFlag(Strategy.EyeTracking))
			{
				speakers.PlayOneShot(startSound);
			}

			Debug.Log($"Trial {_trialId} starting. Strategy: {_trial.strategy}");
		}

		public void EndTrial(int numCorrect)
		{
			// safety check
			if (_trialStarted == false)
				return;

			_trialStarted = false;

			// add trial
			_trial.endtime = Time.time;
			_trial.timeTaken = _trial.endtime - _trial.startTime;
			_trial.numCorrect = numCorrect;

			// misc stuff
			//speakers.PlayOneShot(startSound);
			HideWords();

			// save data
			_csv.AppendRow(_trial.participant, _trial.session, _trial.trialId, _trial.strategy, _trial.startTime, _trial.endtime, _trial.timeTaken, _trial.numCorrect);
			_csv.SaveWStream(path + $"Fad_Cnd_Res_{_trialId}_{participant}{session}.csv");

			// save rot and eye
			FindObjectOfType<RotationRecorder>().Save(_trialId);
			FindObjectOfType<EyeRecorder>().Save(_trialId);

			Debug.Log($"Trial {_trialId} ending. Participant got {numCorrect} correct.");

			// incr
			_trialId++;

			// if that was the last trial
			if (_trialId == _strategies.Length)
			{
				// stop the study
				UnityApp.Quit();
			}
			else
			{
				// otherwise, start next trial in 2 seconds
				Invoke(nameof(StartTrial), 2);
			}
		}

		private void SetStrategyParameters()
		{
			// update saccade/interrupt simulator
			simulator.saccadeType = _trial.strategy.HasFlag(Strategy.Saccade)
									? SaccadeSimulator.SaccadeType.Left
									: SaccadeSimulator.SaccadeType.None;

			simulator.offTime = _trial.strategy.HasFlag(Strategy.Interrupt)
								? 0.6f
								: 0.0f;

			// update implant
			switch (_trial.strategy)
			{
				case Strategy.None:
					implant.decayT1 = .1f;
					implant.decayT2 = .3f;
					break;
				case Strategy.Saccade:
					implant.decayT1 = .175f;
					implant.decayT2 = .525f;
					break;
				case Strategy.Random:
					implant.decayT1 = .2f;
					implant.decayT2 = .6f;
					break;
				case Strategy.Interrupt:
					implant.decayT1 = .1f;
					implant.decayT2 = .3f;
					break;
				case Strategy.Saccade | Strategy.Random:
					implant.decayT1 = .175f;
					implant.decayT2 = .525f;
					break;
				case Strategy.Saccade | Strategy.Interrupt:
					implant.decayT1 = .275f;
					implant.decayT2 = .825f;
					break;
				case Strategy.Random | Strategy.Interrupt:
					implant.decayT1 = .8f;
					implant.decayT2 = 2.4f;
					break;
				case Strategy.Saccade | Strategy.Random | Strategy.Interrupt:
					implant.decayT1 = .55f;
					implant.decayT2 = 1.65f;
					break;
			}

			// eye tracking
			implant.eyeGazeSource = _trial.strategy.HasFlag(Strategy.EyeTracking) ?
									EyeGaze.Source.EyeTracking :
									EyeGaze.Source.None;

			// no fading
			implant.useFading = !_trial.strategy.HasFlag(Strategy.NoFading);

			implant.ResetFadingParameters();

#if !UNITY_EDITOR
			simulator.Update();
			implant.Update();
#endif
		}

		private void ShowWords()
		{
			var words = randomWords;
			var text = "";

			for (int i = 0; i < rows; i++)
			{
				for (int j = 0; j < columns; j++)
				{
					text += words[(i * columns) + j] + " ";
				}

				text += "\n";
			}

			FindObjectOfType<Text>().text = text;

			Debug.Log("<b>" + text.Replace("\n", "") + "</b>");
		}

		private void HideWords()
		{
			FindObjectOfType<Text>().text = "";
		}
	}

	[Serializable]
	public class TestData_Conditions
	{
		public TrialData_Conditions[] trials;
	}

	[Serializable]
	public class TrialData_Conditions
	{
		public string participant;
		public int session;
		public int trialId;

		public Strategy strategy;

		public float startTime;
		public float endtime;
		public float timeTaken;

		public bool success;
		public int numCorrect;
	}
}

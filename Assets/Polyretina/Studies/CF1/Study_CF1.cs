#pragma warning disable 649

using System.IO;
using UnityEngine;

namespace LNE.Studies
{
	using ProstheticVision;
	using Threading;
	using UI.Attributes;
	using static ArrayExts.ArrayExtensions;

	public class Study_CF1 : Singleton<Study_CF1>
	{
		public enum State { FadingIn, FadedIn, FadingOut, FadedOut }

		/*
		 * Editor fields
		 */

		[Header("Participant")]

		[SerializeField]
		private string _name;

		[SerializeField]
		private int _id = 100;

		[SerializeField]
		private int _startingExposure = 0;

		[SerializeField, Path]
		private string _savePath;

		[Header("Factors")]

		[SerializeField]
		private ElectrodeLayout[] _layouts;

		[SerializeField, Range(0, 46.3f)]
		private float[] _fieldOfViews;

		[SerializeField, Range(0, 3)]
		private float[] _decayConstants;

		[Space]

		[SerializeField]
		private int _exposuresPerCondition = 1;

		[Header("Other")]

		[SerializeField, Range(.01f, 2)]
		private float _fadeTime = 1;

		[Header("Positions")]

		[SerializeField]
		private Transform[] _tablePositions;

		[SerializeField]
		private Transform[] _kitchenPositions;

		[SerializeField]
		private Transform[] _ovenPositions;

		[Header("Objects")]

		[SerializeField]
		private GameObject[] _tableObjects;

		[SerializeField]
		private GameObject[] _kitchenObjects;

		[SerializeField]
		private GameObject[] _ovenObjects;

		[Space]

		[SerializeField]
		private GameObject _cup;

		/*
		 * Private fields
		 */

		private Exposure_CF1[] exposures;
		private int exposureId;
		private State state;
		private float startTime;
		private float timeTaken;

		/*
		 * Private properties
		 */

		private int participantSeed
		{
			get
			{
				return _id;
			}
		}

		private int exposureSeed
		{
			get 
			{
				return _id * (exposureId + 1);
			}
		}

		private EpiretinalImplant implant => Prosthesis.Instance.Implant as EpiretinalImplant;

		/*
		 * Unity callbacks
		 */

		void Start()
		{
			exposures = CreateArray(
				_layouts,
				_fieldOfViews,
				_decayConstants,
				_exposuresPerCondition,
				(ed, fov, dc) => new Exposure_CF1(ed, fov, dc)
			);

			exposures.Randomise(participantSeed);
			exposureId = _startingExposure;
			state = State.FadedOut;

			implant.brightness = 0;
		}

		void OnGUI()
		{
			GUI.skin.label.fontSize = 48;

			GUILayout.Label("Exposure: " + exposureId.ToString());
			GUILayout.Label("Electrode Layout: " + implant.layout.ToString());
			GUILayout.Label("Field of View: " + implant.fieldOfView.ToString());
			GUILayout.Label("Decay Constant: " + implant.tailLength.ToString());
		}

		void OnApplicationQuit()
		{
			Save();
		}

		/*
		 * Public methods 
		 */

		public void Begin()
		{
			ShowExposure();
		}

		public void Accept()
		{
			if (state == State.FadedIn)
			{
				SaveAnswer();
			}
			else if (state == State.FadedOut)
			{
				ShowNextExposure();
			}
		}

		public void Cancel()
		{
			FadeIn();
		}

		/*
		 * Private methods
		 */

		private void ShowExposure()
		{
			implant.layout = exposures[exposureId].layout;
			implant.fieldOfView = exposures[exposureId].fieldOfView;
			implant.tailLength = exposures[exposureId].decayConstant;

			ShuffleObjects();
			PlaceCup();

			FadeIn();
		}

		private void SaveAnswer()
		{
			exposures[exposureId].timeTaken += Time.time - startTime;
			FadeOut();
		}

		private void ShowNextExposure()
		{
			exposureId++;
			if (exposureId < exposures.Length)
			{
				ShowExposure();
			}
			else
			{
				UnityApp.Quit();
			}
		}

		private void ShuffleObjects()
		{
			ShuffleObjects(_tablePositions, _tableObjects);
			ShuffleObjects(_kitchenPositions, _kitchenObjects);
			ShuffleObjects(_ovenPositions, _ovenObjects);
		}

		private void ShuffleObjects(Transform[] positions, GameObject[] objects)
		{
			objects.Randomise(exposureSeed);
			objects.ForEach((obj) => { obj.SetActive(false); });

			for (int i = 0; i < positions.Length; ++i)
			{
				objects[i].SetActive(true);
				objects[i].transform.position = positions[i].position;
			}
		}

		private void PlaceCup()
		{
			var area = new System.Random(exposureSeed).Next(0, 2);
			var obj = default(GameObject);

			switch (area)
			{
				case 0: obj = _tableObjects		.Where((to) => to.activeSelf).Random(exposureSeed); break;
				case 1: obj = _kitchenObjects	.Where((ko) => ko.activeSelf).Random(exposureSeed); break;
				case 2: obj = _ovenObjects		.Where((oo) => oo.activeSelf).Random(exposureSeed); break;
			}

			_cup.transform.position = obj.transform.position;
			obj.SetActive(false);
		}

		private void FadeIn()
		{
			if (state != State.FadedOut)
				return;

			state = State.FadingIn;

			HideArrow();

			//Callback.Lerp(0, 1, _fadeTime, 
			//(val) => {
			//	Device.instance.brightness = val;
			//}, 
			//() => {
			//	state = State.FadedIn;
			//	startTime = Time.time;
			//});

			CallbackManager.InvokeUntil(() =>
			{
				implant.brightness = Mathf.Min(implant.brightness + Time.deltaTime / _fadeTime, 1);
				return implant.brightness < 1;
			}, () =>
			{
				state = State.FadedIn;
				startTime = Time.time;
			});
		}

		private void FadeOut()
		{
			if (state != State.FadedIn)
				return;

			state = State.FadingOut;

			//Callback.Lerp(1, 0, _fadeTime,
			//(val) =>
			//{
			//	Device.instance.brightness = val;
			//},
			//() =>
			//{
			//	state = State.FadedOut;
			//});

			CallbackManager.InvokeUntil(() =>
			{
				implant.brightness = Mathf.Max(implant.brightness - Time.deltaTime / _fadeTime, 0);
				return implant.brightness > 0;
			}, () =>
			{
				state = State.FadedOut;

				ShowArrow();
			});
		}

		private void ShowArrow()
		{
			Prosthesis.Instance.enabled = false;
			Camera.main.cullingMask = 256;
		}

		private void HideArrow()
		{
			Prosthesis.Instance.enabled = true;
			Camera.main.cullingMask = 55;
		}

		private void Save()
		{
			var csv = "Participant; Electrode Layout; Field of View; Decay Constant; Time Taken; \n";
			foreach (var exposure in exposures)
			{
				csv += _id.ToString() + "; ";
				csv += exposure.layout.ToString() + "; ";
				csv += exposure.fieldOfView.ToString() + "; ";
				csv += exposure.decayConstant.ToString() + "; ";
				csv += exposure.timeTaken.ToString() + "; ";
				csv += "\n";
			}

			File.WriteAllText(_savePath + _id.ToString() + "_" + System.DateTime.Now.ToString("dd-MM-yyy_hh-mm-ss") + ".csv", csv);
		}
	}

	public class Exposure_CF1
	{
		public ElectrodeLayout layout;
		public float fieldOfView;
		public float decayConstant;

		public float timeTaken;

		public Exposure_CF1(ElectrodeLayout l, float fov, float dc)
		{
			layout = l;
			fieldOfView = fov;
			decayConstant = dc;

			timeTaken = 0;
		}
	}
}

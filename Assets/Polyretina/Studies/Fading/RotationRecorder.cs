using UnityEngine;
using LNE.IO;

namespace LNE.Studies.FadingV2
{
	public class RotationRecorder : MonoBehaviour
	{
		[SerializeField]
		private Transform target = default;

		private FadingStudy2 _study;
		private string _participant;
		private int _session;
		private string _path;

		public CSV csv { get; private set; }

		void Awake()
		{
			_study = FindObjectOfType<FadingStudy2>();
			_participant = _study.participant;
			_session = _study.session;
			_path = _study.path;

			csv = new CSV();
			csv.AppendRow("participant", "session", "trial", "time", "px", "py", "pz", "rx", "ry", "rz", "rw");
		}

		void FixedUpdate()
		{
			if (_study.trialId < 0)
				return;

			csv.AppendRow(
				_participant,
				_session,
				_study.trialId,
				Time.time,
				target.position.x,
				target.position.y,
				target.position.z,
				target.rotation.x,
				target.rotation.y,
				target.rotation.z,
				target.rotation.w
			);
		}

		void OnApplicationQuit()
		{
			Save(411);
		}

		public void Save(int trial)
		{
			csv.SaveWStream(_path + $"Fad_Cnd_Rot_{trial}_{_participant}{_session}.csv");
		}
	}
}

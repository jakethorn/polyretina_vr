using System;

namespace LNE.Studies
{
	[Serializable]
	public class StudyData
	{
		public int identifier;
		public string date;
		public string startTime;
		public Exposure[] exposures;
		
		[Serializable]
		public class Exposure
		{
			public int trialId;

			public string electrodeLayout;
			public float fieldOfView;
			public float visualAngle;
			public float tailLength;
			public string itemName;
			public bool result;
			public float elapsedTime;

			public float startTime;
			public float endTime;
			public float guessTime;
			public bool isControl;

			public bool isReduced;
		}
	}
}

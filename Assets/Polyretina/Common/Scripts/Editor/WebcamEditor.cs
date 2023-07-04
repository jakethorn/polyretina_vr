using UnityEditor;
using UnityEngine;

namespace LNE.UI
{
	using ArrayExts;

	[CustomEditor(typeof(Webcam))]
	public class WebcamEditor : ExtendedEditor<Webcam>
	{
		public override void OnInspectorGUI()
		{
			BeginInspectorGUI();

			var targetProperty = Field(true, "target");
			if (targetProperty.enumValueIndex == (int)Webcam.Target.Material)
			{
				Field(true, "material");
			}

			DrawWebcamList();

			EndInspectorGUI();
		}

		private void DrawWebcamList()
		{
			var devices = WebCamTexture.devices;
			var deviceNames = devices.Convert((d) => d.name);
			if (deviceNames.Length == 0)
			{
				deviceNames = new[] { Webcam.NO_WEBCAM_FOUND };
			}

			var index = deviceNames.IndexOfOrZero(FindProperty("deviceName").stringValue);

			index = EditorGUILayout.Popup("Device", index, deviceNames);

			FindProperty("deviceName").stringValue = deviceNames[index];
		}
	}
}

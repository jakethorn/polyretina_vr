using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;

namespace LNE.ProstheticVision
{
	using LNE.UI.Attributes;

	using IO;
	using ImageExts;

    [CreateAssetMenu(fileName = "Epiretinal Implant with Readback", menuName = "LNE/Implants/Epiretinal Implant with Readback")]
    public class EpiretinalImplantWithReadback : EpiretinalImplant
    {
		[SerializeField, Path]
		private string _path;

		private readonly Queue<byte[]> data = new Queue<byte[]>();
		private readonly Queue<float> frameTime = new Queue<float>();
		private readonly List<(float, float)> brightnessData = new List<(float, float)>();
		private Thread thread;

		public override void Start()
		{
			base.Start();
			thread = new Thread(Async_CalculateBrightness);
			thread.Start();
		}

		public override void OnRenderImage(Texture source, RenderTexture destination)
		{
			base.OnRenderImage(source, destination);

			if (Pulse)
			{
				frameTime.Enqueue(Time.time);
				AsyncGPUReadback.Request(destination, 0, Callback);
			}
		}

		void OnDestroy()
		{
			// abort thread
			thread.Abort();

			// save data
			var csv = new CSV();
			csv.AppendRow("time", "brightness");

			foreach (var datum in brightnessData)
			{
				csv.AppendRow(datum.Item1, datum.Item2);
			}

			csv.SaveWStream(_path + $"GPUReadback-{DateTime.Now:HH_mm_ss}.csv");
		}

		private void Callback(AsyncGPUReadbackRequest request)
		{
			if (request.hasError)
			{
				Debug.LogError("GPU readback error detected.");
				return;
			}

			data.Enqueue(request.GetData<byte>().ToArray());
		}

		private void Async_CalculateBrightness()
		{
			while (true)
			{
				if (data.Count > 0 && data.Peek() != null)
				{
					// calculate brightness
					brightnessData.Add((frameTime.Dequeue(), CalculateBrightness(data.Dequeue())));
				}
			}
		}

		private float CalculateBrightness(byte[] bytes)
		{
			float total = 0;
			for (int i = 0; i < bytes.Length; i += 4)
			{
				total += bytes[i];
			}

			return total / bytes.Length / 255;
		}
	}
}

using LNE.ProstheticVision.Fading;
using LNE.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExponentialTests : MonoBehaviour
{
	public enum ValueType { Linear, Exponential, Logarithmic };
	
	[Range(0, 1)]
	public float value;

	public ValueType valueType;
	public float exponent;

	public LineGraph graph;

	public EditorButton zeroToOne;

	void Start()
	{
		zeroToOne = new EditorButton(ZeroToOne);
	}

	void Update()
	{
		switch (valueType)
		{
			case ValueType.Linear:
				graph.Value = value;
				break;
			case ValueType.Exponential:
				graph.Value = Mathf.Pow(value, exponent);
				break;
			case ValueType.Logarithmic:
				graph.Value = Mathf.Log(value, exponent);
				break;
		}
	}

	void ZeroToOne()
	{
		StartCoroutine(ZeroToOne_Coroutine());
	}

	IEnumerator ZeroToOne_Coroutine()
	{
		value = 0;

		while (value < 1)
		{
			value += .001f;
			yield return new WaitForFixedUpdate();
		}
	}
}

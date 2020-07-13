using UnityEngine;

using LNE.ProstheticVision;

public class FadeValues : MonoBehaviour
{
    [SerializeField]
    private Material fade;

    private EpiretinalImplant Implant => Prosthesis.Instance.Implant as EpiretinalImplant;

	void Update()
    {
        fade.SetTexture("_SubTex", Implant.FadeRT.Back);
    }
}

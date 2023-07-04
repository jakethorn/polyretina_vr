using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LNE.ProstheticVision
{
    [CreateAssetMenu(fileName = "Random Activator", menuName = "LNE/External Processor/Random Activator")]
    public class RandomActivator : ExternalProcessor
    {
        /*
         * Public fields
         */

        [Space]
        public Shader shader = null;

        [Range(0, 100)]
        public int percentageActive = 20;

        [Range(0, 1)]
        public float brightness = 1;

        public bool interrupt = true;

        /*
         * Private fields
         */

        private Material material;

        /*
         * Private properties
         */

        private EpiretinalImplant implant => Prosthesis.Instance.Implant as EpiretinalImplant;

		/*
         * ImageRenderer overrides
         */

		public override void Start()
        {
            if (shader == null)
            {
                Debug.LogError($"{name} does not have a shader.");
                return;
            }

            material = new Material(shader);

            var electrodeTex = implant.epiretinalData.GetPhospheneTexture(
                implant.headset,
                implant.pattern,
                implant.layout
            );

            material.SetTexture("electrodes", electrodeTex);
        }

        public override void Update()
        {
            if (material == null)
            {
                Debug.LogError($"{name} does not have a material.");
                return;
            }

            material.SetFloat("threshold", 1 - (percentageActive / 100f));
            material.SetFloat("brightness", brightness);
            material.SetInt("interrupt", interrupt ? 1 : 0);
        }

        public override void GetDimensions(out int width, out int height)
        {
            throw new System.Exception("Random Activator does not have dimensions.");
        }

        public override void OnRenderImage(Texture source, RenderTexture destination)
        {
            if (material == null)
            {
                Debug.LogError($"{name} does not have a material.");
                Graphics.Blit(source, destination);
                return;
            }

            if (on)
            {
                Graphics.Blit(source, destination, material);
            }
            else
            {
                Graphics.Blit(source, destination);
            }
        }

    }
}

using UnityEngine;

namespace LNE
{
    public class ControllerTest : MonoBehaviour
    {
        void Update()
        {
            foreach (KeyCode keycode in System.Enum.GetValues(typeof(KeyCode)))
            {
                if (Input.GetKeyDown(keycode))
                {
                    Debug.Log(keycode);
                }
            }
        }
    }
}

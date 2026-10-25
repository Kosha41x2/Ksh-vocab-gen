using UnityEngine;

namespace VocabGen.UI
{
    /// <summary>
    /// Handles menu actions. Attach this component to a menu object and connect its methods to UI buttons.
    /// </summary>
    public sealed class MenuController : MonoBehaviour
    {
        /// <summary>Quits the built application, or exits Play mode when tested in the Unity Editor.</summary>
        public void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

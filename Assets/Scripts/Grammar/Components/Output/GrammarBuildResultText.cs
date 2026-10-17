using TMPro;
using UnityEngine;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Displays the latest grammar build result in a TextMeshPro text object.
    /// Attach this component to the same GameObject as a TMP text component.
    /// </summary>
    public sealed class GrammarBuildResultText : MonoBehaviour
    {
        [SerializeField] private GrammarController grammarController;

        private TMP_Text outputText;

        private void Awake()
        {
            outputText = GetComponent<TMP_Text>();
        }

        private void OnEnable()
        {
            if (outputText == null)
            {
                outputText = GetComponent<TMP_Text>();
            }

            if (grammarController == null)
            {
                grammarController = FindFirstObjectByType<GrammarController>();
            }

            if (grammarController == null)
            {
                Debug.LogError("Assign a GrammarController to display grammar build results.", this);
                return;
            }

            grammarController.OnBuildFinished.AddListener(DisplayResult);
        }

        private void OnDisable()
        {
            if (grammarController != null)
            {
                grammarController.OnBuildFinished.RemoveListener(DisplayResult);
            }
        }

        public void DisplayResult(string result)
        {
            if (outputText == null)
            {
                outputText = GetComponent<TMP_Text>();
            }

            if (outputText == null)
            {
                Debug.LogError("Attach this component to a TextMeshPro text object.", this);
                return;
            }

            outputText.text = result;
        }
    }
}

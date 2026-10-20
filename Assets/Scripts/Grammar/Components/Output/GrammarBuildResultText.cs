using TMPro;
using UnityEngine;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Displays a UnityEvent string in a selectable TMP_InputField.
    /// Connect the desired source events to DisplayResult in the Inspector.
    /// </summary>
    public sealed class GrammarBuildResultText : MonoBehaviour
    {
        [SerializeField] private TMP_InputField outputInputField;
        private TMP_Text outputText;

        private void Awake()
        {
            ResolveOutputComponents();
        }

        public void DisplayResult(string result)
        {
            ResolveOutputComponents();

            if (outputInputField != null)
            {
                outputInputField.text = result;
                return;
            }

            if (outputText == null)
            {
                Debug.LogError("Assign a TMP_InputField or attach this component to one.", this);
                return;
            }

            outputText.text = result;
        }

        private void ResolveOutputComponents()
        {
            if (outputInputField == null)
            {
                outputInputField = GetComponent<TMP_InputField>();
            }

            if (outputInputField != null)
            {
                outputText = outputInputField.textComponent;
            }
            else if (outputText == null)
            {
                outputText = GetComponent<TMP_Text>();
            }
        }
    }
}

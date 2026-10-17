using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Reads comma-separated variable names from a TMP_InputField or uGUI InputField and creates VariableSymbol instances.
    /// Attach this component to the same GameObject as its input field.
    /// </summary>
    public sealed class VariableSymbolInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField textMeshProInputField;
        [SerializeField] private InputField legacyInputField;

        public VariableSymbol[] Variables { get; private set; } = new VariableSymbol[0];
        public string LastError { get; private set; } = string.Empty;

        private void Awake()
        {
            ResolveInputFields();
        }

        /// <summary>
        /// Call this from a UI Button's OnClick event to read and create the variables.
        /// </summary>
        public void CreateVariables()
        {
            ResolveInputFields();
            if (textMeshProInputField == null && legacyInputField == null)
            {
                SetError("Attach this component to a TMP_InputField or uGUI InputField GameObject.");
                Variables = new VariableSymbol[0];
                return;
            }

            if (!SymbolNameParser.TryParse(ReadInputText(), out List<string> names, out string error))
            {
                SetError(error);
                Variables = new VariableSymbol[0];
                return;
            }

            var created = new List<VariableSymbol>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                created.Add(new VariableSymbol(names[i]));
            }

            Variables = created.ToArray();
            LastError = string.Empty;
        }

        private void ResolveInputFields()
        {
            if (textMeshProInputField == null)
            {
                textMeshProInputField = GetComponent<TMP_InputField>();
            }

            if (legacyInputField == null)
            {
                legacyInputField = GetComponent<InputField>();
            }
        }

        private string ReadInputText()
        {
            ResolveInputFields();
            if (textMeshProInputField != null)
            {
                return textMeshProInputField.text;
            }

            return legacyInputField != null ? legacyInputField.text : null;
        }

        private void SetError(string error)
        {
            LastError = error;
            Debug.LogError("Variable symbols: " + error, this);
        }
    }
}

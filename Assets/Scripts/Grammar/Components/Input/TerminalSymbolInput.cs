using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Reads comma-separated terminal names from a TMP_InputField or uGUI InputField and creates TerminalSymbol instances.
    /// Attach this component to the same GameObject as its input field.
    /// </summary>
    public sealed class TerminalSymbolInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField textMeshProInputField;
        [SerializeField] private InputField legacyInputField;

        public TerminalSymbol[] Terminals { get; private set; } = new TerminalSymbol[0];
        public string LastError { get; private set; } = string.Empty;
        public string InputText => ReadInputText();

        private void Awake()
        {
            ResolveInputFields();
        }

        /// <summary>
        /// Call this from a UI Button's OnClick event to read and create the terminals.
        /// </summary>
        public void CreateTerminals()
        {
            ResolveInputFields();
            if (textMeshProInputField == null && legacyInputField == null)
            {
                LastError = "Attach this component to a TMP_InputField or uGUI InputField GameObject.";
                Terminals = new TerminalSymbol[0];
                Debug.LogError("Terminal symbols: " + LastError, this);
                return;
            }

            if (!SymbolNameParser.TryParse(ReadInputText(), out List<string> names, out string error))
            {
                LastError = error;
                Terminals = new TerminalSymbol[0];
                Debug.LogError("Terminal symbols: " + error, this);
                return;
            }

            var created = new List<TerminalSymbol>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                created.Add(new TerminalSymbol(names[i]));
            }

            Terminals = created.ToArray();
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
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Reads rule text from a TMP_InputField or uGUI InputField and builds rules from symbols supplied by GrammarController.
    /// Attach this component to the same GameObject as its rules input field.
    /// </summary>
    public sealed class RuleInput : MonoBehaviour
    {
        [SerializeField] private TMP_InputField textMeshProInputField;
        [SerializeField] private InputField legacyInputField;

        public Rule[] Rules { get; private set; } = new Rule[0];
        public string LastError { get; private set; } = string.Empty;
        public string InputText => ReadInputText();

        private void Awake()
        {
            ResolveInputFields();
        }

        /// <summary>
        /// Validates the input against the supplied symbols and creates rules only when all lines are valid.
        /// </summary>
        public bool CreateRules(VariableSymbol[] variables, TerminalSymbol[] terminals)
        {
            ResolveInputFields();
            if (textMeshProInputField == null && legacyInputField == null)
            {
                return Fail("Attach this component to a TMP_InputField or uGUI InputField GameObject.");
            }

            return CreateRules(ReadInputText(), variables, terminals);
        }

        /// <summary>
        /// Parses rule text directly, without reading an input field, and creates rules only when all lines are valid.
        /// </summary>
        public bool CreateRules(string input, VariableSymbol[] variables, TerminalSymbol[] terminals)
        {
            if (variables == null || terminals == null)
            {
                return Fail("The grammar controller must supply variable and terminal symbols.");
            }

            for (int i = 0; i < variables.Length; i++)
            {
                if (variables[i] != null)
                {
                    variables[i].ClearRules();
                }
            }

            if (HasNameCollision(variables, terminals, out string collision))
            {
                return Fail("'" + collision + "' is declared as both a variable and a terminal.");
            }

            if (!TryParseRules(input, variables, terminals,
                    out List<RuleSpecification> specifications, out string error))
            {
                return Fail(error);
            }

            var created = new List<Rule>(specifications.Count);
            for (int i = 0; i < specifications.Count; i++)
            {
                RuleSpecification specification = specifications[i];
                Symbol[] rightSide = new Symbol[specification.RightNames.Length];
                for (int j = 0; j < rightSide.Length; j++)
                {
                    rightSide[j] = FindSymbol(specification.RightNames[j], variables, terminals);
                }

                created.Add(new Rule(FindVariable(specification.LeftName, variables), rightSide, specification.Weight));
            }

            Rules = created.ToArray();
            LastError = string.Empty;
            return true;
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

        private static bool TryParseRules(
            string input,
            VariableSymbol[] variables,
            TerminalSymbol[] terminals,
            out List<RuleSpecification> specifications,
            out string error)
        {
            specifications = new List<RuleSpecification>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Enter at least one rule.";
                return false;
            }

            var variableRules = new Dictionary<string, List<float>>(StringComparer.Ordinal);
            // Commas and newlines both separate productions; commas are already reserved in symbol names.
            string[] lines = input.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                int arrow = line.IndexOf("->", StringComparison.Ordinal);
                if (arrow < 0 || line.IndexOf("->", arrow + 2, StringComparison.Ordinal) >= 0)
                {
                    error = "Line " + (i + 1) + ": use exactly one '->' between the left and right sides.";
                    return false;
                }

                string leftName = line.Substring(0, arrow).Trim();
                if (leftName.Length == 0 || leftName.IndexOfAny(new[] { ' ', '\t', ',' }) >= 0)
                {
                    error = "Line " + (i + 1) + ": the left side must be one declared variable symbol.";
                    return false;
                }

                if (FindVariable(leftName, variables) == null)
                {
                    error = "Line " + (i + 1) + ": left-side variable '" + leftName + "' is not declared.";
                    return false;
                }

                string rightText = line.Substring(arrow + 2).Trim();
                string[] alternatives = rightText.Split('|');
                for (int alternativeIndex = 0; alternativeIndex < alternatives.Length; alternativeIndex++)
                {
                    string alternative = alternatives[alternativeIndex].Trim();
                    string location = "Line " + (i + 1) + ", alternative " + (alternativeIndex + 1) + ": ";
                    if (!TryParseAlternative(alternative, variables, terminals,
                            out string[] rightNames, out float weight, out error))
                    {
                        error = location + error;
                        return false;
                    }

                    if (!variableRules.ContainsKey(leftName))
                    {
                        variableRules.Add(leftName, new List<float>());
                    }
                    variableRules[leftName].Add(weight);

                    specifications.Add(new RuleSpecification
                    {
                        LeftName = leftName,
                        RightNames = rightNames,
                        Weight = weight
                    });
                }
            }

            if (specifications.Count == 0)
            {
                error = "Enter at least one rule.";
                return false;
            }

            foreach (KeyValuePair<string, List<float>> entry in variableRules)
            {
                bool hasPositiveWeight = false;
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    if (entry.Value[i] > 0f)
                    {
                        hasPositiveWeight = true;
                        break;
                    }
                }

                if (!hasPositiveWeight)
                {
                    error = "Variable '" + entry.Key + "' has rules, but all of their weights are zero.";
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseAlternative(
            string rightText,
            VariableSymbol[] variables,
            TerminalSymbol[] terminals,
            out string[] rightNames,
            out float weight,
            out string error)
        {
            rightNames = null;
            weight = 1f;
            error = string.Empty;

            int openWeight = rightText.LastIndexOf('(');
            int closeWeight = rightText.LastIndexOf(')');
            if (openWeight >= 0 || closeWeight >= 0)
            {
                if (openWeight < 0 || closeWeight != rightText.Length - 1 || closeWeight < openWeight)
                {
                    error = "put an optional weight in parentheses at the end, for example '(2)'.";
                    return false;
                }

                string weightText = rightText.Substring(openWeight + 1, closeWeight - openWeight - 1).Trim();
                if (!float.TryParse(weightText, NumberStyles.Float, CultureInfo.InvariantCulture, out weight) ||
                    float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
                {
                    error = "weight must be a finite, non-negative number using '.' for decimals.";
                    return false;
                }

                rightText = rightText.Substring(0, openWeight).Trim();
            }

            if (rightText == "ε" || string.Equals(rightText, "epsilon", StringComparison.OrdinalIgnoreCase) ||
                rightText.Length == 0)
            {
                rightNames = new[] { "ε" };
                if (FindSymbol("ε", variables, terminals) == null)
                {
                    error = "the grammar is missing its built-in empty terminal symbol.";
                    return false;
                }

                return true;
            }

            var parsedNames = new List<string>();
            string[] groups = rightText.Split('.');
            for (int groupIndex = 0; groupIndex < groups.Length; groupIndex++)
            {
                string group = groups[groupIndex].Trim();
                if (group.Length == 0)
                {
                    error = "a period must have a symbol on both sides.";
                    return false;
                }

                string[] groupNames = group.Split(new[] { ' ', '\t', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                parsedNames.AddRange(groupNames);
            }

            rightNames = parsedNames.ToArray();
            for (int i = 0; i < rightNames.Length; i++)
            {
                if (rightNames[i] == "ε" || string.Equals(rightNames[i], "epsilon", StringComparison.OrdinalIgnoreCase))
                {
                    error = "use 'ε' by itself for an empty production.";
                    return false;
                }

                if (FindSymbol(rightNames[i], variables, terminals) == null)
                {
                    error = "right-side symbol '" + rightNames[i] + "' is not declared.";
                    return false;
                }
            }

            return true;
        }

        private static bool HasNameCollision(VariableSymbol[] variables, TerminalSymbol[] terminals, out string collision)
        {
            for (int i = 0; i < variables.Length; i++)
            {
                if (variables[i] == null)
                {
                    collision = string.Empty;
                    return true;
                }

                for (int j = 0; j < terminals.Length; j++)
                {
                    if (terminals[j] == null || variables[i].GetRepresentation() == terminals[j].GetRepresentation())
                    {
                        collision = variables[i].GetRepresentation();
                        return true;
                    }
                }
            }

            collision = string.Empty;
            return false;
        }

        private static VariableSymbol FindVariable(string name, VariableSymbol[] variables)
        {
            for (int i = 0; i < variables.Length; i++)
            {
                if (variables[i] != null && variables[i].GetRepresentation() == name)
                {
                    return variables[i];
                }
            }

            return null;
        }

        private static Symbol FindSymbol(string name, VariableSymbol[] variables, TerminalSymbol[] terminals)
        {
            if (name == "ε" || string.Equals(name, "epsilon", StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; i < terminals.Length; i++)
                {
                    if (terminals[i] != null && terminals[i].IsEmpty)
                    {
                        return terminals[i];
                    }
                }

                return null;
            }

            VariableSymbol variable = FindVariable(name, variables);
            if (variable != null)
            {
                return variable;
            }

            for (int i = 0; i < terminals.Length; i++)
            {
                if (terminals[i] != null && terminals[i].GetRepresentation() == name)
                {
                    return terminals[i];
                }
            }

            return null;
        }

        private bool Fail(string error)
        {
            LastError = error;
            Rules = new Rule[0];
            Debug.LogError("Grammar rules: " + error, this);
            return false;
        }

        private sealed class RuleSpecification
        {
            public string LeftName;
            public string[] RightNames;
            public float Weight;
        }
    }
}

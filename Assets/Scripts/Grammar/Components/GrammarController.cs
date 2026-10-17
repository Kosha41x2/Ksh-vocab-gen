using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace VocabGen.Grammar
{
    [System.Serializable]
    public sealed class GrammarBuildResultEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// Coordinates the symbol input components and asks RuleInput to validate and build the grammar rules.
    /// </summary>
    public sealed class GrammarController : MonoBehaviour
    {
        [SerializeField] private VariableSymbolInput variableInput;
        [SerializeField] private TerminalSymbolInput terminalInput;
        [SerializeField] private RuleInput ruleInput;
        [SerializeField] private GrammarBuildResultEvent onBuildFinished = new GrammarBuildResultEvent();

        public VariableSymbol[] Variables { get; private set; } = new VariableSymbol[0];
        public TerminalSymbol[] Terminals { get; private set; } = new TerminalSymbol[0];
        public Rule[] Rules { get; private set; } = new Rule[0];
        public string LastError { get; private set; } = string.Empty;
        public GrammarBuildResultEvent OnBuildFinished => onBuildFinished;

        /// <summary>
        /// Call from a UI Button. Reads the symbol fields, passes those symbols to RuleInput,
        /// and only publishes the grammar when all rules are valid.
        /// </summary>
        public void BuildGrammar()
        {
            if (variableInput == null || terminalInput == null || ruleInput == null)
            {
                Fail("Assign the Variable Symbol Input, Terminal Symbol Input, and Rule Input components in the Inspector.");
                return;
            }

            variableInput.CreateVariables();
            if (!string.IsNullOrEmpty(variableInput.LastError))
            {
                Fail("Fix the variable symbol input: " + variableInput.LastError);
                return;
            }

            terminalInput.CreateTerminals();
            if (!string.IsNullOrEmpty(terminalInput.LastError))
            {
                Fail("Fix the terminal symbol input: " + terminalInput.LastError);
                return;
            }

            VariableSymbol[] variables = variableInput.Variables;
            TerminalSymbol[] terminals = terminalInput.Terminals;
            if (!ruleInput.CreateRules(variables, terminals))
            {
                Fail(ruleInput.LastError);
                return;
            }

            Variables = variables;
            Terminals = terminals;
            Rules = ruleInput.Rules;

            // Associate every built rule with its matching left-side variable.
            for (int i = 0; i < Variables.Length; i++)
            {
                Variables[i].AddRules(Rules);
            }

            LastError = string.Empty;
            string result = FormatGrammar();
            Debug.Log(result, this);
            onBuildFinished.Invoke(result);
        }

        private string FormatGrammar()
        {
            var output = new StringBuilder();
            output.AppendLine("Grammar built successfully.");
            output.Append("Variables: ");
            AppendSymbols(output, Variables);
            output.AppendLine();
            output.Append("Terminals: ");
            AppendSymbols(output, Terminals);
            output.AppendLine();
            output.AppendLine("Rules:");

            for (int i = 0; i < Rules.Length; i++)
            {
                Rule rule = Rules[i];
                output.Append("  ");
                output.Append(rule.LeftSide.GetRepresentation());
                output.Append(" -> ");

                Symbol[] rightSide = rule.RightSide;
                if (rightSide.Length == 0)
                {
                    output.Append('ε');
                }
                else
                {
                    for (int symbolIndex = 0; symbolIndex < rightSide.Length; symbolIndex++)
                    {
                        if (symbolIndex > 0)
                        {
                            output.Append(' ');
                        }
                        output.Append(rightSide[symbolIndex].GetRepresentation());
                    }
                }

                output.Append(" (");
                output.Append(rule.Weight.ToString(CultureInfo.InvariantCulture));
                output.AppendLine(")");
            }

            return output.ToString();
        }

        private static void AppendSymbols(StringBuilder output, Symbol[] symbols)
        {
            for (int i = 0; i < symbols.Length; i++)
            {
                if (i > 0)
                {
                    output.Append(", ");
                }
                output.Append(symbols[i].GetRepresentation());
            }
        }

        private void Fail(string error)
        {
            LastError = error;
            Variables = new VariableSymbol[0];
            Terminals = new TerminalSymbol[0];
            Rules = new Rule[0];
            string result = "Grammar build failed: " + error;
            Debug.LogError(result, this);
            onBuildFinished.Invoke(result);
        }
    }
}

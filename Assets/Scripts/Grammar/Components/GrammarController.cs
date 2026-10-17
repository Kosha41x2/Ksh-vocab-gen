using System.Collections.Generic;
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

        public Grammar Grammar { get; private set; }
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
            var terminalList = new List<TerminalSymbol>(terminalInput.Terminals);
            if (!terminalList.Contains(TerminalSymbol.Empty))
            {
                terminalList.Add(TerminalSymbol.Empty);
            }
            TerminalSymbol[] terminals = terminalList.ToArray();
            if (!ruleInput.CreateRules(variables, terminals))
            {
                Fail(ruleInput.LastError);
                return;
            }

            Grammar = new Grammar(terminals, variables, ruleInput.Rules);

            // Associate every built rule with its matching left-side variable.
            for (int i = 0; i < Grammar.Variables.Count; i++)
            {
                Grammar.Variables[i].AddRules(Grammar.Rules);
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
            AppendSymbols(output, Grammar.Variables);
            output.AppendLine();
            output.Append("Terminals: ");
            AppendSymbols(output, Grammar.Terminals);
            output.AppendLine();
            output.AppendLine("Rules:");

            for (int i = 0; i < Grammar.Rules.Count; i++)
            {
                Rule rule = Grammar.Rules[i];
                output.Append("  ");
                output.Append(rule.LeftSide.GetRepresentation());
                output.Append(" -> ");

                Symbol[] rightSide = rule.RightSide;
                if (rightSide.Length == 0 || (rightSide.Length == 1 && IsEmptyTerminal(rightSide[0])))
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

        private static void AppendSymbols(StringBuilder output, IEnumerable<Symbol> symbols)
        {
            bool first = true;
            foreach (Symbol symbol in symbols)
            {
                if (!first)
                {
                    output.Append(", ");
                }
                output.Append(IsEmptyTerminal(symbol) ? "ε" : symbol.GetRepresentation());
                first = false;
            }
        }

        private static bool IsEmptyTerminal(Symbol symbol)
        {
            return symbol is TerminalSymbol terminal && terminal.IsEmpty;
        }

        private void Fail(string error)
        {
            LastError = error;
            Grammar = null;
            string result = "Grammar build failed: " + error;
            Debug.LogError(result, this);
            onBuildFinished.Invoke(result);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("onBuildFinished")]
        [SerializeField] private GrammarBuildResultEvent onGrammarModificationFinished = new GrammarBuildResultEvent();

        public Grammar Grammar { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public GrammarBuildResultEvent OnGrammarModificationFinished => onGrammarModificationFinished;

        /// <summary>
        /// Reads all three input fields and adds their contents to the current grammar.
        /// If no grammar exists yet, starts with an empty grammar first.
        /// </summary>
        public void AddFromInputFields()
        {
            if (variableInput == null || terminalInput == null || ruleInput == null)
            {
                PublishModificationError("Assign the Variable Symbol Input, Terminal Symbol Input, and Rule Input components in the Inspector.");
                return;
            }

            if (Grammar == null)
            {
                Grammar = new Grammar(
                    Array.Empty<TerminalSymbol>(),
                    Array.Empty<VariableSymbol>(),
                    Array.Empty<Rule>());
            }

            if (!string.IsNullOrWhiteSpace(variableInput.InputText))
            {
                variableInput.CreateVariables();
                if (!string.IsNullOrEmpty(variableInput.LastError))
                {
                    PublishModificationError("Fix the variable symbol input: " + variableInput.LastError);
                    return;
                }
                if (!AddVariables(variableInput.InputText))
                {
                    PublishModificationError("Could not add the variable symbols. Check for duplicates or reserved names.");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(terminalInput.InputText))
            {
                terminalInput.CreateTerminals();
                if (!string.IsNullOrEmpty(terminalInput.LastError))
                {
                    PublishModificationError("Fix the terminal symbol input: " + terminalInput.LastError);
                    return;
                }
                if (!AddTerminals(terminalInput.InputText))
                {
                    PublishModificationError("Could not add the terminal symbols. Check for duplicates or reserved names.");
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(ruleInput.InputText) && !AddRules(ruleInput.InputText))
            {
                PublishModificationError("Could not add the rules: " + LastError);
                return;
            }

            LastError = string.Empty;
            PublishModificationResult("Grammar modification finished.");
        }

        /// <summary>Adds a new variable to the current grammar. Returns false if it is missing or already used.</summary>
        private bool AddVariable(string representation)
        {
            if (!SymbolNameParser.TryParse(representation, out List<string> names, out _) || names.Count != 1)
            {
                return false;
            }

            return AddVariables(representation);
        }

        /// <summary>Parses comma-separated variable names and adds them when all are valid.</summary>
        private bool AddVariables(string text)
        {
            if (Grammar == null || !SymbolNameParser.TryParse(text, out List<string> names, out _))
            {
                return false;
            }

            var additions = new List<VariableSymbol>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (IsReservedEmptyName(name) || FindVariable(name) != null || FindTerminal(name) != null)
                {
                    return false;
                }

                additions.Add(new VariableSymbol(name));
            }

            Grammar.Variables.AddRange(additions);
            return true;
        }

        /// <summary>Adds a new terminal to the current grammar. The built-in empty terminal is reserved.</summary>
        private bool AddTerminal(string representation)
        {
            if (!SymbolNameParser.TryParse(representation, out List<string> names, out _) || names.Count != 1)
            {
                return false;
            }

            return AddTerminals(representation);
        }

        /// <summary>Parses comma-separated terminal names and adds them when all are valid.</summary>
        private bool AddTerminals(string text)
        {
            if (Grammar == null || !SymbolNameParser.TryParse(text, out List<string> names, out _))
            {
                return false;
            }

            var additions = new List<TerminalSymbol>(names.Count);
            for (int i = 0; i < names.Count; i++)
            {
                string name = names[i];
                if (IsReservedEmptyName(name) || FindTerminal(name) != null || FindVariable(name) != null)
                {
                    return false;
                }

                additions.Add(new TerminalSymbol(name));
            }

            Grammar.Terminals.AddRange(additions);
            return true;
        }

        /// <summary>Parses one or more complete rule lines and adds the resulting Rule objects.</summary>
        private bool AddRules(string text)
        {
            if (Grammar == null || ruleInput == null)
            {
                return false;
            }

            VariableSymbol[] variables = Grammar.Variables.ToArray();
            TerminalSymbol[] terminals = Grammar.Terminals.ToArray();
            bool parsed = ruleInput.CreateRules(text, variables, terminals);

            // RuleInput clears and rebuilds each variable's cached rule list while parsing.
            RefreshVariableRules();
            if (!parsed)
            {
                LastError = ruleInput.LastError;
                return false;
            }

            Grammar.Rules.AddRange(ruleInput.Rules);
            RefreshVariableRules();
            LastError = string.Empty;
            return true;
        }

        /// <summary>Parses and adds rule text, including multiple alternatives separated by '|'.</summary>
        private bool AddRule(string ruleText)
        {
            return AddRules(ruleText);
        }

        /// <summary>
        /// Adds a rule using the same right-side notation as the input field: symbols separated by periods,
        /// or ε/an empty string for an empty production.
        /// </summary>
        private bool AddRule(string leftSide, string rightSide, float weight = 1f)
        {
            if (Grammar == null || string.IsNullOrWhiteSpace(leftSide))
            {
                return false;
            }

            VariableSymbol left = FindVariable(leftSide.Trim());
            if (left == null || !TryParseRightSide(rightSide, out Symbol[] symbols))
            {
                return false;
            }

            try
            {
                var rule = new Rule(left, symbols, weight);
                Grammar.Rules.Add(rule);
                RefreshVariableRules();
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>Adds a rule object when its symbols belong to the current grammar.</summary>
        private bool AddRule(Rule rule)
        {
            if (Grammar == null || rule == null || !ContainsVariable(rule.LeftSide) || Grammar.Rules.Contains(rule))
            {
                return false;
            }

            Symbol[] rightSide = rule.RightSide;
            for (int i = 0; i < rightSide.Length; i++)
            {
                if (!ContainsSymbol(rightSide[i]))
                {
                    RefreshVariableRules();
                    return false;
                }
            }

            Grammar.Rules.Add(rule);
            RefreshVariableRules();
            return true;
        }

        private bool RemoveVariableAt(int position)
        {
            return RemoveVariableAt(position, true);
        }

        private bool RemoveVariableAt(int position, bool publishResult)
        {
            if (Grammar == null || position < 0 || position >= Grammar.Variables.Count)
            {
                LastError = "Variable index is outside the current grammar.";
                if (publishResult) PublishModificationError(LastError);
                return false;
            }

            VariableSymbol variable = Grammar.Variables[position];
            if (IsUsedByAnyRule(variable))
            {
                LastError = "Variable [" + position + "] '" + variable.GetRepresentation() + "' is used by one or more rules.";
                if (publishResult) PublishModificationError("Deletion not completed because a used symbol is trying to be deleted: " + LastError);
                return false;
            }

            Grammar.Variables.RemoveAt(position);
            RefreshVariableRules();
            LastError = string.Empty;
            if (publishResult) PublishModificationResult("Grammar modification finished.");
            return true;
        }

        private bool RemoveVariable(string representation)
        {
            if (Grammar == null || string.IsNullOrWhiteSpace(representation))
            {
                return false;
            }

            VariableSymbol variable = FindVariable(representation.Trim());
            if (variable == null)
            {
                return false;
            }
            if (IsUsedByAnyRule(variable))
            {
                PublishModificationError("Deletion not completed because a used symbol is trying to be deleted: variable '" +
                    variable.GetRepresentation() + "' is used by one or more rules.");
                return false;
            }

            Grammar.Variables.Remove(variable);
            RefreshVariableRules();
            LastError = string.Empty;
            PublishModificationResult("Grammar modification finished.");
            return true;
        }

        private bool RemoveTerminalAt(int position)
        {
            return RemoveTerminalAt(position, true);
        }

        private bool RemoveTerminalAt(int position, bool publishResult)
        {
            if (Grammar == null || position < 0 || position >= Grammar.Terminals.Count)
            {
                LastError = "Terminal index is outside the current grammar.";
                if (publishResult) PublishModificationError(LastError);
                return false;
            }

            TerminalSymbol terminal = Grammar.Terminals[position];
            if (terminal.IsEmpty)
            {
                LastError = "The built-in ε terminal cannot be removed.";
                if (publishResult) PublishModificationError(LastError);
                return false;
            }
            if (IsUsedByAnyRule(terminal))
            {
                LastError = "Terminal [" + position + "] '" + terminal.GetRepresentation() + "' is used by one or more rules.";
                if (publishResult) PublishModificationError("Deletion not completed because a used symbol is trying to be deleted: " + LastError);
                return false;
            }

            Grammar.Terminals.RemoveAt(position);
            RefreshVariableRules();
            LastError = string.Empty;
            if (publishResult) PublishModificationResult("Grammar modification finished.");
            return true;
        }

        private bool RemoveTerminal(string representation)
        {
            if (Grammar == null || representation == null)
            {
                return false;
            }

            TerminalSymbol terminal = FindTerminal(representation.Trim());
            if (terminal == null || terminal.IsEmpty)
            {
                return false;
            }
            if (IsUsedByAnyRule(terminal))
            {
                PublishModificationError("Deletion not completed because a used symbol is trying to be deleted: terminal '" +
                    terminal.GetRepresentation() + "' is used by one or more rules.");
                return false;
            }

            Grammar.Terminals.Remove(terminal);
            RefreshVariableRules();
            LastError = string.Empty;
            PublishModificationResult("Grammar modification finished.");
            return true;
        }

        private bool RemoveRuleAt(int position)
        {
            return RemoveRuleAt(position, true);
        }

        private bool RemoveRuleAt(int position, bool publishResult)
        {
            if (Grammar == null || position < 0 || position >= Grammar.Rules.Count)
            {
                LastError = "Rule index is outside the current grammar.";
                if (publishResult) PublishModificationError(LastError);
                return false;
            }

            Grammar.Rules.RemoveAt(position);
            RefreshVariableRules();
            LastError = string.Empty;
            if (publishResult) PublishModificationResult("Grammar modification finished.");
            return true;
        }

        /// <summary>
        /// Reads comma-separated zero-based indexes from the variable, terminal, and rule fields,
        /// then removes the selected entries and reports the final grammar once.
        /// </summary>
        public void RemoveFromInputFields()
        {
            if (Grammar == null)
            {
                PublishModificationError("Build a grammar before removing entries.");
                return;
            }
            if (variableInput == null || terminalInput == null || ruleInput == null)
            {
                PublishModificationError("Assign the Variable Symbol Input, Terminal Symbol Input, and Rule Input components in the Inspector.");
                return;
            }

            if (!TryParseIndexes(variableInput.InputText, "variable", out List<int> variableIndexes, out string error) ||
                !TryParseIndexes(terminalInput.InputText, "terminal", out List<int> terminalIndexes, out error) ||
                !TryParseIndexes(ruleInput.InputText, "rule", out List<int> ruleIndexes, out error))
            {
                PublishModificationError(error);
                return;
            }

            if (!IndexesAreInRange(variableIndexes, Grammar.Variables.Count, "variable", out error) ||
                !IndexesAreInRange(terminalIndexes, Grammar.Terminals.Count, "terminal", out error) ||
                !IndexesAreInRange(ruleIndexes, Grammar.Rules.Count, "rule", out error))
            {
                PublishModificationError(error);
                return;
            }

            var messages = new List<string>();
            ruleIndexes.Sort((left, right) => right.CompareTo(left));
            variableIndexes.Sort((left, right) => right.CompareTo(left));
            terminalIndexes.Sort((left, right) => right.CompareTo(left));

            for (int i = 0; i < ruleIndexes.Count; i++)
            {
                RemoveRuleAt(ruleIndexes[i], false);
            }

            for (int i = 0; i < variableIndexes.Count; i++)
            {
                int index = variableIndexes[i];
                if (!RemoveVariableAt(index, false))
                {
                    messages.Add("Deletion not completed because a used symbol is trying to be deleted: " + LastError);
                }
            }

            for (int i = 0; i < terminalIndexes.Count; i++)
            {
                int index = terminalIndexes[i];
                if (!RemoveTerminalAt(index, false))
                {
                    string message = LastError;
                    if (message.StartsWith("Terminal [", StringComparison.Ordinal))
                    {
                        message = "Deletion not completed because a used symbol is trying to be deleted: " + message;
                    }
                    messages.Add(message);
                }
            }

            RefreshVariableRules();
            LastError = string.Join("\n", messages);
            PublishModificationResult("Grammar modification finished.", messages);
        }

        private static bool TryParseIndexes(string text, string kind, out List<int> indexes, out string error)
        {
            indexes = new List<int>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                return true;
            }

            var uniqueIndexes = new HashSet<int>();
            string[] entries = text.Split(',');
            for (int i = 0; i < entries.Length; i++)
            {
                string entry = entries[i].Trim();
                if (!int.TryParse(entry, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) || index < 0)
                {
                    error = "Enter " + kind + " indexes as comma-separated zero-based numbers (for example: 0,1,2).";
                    return false;
                }

                if (uniqueIndexes.Add(index))
                {
                    indexes.Add(index);
                }
            }

            return true;
        }

        private static bool IndexesAreInRange(List<int> indexes, int count, string kind, out string error)
        {
            for (int i = 0; i < indexes.Count; i++)
            {
                if (indexes[i] >= count)
                {
                    error = "The " + kind + " index " + indexes[i] + " is outside the current grammar.";
                    return false;
                }
            }

            error = string.Empty;
            return true;
        }

        private bool IsUsedByAnyRule(Symbol symbol)
        {
            for (int i = 0; i < Grammar.Rules.Count; i++)
            {
                Rule rule = Grammar.Rules[i];
                if (ReferenceEquals(rule.LeftSide, symbol))
                {
                    return true;
                }

                Symbol[] rightSide = rule.RightSide;
                for (int j = 0; j < rightSide.Length; j++)
                {
                    if (ReferenceEquals(rightSide[j], symbol))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Removes the specified rule instance.</summary>
        private bool RemoveRule(Rule rule)
        {
            if (Grammar == null || rule == null || !Grammar.Rules.Remove(rule))
            {
                return false;
            }

            RefreshVariableRules();
            return true;
        }

        /// <summary>Removes the first rule matching its left side, right-side notation, and optional weight.</summary>
        private bool RemoveRule(string leftSide, string rightSide, float? weight = null)
        {
            if (Grammar == null || string.IsNullOrWhiteSpace(leftSide) ||
                !TryParseRightSide(rightSide, out Symbol[] expectedRightSide))
            {
                return false;
            }

            string leftName = leftSide.Trim();
            for (int i = 0; i < Grammar.Rules.Count; i++)
            {
                Rule rule = Grammar.Rules[i];
                if (rule.LeftSide.GetRepresentation() == leftName &&
                    (!weight.HasValue || rule.Weight.Equals(weight.Value)) &&
                    RightSidesMatch(rule.RightSide, expectedRightSide))
                {
                    Grammar.Rules.RemoveAt(i);
                    RefreshVariableRules();
                    return true;
                }
            }

            return false;
        }

        private VariableSymbol FindVariable(string representation)
        {
            for (int i = 0; i < Grammar.Variables.Count; i++)
            {
                if (Grammar.Variables[i].GetRepresentation() == representation)
                {
                    return Grammar.Variables[i];
                }
            }

            return null;
        }

        private TerminalSymbol FindTerminal(string representation)
        {
            for (int i = 0; i < Grammar.Terminals.Count; i++)
            {
                if (Grammar.Terminals[i].GetRepresentation() == representation)
                {
                    return Grammar.Terminals[i];
                }
            }

            return null;
        }

        private bool TryParseRightSide(string rightSide, out Symbol[] symbols)
        {
            symbols = null;
            if (Grammar == null)
            {
                return false;
            }

            string text = rightSide == null ? string.Empty : rightSide.Trim();
            if (text.Length == 0 || text == "ε" || string.Equals(text, "epsilon", StringComparison.OrdinalIgnoreCase))
            {
                TerminalSymbol emptyTerminal = FindTerminal(string.Empty);
                if (emptyTerminal == null || !emptyTerminal.IsEmpty)
                {
                    return false;
                }

                symbols = new Symbol[] { emptyTerminal };
                return true;
            }

            var parsedSymbols = new List<Symbol>();
            string[] groups = text.Split('.');
            for (int i = 0; i < groups.Length; i++)
            {
                string group = groups[i].Trim();
                if (group.Length == 0)
                {
                    return false;
                }

                string[] names = group.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int j = 0; j < names.Length; j++)
                {
                    if (names[j] == "ε" || string.Equals(names[j], "epsilon", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    VariableSymbol variable = FindVariable(names[j]);
                    Symbol symbol = variable != null ? (Symbol)variable : FindTerminal(names[j]);
                    if (symbol == null)
                    {
                        return false;
                    }

                    parsedSymbols.Add(symbol);
                }
            }

            if (parsedSymbols.Count == 0)
            {
                return false;
            }

            symbols = parsedSymbols.ToArray();
            return true;
        }

        private bool ContainsVariable(VariableSymbol variable)
        {
            for (int i = 0; i < Grammar.Variables.Count; i++)
            {
                if (ReferenceEquals(Grammar.Variables[i], variable))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ContainsSymbol(Symbol symbol)
        {
            if (symbol is VariableSymbol variable)
            {
                return ContainsVariable(variable);
            }

            if (symbol is TerminalSymbol terminal)
            {
                for (int i = 0; i < Grammar.Terminals.Count; i++)
                {
                    if (ReferenceEquals(Grammar.Terminals[i], terminal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RefreshVariableRules()
        {
            for (int i = 0; i < Grammar.Variables.Count; i++)
            {
                Grammar.Variables[i].ClearRules();
                Grammar.Variables[i].AddRules(Grammar.Rules);
            }
        }

        private static bool RightSidesMatch(Symbol[] left, Symbol[] right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            for (int i = 0; i < left.Length; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsReservedEmptyName(string name)
        {
            return name == "ε" || string.Equals(name, "epsilon", StringComparison.OrdinalIgnoreCase);
        }

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
            onGrammarModificationFinished.Invoke(result);
        }

        private string FormatGrammar(string heading = "Grammar built successfully.")
        {
            var output = new StringBuilder();
            output.AppendLine(heading);
            output.AppendLine("Variables:");
            AppendIndexedSymbols(output, Grammar.Variables);
            output.AppendLine();
            output.AppendLine("Terminals:");
            AppendIndexedSymbols(output, Grammar.Terminals);
            output.AppendLine();
            output.AppendLine("Rules:");

            for (int i = 0; i < Grammar.Rules.Count; i++)
            {
                Rule rule = Grammar.Rules[i];
                output.Append("  [");
                output.Append(i);
                output.Append("] ");
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

        private static void AppendIndexedSymbols<T>(StringBuilder output, IList<T> symbols) where T : Symbol
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                output.Append("  [");
                output.Append(i);
                output.Append("] ");
                output.AppendLine(IsEmptyTerminal(symbols[i]) ? "ε" : symbols[i].GetRepresentation());
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
            onGrammarModificationFinished.Invoke(result);
        }

        private void PublishModificationResult(string heading, IList<string> messages = null)
        {
            var output = new StringBuilder();
            if (messages != null && messages.Count > 0)
            {
                for (int i = 0; i < messages.Count; i++)
                {
                    output.AppendLine(messages[i]);
                }
            }

            output.Append(FormatGrammar(heading));
            string result = output.ToString();
            Debug.Log(result, this);
            onGrammarModificationFinished.Invoke(result);
        }

        private void PublishModificationError(string error)
        {
            LastError = error;
            string result = "Grammar modification failed: " + error;
            if (Grammar != null)
            {
                result += "\n" + FormatGrammar("Current grammar:");
            }
            Debug.LogError(result, this);
            onGrammarModificationFinished.Invoke(result);
        }
    }
}

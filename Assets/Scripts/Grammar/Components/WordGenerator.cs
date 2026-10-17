using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Generates terminal words from the grammar built by a GrammarController.
    /// </summary>
    public sealed class WordGenerator : MonoBehaviour
    {
        [SerializeField] private GrammarController grammarController;
        [SerializeField, Min(1)] private int maxVariableExpansions = 10000;

        private readonly System.Random random = new System.Random();

        public Grammar Grammar { get; private set; }
        public TerminalSymbol[] GeneratedWord { get; private set; } = new TerminalSymbol[0];
        public string LastError { get; private set; } = string.Empty;

        /// <summary>
        /// Captures the current grammar from the assigned controller. Call after BuildGrammar succeeds.
        /// </summary>
        public bool CaptureGrammar()
        {
            if (grammarController == null)
            {
                grammarController = GetComponent<GrammarController>();
            }

            if (grammarController == null)
            {
                return Fail("Assign a GrammarController to the WordGenerator.");
            }

            if (grammarController.Grammar == null)
            {
                return Fail("The GrammarController does not currently contain a valid grammar. Build the grammar first.");
            }

            Grammar = grammarController.Grammar;
            LastError = string.Empty;
            return true;
        }

        /// <summary>
        /// Gets the start symbol: the first variable declared in the grammar.
        /// </summary>
        public VariableSymbol GetInitialSymbol()
        {
            if (Grammar == null && !CaptureGrammar())
            {
                throw new InvalidOperationException(LastError);
            }

            if (Grammar.Variables.Count == 0)
            {
                throw new InvalidOperationException("The grammar has no variable symbols to use as a start symbol.");
            }

            return Grammar.Variables[0];
        }

        /// <summary>
        /// Expands the start variable until only terminal symbols remain, preserving their order.
        /// Can be called from a Unity UI Button.
        /// </summary>
        public void BuildWord()
        {
            if (!CaptureGrammar())
            {
                GeneratedWord = new TerminalSymbol[0];
                return;
            }

            try
            {
                VariableSymbol initialSymbol = GetInitialSymbol();
                var pendingSymbols = new Stack<Symbol>();
                var finalSymbols = new List<TerminalSymbol>();
                pendingSymbols.Push(initialSymbol);

                int expansions = 0;
                while (pendingSymbols.Count > 0)
                {
                    Symbol symbol = pendingSymbols.Pop();
                    if (symbol is TerminalSymbol terminal)
                    {
                        finalSymbols.Add(terminal);
                        continue;
                    }

                    if (!(symbol is VariableSymbol variable))
                    {
                        throw new InvalidOperationException("The grammar contains a symbol type that cannot be expanded.");
                    }

                    expansions++;
                    if (expansions > maxVariableExpansions)
                    {
                        throw new InvalidOperationException(
                            "Word generation exceeded the maximum of " + maxVariableExpansions +
                            " variable expansions. The grammar may contain a non-terminating cycle.");
                    }

                    Rule selectedRule = variable.GetRandomRule(random);
                    Symbol[] rightSide = selectedRule.RightSide;

                    // A stack is last-in-first-out, so push right-side symbols backward to process them in order.
                    for (int i = rightSide.Length - 1; i >= 0; i--)
                    {
                        pendingSymbols.Push(rightSide[i]);
                    }
                }

                GeneratedWord = finalSymbols.ToArray();
                LastError = string.Empty;
                Debug.Log("Generated word: " + FormatWord(GeneratedWord), this);
            }
            catch (InvalidOperationException exception)
            {
                GeneratedWord = new TerminalSymbol[0];
                LastError = exception.Message;
                Debug.LogError("Word generation failed: " + LastError, this);
            }
        }

        private bool Fail(string error)
        {
            Grammar = null;
            LastError = error;
            Debug.LogError("Word generation failed: " + error, this);
            return false;
        }

        private static string FormatWord(IEnumerable<TerminalSymbol> symbols)
        {
            var output = new StringBuilder();
            foreach (TerminalSymbol symbol in symbols)
            {
                output.Append(symbol.GetRepresentation());
            }

            return output.ToString();
        }
    }
}

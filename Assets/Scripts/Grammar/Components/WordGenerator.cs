using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace VocabGen.Grammar
{
    [System.Serializable]
    public sealed class WordGenerationResultEvent : UnityEvent<string>
    {
    }

    /// <summary>
    /// Generates terminal words from the grammar built by a GrammarController.
    /// </summary>
    public sealed class WordGenerator : MonoBehaviour
    {
        [SerializeField] private GrammarController grammarController;
        [SerializeField] private TMP_InputField wordCountInputField;
        [SerializeField, Min(1)] private int maxVariableExpansions = 10000;
        [SerializeField] private WordGenerationResultEvent onGenerationFinished = new WordGenerationResultEvent();

        private readonly System.Random random = new System.Random();

        public Grammar Grammar { get; private set; }
        public TerminalSymbol[] GeneratedWord { get; private set; } = new TerminalSymbol[0];
        public TerminalSymbol[][] GeneratedWords { get; private set; } = new TerminalSymbol[0][];
        public string LastError { get; private set; } = string.Empty;
        public WordGenerationResultEvent OnGenerationFinished => onGenerationFinished;

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
        /// Can be called from a Unity UI Button to generate one word.
        /// </summary>
        public void BuildWord()
        {
            BuildWords(1);
        }

        /// <summary>Reads the count field and generates that many words. Connect this to the Generate button.</summary>
        public void BuildWordsFromInput()
        {
            if (wordCountInputField == null)
            {
                ClearGeneratedWords();
                ReportFailure("Assign the word count TMP_InputField to the WordGenerator.");
                return;
            }

            string value = wordCountInputField.text;
            if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int count) || count < 1)
            {
                ClearGeneratedWords();
                ReportFailure("Enter a whole number greater than zero in the word count field.");
                return;
            }

            BuildWords(count);
        }

        /// <summary>
        /// Generates the requested number of words and stores each terminal sequence in GeneratedWords.
        /// </summary>
        public void BuildWords(int count)
        {
            ClearGeneratedWords();

            if (count < 1)
            {
                ReportFailure("The number of words to generate must be at least 1.");
                return;
            }

            if (!CaptureGrammar())
            {
                return;
            }

            var generated = new List<TerminalSymbol[]>(count);
            var output = new StringBuilder();
            try
            {
                for (int wordIndex = 0; wordIndex < count; wordIndex++)
                {
                    TerminalSymbol[] word = GenerateOneWord();
                    generated.Add(word);
                    GeneratedWord = word;
                    output.AppendLine(FormatWord(word));
                }

                GeneratedWords = generated.ToArray();
                LastError = string.Empty;
                string result = output.ToString();
                Debug.Log(result, this);
                onGenerationFinished.Invoke(result);
            }
            catch (InvalidOperationException exception)
            {
                GeneratedWords = generated.ToArray();
                string result = output.ToString() + "Word generation failed: " + exception.Message;
                LastError = exception.Message;
                Debug.LogError(result, this);
                onGenerationFinished.Invoke(result);
            }
        }

        private TerminalSymbol[] GenerateOneWord()
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

            return finalSymbols.ToArray();
        }

        private bool Fail(string error)
        {
            Grammar = null;
            ReportFailure(error);
            return false;
        }

        private void ReportFailure(string error)
        {
            LastError = error;
            string result = "Word generation failed: " + error;
            Debug.LogError(result, this);
            onGenerationFinished.Invoke(result);
        }

        private void ClearGeneratedWords()
        {
            GeneratedWord = new TerminalSymbol[0];
            GeneratedWords = new TerminalSymbol[0][];
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

using System;
using System.Collections.Generic;

namespace VocabGen.Grammar
{
    /// <summary>
    /// A complete context-independent grammar and its symbols and production rules.
    /// </summary>
    public sealed class Grammar
    {
        public List<TerminalSymbol> Terminals { get; }
        public List<VariableSymbol> Variables { get; }
        public List<Rule> Rules { get; }

        public Grammar(
            IEnumerable<TerminalSymbol> terminals,
            IEnumerable<VariableSymbol> variables,
            IEnumerable<Rule> rules)
        {
            if (terminals == null) throw new ArgumentNullException(nameof(terminals));
            if (variables == null) throw new ArgumentNullException(nameof(variables));
            if (rules == null) throw new ArgumentNullException(nameof(rules));

            Terminals = new List<TerminalSymbol>(terminals);
            if (!Terminals.Contains(TerminalSymbol.Empty))
            {
                Terminals.Add(TerminalSymbol.Empty);
            }
            Variables = new List<VariableSymbol>(variables);
            Rules = new List<Rule>(rules);
        }
    }
}

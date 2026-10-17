using System;

namespace VocabGen.Grammar
{
    /// <summary>
    /// A terminal symbol, which cannot be expanded by a grammar rule.
    /// </summary>
    public sealed class TerminalSymbol : Symbol
    {
        public string Representation { get; }

        public TerminalSymbol(string representation)
        {
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));
        }

        public override string GetRepresentation()
        {
            return Representation;
        }
    }
}

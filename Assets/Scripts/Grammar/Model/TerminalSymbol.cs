using System;

namespace VocabGen.Grammar
{
    /// <summary>
    /// A terminal symbol, which cannot be expanded by a grammar rule.
    /// </summary>
    public sealed class TerminalSymbol : Symbol
    {
        public static TerminalSymbol Empty { get; } = new TerminalSymbol(string.Empty, true);

        public string Representation { get; }
        public bool IsEmpty { get; }

        public TerminalSymbol(string representation)
            : this(representation, false)
        {
        }

        private TerminalSymbol(string representation, bool isEmpty)
        {
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));
            IsEmpty = isEmpty;
        }

        public override string GetRepresentation()
        {
            return Representation;
        }
    }
}

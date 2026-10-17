namespace VocabGen.Grammar
{
    /// <summary>
    /// Base type for symbols used in a context-free grammar.
    /// </summary>
    public abstract class Symbol
    {
        public abstract string GetRepresentation();
    }
}

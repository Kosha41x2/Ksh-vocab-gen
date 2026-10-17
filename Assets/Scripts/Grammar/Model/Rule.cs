using System;

namespace VocabGen.Grammar
{
    /// <summary>
    /// A grammar production: one variable on the left and a sequence of symbols on the right.
    /// </summary>
    public sealed class Rule
    {
        private readonly Symbol[] rightSide;

        public VariableSymbol LeftSide { get; }
        public Symbol[] RightSide => (Symbol[])rightSide.Clone();
        public float Weight { get; }

        public Rule(VariableSymbol leftSide, Symbol[] rightSide, float weight = 1f)
        {
            LeftSide = leftSide ?? throw new ArgumentNullException(nameof(leftSide));
            if (rightSide == null)
            {
                throw new ArgumentNullException(nameof(rightSide));
            }

            if (float.IsNaN(weight) || float.IsInfinity(weight) || weight < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(weight), "Rule weight must be finite and non-negative.");
            }

            this.rightSide = (Symbol[])rightSide.Clone();
            for (int i = 0; i < this.rightSide.Length; i++)
            {
                if (this.rightSide[i] == null)
                {
                    throw new ArgumentException("The right side cannot contain null symbols.", nameof(rightSide));
                }
            }

            Weight = weight;
            LeftSide.AddRule(this);
        }
    }
}

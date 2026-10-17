using System;
using System.Collections.Generic;

namespace VocabGen.Grammar
{
    /// <summary>
    /// A non-terminal grammar symbol and the rules that can expand it.
    /// </summary>
    public sealed class VariableSymbol : Symbol
    {
        private readonly List<Rule> rules = new List<Rule>();

        public string Representation { get; }

        /// <summary>
        /// Returns a snapshot of all rules whose left side is this variable.
        /// </summary>
        public Rule[] Rules => rules.ToArray();

        public VariableSymbol(string representation)
        {
            Representation = representation ?? throw new ArgumentNullException(nameof(representation));
        }

        public override string GetRepresentation()
        {
            return Representation;
        }

        internal void AddRule(Rule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            rules.Add(rule);
        }

        internal void ClearRules()
        {
            rules.Clear();
        }

        /// <summary>
        /// Adds each supplied rule whose left side is this variable. Existing rules are not added twice.
        /// </summary>
        public void AddRules(IEnumerable<Rule> availableRules)
        {
            if (availableRules == null)
            {
                throw new ArgumentNullException(nameof(availableRules));
            }

            foreach (Rule rule in availableRules)
            {
                if (rule != null && ReferenceEquals(rule.LeftSide, this) && !rules.Contains(rule))
                {
                    rules.Add(rule);
                }
            }
        }

        /// <summary>
        /// Chooses one of this variable's rules in proportion to its weight.
        /// Weights must be non-negative and at least one rule must have a positive weight.
        /// </summary>
        public Rule GetRandomRule(Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (rules.Count == 0)
            {
                throw new InvalidOperationException("This variable has no rules.");
            }

            double totalWeight = 0d;
            foreach (Rule rule in rules)
            {
                totalWeight += rule.Weight;
            }

            if (totalWeight <= 0d)
            {
                throw new InvalidOperationException("At least one rule must have a positive weight.");
            }

            double choice = random.NextDouble() * totalWeight;
            double cumulativeWeight = 0d;

            foreach (Rule rule in rules)
            {
                cumulativeWeight += rule.Weight;
                if (choice < cumulativeWeight)
                {
                    return rule;
                }
            }

            // Protect against floating-point rounding at the upper end of the range.
            return rules[rules.Count - 1];
        }
    }
}

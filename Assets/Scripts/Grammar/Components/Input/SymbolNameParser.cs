using System;
using System.Collections.Generic;

namespace VocabGen.Grammar
{
    /// <summary>
    /// Shared validation for comma-separated terminal and variable symbol names.
    /// </summary>
    internal static class SymbolNameParser
    {
        public static bool TryParse(string input, out List<string> names, out string error)
        {
            names = new List<string>();
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Enter one or more symbols separated by commas.";
                return false;
            }

            string[] entries = input.Split(',');
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                string name = entries[i].Trim();
                if (name.Length == 0)
                {
                    error = "Entry " + (i + 1) + " is empty. Remove extra commas.";
                    return false;
                }

                if (!IsValidName(name))
                {
                    error = "'" + name + "' contains whitespace or a reserved character. Use one token per symbol.";
                    return false;
                }

                if (!seen.Add(name))
                {
                    error = "Symbol '" + name + "' is listed more than once.";
                    return false;
                }

                names.Add(name);
            }

            return true;
        }

        private static bool IsValidName(string name)
        {
            if (name == "->" || name == "ε" || string.Equals(name, "epsilon", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            for (int i = 0; i < name.Length; i++)
            {
                char character = name[i];
                if (char.IsWhiteSpace(character) || character == '(' || character == ')' || character == ',' || character == '.')
                {
                    return false;
                }

                if (character == '-' && i + 1 < name.Length && name[i + 1] == '>')
                {
                    return false;
                }
            }

            return true;
        }
    }
}

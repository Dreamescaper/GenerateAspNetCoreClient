using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GenerateClientCommand.Extensions
{
    internal static class StringExtensions
    {
        private static readonly Regex NonAlphaNumericRegex = new Regex(@"[^a-zA-Z0-9]+");

        public static string ToCamelCase(this string @this)
        {
            if (@this.Length == 0)
                return @this;

            return char.ToLowerInvariant(@this[0]) + @this.Substring(1);
        }

        public static string ToPascalCase(this string @this)
        {
            if (@this.Length == 0)
                return @this;

            var parts = NonAlphaNumericRegex
                .Split(@this)
                .Select(PartToPascalCase);

            return string.Concat(parts);

            static string PartToPascalCase(string part)
            {
                if (part.Length == 0)
                    return part;

                if (part.ToUpperInvariant() == part)
                {
                    // If part is all uppercase - convert to lowercase all apart from first letter.
                    return part[0] + part.Substring(1).ToLowerInvariant();
                }
                else
                {
                    // Otherwise - convert first letter to uppercase, everything else - as is.
                    return char.ToUpperInvariant(part[0]) + part.Substring(1);
                }
            }
        }

        public static string Indent(this string @this, string indent)
        {
            var lines = @this.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var indentedLines = lines.Select(line => line.Length == 0 ? line : indent + line);
            return string.Join(Environment.NewLine, indentedLines);
        }

        public static string GetCommonPart(this IEnumerable<string> @this, string partSeparator)
        {
            string[][] partsArrays = @this
                .Select(ns => ns.Split(partSeparator))
                .ToArray();

            if (partsArrays.Length == 0)
                return "";

            var anyParts = partsArrays[0];
            var commonParts = anyParts.TakeWhile((part, i) => partsArrays.All(ns => ns.Length > i && ns[i] == part));

            return string.Join(partSeparator, commonParts);
        }
    }
}

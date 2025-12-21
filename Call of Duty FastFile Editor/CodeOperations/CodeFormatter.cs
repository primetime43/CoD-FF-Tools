using System.Text;
using System.Text.RegularExpressions;

namespace Call_of_Duty_FastFile_Editor.CodeOperations
{
    /// <summary>
    /// Formats/beautifies compressed or minified code by adding proper indentation,
    /// line breaks, and spacing. The opposite of CodeCompressor.
    /// Supports both GSC scripts (semicolon-based) and arena/cfg files (key-value based).
    /// </summary>
    public static class CodeFormatter
    {
        private const string IndentString = "\t";

        /// <summary>
        /// Formats the given code with proper indentation and line breaks.
        /// Auto-detects format: GSC (has semicolons) vs Arena/Config (key "value" pairs).
        /// </summary>
        /// <param name="code">The compressed or unformatted code.</param>
        /// <returns>Properly formatted code with indentation and line breaks.</returns>
        public static string FormatCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return code;

            // Normalize line endings
            code = code.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

            // Detect format: if code has semicolons outside of strings, it's likely GSC
            // If it has pattern like: keyword"value" or keyword "value", it's arena/config format
            bool isGscFormat = ContainsSemicolonsOutsideStrings(code);

            if (isGscFormat)
            {
                return FormatGscCode(code);
            }
            else
            {
                return FormatArenaConfigCode(code);
            }
        }

        /// <summary>
        /// Checks if the code contains semicolons outside of string literals.
        /// </summary>
        private static bool ContainsSemicolonsOutsideStrings(string code)
        {
            bool inString = false;
            char stringChar = '\0';

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                char prev = i > 0 ? code[i - 1] : '\0';

                if ((c == '"' || c == '\'') && prev != '\\')
                {
                    if (!inString)
                    {
                        inString = true;
                        stringChar = c;
                    }
                    else if (c == stringChar)
                    {
                        inString = false;
                    }
                }

                if (!inString && c == ';')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Formats arena/config files that use key "value" pairs.
        /// Each key-value pair goes on its own line, with proper indentation inside braces.
        /// </summary>
        private static string FormatArenaConfigCode(string code)
        {
            var result = new StringBuilder();
            int indentLevel = 0;
            bool inString = false;
            int i = 0;

            while (i < code.Length)
            {
                char c = code[i];
                char prev = i > 0 ? code[i - 1] : '\0';
                char next = i < code.Length - 1 ? code[i + 1] : '\0';

                // Track string state
                if (c == '"' && prev != '\\')
                {
                    inString = !inString;
                    result.Append(c);

                    // After closing quote, check if next non-whitespace is a keyword (letter)
                    if (!inString)
                    {
                        // Look ahead to see what follows
                        int lookAhead = i + 1;
                        while (lookAhead < code.Length && (code[lookAhead] == ' ' || code[lookAhead] == '\t'))
                        {
                            lookAhead++;
                        }

                        // If followed by a letter (start of keyword) or end of content before }, add newline
                        if (lookAhead < code.Length)
                        {
                            char nextNonSpace = code[lookAhead];
                            if (char.IsLetter(nextNonSpace))
                            {
                                // This is another keyword - add newline
                                result.Append('\n');
                                AppendIndent(result, indentLevel);
                            }
                            else if (nextNonSpace == '}')
                            {
                                // Closing brace coming - will be handled below
                            }
                        }
                    }
                    i++;
                    continue;
                }

                // Inside string - just append
                if (inString)
                {
                    result.Append(c);
                    i++;
                    continue;
                }

                // Handle opening brace
                if (c == '{')
                {
                    // Trim trailing whitespace before brace
                    TrimTrailingWhitespace(result);
                    result.Append('\n');
                    AppendIndent(result, indentLevel);
                    result.Append('{');
                    indentLevel++;
                    result.Append('\n');
                    AppendIndent(result, indentLevel);
                    i++;
                    continue;
                }

                // Handle closing brace
                if (c == '}')
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    TrimTrailingWhitespace(result);
                    result.Append('\n');
                    AppendIndent(result, indentLevel);
                    result.Append('}');
                    result.Append('\n');
                    // Add blank line between blocks
                    if (next != '\0' && next != '}')
                    {
                        result.Append('\n');
                    }
                    AppendIndent(result, indentLevel);
                    i++;
                    continue;
                }

                // Skip existing newlines and tabs - we control formatting
                if (c == '\n' || c == '\r' || c == '\t')
                {
                    i++;
                    continue;
                }

                // Handle spaces - normalize multiple spaces to tabs for alignment
                if (c == ' ')
                {
                    // Count consecutive spaces
                    int spaceCount = 0;
                    int j = i;
                    while (j < code.Length && code[j] == ' ')
                    {
                        spaceCount++;
                        j++;
                    }

                    // If we just wrote a keyword (last char is letter) and next is quote, use tabs for alignment
                    if (result.Length > 0 && char.IsLetter(result[result.Length - 1]) && j < code.Length && code[j] == '"')
                    {
                        // Add tabs for alignment (arena style)
                        result.Append('\t');
                        if (spaceCount > 4)
                        {
                            result.Append('\t'); // Extra tab for longer keywords
                        }
                    }
                    else if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
                    {
                        // Single space otherwise
                        result.Append(' ');
                    }

                    i = j; // Skip all spaces
                    continue;
                }

                // Default: append character
                result.Append(c);
                i++;
            }

            // Final cleanup
            string formatted = result.ToString().Trim();

            // Remove excessive blank lines
            while (formatted.Contains("\n\n\n"))
            {
                formatted = formatted.Replace("\n\n\n", "\n\n");
            }

            // Remove trailing whitespace from each line
            var lines = formatted.Split('\n');
            formatted = string.Join("\n", lines.Select(line => line.TrimEnd()));

            return formatted;
        }

        /// <summary>
        /// Formats GSC script code that uses semicolons to end statements.
        /// </summary>
        private static string FormatGscCode(string code)
        {
            var result = new StringBuilder();
            int indentLevel = 0;
            bool inString = false;
            bool inSingleLineComment = false;
            bool inMultiLineComment = false;
            char stringChar = '\0';
            int parenDepth = 0;

            for (int i = 0; i < code.Length; i++)
            {
                char c = code[i];
                char prev = i > 0 ? code[i - 1] : '\0';
                char next = i < code.Length - 1 ? code[i + 1] : '\0';

                // Handle string literals
                if (!inSingleLineComment && !inMultiLineComment)
                {
                    if ((c == '"' || c == '\'') && prev != '\\')
                    {
                        if (!inString)
                        {
                            inString = true;
                            stringChar = c;
                        }
                        else if (c == stringChar)
                        {
                            inString = false;
                        }
                    }
                }

                // Handle comments
                if (!inString && !inMultiLineComment && c == '/' && next == '/')
                {
                    inSingleLineComment = true;
                }
                if (!inString && !inSingleLineComment && c == '/' && next == '*')
                {
                    inMultiLineComment = true;
                }
                if (inMultiLineComment && c == '*' && next == '/')
                {
                    result.Append("*/");
                    i++;
                    inMultiLineComment = false;
                    continue;
                }
                if (inSingleLineComment && c == '\n')
                {
                    inSingleLineComment = false;
                }

                // Inside string or comment - just append
                if (inString || inSingleLineComment || inMultiLineComment)
                {
                    result.Append(c);
                    continue;
                }

                // Track parentheses depth
                if (c == '(')
                {
                    parenDepth++;
                    result.Append(c);
                    continue;
                }
                if (c == ')')
                {
                    parenDepth--;
                    result.Append(c);
                    continue;
                }

                // Handle opening brace
                if (c == '{')
                {
                    if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
                    {
                        result.Append(' ');
                    }
                    result.Append('{');
                    indentLevel++;
                    result.Append('\n');
                    AppendIndent(result, indentLevel);
                    continue;
                }

                // Handle closing brace
                if (c == '}')
                {
                    indentLevel = Math.Max(0, indentLevel - 1);
                    TrimTrailingWhitespace(result);
                    result.Append('\n');
                    AppendIndent(result, indentLevel);
                    result.Append('}');
                    if (next != '\0' && next != '}' && !IsFollowedByElse(code, i + 1))
                    {
                        result.Append('\n');
                        AppendIndent(result, indentLevel);
                    }
                    continue;
                }

                // Handle semicolon
                if (c == ';')
                {
                    result.Append(';');
                    if (parenDepth == 0)
                    {
                        result.Append('\n');
                        AppendIndent(result, indentLevel);
                    }
                    else
                    {
                        result.Append(' ');
                    }
                    continue;
                }

                // Skip newlines/tabs - we control formatting
                if (c == '\n' || c == '\r' || c == '\t')
                {
                    continue;
                }

                // Normalize spaces
                if (c == ' ')
                {
                    if (result.Length > 0 && !char.IsWhiteSpace(result[result.Length - 1]))
                    {
                        result.Append(' ');
                    }
                    continue;
                }

                // Handle operators with spacing
                if (IsOperatorChar(c) && ShouldAddSpacing(code, i, result))
                {
                    string op = GetOperator(code, i);
                    if (op.Length > 1)
                    {
                        EnsureSpaceBefore(result);
                        result.Append(op);
                        i += op.Length - 1;
                        if (i + 1 < code.Length && !char.IsWhiteSpace(code[i + 1]))
                        {
                            result.Append(' ');
                        }
                        continue;
                    }
                    else if (c == '=' || c == '+' || c == '-' || c == '<' || c == '>')
                    {
                        EnsureSpaceBefore(result);
                        result.Append(c);
                        result.Append(' ');
                        continue;
                    }
                }

                result.Append(c);
            }

            string formatted = result.ToString().Trim();

            while (formatted.Contains("\n\n\n"))
            {
                formatted = formatted.Replace("\n\n\n", "\n\n");
            }

            var lines = formatted.Split('\n');
            formatted = string.Join("\n", lines.Select(line => line.TrimEnd()));

            return formatted;
        }

        private static void AppendIndent(StringBuilder sb, int level)
        {
            for (int i = 0; i < level; i++)
            {
                sb.Append(IndentString);
            }
        }

        private static void TrimTrailingWhitespace(StringBuilder sb)
        {
            while (sb.Length > 0 && (sb[sb.Length - 1] == ' ' || sb[sb.Length - 1] == '\t'))
            {
                sb.Length--;
            }
        }

        private static void EnsureSpaceBefore(StringBuilder sb)
        {
            if (sb.Length > 0 && !char.IsWhiteSpace(sb[sb.Length - 1]))
            {
                sb.Append(' ');
            }
        }

        private static bool IsOperatorChar(char c)
        {
            return c == '=' || c == '+' || c == '-' || c == '*' || c == '/' ||
                   c == '<' || c == '>' || c == '!' || c == '&' || c == '|';
        }

        private static bool ShouldAddSpacing(string code, int index, StringBuilder result)
        {
            char c = code[index];
            char prev = index > 0 ? code[index - 1] : '\0';
            char next = index < code.Length - 1 ? code[index + 1] : '\0';

            if ((c == '-' || c == '+') && (prev == '(' || prev == ',' || prev == '=' || prev == '['))
            {
                return false;
            }

            if ((c == '+' && next == '+') || (c == '-' && next == '-'))
            {
                return false;
            }

            if (c == '/' && (next == '/' || next == '*'))
            {
                return false;
            }
            if (c == '*' && next == '/')
            {
                return false;
            }

            return true;
        }

        private static string GetOperator(string code, int index)
        {
            if (index + 2 < code.Length)
            {
                string threeChar = code.Substring(index, 3);
                if (threeChar == "===" || threeChar == "!==")
                    return threeChar;
            }

            if (index + 1 < code.Length)
            {
                string twoChar = code.Substring(index, 2);
                if (twoChar == "==" || twoChar == "!=" || twoChar == "+=" ||
                    twoChar == "-=" || twoChar == "*=" || twoChar == "/=" ||
                    twoChar == "<=" || twoChar == ">=" || twoChar == "&&" ||
                    twoChar == "||" || twoChar == "++" || twoChar == "--")
                    return twoChar;
            }

            return code[index].ToString();
        }

        private static bool IsFollowedByElse(string code, int index)
        {
            while (index < code.Length && char.IsWhiteSpace(code[index]))
            {
                index++;
            }

            if (index + 4 <= code.Length && code.Substring(index, 4) == "else")
            {
                return true;
            }

            return false;
        }
    }
}

namespace DarkChronicle;

using System;
using System.Globalization;
using System.Text.RegularExpressions;

public static class StringUtils
{
    public static string Unescape(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        return Regex.Unescape(UnicodeConverter.Unescape(value));
    }
}

internal static class UnicodeConverter
{
    private static readonly Regex rgx = new(@"(\[UNI(?<hex>[0-9a-fA-F]{4})\])", RegexOptions.Compiled);

    public static string Unescape(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));

        return rgx.Replace(value, m => ((char)int.Parse(m.Groups["hex"].Value, NumberStyles.HexNumber)).ToString());
    }
}

public sealed class Wildcard
{
    public static bool IsMatch(string s, string? pattern)
    {
        if (s is null) throw new ArgumentNullException(nameof(s));

        if (pattern is null) return true;

        // Characters matched so far
        int matched = 0;

        // Loop through pattern string
        for (int i = 0; i < pattern.Length;)
        {
            // Check for end of string
            if (matched > s.Length)
                return false;

            // Get next pattern character
            char c = pattern[i++];
            if (c == '?') // Any single character
            {
                matched++;
            }
            else if (c == '#') // Any single digit
            {
                if (!Char.IsDigit(s[matched]))
                    return false;
                matched++;
            }
            else if (c == '*') // Zero or more characters
            {
                if (i < pattern.Length)
                {
                    // Matches all characters until
                    // next character in pattern
                    char next = pattern[i];
                    int j = s.IndexOf(next, matched);
                    if (j < 0)
                        return false;
                    matched = j;
                }
                else
                {
                    // Matches all remaining characters
                    matched = s.Length;
                    break;
                }
            }
            else // Exact character
            {
                if (matched >= s.Length || c != s[matched])
                    return false;
                matched++;
            }
        }

        // Return true if all characters matched
        return (matched == s.Length);
    }
}

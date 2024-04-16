using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public static class StringExtensions
{
    public static string Truncate(this string str, int length)
    {
        if (str.Length <= length)
            return str;

        return string.Concat(str.AsSpan(0, length - 1), "…");
    }

    public static IEnumerable<string> ToLines(this string input)
    {
        using var sr = new StringReader(input);
        string? line = sr.ReadLine(), nextLine = null;
        if (string.IsNullOrEmpty(line))
            nextLine = sr.ReadLine();
        else nextLine = line;

        while ((line = sr.ReadLine()) != null)
        {
            yield return nextLine!;
            nextLine = line;
        }

        if (!string.IsNullOrEmpty(nextLine))
            yield return nextLine;
    }

    public static string NullIfEmpty(this string str) => string.IsNullOrEmpty(str) ? null : str;
}
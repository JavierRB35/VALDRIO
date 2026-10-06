using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Valdrio.Application;

namespace Valdrio.Infrastructure;

// Classify the section Excel applies to this stored value, never all sections together.
internal static class XlsxNumberFormatClassifier
{
    private static readonly Regex Number = new(
        @"^[+-]?(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static bool IsDate(string code, string? raw)
    {
        var sections = SplitSections(code).Select(ParseSection).ToArray();
        if (sections.Length is < 1 or > 4) throw Unsupported();
        var numeric = sections.Take(3).ToArray();
        if (numeric.Skip(2).Any(s => s.Condition is not null)) throw Unsupported();

        Section selected;
        if (numeric.Any(s => s.Condition is not null))
        {
            // One condition + a fallback, or two conditions + an optional fallback.
            if (numeric[0].Condition is null ||
                numeric.Length == 3 && numeric[1].Condition is null)
                throw Unsupported();
            var value = ReadNumber(raw);
            var match = numeric.FirstOrDefault(s => s.Condition is null || s.Condition.Matches(value));
            if (match is null) return false; // Excel displays hashes when no section matches.
            selected = match;
        }
        else if (numeric.Length == 1)
        {
            selected = numeric[0];
        }
        else
        {
            var sign = ReadNumber(raw).Coefficient.Sign;
            selected = numeric[sign < 0 ? 1 : sign == 0 && numeric.Length == 3 ? 2 : 0];
        }

        return ContainsDateTokens(selected.Tokens);
    }

    private sealed record Section(string Tokens, Condition? Condition);
    private sealed record ExactNumber(BigInteger Coefficient, int Scale);
    private sealed record Condition(string Operator, ExactNumber Threshold)
    {
        public bool Matches(ExactNumber value)
        {
            var scale = Math.Max(value.Scale, Threshold.Scale);
            var comparison = (value.Coefficient * BigInteger.Pow(10, scale - value.Scale))
                .CompareTo(Threshold.Coefficient * BigInteger.Pow(10, scale - Threshold.Scale));
            return Operator switch
            {
                "<" => comparison < 0, "<=" => comparison <= 0,
                ">" => comparison > 0, ">=" => comparison >= 0,
                "=" => comparison == 0, "<>" => comparison != 0,
                _ => throw Unsupported()
            };
        }
    }

    private static ExactNumber ReadNumber(string? raw)
    {
        if (raw is null || raw.Length > 4096 || !Number.IsMatch(raw)) throw Unsupported();
        var exponentIndex = raw.IndexOfAny(['e', 'E']);
        var exponent = 0;
        if (exponentIndex >= 0 &&
            (!int.TryParse(raw[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture, out exponent) || exponent is < -4096 or > 4096))
            throw Unsupported();
        var mantissa = exponentIndex < 0 ? raw : raw[..exponentIndex];
        var dot = mantissa.IndexOf('.');
        var fraction = dot < 0 ? 0 : mantissa.Length - dot - 1;
        var coefficient = BigInteger.Parse(mantissa.Replace(".", "", StringComparison.Ordinal),
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        return new(coefficient, fraction - exponent);
    }

    private static IEnumerable<string> SplitSections(string code)
    {
        if (code.Length > 4096) throw Unsupported();
        var start = 0;
        var quoted = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c is '\\' or '_' or '*')
            {
                if (++i == code.Length) throw Invalid();
                continue;
            }
            if (c == '[')
            {
                var closing = code.IndexOf(']', i + 1);
                if (closing < 0) throw Invalid();
                i = closing;
            }
            else if (c == ';') { yield return code[start..i]; start = i + 1; }
        }
        if (quoted) throw Invalid();
        yield return code[start..];
    }

    private static Section ParseSection(string code)
    {
        var tokens = new StringBuilder();
        Condition? condition = null;
        var quoted = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (c is '\\' or '_' or '*') { i++; continue; }
            if (c == '[')
            {
                var end = code.IndexOf(']', i + 1);
                var bracket = code[(i + 1)..end];
                if (bracket.StartsWith('<') || bracket.StartsWith('>') || bracket.StartsWith('='))
                {
                    if (condition is not null) throw Unsupported();
                    var opLength = bracket.Length > 1 && bracket[1] is '=' or '>' ? 2 : 1;
                    condition = new(bracket[..opLength], ReadNumber(bracket[opLength..]));
                    if (condition.Operator is not ("<" or "<=" or ">" or ">=" or "=" or "<>"))
                        throw Unsupported();
                }
                else if (bracket.Length != 0 &&
                    bracket.All(ch => char.ToLowerInvariant(ch) is 'h' or 'm' or 's'))
                    tokens.Append('h'); // Elapsed time does not introduce a month.
                else if (bracket.Equals("$-F800", StringComparison.OrdinalIgnoreCase))
                    tokens.Append('d');
                i = end;
                continue;
            }
            if (code.AsSpan(i).StartsWith("AM/PM", StringComparison.OrdinalIgnoreCase)) { i += 4; continue; }
            if (code.AsSpan(i).StartsWith("A/P", StringComparison.OrdinalIgnoreCase)) { i += 2; continue; }
            tokens.Append(char.ToLowerInvariant(c));
        }
        return new(tokens.ToString(), condition);
    }

    private static bool ContainsDateTokens(string value)
    {
        if (value.Contains('y') || value.Contains('d')) return true;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != 'm') continue;
            var start = i;
            while (i + 1 < value.Length && value[i + 1] == 'm') i++;
            var previous = FindToken(value, start - 1, -1);
            var next = FindToken(value, i + 1, 1);
            if (previous != 'h' && next != 's') return true;
        }
        return false;
    }

    private static char? FindToken(string value, int index, int step)
    {
        while (index >= 0 && index < value.Length)
        {
            if (value[index] is 'y' or 'm' or 'd' or 'h' or 's') return value[index];
            index += step;
        }
        return null;
    }

    private static Valdrio.Domain.DomainRuleException Invalid() =>
        ImportRules.Error("IMP_XLSX_INVALID", "Import.XlsxInvalid");
    private static Valdrio.Domain.DomainRuleException Unsupported() =>
        ImportRules.Error("IMP_XLSX_FORMAT_UNSUPPORTED", "Import.XlsxFormatUnsupported");
}

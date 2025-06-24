using System.Text.RegularExpressions;

static partial class Patterns
{
    [GeneratedRegex(@"[^/\\]+$")]
    internal static partial Regex ReferenceSplitRegex();

    [GeneratedRegex(@"WHERE\s+(.+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    internal static partial Regex FilterMatchRegex();

    [GeneratedRegex(@"utilities\.reference\.(.+?)\.yml$", RegexOptions.IgnoreCase)]
    internal static partial Regex FileNameToReferenceRegex();
}
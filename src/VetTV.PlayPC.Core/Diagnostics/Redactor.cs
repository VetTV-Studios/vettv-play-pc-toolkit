using System.Text.RegularExpressions;

namespace VetTV.PlayPC.Core.Diagnostics;

/// <summary>Removes user names, profile paths, machine name and email addresses from text.</summary>
public sealed partial class Redactor
{
    private readonly string[] _secrets;

    public Redactor(IEnumerable<string?> secrets) =>
        _secrets = secrets.Where(s => !string.IsNullOrWhiteSpace(s) && s!.Length >= 2).Select(s => s!).Distinct()
            .OrderByDescending(s => s.Length).ToArray();

    public static Redactor ForCurrentUser() => new(new[] { Environment.UserName, Environment.MachineName, Environment.UserDomainName });

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)([A-Z]:\\Users\\)[^\\/\s""']+")]
    private static partial Regex ProfileRegex();

    public string Redact(string text)
    {
        text = EmailRegex().Replace(text, "<email>");
        text = ProfileRegex().Replace(text, "$1<user>");
        foreach (var s in _secrets)
            text = Regex.Replace(text, $@"(?<![A-Za-z0-9]){Regex.Escape(s)}(?![A-Za-z0-9])", "<redacted>", RegexOptions.IgnoreCase);
        return text;
    }
}

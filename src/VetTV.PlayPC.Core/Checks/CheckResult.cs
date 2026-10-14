namespace VetTV.PlayPC.Core.Checks;

public enum CheckStatus { Pass, Warn, Fail }

/// <summary>One diagnostic result. Every non-pass result carries a fix hint.</summary>
public sealed record CheckResult(string Id, string Category, string Title, CheckStatus Status, string Detail, string? FixHint = null)
{
    public static CheckResult Pass(string id, string cat, string title, string detail) => new(id, cat, title, CheckStatus.Pass, detail);
    public static CheckResult Warn(string id, string cat, string title, string detail, string fix) => new(id, cat, title, CheckStatus.Warn, detail, fix);
    public static CheckResult Fail(string id, string cat, string title, string detail, string fix) => new(id, cat, title, CheckStatus.Fail, detail, fix);
}

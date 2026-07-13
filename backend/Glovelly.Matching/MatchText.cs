namespace Glovelly.Matching;

public sealed record MatchText(
    string Original,
    string Canonical,
    string Compact,
    IReadOnlyList<string> Tokens);

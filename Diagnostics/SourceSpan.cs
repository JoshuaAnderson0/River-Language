namespace Diagnostics;

/// <summary>
/// Inclusive source range. Lines and columns are 1-based.
/// </summary>
public readonly struct SourceSpan
{
    public readonly int StartLine;
    public readonly int StartColumn;
    public readonly int EndLine;
    public readonly int EndColumn;

    public SourceSpan(int startLine, int startColumn, int endLine, int endColumn)
    {
        StartLine = startLine;
        StartColumn = startColumn;
        EndLine = endLine;
        EndColumn = endColumn;
    }

    public static SourceSpan Point(int line, int column) =>
        new(line, column, line, column);

    /// <summary>
    /// Smallest span covering both inputs.
    /// </summary>
    public SourceSpan Union(SourceSpan other)
    {
        (int startLine, int startColumn) = StartsBefore(this, other)
            ? (StartLine, StartColumn)
            : (other.StartLine, other.StartColumn);

        (int endLine, int endColumn) = EndsAfter(this, other)
            ? (EndLine, EndColumn)
            : (other.EndLine, other.EndColumn);

        return new SourceSpan(startLine, startColumn, endLine, endColumn);
    }

    private static bool StartsBefore(SourceSpan a, SourceSpan b) =>
        a.StartLine < b.StartLine || (a.StartLine == b.StartLine && a.StartColumn <= b.StartColumn);

    private static bool EndsAfter(SourceSpan a, SourceSpan b) =>
        a.EndLine > b.EndLine || (a.EndLine == b.EndLine && a.EndColumn >= b.EndColumn);

    public override string ToString() => $"{StartLine}:{StartColumn}";
}

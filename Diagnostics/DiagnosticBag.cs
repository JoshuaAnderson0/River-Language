namespace Diagnostics;

/// <summary>
/// Accumulates diagnostics across pipeline stages so a single compile surfaces every issue,
/// per the error handling section of DESIGN.md.
/// </summary>
public class DiagnosticBag
{
    public readonly List<Diagnostic> Items = [];

    public bool HasErrors => Items.Any(d => d.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic) => Items.Add(diagnostic);

    public void AddRange(IEnumerable<Diagnostic> diagnostics) => Items.AddRange(diagnostics);

    /// <summary>
    /// Converts accumulated state into a stage result: errors poison the pipeline,
    /// warnings ride along in the bag.
    /// </summary>
    public Result<TValue> ToResult<TValue>(TValue value) =>
        HasErrors
            ? Result<TValue>.Error($"{Items.Count(d => d.Severity == DiagnosticSeverity.Error)} error(s) reported")
            : Result<TValue>.Ok(value);
}

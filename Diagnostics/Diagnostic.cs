namespace Diagnostics;

public enum DiagnosticSeverity
{
    Warning,
    Error
}

public class Diagnostic
{
    public required DiagnosticSeverity Severity;
    public required string Message;
    public string? FilePath;
    public SourceSpan? Span;
    public string? Note;

    public static Diagnostic Error(string message) =>
        new() { Severity = DiagnosticSeverity.Error, Message = message };

    public static Diagnostic Error(string message, string filePath, SourceSpan span) =>
        new() { Severity = DiagnosticSeverity.Error, Message = message, FilePath = filePath, Span = span };

    public static Diagnostic Warning(string message) =>
        new() { Severity = DiagnosticSeverity.Warning, Message = message };

    public static Diagnostic Warning(string message, string filePath, SourceSpan span) =>
        new() { Severity = DiagnosticSeverity.Warning, Message = message, FilePath = filePath, Span = span };

    public Diagnostic WithNote(string note)
    {
        Note = note;
        return this;
    }
}

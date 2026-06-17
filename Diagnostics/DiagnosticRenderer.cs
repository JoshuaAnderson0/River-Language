using System.Text;

namespace Diagnostics;

/// <summary>
/// Renders diagnostics in Rust style:
///
/// error: expected expression
///  --> main.script:3:10
///   |
/// 3 | x := 1 + )
///   |          ^
///   = note: ...
/// </summary>
public static class DiagnosticRenderer
{
    public static string Render(Diagnostic diagnostic, SourceFile? sourceFile)
    {
        StringBuilder builder = new();

        string severity = diagnostic.Severity == DiagnosticSeverity.Error ? "error" : "warning";
        builder.AppendLine($"{severity}: {diagnostic.Message}");

        if (diagnostic is { FilePath: not null, Span: not null })
        {
            SourceSpan span = diagnostic.Span.Value;
            builder.AppendLine($" --> {diagnostic.FilePath}:{span.StartLine}:{span.StartColumn}");

            if (sourceFile is not null)
            {
                RenderSourceLine(builder, sourceFile, span);
            }
        }

        if (diagnostic.Note is not null)
        {
            builder.AppendLine($"  = note: {diagnostic.Note}");
        }

        return builder.ToString();
    }

    public static string RenderAll(DiagnosticBag bag, Func<string, SourceFile?> sourceLookup)
    {
        StringBuilder builder = new();

        foreach (Diagnostic diagnostic in bag.Items)
        {
            SourceFile? sourceFile = diagnostic.FilePath is not null
                ? sourceLookup(diagnostic.FilePath)
                : null;

            builder.AppendLine(Render(diagnostic, sourceFile));
        }

        return builder.ToString();
    }

    private static void RenderSourceLine(StringBuilder builder, SourceFile sourceFile, SourceSpan span)
    {
        string lineText = sourceFile.GetLine(span.StartLine);
        string lineNumber = span.StartLine.ToString();
        string gutter = new(' ', lineNumber.Length);

        int underlineStart = span.StartColumn;
        int underlineEnd = span.EndLine == span.StartLine
            ? Math.Max(span.EndColumn, span.StartColumn)
            : Math.Max(lineText.Length, span.StartColumn);

        builder.AppendLine($"{gutter} |");
        builder.AppendLine($"{lineNumber} | {lineText}");
        builder.AppendLine($"{gutter} | {new string(' ', underlineStart - 1)}{new string('^', underlineEnd - underlineStart + 1)}");
    }
}

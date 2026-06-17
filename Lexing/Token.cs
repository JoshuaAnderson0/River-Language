namespace Lexing;

public class Token
{
    /// <summary>
    /// Terminal name as known to the grammar: a literal's text (":=", "print") or a
    /// builtin class name ("NUMBER", "FLOAT", "IDENTIFIER", "VERSION", "NEWLINE").
    /// </summary>
    public required string TerminalName;
    public required string Text;
    public SourceSpan Span;

    public override string ToString() => $"{TerminalName}('{Text}') at {Span}";
}

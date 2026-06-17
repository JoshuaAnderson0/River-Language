using Lexing;

namespace Parsing;

/// <summary>
/// Generic parse tree node shaped by grammar capture annotations. Leaves wrap a captured
/// terminal token; interior nodes carry named fields. Pass-through productions never
/// appear in the tree (EXPR wrappers and parens vanish).
/// </summary>
public sealed class SyntaxNode
{
    public required string Atom;

    /// <summary>Non-null exactly when this is a captured-terminal leaf.</summary>
    public Token? Token;

    public Dictionary<string, SyntaxChild> Fields = [];
    public SourceSpan Span;

    public SyntaxNode? Single(string field) =>
        Fields.TryGetValue(field, out SyntaxChild? child) ? child.Single : null;

    public List<SyntaxNode> List(string field) =>
        Fields.TryGetValue(field, out SyntaxChild? child) ? child.List ?? [] : [];

    public string TokenText => Token?.Text ?? "";

    public override string ToString() => Token is not null ? $"{Atom}({Token.Text})" : Atom;
}

/// <summary>Tagged single/list union; exactly one side is non-null.</summary>
public sealed class SyntaxChild
{
    public SyntaxNode? Single;
    public List<SyntaxNode>? List;
}

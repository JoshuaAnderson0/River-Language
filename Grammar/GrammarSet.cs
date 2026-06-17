namespace Grammar;

public enum BuiltinClass
{
    None,
    Number,
    Float,
    Identifier,
    Version,
    Newline
}

public class TerminalInfo
{
    public required string Name;
    public required bool IsLiteral;
    public BuiltinClass Builtin = BuiltinClass.None;
    public int PrecedenceLevel = -1;
    public Associativity Assoc = Associativity.Unspecified;

    /// <summary>
    /// Literal terminals that look like identifiers (e.g. `print`, `using`) are lexed via
    /// the keyword check; symbol literals (e.g. `:=`) are matched longest-first.
    /// </summary>
    public bool IsIdentifierLike =>
        IsLiteral && Name.Length > 0 && (char.IsLetter(Name[0]) || Name[0] == '_');
}

/// <summary>
/// How the parser builds a value when it reduces a production. There are no interfaces in
/// this codebase, so the action is a tag the parser runtime switches on.
/// </summary>
public enum ProductionActionKind
{
    /// <summary>Build a SyntaxNode with the production's atom name and captured fields.</summary>
    Construct,

    /// <summary>The value of the single unaliased capture flows up unchanged (EXPR, PAREN...).</summary>
    PassThrough,

    /// <summary>Synthetic optional matched nothing: the value is absent.</summary>
    EmptyOptional,

    /// <summary>Synthetic repetition base case: the value is an empty list.</summary>
    EmptyList,

    /// <summary>Synthetic one-or-more base case: a fresh list containing the item.</summary>
    NewList,

    /// <summary>Synthetic repetition step: append the item to the inherited list.</summary>
    AppendList
}

public class ProductionSymbol
{
    public required string Name;
    public required bool IsTerminal;

    /// <summary>Null means the value of this symbol is discarded ($NAME or a literal).</summary>
    public string? FieldName;
}

public class Production
{
    public int Id;
    public required string Lhs;
    public required List<ProductionSymbol> Rhs;
    public ProductionActionKind Action = ProductionActionKind.Construct;

    /// <summary>Index of the value-carrying RHS symbol for PassThrough/NewList/AppendList.</summary>
    public int ValueIndex;

    public int PrecedenceLevel = -1;
    public Associativity Assoc = Associativity.Unspecified;
    public string FilePath = "";
    public SourceSpan Span;

    public override string ToString() =>
        $"{Lhs} := {(Rhs.Count == 0 ? "ε" : string.Join(" ", Rhs.Select(s => s.Name)))}";
}

/// <summary>
/// The desugared BNF grammar: the set union of all productions from every loaded .grammar
/// file, plus the terminal table the lexer is driven by.
/// </summary>
public class GrammarSet
{
    public required List<Production> Productions;
    public required Dictionary<string, TerminalInfo> Terminals;
    public required HashSet<string> Nonterminals;

    public IEnumerable<Production> ProductionsOf(string atom) =>
        Productions.Where(p => p.Lhs == atom);
}

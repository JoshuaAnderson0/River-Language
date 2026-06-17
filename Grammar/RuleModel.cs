namespace Grammar;

public enum Associativity
{
    Unspecified,
    Left,
    Right,
    NonAssoc
}

public enum ElementQuantifier
{
    One,
    Optional,
    ZeroOrMore,
    OneOrMore
}

public enum ElementKind
{
    Reference,
    LiteralText,
    Group
}

public enum ReferenceCapture
{
    Discard,
    Capture
}

/// <summary>
/// One element of a rule's right-hand side as written in a .grammar file:
/// $NAME (discard), @NAME (capture), @NAME:alias, a bare/quoted literal, or a (group).
/// </summary>
public class RuleElement
{
    public required ElementKind Kind;
    public string? Name;
    public ReferenceCapture Capture = ReferenceCapture.Discard;
    public string? Alias;
    public string? Literal;
    public List<List<RuleElement>>? GroupAlternatives;
    public ElementQuantifier Quantifier = ElementQuantifier.One;
    public SourceSpan Span;
}

/// <summary>
/// One rule line from a .grammar file. Multiple lines for the same atom and inline
/// alternation both contribute productions to that atom.
/// </summary>
public class GrammarRule
{
    public required string AtomName;
    public required List<List<RuleElement>> Alternatives;
    public required string FilePath;
    public int PrecedenceLevel = -1;
    public Associativity Assoc = Associativity.Unspecified;
    public SourceSpan Span;
}

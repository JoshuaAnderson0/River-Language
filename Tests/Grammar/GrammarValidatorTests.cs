using Grammar;

namespace Tests.Grammar;

public class GrammarValidatorTests
{
    private static DiagnosticBag Validate(string source, string startSymbol)
    {
        DiagnosticBag bag = new();

        MetaParser
            .Run(new SourceFile("test.grammar", source), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => GrammarValidator.Run(grammarSet, startSymbol, bag));

        return bag;
    }

    [Fact]
    public void UnreachableRuleIsWarned()
    {
        DiagnosticBag bag = Validate("""
            PROGRAM := @NUMBER
            ORPHAN := @FLOAT
            """, "PROGRAM");

        Diagnostic warning = Assert.Single(bag.Items);
        Assert.Equal(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains("ORPHAN", warning.Message);
    }

    [Fact]
    public void MissingStartSymbolIsAnError()
    {
        DiagnosticBag bag = Validate("A := @NUMBER", "PROGRAM");

        Assert.Contains(bag.Items, d => d.Message.Contains("start symbol 'PROGRAM'"));
    }

    [Fact]
    public void TerminalsInheritPrecedenceFromProductions()
    {
        DiagnosticBag bag = new();

        Result<GrammarSet> result = MetaParser
            .Run(new SourceFile("test.grammar", """
                EXPR := @EXPR:lhs '+' @EXPR:rhs %left %1
                EXPR := @EXPR:lhs '*' @EXPR:rhs %left %2
                EXPR := @NUMBER
                """), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => GrammarValidator.Run(grammarSet, "EXPR", bag));

        GrammarSet grammarSet = result.Unwrap();
        Assert.Equal(1, grammarSet.Terminals["+"].PrecedenceLevel);
        Assert.Equal(2, grammarSet.Terminals["*"].PrecedenceLevel);
        Assert.Equal(Associativity.Left, grammarSet.Terminals["+"].Assoc);
    }

    [Fact]
    public void ConflictingTerminalPrecedenceIsAnError()
    {
        DiagnosticBag bag = Validate("""
            A := @NUMBER '+' @NUMBER %1
            B := @FLOAT '+' @FLOAT %2
            PROGRAM := @A @B
            """, "PROGRAM");

        Assert.Contains(bag.Items, d => d.Message.Contains("conflicting precedence for terminal '+'"));
    }
}

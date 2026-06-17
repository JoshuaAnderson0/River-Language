using Grammar;

namespace Tests.Grammar;

public class DesugarerTests
{
    private static GrammarSet Desugar(string source)
    {
        DiagnosticBag bag = new();

        Result<GrammarSet> result = MetaParser
            .Run(new SourceFile("test.grammar", source), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag));

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return result.Unwrap();
    }

    [Fact]
    public void ZeroOrMoreBecomesLeftRecursiveSyntheticProductions()
    {
        GrammarSet grammarSet = Desugar("""
            PROGRAM := @STATEMENT*
            STATEMENT := @NUMBER
            """);

        Production parent = Assert.Single(grammarSet.ProductionsOf("PROGRAM"));
        ProductionSymbol repSymbol = Assert.Single(parent.Rhs);
        Assert.Equal("PROGRAM#rep0", repSymbol.Name);
        Assert.Equal("statement", repSymbol.FieldName);
        Assert.Equal(ProductionActionKind.Construct, parent.Action);

        List<Production> synthetic = grammarSet.ProductionsOf("PROGRAM#rep0").ToList();
        Assert.Equal(2, synthetic.Count);
        Assert.Equal(ProductionActionKind.EmptyList, synthetic[0].Action);
        Assert.Empty(synthetic[0].Rhs);
        Assert.Equal(ProductionActionKind.AppendList, synthetic[1].Action);
        Assert.Equal(["PROGRAM#rep0", "STATEMENT"], synthetic[1].Rhs.Select(s => s.Name));
        Assert.Equal(1, synthetic[1].ValueIndex);
    }

    [Fact]
    public void OptionalBecomesEmptyOrPassThrough()
    {
        GrammarSet grammarSet = Desugar("""
            USING := using @IDENTIFIER:name @VERSION:version? $NEWLINE
            """);

        Production parent = Assert.Single(grammarSet.ProductionsOf("USING"));
        Assert.Equal("USING#opt0", parent.Rhs[2].Name);
        Assert.Equal("version", parent.Rhs[2].FieldName);

        List<Production> synthetic = grammarSet.ProductionsOf("USING#opt0").ToList();
        Assert.Equal(ProductionActionKind.EmptyOptional, synthetic[0].Action);
        Assert.Equal(ProductionActionKind.PassThrough, synthetic[1].Action);
    }

    [Fact]
    public void AutoNameCollisionsGetPositionalSuffixes()
    {
        GrammarSet grammarSet = Desugar("""
            ADD := @EXPR '+' @EXPR
            EXPR := @NUMBER
            """);

        Production add = Assert.Single(grammarSet.ProductionsOf("ADD"));
        Assert.Equal("expr0", add.Rhs[0].FieldName);
        Assert.Null(add.Rhs[1].FieldName);
        Assert.Equal("expr1", add.Rhs[2].FieldName);
        Assert.Equal(ProductionActionKind.Construct, add.Action);
    }

    [Fact]
    public void SingleUnaliasedCapturePassesThrough()
    {
        GrammarSet grammarSet = Desugar("""
            PAREN := '(' @EXPR ')'
            EXPR := @NUMBER
            """);

        Production paren = Assert.Single(grammarSet.ProductionsOf("PAREN"));
        Assert.Equal(ProductionActionKind.PassThrough, paren.Action);
        Assert.Equal(1, paren.ValueIndex);

        Production expr = Assert.Single(grammarSet.ProductionsOf("EXPR"));
        Assert.Equal(ProductionActionKind.PassThrough, expr.Action);
    }

    [Fact]
    public void AliasedSingleCaptureConstructs()
    {
        GrammarSet grammarSet = Desugar("""
            PRINT := print '(' @EXPR:value ')' $NEWLINE
            EXPR := @NUMBER
            """);

        Production print = Assert.Single(grammarSet.ProductionsOf("PRINT"));
        Assert.Equal(ProductionActionKind.Construct, print.Action);
        Assert.Equal("value", print.Rhs[2].FieldName);
    }

    [Fact]
    public void LiteralsAndBuiltinsBecomeTerminals()
    {
        GrammarSet grammarSet = Desugar("BINDING := @IDENTIFIER:name ':=' @NUMBER $NEWLINE");

        Assert.True(grammarSet.Terminals[":="].IsLiteral);
        Assert.False(grammarSet.Terminals[":="].IsIdentifierLike);
        Assert.Equal(BuiltinClass.Identifier, grammarSet.Terminals["IDENTIFIER"].Builtin);
        Assert.Equal(BuiltinClass.Newline, grammarSet.Terminals["NEWLINE"].Builtin);
    }

    [Fact]
    public void UndefinedSymbolIsReported()
    {
        DiagnosticBag bag = new();

        MetaParser
            .Run(new SourceFile("test.grammar", "X := @NOPE"), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag));

        Assert.Contains(bag.Items, d => d.Message.Contains("undefined symbol 'NOPE'"));
    }

    [Fact]
    public void CapturesInsideGroupsAreRejected()
    {
        DiagnosticBag bag = new();

        MetaParser
            .Run(new SourceFile("test.grammar", "X := (@A)?\nA := @NUMBER"), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag));

        Assert.Contains(bag.Items, d => d.Message.Contains("captures inside groups"));
    }

    [Fact]
    public void DuplicateAliasIsAnError()
    {
        DiagnosticBag bag = new();

        MetaParser
            .Run(new SourceFile("test.grammar", "X := @NUMBER:n @NUMBER:n"), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag));

        Assert.Contains(bag.Items, d => d.Message.Contains("duplicate field name 'n'"));
    }
}

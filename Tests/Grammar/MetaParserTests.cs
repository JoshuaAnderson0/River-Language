using Grammar;

namespace Tests.Grammar;

public class MetaParserTests
{
    private static List<GrammarRule> Parse(string source)
    {
        DiagnosticBag bag = new();
        Result<List<GrammarRule>> result = MetaParser.Run(new SourceFile("test.grammar", source), bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return result.Unwrap();
    }

    private static DiagnosticBag ParseExpectingErrors(string source)
    {
        DiagnosticBag bag = new();
        MetaParser.Run(new SourceFile("test.grammar", source), bag);

        Assert.True(bag.HasErrors);
        return bag;
    }

    [Fact]
    public void ParsesDiscardAndCaptureReferences()
    {
        List<GrammarRule> rules = Parse("BINDING := @IDENTIFIER:name ':=' @EXPR:value $NEWLINE");

        GrammarRule rule = Assert.Single(rules);
        Assert.Equal("BINDING", rule.AtomName);

        List<RuleElement> sequence = Assert.Single(rule.Alternatives);
        Assert.Equal(4, sequence.Count);

        Assert.Equal(ReferenceCapture.Capture, sequence[0].Capture);
        Assert.Equal("IDENTIFIER", sequence[0].Name);
        Assert.Equal("name", sequence[0].Alias);

        Assert.Equal(ElementKind.LiteralText, sequence[1].Kind);
        Assert.Equal(":=", sequence[1].Literal);

        Assert.Equal(ReferenceCapture.Discard, sequence[3].Capture);
        Assert.Equal("NEWLINE", sequence[3].Name);
    }

    [Fact]
    public void ParsesQuantifiersAndGroups()
    {
        List<GrammarRule> rules = Parse("X := @A? @B* @C+ ($D $E)?");

        List<RuleElement> sequence = Assert.Single(rules[0].Alternatives);
        Assert.Equal(ElementQuantifier.Optional, sequence[0].Quantifier);
        Assert.Equal(ElementQuantifier.ZeroOrMore, sequence[1].Quantifier);
        Assert.Equal(ElementQuantifier.OneOrMore, sequence[2].Quantifier);
        Assert.Equal(ElementKind.Group, sequence[3].Kind);
        Assert.Equal(ElementQuantifier.Optional, sequence[3].Quantifier);
        Assert.Equal(2, Assert.Single(sequence[3].GroupAlternatives!).Count);
    }

    [Fact]
    public void ParsesInlineAlternation()
    {
        List<GrammarRule> rules = Parse("STATEMENT := @BINDING | @PRINT");

        Assert.Equal(2, rules[0].Alternatives.Count);
    }

    [Fact]
    public void ParsesPrecedenceAnnotations()
    {
        List<GrammarRule> rules = Parse("ADD := @EXPR:lhs '+' @EXPR:rhs   %left %1");

        Assert.Equal(1, rules[0].PrecedenceLevel);
        Assert.Equal(Associativity.Left, rules[0].Assoc);
    }

    [Fact]
    public void SkipsCommentsAndBlankLines()
    {
        List<GrammarRule> rules = Parse("""
            // header comment

            A := @B   // trailing comment
            """);

        Assert.Single(rules);
        Assert.Single(Assert.Single(rules[0].Alternatives));
    }

    [Fact]
    public void BareLiteralsMustBeIdentifierLike()
    {
        DiagnosticBag bag = ParseExpectingErrors("ADD := @EXPR + @EXPR");

        Assert.Contains(bag.Items, d => d.Message.Contains("must be quoted"));
    }

    [Fact]
    public void ReportsUnterminatedQuote()
    {
        DiagnosticBag bag = ParseExpectingErrors("X := 'abc");

        Assert.Contains(bag.Items, d => d.Message.Contains("unterminated"));
    }

    [Fact]
    public void ReportsUnknownAnnotation()
    {
        DiagnosticBag bag = ParseExpectingErrors("X := @A %sideways");

        Assert.Contains(bag.Items, d => d.Message.Contains("%sideways"));
    }

    [Fact]
    public void ReportsEmptyAlternative()
    {
        DiagnosticBag bag = ParseExpectingErrors("X := @A | ");

        Assert.Contains(bag.Items, d => d.Message.Contains("empty"));
    }
}

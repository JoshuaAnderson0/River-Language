using Parsing;

namespace Tests.Parsing;

public class ParserRunnerTests
{
    [Fact]
    public void ParsesBindingIntoNamedFields()
    {
        SyntaxNode program = ParserTestHarness.ParseOk("x := 42");

        SyntaxNode binding = Assert.Single(program.List("statement"));
        Assert.Equal("BINDING", binding.Atom);
        Assert.Equal("x", binding.Single("name")!.TokenText);
        Assert.Equal("NUMBER", binding.Single("value")!.Atom);
        Assert.Equal("42", binding.Single("value")!.TokenText);
    }

    [Fact]
    public void PrecedenceShapesTheTree()
    {
        // 1 + 2 * 3 parses as ADD(1, MUL(2, 3)).
        SyntaxNode program = ParserTestHarness.ParseOk("x := 1 + 2 * 3");
        SyntaxNode value = program.List("statement")[0].Single("value")!;

        Assert.Equal("ADD", value.Atom);
        Assert.Equal("1", value.Single("lhs")!.TokenText);
        Assert.Equal("MUL", value.Single("rhs")!.Atom);

        // 1 * 2 + 3 parses as ADD(MUL(1, 2), 3).
        SyntaxNode flipped = ParserTestHarness.ParseOk("x := 1 * 2 + 3")
            .List("statement")[0].Single("value")!;

        Assert.Equal("ADD", flipped.Atom);
        Assert.Equal("MUL", flipped.Single("lhs")!.Atom);
        Assert.Equal("3", flipped.Single("rhs")!.TokenText);
    }

    [Fact]
    public void LeftAssociativityNestsLeft()
    {
        // 1 - 2 - 3 parses as SUB(SUB(1, 2), 3).
        SyntaxNode value = ParserTestHarness.ParseOk("x := 1 - 2 - 3")
            .List("statement")[0].Single("value")!;

        Assert.Equal("SUB", value.Atom);
        Assert.Equal("SUB", value.Single("lhs")!.Atom);
        Assert.Equal("3", value.Single("rhs")!.TokenText);
        Assert.Equal("1", value.Single("lhs")!.Single("lhs")!.TokenText);
    }

    [Fact]
    public void ParensOverridePrecedenceAndVanish()
    {
        // (1 + 2) * 3: the PAREN wrapper passes through, leaving MUL(ADD(1, 2), 3).
        SyntaxNode value = ParserTestHarness.ParseOk("x := (1 + 2) * 3")
            .List("statement")[0].Single("value")!;

        Assert.Equal("MUL", value.Atom);
        Assert.Equal("ADD", value.Single("lhs")!.Atom);
    }

    [Fact]
    public void MultipleStatementsAccumulateInOrder()
    {
        SyntaxNode program = ParserTestHarness.ParseOk("""
            x := 1
            y := 2.5
            print(x)
            """);

        List<SyntaxNode> statements = program.List("statement");
        Assert.Equal(3, statements.Count);
        Assert.Equal("BINDING", statements[0].Atom);
        Assert.Equal("FLOAT", statements[1].Single("value")!.Atom);
        Assert.Equal("PRINT", statements[2].Atom);
        Assert.Equal("IDENTIFIER", statements[2].Single("value")!.Atom);
    }

    [Fact]
    public void EmptyProgramParsesToEmptyList()
    {
        SyntaxNode program = ParserTestHarness.ParseOk("// nothing here\n");

        Assert.Equal("PROGRAM", program.Atom);
        Assert.Empty(program.List("statement"));
    }

    [Fact]
    public void OptionalFieldsAreOmittedWhenAbsent()
    {
        const string buildGrammar = """
            BUILD := @PACKAGE? @USING*
            PACKAGE := package @IDENTIFIER:name $NEWLINE
            USING := using @IDENTIFIER:name @VERSION:version? $NEWLINE
            """;

        SyntaxNode withVersion = ParserTestHarness.ParseOk("using Math v1.0", buildGrammar, "BUILD");
        SyntaxNode usingNode = Assert.Single(withVersion.List("using"));
        Assert.Equal("v1.0", usingNode.Single("version")!.TokenText);
        Assert.Null(withVersion.Single("package"));

        SyntaxNode withoutVersion = ParserTestHarness.ParseOk("package Demo\nusing Math", buildGrammar, "BUILD");
        Assert.Equal("Demo", withoutVersion.Single("package")!.Single("name")!.TokenText);
        Assert.Null(Assert.Single(withoutVersion.List("using")).Single("version"));
    }

    [Fact]
    public void SyntaxErrorListsExpectedTerminals()
    {
        DiagnosticBag bag = new();
        Result<SyntaxNode> result = ParserTestHarness.Parse(
            ParserTestHarness.SliceGrammar, "PROGRAM", "x := + 1", bag);

        Assert.False(result.IsOk);
        Diagnostic error = Assert.Single(bag.Items, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("unexpected '+'", error.Message);
        Assert.Contains("expected", error.Message);
    }

    [Fact]
    public void NodeSpansCoverTheirSource()
    {
        SyntaxNode program = ParserTestHarness.ParseOk("x := 1 + 23");
        SyntaxNode add = program.List("statement")[0].Single("value")!;

        Assert.Equal(6, add.Span.StartColumn);
        Assert.Equal(11, add.Span.EndColumn);
    }
}

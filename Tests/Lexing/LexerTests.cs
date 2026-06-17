using Grammar;
using Lexing;

namespace Tests.Lexing;

public class LexerTests
{
    private const string SliceGrammar = """
        PROGRAM := @STATEMENT*
        STATEMENT := @BINDING
        STATEMENT := @PRINT
        BINDING := @IDENTIFIER:name ':=' @EXPR:value $NEWLINE
        PRINT := print '(' @EXPR:value ')' $NEWLINE
        EXPR := @NUMBER
        EXPR := @FLOAT
        EXPR := @IDENTIFIER
        ADD := @EXPR:lhs '+' @EXPR:rhs %left %1
        """;

    private static LexerTables Tables(string grammarSource = SliceGrammar)
    {
        DiagnosticBag bag = new();

        Result<GrammarSet> grammarSet = MetaParser
            .Run(new SourceFile("test.grammar", grammarSource), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag));

        Assert.True(grammarSet.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return LexerTables.FromGrammar(grammarSet.Unwrap());
    }

    private static List<Token> Lex(string source, LexerTables? tables = null)
    {
        DiagnosticBag bag = new();
        Result<List<Token>> result = LexerRunner.Run(new SourceFile("main.script", source), tables ?? Tables(), bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return result.Unwrap();
    }

    private static List<string> Names(string source) => Lex(source).Select(t => t.TerminalName).ToList();

    [Fact]
    public void MaxMunchPrefersLongerSymbol()
    {
        Assert.Equal(["IDENTIFIER", ":=", "NUMBER", "NEWLINE"], Names("x := 1"));
    }

    [Fact]
    public void KeywordCheckAfterFullIdentifierScan()
    {
        Assert.Equal(["print", "(", "NUMBER", ")", "NEWLINE"], Names("print(1)"));
        Assert.Equal(["IDENTIFIER", ":=", "NUMBER", "NEWLINE"], Names("printx := 1"));
    }

    [Fact]
    public void FloatVersusNumber()
    {
        Assert.Equal(["NUMBER"], Lex("1 + 2").Where(t => t.Text == "1").Select(t => t.TerminalName));
        Assert.Equal(["FLOAT", "NEWLINE"], Names("1.5"));
        Assert.Equal(["NUMBER", "FLOAT", "NEWLINE"], Names("1 2.25"));
    }

    [Fact]
    public void CommentsAreSkipped()
    {
        Assert.Equal(["IDENTIFIER", ":=", "NUMBER", "NEWLINE"], Names("x := 1 // comment"));
    }

    [Fact]
    public void NewlinesCollapseAndInject()
    {
        // Leading newlines suppressed, runs collapsed, final newline injected.
        Assert.Equal(
            ["IDENTIFIER", ":=", "NUMBER", "NEWLINE", "IDENTIFIER", ":=", "NUMBER", "NEWLINE"],
            Names("\n\nx := 1\n\n\ny := 2"));
    }

    [Fact]
    public void EmptySourceYieldsNoTokens()
    {
        Assert.Empty(Lex("  \n // only a comment \n"));
    }

    [Fact]
    public void VersionOnlyWhenGrammarReferencesIt()
    {
        LexerTables buildTables = Tables("""
            BUILD := @PACKAGE? @USING*
            PACKAGE := package @IDENTIFIER:name $NEWLINE
            USING := using @IDENTIFIER:name @VERSION:version? $NEWLINE
            """);

        List<Token> tokens = Lex("using Math v1.0", buildTables);
        Assert.Equal(["using", "IDENTIFIER", "VERSION", "NEWLINE"], tokens.Select(t => t.TerminalName));
        Assert.Equal("v1.0", tokens[2].Text);

        // Script grammar has no VERSION: v1 is an identifier, and v1.0 won't lex as one token.
        Assert.Equal(["IDENTIFIER", ":=", "IDENTIFIER", "NEWLINE"], Names("x := v1"));
    }

    [Fact]
    public void VersionRequiresDotAndDigits()
    {
        LexerTables buildTables = Tables("""
            USING := using @IDENTIFIER:name @VERSION:version? $NEWLINE
            """);

        // 'v1' and 'value' are identifiers, not versions.
        Assert.Equal(
            ["using", "IDENTIFIER", "IDENTIFIER", "NEWLINE"],
            Lex("using v1 value", buildTables).Select(t => t.TerminalName));
    }

    [Fact]
    public void SpansAreOneBasedInclusive()
    {
        List<Token> tokens = Lex("x := 10\nprint(x)");

        Token number = tokens.First(t => t.TerminalName == "NUMBER");
        Assert.Equal(1, number.Span.StartLine);
        Assert.Equal(6, number.Span.StartColumn);
        Assert.Equal(7, number.Span.EndColumn);

        Token printKeyword = tokens.First(t => t.TerminalName == "print");
        Assert.Equal(2, printKeyword.Span.StartLine);
        Assert.Equal(1, printKeyword.Span.StartColumn);
    }

    [Fact]
    public void UnexpectedCharacterAccumulatesAndContinues()
    {
        DiagnosticBag bag = new();
        Result<List<Token>> result = LexerRunner.Run(new SourceFile("main.script", "x := 1 ; y := 2 ;"), Tables(), bag);

        Assert.False(result.IsOk);
        Assert.Equal(2, bag.Items.Count(d => d.Message.Contains("unexpected character ';'")));
    }
}

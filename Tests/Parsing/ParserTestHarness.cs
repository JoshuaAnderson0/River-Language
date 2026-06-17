using Grammar;
using Lexing;
using Parsing;

namespace Tests.Parsing;

/// <summary>Shared helper: grammar source + script source -> parse tree (or diagnostics).</summary>
public static class ParserTestHarness
{
    public const string SliceGrammar = """
        PROGRAM := @STATEMENT*

        STATEMENT := @BINDING
        STATEMENT := @PRINT

        BINDING := @IDENTIFIER:name ':=' @EXPR:value $NEWLINE
        PRINT   := print '(' @EXPR:value ')' $NEWLINE

        EXPR := @ADD
        EXPR := @SUB
        EXPR := @MUL
        EXPR := @DIV
        EXPR := @PAREN
        EXPR := @NUMBER
        EXPR := @FLOAT
        EXPR := @IDENTIFIER

        ADD := @EXPR:lhs '+' @EXPR:rhs   %left %1
        SUB := @EXPR:lhs '-' @EXPR:rhs   %left %1
        MUL := @EXPR:lhs '*' @EXPR:rhs   %left %2
        DIV := @EXPR:lhs '/' @EXPR:rhs   %left %2

        PAREN := '(' @EXPR ')'
        """;

    public static Result<ParseTable> BuildTable(string grammarSource, string startSymbol, DiagnosticBag bag) =>
        MetaParser
            .Run(new SourceFile("test.grammar", grammarSource), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => GrammarValidator.Run(grammarSet, startSymbol, bag))
            .FlatMap(grammarSet => LalrBuilder.Run(grammarSet, startSymbol, bag));

    public static Result<SyntaxNode> Parse(string grammarSource, string startSymbol, string source, DiagnosticBag bag)
    {
        SourceFile file = new("main.script", source);

        return MetaParser
            .Run(new SourceFile("test.grammar", grammarSource), bag)
            .FlatMap(rules => Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => GrammarValidator.Run(grammarSet, startSymbol, bag))
            .FlatMap(grammarSet => LalrBuilder
                .Run(grammarSet, startSymbol, bag)
                .FlatMap(table => LexerRunner
                    .Run(file, LexerTables.FromGrammar(grammarSet), bag)
                    .FlatMap(tokens => ParserRunner.Run(tokens, table, file, bag))));
    }

    public static SyntaxNode ParseOk(string source, string grammarSource = SliceGrammar, string startSymbol = "PROGRAM")
    {
        DiagnosticBag bag = new();
        Result<SyntaxNode> result = Parse(grammarSource, startSymbol, source, bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return result.Unwrap();
    }
}

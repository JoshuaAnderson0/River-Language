using Parsing;

namespace Tests.Parsing;

public class LalrBuilderTests
{
    [Fact]
    public void SliceGrammarBuildsWithoutConflicts()
    {
        DiagnosticBag bag = new();
        Result<ParseTable> result = ParserTestHarness.BuildTable(ParserTestHarness.SliceGrammar, "PROGRAM", bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        Assert.DoesNotContain(bag.Items, d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildGrammarBuildsWithoutConflicts()
    {
        DiagnosticBag bag = new();

        Result<ParseTable> result = ParserTestHarness.BuildTable("""
            BUILD := @PACKAGE? @USING*
            PACKAGE := package @IDENTIFIER:name $NEWLINE
            USING := using @IDENTIFIER:name @VERSION:version? $NEWLINE
            """, "BUILD", bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
    }

    [Fact]
    public void DesignDocExprGrammarBuilds()
    {
        // The EXPR example from DESIGN.md, written in the implemented meta-syntax.
        DiagnosticBag bag = new();

        Result<ParseTable> result = ParserTestHarness.BuildTable("""
            EXPR := @EXPR '+' @EXPR   %left %1
            EXPR := @EXPR '-' @EXPR   %left %1
            EXPR := @EXPR '*' @EXPR   %left %2
            EXPR := @EXPR '/' @EXPR   %left %2
            EXPR := @NUMBER
            EXPR := '(' @EXPR ')'
            """, "EXPR", bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
    }

    [Fact]
    public void MissingPrecedenceReportsShiftReduceConflict()
    {
        DiagnosticBag bag = new();

        ParserTestHarness.BuildTable("""
            EXPR := @EXPR '+' @EXPR
            EXPR := @NUMBER
            """, "EXPR", bag);

        Diagnostic conflict = Assert.Single(bag.Items, d => d.Severity == DiagnosticSeverity.Error);
        Assert.Contains("shift-reduce conflict on '+'", conflict.Message);
        Assert.Contains("conflicting item set", conflict.Note!);
    }

    [Fact]
    public void ReduceReduceConflictIsReported()
    {
        DiagnosticBag bag = new();

        ParserTestHarness.BuildTable("""
            PROGRAM := @A
            PROGRAM := @B
            A := @NUMBER
            B := @NUMBER
            """, "PROGRAM", bag);

        Assert.Contains(bag.Items, d => d.Message.Contains("reduce-reduce conflict"));
    }
}

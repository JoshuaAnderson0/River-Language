using Ir;
using Tests.Parsing;

namespace Tests.Ir;

public class IrGeneratorTests
{
    private static IrProgram Generate(string source)
    {
        DiagnosticBag bag = new();

        Result<IrProgram> result = IrGenerator.Run(ParserTestHarness.ParseOk(source), "main.script", bag);

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)));
        return result.Unwrap();
    }

    private static List<string> Render(string source) =>
        Generate(source).Ops.Select(op => op.ToString()).ToList();

    [Fact]
    public void BindingStoresIntoSlot()
    {
        Assert.Equal(
            ["PushInt 7", "StoreLocal 0", "Exit"],
            Render("x := 7"));
    }

    [Fact]
    public void PrintLoadsAndPrintsTyped()
    {
        Assert.Equal(
            ["PushFloat 1.5", "StoreLocal 0", "LoadLocal 0", "Print.F64", "Exit"],
            Render("f := 1.5\nprint(f)"));
    }

    [Fact]
    public void IntArithmeticStaysTyped()
    {
        Assert.Equal(
            ["PushInt 1", "PushInt 2", "PushInt 3", "Mul.I64", "Add.I64", "Print.I64", "Exit"],
            Render("print(1 + 2 * 3)"));
    }

    [Fact]
    public void MixedArithmeticPromotesIntOperand()
    {
        // int + float: lhs promotes right after it is pushed.
        Assert.Equal(
            ["PushInt 1", "IntToFloat", "PushFloat 0.5", "Add.F64", "Print.F64", "Exit"],
            Render("print(1 + 0.5)"));

        // float + int: rhs promotes after it is pushed.
        Assert.Equal(
            ["PushFloat 0.5", "PushInt 1", "IntToFloat", "Add.F64", "Print.F64", "Exit"],
            Render("print(0.5 + 1)"));
    }

    [Fact]
    public void PromotionSeesThroughLocals()
    {
        List<string> ops = Render("""
            f := 2.5
            print(f / 2)
            """);

        Assert.Equal(
            ["PushFloat 2.5", "StoreLocal 0", "LoadLocal 0", "PushInt 2", "IntToFloat", "Div.F64", "Print.F64", "Exit"],
            ops);
    }

    [Fact]
    public void RebindingAllocatesFreshSlot()
    {
        IrProgram program = Generate("""
            x := 1
            x := x + 1
            print(x)
            """);

        Assert.Equal([IrType.I64, IrType.I64], program.LocalTypes);
        Assert.Equal(
            ["PushInt 1", "StoreLocal 0", "LoadLocal 0", "PushInt 1", "Add.I64", "StoreLocal 1", "LoadLocal 1", "Print.I64", "Exit"],
            program.Ops.Select(op => op.ToString()));
    }

    [Fact]
    public void RebindingCanChangeType()
    {
        IrProgram program = Generate("""
            x := 1
            x := 2.5
            print(x)
            """);

        Assert.Equal([IrType.I64, IrType.F64], program.LocalTypes);
    }

    [Fact]
    public void UndefinedVariableReportsAndRecovers()
    {
        DiagnosticBag bag = new();
        IrGenerator.Run(ParserTestHarness.ParseOk("print(nope)\nprint(alsonope)"), "main.script", bag);

        Assert.Equal(2, bag.Items.Count(d => d.Message.Contains("undefined variable")));
    }

    [Fact]
    public void OverflowingIntLiteralIsReported()
    {
        DiagnosticBag bag = new();
        IrGenerator.Run(ParserTestHarness.ParseOk("x := 99999999999999999999"), "main.script", bag);

        Assert.Contains(bag.Items, d => d.Message.Contains("does not fit in 64 bits"));
    }
}

using Bytecode;
using Ir;
using Tests.Parsing;
using Vm;

namespace Tests.Vm;

public class VmRunnerTests
{
    private static Result<int> Execute(string source, TextWriter stdout)
    {
        DiagnosticBag bag = new();

        return IrGenerator
            .Run(ParserTestHarness.ParseOk(source), "main.script", bag)
            .FlatMap(BytecodeEmitter.Run)
            .FlatMap(chunk => VmRunner.Run(chunk, stdout));
    }

    private static string RunToString(string source)
    {
        StringWriter writer = new();
        Result<int> result = Execute(source, writer);

        Assert.True(result.IsOk, result.Message);
        Assert.Equal(0, result.Unwrap());
        return writer.ToString();
    }

    [Fact]
    public void IntArithmeticWithPrecedence()
    {
        Assert.Equal("7\n", RunToString("print(1 + 2 * 3)"));
        Assert.Equal("9\n", RunToString("print((1 + 2) * 3)"));
    }

    [Fact]
    public void IntDivisionTruncates()
    {
        Assert.Equal("0\n", RunToString("print(1 / 2)"));
        Assert.Equal("-2\n", RunToString("print((0 - 7) / 3)"));
    }

    [Fact]
    public void FloatArithmeticAndPromotion()
    {
        Assert.Equal("3.75\n", RunToString("print(1.5 + 2.25)"));
        Assert.Equal("7.5\n", RunToString("print(7 + 0.5)"));
        Assert.Equal("0.333333\n", RunToString("print(1.0 / 3)"));
    }

    [Fact]
    public void LocalsStoreAndLoadAcrossStatements()
    {
        Assert.Equal("14\n", RunToString("""
            x := 1 + 2 * 3
            x := x * 2
            print(x)
            """));
    }

    [Fact]
    public void ConstantsAreDeduplicated()
    {
        DiagnosticBag bag = new();

        Chunk chunk = IrGenerator
            .Run(ParserTestHarness.ParseOk("print(1 + 1 + 1)"), "main.script", bag)
            .FlatMap(BytecodeEmitter.Run)
            .Unwrap();

        Assert.Single(chunk.Constants);
    }

    [Fact]
    public void IntDivisionByZeroIsACleanError()
    {
        StringWriter writer = new();
        Result<int> result = Execute("print(1 / 0)", writer);

        Assert.False(result.IsOk);
        Assert.Contains("integer division by zero", result.Message);
    }

    [Fact]
    public void FloatDivisionByZeroIsIeeeInfinity()
    {
        // IEEE semantics: no error. (Cross-backend output for inf is intentionally
        // not guaranteed; msvcrt's legacy printf renders it as 1.#INF.)
        StringWriter writer = new();
        Result<int> result = Execute("print(1.0 / 0.0)", writer);

        Assert.True(result.IsOk);
    }
}

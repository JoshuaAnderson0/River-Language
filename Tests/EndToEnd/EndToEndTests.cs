using System.Diagnostics;
using Bytecode;
using Ir;
using Lexing;
using Parsing;
using Vm;
using GrammarSetModel = Grammar.GrammarSet;

namespace Tests.EndToEnd;

/// <summary>
/// Full-pipeline tests against the real Core grammar files and the Examples/Arithmetic
/// project: grammar files → meta-parser → LALR → lexer → parser → IR → both backends.
/// Output lines must be identical across the VM and the native exe (line endings are
/// normalized: msvcrt printf in text mode emits CRLF, the VM's TextWriter emits LF).
/// </summary>
public class EndToEndTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static readonly string[] ExpectedOutput = ["7", "9", "0", "3.75", "7.5", "14", "0.333333"];

    private static string FindRepoRoot()
    {
        string? directory = AppContext.BaseDirectory;

        while (directory is not null && !File.Exists(Path.Combine(directory, "Program.sln")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.NotNull(directory);
        return directory;
    }

    private static Result<IrProgram> CompileArithmetic(DiagnosticBag bag)
    {
        string grammarPath = Path.Combine(RepoRoot, "Core", "core.script.grammar");
        string scriptPath = Path.Combine(RepoRoot, "Examples", "Arithmetic", "main.script");

        return SourceFile
            .Load(grammarPath)
            .FlatMap(grammarFile => global::Grammar.MetaParser.Run(grammarFile, bag))
            .FlatMap(rules => global::Grammar.Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => global::Grammar.GrammarValidator.Run(grammarSet, "PROGRAM", bag))
            .FlatMap(grammarSet => global::Parsing.LalrBuilder
                .Run(grammarSet, "PROGRAM", bag)
                .FlatMap(table => SourceFile
                    .Load(scriptPath)
                    .FlatMap(scriptFile => LexerRunner
                        .Run(scriptFile, LexerTables.FromGrammar(grammarSet), bag)
                        .FlatMap(tokens => ParserRunner.Run(tokens, table, scriptFile, bag)))))
            .FlatMap(program => IrGenerator.Run(program, "main.script", bag));
    }

    [Fact]
    public void VmBackendProducesExpectedOutput()
    {
        DiagnosticBag bag = new();
        StringWriter stdout = new();

        Result<int> result = CompileArithmetic(bag)
            .FlatMap(BytecodeEmitter.Run)
            .FlatMap(chunk => VmRunner.Run(chunk, stdout));

        Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)) + result.Message);
        Assert.Equal(0, result.Unwrap());
        Assert.Equal(ExpectedOutput, Lines(stdout.ToString()));
    }

    [Fact]
    public void AsmBackendExeMatchesVmOutput()
    {
        DiagnosticBag bag = new();
        string exePath = Path.Combine(Path.GetTempPath(), $"compiler-e2e-{Guid.NewGuid():N}.exe");

        try
        {
            Result result = CompileArithmetic(bag)
                .FlatMap(ir => global::Asm.AsmGenerator.Run(ir))
                .FlatMap(global::Assembler.AssemblerRunner.Run)
                .FlatMap(code => global::Linker.PeLinker.Run(code, exePath));

            Assert.True(result.IsOk, string.Join("; ", bag.Items.Select(d => d.Message)) + result.Message);

            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                RedirectStandardOutput = true,
                UseShellExecute = false
            })!;

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(30_000);

            Assert.Equal(0, process.ExitCode);
            Assert.Equal(ExpectedOutput, Lines(output));
        }
        finally
        {
            File.Delete(exePath);
        }
    }

    [Fact]
    public void LinkedExeHasWellFormedPeHeaders()
    {
        DiagnosticBag bag = new();
        string exePath = Path.Combine(Path.GetTempPath(), $"compiler-pe-{Guid.NewGuid():N}.exe");

        try
        {
            Result result = CompileArithmetic(bag)
                .FlatMap(ir => global::Asm.AsmGenerator.Run(ir))
                .FlatMap(global::Assembler.AssemblerRunner.Run)
                .FlatMap(code => global::Linker.PeLinker.Run(code, exePath));

            Assert.True(result.IsOk, result.Message);

            byte[] bytes = File.ReadAllBytes(exePath);

            // MZ magic and e_lfanew.
            Assert.Equal((byte)'M', bytes[0]);
            Assert.Equal((byte)'Z', bytes[1]);
            int peOffset = BitConverter.ToInt32(bytes, 0x3C);
            Assert.Equal(0x40, peOffset);

            // PE signature, machine, section count.
            Assert.Equal("PE\0\0"u8.ToArray(), bytes[peOffset..(peOffset + 4)]);
            Assert.Equal(0x8664, BitConverter.ToUInt16(bytes, peOffset + 4));
            Assert.Equal(3, BitConverter.ToUInt16(bytes, peOffset + 6));

            // PE32+ magic and console subsystem.
            int optionalHeader = peOffset + 24;
            Assert.Equal(0x20B, BitConverter.ToUInt16(bytes, optionalHeader));
            Assert.Equal(3, BitConverter.ToUInt16(bytes, optionalHeader + 68));

            // Entry point at .text RVA.
            Assert.Equal(0x1000u, BitConverter.ToUInt32(bytes, optionalHeader + 16));

            // Imported DLL names are present.
            string image = System.Text.Encoding.ASCII.GetString(bytes);
            Assert.Contains("msvcrt.dll", image);
            Assert.Contains("kernel32.dll", image);
            Assert.Contains("printf", image);
            Assert.Contains("ExitProcess", image);
        }
        finally
        {
            File.Delete(exePath);
        }
    }

    private static string[] Lines(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .ToArray();
}

using Asm;
using Assembler;
using Ir;
using Linker;

namespace Program;

/// <summary>
/// Native backend: x64 codegen → MASM-style listing (for inspection) → our own assembler
/// → our own PE32+ linker. Outputs land in <c>&lt;project&gt;/out/&lt;package&gt;.asm|.exe</c>.
/// </summary>
public static class AsmBackend
{
    public static Result<int> Run(IrProgram ir, BuildInfo buildInfo, Project project)
    {
        string outDirectory = Path.Combine(project.RootPath, "out");
        string listingPath = Path.Combine(outDirectory, $"{buildInfo.PackageName}.asm");
        string exePath = Path.Combine(outDirectory, $"{buildInfo.PackageName}.exe");

        return AsmGenerator
            .Run(ir)
            .FlatMap(asm => WriteListing(asm, listingPath)
                .FlatMap(_ => AssemblerRunner.Run(asm)))
            .FlatMap(code => PeLinker
                .Run(code, exePath)
                .Map(() => 0))
            .Map(exitCode =>
            {
                Console.WriteLine($"wrote {listingPath}");
                Console.WriteLine($"wrote {exePath}");
                return exitCode;
            });
    }

    private static Result<string> WriteListing(AsmProgram asm, string listingPath)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(listingPath)!);
            File.WriteAllText(listingPath, ListingWriter.Render(asm));
            return Result<string>.Ok(listingPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<string>.Error($"cannot write '{listingPath}': {exception.Message}");
        }
    }
}

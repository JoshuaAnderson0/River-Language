using Bytecode;
using Diagnostics;
using Grammar;
using Ir;
using Lexing;
using Parsing;
using Vm;

namespace Program;

public enum BackendKind
{
    Vm,
    Asm
}

/// <summary>
/// The compilation pipeline, mirroring the phases in DESIGN.md. Every stage is a static
/// Run function returning a Result, composed with FlatMap; diagnostics accumulate in one
/// bag and render Rust-style at the end.
/// </summary>
public static class Compilation
{
    public static Result<int> Run(Project project, BackendKind backend)
    {
        DiagnosticBag bag = new();
        Dictionary<string, SourceFile> sources = [];

        Result<int> result = project
            .ResolveBuildFiles()
            .FlatMap(buildFiles => CheckBackendSupportsProfiles(backend, buildFiles))
            .FlatMap(buildFiles => CompileProfiles(project, buildFiles, backend, bag, sources));

        string rendered = DiagnosticRenderer.RenderAll(bag, path => sources.GetValueOrDefault(path));

        if (rendered.Length > 0)
        {
            Console.Error.Write(rendered);
        }

        return result;
    }

    private static Result<List<string>> CheckBackendSupportsProfiles(BackendKind backend, List<string> buildFiles) =>
        backend == BackendKind.Vm && buildFiles.Count > 1
            ? Result<List<string>>.Error("the VM backend supports only one build profile per build")
            : Result<List<string>>.Ok(buildFiles);

    private static Result<int> CompileProfiles(
        Project project,
        List<string> buildFiles,
        BackendKind backend,
        DiagnosticBag bag,
        Dictionary<string, SourceFile> sources)
    {
        Result<int> result = Result<int>.Ok(0);

        foreach (string buildFile in buildFiles)
        {
            result = result.FlatMap(_ => CompileProfile(project, buildFile, backend, bag, sources));
        }

        return result;
    }

    private static Result<int> CompileProfile(
        Project project,
        string buildFile,
        BackendKind backend,
        DiagnosticBag bag,
        Dictionary<string, SourceFile> sources)
    {
        return CompiledGrammar
            .Load(BuildGrammarFiles(project), "BUILD", bag, sources)
            .FlatMap(buildGrammar => ParseFile(buildFile, buildGrammar, bag, sources))
            .FlatMap(buildNode => BuildInfo.Extract(buildNode, buildFile, project, bag))
            .FlatMap(buildInfo => CompiledGrammar
                .Load(ScriptGrammarFiles(project), "PROGRAM", bag, sources)
                .FlatMap(scriptGrammar => SelectScriptFile(project)
                    .FlatMap(scriptFile => ParseFile(scriptFile, scriptGrammar, bag, sources)
                        .FlatMap(programNode => IrGenerator.Run(programNode, scriptFile, bag))))
                .FlatMap(ir => RunBackend(ir, buildInfo, project, backend)));
    }

    /// <summary>Phase 2 of DESIGN.md: core build grammar plus the project's .build.grammar files.</summary>
    private static List<string> BuildGrammarFiles(Project project)
    {
        List<string> files = [Path.Combine(CoreGrammarDirectory(), "core.build.grammar")];

        files.AddRange(project.GrammarFiles.Where(f => f.EndsWith(".build.grammar")));
        return files;
    }

    /// <summary>Phase 4 of DESIGN.md: everything except .build.grammar files.</summary>
    private static List<string> ScriptGrammarFiles(Project project)
    {
        List<string> files = [Path.Combine(CoreGrammarDirectory(), "core.script.grammar")];

        files.AddRange(project.GrammarFiles.Where(f => !f.EndsWith(".build.grammar")));
        return files;
    }

    private static string CoreGrammarDirectory() =>
        Path.Combine(AppContext.BaseDirectory, "Core");

    private static Result<string> SelectScriptFile(Project project) =>
        project.ScriptFiles.Count switch
        {
            0 => Result<string>.Error("project contains no .script files"),
            1 => Result<string>.Ok(project.ScriptFiles[0]),
            _ => Result<string>.Error("multiple script files are not yet supported")
        };

    private static Result<SyntaxNode> ParseFile(
        string path,
        CompiledGrammar grammar,
        DiagnosticBag bag,
        Dictionary<string, SourceFile> sources)
    {
        return SourceFile
            .Load(path)
            .FlatMap(file =>
            {
                sources[path] = file;

                return LexerRunner
                    .Run(file, grammar.Lexer, bag)
                    .FlatMap(tokens => ParserRunner.Run(tokens, grammar.Table, file, bag));
            });
    }

    private static Result<int> RunBackend(IrProgram ir, BuildInfo buildInfo, Project project, BackendKind backend) =>
        backend switch
        {
            BackendKind.Vm => BytecodeEmitter
                .Run(ir)
                .FlatMap(chunk => VmRunner.Run(chunk, Console.Out)),

            _ => AsmBackend.Run(ir, buildInfo, project)
        };
}

/// <summary>A grammar compiled to runnable form: lexer tables plus the LALR parse table.</summary>
public class CompiledGrammar
{
    public required LexerTables Lexer;
    public required ParseTable Table;

    public static Result<CompiledGrammar> Load(
        List<string> grammarFiles,
        string startSymbol,
        DiagnosticBag bag,
        Dictionary<string, SourceFile> sources)
    {
        Result<List<GrammarRule>> allRules = Result<List<GrammarRule>>.Ok([]);

        foreach (string path in grammarFiles)
        {
            allRules = allRules.FlatMap(rules => SourceFile
                .Load(path)
                .FlatMap(file =>
                {
                    sources[path] = file;
                    return MetaParser.Run(file, bag);
                })
                .Map(fileRules =>
                {
                    rules.AddRange(fileRules);
                    return rules;
                }));
        }

        return allRules
            .FlatMap(rules => Desugarer.Run(rules, bag))
            .FlatMap(grammarSet => GrammarValidator.Run(grammarSet, startSymbol, bag))
            .FlatMap(grammarSet => LalrBuilder
                .Run(grammarSet, startSymbol, bag)
                .Map(table => new CompiledGrammar
                {
                    Lexer = LexerTables.FromGrammar(grammarSet),
                    Table = table
                }));
    }
}

/// <summary>Phase 3 of DESIGN.md, sliced down: package name extraction, no dependencies yet.</summary>
public class BuildInfo
{
    public required string PackageName;

    public static Result<BuildInfo> Extract(SyntaxNode buildNode, string buildFile, Project project, DiagnosticBag bag)
    {
        foreach (SyntaxNode usingNode in buildNode.List("using"))
        {
            bag.Add(Diagnostic.Error(
                $"dependencies are not yet supported (using '{usingNode.Single("name")!.TokenText}')",
                buildFile,
                usingNode.Span));
        }

        string packageName = buildNode.Single("package")?.Single("name")?.TokenText
            ?? Path.GetFileName(Path.TrimEndingDirectorySeparator(project.RootPath));

        return bag.ToResult(new BuildInfo { PackageName = packageName });
    }
}

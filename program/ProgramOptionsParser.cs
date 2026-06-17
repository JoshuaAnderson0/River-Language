namespace Program;

public class ProgramCommand;

public class BuildCommand : ProgramCommand
{
    public required string ProjectPath;
    public List<string> BuildProfiles = [];
    public BackendKind Backend = BackendKind.Vm;
}

public class ProgramOptions
{
    public required ProgramCommand Command;
}

public class ProgramOptionsParser
{
    public readonly string[] Args;

    public ProgramOptionsParser(string[] args)
    {
        Args = args;
    }

    public Result<ProgramOptions> Parse()
    {
        if (Args.Length == 0)
        {
            return BuildError<ProgramOptions>("No command specified.");
        }

        string commandArgument = Args[0].ToLowerInvariant();

        Result<ProgramCommand> result = commandArgument switch
        {
            "build" => ParseBuildCommand(),
            _ => BuildError<ProgramCommand>($"Unknown command: {commandArgument}")
        };
        
        return result.Map(command => new ProgramOptions { Command = command });
    }

    private Result<ProgramCommand> ParseBuildCommand()
    {
        if (Args.Length < 2)
        {
            return BuildError<ProgramCommand>("build command requires a project-path argument.");
        }

        string projectPath = Args[1];
        List<string> buildProfiles = [];
        BackendKind backend = BackendKind.Vm;

        for (int index = 2; index < Args.Length; index++)
        {
            if (Args[index] == "--backend")
            {
                if (index + 1 >= Args.Length)
                {
                    return BuildError<ProgramCommand>("--backend requires a value: vm or asm.");
                }

                index++;

                switch (Args[index].ToLowerInvariant())
                {
                    case "vm":
                        backend = BackendKind.Vm;
                        break;
                    case "asm":
                        backend = BackendKind.Asm;
                        break;
                    default:
                        return BuildError<ProgramCommand>($"Unknown backend: {Args[index]} (expected vm or asm).");
                }
            }
            else
            {
                buildProfiles.Add(Args[index]);
            }
        }

        return Result<ProgramCommand>.Ok(new BuildCommand
        {
            ProjectPath = projectPath,
            BuildProfiles = buildProfiles,
            Backend = backend
        });
    }

    public string GetUsage()
    {
        return """
            USAGE: script <command> [arguments]
            (*) means required argument

            COMMANDS:
              build 
                  Compile one or more projects.

                  Arguments:
                    *project-path      System path of project to compile.
                     build-profiles    Space-separated list of project build profiles to compile

                  Options:
                     --backend <vm|asm>    Code generation backend (default: vm).
                                           vm  - emit bytecode and execute immediately
                                           asm - emit native x64 assembly and link an executable
            """;
    }
    
    public Result<T> BuildError<T>(string message)
    {
        return Result<T>.Error($"{GetUsage()}\n\nError: {message}");
    }
}
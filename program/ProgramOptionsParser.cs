namespace Program;

public class ProgramCommand;

public class BuildCommand : ProgramCommand
{
    public required string ProjectPath;
    public List<string> BuildProfiles = [];
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
        List<string> buildProfiles = Args.Length > 2
            ? Args[2..].ToList()
            : [];

        return Result<ProgramCommand>.Ok(new BuildCommand
        {
            ProjectPath = projectPath,
            BuildProfiles = buildProfiles
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
            """;
    }
    
    public Result<T> BuildError<T>(string message)
    {
        return Result<T>.Error($"{GetUsage()}\n\nError: {message}");
    }
}
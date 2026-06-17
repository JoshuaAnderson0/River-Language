namespace Program;

public static class ProgramEntry
{
    public static void Main(string[] args)
    {
        ProgramOptionsParser optionsParser = new ProgramOptionsParser(args);
        Result<ProgramOptions> programOptions = optionsParser.Parse();

        if (!programOptions.IsOk)
        {
            Console.WriteLine(programOptions.Message);
            return;
        }

        switch (programOptions.Unwrap().Command)
        {
            case BuildCommand buildCommand:
                BuildProject(buildCommand);
                break;
        }
    }

    private static void BuildProject(BuildCommand buildCommand)
    {
        Result<int> result = Project
            .FromDirectory(buildCommand.ProjectPath)
            .FlatMap(project => Compilation.Run(
                project.WithBuildProfiles(buildCommand.BuildProfiles),
                buildCommand.Backend));

        if (!result.IsOk)
        {
            Console.Error.WriteLine(result.Message);
            Environment.ExitCode = 1;
            return;
        }

        Environment.ExitCode = result.Unwrap();
    }
}
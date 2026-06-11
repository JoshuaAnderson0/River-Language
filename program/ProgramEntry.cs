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
        Result result = Project
            .FromDirectory(buildCommand.ProjectPath)
            .FlatMap(project => project
                .WithBuildProfiles(buildCommand.BuildProfiles)
                .Build());
        
        if (!result.IsOk)
        {
            Console.WriteLine(result.Message);
            return;
        }
    }
}
namespace Program;

public class Project
{
    public string RootPath;
    public List<string> BuildFiles;
    public List<string> GrammarFiles;
    public List<string> ScriptFiles;
    public List<string> ProfilesToBuild;

    public Project(
        string rootPath,
        List<string> buildFiles,
        List<string> grammarFiles,
        List<string> scriptFiles,
        List<string> profilesToBuild)
    {
        RootPath = rootPath;
        BuildFiles = buildFiles;
        GrammarFiles = grammarFiles;
        ScriptFiles = scriptFiles;
        ProfilesToBuild = profilesToBuild;
    }

    public static Result<Project> FromDirectory(string projectPath)
    {
        if (!Path.Exists(projectPath))
        {
            return Result<Project>.Error($"Project path does not exist: {projectPath}");
        }
        
        var buildFiles = GetFilesRecursive(projectPath, "*.build");
        var grammarFiles = GetFilesRecursive(projectPath, "*.grammar");
        var scriptFiles = GetFilesRecursive(projectPath, "*.script");

        return Result<Project>.Ok(
            new Project(
                projectPath,
                buildFiles,
                grammarFiles,
                scriptFiles,
                profilesToBuild: []));
    }

    public static List<string> GetFilesRecursive(string path, string pattern)
    {
        return Directory.GetFiles(path, pattern, SearchOption.AllDirectories).ToList();
    }

    public Project WithBuildProfiles(params List<string> profilesToBuild)
    {
        if (profilesToBuild.Count != 0)
        {
            ProfilesToBuild = profilesToBuild;
        }
        
        return this;
    }

    /// <summary>
    /// Resolves the build files selected by the requested profiles, or the project's
    /// single build file when no profiles were specified.
    /// </summary>
    public Result<List<string>> ResolveBuildFiles()
    {
        if (ProfilesToBuild.Count == 0)
        {
            return BuildFiles.Count == 1
                ? Result<List<string>>.Ok([BuildFiles[0]])
                : Result<List<string>>.Error(
                    $"No build profiles specified and project contains {BuildFiles.Count} build files. " +
                    "Either specify build profiles or ensure exactly one .build file exists.");
        }

        List<string> missingProfiles = ProfilesToBuild
            .Where(profile => BuildFiles.All(f => ProfileName(f) != profile))
            .ToList();

        if (missingProfiles.Count > 0)
        {
            return Result<List<string>>.Error(
                $"Build profiles not found in project: {string.Join(", ", missingProfiles)}");
        }

        List<string> selected = ProfilesToBuild
            .Select(profile => BuildFiles.First(f => ProfileName(f) == profile))
            .ToList();

        return Result<List<string>>.Ok(selected);
    }

    private static string ProfileName(string buildFilePath) =>
        Path.GetFileName(buildFilePath).Split(".")[0];
}

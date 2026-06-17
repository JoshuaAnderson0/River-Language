namespace Diagnostics;

public class SourceFile
{
    public readonly string Path;
    public readonly string Text;
    private readonly List<int> _lineStarts;

    public SourceFile(string path, string text)
    {
        Path = path;
        Text = text;
        _lineStarts = ComputeLineStarts(text);
    }

    public static Result<SourceFile> Load(string path)
    {
        try
        {
            return Result<SourceFile>.Ok(new SourceFile(path, File.ReadAllText(path)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result<SourceFile>.Error($"Cannot read file '{path}': {exception.Message}");
        }
    }

    public int LineCount => _lineStarts.Count;

    /// <summary>
    /// Returns the text of a 1-based line without its terminator.
    /// </summary>
    public string GetLine(int line)
    {
        if (line < 1 || line > _lineStarts.Count)
        {
            return string.Empty;
        }

        int start = _lineStarts[line - 1];
        int end = line < _lineStarts.Count ? _lineStarts[line] : Text.Length;

        return Text[start..end].TrimEnd('\r', '\n');
    }

    private static List<int> ComputeLineStarts(string text)
    {
        List<int> starts = [0];

        for (int index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }
}

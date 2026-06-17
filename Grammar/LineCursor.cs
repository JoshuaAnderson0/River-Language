namespace Grammar;

/// <summary>
/// Character cursor over a single .grammar line with 1-based column tracking and
/// diagnostic reporting against the owning file.
/// </summary>
public class LineCursor
{
    public readonly string FilePath;
    public readonly int Line;
    private readonly string _text;
    private readonly DiagnosticBag _bag;
    private int _position;

    public LineCursor(string text, int line, string filePath, DiagnosticBag bag)
    {
        _text = text;
        Line = line;
        FilePath = filePath;
        _bag = bag;
    }

    public int Column => _position + 1;

    public bool AtEnd => _position >= _text.Length;

    public char Peek(int offset = 0) =>
        _position + offset < _text.Length ? _text[_position + offset] : '\0';

    public void Advance(int count) => _position += count;

    public void SkipWhitespace()
    {
        while (!AtEnd && char.IsWhiteSpace(Peek()))
        {
            Advance(1);
        }
    }

    public bool TryConsume(string expected)
    {
        if (_position + expected.Length > _text.Length)
        {
            return false;
        }

        if (_text.AsSpan(_position, expected.Length).SequenceEqual(expected))
        {
            Advance(expected.Length);
            return true;
        }

        return false;
    }

    public string? TakeIdentifier()
    {
        if (AtEnd || (!char.IsLetter(Peek()) && Peek() != '_'))
        {
            return null;
        }

        int start = _position;

        while (!AtEnd && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            Advance(1);
        }

        return _text[start.._position];
    }

    public int TakeNumber()
    {
        int start = _position;

        while (!AtEnd && char.IsDigit(Peek()))
        {
            Advance(1);
        }

        return int.Parse(_text[start.._position]);
    }

    /// <summary>Consumes up to (and including) the terminator; returns the content, or null at end of line.</summary>
    public string? TakeUntil(char terminator)
    {
        int start = _position;

        while (!AtEnd && Peek() != terminator)
        {
            Advance(1);
        }

        if (AtEnd)
        {
            return null;
        }

        string content = _text[start.._position];
        Advance(1);
        return content;
    }

    public void Error(string message) =>
        _bag.Add(Diagnostic.Error(message, FilePath, SourceSpan.Point(Line, Column)));
}

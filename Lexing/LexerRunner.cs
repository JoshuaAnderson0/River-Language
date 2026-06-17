namespace Lexing;

/// <summary>
/// Single-pass max-munch lexer. NEWLINE terminals (when the grammar uses them) are
/// normalized: no leading separator, runs collapse to one, and a final NEWLINE is
/// injected before end of input so trailing newlines are optional in source files.
/// Lex errors accumulate and scanning continues.
/// </summary>
public static class LexerRunner
{
    public static Result<List<Token>> Run(SourceFile file, LexerTables tables, DiagnosticBag bag)
    {
        Scanner scanner = new(file, tables, bag);
        return bag.ToResult(scanner.ScanAll());
    }

    private class Scanner
    {
        private readonly SourceFile _file;
        private readonly LexerTables _tables;
        private readonly DiagnosticBag _bag;
        private readonly List<Token> _tokens = [];
        private int _position;
        private int _line = 1;
        private int _column = 1;

        public Scanner(SourceFile file, LexerTables tables, DiagnosticBag bag)
        {
            _file = file;
            _tables = tables;
            _bag = bag;
        }

        private string Text => _file.Text;

        private bool AtEnd => _position >= Text.Length;

        private char Peek(int offset = 0) =>
            _position + offset < Text.Length ? Text[_position + offset] : '\0';

        public List<Token> ScanAll()
        {
            while (!AtEnd)
            {
                char current = Peek();

                if (current is ' ' or '\t' or '\r')
                {
                    Advance(1);
                }
                else if (current == '/' && Peek(1) == '/')
                {
                    SkipLineComment();
                }
                else if (current == '\n')
                {
                    HandleNewline();
                }
                else if (_tables.HasVersion && TryScanVersion())
                {
                }
                else if (char.IsDigit(current))
                {
                    ScanNumber();
                }
                else if (char.IsLetter(current) || current == '_')
                {
                    ScanIdentifierOrKeyword();
                }
                else if (!TryScanSymbolLiteral())
                {
                    _bag.Add(Diagnostic.Error(
                        $"unexpected character '{current}'",
                        _file.Path,
                        SourceSpan.Point(_line, _column)));
                    Advance(1);
                }
            }

            InjectFinalNewline();
            return _tokens;
        }

        private void Advance(int count)
        {
            for (int step = 0; step < count; step++)
            {
                if (Peek() == '\n')
                {
                    _line++;
                    _column = 1;
                }
                else
                {
                    _column++;
                }

                _position++;
            }
        }

        private void SkipLineComment()
        {
            while (!AtEnd && Peek() != '\n')
            {
                Advance(1);
            }
        }

        private void HandleNewline()
        {
            bool shouldEmit = _tables.HasNewline
                && _tokens.Count > 0
                && _tokens[^1].TerminalName != "NEWLINE";

            if (shouldEmit)
            {
                _tokens.Add(new Token
                {
                    TerminalName = "NEWLINE",
                    Text = "\n",
                    Span = SourceSpan.Point(_line, _column)
                });
            }

            Advance(1);
        }

        private void InjectFinalNewline()
        {
            if (_tables.HasNewline && _tokens.Count > 0 && _tokens[^1].TerminalName != "NEWLINE")
            {
                _tokens.Add(new Token
                {
                    TerminalName = "NEWLINE",
                    Text = "\n",
                    Span = SourceSpan.Point(_line, _column)
                });
            }
        }

        /// <summary>Matches v1.0-style versions; checked before identifiers so `v` stays usable as one.</summary>
        private bool TryScanVersion()
        {
            if (Peek() != 'v' || !char.IsDigit(Peek(1)))
            {
                return false;
            }

            int length = 1;

            while (char.IsDigit(Peek(length)))
            {
                length++;
            }

            if (Peek(length) != '.' || !char.IsDigit(Peek(length + 1)))
            {
                return false;
            }

            length++;

            while (char.IsDigit(Peek(length)))
            {
                length++;
            }

            if (IsIdentifierChar(Peek(length)))
            {
                return false;
            }

            EmitToken("VERSION", length);
            return true;
        }

        private void ScanNumber()
        {
            int length = 0;

            while (char.IsDigit(Peek(length)))
            {
                length++;
            }

            bool isFloat = _tables.HasFloat && Peek(length) == '.' && char.IsDigit(Peek(length + 1));

            if (isFloat)
            {
                length++;

                while (char.IsDigit(Peek(length)))
                {
                    length++;
                }
            }

            EmitToken(isFloat ? "FLOAT" : "NUMBER", length);
        }

        private void ScanIdentifierOrKeyword()
        {
            int length = 0;

            while (IsIdentifierChar(Peek(length)))
            {
                length++;
            }

            string text = Text.Substring(_position, length);

            if (_tables.IdentifierKeywords.Contains(text))
            {
                EmitToken(text, length);
            }
            else if (_tables.HasIdentifier)
            {
                EmitToken("IDENTIFIER", length);
            }
            else
            {
                _bag.Add(Diagnostic.Error(
                    $"unexpected identifier '{text}' (grammar defines no IDENTIFIER terminal)",
                    _file.Path,
                    new SourceSpan(_line, _column, _line, _column + length - 1)));
                Advance(length);
            }
        }

        private bool TryScanSymbolLiteral()
        {
            foreach (string literal in _tables.SymbolLiterals)
            {
                if (Matches(literal))
                {
                    EmitToken(literal, literal.Length);
                    return true;
                }
            }

            return false;
        }

        private bool Matches(string literal)
        {
            if (_position + literal.Length > Text.Length)
            {
                return false;
            }

            return Text.AsSpan(_position, literal.Length).SequenceEqual(literal);
        }

        private void EmitToken(string terminalName, int length)
        {
            string text = Text.Substring(_position, length);
            SourceSpan span = new(_line, _column, _line, _column + length - 1);

            _tokens.Add(new Token { TerminalName = terminalName, Text = text, Span = span });
            Advance(length);
        }

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}

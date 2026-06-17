namespace Grammar;

/// <summary>
/// Hand-written parser for .grammar files. The meta-syntax is line-oriented:
///
///   ATOM := $DISCARDED @CAPTURED @CAPTURED:alias bare_literal ':=' ( $GROUPED )? %left %2
///
/// Bare literals must be identifier-like; anything containing meta-characters must be
/// quoted with single quotes ('(' ':=' '+'). Comments start with //.
/// </summary>
public static class MetaParser
{
    public static Result<List<GrammarRule>> Run(SourceFile file, DiagnosticBag bag)
    {
        List<GrammarRule> rules = [];

        for (int lineNumber = 1; lineNumber <= file.LineCount; lineNumber++)
        {
            string line = StripComment(file.GetLine(lineNumber));

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            LineCursor cursor = new(line, lineNumber, file.Path, bag);
            GrammarRule? rule = ParseRule(cursor);

            if (rule is not null)
            {
                rules.Add(rule);
            }
        }

        return bag.ToResult(rules);
    }

    private static string StripComment(string line)
    {
        bool inQuote = false;

        for (int index = 0; index < line.Length - 1; index++)
        {
            if (line[index] == '\'')
            {
                inQuote = !inQuote;
            }
            else if (!inQuote && line[index] == '/' && line[index + 1] == '/')
            {
                return line[..index];
            }
        }

        return line;
    }

    private static GrammarRule? ParseRule(LineCursor cursor)
    {
        cursor.SkipWhitespace();
        string? atomName = cursor.TakeIdentifier();

        if (atomName is null)
        {
            cursor.Error("expected atom name at start of rule");
            return null;
        }

        cursor.SkipWhitespace();

        if (!cursor.TryConsume(":="))
        {
            cursor.Error($"expected ':=' after atom name '{atomName}'");
            return null;
        }

        List<List<RuleElement>>? alternatives = ParseAlternation(cursor, insideGroup: false);

        if (alternatives is null)
        {
            return null;
        }

        GrammarRule rule = new()
        {
            AtomName = atomName,
            Alternatives = alternatives,
            FilePath = cursor.FilePath,
            Span = new SourceSpan(cursor.Line, 1, cursor.Line, cursor.Column)
        };

        return ParseAnnotations(cursor, rule) ? rule : null;
    }

    private static List<List<RuleElement>>? ParseAlternation(LineCursor cursor, bool insideGroup)
    {
        List<List<RuleElement>> alternatives = [];
        List<RuleElement> sequence = [];

        while (true)
        {
            cursor.SkipWhitespace();

            if (cursor.AtEnd)
            {
                if (insideGroup)
                {
                    cursor.Error("unclosed group: expected ')'");
                    return null;
                }

                break;
            }

            char next = cursor.Peek();

            if (next == ')' && insideGroup)
            {
                break;
            }

            if (next == '%' && !insideGroup)
            {
                break;
            }

            if (next == '|')
            {
                cursor.Advance(1);

                if (sequence.Count == 0)
                {
                    cursor.Error("empty alternative before '|'");
                    return null;
                }

                alternatives.Add(sequence);
                sequence = [];
                continue;
            }

            RuleElement? element = ParseElement(cursor);

            if (element is null)
            {
                return null;
            }

            sequence.Add(element);
        }

        if (sequence.Count == 0)
        {
            cursor.Error("empty rule alternative");
            return null;
        }

        alternatives.Add(sequence);
        return alternatives;
    }

    private static RuleElement? ParseElement(LineCursor cursor)
    {
        int startColumn = cursor.Column;
        char next = cursor.Peek();

        RuleElement? element = next switch
        {
            '$' => ParseReference(cursor, ReferenceCapture.Discard),
            '@' => ParseReference(cursor, ReferenceCapture.Capture),
            '\'' => ParseQuotedLiteral(cursor),
            '(' => ParseGroup(cursor),
            _ => ParseBareLiteral(cursor)
        };

        if (element is null)
        {
            return null;
        }

        element.Quantifier = TakeQuantifier(cursor);
        element.Span = new SourceSpan(cursor.Line, startColumn, cursor.Line, cursor.Column - 1);
        return element;
    }

    private static RuleElement? ParseReference(LineCursor cursor, ReferenceCapture capture)
    {
        char sigil = cursor.Peek();
        cursor.Advance(1);
        string? name = cursor.TakeIdentifier();

        if (name is null)
        {
            cursor.Error($"expected name after '{sigil}'");
            return null;
        }

        string? alias = null;

        // ':' starts an alias, but ':=' would be the next rule's operator misplaced here.
        if (capture == ReferenceCapture.Capture && cursor.Peek() == ':' && cursor.Peek(1) != '=')
        {
            cursor.Advance(1);
            alias = cursor.TakeIdentifier();

            if (alias is null)
            {
                cursor.Error("expected alias name after ':'");
                return null;
            }
        }

        return new RuleElement
        {
            Kind = ElementKind.Reference,
            Name = name,
            Capture = capture,
            Alias = alias
        };
    }

    private static RuleElement? ParseQuotedLiteral(LineCursor cursor)
    {
        cursor.Advance(1);
        string? text = cursor.TakeUntil('\'');

        if (text is null)
        {
            cursor.Error("unterminated quoted literal");
            return null;
        }

        if (text.Length == 0)
        {
            cursor.Error("empty quoted literal");
            return null;
        }

        return new RuleElement { Kind = ElementKind.LiteralText, Literal = text };
    }

    private static RuleElement? ParseGroup(LineCursor cursor)
    {
        cursor.Advance(1);
        List<List<RuleElement>>? alternatives = ParseAlternation(cursor, insideGroup: true);

        if (alternatives is null)
        {
            return null;
        }

        cursor.Advance(1); // consume ')'
        return new RuleElement { Kind = ElementKind.Group, GroupAlternatives = alternatives };
    }

    private static RuleElement? ParseBareLiteral(LineCursor cursor)
    {
        string? text = cursor.TakeIdentifier();

        if (text is null)
        {
            cursor.Error($"unexpected character '{cursor.Peek()}' (symbol literals must be quoted, e.g. '+')");
            return null;
        }

        return new RuleElement { Kind = ElementKind.LiteralText, Literal = text };
    }

    private static ElementQuantifier TakeQuantifier(LineCursor cursor)
    {
        ElementQuantifier quantifier = cursor.Peek() switch
        {
            '?' => ElementQuantifier.Optional,
            '*' => ElementQuantifier.ZeroOrMore,
            '+' => ElementQuantifier.OneOrMore,
            _ => ElementQuantifier.One
        };

        if (quantifier != ElementQuantifier.One)
        {
            cursor.Advance(1);
        }

        return quantifier;
    }

    private static bool ParseAnnotations(LineCursor cursor, GrammarRule rule)
    {
        while (true)
        {
            cursor.SkipWhitespace();

            if (cursor.AtEnd)
            {
                return true;
            }

            if (cursor.Peek() != '%')
            {
                cursor.Error($"unexpected character '{cursor.Peek()}' after rule body");
                return false;
            }

            cursor.Advance(1);

            if (char.IsDigit(cursor.Peek()))
            {
                if (rule.PrecedenceLevel >= 0)
                {
                    cursor.Error("duplicate precedence annotation");
                    return false;
                }

                rule.PrecedenceLevel = cursor.TakeNumber();
                continue;
            }

            string? word = cursor.TakeIdentifier();

            Associativity assoc = word switch
            {
                "left" => Associativity.Left,
                "right" => Associativity.Right,
                "none" => Associativity.NonAssoc,
                _ => Associativity.Unspecified
            };

            if (assoc == Associativity.Unspecified)
            {
                cursor.Error($"unknown annotation '%{word}' (expected %left, %right, %none, or %N)");
                return false;
            }

            if (rule.Assoc != Associativity.Unspecified)
            {
                cursor.Error("duplicate associativity annotation");
                return false;
            }

            rule.Assoc = assoc;
        }
    }
}

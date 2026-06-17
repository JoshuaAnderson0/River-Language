using Grammar;

namespace Lexing;

/// <summary>
/// The lexer is driven entirely by the active grammar: literal terminals split into
/// identifier-like keywords and symbol literals (matched longest-first), and a builtin
/// terminal class is only lexed if some grammar rule references it. This is how VERSION
/// exists for build files without polluting script lexing.
/// </summary>
public class LexerTables
{
    public required HashSet<string> IdentifierKeywords;
    public required List<string> SymbolLiterals;
    public bool HasNumber;
    public bool HasFloat;
    public bool HasIdentifier;
    public bool HasVersion;
    public bool HasNewline;

    public static LexerTables FromGrammar(GrammarSet grammarSet)
    {
        List<TerminalInfo> literals = grammarSet.Terminals.Values.Where(t => t.IsLiteral).ToList();

        return new LexerTables
        {
            IdentifierKeywords = literals
                .Where(t => t.IsIdentifierLike)
                .Select(t => t.Name)
                .ToHashSet(),
            SymbolLiterals = literals
                .Where(t => !t.IsIdentifierLike)
                .Select(t => t.Name)
                .OrderByDescending(t => t.Length)
                .ToList(),
            HasNumber = HasBuiltin(grammarSet, BuiltinClass.Number),
            HasFloat = HasBuiltin(grammarSet, BuiltinClass.Float),
            HasIdentifier = HasBuiltin(grammarSet, BuiltinClass.Identifier),
            HasVersion = HasBuiltin(grammarSet, BuiltinClass.Version),
            HasNewline = HasBuiltin(grammarSet, BuiltinClass.Newline)
        };
    }

    private static bool HasBuiltin(GrammarSet grammarSet, BuiltinClass builtin) =>
        grammarSet.Terminals.Values.Any(t => t.Builtin == builtin);
}

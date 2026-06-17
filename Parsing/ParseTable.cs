using Grammar;

namespace Parsing;

public enum ParseActionKind : byte
{
    Error,
    Shift,
    Reduce,
    Accept,

    /// <summary>%none associativity violation: a deliberate error entry.</summary>
    NonAssoc
}

public struct ParseAction
{
    public ParseActionKind Kind;

    /// <summary>Target state for Shift, production index for Reduce.</summary>
    public int Target;

    public static readonly ParseAction None = new() { Kind = ParseActionKind.Error };

    public static ParseAction Shift(int state) => new() { Kind = ParseActionKind.Shift, Target = state };

    public static ParseAction Reduce(int production) => new() { Kind = ParseActionKind.Reduce, Target = production };
}

/// <summary>A production as the parser runtime sees it: lengths, ids, and the capture plan.</summary>
public class TableProduction
{
    public required string Lhs;
    public required int LhsId;
    public required int RhsLength;

    /// <summary>Per-RHS-position field names; null entries are discarded values.</summary>
    public required string?[] FieldNames;

    public ProductionActionKind Action;
    public int ValueIndex;
}

public class ParseTable
{
    public required string[] TerminalNames;
    public required Dictionary<string, int> TerminalIds;
    public required List<TableProduction> Productions;

    /// <summary>[state, terminalId]</summary>
    public required ParseAction[,] Actions;

    /// <summary>[state, nonterminalId]; -1 means no transition.</summary>
    public required int[,] Gotos;

    public required int EndTerminalId;
    public int StateCount => Actions.GetLength(0);
}

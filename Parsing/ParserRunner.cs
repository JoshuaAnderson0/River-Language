using Grammar;
using Lexing;

namespace Parsing;

/// <summary>
/// Table-driven shift/reduce parser. Reduces execute the production's capture plan to
/// assemble SyntaxNodes directly on the parse stack (DESIGN.md: node substitution happens
/// on the stack, bottom-up).
/// </summary>
public static class ParserRunner
{
    public static Result<SyntaxNode> Run(List<Token> tokens, ParseTable table, SourceFile file, DiagnosticBag bag)
    {
        List<StackEntry> stack = [new StackEntry { State = 0, Value = StackValue.NothingValue }];
        int position = 0;

        while (true)
        {
            Token? token = position < tokens.Count ? tokens[position] : null;
            int terminalId = token is null ? table.EndTerminalId : table.TerminalIds[token.TerminalName];
            ParseAction action = table.Actions[stack[^1].State, terminalId];

            switch (action.Kind)
            {
                case ParseActionKind.Shift:
                    stack.Add(new StackEntry
                    {
                        State = action.Target,
                        Value = StackValue.FromToken(token!),
                        Span = token!.Span,
                        HasSpan = true
                    });
                    position++;
                    break;

                case ParseActionKind.Reduce:
                    ApplyReduce(stack, table, table.Productions[action.Target]);
                    break;

                case ParseActionKind.Accept:
                    return bag.ToResult(ToNode(stack[^1].Value, stack[^1].Span));

                case ParseActionKind.NonAssoc:
                    bag.Add(Diagnostic.Error(
                        $"operator '{table.TerminalNames[action.Target]}' is non-associative and cannot be chained",
                        file.Path,
                        token?.Span ?? EndSpan(tokens)));
                    return bag.ToResult<SyntaxNode>(null!);

                default:
                    bag.Add(Diagnostic.Error(
                        token is null
                            ? $"unexpected end of input{ExpectedSuffix(table, stack[^1].State)}"
                            : $"unexpected {Describe(token)}{ExpectedSuffix(table, stack[^1].State)}",
                        file.Path,
                        token?.Span ?? EndSpan(tokens)));
                    return bag.ToResult<SyntaxNode>(null!);
            }
        }
    }

    private class StackEntry
    {
        public required int State;
        public required StackValue Value;
        public SourceSpan Span;
        public bool HasSpan;
    }

    /// <summary>Semantic value on the parse stack: a token, a node, a list, or nothing.</summary>
    private class StackValue
    {
        public Token? Token;
        public SyntaxNode? Node;
        public List<SyntaxNode>? List;

        public static readonly StackValue NothingValue = new();

        public static StackValue FromToken(Token token) => new() { Token = token };

        public static StackValue FromNode(SyntaxNode node) => new() { Node = node };

        public static StackValue FromList(List<SyntaxNode> list) => new() { List = list };

        public bool IsNothing => Token is null && Node is null && List is null;
    }

    private static void ApplyReduce(List<StackEntry> stack, ParseTable table, TableProduction production)
    {
        int rhsLength = production.RhsLength;
        List<StackEntry> popped = stack[^rhsLength..];
        stack.RemoveRange(stack.Count - rhsLength, rhsLength);

        (SourceSpan span, bool hasSpan) = UnionSpans(popped);
        StackValue value = ComputeValue(production, popped, span, hasSpan);

        int nextState = table.Gotos[stack[^1].State, production.LhsId];

        stack.Add(new StackEntry
        {
            State = nextState,
            Value = value,
            Span = span,
            HasSpan = hasSpan
        });
    }

    private static StackValue ComputeValue(
        TableProduction production,
        List<StackEntry> popped,
        SourceSpan span,
        bool hasSpan)
    {
        switch (production.Action)
        {
            case ProductionActionKind.PassThrough:
            {
                StackEntry entry = popped[production.ValueIndex];
                StackValue value = entry.Value;

                return value.Token is not null
                    ? StackValue.FromNode(WrapToken(value.Token))
                    : value;
            }

            case ProductionActionKind.EmptyOptional:
                return StackValue.NothingValue;

            case ProductionActionKind.EmptyList:
                return StackValue.FromList([]);

            case ProductionActionKind.NewList:
            {
                SyntaxNode? item = ToNodeOrNull(popped[production.ValueIndex].Value);
                return StackValue.FromList(item is null ? [] : [item]);
            }

            case ProductionActionKind.AppendList:
            {
                List<SyntaxNode> list = popped[0].Value.List ?? [];
                SyntaxNode? item = ToNodeOrNull(popped[production.ValueIndex].Value);

                if (item is not null)
                {
                    list.Add(item);
                }

                return StackValue.FromList(list);
            }

            default:
                return StackValue.FromNode(ConstructNode(production, popped, span, hasSpan));
        }
    }

    private static SyntaxNode ConstructNode(
        TableProduction production,
        List<StackEntry> popped,
        SourceSpan span,
        bool hasSpan)
    {
        SyntaxNode node = new()
        {
            Atom = production.Lhs,
            Span = hasSpan ? span : SourceSpan.Point(1, 1)
        };

        for (int index = 0; index < popped.Count; index++)
        {
            string? fieldName = production.FieldNames[index];

            if (fieldName is null)
            {
                continue;
            }

            StackValue value = popped[index].Value;

            if (value.List is not null)
            {
                node.Fields[fieldName] = new SyntaxChild { List = value.List };
            }
            else if (!value.IsNothing)
            {
                node.Fields[fieldName] = new SyntaxChild { Single = ToNodeOrNull(value) };
            }
        }

        return node;
    }

    private static SyntaxNode WrapToken(Token token) =>
        new() { Atom = token.TerminalName, Token = token, Span = token.Span };

    private static SyntaxNode? ToNodeOrNull(StackValue value)
    {
        if (value.Node is not null)
        {
            return value.Node;
        }

        return value.Token is not null ? WrapToken(value.Token) : null;
    }

    private static SyntaxNode ToNode(StackValue value, SourceSpan span) =>
        ToNodeOrNull(value) ?? new SyntaxNode { Atom = "EMPTY", Span = span };

    private static (SourceSpan, bool) UnionSpans(List<StackEntry> entries)
    {
        SourceSpan span = default;
        bool hasSpan = false;

        foreach (StackEntry entry in entries.Where(e => e.HasSpan))
        {
            span = hasSpan ? span.Union(entry.Span) : entry.Span;
            hasSpan = true;
        }

        return (span, hasSpan);
    }

    private static string Describe(Token token) =>
        token.TerminalName switch
        {
            "NEWLINE" => "end of line",
            _ => $"'{token.Text}'"
        };

    private static string ExpectedSuffix(ParseTable table, int state)
    {
        List<string> expected = [];

        for (int terminal = 0; terminal < table.TerminalNames.Length; terminal++)
        {
            if (table.Actions[state, terminal].Kind != ParseActionKind.Error)
            {
                expected.Add(table.TerminalNames[terminal] switch
                {
                    "NEWLINE" => "end of line",
                    "$end" => "end of input",
                    var name => $"'{name}'"
                });
            }
        }

        return expected.Count == 0 ? "" : $" (expected {string.Join(", ", expected)})";
    }

    private static SourceSpan EndSpan(List<Token> tokens) =>
        tokens.Count > 0
            ? SourceSpan.Point(tokens[^1].Span.EndLine, tokens[^1].Span.EndColumn)
            : SourceSpan.Point(1, 1);
}

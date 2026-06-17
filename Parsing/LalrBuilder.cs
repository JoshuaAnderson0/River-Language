using System.Text;
using Grammar;

namespace Parsing;

/// <summary>
/// LALR(1) table construction: build the canonical LR(1) automaton, then merge states with
/// identical cores. Simple and exactly correct for the grammar sizes the compiler sees;
/// merge-introduced reduce-reduce conflicts are reported, never silently resolved.
///
/// Shift-reduce conflicts resolve through %N precedence and %left/%right/%none
/// associativity per DESIGN.md; anything unresolved is rendered with its item set.
/// </summary>
public static class LalrBuilder
{
    public static Result<ParseTable> Run(GrammarSet grammarSet, string startSymbol, DiagnosticBag bag)
    {
        Builder builder = new(grammarSet, startSymbol, bag);
        ParseTable table = builder.Build();
        return bag.ToResult(table);
    }

    private class Builder
    {
        private readonly GrammarSet _grammarSet;
        private readonly DiagnosticBag _bag;

        // Symbol numbering: terminals get 0..T-1 ($end last); nonterminals are encoded as
        // T + index so one int space covers both.
        private readonly List<string> _terminalNames = [];
        private readonly Dictionary<string, int> _terminalIds = [];
        private readonly List<string> _nonterminalNames = [];
        private readonly Dictionary<string, int> _nonterminalIds = [];
        private readonly int _endId;

        // Production 0 is the augmented start S' := START.
        private readonly List<int> _prodLhs = [];
        private readonly List<int[]> _prodRhs = [];
        private readonly List<Production?> _prodSource = [];

        private bool[] _nullable = [];
        private HashSet<int>[] _first = [];

        // LR(1) item: (production << 8 | dot) -> lookahead terminal set.
        private readonly List<Dictionary<int, HashSet<int>>> _states = [];
        private readonly List<Dictionary<int, int>> _transitions = [];

        public Builder(GrammarSet grammarSet, string startSymbol, DiagnosticBag bag)
        {
            _grammarSet = grammarSet;
            _bag = bag;

            foreach (string name in grammarSet.Terminals.Keys)
            {
                _terminalIds[name] = _terminalNames.Count;
                _terminalNames.Add(name);
            }

            _endId = _terminalNames.Count;
            _terminalIds["$end"] = _endId;
            _terminalNames.Add("$end");

            foreach (string name in grammarSet.Nonterminals)
            {
                _nonterminalIds[name] = _nonterminalNames.Count;
                _nonterminalNames.Add(name);
            }

            int augmentedId = _nonterminalNames.Count;
            _nonterminalIds["S'"] = augmentedId;
            _nonterminalNames.Add("S'");

            _prodLhs.Add(augmentedId);
            _prodRhs.Add([EncodeNonterminal(_nonterminalIds[startSymbol])]);
            _prodSource.Add(null);

            foreach (Production production in grammarSet.Productions)
            {
                _prodLhs.Add(_nonterminalIds[production.Lhs]);
                _prodRhs.Add(production.Rhs
                    .Select(s => s.IsTerminal ? _terminalIds[s.Name] : EncodeNonterminal(_nonterminalIds[s.Name]))
                    .ToArray());
                _prodSource.Add(production);
            }
        }

        private int TerminalCount => _terminalNames.Count;

        private int EncodeNonterminal(int nonterminalId) => TerminalCount + nonterminalId;

        private bool IsTerminal(int symbol) => symbol < TerminalCount;

        private int DecodeNonterminal(int symbol) => symbol - TerminalCount;

        public ParseTable Build()
        {
            ComputeNullableAndFirst();
            BuildCanonicalStates();
            (List<Dictionary<int, HashSet<int>>> merged, List<Dictionary<int, int>> mergedTransitions) = MergeCores();
            return BuildTable(merged, mergedTransitions);
        }

        private void ComputeNullableAndFirst()
        {
            int nonterminalCount = _nonterminalNames.Count;
            _nullable = new bool[nonterminalCount];
            _first = new HashSet<int>[nonterminalCount];

            for (int index = 0; index < nonterminalCount; index++)
            {
                _first[index] = [];
            }

            bool changed = true;

            while (changed)
            {
                changed = false;

                for (int prod = 0; prod < _prodLhs.Count; prod++)
                {
                    int lhs = _prodLhs[prod];
                    bool allNullable = true;

                    foreach (int symbol in _prodRhs[prod])
                    {
                        if (IsTerminal(symbol))
                        {
                            changed |= _first[lhs].Add(symbol);
                            allNullable = false;
                            break;
                        }

                        int nonterminal = DecodeNonterminal(symbol);

                        foreach (int terminal in _first[nonterminal])
                        {
                            changed |= _first[lhs].Add(terminal);
                        }

                        if (!_nullable[nonterminal])
                        {
                            allNullable = false;
                            break;
                        }
                    }

                    if (allNullable && !_nullable[lhs])
                    {
                        _nullable[lhs] = true;
                        changed = true;
                    }
                }
            }
        }

        /// <summary>FIRST of the suffix rhs[from..] followed by the lookahead terminal.</summary>
        private HashSet<int> FirstOfSuffix(int[] rhs, int from, HashSet<int> lookaheads)
        {
            HashSet<int> result = [];

            for (int index = from; index < rhs.Length; index++)
            {
                int symbol = rhs[index];

                if (IsTerminal(symbol))
                {
                    result.Add(symbol);
                    return result;
                }

                int nonterminal = DecodeNonterminal(symbol);
                result.UnionWith(_first[nonterminal]);

                if (!_nullable[nonterminal])
                {
                    return result;
                }
            }

            result.UnionWith(lookaheads);
            return result;
        }

        private Dictionary<int, HashSet<int>> Closure(Dictionary<int, HashSet<int>> kernel)
        {
            Dictionary<int, HashSet<int>> items = kernel.ToDictionary(kv => kv.Key, kv => new HashSet<int>(kv.Value));
            Queue<int> pending = new(items.Keys);

            while (pending.Count > 0)
            {
                int core = pending.Dequeue();
                int prod = core >> 8;
                int dot = core & 0xFF;
                int[] rhs = _prodRhs[prod];

                if (dot >= rhs.Length || IsTerminal(rhs[dot]))
                {
                    continue;
                }

                int target = DecodeNonterminal(rhs[dot]);
                HashSet<int> follow = FirstOfSuffix(rhs, dot + 1, items[core]);

                for (int candidate = 0; candidate < _prodLhs.Count; candidate++)
                {
                    if (_prodLhs[candidate] != target)
                    {
                        continue;
                    }

                    int candidateCore = candidate << 8;

                    if (!items.TryGetValue(candidateCore, out HashSet<int>? lookaheads))
                    {
                        lookaheads = [];
                        items[candidateCore] = lookaheads;
                    }

                    int before = lookaheads.Count;
                    lookaheads.UnionWith(follow);

                    if (lookaheads.Count > before)
                    {
                        pending.Enqueue(candidateCore);
                    }
                }
            }

            return items;
        }

        private void BuildCanonicalStates()
        {
            Dictionary<string, int> stateIndex = [];

            Dictionary<int, HashSet<int>> startKernel = new()
            {
                [0 << 8] = [_endId]
            };

            Dictionary<int, HashSet<int>> startState = Closure(startKernel);
            stateIndex[StateKey(startState)] = 0;
            _states.Add(startState);
            _transitions.Add([]);

            Queue<int> pending = new([0]);

            while (pending.Count > 0)
            {
                int current = pending.Dequeue();

                foreach (int symbol in OutgoingSymbols(_states[current]))
                {
                    Dictionary<int, HashSet<int>> kernel = [];

                    foreach ((int core, HashSet<int> lookaheads) in _states[current])
                    {
                        int prod = core >> 8;
                        int dot = core & 0xFF;
                        int[] rhs = _prodRhs[prod];

                        if (dot < rhs.Length && rhs[dot] == symbol)
                        {
                            kernel[(prod << 8) | (dot + 1)] = new HashSet<int>(lookaheads);
                        }
                    }

                    Dictionary<int, HashSet<int>> nextState = Closure(kernel);
                    string key = StateKey(nextState);

                    if (!stateIndex.TryGetValue(key, out int target))
                    {
                        target = _states.Count;
                        stateIndex[key] = target;
                        _states.Add(nextState);
                        _transitions.Add([]);
                        pending.Enqueue(target);
                    }

                    _transitions[current][symbol] = target;
                }
            }
        }

        private IEnumerable<int> OutgoingSymbols(Dictionary<int, HashSet<int>> state)
        {
            HashSet<int> symbols = [];

            foreach (int core in state.Keys)
            {
                int prod = core >> 8;
                int dot = core & 0xFF;
                int[] rhs = _prodRhs[prod];

                if (dot < rhs.Length)
                {
                    symbols.Add(rhs[dot]);
                }
            }

            return symbols.OrderBy(s => s);
        }

        private static string StateKey(Dictionary<int, HashSet<int>> state) =>
            string.Join("|", state
                .OrderBy(kv => kv.Key)
                .Select(kv => $"{kv.Key}:{string.Join(",", kv.Value.OrderBy(la => la))}"));

        private static string CoreKey(Dictionary<int, HashSet<int>> state) =>
            string.Join("|", state.Keys.OrderBy(core => core));

        private (List<Dictionary<int, HashSet<int>>>, List<Dictionary<int, int>>) MergeCores()
        {
            Dictionary<string, int> mergedIndexByCore = [];
            int[] remap = new int[_states.Count];
            List<Dictionary<int, HashSet<int>>> merged = [];

            for (int index = 0; index < _states.Count; index++)
            {
                string coreKey = CoreKey(_states[index]);

                if (!mergedIndexByCore.TryGetValue(coreKey, out int target))
                {
                    target = merged.Count;
                    mergedIndexByCore[coreKey] = target;
                    merged.Add(_states[index].ToDictionary(kv => kv.Key, kv => new HashSet<int>(kv.Value)));
                }
                else
                {
                    foreach ((int core, HashSet<int> lookaheads) in _states[index])
                    {
                        merged[target][core].UnionWith(lookaheads);
                    }
                }

                remap[index] = target;
            }

            List<Dictionary<int, int>> mergedTransitions = [];

            for (int index = 0; index < merged.Count; index++)
            {
                mergedTransitions.Add([]);
            }

            for (int index = 0; index < _states.Count; index++)
            {
                foreach ((int symbol, int target) in _transitions[index])
                {
                    mergedTransitions[remap[index]][symbol] = remap[target];
                }
            }

            return (merged, mergedTransitions);
        }

        private ParseTable BuildTable(
            List<Dictionary<int, HashSet<int>>> states,
            List<Dictionary<int, int>> transitions)
        {
            int stateCount = states.Count;
            ParseAction[,] actions = new ParseAction[stateCount, TerminalCount];
            int[,] gotos = new int[stateCount, _nonterminalNames.Count];

            for (int state = 0; state < stateCount; state++)
            {
                for (int nonterminal = 0; nonterminal < _nonterminalNames.Count; nonterminal++)
                {
                    gotos[state, nonterminal] = -1;
                }
            }

            for (int state = 0; state < stateCount; state++)
            {
                foreach ((int symbol, int target) in transitions[state])
                {
                    if (IsTerminal(symbol))
                    {
                        actions[state, symbol] = ParseAction.Shift(target);
                    }
                    else
                    {
                        gotos[state, DecodeNonterminal(symbol)] = target;
                    }
                }
            }

            for (int state = 0; state < stateCount; state++)
            {
                foreach ((int core, HashSet<int> lookaheads) in states[state])
                {
                    int prod = core >> 8;
                    int dot = core & 0xFF;

                    if (dot < _prodRhs[prod].Length)
                    {
                        continue;
                    }

                    foreach (int lookahead in lookaheads)
                    {
                        if (prod == 0)
                        {
                            if (lookahead == _endId)
                            {
                                actions[state, lookahead] = new ParseAction { Kind = ParseActionKind.Accept };
                            }

                            continue;
                        }

                        PlaceReduce(states, actions, state, lookahead, prod);
                    }
                }
            }

            return new ParseTable
            {
                TerminalNames = _terminalNames.ToArray(),
                TerminalIds = _terminalIds,
                Productions = BuildTableProductions(),
                Actions = actions,
                Gotos = gotos,
                EndTerminalId = _endId
            };
        }

        private void PlaceReduce(
            List<Dictionary<int, HashSet<int>>> states,
            ParseAction[,] actions,
            int state,
            int lookahead,
            int prod)
        {
            ParseAction existing = actions[state, lookahead];

            switch (existing.Kind)
            {
                case ParseActionKind.Error:
                    actions[state, lookahead] = ParseAction.Reduce(prod);
                    return;

                case ParseActionKind.Reduce:
                    _bag.Add(Diagnostic.Error(
                        $"reduce-reduce conflict on '{_terminalNames[lookahead]}' between " +
                        $"'{RenderProduction(existing.Target)}' and '{RenderProduction(prod)}'")
                        .WithNote(RenderState(states[state])));
                    return;

                case ParseActionKind.Shift:
                    actions[state, lookahead] = ResolveShiftReduce(states, state, lookahead, prod, existing);
                    return;
            }
        }

        private ParseAction ResolveShiftReduce(
            List<Dictionary<int, HashSet<int>>> states,
            int state,
            int lookahead,
            int prod,
            ParseAction shift)
        {
            TerminalInfo? terminal = _grammarSet.Terminals.GetValueOrDefault(_terminalNames[lookahead]);
            int shiftPrecedence = terminal?.PrecedenceLevel ?? -1;
            int reducePrecedence = EffectiveProductionPrecedence(prod);

            if (shiftPrecedence >= 0 && reducePrecedence >= 0)
            {
                if (reducePrecedence > shiftPrecedence)
                {
                    return ParseAction.Reduce(prod);
                }

                if (reducePrecedence < shiftPrecedence)
                {
                    return shift;
                }

                Associativity assoc = terminal!.Assoc != Associativity.Unspecified
                    ? terminal.Assoc
                    : EffectiveProductionAssociativity(prod);

                switch (assoc)
                {
                    case Associativity.Left:
                        return ParseAction.Reduce(prod);
                    case Associativity.Right:
                        return shift;
                    case Associativity.NonAssoc:
                        return new ParseAction { Kind = ParseActionKind.NonAssoc, Target = lookahead };
                }
            }

            _bag.Add(Diagnostic.Error(
                $"shift-reduce conflict on '{_terminalNames[lookahead]}': shift vs reduce " +
                $"'{RenderProduction(prod)}' (add %N precedence or %left/%right/%none to resolve)")
                .WithNote(RenderState(states[state])));

            return shift;
        }

        /// <summary>Explicit %N wins; otherwise yacc-style fallback to the last terminal's level.</summary>
        private int EffectiveProductionPrecedence(int prod)
        {
            Production? source = _prodSource[prod];

            if (source is null)
            {
                return -1;
            }

            if (source.PrecedenceLevel >= 0)
            {
                return source.PrecedenceLevel;
            }

            return LastTerminal(prod)?.PrecedenceLevel ?? -1;
        }

        private Associativity EffectiveProductionAssociativity(int prod)
        {
            Production? source = _prodSource[prod];

            if (source is null)
            {
                return Associativity.Unspecified;
            }

            if (source.Assoc != Associativity.Unspecified)
            {
                return source.Assoc;
            }

            return LastTerminal(prod)?.Assoc ?? Associativity.Unspecified;
        }

        private TerminalInfo? LastTerminal(int prod)
        {
            for (int index = _prodRhs[prod].Length - 1; index >= 0; index--)
            {
                int symbol = _prodRhs[prod][index];

                if (IsTerminal(symbol))
                {
                    return _grammarSet.Terminals.GetValueOrDefault(_terminalNames[symbol]);
                }
            }

            return null;
        }

        private List<TableProduction> BuildTableProductions()
        {
            List<TableProduction> productions =
            [
                new TableProduction
                {
                    Lhs = "S'",
                    LhsId = _nonterminalIds["S'"],
                    RhsLength = 1,
                    FieldNames = new string?[1],
                    Action = ProductionActionKind.PassThrough,
                    ValueIndex = 0
                }
            ];

            foreach (Production production in _grammarSet.Productions)
            {
                productions.Add(new TableProduction
                {
                    Lhs = production.Lhs,
                    LhsId = _nonterminalIds[production.Lhs],
                    RhsLength = production.Rhs.Count,
                    FieldNames = production.Rhs.Select(s => s.FieldName).ToArray(),
                    Action = production.Action,
                    ValueIndex = production.ValueIndex
                });
            }

            return productions;
        }

        private string RenderProduction(int prod)
        {
            string rhs = _prodRhs[prod].Length == 0
                ? "ε"
                : string.Join(" ", _prodRhs[prod].Select(SymbolName));

            return $"{_nonterminalNames[_prodLhs[prod]]} := {rhs}";
        }

        private string SymbolName(int symbol) =>
            IsTerminal(symbol) ? $"'{_terminalNames[symbol]}'" : _nonterminalNames[DecodeNonterminal(symbol)];

        private string RenderState(Dictionary<int, HashSet<int>> state)
        {
            StringBuilder builder = new();
            builder.AppendLine("conflicting item set:");

            foreach ((int core, HashSet<int> lookaheads) in state.OrderBy(kv => kv.Key))
            {
                int prod = core >> 8;
                int dot = core & 0xFF;
                int[] rhs = _prodRhs[prod];

                IEnumerable<string> before = rhs.Take(dot).Select(SymbolName);
                IEnumerable<string> after = rhs.Skip(dot).Select(SymbolName);
                string lookaheadList = string.Join(", ", lookaheads.OrderBy(la => la).Select(la => _terminalNames[la]));

                builder.AppendLine(
                    $"    {_nonterminalNames[_prodLhs[prod]]} := {string.Join(" ", before)} · {string.Join(" ", after)} [{lookaheadList}]");
            }

            return builder.ToString().TrimEnd();
        }
    }
}

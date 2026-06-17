namespace Grammar;

/// <summary>
/// Pre-table-construction validation per DESIGN.md: reachability from the start symbol and
/// terminal precedence consistency. (Undefined symbols are reported during desugaring,
/// conflicts during LALR table construction.)
/// </summary>
public static class GrammarValidator
{
    public static Result<GrammarSet> Run(GrammarSet grammarSet, string startSymbol, DiagnosticBag bag)
    {
        if (!grammarSet.Nonterminals.Contains(startSymbol))
        {
            bag.Add(Diagnostic.Error($"start symbol '{startSymbol}' is not defined by any grammar rule"));
            return bag.ToResult(grammarSet);
        }

        CheckReachability(grammarSet, startSymbol, bag);
        AssignTerminalPrecedence(grammarSet, bag);

        return bag.ToResult(grammarSet);
    }

    private static void CheckReachability(GrammarSet grammarSet, string startSymbol, DiagnosticBag bag)
    {
        HashSet<string> reachable = [startSymbol];
        Queue<string> pending = new([startSymbol]);

        while (pending.Count > 0)
        {
            string atom = pending.Dequeue();

            foreach (Production production in grammarSet.ProductionsOf(atom))
            {
                foreach (ProductionSymbol symbol in production.Rhs.Where(s => !s.IsTerminal))
                {
                    if (reachable.Add(symbol.Name))
                    {
                        pending.Enqueue(symbol.Name);
                    }
                }
            }
        }

        IEnumerable<string> unreachable = grammarSet.Nonterminals
            .Where(atom => !reachable.Contains(atom) && !atom.Contains('#'))
            .OrderBy(atom => atom);

        foreach (string atom in unreachable)
        {
            Production representative = grammarSet.ProductionsOf(atom).First();

            bag.Add(Diagnostic.Warning(
                $"rule '{atom}' is unreachable from start symbol '{startSymbol}'",
                representative.FilePath,
                representative.Span));
        }
    }

    /// <summary>
    /// Terminals inherit precedence/associativity from the annotated productions that use
    /// them; the LALR builder consults this when resolving shift-reduce conflicts.
    /// </summary>
    private static void AssignTerminalPrecedence(GrammarSet grammarSet, DiagnosticBag bag)
    {
        foreach (Production production in grammarSet.Productions)
        {
            if (production.PrecedenceLevel < 0 && production.Assoc == Associativity.Unspecified)
            {
                continue;
            }

            foreach (ProductionSymbol symbol in production.Rhs.Where(s => s.IsTerminal))
            {
                TerminalInfo terminal = grammarSet.Terminals[symbol.Name];

                bool levelConflict = terminal.PrecedenceLevel >= 0
                    && production.PrecedenceLevel >= 0
                    && terminal.PrecedenceLevel != production.PrecedenceLevel;

                bool assocConflict = terminal.Assoc != Associativity.Unspecified
                    && production.Assoc != Associativity.Unspecified
                    && terminal.Assoc != production.Assoc;

                if (levelConflict || assocConflict)
                {
                    bag.Add(Diagnostic.Error(
                        $"conflicting precedence for terminal '{symbol.Name}': " +
                        $"declared %{terminal.PrecedenceLevel} here but %{production.PrecedenceLevel} elsewhere",
                        production.FilePath,
                        production.Span));
                    continue;
                }

                if (production.PrecedenceLevel >= 0)
                {
                    terminal.PrecedenceLevel = production.PrecedenceLevel;
                }

                if (production.Assoc != Associativity.Unspecified)
                {
                    terminal.Assoc = production.Assoc;
                }
            }
        }
    }
}

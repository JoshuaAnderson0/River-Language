namespace Grammar;

/// <summary>
/// Lowers parsed grammar rules (EBNF with capture annotations) into plain BNF productions
/// the LALR builder can consume. Quantifiers become synthetic left-recursive productions
/// (ATOM#repN / ATOM#optN) whose actions thread list/optional values back to the parent
/// production's fields.
/// </summary>
public static class Desugarer
{
    private static readonly Dictionary<string, BuiltinClass> BuiltinClasses = new()
    {
        ["NUMBER"] = BuiltinClass.Number,
        ["FLOAT"] = BuiltinClass.Float,
        ["IDENTIFIER"] = BuiltinClass.Identifier,
        ["VERSION"] = BuiltinClass.Version,
        ["NEWLINE"] = BuiltinClass.Newline
    };

    public static Result<GrammarSet> Run(List<GrammarRule> rules, DiagnosticBag bag)
    {
        Context context = new()
        {
            Bag = bag,
            Nonterminals = rules.Select(rule => rule.AtomName).ToHashSet()
        };

        foreach (GrammarRule rule in rules)
        {
            foreach (List<RuleElement> sequence in rule.Alternatives)
            {
                BuildProduction(context, rule, sequence);
            }
        }

        for (int index = 0; index < context.Productions.Count; index++)
        {
            context.Productions[index].Id = index;
        }

        GrammarSet grammarSet = new()
        {
            Productions = context.Productions,
            Terminals = context.Terminals,
            Nonterminals = context.Nonterminals
        };

        return bag.ToResult(grammarSet);
    }

    private class Context
    {
        public required DiagnosticBag Bag;
        public required HashSet<string> Nonterminals;
        public readonly List<Production> Productions = [];
        public readonly Dictionary<string, TerminalInfo> Terminals = [];
        public readonly Dictionary<string, int> SyntheticCounters = [];
    }

    /// <summary>A lowered RHS symbol plus the metadata the action/naming decisions need.</summary>
    private class LoweredSymbol
    {
        public required ProductionSymbol Symbol;
        public bool WasAliased;
        public bool WasQuantified;
    }

    private static void BuildProduction(Context context, GrammarRule rule, List<RuleElement> sequence)
    {
        List<LoweredSymbol> lowered = [];

        foreach (RuleElement element in sequence)
        {
            LoweredSymbol? symbol = LowerElement(context, rule, element);

            if (symbol is null)
            {
                return; // diagnostic already reported; skip this production, keep going
            }

            lowered.Add(symbol);
        }

        if (!ResolveFieldNames(context, rule, lowered))
        {
            return;
        }

        Production production = new()
        {
            Lhs = rule.AtomName,
            Rhs = lowered.Select(s => s.Symbol).ToList(),
            PrecedenceLevel = rule.PrecedenceLevel,
            Assoc = rule.Assoc,
            FilePath = rule.FilePath,
            Span = rule.Span
        };

        // Pass-through rule: exactly one unaliased, unquantified capture and nothing else
        // captured means the child node IS the result (EXPR := @ADD, PAREN := '(' @EXPR ')').
        List<int> capturedIndices = Enumerable.Range(0, lowered.Count)
            .Where(i => lowered[i].Symbol.FieldName is not null)
            .ToList();

        if (capturedIndices.Count == 1
            && !lowered[capturedIndices[0]].WasAliased
            && !lowered[capturedIndices[0]].WasQuantified)
        {
            production.Action = ProductionActionKind.PassThrough;
            production.ValueIndex = capturedIndices[0];
        }
        else
        {
            production.Action = ProductionActionKind.Construct;
        }

        context.Productions.Add(production);
    }

    private static LoweredSymbol? LowerElement(Context context, GrammarRule rule, RuleElement element)
    {
        LoweredSymbol? primary = element.Kind switch
        {
            ElementKind.LiteralText => LowerLiteral(context, element),
            ElementKind.Reference => LowerReference(context, rule, element),
            ElementKind.Group => LowerGroup(context, rule, element),
            _ => null
        };

        if (primary is null || element.Quantifier == ElementQuantifier.One)
        {
            return primary;
        }

        return WrapQuantified(context, rule, element, primary);
    }

    private static LoweredSymbol LowerLiteral(Context context, RuleElement element)
    {
        string text = element.Literal!;

        if (!context.Terminals.ContainsKey(text))
        {
            context.Terminals[text] = new TerminalInfo { Name = text, IsLiteral = true };
        }

        return new LoweredSymbol
        {
            Symbol = new ProductionSymbol { Name = text, IsTerminal = true, FieldName = null }
        };
    }

    private static LoweredSymbol? LowerReference(Context context, GrammarRule rule, RuleElement element)
    {
        string name = element.Name!;
        bool isTerminal;

        if (context.Nonterminals.Contains(name))
        {
            isTerminal = false;
        }
        else if (BuiltinClasses.TryGetValue(name, out BuiltinClass builtin))
        {
            isTerminal = true;

            if (!context.Terminals.ContainsKey(name))
            {
                context.Terminals[name] = new TerminalInfo
                {
                    Name = name,
                    IsLiteral = false,
                    Builtin = builtin
                };
            }
        }
        else
        {
            context.Bag.Add(Diagnostic.Error(
                $"undefined symbol '{name}': not a defined atom or builtin terminal class",
                rule.FilePath,
                element.Span));
            return null;
        }

        string? fieldName = element.Capture == ReferenceCapture.Capture
            ? element.Alias ?? name.ToLowerInvariant()
            : null;

        return new LoweredSymbol
        {
            Symbol = new ProductionSymbol { Name = name, IsTerminal = isTerminal, FieldName = fieldName },
            WasAliased = element.Alias is not null
        };
    }

    private static LoweredSymbol? LowerGroup(Context context, GrammarRule rule, RuleElement element)
    {
        if (element.GroupAlternatives!.Any(sequence => sequence.Any(ContainsCapture)))
        {
            context.Bag.Add(Diagnostic.Error(
                "captures inside groups are not yet supported; extract the group into its own atom",
                rule.FilePath,
                element.Span));
            return null;
        }

        string groupName = NextSyntheticName(context, rule.AtomName, "grp");
        context.Nonterminals.Add(groupName);

        foreach (List<RuleElement> sequence in element.GroupAlternatives!)
        {
            List<ProductionSymbol> rhs = [];

            foreach (RuleElement inner in sequence)
            {
                LoweredSymbol? loweredInner = LowerElement(context, rule, inner);

                if (loweredInner is null)
                {
                    return null;
                }

                rhs.Add(loweredInner.Symbol);
            }

            context.Productions.Add(new Production
            {
                Lhs = groupName,
                Rhs = rhs,
                Action = ProductionActionKind.EmptyOptional,
                FilePath = rule.FilePath,
                Span = element.Span
            });
        }

        return new LoweredSymbol
        {
            Symbol = new ProductionSymbol { Name = groupName, IsTerminal = false, FieldName = null }
        };
    }

    private static LoweredSymbol WrapQuantified(
        Context context,
        GrammarRule rule,
        RuleElement element,
        LoweredSymbol primary)
    {
        ProductionSymbol item = new()
        {
            Name = primary.Symbol.Name,
            IsTerminal = primary.Symbol.IsTerminal,
            FieldName = null
        };

        string syntheticName;

        if (element.Quantifier == ElementQuantifier.Optional)
        {
            syntheticName = NextSyntheticName(context, rule.AtomName, "opt");
            context.Nonterminals.Add(syntheticName);

            context.Productions.Add(new Production
            {
                Lhs = syntheticName,
                Rhs = [],
                Action = ProductionActionKind.EmptyOptional,
                FilePath = rule.FilePath,
                Span = element.Span
            });

            context.Productions.Add(new Production
            {
                Lhs = syntheticName,
                Rhs = [item],
                Action = ProductionActionKind.PassThrough,
                ValueIndex = 0,
                FilePath = rule.FilePath,
                Span = element.Span
            });
        }
        else
        {
            syntheticName = NextSyntheticName(context, rule.AtomName, "rep");
            context.Nonterminals.Add(syntheticName);

            ProductionSymbol recurse = new() { Name = syntheticName, IsTerminal = false, FieldName = null };

            if (element.Quantifier == ElementQuantifier.ZeroOrMore)
            {
                context.Productions.Add(new Production
                {
                    Lhs = syntheticName,
                    Rhs = [],
                    Action = ProductionActionKind.EmptyList,
                    FilePath = rule.FilePath,
                    Span = element.Span
                });
            }
            else
            {
                context.Productions.Add(new Production
                {
                    Lhs = syntheticName,
                    Rhs = [item],
                    Action = ProductionActionKind.NewList,
                    ValueIndex = 0,
                    FilePath = rule.FilePath,
                    Span = element.Span
                });
            }

            context.Productions.Add(new Production
            {
                Lhs = syntheticName,
                Rhs = [recurse, item],
                Action = ProductionActionKind.AppendList,
                ValueIndex = 1,
                FilePath = rule.FilePath,
                Span = element.Span
            });
        }

        return new LoweredSymbol
        {
            Symbol = new ProductionSymbol
            {
                Name = syntheticName,
                IsTerminal = false,
                FieldName = primary.Symbol.FieldName
            },
            WasAliased = primary.WasAliased,
            WasQuantified = true
        };
    }

    /// <summary>
    /// Auto-named captures that collide get positional suffixes (@EXPR '+' @EXPR -> expr0,
    /// expr1); colliding aliases are an error since the author named them deliberately.
    /// </summary>
    private static bool ResolveFieldNames(Context context, GrammarRule rule, List<LoweredSymbol> lowered)
    {
        List<LoweredSymbol> captured = lowered.Where(s => s.Symbol.FieldName is not null).ToList();

        foreach (IGrouping<string, LoweredSymbol> collision in captured
                     .GroupBy(s => s.Symbol.FieldName!)
                     .Where(group => group.Count() > 1))
        {
            if (collision.Any(s => s.WasAliased))
            {
                context.Bag.Add(Diagnostic.Error(
                    $"duplicate field name '{collision.Key}' in rule '{rule.AtomName}'",
                    rule.FilePath,
                    rule.Span));
                return false;
            }

            int suffix = 0;

            foreach (LoweredSymbol symbol in collision)
            {
                symbol.Symbol.FieldName = $"{collision.Key}{suffix}";
                suffix++;
            }
        }

        return true;
    }

    private static string NextSyntheticName(Context context, string atomName, string kind)
    {
        int counter = context.SyntheticCounters.GetValueOrDefault(atomName);
        context.SyntheticCounters[atomName] = counter + 1;
        return $"{atomName}#{kind}{counter}";
    }

    private static bool ContainsCapture(RuleElement element) =>
        element.Capture == ReferenceCapture.Capture
        || (element.GroupAlternatives?.Any(sequence => sequence.Any(ContainsCapture)) ?? false);
}

using System.Globalization;
using Parsing;

namespace Ir;

/// <summary>
/// Lowers the parse tree to typed linear IR. Types are I64/F64 with int-to-float
/// promotion on mixed arithmetic; expression types are computed by a pure pre-pass
/// (TypeOf) so promotions land directly after the operand that needs them.
///
/// Re-binding a name allocates a fresh slot (shadowing, Rust let-style); slots are never
/// retyped. Undefined identifiers report a diagnostic and recover as I64 zero so later
/// errors still surface in the same compile.
/// </summary>
public static class IrGenerator
{
    public static Result<IrProgram> Run(SyntaxNode program, string filePath, DiagnosticBag bag)
    {
        Generator generator = new(filePath, bag);
        return bag.ToResult(generator.Generate(program));
    }

    private class LocalSymbol
    {
        public required int Slot;
        public required IrType Type;
    }

    private class Generator
    {
        private readonly string _filePath;
        private readonly DiagnosticBag _bag;
        private readonly List<IrOp> _ops = [];
        private readonly List<IrType> _localTypes = [];
        private readonly Dictionary<string, LocalSymbol> _locals = [];

        public Generator(string filePath, DiagnosticBag bag)
        {
            _filePath = filePath;
            _bag = bag;
        }

        public IrProgram Generate(SyntaxNode program)
        {
            foreach (SyntaxNode statement in program.List("statement"))
            {
                EmitStatement(statement);
            }

            _ops.Add(new IrOp { Code = IrOpCode.Exit, Span = program.Span });
            return new IrProgram { Ops = _ops, LocalTypes = _localTypes };
        }

        private void EmitStatement(SyntaxNode statement)
        {
            switch (statement.Atom)
            {
                case "BINDING":
                    EmitBinding(statement);
                    break;

                case "PRINT":
                    EmitPrint(statement);
                    break;

                default:
                    _bag.Add(Diagnostic.Error(
                        $"statement '{statement.Atom}' is not supported yet",
                        _filePath,
                        statement.Span));
                    break;
            }
        }

        private void EmitBinding(SyntaxNode binding)
        {
            SyntaxNode value = binding.Single("value")!;
            string name = binding.Single("name")!.TokenText;

            IrType type = EmitExpression(value);

            int slot = _localTypes.Count;
            _localTypes.Add(type);
            _locals[name] = new LocalSymbol { Slot = slot, Type = type };

            _ops.Add(new IrOp { Code = IrOpCode.StoreLocal, Slot = slot, Type = type, Span = binding.Span });
        }

        private void EmitPrint(SyntaxNode print)
        {
            SyntaxNode value = print.Single("value")!;
            IrType type = EmitExpression(value);

            _ops.Add(new IrOp { Code = IrOpCode.Print, Type = type, Span = print.Span });
        }

        private IrType EmitExpression(SyntaxNode expression)
        {
            switch (expression.Atom)
            {
                case "NUMBER":
                    return EmitIntLiteral(expression);

                case "FLOAT":
                    return EmitFloatLiteral(expression);

                case "IDENTIFIER":
                    return EmitLoad(expression);

                case "ADD":
                    return EmitBinary(expression, IrOpCode.Add);

                case "SUB":
                    return EmitBinary(expression, IrOpCode.Sub);

                case "MUL":
                    return EmitBinary(expression, IrOpCode.Mul);

                case "DIV":
                    return EmitBinary(expression, IrOpCode.Div);

                default:
                    _bag.Add(Diagnostic.Error(
                        $"expression '{expression.Atom}' is not supported yet",
                        _filePath,
                        expression.Span));
                    return RecoverAsZero(expression);
            }
        }

        private IrType EmitIntLiteral(SyntaxNode literal)
        {
            if (!long.TryParse(literal.TokenText, NumberStyles.None, CultureInfo.InvariantCulture, out long value))
            {
                _bag.Add(Diagnostic.Error(
                    $"integer literal '{literal.TokenText}' does not fit in 64 bits",
                    _filePath,
                    literal.Span));
                value = 0;
            }

            _ops.Add(new IrOp { Code = IrOpCode.PushInt, IntValue = value, Span = literal.Span });
            return IrType.I64;
        }

        private IrType EmitFloatLiteral(SyntaxNode literal)
        {
            if (!double.TryParse(literal.TokenText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                _bag.Add(Diagnostic.Error(
                    $"invalid float literal '{literal.TokenText}'",
                    _filePath,
                    literal.Span));
                value = 0;
            }

            _ops.Add(new IrOp { Code = IrOpCode.PushFloat, FloatValue = value, Span = literal.Span });
            return IrType.F64;
        }

        private IrType EmitLoad(SyntaxNode identifier)
        {
            if (!_locals.TryGetValue(identifier.TokenText, out LocalSymbol? local))
            {
                _bag.Add(Diagnostic.Error(
                    $"undefined variable '{identifier.TokenText}'",
                    _filePath,
                    identifier.Span));
                return RecoverAsZero(identifier);
            }

            _ops.Add(new IrOp { Code = IrOpCode.LoadLocal, Slot = local.Slot, Type = local.Type, Span = identifier.Span });
            return local.Type;
        }

        private IrType EmitBinary(SyntaxNode binary, IrOpCode code)
        {
            SyntaxNode lhs = binary.Single("lhs")!;
            SyntaxNode rhs = binary.Single("rhs")!;

            IrType resultType = TypeOf(lhs) == IrType.F64 || TypeOf(rhs) == IrType.F64
                ? IrType.F64
                : IrType.I64;

            IrType lhsType = EmitExpression(lhs);

            if (lhsType == IrType.I64 && resultType == IrType.F64)
            {
                _ops.Add(new IrOp { Code = IrOpCode.IntToFloat, Span = lhs.Span });
            }

            IrType rhsType = EmitExpression(rhs);

            if (rhsType == IrType.I64 && resultType == IrType.F64)
            {
                _ops.Add(new IrOp { Code = IrOpCode.IntToFloat, Span = rhs.Span });
            }

            _ops.Add(new IrOp { Code = code, Type = resultType, Span = binary.Span });
            return resultType;
        }

        /// <summary>
        /// Pure type pre-pass: expression types depend only on literals and already-bound
        /// locals, so this never emits and stays in sync with EmitExpression.
        /// </summary>
        private IrType TypeOf(SyntaxNode expression) => expression.Atom switch
        {
            "FLOAT" => IrType.F64,
            "NUMBER" => IrType.I64,
            "IDENTIFIER" => _locals.GetValueOrDefault(expression.TokenText)?.Type ?? IrType.I64,
            "ADD" or "SUB" or "MUL" or "DIV" =>
                TypeOf(expression.Single("lhs")!) == IrType.F64 || TypeOf(expression.Single("rhs")!) == IrType.F64
                    ? IrType.F64
                    : IrType.I64,
            _ => IrType.I64
        };

        private IrType RecoverAsZero(SyntaxNode at)
        {
            _ops.Add(new IrOp { Code = IrOpCode.PushInt, IntValue = 0, Span = at.Span });
            return IrType.I64;
        }
    }
}

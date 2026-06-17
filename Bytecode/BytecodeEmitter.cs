using Ir;

namespace Bytecode;

/// <summary>
/// 1:1 translation from IR to a bytecode chunk. Constants are pooled with deduplication
/// keyed by (kind, raw bits). Operands are little-endian u16.
/// </summary>
public static class BytecodeEmitter
{
    public static Result<Chunk> Run(IrProgram program)
    {
        List<byte> code = [];
        List<Value> constants = [];
        Dictionary<(ValueKind, long), ushort> constantIndex = [];

        foreach (IrOp op in program.Ops)
        {
            switch (op.Code)
            {
                case IrOpCode.PushInt:
                    Emit(code, OpCode.PushConst, PoolConstant(constants, constantIndex, Value.Int(op.IntValue)));
                    break;

                case IrOpCode.PushFloat:
                    Emit(code, OpCode.PushConst, PoolConstant(constants, constantIndex, Value.Float(op.FloatValue)));
                    break;

                case IrOpCode.LoadLocal:
                    Emit(code, OpCode.LoadLocal, (ushort)op.Slot);
                    break;

                case IrOpCode.StoreLocal:
                    Emit(code, OpCode.StoreLocal, (ushort)op.Slot);
                    break;

                case IrOpCode.Add:
                    code.Add((byte)(op.Type == IrType.I64 ? OpCode.AddI : OpCode.AddF));
                    break;

                case IrOpCode.Sub:
                    code.Add((byte)(op.Type == IrType.I64 ? OpCode.SubI : OpCode.SubF));
                    break;

                case IrOpCode.Mul:
                    code.Add((byte)(op.Type == IrType.I64 ? OpCode.MulI : OpCode.MulF));
                    break;

                case IrOpCode.Div:
                    code.Add((byte)(op.Type == IrType.I64 ? OpCode.DivI : OpCode.DivF));
                    break;

                case IrOpCode.IntToFloat:
                    code.Add((byte)OpCode.I2F);
                    break;

                case IrOpCode.Print:
                    code.Add((byte)(op.Type == IrType.I64 ? OpCode.PrintI : OpCode.PrintF));
                    break;

                case IrOpCode.Exit:
                    code.Add((byte)OpCode.Halt);
                    break;

                default:
                    return Result<Chunk>.Error($"bytecode emitter: unhandled IR op {op.Code}");
            }
        }

        return Result<Chunk>.Ok(new Chunk
        {
            Code = code.ToArray(),
            Constants = constants.ToArray(),
            LocalCount = program.LocalTypes.Count
        });
    }

    private static ushort PoolConstant(
        List<Value> constants,
        Dictionary<(ValueKind, long), ushort> index,
        Value value)
    {
        (ValueKind, long) key = (value.Kind, value.Kind == ValueKind.I64
            ? value.I64
            : BitConverter.DoubleToInt64Bits(value.F64));

        if (index.TryGetValue(key, out ushort existing))
        {
            return existing;
        }

        ushort slot = (ushort)constants.Count;
        constants.Add(value);
        index[key] = slot;
        return slot;
    }

    private static void Emit(List<byte> code, OpCode opCode, ushort operand)
    {
        code.Add((byte)opCode);
        code.Add((byte)(operand & 0xFF));
        code.Add((byte)(operand >> 8));
    }
}

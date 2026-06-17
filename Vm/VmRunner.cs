using Bytecode;

namespace Vm;

/// <summary>
/// Stack-machine bytecode interpreter. Output goes to the provided TextWriter so tests
/// can capture it; the returned value is the process exit code.
/// </summary>
public static class VmRunner
{
    public static Result<int> Run(Chunk chunk, TextWriter stdout)
    {
        Value[] stack = new Value[256];
        int stackPointer = 0;
        Value[] locals = new Value[chunk.LocalCount];
        byte[] code = chunk.Code;
        int instructionPointer = 0;

        while (instructionPointer < code.Length)
        {
            OpCode opCode = (OpCode)code[instructionPointer];
            instructionPointer++;

            switch (opCode)
            {
                case OpCode.PushConst:
                    EnsureCapacity(ref stack, stackPointer);
                    stack[stackPointer] = chunk.Constants[ReadU16(code, ref instructionPointer)];
                    stackPointer++;
                    break;

                case OpCode.LoadLocal:
                    EnsureCapacity(ref stack, stackPointer);
                    stack[stackPointer] = locals[ReadU16(code, ref instructionPointer)];
                    stackPointer++;
                    break;

                case OpCode.StoreLocal:
                    stackPointer--;
                    locals[ReadU16(code, ref instructionPointer)] = stack[stackPointer];
                    break;

                case OpCode.AddI:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Int(stack[stackPointer - 1].I64 + stack[stackPointer].I64);
                    break;

                case OpCode.SubI:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Int(stack[stackPointer - 1].I64 - stack[stackPointer].I64);
                    break;

                case OpCode.MulI:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Int(stack[stackPointer - 1].I64 * stack[stackPointer].I64);
                    break;

                case OpCode.DivI:
                    stackPointer--;

                    if (stack[stackPointer].I64 == 0)
                    {
                        return Result<int>.Error("runtime error: integer division by zero");
                    }

                    stack[stackPointer - 1] = Value.Int(stack[stackPointer - 1].I64 / stack[stackPointer].I64);
                    break;

                case OpCode.AddF:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Float(stack[stackPointer - 1].F64 + stack[stackPointer].F64);
                    break;

                case OpCode.SubF:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Float(stack[stackPointer - 1].F64 - stack[stackPointer].F64);
                    break;

                case OpCode.MulF:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Float(stack[stackPointer - 1].F64 * stack[stackPointer].F64);
                    break;

                case OpCode.DivF:
                    stackPointer--;
                    stack[stackPointer - 1] = Value.Float(stack[stackPointer - 1].F64 / stack[stackPointer].F64);
                    break;

                case OpCode.I2F:
                    stack[stackPointer - 1] = Value.Float(stack[stackPointer - 1].I64);
                    break;

                case OpCode.PrintI:
                    stackPointer--;
                    stdout.Write(PrintFormat.Int(stack[stackPointer].I64));
                    stdout.Write('\n');
                    break;

                case OpCode.PrintF:
                    stackPointer--;
                    stdout.Write(PrintFormat.Float(stack[stackPointer].F64));
                    stdout.Write('\n');
                    break;

                case OpCode.Halt:
                    return Result<int>.Ok(0);

                default:
                    return Result<int>.Error($"runtime error: unknown opcode {opCode}");
            }
        }

        return Result<int>.Ok(0);
    }

    private static ushort ReadU16(byte[] code, ref int instructionPointer)
    {
        ushort value = (ushort)(code[instructionPointer] | (code[instructionPointer + 1] << 8));
        instructionPointer += 2;
        return value;
    }

    private static void EnsureCapacity(ref Value[] stack, int stackPointer)
    {
        if (stackPointer >= stack.Length)
        {
            Array.Resize(ref stack, stack.Length * 2);
        }
    }
}

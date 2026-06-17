namespace Bytecode;

public enum OpCode : byte
{
    /// <summary>Operand: u16 constant pool index.</summary>
    PushConst,

    /// <summary>Operand: u16 local slot.</summary>
    LoadLocal,

    /// <summary>Operand: u16 local slot.</summary>
    StoreLocal,

    AddI,
    SubI,
    MulI,
    DivI,
    AddF,
    SubF,
    MulF,
    DivF,
    I2F,
    PrintI,
    PrintF,
    Halt
}

public enum ValueKind : byte
{
    I64,
    F64
}

public struct Value
{
    public ValueKind Kind;
    public long I64;
    public double F64;

    public static Value Int(long value) => new() { Kind = ValueKind.I64, I64 = value };

    public static Value Float(double value) => new() { Kind = ValueKind.F64, F64 = value };
}

public sealed class Chunk
{
    public required byte[] Code;
    public required Value[] Constants;
    public required int LocalCount;
}

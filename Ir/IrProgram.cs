namespace Ir;

public enum IrType
{
    I64,
    F64
}

public enum IrOpCode
{
    PushInt,
    PushFloat,
    LoadLocal,
    StoreLocal,
    Add,
    Sub,
    Mul,
    Div,
    IntToFloat,
    Print,
    Exit
}

/// <summary>
/// One op of the linear stack IR. Arithmetic and Print use <see cref="Type"/> to select
/// the I64/F64 variant; locals live in numbered slots.
/// </summary>
public sealed class IrOp
{
    public required IrOpCode Code;
    public long IntValue;
    public double FloatValue;
    public int Slot;
    public IrType Type;
    public SourceSpan Span;

    public override string ToString() => Code switch
    {
        IrOpCode.PushInt => $"PushInt {IntValue}",
        IrOpCode.PushFloat => $"PushFloat {FloatValue}",
        IrOpCode.LoadLocal => $"LoadLocal {Slot}",
        IrOpCode.StoreLocal => $"StoreLocal {Slot}",
        IrOpCode.Print => $"Print.{Type}",
        IrOpCode.Add or IrOpCode.Sub or IrOpCode.Mul or IrOpCode.Div => $"{Code}.{Type}",
        _ => Code.ToString()
    };
}

public sealed class IrProgram
{
    public required List<IrOp> Ops;
    public required List<IrType> LocalTypes;
}

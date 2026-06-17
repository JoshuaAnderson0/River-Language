namespace Asm;

public enum Mnemonic
{
    PushReg,
    PopReg,
    MovRegImm64,
    MovRegReg,
    MovRegMem,
    MovMemReg,
    AddRegReg,
    SubRegReg,
    ImulRegReg,
    Cqo,
    IdivReg,
    SubRspImm,
    AddRspImm,
    MovsdXmmMem,
    MovsdMemXmm,
    AddsdXmmXmm,
    SubsdXmmXmm,
    MulsdXmmXmm,
    DivsdXmmXmm,
    Cvtsi2sdXmmMem,
    LeaRipRel,
    CallRipIndirect,
    XorReg32Reg32,
    Ret
}

/// <summary>Register numbers match x64 encoding (rd / ModRM fields).</summary>
public enum Reg
{
    Rax = 0,
    Rcx = 1,
    Rdx = 2,
    Rbx = 3,
    Rsp = 4,
    Rbp = 5,
    Rsi = 6,
    Rdi = 7
}

public enum XmmReg
{
    Xmm0 = 0,
    Xmm1 = 1
}

/// <summary>Things only the linker can place; rip-relative operands reference these.</summary>
public enum SymbolRef
{
    None,
    FmtInt,
    FmtFloat,
    ImpPrintf,
    ImpExitProcess
}

public sealed class MemOperand
{
    public required Reg Base;
    public int Disp;
}

public sealed class Instruction
{
    public required Mnemonic M;
    public Reg R1;
    public Reg R2;
    public XmmReg X1;
    public XmmReg X2;
    public MemOperand? Mem;
    public long Imm;
    public SymbolRef Symbol = SymbolRef.None;
}

public sealed class AsmProgram
{
    public required List<Instruction> Text;
}

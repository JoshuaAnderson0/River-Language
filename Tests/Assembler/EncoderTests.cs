using Asm;
using Assembler;

namespace Tests.Assembler;

/// <summary>
/// Byte-exact encoder tests. Expected encodings were cross-checked against a reference
/// disassembler during development and are frozen here.
/// </summary>
public class EncoderTests
{
    private static byte[] Encode(Instruction instruction)
    {
        List<byte> text = [];
        List<Fixup> fixups = [];
        Result result = Encoder.Encode(instruction, text, fixups);

        Assert.True(result.IsOk, result.Message);
        return text.ToArray();
    }

    [Fact]
    public void PushPopRegisters()
    {
        Assert.Equal([0x55], Encode(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rbp }));
        Assert.Equal([0x50], Encode(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rax }));
        Assert.Equal([0x58], Encode(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rax }));
        Assert.Equal([0x59], Encode(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rcx }));
        Assert.Equal([0x5A], Encode(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rdx }));
    }

    [Fact]
    public void MovImmediateAndRegisterForms()
    {
        Assert.Equal(
            [0x48, 0xB8, 0x07, 0, 0, 0, 0, 0, 0, 0],
            Encode(new Instruction { M = Mnemonic.MovRegImm64, R1 = Reg.Rax, Imm = 7 }));

        // mov rbp, rsp
        Assert.Equal(
            [0x48, 0x89, 0xE5],
            Encode(new Instruction { M = Mnemonic.MovRegReg, R1 = Reg.Rbp, R2 = Reg.Rsp }));
    }

    [Fact]
    public void MovLocalSlotForms()
    {
        // mov rax, [rbp-8]
        Assert.Equal(
            [0x48, 0x8B, 0x45, 0xF8],
            Encode(new Instruction
            {
                M = Mnemonic.MovRegMem, R1 = Reg.Rax, Mem = new MemOperand { Base = Reg.Rbp, Disp = -8 }
            }));

        // mov [rbp-8], rax
        Assert.Equal(
            [0x48, 0x89, 0x45, 0xF8],
            Encode(new Instruction
            {
                M = Mnemonic.MovMemReg, R1 = Reg.Rax, Mem = new MemOperand { Base = Reg.Rbp, Disp = -8 }
            }));

        // mov rax, [rbp-1024] — disp32 form for far slots
        Assert.Equal(
            [0x48, 0x8B, 0x85, 0x00, 0xFC, 0xFF, 0xFF],
            Encode(new Instruction
            {
                M = Mnemonic.MovRegMem, R1 = Reg.Rax, Mem = new MemOperand { Base = Reg.Rbp, Disp = -1024 }
            }));

        // mov rdx, [rsp] — rsp base needs a SIB byte
        Assert.Equal(
            [0x48, 0x8B, 0x14, 0x24],
            Encode(new Instruction
            {
                M = Mnemonic.MovRegMem, R1 = Reg.Rdx, Mem = new MemOperand { Base = Reg.Rsp }
            }));
    }

    [Fact]
    public void IntegerArithmetic()
    {
        Assert.Equal([0x48, 0x01, 0xC8], Encode(new Instruction { M = Mnemonic.AddRegReg, R1 = Reg.Rax, R2 = Reg.Rcx }));
        Assert.Equal([0x48, 0x29, 0xC8], Encode(new Instruction { M = Mnemonic.SubRegReg, R1 = Reg.Rax, R2 = Reg.Rcx }));
        Assert.Equal([0x48, 0x0F, 0xAF, 0xC1], Encode(new Instruction { M = Mnemonic.ImulRegReg, R1 = Reg.Rax, R2 = Reg.Rcx }));
        Assert.Equal([0x48, 0x99], Encode(new Instruction { M = Mnemonic.Cqo }));
        Assert.Equal([0x48, 0xF7, 0xF9], Encode(new Instruction { M = Mnemonic.IdivReg, R1 = Reg.Rcx }));
    }

    [Fact]
    public void StackPointerAdjustments()
    {
        Assert.Equal([0x48, 0x83, 0xEC, 0x20], Encode(new Instruction { M = Mnemonic.SubRspImm, Imm = 32 }));
        Assert.Equal([0x48, 0x83, 0xC4, 0x28], Encode(new Instruction { M = Mnemonic.AddRspImm, Imm = 40 }));

        // imm32 form past the sbyte range
        Assert.Equal(
            [0x48, 0x81, 0xEC, 0x00, 0x01, 0x00, 0x00],
            Encode(new Instruction { M = Mnemonic.SubRspImm, Imm = 256 }));
    }

    [Fact]
    public void SseLoadsStoresAndArithmetic()
    {
        Assert.Equal(
            [0xF2, 0x0F, 0x10, 0x04, 0x24],
            Encode(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm0, Mem = new MemOperand { Base = Reg.Rsp } }));

        Assert.Equal(
            [0xF2, 0x0F, 0x10, 0x0C, 0x24],
            Encode(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm1, Mem = new MemOperand { Base = Reg.Rsp } }));

        Assert.Equal(
            [0xF2, 0x0F, 0x10, 0x44, 0x24, 0x08],
            Encode(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm0, Mem = new MemOperand { Base = Reg.Rsp, Disp = 8 } }));

        Assert.Equal(
            [0xF2, 0x0F, 0x11, 0x04, 0x24],
            Encode(new Instruction { M = Mnemonic.MovsdMemXmm, X1 = XmmReg.Xmm0, Mem = new MemOperand { Base = Reg.Rsp } }));

        Assert.Equal([0xF2, 0x0F, 0x58, 0xC1], Encode(new Instruction { M = Mnemonic.AddsdXmmXmm, X1 = XmmReg.Xmm0, X2 = XmmReg.Xmm1 }));
        Assert.Equal([0xF2, 0x0F, 0x5C, 0xC1], Encode(new Instruction { M = Mnemonic.SubsdXmmXmm, X1 = XmmReg.Xmm0, X2 = XmmReg.Xmm1 }));
        Assert.Equal([0xF2, 0x0F, 0x59, 0xC1], Encode(new Instruction { M = Mnemonic.MulsdXmmXmm, X1 = XmmReg.Xmm0, X2 = XmmReg.Xmm1 }));
        Assert.Equal([0xF2, 0x0F, 0x5E, 0xC1], Encode(new Instruction { M = Mnemonic.DivsdXmmXmm, X1 = XmmReg.Xmm0, X2 = XmmReg.Xmm1 }));

        // REX.W sits between the F2 prefix and the 0F escape.
        Assert.Equal(
            [0xF2, 0x48, 0x0F, 0x2A, 0x04, 0x24],
            Encode(new Instruction { M = Mnemonic.Cvtsi2sdXmmMem, X1 = XmmReg.Xmm0, Mem = new MemOperand { Base = Reg.Rsp } }));
    }

    [Fact]
    public void RipRelativeFormsRecordFixups()
    {
        List<byte> text = [];
        List<Fixup> fixups = [];

        Encoder.Encode(new Instruction { M = Mnemonic.LeaRipRel, R1 = Reg.Rcx, Symbol = SymbolRef.FmtInt }, text, fixups);
        Encoder.Encode(new Instruction { M = Mnemonic.CallRipIndirect, Symbol = SymbolRef.ImpPrintf }, text, fixups);

        Assert.Equal([0x48, 0x8D, 0x0D, 0, 0, 0, 0, 0xFF, 0x15, 0, 0, 0, 0], text);
        Assert.Equal(2, fixups.Count);
        Assert.Equal(3, fixups[0].TextOffset);
        Assert.Equal(SymbolRef.FmtInt, fixups[0].Symbol);
        Assert.Equal(9, fixups[1].TextOffset);
        Assert.Equal(SymbolRef.ImpPrintf, fixups[1].Symbol);
    }

    [Fact]
    public void MiscForms()
    {
        Assert.Equal([0x31, 0xC9], Encode(new Instruction { M = Mnemonic.XorReg32Reg32, R1 = Reg.Rcx, R2 = Reg.Rcx }));
        Assert.Equal([0xC3], Encode(new Instruction { M = Mnemonic.Ret }));
    }
}

using Ir;

namespace Asm;

/// <summary>
/// Stack-machine x64 codegen from linear IR: operands live on the hardware stack, floats
/// travel as raw 8-byte bit patterns. The operand stack depth is statically known at every
/// op, so printf call sites can reserve 32 or 40 bytes (shadow space + alignment padding)
/// at compile time — RSP is 16-aligned with an empty operand stack after the prologue.
///
/// Win64 varargs: a float argument must be duplicated in BOTH xmm1 and rdx.
/// </summary>
public static class AsmGenerator
{
    public static Result<AsmProgram> Run(IrProgram ir)
    {
        List<Instruction> text = [];

        // Prologue. Entry is called by the loader: RSP ≡ 8 (mod 16) on entry, so after
        // push rbp it is 16-aligned; the frame size keeps it that way.
        int frameSize = RoundUpTo16(8 * ir.LocalTypes.Count);
        text.Add(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rbp });
        text.Add(new Instruction { M = Mnemonic.MovRegReg, R1 = Reg.Rbp, R2 = Reg.Rsp });

        if (frameSize > 0)
        {
            text.Add(new Instruction { M = Mnemonic.SubRspImm, Imm = frameSize });
        }

        int depth = 0;

        foreach (IrOp op in ir.Ops)
        {
            switch (op.Code)
            {
                case IrOpCode.PushInt:
                    EmitPushImm64(text, op.IntValue);
                    depth++;
                    break;

                case IrOpCode.PushFloat:
                    EmitPushImm64(text, BitConverter.DoubleToInt64Bits(op.FloatValue));
                    depth++;
                    break;

                case IrOpCode.LoadLocal:
                    text.Add(new Instruction { M = Mnemonic.MovRegMem, R1 = Reg.Rax, Mem = Slot(op.Slot) });
                    text.Add(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rax });
                    depth++;
                    break;

                case IrOpCode.StoreLocal:
                    text.Add(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rax });
                    text.Add(new Instruction { M = Mnemonic.MovMemReg, Mem = Slot(op.Slot), R1 = Reg.Rax });
                    depth--;
                    break;

                case IrOpCode.Add:
                case IrOpCode.Sub:
                case IrOpCode.Mul:
                case IrOpCode.Div:
                    if (op.Type == IrType.I64)
                    {
                        EmitIntBinary(text, op.Code);
                    }
                    else
                    {
                        EmitFloatBinary(text, op.Code);
                    }

                    depth--;
                    break;

                case IrOpCode.IntToFloat:
                    // cvtsi2sd xmm0, qword [rsp] ; movsd [rsp], xmm0 — in-place conversion.
                    text.Add(new Instruction { M = Mnemonic.Cvtsi2sdXmmMem, X1 = XmmReg.Xmm0, Mem = TopOfStack() });
                    text.Add(new Instruction { M = Mnemonic.MovsdMemXmm, Mem = TopOfStack(), X1 = XmmReg.Xmm0 });
                    break;

                case IrOpCode.Print:
                    depth--;
                    EmitPrint(text, op.Type, depth);
                    break;

                case IrOpCode.Exit:
                    text.Add(new Instruction { M = Mnemonic.XorReg32Reg32, R1 = Reg.Rcx, R2 = Reg.Rcx });
                    EmitCall(text, SymbolRef.ImpExitProcess, depth);
                    break;

                default:
                    return Result<AsmProgram>.Error($"asm generator: unhandled IR op {op.Code}");
            }
        }

        // Never reached (ExitProcess does not return); keeps the text well-formed.
        text.Add(new Instruction { M = Mnemonic.Ret });

        return Result<AsmProgram>.Ok(new AsmProgram { Text = text });
    }

    private static MemOperand Slot(int slot) =>
        new() { Base = Reg.Rbp, Disp = -8 * (slot + 1) };

    private static MemOperand TopOfStack(int disp = 0) =>
        new() { Base = Reg.Rsp, Disp = disp };

    private static void EmitPushImm64(List<Instruction> text, long value)
    {
        text.Add(new Instruction { M = Mnemonic.MovRegImm64, R1 = Reg.Rax, Imm = value });
        text.Add(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rax });
    }

    private static void EmitIntBinary(List<Instruction> text, Ir.IrOpCode code)
    {
        text.Add(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rcx });
        text.Add(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rax });

        switch (code)
        {
            case IrOpCode.Add:
                text.Add(new Instruction { M = Mnemonic.AddRegReg, R1 = Reg.Rax, R2 = Reg.Rcx });
                break;

            case IrOpCode.Sub:
                text.Add(new Instruction { M = Mnemonic.SubRegReg, R1 = Reg.Rax, R2 = Reg.Rcx });
                break;

            case IrOpCode.Mul:
                text.Add(new Instruction { M = Mnemonic.ImulRegReg, R1 = Reg.Rax, R2 = Reg.Rcx });
                break;

            default:
                text.Add(new Instruction { M = Mnemonic.Cqo });
                text.Add(new Instruction { M = Mnemonic.IdivReg, R1 = Reg.Rcx });
                break;
        }

        text.Add(new Instruction { M = Mnemonic.PushReg, R1 = Reg.Rax });
    }

    private static void EmitFloatBinary(List<Instruction> text, IrOpCode code)
    {
        // rhs -> xmm1, pop it; lhs at [rsp] -> xmm0; op; result back to [rsp].
        text.Add(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm1, Mem = TopOfStack() });
        text.Add(new Instruction { M = Mnemonic.AddRspImm, Imm = 8 });
        text.Add(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm0, Mem = TopOfStack() });

        Mnemonic arithmetic = code switch
        {
            IrOpCode.Add => Mnemonic.AddsdXmmXmm,
            IrOpCode.Sub => Mnemonic.SubsdXmmXmm,
            IrOpCode.Mul => Mnemonic.MulsdXmmXmm,
            _ => Mnemonic.DivsdXmmXmm
        };

        text.Add(new Instruction { M = arithmetic, X1 = XmmReg.Xmm0, X2 = XmmReg.Xmm1 });
        text.Add(new Instruction { M = Mnemonic.MovsdMemXmm, Mem = TopOfStack(), X1 = XmmReg.Xmm0 });
    }

    private static void EmitPrint(List<Instruction> text, IrType type, int depthAtCall)
    {
        if (type == IrType.I64)
        {
            text.Add(new Instruction { M = Mnemonic.PopReg, R1 = Reg.Rdx });
            text.Add(new Instruction { M = Mnemonic.LeaRipRel, R1 = Reg.Rcx, Symbol = SymbolRef.FmtInt });
        }
        else
        {
            // Varargs float: duplicate the value in xmm1 and rdx, then pop.
            text.Add(new Instruction { M = Mnemonic.MovsdXmmMem, X1 = XmmReg.Xmm1, Mem = TopOfStack() });
            text.Add(new Instruction { M = Mnemonic.MovRegMem, R1 = Reg.Rdx, Mem = TopOfStack() });
            text.Add(new Instruction { M = Mnemonic.AddRspImm, Imm = 8 });
            text.Add(new Instruction { M = Mnemonic.LeaRipRel, R1 = Reg.Rcx, Symbol = SymbolRef.FmtFloat });
        }

        EmitCall(text, SymbolRef.ImpPrintf, depthAtCall);
    }

    /// <summary>
    /// Reserve shadow space sized to restore 16-byte alignment: 32 bytes when the operand
    /// stack depth is even (RSP already aligned), 40 when odd.
    /// </summary>
    private static void EmitCall(List<Instruction> text, SymbolRef target, int depthAtCall)
    {
        int reservation = depthAtCall % 2 == 0 ? 32 : 40;

        text.Add(new Instruction { M = Mnemonic.SubRspImm, Imm = reservation });
        text.Add(new Instruction { M = Mnemonic.CallRipIndirect, Symbol = target });
        text.Add(new Instruction { M = Mnemonic.AddRspImm, Imm = reservation });
    }

    private static int RoundUpTo16(int value) => (value + 15) & ~15;
}

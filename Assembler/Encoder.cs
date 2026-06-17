using Asm;

namespace Assembler;

/// <summary>
/// Hand-written x64 instruction encoder for the closed instruction set the codegen emits.
/// Only rax/rcx/rdx/rsp/rbp and xmm0/xmm1 occur, so no REX.B/REX.R extensions are needed;
/// every form has a fixed shape. Encodings are unit-tested byte-for-byte.
/// </summary>
public static class Encoder
{
    private const byte RexW = 0x48;

    public static Result Encode(Instruction instruction, List<byte> text, List<Fixup> fixups)
    {
        switch (instruction.M)
        {
            case Mnemonic.PushReg:
                text.Add((byte)(0x50 + (int)instruction.R1));
                return Result.Ok();

            case Mnemonic.PopReg:
                text.Add((byte)(0x58 + (int)instruction.R1));
                return Result.Ok();

            case Mnemonic.MovRegImm64:
                text.Add(RexW);
                text.Add((byte)(0xB8 + (int)instruction.R1));
                AddImm64(text, instruction.Imm);
                return Result.Ok();

            case Mnemonic.MovRegReg:
                // 48 89 /r : mov r/m64, r64 (reg = source, rm = destination).
                text.Add(RexW);
                text.Add(0x89);
                text.Add(ModRmDirect(instruction.R2, instruction.R1));
                return Result.Ok();

            case Mnemonic.MovRegMem:
                text.Add(RexW);
                text.Add(0x8B);
                AddMemOperand(text, (int)instruction.R1, instruction.Mem!);
                return Result.Ok();

            case Mnemonic.MovMemReg:
                text.Add(RexW);
                text.Add(0x89);
                AddMemOperand(text, (int)instruction.R1, instruction.Mem!);
                return Result.Ok();

            case Mnemonic.AddRegReg:
                text.Add(RexW);
                text.Add(0x01);
                text.Add(ModRmDirect(instruction.R2, instruction.R1));
                return Result.Ok();

            case Mnemonic.SubRegReg:
                text.Add(RexW);
                text.Add(0x29);
                text.Add(ModRmDirect(instruction.R2, instruction.R1));
                return Result.Ok();

            case Mnemonic.ImulRegReg:
                // 48 0F AF /r (reg = destination).
                text.Add(RexW);
                text.Add(0x0F);
                text.Add(0xAF);
                text.Add(ModRmDirect(instruction.R1, instruction.R2));
                return Result.Ok();

            case Mnemonic.Cqo:
                text.Add(RexW);
                text.Add(0x99);
                return Result.Ok();

            case Mnemonic.IdivReg:
                // 48 F7 /7
                text.Add(RexW);
                text.Add(0xF7);
                text.Add((byte)(0xF8 + (int)instruction.R1));
                return Result.Ok();

            case Mnemonic.SubRspImm:
                return EncodeRspAdjust(text, 0xEC, 0x81EC, instruction.Imm);

            case Mnemonic.AddRspImm:
                return EncodeRspAdjust(text, 0xC4, 0x81C4, instruction.Imm);

            case Mnemonic.MovsdXmmMem:
                text.Add(0xF2);
                text.Add(0x0F);
                text.Add(0x10);
                AddMemOperand(text, (int)instruction.X1, instruction.Mem!);
                return Result.Ok();

            case Mnemonic.MovsdMemXmm:
                text.Add(0xF2);
                text.Add(0x0F);
                text.Add(0x11);
                AddMemOperand(text, (int)instruction.X1, instruction.Mem!);
                return Result.Ok();

            case Mnemonic.AddsdXmmXmm:
                return EncodeSseArithmetic(text, 0x58, instruction);

            case Mnemonic.SubsdXmmXmm:
                return EncodeSseArithmetic(text, 0x5C, instruction);

            case Mnemonic.MulsdXmmXmm:
                return EncodeSseArithmetic(text, 0x59, instruction);

            case Mnemonic.DivsdXmmXmm:
                return EncodeSseArithmetic(text, 0x5E, instruction);

            case Mnemonic.Cvtsi2sdXmmMem:
                // F2 REX.W 0F 2A /r — REX sits between the F2 prefix and the opcode.
                text.Add(0xF2);
                text.Add(RexW);
                text.Add(0x0F);
                text.Add(0x2A);
                AddMemOperand(text, (int)instruction.X1, instruction.Mem!);
                return Result.Ok();

            case Mnemonic.LeaRipRel:
                // 48 8D /r with mod=00 rm=101 (rip-relative) + disp32 fixup.
                text.Add(RexW);
                text.Add(0x8D);
                text.Add((byte)(((int)instruction.R1 << 3) | 0b101));
                AddFixupHole(text, fixups, instruction.Symbol);
                return Result.Ok();

            case Mnemonic.CallRipIndirect:
                // FF /2 with mod=00 rm=101 (rip-relative) + disp32 fixup.
                text.Add(0xFF);
                text.Add(0x15);
                AddFixupHole(text, fixups, instruction.Symbol);
                return Result.Ok();

            case Mnemonic.XorReg32Reg32:
                text.Add(0x31);
                text.Add(ModRmDirect(instruction.R2, instruction.R1));
                return Result.Ok();

            case Mnemonic.Ret:
                text.Add(0xC3);
                return Result.Ok();

            default:
                return Result.Error($"encoder: unsupported instruction {instruction.M}");
        }
    }

    private static byte ModRmDirect(Reg reg, Reg rm) =>
        (byte)(0b11_000_000 | ((int)reg << 3) | (int)rm);

    /// <summary>
    /// ModRM (+ SIB, + disp) for a [base+disp] operand. rsp requires a SIB byte; rbp
    /// requires an explicit displacement even when zero.
    /// </summary>
    private static void AddMemOperand(List<byte> text, int regField, MemOperand mem)
    {
        bool needsSib = mem.Base == Reg.Rsp;
        bool fitsDisp8 = mem.Disp is >= -128 and <= 127;
        bool zeroDisp = mem.Disp == 0 && mem.Base != Reg.Rbp;

        int mod = zeroDisp ? 0b00 : fitsDisp8 ? 0b01 : 0b10;
        text.Add((byte)((mod << 6) | (regField << 3) | (int)mem.Base));

        if (needsSib)
        {
            text.Add(0x24); // scale=0, index=none, base=rsp
        }

        if (!zeroDisp)
        {
            if (fitsDisp8)
            {
                text.Add((byte)(sbyte)mem.Disp);
            }
            else
            {
                AddImm32(text, mem.Disp);
            }
        }
    }

    private static Result EncodeRspAdjust(List<byte> text, byte imm8ModRm, int imm32Opcode, long amount)
    {
        if (amount is < 0 or > int.MaxValue)
        {
            return Result.Error($"encoder: invalid rsp adjustment {amount}");
        }

        text.Add(RexW);

        if (amount <= 127)
        {
            text.Add(0x83);
            text.Add(imm8ModRm);
            text.Add((byte)amount);
        }
        else
        {
            text.Add((byte)(imm32Opcode >> 8));
            text.Add((byte)(imm32Opcode & 0xFF));
            AddImm32(text, (int)amount);
        }

        return Result.Ok();
    }

    private static Result EncodeSseArithmetic(List<byte> text, byte opcode, Instruction instruction)
    {
        text.Add(0xF2);
        text.Add(0x0F);
        text.Add(opcode);
        text.Add((byte)(0b11_000_000 | ((int)instruction.X1 << 3) | (int)instruction.X2));
        return Result.Ok();
    }

    private static void AddFixupHole(List<byte> text, List<Fixup> fixups, SymbolRef symbol)
    {
        fixups.Add(new Fixup { TextOffset = text.Count, Symbol = symbol });
        AddImm32(text, 0);
    }

    private static void AddImm32(List<byte> text, int value)
    {
        text.Add((byte)(value & 0xFF));
        text.Add((byte)((value >> 8) & 0xFF));
        text.Add((byte)((value >> 16) & 0xFF));
        text.Add((byte)((value >> 24) & 0xFF));
    }

    private static void AddImm64(List<byte> text, long value)
    {
        for (int shift = 0; shift < 64; shift += 8)
        {
            text.Add((byte)((value >> shift) & 0xFF));
        }
    }
}

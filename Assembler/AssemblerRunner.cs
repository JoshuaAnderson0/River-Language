using System.Text;
using Asm;

namespace Assembler;

/// <summary>
/// Assembles an AsmProgram into raw machine code plus the .rdata blob (printf format
/// strings). Single pass: every instruction form has a fixed size, so no relaxation is
/// needed; rip-relative holes are recorded as fixups for the linker.
/// </summary>
public static class AssemblerRunner
{
    public static Result<AssembledCode> Run(AsmProgram program)
    {
        List<byte> text = [];
        List<Fixup> fixups = [];

        foreach (Instruction instruction in program.Text)
        {
            Result encoded = Encoder.Encode(instruction, text, fixups);

            if (!encoded.IsOk)
            {
                return Result<AssembledCode>.Error(encoded.Message!);
            }
        }

        (byte[] rdata, Dictionary<SymbolRef, int> offsets) = BuildRdata();

        return Result<AssembledCode>.Ok(new AssembledCode
        {
            Text = text.ToArray(),
            Fixups = fixups,
            Rdata = rdata,
            RdataOffsets = offsets
        });
    }

    private static (byte[], Dictionary<SymbolRef, int>) BuildRdata()
    {
        List<byte> rdata = [];
        Dictionary<SymbolRef, int> offsets = [];

        offsets[SymbolRef.FmtInt] = rdata.Count;
        rdata.AddRange(Encoding.ASCII.GetBytes("%lld\n\0"));

        offsets[SymbolRef.FmtFloat] = rdata.Count;
        rdata.AddRange(Encoding.ASCII.GetBytes("%g\n\0"));

        return (rdata.ToArray(), offsets);
    }
}

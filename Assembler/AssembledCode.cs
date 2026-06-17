using Asm;

namespace Assembler;

/// <summary>
/// A 4-byte rip-relative hole in the text awaiting a linker-assigned RVA:
/// disp32 = symbolRVA - (textRVA + TextOffset + 4).
/// </summary>
public sealed class Fixup
{
    public required int TextOffset;
    public required SymbolRef Symbol;
}

public sealed class AssembledCode
{
    public required byte[] Text;
    public required List<Fixup> Fixups;
    public required byte[] Rdata;
    public required Dictionary<SymbolRef, int> RdataOffsets;
}

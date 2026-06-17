using Asm;
using Assembler;

namespace Linker;

/// <summary>
/// PE32+ executable writer — the final stage of the compiler's own toolchain (no external
/// linker). Three sections: .text (code), .rdata (format strings), .idata (imports for
/// msvcrt!printf and kernel32!ExitProcess). The entry point is our code directly: msvcrt
/// initializes itself on load, so no CRT startup is required. ASLR is disabled (relocs
/// stripped), so the image loads at its preferred base and only rip-relative fixups exist,
/// patched here as disp32 = targetRVA - (siteRVA + 4).
/// </summary>
public static class PeLinker
{
    private const ulong ImageBase = 0x1_4000_0000;
    private const uint SectionAlignment = 0x1000;
    private const uint FileAlignment = 0x200;
    private const uint HeadersFileSize = 0x200;

    public static Result Run(AssembledCode code, string exePath)
    {
        if (code.Text.Length == 0)
        {
            return Result.Error("linker: empty text section");
        }

        // Virtual layout.
        uint textRva = SectionAlignment;
        uint rdataRva = textRva + AlignUp((uint)code.Text.Length, SectionAlignment);
        uint idataRva = rdataRva + AlignUp((uint)Math.Max(code.Rdata.Length, 1), SectionAlignment);

        ImportSection.Built imports = ImportSection.Build((int)idataRva);
        uint sizeOfImage = idataRva + AlignUp((uint)imports.Bytes.Length, SectionAlignment);

        // File layout.
        uint textRaw = HeadersFileSize;
        uint textRawSize = AlignUp((uint)code.Text.Length, FileAlignment);
        uint rdataRaw = textRaw + textRawSize;
        uint rdataRawSize = AlignUp((uint)Math.Max(code.Rdata.Length, 1), FileAlignment);
        uint idataRaw = rdataRaw + rdataRawSize;
        uint idataRawSize = AlignUp((uint)imports.Bytes.Length, FileAlignment);

        byte[] text = PatchFixups(code, textRva, rdataRva, idataRva, imports);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(exePath))!);

            using FileStream stream = File.Create(exePath);
            using BinaryWriter writer = new(stream);

            WriteDosHeader(writer);
            WritePeHeaders(writer, textRva, rdataRva, idataRva, sizeOfImage, code, imports,
                textRawSize, rdataRawSize, idataRawSize, textRaw, rdataRaw, idataRaw);

            WriteSection(writer, textRaw, text, textRawSize);
            WriteSection(writer, rdataRaw, code.Rdata, rdataRawSize);
            WriteSection(writer, idataRaw, imports.Bytes, idataRawSize);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Result.Error($"linker: cannot write '{exePath}': {exception.Message}");
        }

        return Result.Ok();
    }

    private static byte[] PatchFixups(
        AssembledCode code,
        uint textRva,
        uint rdataRva,
        uint idataRva,
        ImportSection.Built imports)
    {
        byte[] text = (byte[])code.Text.Clone();

        foreach (Fixup fixup in code.Fixups)
        {
            uint targetRva = fixup.Symbol switch
            {
                SymbolRef.FmtInt => rdataRva + (uint)code.RdataOffsets[SymbolRef.FmtInt],
                SymbolRef.FmtFloat => rdataRva + (uint)code.RdataOffsets[SymbolRef.FmtFloat],
                _ => (uint)ImportSection.IatRva(imports, (int)idataRva, fixup.Symbol)
            };

            int disp32 = (int)targetRva - (int)(textRva + fixup.TextOffset + 4);

            text[fixup.TextOffset] = (byte)disp32;
            text[fixup.TextOffset + 1] = (byte)(disp32 >> 8);
            text[fixup.TextOffset + 2] = (byte)(disp32 >> 16);
            text[fixup.TextOffset + 3] = (byte)(disp32 >> 24);
        }

        return text;
    }

    private static void WriteDosHeader(BinaryWriter writer)
    {
        byte[] dosHeader = new byte[0x40];
        dosHeader[0] = (byte)'M';
        dosHeader[1] = (byte)'Z';
        dosHeader[0x3C] = 0x40; // e_lfanew: PE signature directly after this header

        writer.Write(dosHeader);
    }

    private static void WritePeHeaders(
        BinaryWriter writer,
        uint textRva,
        uint rdataRva,
        uint idataRva,
        uint sizeOfImage,
        AssembledCode code,
        ImportSection.Built imports,
        uint textRawSize,
        uint rdataRawSize,
        uint idataRawSize,
        uint textRaw,
        uint rdataRaw,
        uint idataRaw)
    {
        // PE signature + COFF header.
        writer.Write("PE\0\0"u8);
        writer.Write((ushort)0x8664);  // Machine: x64
        writer.Write((ushort)3);       // NumberOfSections
        writer.Write(0u);              // TimeDateStamp
        writer.Write(0u);              // PointerToSymbolTable
        writer.Write(0u);              // NumberOfSymbols
        writer.Write((ushort)0xF0);    // SizeOfOptionalHeader
        writer.Write((ushort)0x0023);  // EXECUTABLE_IMAGE | RELOCS_STRIPPED | LARGE_ADDRESS_AWARE

        // Optional header (PE32+).
        writer.Write((ushort)0x20B);   // Magic
        writer.Write((byte)1);         // MajorLinkerVersion (cosmetic)
        writer.Write((byte)0);
        writer.Write(textRawSize);     // SizeOfCode
        writer.Write(rdataRawSize + idataRawSize); // SizeOfInitializedData
        writer.Write(0u);              // SizeOfUninitializedData
        writer.Write(textRva);         // AddressOfEntryPoint: our code, no CRT startup
        writer.Write(textRva);         // BaseOfCode
        writer.Write(ImageBase);
        writer.Write(SectionAlignment);
        writer.Write(FileAlignment);
        writer.Write((ushort)6);       // MajorOperatingSystemVersion
        writer.Write((ushort)0);
        writer.Write((ushort)0);       // Image version
        writer.Write((ushort)0);
        writer.Write((ushort)6);       // MajorSubsystemVersion
        writer.Write((ushort)0);
        writer.Write(0u);              // Win32VersionValue
        writer.Write(sizeOfImage);
        writer.Write(HeadersFileSize); // SizeOfHeaders
        writer.Write(0u);              // CheckSum (only drivers need a real one)
        writer.Write((ushort)3);       // Subsystem: console
        writer.Write((ushort)0);       // DllCharacteristics: no ASLR, no NX requirements
        writer.Write(0x100000UL);      // SizeOfStackReserve
        writer.Write(0x1000UL);        // SizeOfStackCommit
        writer.Write(0x100000UL);      // SizeOfHeapReserve
        writer.Write(0x1000UL);        // SizeOfHeapCommit
        writer.Write(0u);              // LoaderFlags
        writer.Write(16u);             // NumberOfRvaAndSizes

        // Data directories: only [1] imports and [12] IAT are populated.
        for (int directory = 0; directory < 16; directory++)
        {
            switch (directory)
            {
                case 1:
                    writer.Write(idataRva);
                    writer.Write((uint)imports.DescriptorTableSize);
                    break;

                case 12:
                    writer.Write(idataRva + (uint)imports.IatRegionOffset);
                    writer.Write((uint)imports.IatRegionSize);
                    break;

                default:
                    writer.Write(0u);
                    writer.Write(0u);
                    break;
            }
        }

        WriteSectionHeader(writer, ".text", (uint)code.Text.Length, textRva, textRawSize, textRaw, 0x60000020);
        WriteSectionHeader(writer, ".rdata", (uint)code.Rdata.Length, rdataRva, rdataRawSize, rdataRaw, 0x40000040);
        WriteSectionHeader(writer, ".idata", (uint)imports.Bytes.Length, idataRva, idataRawSize, idataRaw, 0xC0000040);
    }

    private static void WriteSectionHeader(
        BinaryWriter writer,
        string name,
        uint virtualSize,
        uint virtualAddress,
        uint rawSize,
        uint rawPointer,
        uint characteristics)
    {
        byte[] nameBytes = new byte[8];

        for (int index = 0; index < name.Length; index++)
        {
            nameBytes[index] = (byte)name[index];
        }

        writer.Write(nameBytes);
        writer.Write(virtualSize);
        writer.Write(virtualAddress);
        writer.Write(rawSize);
        writer.Write(rawPointer);
        writer.Write(0u);       // PointerToRelocations
        writer.Write(0u);       // PointerToLinenumbers
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(characteristics);
    }

    private static void WriteSection(BinaryWriter writer, uint rawOffset, byte[] content, uint rawSize)
    {
        writer.BaseStream.Position = rawOffset;
        writer.Write(content);
        writer.BaseStream.Position = rawOffset + rawSize - 1;
        writer.Write((byte)0); // extend the file to the aligned size
    }

    private static uint AlignUp(uint value, uint alignment) =>
        (value + alignment - 1) & ~(alignment - 1);
}

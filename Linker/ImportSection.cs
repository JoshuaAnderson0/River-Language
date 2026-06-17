using System.Text;
using Asm;

namespace Linker;

/// <summary>
/// Builds the .idata section: import descriptors for msvcrt.dll (printf) and
/// kernel32.dll (ExitProcess). The two IATs are laid out adjacently right after the
/// descriptors so data directory 12 can describe one contiguous IAT region.
/// </summary>
public static class ImportSection
{
    public sealed class Built
    {
        public required byte[] Bytes;
        public required int DescriptorTableSize;
        public required int IatRegionOffset;
        public required int IatRegionSize;
        public required int PrintfIatOffset;
        public required int ExitProcessIatOffset;
    }

    public static Built Build(int sectionRva)
    {
        const int descriptorSize = 20;
        const int descriptorTableSize = 3 * descriptorSize; // msvcrt, kernel32, null
        const int thunkBlockSize = 16;                      // one u64 entry + null terminator

        int iatMsvcrt = descriptorTableSize;
        int iatKernel32 = iatMsvcrt + thunkBlockSize;
        int iltMsvcrt = iatKernel32 + thunkBlockSize;
        int iltKernel32 = iltMsvcrt + thunkBlockSize;
        int hintNames = iltKernel32 + thunkBlockSize;

        byte[] printfHintName = HintName("printf");
        byte[] exitHintName = HintName("ExitProcess");

        int printfHintNameOffset = hintNames;
        int exitHintNameOffset = printfHintNameOffset + printfHintName.Length;
        int msvcrtNameOffset = exitHintNameOffset + exitHintName.Length;
        byte[] msvcrtName = Encoding.ASCII.GetBytes("msvcrt.dll\0");
        int kernel32NameOffset = msvcrtNameOffset + msvcrtName.Length;
        byte[] kernel32Name = Encoding.ASCII.GetBytes("kernel32.dll\0");

        int totalSize = kernel32NameOffset + kernel32Name.Length;
        byte[] bytes = new byte[totalSize];

        WriteDescriptor(bytes, 0, sectionRva + iltMsvcrt, sectionRva + msvcrtNameOffset, sectionRva + iatMsvcrt);
        WriteDescriptor(bytes, descriptorSize, sectionRva + iltKernel32, sectionRva + kernel32NameOffset, sectionRva + iatKernel32);
        // Third descriptor stays zeroed: the table terminator.

        WriteU64(bytes, iatMsvcrt, (ulong)(sectionRva + printfHintNameOffset));
        WriteU64(bytes, iltMsvcrt, (ulong)(sectionRva + printfHintNameOffset));
        WriteU64(bytes, iatKernel32, (ulong)(sectionRva + exitHintNameOffset));
        WriteU64(bytes, iltKernel32, (ulong)(sectionRva + exitHintNameOffset));

        printfHintName.CopyTo(bytes, printfHintNameOffset);
        exitHintName.CopyTo(bytes, exitHintNameOffset);
        msvcrtName.CopyTo(bytes, msvcrtNameOffset);
        kernel32Name.CopyTo(bytes, kernel32NameOffset);

        return new Built
        {
            Bytes = bytes,
            DescriptorTableSize = descriptorTableSize,
            IatRegionOffset = iatMsvcrt,
            IatRegionSize = 2 * thunkBlockSize,
            PrintfIatOffset = iatMsvcrt,
            ExitProcessIatOffset = iatKernel32
        };
    }

    public static int IatRva(Built built, int sectionRva, SymbolRef symbol) =>
        symbol == SymbolRef.ImpPrintf
            ? sectionRva + built.PrintfIatOffset
            : sectionRva + built.ExitProcessIatOffset;

    /// <summary>u16 hint (0) + function name, padded to even length.</summary>
    private static byte[] HintName(string functionName)
    {
        List<byte> bytes = [0, 0];
        bytes.AddRange(Encoding.ASCII.GetBytes(functionName));
        bytes.Add(0);

        if (bytes.Count % 2 != 0)
        {
            bytes.Add(0);
        }

        return bytes.ToArray();
    }

    private static void WriteDescriptor(byte[] bytes, int offset, int iltRva, int nameRva, int iatRva)
    {
        WriteU32(bytes, offset, (uint)iltRva);       // OriginalFirstThunk
        WriteU32(bytes, offset + 4, 0);              // TimeDateStamp
        WriteU32(bytes, offset + 8, 0);              // ForwarderChain
        WriteU32(bytes, offset + 12, (uint)nameRva); // Name
        WriteU32(bytes, offset + 16, (uint)iatRva);  // FirstThunk
    }

    private static void WriteU32(byte[] bytes, int offset, uint value)
    {
        bytes[offset] = (byte)value;
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
    }

    private static void WriteU64(byte[] bytes, int offset, ulong value)
    {
        WriteU32(bytes, offset, (uint)value);
        WriteU32(bytes, offset + 4, (uint)(value >> 32));
    }
}

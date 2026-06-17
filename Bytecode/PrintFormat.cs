using System.Globalization;

namespace Bytecode;

/// <summary>
/// Print formatting shared by both backends' semantics. The ASM backend prints through
/// msvcrt printf with "%lld" / "%g"; the VM must produce byte-identical output, so ints
/// use invariant formatting (≡ %lld) and floats emulate %g exactly: 6 significant
/// digits, trailing zeros stripped, plain/exponent auto-switch, two-digit exponent.
/// </summary>
public static class PrintFormat
{
    public static string Int(long value) => value.ToString(CultureInfo.InvariantCulture);

    public static string Float(double value)
    {
        string formatted = value.ToString("G6", CultureInfo.InvariantCulture);
        int exponentIndex = formatted.IndexOf('E');

        if (exponentIndex < 0)
        {
            return formatted;
        }

        // .NET "1E+20" / "3.33333E-07" -> printf "1e+20" / "3.33333e-07" (>= 2 exponent digits).
        string mantissa = formatted[..exponentIndex];
        char sign = formatted[exponentIndex + 1];
        string digits = formatted[(exponentIndex + 2)..];

        if (digits.Length < 2)
        {
            digits = "0" + digits;
        }

        return $"{mantissa}e{sign}{digits}";
    }
}

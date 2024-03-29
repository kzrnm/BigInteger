// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// The BigNumber class implements methods for formatting and parsing
// big numeric values. To format and parse numeric values, applications should
// use the Format and Parse methods provided by the numeric
// classes (BigInteger). Those
// Format and Parse methods share a common implementation
// provided by this class, and are thus documented in detail here.
//
// Formatting
//
// The Format methods provided by the numeric classes are all of the
// form
//
//  public static String Format(XXX value, String format);
//  public static String Format(XXX value, String format, NumberFormatInfo info);
//
// where XXX is the name of the particular numeric class. The methods convert
// the numeric value to a string using the format string given by the
// format parameter. If the format parameter is null or
// an empty string, the number is formatted as if the string "G" (general
// format) was specified. The info parameter specifies the
// NumberFormatInfo instance to use when formatting the number. If the
// info parameter is null or omitted, the numeric formatting information
// is obtained from the current culture. The NumberFormatInfo supplies
// such information as the characters to use for decimal and thousand
// separators, and the spelling and placement of currency symbols in monetary
// values.
//
// Format strings fall into two categories: Standard format strings and
// user-defined format strings. A format string consisting of a single
// alphabetic character (A-Z or a-z), optionally followed by a sequence of
// digits (0-9), is a standard format string. All other format strings are
// used-defined format strings.
//
// A standard format string takes the form Axx, where A is an
// alphabetic character called the format specifier and xx is a
// sequence of digits called the precision specifier. The format
// specifier controls the type of formatting applied to the number and the
// precision specifier controls the number of significant digits or decimal
// places of the formatting operation. The following table describes the
// supported standard formats.
//
// C c - Currency format. The number is
// converted to a string that represents a currency amount. The conversion is
// controlled by the currency format information of the NumberFormatInfo
// used to format the number. The precision specifier indicates the desired
// number of decimal places. If the precision specifier is omitted, the default
// currency precision given by the NumberFormatInfo is used.
//
// D d - Decimal format. This format is
// supported for integral types only. The number is converted to a string of
// decimal digits, prefixed by a minus sign if the number is negative. The
// precision specifier indicates the minimum number of digits desired in the
// resulting string. If required, the number will be left-padded with zeros to
// produce the number of digits given by the precision specifier.
//
// E e Engineering (scientific) format.
// The number is converted to a string of the form
// "-d.ddd...E+ddd" or "-d.ddd...e+ddd", where each
// 'd' indicates a digit (0-9). The string starts with a minus sign if the
// number is negative, and one digit always precedes the decimal point. The
// precision specifier indicates the desired number of digits after the decimal
// point. If the precision specifier is omitted, a default of 6 digits after
// the decimal point is used. The format specifier indicates whether to prefix
// the exponent with an 'E' or an 'e'. The exponent is always consists of a
// plus or minus sign and three digits.
//
// F f Fixed point format. The number is
// converted to a string of the form "-ddd.ddd....", where each
// 'd' indicates a digit (0-9). The string starts with a minus sign if the
// number is negative. The precision specifier indicates the desired number of
// decimal places. If the precision specifier is omitted, the default numeric
// precision given by the NumberFormatInfo is used.
//
// G g - General format. The number is
// converted to the shortest possible decimal representation using fixed point
// or scientific format. The precision specifier determines the number of
// significant digits in the resulting string. If the precision specifier is
// omitted, the number of significant digits is determined by the type of the
// number being converted (10 for int, 19 for long, 7 for
// float, 15 for double, 19 for Currency, and 29 for
// Decimal). Trailing zeros after the decimal point are removed, and the
// resulting string contains a decimal point only if required. The resulting
// string uses fixed point format if the exponent of the number is less than
// the number of significant digits and greater than or equal to -4. Otherwise,
// the resulting string uses scientific format, and the case of the format
// specifier controls whether the exponent is prefixed with an 'E' or an
// 'e'.
//
// N n Number format. The number is
// converted to a string of the form "-d,ddd,ddd.ddd....", where
// each 'd' indicates a digit (0-9). The string starts with a minus sign if the
// number is negative. Thousand separators are inserted between each group of
// three digits to the left of the decimal point. The precision specifier
// indicates the desired number of decimal places. If the precision specifier
// is omitted, the default numeric precision given by the
// NumberFormatInfo is used.
//
// X x - Hexadecimal format. This format is
// supported for integral types only. The number is converted to a string of
// hexadecimal digits. The format specifier indicates whether to use upper or
// lower case characters for the hexadecimal digits above 9 ('X' for 'ABCDEF',
// and 'x' for 'abcdef'). The precision specifier indicates the minimum number
// of digits desired in the resulting string. If required, the number will be
// left-padded with zeros to produce the number of digits given by the
// precision specifier.
//
// Some examples of standard format strings and their results are shown in the
// table below. (The examples all assume a default NumberFormatInfo.)
//
// Value        Format  Result
// 12345.6789   C       $12,345.68
// -12345.6789  C       ($12,345.68)
// 12345        D       12345
// 12345        D8      00012345
// 12345.6789   E       1.234568E+004
// 12345.6789   E10     1.2345678900E+004
// 12345.6789   e4      1.2346e+004
// 12345.6789   F       12345.68
// 12345.6789   F0      12346
// 12345.6789   F6      12345.678900
// 12345.6789   G       12345.6789
// 12345.6789   G7      12345.68
// 123456789    G7      1.234568E8
// 12345.6789   N       12,345.68
// 123456789    N4      123,456,789.0000
// 0x2c45e      x       2c45e
// 0x2c45e      X       2C45E
// 0x2c45e      X8      0002C45E
//
// Format strings that do not start with an alphabetic character, or that start
// with an alphabetic character followed by a non-digit, are called
// user-defined format strings. The following table describes the formatting
// characters that are supported in user defined format strings.
//
//
// 0 - Digit placeholder. If the value being
// formatted has a digit in the position where the '0' appears in the format
// string, then that digit is copied to the output string. Otherwise, a '0' is
// stored in that position in the output string. The position of the leftmost
// '0' before the decimal point and the rightmost '0' after the decimal point
// determines the range of digits that are always present in the output
// string.
//
// # - Digit placeholder. If the value being
// formatted has a digit in the position where the '#' appears in the format
// string, then that digit is copied to the output string. Otherwise, nothing
// is stored in that position in the output string.
//
// . - Decimal point. The first '.' character
// in the format string determines the location of the decimal separator in the
// formatted value; any additional '.' characters are ignored. The actual
// character used as a the decimal separator in the output string is given by
// the NumberFormatInfo used to format the number.
//
// , - Thousand separator and number scaling.
// The ',' character serves two purposes. First, if the format string contains
// a ',' character between two digit placeholders (0 or #) and to the left of
// the decimal point if one is present, then the output will have thousand
// separators inserted between each group of three digits to the left of the
// decimal separator. The actual character used as a the decimal separator in
// the output string is given by the NumberFormatInfo used to format the
// number. Second, if the format string contains one or more ',' characters
// immediately to the left of the decimal point, or after the last digit
// placeholder if there is no decimal point, then the number will be divided by
// 1000 times the number of ',' characters before it is formatted. For example,
// the format string '0,,' will represent 100 million as just 100. Use of the
// ',' character to indicate scaling does not also cause the formatted number
// to have thousand separators. Thus, to scale a number by 1 million and insert
// thousand separators you would use the format string '#,##0,,'.
//
// % - Percentage placeholder. The presence of
// a '%' character in the format string causes the number to be multiplied by
// 100 before it is formatted. The '%' character itself is inserted in the
// output string where it appears in the format string.
//
// E+ E- e+ e-   - Scientific notation.
// If any of the strings 'E+', 'E-', 'e+', or 'e-' are present in the format
// string and are immediately followed by at least one '0' character, then the
// number is formatted using scientific notation with an 'E' or 'e' inserted
// between the number and the exponent. The number of '0' characters following
// the scientific notation indicator determines the minimum number of digits to
// output for the exponent. The 'E+' and 'e+' formats indicate that a sign
// character (plus or minus) should always precede the exponent. The 'E-' and
// 'e-' formats indicate that a sign character should only precede negative
// exponents.
//
// \ - Literal character. A backslash character
// causes the next character in the format string to be copied to the output
// string as-is. The backslash itself isn't copied, so to place a backslash
// character in the output string, use two backslashes (\\) in the format
// string.
//
// 'ABC' "ABC" - Literal string. Characters
// enclosed in single or double quotation marks are copied to the output string
// as-is and do not affect formatting.
//
// ; - Section separator. The ';' character is
// used to separate sections for positive, negative, and zero numbers in the
// format string.
//
// Other - All other characters are copied to
// the output string in the position they appear.
//
// For fixed point formats (formats not containing an 'E+', 'E-', 'e+', or
// 'e-'), the number is rounded to as many decimal places as there are digit
// placeholders to the right of the decimal point. If the format string does
// not contain a decimal point, the number is rounded to the nearest
// integer. If the number has more digits than there are digit placeholders to
// the left of the decimal point, the extra digits are copied to the output
// string immediately before the first digit placeholder.
//
// For scientific formats, the number is rounded to as many significant digits
// as there are digit placeholders in the format string.
//
// To allow for different formatting of positive, negative, and zero values, a
// user-defined format string may contain up to three sections separated by
// semicolons. The results of having one, two, or three sections in the format
// string are described in the table below.
//
// Sections:
//
// One - The format string applies to all values.
//
// Two - The first section applies to positive values
// and zeros, and the second section applies to negative values. If the number
// to be formatted is negative, but becomes zero after rounding according to
// the format in the second section, then the resulting zero is formatted
// according to the first section.
//
// Three - The first section applies to positive
// values, the second section applies to negative values, and the third section
// applies to zeros. The second section may be left empty (by having no
// characters between the semicolons), in which case the first section applies
// to all non-zero values. If the number to be formatted is non-zero, but
// becomes zero after rounding according to the format in the first or second
// section, then the resulting zero is formatted according to the third
// section.
//
// For both standard and user-defined formatting operations on values of type
// float and double, if the value being formatted is a NaN (Not
// a Number) or a positive or negative infinity, then regardless of the format
// string, the resulting string is given by the NaNSymbol,
// PositiveInfinitySymbol, or NegativeInfinitySymbol property of
// the NumberFormatInfo used to format the number.
//
// Parsing
//
// The Parse methods provided by the numeric classes are all of the form
//
//  public static XXX Parse(String s);
//  public static XXX Parse(String s, int style);
//  public static XXX Parse(String s, int style, NumberFormatInfo info);
//
// where XXX is the name of the particular numeric class. The methods convert a
// string to a numeric value. The optional style parameter specifies the
// permitted style of the numeric string. It must be a combination of bit flags
// from the NumberStyles enumeration. The optional info parameter
// specifies the NumberFormatInfo instance to use when parsing the
// string. If the info parameter is null or omitted, the numeric
// formatting information is obtained from the current culture.
//
// Numeric strings produced by the Format methods using the Currency,
// Decimal, Engineering, Fixed point, General, or Number standard formats
// (the C, D, E, F, G, and N format specifiers) are guaranteed to be parseable
// by the Parse methods if the NumberStyles.Any style is
// specified. Note, however, that the Parse methods do not accept
// NaNs or Infinities.
//

using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Kzrnm.Numerics.Experiment
{
    using HexConverter = Logic.HexConverter;
    internal static partial class Number
    {
        // We need 1 additional byte, per length, for the terminating null
        internal const int DecimalNumberBufferLength = 29 + 1 + 1;  // 29 for the longest input + 1 for rounding
        internal const int DoubleNumberBufferLength = 767 + 1 + 1;  // 767 for the longest input + 1 for rounding: 4.9406564584124654E-324
        internal const int Int32NumberBufferLength = 10 + 1;    // 10 for the longest input: 2,147,483,647
        internal const int Int64NumberBufferLength = 19 + 1;    // 19 for the longest input: 9,223,372,036,854,775,807
        internal const int Int128NumberBufferLength = 39 + 1;    // 39 for the longest input: 170,141,183,460,469,231,731,687,303,715,884,105,727
        internal const int SingleNumberBufferLength = 112 + 1 + 1;  // 112 for the longest input + 1 for rounding: 1.40129846E-45
        internal const int HalfNumberBufferLength = 21; // 19 for the longest input + 1 for rounding (+1 for the null terminator)
        internal const int UInt32NumberBufferLength = 10 + 1;   // 10 for the longest input: 4,294,967,295
        internal const int UInt64NumberBufferLength = 20 + 1;   // 20 for the longest input: 18,446,744,073,709,551,615
        internal const int UInt128NumberBufferLength = 39 + 1; // 39 for the longest input: 340,282,366,920,938,463,463,374,607,431,768,211,455

        internal ref struct NumberBuffer
        {
            public int DigitsCount;
            public int Scale;
            public bool IsNegative;
            public bool HasNonZeroTail;
            public NumberBufferKind Kind;
            public Span<byte> Digits;


            /// <summary>Initializes the NumberBuffer.</summary>
            /// <param name="kind">The kind of the buffer.</param>
            /// <param name="digits">The digits scratch space. The referenced memory must not be moveable, e.g. stack memory, pinned array, etc.</param>
            public NumberBuffer(NumberBufferKind kind, Span<byte> digits)
            {
                Debug.Assert(!digits.IsEmpty);

                DigitsCount = 0;
                Scale = 0;
                IsNegative = false;
                HasNonZeroTail = false;
                Kind = kind;
                Digits = digits;
#if DEBUG
                Digits.Fill(0xCC);
#endif
                Digits[0] = (byte)'\0';
                CheckConsistency();
            }

#pragma warning disable CA1822
            [Conditional("DEBUG")]
            public void CheckConsistency()
            {
#if DEBUG
                Debug.Assert((Kind == NumberBufferKind.Integer) || (Kind == NumberBufferKind.Decimal) || (Kind == NumberBufferKind.FloatingPoint));
                Debug.Assert(Digits[0] != '0', "Leading zeros should never be stored in a Number");

                int numDigits;
                for (numDigits = 0; numDigits < Digits.Length; numDigits++)
                {
                    byte digit = Digits[numDigits];

                    if (digit == 0)
                    {
                        break;
                    }

                    Debug.Assert(char.IsAsciiDigit((char)digit), $"Unexpected character found in Number: {digit}");
                }

                Debug.Assert(numDigits == DigitsCount, "Null terminator found in unexpected location in Number");
                Debug.Assert(numDigits < Digits.Length, "Null terminator not found in Number");
#endif // DEBUG
            }
#pragma warning restore CA1822

            //
            // Code coverage note: This only exists so that Number displays nicely in the VS watch window. So yes, I know it works.
            //
            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();

                sb.Append('[');
                sb.Append('"');

                for (int i = 0; i < Digits.Length; i++)
                {
                    byte digit = Digits[i];

                    if (digit == 0)
                    {
                        break;
                    }

                    sb.Append((char)(digit));
                }

                sb.Append('"');
                sb.Append(", Length = ").Append(DigitsCount);
                sb.Append(", Scale = ").Append(Scale);
                sb.Append(", IsNegative = ").Append(IsNegative);
                sb.Append(", HasNonZeroTail = ").Append(HasNonZeroTail);
                sb.Append(", Kind = ").Append(Kind);
                sb.Append(']');

                return sb.ToString();
            }
        }

        internal enum NumberBufferKind : byte
        {
            Unknown = 0,
            Integer = 1,
            Decimal = 2,
            FloatingPoint = 3,
        }
    }
    internal static partial class Number
    {
        private const NumberStyles InvalidNumberStyles = ~(NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                                                           | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign
                                                           | NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint
                                                           | NumberStyles.AllowThousands | NumberStyles.AllowExponent
#if NET8_0_OR_GREATER
                                                           | NumberStyles.AllowBinarySpecifier
#endif
                                                           | NumberStyles.AllowCurrencySymbol | NumberStyles.AllowHexSpecifier);

        private static ReadOnlySpan<nuint> NUIntPowersOfTen
            => Environment.Is64BitProcess
            ? MemoryMarshal.Cast<ulong, nuint>(UInt64PowersOfTen)
            : MemoryMarshal.Cast<uint, nuint>(UInt32PowersOfTen);

#if NET8_0_OR_GREATER
        private static ReadOnlySpan<uint> UInt32PowersOfTen => [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000];
        private static ReadOnlySpan<ulong> UInt64PowersOfTen => [
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
            10000000000000000,
            100000000000000000,
            1000000000000000000,
            10000000000000000000,
        ];
#else
        private static uint[] _UInt32PowersOfTenCache = new uint[] { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };
        private static ReadOnlySpan<uint> UInt32PowersOfTen => _UInt32PowersOfTenCache;
        private static ulong[] _UInt64PowersOfTenCache = new ulong[]
        {
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
            10000000000000000,
            100000000000000000,
            1000000000000000000,
            10000000000000000000,
        };
        private static ReadOnlySpan<ulong> UInt64PowersOfTen => _UInt64PowersOfTenCache;
#endif

        [DoesNotReturn]
        internal static void ThrowOverflowOrFormatException(ParsingStatus status) => throw GetException(status);

        private static Exception GetException(ParsingStatus status)
        {
            return status == ParsingStatus.Failed
                ? new FormatException(SR.Overflow_ParseBigInteger)
                : new OverflowException(SR.Overflow_ParseBigInteger);
        }

        internal static bool TryValidateParseStyleInteger(NumberStyles style, [NotNullWhen(false)] out ArgumentException? e)
        {
            // Check for undefined flags
            if ((style & InvalidNumberStyles) != 0)
            {
                e = new ArgumentException(SR.Argument_InvalidNumberStyles, nameof(style));
                return false;
            }
            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            { // Check for hex number
                if ((style & ~NumberStyles.HexNumber) != 0)
                {
                    e = new ArgumentException(SR.Argument_InvalidHexStyle, nameof(style));
                    return false;
                }
            }
            e = null;
            return true;
        }

        internal static ParsingStatus TryParseBigInteger(ReadOnlySpan<char> value, NumberStyles style, NumberFormatInfo info, out BigIntegerNative result)
        {
            if (!TryValidateParseStyleInteger(style, out ArgumentException? e))
            {
                throw e; // TryParse still throws ArgumentException on invalid NumberStyles
            }

            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            {
                return TryParseBigIntegerHexNumberStyle(value, style, out result);
            }

#if NET8_0_OR_GREATER
            if ((style & NumberStyles.AllowBinarySpecifier) != 0)
            {
                return TryParseBigIntegerBinaryNumberStyle(value, style, out result);
            }
#endif

            return TryParseBigIntegerNumber(value, style, info, out result);
        }

        internal static ParsingStatus TryParseBigIntegerNumber(ReadOnlySpan<char> value, NumberStyles style, NumberFormatInfo info, out BigIntegerNative result)
        {
            scoped Span<byte> buffer;
            byte[]? arrayFromPool = null;

            if (value.Length == 0)
            {
                result = default;
                return ParsingStatus.Failed;
            }
            if (value.Length < 255)
            {
                buffer = stackalloc byte[value.Length + 1 + 1];
            }
            else
            {
                buffer = arrayFromPool = ArrayPool<byte>.Shared.Rent(value.Length + 1 + 1);
            }

            ParsingStatus ret;

            NumberBuffer number = new NumberBuffer(NumberBufferKind.Integer, buffer);

            if (!TryStringToNumber(value, ref number))
            {
                result = default;
                ret = ParsingStatus.Failed;
            }
            else
            {
                ret = NumberToBigInteger(ref number, out result);
            }

            if (arrayFromPool != null)
            {
                ArrayPool<byte>.Shared.Return(arrayFromPool);
            }

            return ret;
        }

        internal static BigIntegerNative ParseBigInteger(ReadOnlySpan<char> value, NumberStyles style, NumberFormatInfo info)
        {
            if (!TryValidateParseStyleInteger(style, out ArgumentException? e))
            {
                throw e;
            }

            ParsingStatus status = TryParseBigInteger(value, style, info, out BigIntegerNative result);
            if (status != ParsingStatus.OK)
            {
                ThrowOverflowOrFormatException(status);
            }

            return result;
        }

        internal static ParsingStatus TryParseBigIntegerHexNumberStyle(ReadOnlySpan<char> value, NumberStyles style, out BigIntegerNative result)
        {
            int whiteIndex = 0;

            // Skip past any whitespace at the beginning.
            if ((style & NumberStyles.AllowLeadingWhite) != 0)
            {
                for (whiteIndex = 0; whiteIndex < value.Length; whiteIndex++)
                {
                    if (!IsWhite(value[whiteIndex]))
                        break;
                }

                value = value[whiteIndex..];
            }

            // Skip past any whitespace at the end.
            if ((style & NumberStyles.AllowTrailingWhite) != 0)
            {
                for (whiteIndex = value.Length - 1; whiteIndex >= 0; whiteIndex--)
                {
                    if (!IsWhite(value[whiteIndex]))
                        break;
                }

                value = value[..(whiteIndex + 1)];
            }

            if (value.IsEmpty)
            {
                goto FailExit;
            }

            int digitsPerBlock = Environment.Is64BitProcess ? 16 : 8;

            int totalDigitCount = value.Length;
            int blockCount, partialDigitCount;

            blockCount = Math.DivRem(totalDigitCount, digitsPerBlock, out int remainder);
            if (remainder == 0)
            {
                partialDigitCount = 0;
            }
            else
            {
                blockCount += 1;
                partialDigitCount = digitsPerBlock - remainder;
            }

            if (!HexConverter.IsHexChar(value[0])) goto FailExit;
            bool isNegative = HexConverter.FromChar(value[0]) >= 8;
            nuint partialValue = (isNegative && partialDigitCount > 0)
                ? (Environment.Is64BitProcess ? unchecked((nuint)0xFFFFFFFFFFFFFFFFul) : 0xFFFFFFFFu)
                : 0;

            nuint[]? arrayFromPool = null;

            Span<nuint> bitsBuffer = ((uint)blockCount <= BigIntegerCalculator.StackAllocThreshold
                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                : arrayFromPool = ArrayPool<nuint>.Shared.Rent(blockCount)).Slice(0, blockCount);

            int bitsBufferPos = blockCount - 1;

            try
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char digitChar = value[i];

                    if (!HexConverter.IsHexChar(digitChar)) goto FailExit;
                    int hexValue = HexConverter.FromChar(digitChar);

                    partialValue = (partialValue << 4) | (uint)hexValue;
                    partialDigitCount++;

                    if (partialDigitCount == digitsPerBlock)
                    {
                        bitsBuffer[bitsBufferPos] = partialValue;
                        bitsBufferPos--;
                        partialValue = 0;
                        partialDigitCount = 0;
                    }
                }

                Debug.Assert(partialDigitCount == 0 && bitsBufferPos == -1);

                if (isNegative)
                {
                    NumericsHelpers.DangerousMakeTwosComplement(bitsBuffer);
                }

                // BigInteger requires leading zero blocks to be truncated.
                bitsBuffer = bitsBuffer.TrimEnd(0u);

                int sign;
                nuint[]? bits;

                if (bitsBuffer.IsEmpty)
                {
                    sign = 0;
                    bits = null;
                }
                else if (bitsBuffer.Length == 1 && bitsBuffer[0] <= int.MaxValue)
                {
                    sign = (int)bitsBuffer[0] * (isNegative ? -1 : 1);
                    bits = null;
                }
                else
                {
                    sign = isNegative ? -1 : 1;
                    bits = bitsBuffer.ToArray();
                }

                result = new BigIntegerNative(sign, bits);
                return ParsingStatus.OK;
            }
            finally
            {
                if (arrayFromPool != null)
                {
                    ArrayPool<nuint>.Shared.Return(arrayFromPool);
                }
            }

        FailExit:
            result = default;
            return ParsingStatus.Failed;
        }

        internal static ParsingStatus TryParseBigIntegerBinaryNumberStyle(ReadOnlySpan<char> value, NumberStyles style, out BigIntegerNative result)
        {
            int whiteIndex = 0;

            // Skip past any whitespace at the beginning.
            if ((style & NumberStyles.AllowLeadingWhite) != 0)
            {
                for (whiteIndex = 0; whiteIndex < value.Length; whiteIndex++)
                {
                    if (!IsWhite(value[whiteIndex]))
                        break;
                }

                value = value[whiteIndex..];
            }

            // Skip past any whitespace at the end.
            if ((style & NumberStyles.AllowTrailingWhite) != 0)
            {
                for (whiteIndex = value.Length - 1; whiteIndex >= 0; whiteIndex--)
                {
                    if (!IsWhite(value[whiteIndex]))
                        break;
                }

                value = value[..(whiteIndex + 1)];
            }

            if (value.IsEmpty)
            {
                goto FailExit;
            }

            int totalDigitCount = value.Length;
            int partialDigitCount;

            (int blockCount, int remainder) = int.DivRem(totalDigitCount, BigInteger.kcbitUint);
            if (remainder == 0)
            {
                partialDigitCount = 0;
            }
            else
            {
                blockCount++;
                partialDigitCount = BigInteger.kcbitUint - remainder;
            }

            if (value[0] is not ('0' or '1')) goto FailExit;
            bool isNegative = value[0] == '1';
            nuint currentBlock = isNegative
                ? (Environment.Is64BitProcess ? unchecked((nuint)0xFFFFFFFFFFFFFFFFu) : 0xFFFFFFFFu)
                : 0x0;

            nuint[]? arrayFromPool = null;
            Span<nuint> bitsBuffer = ((uint)blockCount <= BigIntegerCalculator.StackAllocThreshold
                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                : arrayFromPool = ArrayPool<nuint>.Shared.Rent(blockCount)).Slice(0, blockCount);

            int bufferPos = blockCount - 1;

            try
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char digitChar = value[i];

                    if (digitChar is not ('0' or '1')) goto FailExit;
                    currentBlock = (currentBlock << 1) | (uint)(digitChar - '0');
                    partialDigitCount++;

                    if (partialDigitCount == BigIntegerNative.kcbitNUint)
                    {
                        bitsBuffer[bufferPos--] = currentBlock;
                        partialDigitCount = 0;

                        // we do not need to reset currentBlock now, because it should always set all its bits by left shift in subsequent iterations
                    }
                }

                Debug.Assert(partialDigitCount == 0 && bufferPos == -1);

                if (isNegative)
                {
                    NumericsHelpers.DangerousMakeTwosComplement(bitsBuffer);
                }
                bitsBuffer = bitsBuffer.TrimEnd(0u);

                int sign;
                nuint[]? bits;

                if (bitsBuffer.IsEmpty)
                {
                    sign = 0;
                    bits = null;
                }
                else if (bitsBuffer.Length == 1 && bitsBuffer[0] <= int.MaxValue)
                {
                    sign = (int)bitsBuffer[0] * (isNegative ? -1 : 1);
                    bits = null;
                }
                else
                {
                    sign = isNegative ? -1 : 1;
                    bits = bitsBuffer.ToArray();
                }

                result = new BigIntegerNative(sign, bits);
                return ParsingStatus.OK;
            }
            finally
            {
                if (arrayFromPool is not null)
                {
                    ArrayPool<nuint>.Shared.Return(arrayFromPool);
                }
            }

        FailExit:
            result = default;
            return ParsingStatus.Failed;
        }

        //
        // This threshold is for choosing the algorithm to use based on the number of digits.
        //
        // Let N be the number of digits. If N is less than or equal to the bound, use a naive
        // algorithm with a running time of O(N^2). And if it is greater than the threshold, use
        // a divide-and-conquer algorithm with a running time of O(NlogN).
        //
#if DEBUG
        // Mutable for unit testing...
        internal static
#else
        internal const
#endif
        int s_naiveThreshold = 3200;
        private static ParsingStatus NumberToBigInteger(ref NumberBuffer number, out BigIntegerNative result)
        {
            if ((uint)number.Scale >= int.MaxValue)
            {
                result = default;
                return number.Scale == int.MaxValue
                    ? ParsingStatus.Overflow
                    : ParsingStatus.Failed;
            }
            if (Environment.Is64BitProcess)
                return NumberToBigIntegerUInt64(ref number, out result);
            return NumberToBigIntegerUInt32(ref number, out result);
        }
        static ParsingStatus NumberToBigIntegerUInt64(ref NumberBuffer number, out BigIntegerNative result)
        {
            Debug.Assert(Environment.Is64BitProcess);
            const int MaxPartialDigits = 19;
            const ulong TenPowMaxPartial = 1000000000_000000000_0;

            if (number.Scale <= s_naiveThreshold)
            {
                return Naive(ref number, out result);
            }
            else
            {
                return DivideAndConquer(ref number, out result);
            }

            static ParsingStatus Naive(ref NumberBuffer number, out BigIntegerNative result)
            {
                int numberScale = number.Scale;

                ulong[]? arrayFromPoolForResultBuffer = null;
                int currentBufferSize = 0;
                int totalDigitCount = 0;
                Span<ulong> stackBuffer = stackalloc ulong[BigIntegerCalculator.StackAllocThreshold];
                Span<ulong> currentBuffer = stackBuffer;
                ulong partialValue = 0;
                int partialDigitCount = 0;

                if (!ProcessChunk(number.Digits[..number.DigitsCount], ref currentBuffer))
                {
                    result = default;
                    return ParsingStatus.Failed;
                }

                if (partialDigitCount > 0)
                {
                    MultiplyAdd(ref currentBuffer, UInt64PowersOfTen[partialDigitCount], partialValue);
                }

                int trailingZeroCount = numberScale - totalDigitCount;
                while (trailingZeroCount >= MaxPartialDigits)
                {
                    MultiplyAdd(ref currentBuffer, TenPowMaxPartial, 0);
                    trailingZeroCount -= MaxPartialDigits;
                }
                if (trailingZeroCount > 0)
                {
                    MultiplyAdd(ref currentBuffer, UInt64PowersOfTen[trailingZeroCount], 0);
                }

                result = NumberBufferToBigInteger(MemoryMarshal.Cast<ulong, nuint>(currentBuffer.Slice(0, currentBufferSize)), number.IsNegative);
                return ParsingStatus.OK;

                bool ProcessChunk(ReadOnlySpan<byte> chunkDigits, ref Span<ulong> currentBuffer)
                {
                    int remainingIntDigitCount = Math.Max(numberScale - totalDigitCount, 0);
                    ReadOnlySpan<byte> intDigitsSpan = chunkDigits.Slice(0, Math.Min(remainingIntDigitCount, chunkDigits.Length));

                    bool endReached = false;

                    // Storing these captured variables in locals for faster access in the loop.
                    ulong _partialValue = partialValue;
                    int _partialDigitCount = partialDigitCount;
                    int _totalDigitCount = totalDigitCount;

                    for (int i = 0; i < intDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)chunkDigits[i];
                        if (digitChar == '\0')
                        {
                            endReached = true;
                            break;
                        }

                        _partialValue = _partialValue * 10 + (uint)(digitChar - '0');
                        _partialDigitCount++;
                        _totalDigitCount++;

                        // Update the buffer when enough partial digits have been accumulated.
                        if (_partialDigitCount == MaxPartialDigits)
                        {
                            MultiplyAdd(ref currentBuffer, TenPowMaxPartial, _partialValue);
                            _partialValue = 0;
                            _partialDigitCount = 0;
                        }
                    }

                    // Check for nonzero digits after the decimal point.
                    if (!endReached)
                    {
                        ReadOnlySpan<byte> fracDigitsSpan = chunkDigits.Slice(intDigitsSpan.Length);
                        for (int i = 0; i < fracDigitsSpan.Length; i++)
                        {
                            char digitChar = (char)fracDigitsSpan[i];
                            if (digitChar == '\0')
                            {
                                break;
                            }
                            if (digitChar != '0')
                            {
                                return false;
                            }
                        }
                    }

                    partialValue = _partialValue;
                    partialDigitCount = _partialDigitCount;
                    totalDigitCount = _totalDigitCount;

                    return true;
                }

                // This function should only be used for result buffer.
                void MultiplyAdd(ref Span<ulong> currentBuffer, ulong multiplier, ulong addValue)
                {
                    Span<ulong> curBits = currentBuffer.Slice(0, currentBufferSize);
                    ulong carry = addValue;

                    for (int i = 0; i < curBits.Length; i++)
                    {
                        var hi = Math.BigMul(multiplier, curBits[i], out curBits[i]);
                        curBits[i] += carry;
                        if (curBits[i] < carry)
                            ++hi;
                        carry = hi;
                    }

                    if (carry == 0)
                    {
                        return;
                    }

                    if (currentBufferSize == currentBuffer.Length)
                    {
                        ulong[]? arrayToReturn = arrayFromPoolForResultBuffer;

                        arrayFromPoolForResultBuffer = ArrayPool<ulong>.Shared.Rent(checked(currentBufferSize * 2));
                        Span<ulong> newBuffer = new Span<ulong>(arrayFromPoolForResultBuffer);
                        currentBuffer.CopyTo(newBuffer);
                        currentBuffer = newBuffer;

                        if (arrayToReturn != null)
                        {
                            ArrayPool<ulong>.Shared.Return(arrayToReturn);
                        }
                    }

                    currentBuffer[currentBufferSize] = carry;
                    currentBufferSize++;
                }
            }

            static ParsingStatus DivideAndConquer(ref NumberBuffer number, out BigIntegerNative result)
            {
                // log_{2^64}(10^19)
                const double digitRatio = 0.98619740317;

                Span<nuint> currentBuffer;
                nuint[]? arrayFromPoolForMultiplier = null;
                nuint[]? arrayFromPoolForResultBuffer = null;
                nuint[]? arrayFromPoolForResultBuffer2 = null;
                nuint[]? arrayFromPoolForTrailingZero = null;
                try
                {
                    int totalDigitCount = Math.Min(number.DigitsCount, number.Scale);
                    int trailingZeroCount = number.Scale - totalDigitCount;
                    int bufferSize = (totalDigitCount + MaxPartialDigits - 1) / MaxPartialDigits;

                    Span<nuint> buffer = new Span<nuint>(arrayFromPoolForResultBuffer = ArrayPool<nuint>.Shared.Rent(bufferSize), 0, bufferSize);
                    Span<nuint> newBuffer = new Span<nuint>(arrayFromPoolForResultBuffer2 = ArrayPool<nuint>.Shared.Rent(bufferSize), 0, bufferSize);
                    newBuffer.Clear();

                    int trailingZeroE9 = Math.DivRem(trailingZeroCount, MaxPartialDigits, out int trailingZeroRemainder);
                    int trailingZeroBufferLength = checked((int)(digitRatio * (trailingZeroE9 + Math.Max(trailingZeroRemainder, 1))) + 1);
                    Span<nuint> trailingZeroBuffer = (trailingZeroBufferLength <= BigIntegerCalculator.StackAllocThreshold
                        ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                        : arrayFromPoolForTrailingZero = ArrayPool<nuint>.Shared.Rent(trailingZeroBufferLength)).Slice(0, trailingZeroBufferLength);

                    int currentTrailingZeroBufferLength = 1;
                    trailingZeroBuffer.Slice(1).Clear();
                    trailingZeroBuffer[0] = (nuint)UInt64PowersOfTen[trailingZeroRemainder];

                    // Separate every MaxPartialDigits digits and store them in the buffer.
                    // Buffers are treated as little-endian. That means, the array { 234567890, 1 }
                    // represents the number 1234567890.
                    int bufferIndex = bufferSize - 1;
                    nuint currentBlock = 0;
                    int shiftUntil = (totalDigitCount - 1) % MaxPartialDigits;
                    int remainingIntDigitCount = totalDigitCount;

                    ReadOnlySpan<byte> digitsChunkSpan = number.Digits[..number.DigitsCount];
                    ReadOnlySpan<byte> intDigitsSpan = digitsChunkSpan.Slice(0, Math.Min(remainingIntDigitCount, digitsChunkSpan.Length));

                    for (int i = 0; i < intDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)intDigitsSpan[i];
                        Debug.Assert(char.IsDigit(digitChar));
                        currentBlock *= 10;
                        currentBlock += unchecked((uint)(digitChar - '0'));
                        if (shiftUntil == 0)
                        {
                            buffer[bufferIndex] = currentBlock;
                            currentBlock = 0;
                            bufferIndex--;
                            shiftUntil = MaxPartialDigits;
                        }
                        shiftUntil--;
                    }
                    remainingIntDigitCount -= intDigitsSpan.Length;
                    Debug.Assert(0 <= remainingIntDigitCount);

                    ReadOnlySpan<byte> fracDigitsSpan = digitsChunkSpan.Slice(intDigitsSpan.Length);
                    for (int i = 0; i < fracDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)fracDigitsSpan[i];
                        if (digitChar == '\0')
                        {
                            break;
                        }
                        if (digitChar != '0')
                        {
                            result = default;
                            return ParsingStatus.Failed;
                        }
                    }

                    Debug.Assert(currentBlock == 0);
                    Debug.Assert(bufferIndex == -1);

                    int blockSize = 1;
                    int multiplierSize = 1;
                    Span<nuint> multiplier = stackalloc nuint[1] { unchecked((nuint)TenPowMaxPartial) };

                    // This loop is executed ceil(log_2(bufferSize)) times.
                    while (true)
                    {
                        if ((trailingZeroE9 & 1) != 0)
                        {
                            nuint[]? previousTrailingZeroBufferFromPool = null;
                            Span<nuint> previousTrailingZeroBuffer = new Span<nuint>(
                                previousTrailingZeroBufferFromPool = ArrayPool<nuint>.Shared.Rent(currentTrailingZeroBufferLength),
                                0, currentTrailingZeroBufferLength);

                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).CopyTo(previousTrailingZeroBuffer);
                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).Clear();

                            int newTrailingZeroBufferLength = currentTrailingZeroBufferLength + multiplier.Length;
                            if (multiplier.Length < previousTrailingZeroBuffer.Length)
                                BigIntegerCalculator.Multiply(previousTrailingZeroBuffer, multiplier, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));
                            else
                                BigIntegerCalculator.Multiply(multiplier, previousTrailingZeroBuffer, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));

                            currentTrailingZeroBufferLength = newTrailingZeroBufferLength;
                            while (--currentTrailingZeroBufferLength >= 0 && trailingZeroBuffer[currentTrailingZeroBufferLength] == 0) ;
                            ++currentTrailingZeroBufferLength;

                            if (previousTrailingZeroBufferFromPool != null)
                                ArrayPool<nuint>.Shared.Return(previousTrailingZeroBufferFromPool);

                            Debug.Assert(currentTrailingZeroBufferLength >= 1);
                        }
                        trailingZeroE9 >>= 1;

                        // merge each block pairs.
                        // When buffer represents:
                        // |     A     |     B     |     C     |     D     |
                        // Make newBuffer like:
                        // |  A + B * multiplier   |  C + D * multiplier   |
                        for (int i = 0; i < bufferSize; i += blockSize * 2)
                        {
                            Span<nuint> curBuffer = buffer.Slice(i);
                            Span<nuint> curNewBuffer = newBuffer.Slice(i);

                            int len = Math.Min(bufferSize - i, blockSize * 2);
                            int lowerLen = Math.Min(len, blockSize);
                            int upperLen = len - lowerLen;
                            if (upperLen != 0)
                            {
                                Debug.Assert(blockSize == lowerLen);
                                Debug.Assert(blockSize >= multiplier.Length);
                                ReadOnlySpan<nuint> curBufferTrimmed = curBuffer.Slice(blockSize, upperLen).TrimEnd(0u);
                                Debug.Assert(multiplier.Length >= curBufferTrimmed.Length);
                                Debug.Assert(multiplier.Length + curBufferTrimmed.Length <= len);
                                BigIntegerCalculator.Multiply(multiplier, curBufferTrimmed, curNewBuffer.Slice(0, multiplier.Length + curBufferTrimmed.Length));
                            }

                            nuint carry = 0;
                            int j = 0;
                            for (; j < lowerLen; j++)
                            {
                                ref var cur = ref curNewBuffer[j];
                                cur += carry;
                                carry = cur < carry ? 1u : 0;
                                cur += curBuffer[j];
                                if (cur < curBuffer[j])
                                    ++carry;
                            }
                            if (carry != 0)
                            {
                                while (true)
                                {
                                    curNewBuffer[j]++;
                                    if (curNewBuffer[j] != 0)
                                    {
                                        break;
                                    }
                                    j++;
                                }
                            }
                        }

                        Span<nuint> tmp = buffer;
                        buffer = newBuffer;
                        newBuffer = tmp;
                        blockSize <<= 1;

                        if (bufferSize <= blockSize)
                        {
                            break;
                        }
                        multiplierSize <<= 1;
                        newBuffer.Clear();
                        nuint[]? arrayToReturn = arrayFromPoolForMultiplier;

                        Span<nuint> newMultiplier = new Span<nuint>(
                            arrayFromPoolForMultiplier = ArrayPool<nuint>.Shared.Rent(multiplierSize),
                             0, multiplierSize);
                        newMultiplier.Clear();
                        BigIntegerCalculator.Square(multiplier, newMultiplier);
                        multiplier = newMultiplier;

                        while (--multiplierSize >= 0 && multiplier[multiplierSize] == 0) ;
                        multiplier = multiplier.Slice(0, ++multiplierSize);

                        if (arrayToReturn is not null)
                        {
                            ArrayPool<nuint>.Shared.Return(arrayToReturn);
                        }
                    }

                    while (trailingZeroE9 != 0)
                    {
                        nuint[]? arrayToReturn = arrayFromPoolForMultiplier;

                        multiplierSize <<= 1;
                        arrayFromPoolForMultiplier = ArrayPool<nuint>.Shared.Rent(multiplierSize);
                        Span<nuint> newMultiplier = new Span<nuint>(arrayFromPoolForMultiplier, 0, multiplierSize);
                        newMultiplier.Clear();
                        BigIntegerCalculator.Square(multiplier, newMultiplier);
                        multiplier = newMultiplier;

                        while (--multiplierSize >= 0 && multiplier[multiplierSize] == 0) ;
                        multiplier = multiplier.Slice(0, ++multiplierSize);

                        if (arrayToReturn is not null)
                        {
                            ArrayPool<nuint>.Shared.Return(arrayToReturn);
                        }

                        if ((trailingZeroE9 & 1) != 0)
                        {
                            nuint[]? previousTrailingZeroBufferFromPool = null;
                            Span<nuint> previousTrailingZeroBuffer = new Span<nuint>(
                                previousTrailingZeroBufferFromPool = ArrayPool<nuint>.Shared.Rent(currentTrailingZeroBufferLength),
                                0, currentTrailingZeroBufferLength);

                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).CopyTo(previousTrailingZeroBuffer);
                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).Clear();

                            int newTrailingZeroBufferLength = currentTrailingZeroBufferLength + multiplier.Length;
                            if (multiplier.Length < previousTrailingZeroBuffer.Length)
                                BigIntegerCalculator.Multiply(previousTrailingZeroBuffer, multiplier, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));
                            else
                                BigIntegerCalculator.Multiply(multiplier, previousTrailingZeroBuffer, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));

                            currentTrailingZeroBufferLength = newTrailingZeroBufferLength;
                            while (--currentTrailingZeroBufferLength >= 0 && trailingZeroBuffer[currentTrailingZeroBufferLength] == 0) ;
                            ++currentTrailingZeroBufferLength;

                            if (previousTrailingZeroBufferFromPool != null)
                                ArrayPool<nuint>.Shared.Return(previousTrailingZeroBufferFromPool);

                            Debug.Assert(currentTrailingZeroBufferLength >= 1);
                        }
                        trailingZeroE9 >>= 1;
                    }


                    // shrink buffer to the currently used portion.
                    // First, calculate the rough size of the buffer from the ratio that the number
                    // of digits follows. Then, shrink the size until there is no more space left.
                    // The Ratio is calculated as: log_{2^32}(10^9)
                    int currentBufferSize = Math.Min((int)(bufferSize * digitRatio) + 1, bufferSize);
                    Debug.Assert(buffer.Length == currentBufferSize || buffer.Slice(currentBufferSize).Trim(0u).Length == 0);
                    while (--currentBufferSize >= 0 && buffer[currentBufferSize] == 0) ;
                    ++currentBufferSize;
                    currentBuffer = buffer.Slice(0, currentBufferSize);

                    trailingZeroBuffer = trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength);
                    if (trailingZeroBuffer.Length <= 1)
                    {
                        Debug.Assert(trailingZeroBuffer.Length == 1);
                        nuint trailingZero = trailingZeroBuffer[0];
                        if (trailingZero != 1)
                        {
                            int i = 0;
                            ulong carry = 0UL;

                            for (; i < currentBuffer.Length; i++)
                            {
                                ref ulong cur = ref Unsafe.As<nuint, ulong>(ref currentBuffer[i]);
                                var hi = Math.BigMul(cur, trailingZero, out cur);
                                cur += carry;
                                if (cur < carry)
                                    ++hi;
                                carry = hi;
                            }
                            if (carry != 0)
                            {
                                currentBuffer = buffer.Slice(0, ++currentBufferSize);
                                currentBuffer[i] = (uint)carry;
                            }
                        }

                        result = NumberBufferToBigInteger(currentBuffer, number.IsNegative);
                    }
                    else
                    {
                        int resultBufferLength = checked(currentBufferSize + trailingZeroBuffer.Length);
                        Span<nuint> resultBuffer = (resultBufferLength <= BigIntegerCalculator.StackAllocThreshold
                            ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                            : arrayFromPoolForTrailingZero = ArrayPool<nuint>.Shared.Rent(resultBufferLength)).Slice(0, resultBufferLength);
                        resultBuffer.Clear();

                        if (trailingZeroBuffer.Length < currentBuffer.Length)
                            BigIntegerCalculator.Multiply(currentBuffer, trailingZeroBuffer, resultBuffer);
                        else
                            BigIntegerCalculator.Multiply(trailingZeroBuffer, currentBuffer, resultBuffer);

                        while (--resultBufferLength >= 0 && resultBuffer[resultBufferLength] == 0) ;
                        ++resultBufferLength;

                        result = NumberBufferToBigInteger(resultBuffer.Slice(0, resultBufferLength), number.IsNegative);
                    }
                }
                finally
                {
                    if (arrayFromPoolForMultiplier != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForMultiplier);

                    if (arrayFromPoolForResultBuffer != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForResultBuffer);

                    if (arrayFromPoolForResultBuffer2 != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForResultBuffer2);

                    if (arrayFromPoolForTrailingZero != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForTrailingZero);
                }
                return ParsingStatus.OK;
            }
        }

        static ParsingStatus NumberToBigIntegerUInt32(ref NumberBuffer number, out BigIntegerNative result)
        {
            Debug.Assert(!Environment.Is64BitProcess);
            const int MaxPartialDigits = 9;
            const uint TenPowMaxPartial = 1000000000;

            if (number.Scale <= s_naiveThreshold)
            {
                return Naive(ref number, out result);
            }
            else
            {
                return DivideAndConquer(ref number, out result);
            }

            static ParsingStatus Naive(ref NumberBuffer number, out BigIntegerNative result)
            {
                int numberScale = number.Scale;

                uint[]? arrayFromPoolForResultBuffer = null;
                int currentBufferSize = 0;
                int totalDigitCount = 0;
                Span<uint> stackBuffer = stackalloc uint[BigIntegerCalculator.StackAllocThreshold];
                Span<uint> currentBuffer = stackBuffer;
                uint partialValue = 0;
                int partialDigitCount = 0;

                if (!ProcessChunk(number.Digits[..number.DigitsCount], ref currentBuffer))
                {
                    result = default;
                    return ParsingStatus.Failed;
                }

                if (partialDigitCount > 0)
                {
                    MultiplyAdd(ref currentBuffer, UInt32PowersOfTen[partialDigitCount], partialValue);
                }

                int trailingZeroCount = numberScale - totalDigitCount;
                while (trailingZeroCount >= MaxPartialDigits)
                {
                    MultiplyAdd(ref currentBuffer, TenPowMaxPartial, 0);
                    trailingZeroCount -= MaxPartialDigits;
                }
                if (trailingZeroCount > 0)
                {
                    MultiplyAdd(ref currentBuffer, UInt32PowersOfTen[trailingZeroCount], 0);
                }

                result = NumberBufferToBigInteger(MemoryMarshal.Cast<uint, nuint>(currentBuffer.Slice(0, currentBufferSize)), number.IsNegative);
                return ParsingStatus.OK;

                bool ProcessChunk(ReadOnlySpan<byte> chunkDigits, ref Span<uint> currentBuffer)
                {
                    int remainingIntDigitCount = Math.Max(numberScale - totalDigitCount, 0);
                    ReadOnlySpan<byte> intDigitsSpan = chunkDigits.Slice(0, Math.Min(remainingIntDigitCount, chunkDigits.Length));

                    bool endReached = false;

                    // Storing these captured variables in locals for faster access in the loop.
                    uint _partialValue = partialValue;
                    int _partialDigitCount = partialDigitCount;
                    int _totalDigitCount = totalDigitCount;

                    for (int i = 0; i < intDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)chunkDigits[i];
                        if (digitChar == '\0')
                        {
                            endReached = true;
                            break;
                        }

                        _partialValue = _partialValue * 10 + (uint)(digitChar - '0');
                        _partialDigitCount++;
                        _totalDigitCount++;

                        // Update the buffer when enough partial digits have been accumulated.
                        if (_partialDigitCount == MaxPartialDigits)
                        {
                            MultiplyAdd(ref currentBuffer, TenPowMaxPartial, _partialValue);
                            _partialValue = 0;
                            _partialDigitCount = 0;
                        }
                    }

                    // Check for nonzero digits after the decimal point.
                    if (!endReached)
                    {
                        ReadOnlySpan<byte> fracDigitsSpan = chunkDigits.Slice(intDigitsSpan.Length);
                        for (int i = 0; i < fracDigitsSpan.Length; i++)
                        {
                            char digitChar = (char)fracDigitsSpan[i];
                            if (digitChar == '\0')
                            {
                                break;
                            }
                            if (digitChar != '0')
                            {
                                return false;
                            }
                        }
                    }

                    partialValue = _partialValue;
                    partialDigitCount = _partialDigitCount;
                    totalDigitCount = _totalDigitCount;

                    return true;
                }

                // This function should only be used for result buffer.
                void MultiplyAdd(ref Span<uint> currentBuffer, uint multiplier, uint addValue)
                {
                    Span<uint> curBits = currentBuffer.Slice(0, currentBufferSize);
                    uint carry = addValue;

                    for (int i = 0; i < curBits.Length; i++)
                    {
                        ulong p = (ulong)multiplier * curBits[i] + carry;
                        curBits[i] = (uint)p;
                        carry = (uint)(p >> 32);
                    }

                    if (carry == 0)
                    {
                        return;
                    }

                    if (currentBufferSize == currentBuffer.Length)
                    {
                        uint[]? arrayToReturn = arrayFromPoolForResultBuffer;

                        arrayFromPoolForResultBuffer = ArrayPool<uint>.Shared.Rent(checked(currentBufferSize * 2));
                        Span<uint> newBuffer = new Span<uint>(arrayFromPoolForResultBuffer);
                        currentBuffer.CopyTo(newBuffer);
                        currentBuffer = newBuffer;

                        if (arrayToReturn != null)
                        {
                            ArrayPool<uint>.Shared.Return(arrayToReturn);
                        }
                    }

                    currentBuffer[currentBufferSize] = carry;
                    currentBufferSize++;
                }
            }

            static ParsingStatus DivideAndConquer(ref NumberBuffer number, out BigIntegerNative result)
            {
                // log_{2^32}(10^9)
                const double digitRatio = 0.934292276687070661;

                scoped Span<nuint> currentBuffer;
                nuint[]? arrayFromPoolForMultiplier = null;
                nuint[]? arrayFromPoolForResultBuffer = null;
                nuint[]? arrayFromPoolForResultBuffer2 = null;
                nuint[]? arrayFromPoolForTrailingZero = null;
                try
                {
                    int totalDigitCount = Math.Min(number.DigitsCount, number.Scale);
                    int trailingZeroCount = number.Scale - totalDigitCount;
                    int bufferSize = (totalDigitCount + MaxPartialDigits - 1) / MaxPartialDigits;

                    Span<nuint> buffer = new Span<nuint>(arrayFromPoolForResultBuffer = ArrayPool<nuint>.Shared.Rent(bufferSize), 0, bufferSize);
                    Span<nuint> newBuffer = new Span<nuint>(arrayFromPoolForResultBuffer2 = ArrayPool<nuint>.Shared.Rent(bufferSize), 0, bufferSize);
                    newBuffer.Clear();

                    int trailingZeroE9 = Math.DivRem(trailingZeroCount, MaxPartialDigits, out int trailingZeroRemainder);
                    int trailingZeroBufferLength = checked((int)(digitRatio * (trailingZeroE9 + Math.Max(trailingZeroRemainder, 1))) + 1);
                    Span<nuint> trailingZeroBuffer = (trailingZeroBufferLength <= BigIntegerCalculator.StackAllocThreshold
                        ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                        : arrayFromPoolForTrailingZero = ArrayPool<nuint>.Shared.Rent(trailingZeroBufferLength)).Slice(0, trailingZeroBufferLength);

                    int currentTrailingZeroBufferLength = 1;
                    trailingZeroBuffer.Slice(1).Clear();
                    trailingZeroBuffer[0] = UInt32PowersOfTen[trailingZeroRemainder];

                    // Separate every MaxPartialDigits digits and store them in the buffer.
                    // Buffers are treated as little-endian. That means, the array { 234567890, 1 }
                    // represents the number 1234567890.
                    int bufferIndex = bufferSize - 1;
                    uint currentBlock = 0;
                    int shiftUntil = (totalDigitCount - 1) % MaxPartialDigits;
                    int remainingIntDigitCount = totalDigitCount;

                    ReadOnlySpan<byte> digitsChunkSpan = number.Digits[..number.DigitsCount];
                    ReadOnlySpan<byte> intDigitsSpan = digitsChunkSpan.Slice(0, Math.Min(remainingIntDigitCount, digitsChunkSpan.Length));

                    for (int i = 0; i < intDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)intDigitsSpan[i];
                        Debug.Assert(char.IsDigit(digitChar));
                        currentBlock *= 10;
                        currentBlock += unchecked((uint)(digitChar - '0'));
                        if (shiftUntil == 0)
                        {
                            buffer[bufferIndex] = currentBlock;
                            currentBlock = 0;
                            bufferIndex--;
                            shiftUntil = MaxPartialDigits;
                        }
                        shiftUntil--;
                    }
                    remainingIntDigitCount -= intDigitsSpan.Length;
                    Debug.Assert(0 <= remainingIntDigitCount);

                    ReadOnlySpan<byte> fracDigitsSpan = digitsChunkSpan.Slice(intDigitsSpan.Length);
                    for (int i = 0; i < fracDigitsSpan.Length; i++)
                    {
                        char digitChar = (char)fracDigitsSpan[i];
                        if (digitChar == '\0')
                        {
                            break;
                        }
                        if (digitChar != '0')
                        {
                            result = default;
                            return ParsingStatus.Failed;
                        }
                    }

                    Debug.Assert(currentBlock == 0);
                    Debug.Assert(bufferIndex == -1);

                    int blockSize = 1;
                    int multiplierSize = 1;
                    Span<nuint> multiplier = stackalloc nuint[1] { TenPowMaxPartial };

                    // This loop is executed ceil(log_2(bufferSize)) times.
                    while (true)
                    {
                        if ((trailingZeroE9 & 1) != 0)
                        {
                            nuint[]? previousTrailingZeroBufferFromPool = null;
                            Span<nuint> previousTrailingZeroBuffer = new Span<nuint>(
                                previousTrailingZeroBufferFromPool = ArrayPool<nuint>.Shared.Rent(currentTrailingZeroBufferLength),
                                0, currentTrailingZeroBufferLength);

                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).CopyTo(previousTrailingZeroBuffer);
                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).Clear();

                            int newTrailingZeroBufferLength = currentTrailingZeroBufferLength + multiplier.Length;
                            if (multiplier.Length < previousTrailingZeroBuffer.Length)
                                BigIntegerCalculator.Multiply(previousTrailingZeroBuffer, multiplier, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));
                            else
                                BigIntegerCalculator.Multiply(multiplier, previousTrailingZeroBuffer, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));

                            currentTrailingZeroBufferLength = newTrailingZeroBufferLength;
                            while (--currentTrailingZeroBufferLength >= 0 && trailingZeroBuffer[currentTrailingZeroBufferLength] == 0) ;
                            ++currentTrailingZeroBufferLength;

                            if (previousTrailingZeroBufferFromPool != null)
                                ArrayPool<nuint>.Shared.Return(previousTrailingZeroBufferFromPool);

                            Debug.Assert(currentTrailingZeroBufferLength >= 1);
                        }
                        trailingZeroE9 >>= 1;

                        // merge each block pairs.
                        // When buffer represents:
                        // |     A     |     B     |     C     |     D     |
                        // Make newBuffer like:
                        // |  A + B * multiplier   |  C + D * multiplier   |
                        for (int i = 0; i < bufferSize; i += blockSize * 2)
                        {
                            Span<nuint> curBuffer = buffer.Slice(i);
                            Span<nuint> curNewBuffer = newBuffer.Slice(i);

                            int len = Math.Min(bufferSize - i, blockSize * 2);
                            int lowerLen = Math.Min(len, blockSize);
                            int upperLen = len - lowerLen;
                            if (upperLen != 0)
                            {
                                Debug.Assert(blockSize == lowerLen);
                                Debug.Assert(blockSize >= multiplier.Length);
                                Debug.Assert(multiplier.Length >= curBuffer.Slice(blockSize, upperLen).TrimEnd(0u).Length);

                                BigIntegerCalculator.Multiply(multiplier, curBuffer.Slice(blockSize, upperLen).TrimEnd(0u), curNewBuffer.Slice(0, len));
                            }

                            long carry = 0;
                            int j = 0;
                            for (; j < lowerLen; j++)
                            {
                                long digit = ((uint)curBuffer[j] + carry) + (uint)curNewBuffer[j];
                                curNewBuffer[j] = unchecked((uint)digit);
                                carry = digit >> 32;
                            }
                            if (carry != 0)
                            {
                                while (true)
                                {
                                    curNewBuffer[j]++;
                                    if (curNewBuffer[j] != 0)
                                    {
                                        break;
                                    }
                                    j++;
                                }
                            }
                        }

                        Span<nuint> tmp = buffer;
                        buffer = newBuffer;
                        newBuffer = tmp;
                        blockSize <<= 1;

                        if (bufferSize <= blockSize)
                        {
                            break;
                        }
                        multiplierSize <<= 1;
                        newBuffer.Clear();
                        nuint[]? arrayToReturn = arrayFromPoolForMultiplier;

                        Span<nuint> newMultiplier = new Span<nuint>(
                            arrayFromPoolForMultiplier = ArrayPool<nuint>.Shared.Rent(multiplierSize),
                             0, multiplierSize);
                        newMultiplier.Clear();
                        BigIntegerCalculator.Square(multiplier, newMultiplier);
                        multiplier = newMultiplier;

                        while (--multiplierSize >= 0 && multiplier[multiplierSize] == 0) ;
                        multiplier = multiplier.Slice(0, ++multiplierSize);

                        if (arrayToReturn is not null)
                        {
                            ArrayPool<nuint>.Shared.Return(arrayToReturn);
                        }
                    }

                    while (trailingZeroE9 != 0)
                    {
                        nuint[]? arrayToReturn = arrayFromPoolForMultiplier;

                        multiplierSize <<= 1;
                        arrayFromPoolForMultiplier = ArrayPool<nuint>.Shared.Rent(multiplierSize);
                        Span<nuint> newMultiplier = new Span<nuint>(arrayFromPoolForMultiplier, 0, multiplierSize);
                        newMultiplier.Clear();
                        BigIntegerCalculator.Square(multiplier, newMultiplier);
                        multiplier = newMultiplier;

                        while (--multiplierSize >= 0 && multiplier[multiplierSize] == 0) ;
                        multiplier = multiplier.Slice(0, ++multiplierSize);

                        if (arrayToReturn is not null)
                        {
                            ArrayPool<nuint>.Shared.Return(arrayToReturn);
                        }

                        if ((trailingZeroE9 & 1) != 0)
                        {
                            nuint[]? previousTrailingZeroBufferFromPool = null;
                            Span<nuint> previousTrailingZeroBuffer = new Span<nuint>(
                                previousTrailingZeroBufferFromPool = ArrayPool<nuint>.Shared.Rent(currentTrailingZeroBufferLength),
                                0, currentTrailingZeroBufferLength);

                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).CopyTo(previousTrailingZeroBuffer);
                            trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength).Clear();

                            int newTrailingZeroBufferLength = currentTrailingZeroBufferLength + multiplier.Length;
                            if (multiplier.Length < previousTrailingZeroBuffer.Length)
                                BigIntegerCalculator.Multiply(previousTrailingZeroBuffer, multiplier, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));
                            else
                                BigIntegerCalculator.Multiply(multiplier, previousTrailingZeroBuffer, trailingZeroBuffer.Slice(0, newTrailingZeroBufferLength));

                            currentTrailingZeroBufferLength = newTrailingZeroBufferLength;
                            while (--currentTrailingZeroBufferLength >= 0 && trailingZeroBuffer[currentTrailingZeroBufferLength] == 0) ;
                            ++currentTrailingZeroBufferLength;

                            if (previousTrailingZeroBufferFromPool != null)
                                ArrayPool<nuint>.Shared.Return(previousTrailingZeroBufferFromPool);

                            Debug.Assert(currentTrailingZeroBufferLength >= 1);
                        }
                        trailingZeroE9 >>= 1;
                    }


                    // shrink buffer to the currently used portion.
                    // First, calculate the rough size of the buffer from the ratio that the number
                    // of digits follows. Then, shrink the size until there is no more space left.
                    // The Ratio is calculated as: log_{2^32}(10^9)
                    int currentBufferSize = Math.Min((int)(bufferSize * digitRatio) + 1, bufferSize);
                    Debug.Assert(buffer.Length == currentBufferSize || buffer.Slice(currentBufferSize).Trim(0u).Length == 0);
                    while (--currentBufferSize >= 0 && buffer[currentBufferSize] == 0) ;
                    ++currentBufferSize;
                    currentBuffer = buffer.Slice(0, currentBufferSize);

                    trailingZeroBuffer = trailingZeroBuffer.Slice(0, currentTrailingZeroBufferLength);
                    if (trailingZeroBuffer.Length <= 1)
                    {
                        Debug.Assert(trailingZeroBuffer.Length == 1);
                        nuint[]? resultBufferFromPool = null;
                        nuint trailingZero = trailingZeroBuffer[0];
                        if (trailingZero != 1)
                        {
                            int i = 0;
                            ulong carry = 0UL;

                            for (; i < currentBuffer.Length; i++)
                            {
                                ulong digits = (ulong)currentBuffer[i] * trailingZero + carry;
                                currentBuffer[i] = unchecked((uint)digits);
                                carry = digits >> 32;
                            }
                            if (carry != 0)
                            {
                                if (buffer.Length < ++currentBufferSize)
                                {
                                    Debug.Assert(buffer.Length + 1 == currentBufferSize);
                                    currentBuffer = (currentBufferSize <= BigIntegerCalculator.StackAllocThreshold
                                       ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                       : resultBufferFromPool = ArrayPool<nuint>.Shared.Rent(currentBufferSize)).Slice(0, currentBufferSize);
                                    buffer.CopyTo(currentBuffer);
                                }
                                else
                                {
                                    currentBuffer = buffer.Slice(0, currentBufferSize);
                                }
                                currentBuffer[i] = (nuint)carry;
                            }
                        }

                        result = NumberBufferToBigInteger(currentBuffer, number.IsNegative);
                        if (resultBufferFromPool != null)
                            ArrayPool<nuint>.Shared.Return(resultBufferFromPool);
                    }
                    else
                    {
                        int resultBufferLength = checked(currentBufferSize + trailingZeroBuffer.Length);
                        Span<nuint> resultBuffer = (resultBufferLength <= BigIntegerCalculator.StackAllocThreshold
                            ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                            : arrayFromPoolForTrailingZero = ArrayPool<nuint>.Shared.Rent(resultBufferLength)).Slice(0, resultBufferLength);
                        resultBuffer.Clear();

                        if (trailingZeroBuffer.Length < currentBuffer.Length)
                            BigIntegerCalculator.Multiply(currentBuffer, trailingZeroBuffer, resultBuffer);
                        else
                            BigIntegerCalculator.Multiply(trailingZeroBuffer, currentBuffer, resultBuffer);

                        while (--resultBufferLength >= 0 && resultBuffer[resultBufferLength] == 0) ;
                        ++resultBufferLength;

                        result = NumberBufferToBigInteger(resultBuffer.Slice(0, resultBufferLength), number.IsNegative);
                    }
                }
                finally
                {
                    if (arrayFromPoolForMultiplier != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForMultiplier);

                    if (arrayFromPoolForResultBuffer != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForResultBuffer);

                    if (arrayFromPoolForResultBuffer2 != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForResultBuffer2);

                    if (arrayFromPoolForTrailingZero != null)
                        ArrayPool<nuint>.Shared.Return(arrayFromPoolForTrailingZero);
                }
                return ParsingStatus.OK;
            }
        }
        static BigIntegerNative NumberBufferToBigInteger(ReadOnlySpan<nuint> currentBuffer, bool isNegative)
        {
            if (currentBuffer.Length == 0)
            {
                return new(0);
            }
            else if (currentBuffer.Length == 1 && currentBuffer[0] <= int.MaxValue)
            {
                int v = (int)currentBuffer[0];
                if (isNegative)
                    v = -v;
                return new(v, null);
            }
            else
            {
                return new(isNegative ? -1 : 1, currentBuffer.ToArray());
            }
        }


        internal static char ParseFormatSpecifier(ReadOnlySpan<char> format, out int digits)
        {
            digits = -1;
            if (format.Length == 0)
            {
                return 'R';
            }

            int i = 0;
            char ch = format[i];
            if (char.IsAsciiLetter(ch))
            {
                // The digits value must be >= 0 && <= 999_999_999,
                // but it can begin with any number of 0s, and thus we may need to check more than 9
                // digits.  Further, for compat, we need to stop when we hit a null char.
                i++;
                int n = 0;
                while ((uint)i < (uint)format.Length && char.IsAsciiDigit(format[i]))
                {
                    // Check if we are about to overflow past our limit of 9 digits
                    if (n >= 100_000_000)
                    {
                        throw new FormatException(SR.Argument_BadFormatSpecifier);
                    }
                    n = ((n * 10) + format[i++] - '0');
                }

                // If we're at the end of the digits rather than having stopped because we hit something
                // other than a digit or overflowed, return the standard format info.
                if (i >= format.Length || format[i] == '\0')
                {
                    digits = n;
                    return ch;
                }
            }
            return (char)0; // Custom format
        }

        private static string? FormatBigIntegerToHex(bool targetSpan, BigIntegerNative value, char format, int digits, NumberFormatInfo info, Span<char> destination, out int charsWritten, out bool spanSuccess)
        {
            Debug.Assert(format == 'x' || format == 'X');

            // Get the bytes that make up the BigInteger.
            byte[]? arrayToReturnToPool = null;
            Span<byte> bits = stackalloc byte[64]; // arbitrary threshold
            if (!value.TryWriteOrCountBytes(bits, out int bytesWrittenOrNeeded))
            {
                bits = arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(bytesWrittenOrNeeded);
                bool success = value.TryWriteBytes(bits, out bytesWrittenOrNeeded);
                Debug.Assert(success);
            }
            bits = bits.Slice(0, bytesWrittenOrNeeded);

            var sb = new ValueStringBuilder(stackalloc char[128]); // each byte is typically two chars

            int cur = bits.Length - 1;
            if (cur > -1)
            {
                // [FF..F8] drop the high F as the two's complement negative number remains clear
                // [F7..08] retain the high bits as the two's complement number is wrong without it
                // [07..00] drop the high 0 as the two's complement positive number remains clear
                bool clearHighF = false;
                byte head = bits[cur];

                if (head > 0xF7)
                {
                    head -= 0xF0;
                    clearHighF = true;
                }

                if (head < 0x08 || clearHighF)
                {
                    // {0xF8-0xFF} print as {8-F}
                    // {0x00-0x07} print as {0-7}
                    sb.Append(head < 10 ?
                        (char)(head + '0') :
                        format == 'X' ? (char)((head & 0xF) - 10 + 'A') : (char)((head & 0xF) - 10 + 'a'));
                    cur--;
                }
            }

            if (cur > -1)
            {
                Span<char> chars = sb.AppendSpan((cur + 1) * 2);
                int charsPos = 0;
                string hexValues = format == 'x' ? "0123456789abcdef" : "0123456789ABCDEF";
                while (cur > -1)
                {
                    byte b = bits[cur--];
                    chars[charsPos++] = hexValues[b >> 4];
                    chars[charsPos++] = hexValues[b & 0xF];
                }
            }

            if (digits > sb.Length)
            {
                // Insert leading zeros, e.g. user specified "X5" so we create "0ABCD" instead of "ABCD"
                sb.Insert(
                    0,
                    value._sign >= 0 ? '0' : (format == 'x') ? 'f' : 'F',
                    digits - sb.Length);
            }

            if (arrayToReturnToPool != null)
            {
                ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
            }

            if (targetSpan)
            {
                spanSuccess = sb.TryCopyTo(destination, out charsWritten);
                return null;
            }
            else
            {
                charsWritten = 0;
                spanSuccess = false;
                return sb.ToString();
            }
        }

        private static string? FormatBigIntegerToBinary(bool targetSpan, BigIntegerNative value, int digits, Span<char> destination, out int charsWritten, out bool spanSuccess)
        {
            // Get the bytes that make up the BigInteger.
            byte[]? arrayToReturnToPool = null;
            Span<byte> bytes = stackalloc byte[64]; // arbitrary threshold
            if (!value.TryWriteOrCountBytes(bytes, out int bytesWrittenOrNeeded))
            {
                bytes = arrayToReturnToPool = ArrayPool<byte>.Shared.Rent(bytesWrittenOrNeeded);
                bool success = value.TryWriteBytes(bytes, out _);
                Debug.Assert(success);
            }
            bytes = bytes.Slice(0, bytesWrittenOrNeeded);

            Debug.Assert(!bytes.IsEmpty);

            byte highByte = bytes[^1];

            int charsInHighByte = 9 - byte.LeadingZeroCount(value._sign >= 0 ? highByte : (byte)~highByte);
            long tmpCharCount = charsInHighByte + ((long)(bytes.Length - 1) << 3);

            if (tmpCharCount > Array.MaxLength)
            {
                Debug.Assert(arrayToReturnToPool is not null);
                ArrayPool<byte>.Shared.Return(arrayToReturnToPool);

                throw new FormatException(SR.Format_TooLarge);
            }

            int charsForBits = (int)tmpCharCount;

            Debug.Assert(digits < Array.MaxLength);
            int charsIncludeDigits = Math.Max(digits, charsForBits);

            try
            {
                scoped ValueStringBuilder sb;
                if (targetSpan)
                {
                    if (charsIncludeDigits > destination.Length)
                    {
                        charsWritten = 0;
                        spanSuccess = false;
                        return null;
                    }

                    // Because we have ensured destination can take actual char length, so now just use ValueStringBuilder as wrapper so that subsequent logic can be reused by 2 flows (targetSpan and non-targetSpan);
                    // meanwhile there is no need to copy to destination again after format data for targetSpan flow.
                    sb = new ValueStringBuilder(destination);
                }
                else
                {
                    // each byte is typically eight chars
                    sb = charsIncludeDigits > 512
                        ? new ValueStringBuilder(charsIncludeDigits)
                        : new ValueStringBuilder(stackalloc char[512]);
                }

                if (digits > charsForBits)
                {
                    sb.Append(value._sign >= 0 ? '0' : '1', digits - charsForBits);
                }

                AppendByte(ref sb, highByte, charsInHighByte - 1);

                for (int i = bytes.Length - 2; i >= 0; i--)
                {
                    AppendByte(ref sb, bytes[i]);
                }

                Debug.Assert(sb.Length == charsIncludeDigits);

                if (targetSpan)
                {
                    charsWritten = charsIncludeDigits;
                    spanSuccess = true;
                    return null;
                }

                charsWritten = 0;
                spanSuccess = false;
                return sb.ToString();
            }
            finally
            {
                if (arrayToReturnToPool is not null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }

            static void AppendByte(ref ValueStringBuilder sb, byte b, int startHighBit = 7)
            {
                for (int i = startHighBit; i >= 0; i--)
                {
                    sb.Append((char)('0' + ((b >> i) & 0x1)));
                }
            }
        }

        //
        // This threshold is for choosing the algorithm to use based on the number of digits.
        //
        // Let N be the number of digits. If N is less than or equal to the bound, use a naive
        // algorithm with a running time of O(N^2). And if it is greater than the threshold, use
        // a divide-and-conquer algorithm with a running time of O(NlogN+N^log3).
        //
#if DEBUG
        // Mutable for unit testing...
        internal static
#else
        internal const
#endif
        int ToStringNaiveThreshold = BigIntegerCalculator.DivideBurnikelZieglerThreshold;

        static void BigIntegerToDigits(ReadOnlySpan<nuint> bits, Span<char> destination, out int backCharsWritten)
        {
            if (Environment.Is64BitProcess)
                BigIntegerToDigits(MemoryMarshal.Cast<nuint, ulong>(bits), destination, out backCharsWritten);
            else
                BigIntegerToDigits(MemoryMarshal.Cast<nuint, uint>(bits), destination, out backCharsWritten);
        }

        static void BigIntegerToDigits(ReadOnlySpan<uint> bits, Span<char> destination, out int backCharsWritten)
        {
            Debug.Assert(destination.Trim('0').Length == 0);
            Debug.Assert(ToStringNaiveThreshold >= 2);

            if (bits.Length <= ToStringNaiveThreshold)
            {
                Naive(MemoryMarshal.Cast<uint, nuint>(bits), destination, out backCharsWritten);
                return;
            }

            uint roundUp = BitOperations.RoundUpToPowerOf2((uint)bits.Length);
            int powerOfTensLength = (int)(roundUp - 2);
            nuint[]? powerOfTensFromPool = null;
            Span<nuint> powerOfTens = (powerOfTensLength <= 512
                ? stackalloc nuint[512]
                : (powerOfTensFromPool = ArrayPool<nuint>.Shared.Rent(powerOfTensLength))).Slice(0, powerOfTensLength);


            int indexesLength = BitOperations.TrailingZeroCount(roundUp);
            Span<int> indexes = stackalloc int[indexesLength];

            {
                // Build powerOfTens
                Debug.Assert(indexes.Length >= 2);

                // powerOfTens[0..2] = 1E18
                powerOfTens[0] = 2808348672;
                powerOfTens[1] = 232830643;
                powerOfTens.Slice(2).Clear();

                indexes[0] = 0;
                indexes[1] = 2;

                for (int i = 1; i + 1 < indexes.Length; i++)
                {
                    Span<nuint> prev = powerOfTens[indexes[i - 1]..indexes[i]];
                    Span<nuint> next = powerOfTens.Slice(indexes[i], prev.Length << 1);
                    BigIntegerCalculator.Square(prev, next);

                    int ni = next.Length;
                    while (next[--ni] == 0) ;
                    indexes[i + 1] = indexes[i] + ni + 1;
                    Debug.Assert(powerOfTens[indexes[i + 1] - 1] != 0);
                }
            }

            DivideAndConquer(powerOfTens, indexes, MemoryMarshal.Cast<uint, nuint>(bits), destination, out backCharsWritten);

            const int kcchBase = 9;
            const uint kuBase = 1000000000;

            static void DivideAndConquer(ReadOnlySpan<nuint> powerOfTens, ReadOnlySpan<int> indexes, ReadOnlySpan<nuint> bits, Span<char> destination, out int backCharsWritten)
            {
                Debug.Assert(bits.Length == 0 || bits[^1] != 0);

                if (bits.Length <= ToStringNaiveThreshold)
                {
                    Naive(bits, destination, out backCharsWritten);
                    return;
                }

                Debug.Assert(indexes.Length >= 2);

                ReadOnlySpan<nuint> powOfTen = powerOfTens[indexes[^2]..indexes[^1]];
                while (bits.Length < powOfTen.Length || (bits.Length == powOfTen.Length && bits[^1] < powOfTen[^1]))
                {
                    indexes = indexes.Slice(0, indexes.Length - 1);
                    powOfTen = powerOfTens[indexes[^2]..indexes[^1]];
                }

                int upperLength = bits.Length - powOfTen.Length + 1;
                nuint[]? upperFromPool = null;
                Span<nuint> upper = ((uint)upperLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                    : upperFromPool = ArrayPool<nuint>.Shared.Rent(upperLength)).Slice(0, upperLength);

                int lowerLength = bits.Length;
                nuint[]? lowerFromPool = null;
                Span<nuint> lower = ((uint)lowerLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                    : lowerFromPool = ArrayPool<nuint>.Shared.Rent(lowerLength)).Slice(0, lowerLength);

                BigIntegerCalculator.Divide(bits, powOfTen, upper, lower);

                if (upper.Length == 1 && upper[0] == 0)
                {
                    indexes = indexes.Slice(0, indexes.Length - 1);
                    powOfTen = powerOfTens[indexes[^2]..indexes[^1]];

                    if (upperFromPool != null)
                        ArrayPool<nuint>.Shared.Return(upperFromPool);

                    upperFromPool = null;
                    upperLength = bits.Length - powOfTen.Length + 1;
                    upper = ((uint)upperLength <= BigIntegerCalculator.StackAllocThreshold
                        ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                        : upperFromPool = ArrayPool<nuint>.Shared.Rent(upperLength)).Slice(0, upperLength);
                    BigIntegerCalculator.Divide(bits, powOfTen, upper, lower);
                }

                int lowerStrLength = kcchBase * (1 << (indexes.Length - 1));
                DivideAndConquer(powerOfTens, indexes, upper.TrimEnd(0u), destination.Slice(0, destination.Length - lowerStrLength), out backCharsWritten);
                if (upperFromPool != null)
                    ArrayPool<nuint>.Shared.Return(upperFromPool);
                backCharsWritten += lowerStrLength;

                DivideAndConquer(powerOfTens, indexes.Slice(0, indexes.Length - 1), lower.TrimEnd(0u), destination, out _);
                if (lowerFromPool != null)
                    ArrayPool<nuint>.Shared.Return(lowerFromPool);
            }

            static void Naive(ReadOnlySpan<nuint> bits, Span<char> destination, out int backCharsWritten)
            {
                int cuSrc = bits.Length;
                int cuMax = checked(cuSrc * 10 / 9 + 2);
                Debug.Assert(cuMax <= 256);
                Span<uint> rguDst = stackalloc uint[cuMax];
                rguDst.Clear();
                int cuDst = 0;

                for (int iuSrc = cuSrc; --iuSrc >= 0;)
                {
                    uint uCarry = (uint)bits[iuSrc];
                    for (int iuDst = 0; iuDst < cuDst; iuDst++)
                    {
                        Debug.Assert(rguDst[iuDst] < kuBase);
                        ulong uuRes = NumericsHelpers.MakeUInt64(rguDst[iuDst], uCarry);
                        rguDst[iuDst] = (uint)(uuRes % kuBase);
                        uCarry = (uint)(uuRes / kuBase);
                    }
                    if (uCarry != 0)
                    {
                        rguDst[cuDst++] = uCarry % kuBase;
                        uCarry /= kuBase;
                        if (uCarry != 0)
                            rguDst[cuDst++] = uCarry;
                    }
                }

                if (cuDst == 0)
                {
                    backCharsWritten = 0;
                    return;
                }

                int ichDst = destination.Length;
                for (int iuDst = 0; iuDst < cuDst - 1; iuDst++)
                {
                    uint uDig = rguDst[iuDst];
                    Debug.Assert(uDig < kuBase);
                    for (int cch = kcchBase; --cch >= 0;)
                    {
                        destination[--ichDst] = (char)('0' + uDig % 10);
                        uDig /= 10;
                    }
                }
                for (uint uDig = rguDst[cuDst - 1]; uDig != 0;)
                {
                    destination[--ichDst] = (char)('0' + uDig % 10);
                    uDig /= 10;
                }

                backCharsWritten = destination.Length - ichDst;
            }
        }
        static void BigIntegerToDigits(ReadOnlySpan<ulong> bits, Span<char> destination, out int backCharsWritten)
        {
            Debug.Assert(destination.Trim('0').Length == 0);
            Debug.Assert(ToStringNaiveThreshold >= 2);

            if (bits.Length <= ToStringNaiveThreshold)
            {
                Naive(MemoryMarshal.Cast<ulong, nuint>(bits), destination, out backCharsWritten);
                return;
            }

            uint roundUp = BitOperations.RoundUpToPowerOf2((uint)bits.Length);
            int powerOfTensLength = (int)(roundUp - 2);
            nuint[]? powerOfTensFromPool = null;
            Span<nuint> powerOfTens = (powerOfTensLength <= 512
                ? stackalloc nuint[512]
                : (powerOfTensFromPool = ArrayPool<nuint>.Shared.Rent(powerOfTensLength))).Slice(0, powerOfTensLength);


            int indexesLength = BitOperations.TrailingZeroCount(roundUp);
            Span<int> indexes = stackalloc int[indexesLength];

            {
                // Build powerOfTens
                Debug.Assert(indexes.Length >= 2);

                // powerOfTens[0..2] = 1E38
                powerOfTens[0] = unchecked((nuint)687399551400673280);
                powerOfTens[1] = unchecked((nuint)5421010862427522170);
                powerOfTens.Slice(2).Clear();

                indexes[0] = 0;
                indexes[1] = 2;

                for (int i = 1; i + 1 < indexes.Length; i++)
                {
                    Span<nuint> prev = powerOfTens[indexes[i - 1]..indexes[i]];
                    Span<nuint> next = powerOfTens.Slice(indexes[i], prev.Length << 1);
                    BigIntegerCalculator.Square(prev, next);

                    int ni = next.Length;
                    while (next[--ni] == 0) ;
                    indexes[i + 1] = indexes[i] + ni + 1;
                    Debug.Assert(powerOfTens[indexes[i + 1] - 1] != 0);
                }
            }

            DivideAndConquer(powerOfTens, indexes, MemoryMarshal.Cast<ulong, nuint>(bits), destination, out backCharsWritten);

            const int kcchBase = 19;
            const ulong kuBase = 1000000000_000000000_0;

            static void DivideAndConquer(ReadOnlySpan<nuint> powerOfTens, ReadOnlySpan<int> indexes, ReadOnlySpan<nuint> bits, Span<char> destination, out int backCharsWritten)
            {
                Debug.Assert(bits.Length == 0 || bits[^1] != 0);

                if (bits.Length <= ToStringNaiveThreshold)
                {
                    Naive(bits, destination, out int charsWritternUInt64);
                    backCharsWritten = charsWritternUInt64;
                    return;
                }

                Debug.Assert(indexes.Length >= 2);

                ReadOnlySpan<nuint> powOfTen = powerOfTens[indexes[^2]..indexes[^1]];
                while (bits.Length < powOfTen.Length || (bits.Length == powOfTen.Length && bits[^1] < powOfTen[^1]))
                {
                    indexes = indexes.Slice(0, indexes.Length - 1);
                    powOfTen = powerOfTens[indexes[^2]..indexes[^1]];
                }

                int upperLength = bits.Length - powOfTen.Length + 1;
                nuint[]? upperFromPool = null;
                Span<nuint> upper = ((uint)upperLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                    : upperFromPool = ArrayPool<nuint>.Shared.Rent(upperLength)).Slice(0, upperLength);

                int lowerLength = bits.Length;
                nuint[]? lowerFromPool = null;
                Span<nuint> lower = ((uint)lowerLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                    : lowerFromPool = ArrayPool<nuint>.Shared.Rent(lowerLength)).Slice(0, lowerLength);

                BigIntegerCalculator.Divide(bits, powOfTen, upper, lower);

                if (upper.Length == 1 && upper[0] == 0)
                {
                    indexes = indexes.Slice(0, indexes.Length - 1);
                    powOfTen = powerOfTens[indexes[^2]..indexes[^1]];

                    if (upperFromPool != null)
                        ArrayPool<nuint>.Shared.Return(upperFromPool);

                    upperFromPool = null;
                    upperLength = bits.Length - powOfTen.Length + 1;
                    upper = ((uint)upperLength <= BigIntegerCalculator.StackAllocThreshold
                        ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                        : upperFromPool = ArrayPool<nuint>.Shared.Rent(upperLength)).Slice(0, upperLength);
                    BigIntegerCalculator.Divide(bits, powOfTen, upper, lower);
                }

                int lowerStrLength = kcchBase * (1 << (indexes.Length - 1));
                DivideAndConquer(powerOfTens, indexes, upper.TrimEnd(0u), destination.Slice(0, destination.Length - lowerStrLength), out backCharsWritten);
                if (upperFromPool != null)
                    ArrayPool<nuint>.Shared.Return(upperFromPool);
                backCharsWritten += lowerStrLength;

                DivideAndConquer(powerOfTens, indexes.Slice(0, indexes.Length - 1), lower.TrimEnd(0u), destination, out _);
                if (lowerFromPool != null)
                    ArrayPool<nuint>.Shared.Return(lowerFromPool);
            }

            static void Naive(ReadOnlySpan<nuint> bits, Span<char> destination, out int backCharsWritten)
            {
                int cuSrc = bits.Length;
                int cuMax = checked(cuSrc * 10 / 9 + 2);
                Debug.Assert(cuMax <= 256);
                Span<ulong> rguDst = stackalloc ulong[cuMax];
                rguDst.Clear();
                int cuDst = 0;

                for (int iuSrc = cuSrc; --iuSrc >= 0;)
                {
                    ulong uCarry = bits[iuSrc];
                    for (int iuDst = 0; iuDst < cuDst; iuDst++)
                    {
                        Debug.Assert(rguDst[iuDst] < kuBase);
                        uCarry = BigIntegerCalculator.DivRem64(rguDst[iuDst], uCarry, kuBase, out rguDst[iuDst]);
                    }
                    if (uCarry != 0)
                    {
                        (uCarry, rguDst[cuDst++]) = Math.DivRem(uCarry, kuBase);
                        if (uCarry != 0)
                            rguDst[cuDst++] = uCarry;
                    }
                }

                if (cuDst == 0)
                {
                    backCharsWritten = 0;
                    return;
                }

                int ichDst = destination.Length;
                for (int iuDst = 0; iuDst < cuDst - 1; iuDst++)
                {
                    ulong uDig = rguDst[iuDst];
                    Debug.Assert(uDig < kuBase);
                    for (int cch = kcchBase; --cch >= 0;)
                    {
                        destination[--ichDst] = (char)('0' + uDig % 10);
                        uDig /= 10;
                    }
                }
                for (ulong uDig = rguDst[cuDst - 1]; uDig != 0;)
                {
                    destination[--ichDst] = (char)('0' + uDig % 10);
                    uDig /= 10;
                }

                backCharsWritten = destination.Length - ichDst;
            }
        }


        internal static string FormatBigInteger(BigIntegerNative value, string? format, NumberFormatInfo info)
        {
            return FormatBigInteger(targetSpan: false, value, format, format, info, default, out _, out _)!;
        }

        internal static bool TryFormatBigInteger(BigIntegerNative value, ReadOnlySpan<char> format, NumberFormatInfo info, Span<char> destination, out int charsWritten)
        {
            FormatBigInteger(targetSpan: true, value, null, format, info, destination, out charsWritten, out bool spanSuccess);
            return spanSuccess;
        }

        private static string? FormatBigInteger(
            bool targetSpan, BigIntegerNative value,
            string? formatString, ReadOnlySpan<char> formatSpan,
            NumberFormatInfo info, Span<char> destination, out int charsWritten, out bool spanSuccess)
        {
            Debug.Assert(formatString == null || formatString.Length == formatSpan.Length);

            char fmt = ParseFormatSpecifier(formatSpan, out int digits);

            if ((char)(fmt | 0x20) == 'x')
            {
                return FormatBigIntegerToHex(targetSpan, value, fmt, digits, info, destination, out charsWritten, out spanSuccess);
            }
            if ((char)(fmt | 0x20) == 'b')
            {
                return FormatBigIntegerToBinary(targetSpan, value, digits, destination, out charsWritten, out spanSuccess);
            }

            if (value._bits == null)
            {
                if ((char)(fmt | 0x20) is 'g' or 'r')
                {
                    formatSpan = formatString = digits > 0 ? $"D{digits}" : "D";
                }

                if (targetSpan)
                {
                    spanSuccess = value._sign.TryFormat(destination, out charsWritten, formatSpan, info);
                    return null;
                }
                else
                {
                    charsWritten = 0;
                    spanSuccess = false;
                    return value._sign.ToString(formatString, info);
                }
            }

            bool decimalFmt = ((char)(fmt | 0x20) is 'g' or 'r' or 'd');
            bool isNegative = value._sign < 0;

            int digitsStrLength;
            try
            {
                checked
                {
                    // The Ratio is calculated as: log10{2^32}
                    double digitRatio = Environment.Is64BitProcess ? 19.2659197224948 : 9.632959861247398;

                    digitsStrLength = (int)(digitRatio * value._bits.Length) + 2;
                    if (decimalFmt)
                    {
                        if (digitsStrLength < digits)
                            digitsStrLength = digits;
                        if (isNegative)
                            digitsStrLength += info.NegativeSign.Length;
                    }
                    ++digitsStrLength;
                }
            }
            catch (OverflowException e)
            {
                throw new FormatException(SR.Format_TooLarge, e);
            }

            char[]? digitsStrFromPool = null;

            try
            {
                Span<char> digitsStr = (digitsStrLength <= 512
                    ? stackalloc char[512]
                    : (digitsStrFromPool = ArrayPool<char>.Shared.Rent(digitsStrLength)));
                digitsStr[digitsStrLength - 1] = '\0'; // for FormatProvider.FormatBigInteger
                digitsStr = digitsStr.Slice(0, digitsStrLength - 1);
                digitsStr.Fill('0');

                BigIntegerToDigits(value._bits, digitsStr, out int backCharsWritten);

                if (!decimalFmt)
                {
                    throw new FormatException();
                }

                if (backCharsWritten < digits)
                {
                    digitsStr.Slice(digitsStr.Length - digits, digits - backCharsWritten).Fill('0');
                    backCharsWritten = digits;
                }

                if (isNegative)
                {
                    info.NegativeSign.CopyTo(digitsStr.Slice(digitsStr.Length - backCharsWritten - info.NegativeSign.Length, info.NegativeSign.Length));
                    backCharsWritten += info.NegativeSign.Length;
                }

                ReadOnlySpan<char> resultSpan = digitsStr.Slice(digitsStr.Length - backCharsWritten);

                if (!targetSpan)
                {
                    charsWritten = 0;
                    spanSuccess = false;
                    return new string(resultSpan);
                }
                else if (resultSpan.TryCopyTo(destination))
                {
                    charsWritten = resultSpan.Length;
                    spanSuccess = true;
                    return null;
                }
                else
                {
                    charsWritten = 0;
                    spanSuccess = false;
                    return null;
                }
            }
            finally
            {
                if (digitsStrFromPool != null)
                    ArrayPool<char>.Shared.Return(digitsStrFromPool);
            }
        }

        private static bool TryStringToNumber(ReadOnlySpan<char> value, ref NumberBuffer number)
        {
            if (value.Length == 0) return false;
            if (value[0] == '+')
            {
                value = value[1..];
            }
            else if (value[0] == '-')
            {
                number.IsNegative = true;
                value = value[1..];
            }

            if (value.Length == 0)
                return false;

            number.Scale = value.Length;
            {
                int iv = value.Length;
                while (--iv >= 0 && value[iv] == '0') ;
                if (iv < 0)
                {
                    number.Digits[0] = 0;
                    return true;
                }
                else if (iv + 1 < value.Length)
                {
                    number.HasNonZeroTail = true;
                    value = value[..(iv + 1)];
                }
            }

            if (number.Digits.Length < value.Length + 1)
                return false;

            number.DigitsCount = value.Length;
            for (int i = 0; i < value.Length; i++)
            {
                number.Digits[i] = (byte)value[i];
            }
            number.Digits[value.Length] = 0;
            return true;
        }

        private static bool IsWhite(uint ch) => (ch == 0x20) || ((ch - 0x09) <= (0x0D - 0x09));

        private static bool IsDigit(uint ch) => (ch - '0') <= 9;

        internal enum ParsingStatus
        {
            OK,
            Failed,
            Overflow
        }
    }
}

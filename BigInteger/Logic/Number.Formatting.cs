// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Kzrnm.Numerics.Logic
{
    using static SR;
    static partial class Number
    {
        private const int CharStackBufferSize = 32;

        private const int DefaultPrecisionExponentialFormat = 6;

        private const int MaxUInt32DecDigits = 10;
        private const string PosNumberFormat = "#";

        private static readonly string[] s_posCurrencyFormats =
        new[]{
            "$#", "#$", "$ #", "# $"
        };

        private static readonly string[] s_negCurrencyFormats =
        new[]{
            "($#)", "-$#", "$-#", "$#-",
            "(#$)", "-#$", "#-$", "#$-",
            "-# $", "-$ #", "# $-", "$ #-",
            "$ -#", "#- $", "($ #)", "(# $)",
            "$- #"
        };

        private static readonly string[] s_posPercentFormats =
        new[]{
            "# %", "#%", "%#", "% #"
        };

        private static readonly string[] s_negPercentFormats =
        new[]{
            "-# %", "-#%", "-%#",
            "%-#", "%#-",
            "#-%", "#%-",
            "-% #", "# %-", "% #-",
            "% -#", "#- %"
        };

        private static readonly string[] s_negNumberFormats =
        new[]{
            "(#)", "-#", "- #", "#-", "# -",
        };

        internal static char ParseFormatSpecifier(ReadOnlySpan<char> format, out int digits)
        {
            char c = default;
            if (format.Length > 0)
            {
                // If the format begins with a symbol, see if it's a standard format
                // with or without a specified number of digits.
                c = format[0];
                if ((uint)((c | 0x20) - 'a') < 26) // char.IsAsciiLetter(c)
                {
                    // Fast path for sole symbol, e.g. "D"
                    if (format.Length == 1)
                    {
                        digits = -1;
                        return c;
                    }

                    if (format.Length == 2)
                    {
                        // Fast path for symbol and single digit, e.g. "X4"
                        int d = format[1] - '0';
                        if ((uint)d < 10)
                        {
                            digits = d;
                            return c;
                        }
                    }
                    else if (format.Length == 3)
                    {
                        // Fast path for symbol and double digit, e.g. "F12"
                        int d1 = format[1] - '0', d2 = format[2] - '0';
                        if ((uint)d1 < 10 && (uint)d2 < 10)
                        {
                            digits = d1 * 10 + d2;
                            return c;
                        }
                    }

                    // Fallback for symbol and any length digits.  The digits value must be >= 0 && <= 999_999_999,
                    // but it can begin with any number of 0s, and thus we may need to check more than 9
                    // digits.  Further, for compat, we need to stop when we hit a null char.
                    int n = 0;
                    int i = 1;
                    while ((uint)i < (uint)format.Length
                        && (uint)(format[i] - '0') < 10) // char.IsAsciiDigit(format[i])
                    {
                        // Check if we are about to overflow past our limit of 9 digits
                        if (n >= 100_000_000)
                        {
                            ThrowHelper.ThrowFormatException_BadFormatSpecifier();
                        }
                        n = (n * 10) + format[i++] - '0';
                    }

                    // If we're at the end of the digits rather than having stopped because we hit something
                    // other than a digit or overflowed, return the standard format info.
                    if ((uint)i >= (uint)format.Length || format[i] == '\0')
                    {
                        digits = n;
                        return c;
                    }
                }
            }

            // Default empty format to be "G"; custom format is signified with '\0'.
            digits = -1;
            return format.Length == 0 || c == '\0' ? // For compat, treat '\0' as the end of the specifier, even if the specifier extends beyond it.
                'G' :
                '\0';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int UInt32ToDecChars<T>(Span<T> buffer, uint value, int digits)
        {
            // TODO: Consider to bring optimized implementation from CoreLib
            int i = buffer.Length;
            while (value != 0 || digits > 0)
            {
                digits--;
                var quo = value / 10;
                var rem = value - quo * 10;
                value = quo;
                buffer[--i] = CastFrom<T>(rem + '0');
            }

            // return remaining length
            return i;
        }

        internal static void NumberToString<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, char format, int nMaxDigits, NumberFormatInfo info)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            number.CheckConsistency();
            bool isCorrectlyRounded = (number.Kind == NumberBufferKind.FloatingPoint);

            switch (format)
            {
                case 'C':
                case 'c':
                    {
                        if (nMaxDigits < 0)
                        {
                            nMaxDigits = info.CurrencyDecimalDigits;
                        }

                        RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded); // Don't change this line to use digPos since digCount could have its sign changed.

                        FormatCurrency(ref vlb, ref number, nMaxDigits, info);

                        break;
                    }

                case 'F':
                case 'f':
                    {
                        if (nMaxDigits < 0)
                        {
                            nMaxDigits = info.NumberDecimalDigits;
                        }

                        RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);

                        if (number.IsNegative)
                        {
                            vlb.Append(info.NegativeSignTChar<T>());
                        }

                        FormatFixed(ref vlb, ref number, nMaxDigits, null, info.NumberDecimalSeparatorTChar<T>(), null);

                        break;
                    }

                case 'N':
                case 'n':
                    {
                        if (nMaxDigits < 0)
                        {
                            nMaxDigits = info.NumberDecimalDigits; // Since we are using digits in our calculation
                        }

                        RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);

                        FormatNumber(ref vlb, ref number, nMaxDigits, info);

                        break;
                    }

                case 'E':
                case 'e':
                    {
                        if (nMaxDigits < 0)
                        {
                            nMaxDigits = DefaultPrecisionExponentialFormat;
                        }
                        nMaxDigits++;

                        RoundNumber(ref number, nMaxDigits, isCorrectlyRounded);

                        if (number.IsNegative)
                        {
                            vlb.Append(info.NegativeSignTChar<T>());
                        }

                        FormatScientific(ref vlb, ref number, nMaxDigits, info, format);

                        break;
                    }

                case 'G':
                case 'g':
                    {
                        bool noRounding = false;
                        if (nMaxDigits < 1)
                        {
                            if ((number.Kind == NumberBufferKind.Decimal) && (nMaxDigits == -1))
                            {
                                noRounding = true;  // Turn off rounding for ECMA compliance to output trailing 0's after decimal as significant

                                if (number.Digits[0] == 0)
                                {
                                    // -0 should be formatted as 0 for decimal. This is normally handled by RoundNumber (which we are skipping)
                                    goto SkipSign;
                                }

                                goto SkipRounding;
                            }
                            else
                            {
                                // This ensures that the PAL code pads out to the correct place even when we use the default precision
                                nMaxDigits = number.DigitsCount;
                            }
                        }

                        RoundNumber(ref number, nMaxDigits, isCorrectlyRounded);

                    SkipRounding:
                        if (number.IsNegative)
                        {
                            vlb.Append(info.NegativeSignTChar<T>());
                        }

                    SkipSign:
                        FormatGeneral(ref vlb, ref number, nMaxDigits, info, (char)(format - ('G' - 'E')), noRounding);

                        break;
                    }

                case 'P':
                case 'p':
                    {
                        if (nMaxDigits < 0)
                        {
                            nMaxDigits = info.PercentDecimalDigits;
                        }
                        number.Scale += 2;

                        RoundNumber(ref number, number.Scale + nMaxDigits, isCorrectlyRounded);

                        FormatPercent(ref vlb, ref number, nMaxDigits, info);

                        break;
                    }

                case 'R':
                case 'r':
                    {
                        format = (char)(format - ('R' - 'G'));
                        Debug.Assert(format is 'G' or 'g');
                        goto case 'G';
                    }

                default:
                    ThrowHelper.ThrowFormatException_BadFormatSpecifier();
                    break;
            }
        }

        internal static void NumberToStringFormat<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, ReadOnlySpan<char> format, NumberFormatInfo info)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            number.CheckConsistency();

            int digitCount;
            int decimalPos;
            int firstDigit;
            int lastDigit;
            int digPos;
            bool scientific;
            int thousandPos;
            int thousandCount = 0;
            bool thousandSeps;
            int scaleAdjust;
            int adjust;

            int section;
            int src;
            ref var dig = ref MemoryMarshal.GetReference(number.Digits);
            char ch;

            section = FindSection(format, dig == 0 ? 2 : number.IsNegative ? 1 : 0);

            while (true)
            {
                digitCount = 0;
                decimalPos = -1;
                firstDigit = 0x7FFFFFFF;
                lastDigit = 0;
                scientific = false;
                thousandPos = -1;
                thousandSeps = false;
                scaleAdjust = 0;
                src = section;

                while (src < format.Length && (ch = format[src++]) != 0 && ch != ';')
                {
                    switch (ch)
                    {
                        case '#':
                            digitCount++;
                            break;

                        case '0':
                            if (firstDigit == 0x7FFFFFFF)
                            {
                                firstDigit = digitCount;
                            }
                            digitCount++;
                            lastDigit = digitCount;
                            break;

                        case '.':
                            if (decimalPos < 0)
                            {
                                decimalPos = digitCount;
                            }
                            break;

                        case ',':
                            if (digitCount > 0 && decimalPos < 0)
                            {
                                if (thousandPos >= 0)
                                {
                                    if (thousandPos == digitCount)
                                    {
                                        thousandCount++;
                                        break;
                                    }
                                    thousandSeps = true;
                                }
                                thousandPos = digitCount;
                                thousandCount = 1;
                            }
                            break;

                        case '%':
                            scaleAdjust += 2;
                            break;

                        case '\x2030':
                            scaleAdjust += 3;
                            break;

                        case '\'':
                        case '"':
                            while (src < format.Length && format[src] != 0 && format[src++] != ch) ;
                            break;

                        case '\\':
                            if (src < format.Length && format[src] != 0)
                            {
                                src++;
                            }
                            break;

                        case 'E':
                        case 'e':
                            if ((src < format.Length && format[src] == '0') ||
                                (src + 1 < format.Length && (format[src] == '+' || format[src] == '-') && format[src + 1] == '0'))
                            {
                                while (++src < format.Length && format[src] == '0') ;
                                scientific = true;
                            }
                            break;
                    }
                }

                if (decimalPos < 0)
                {
                    decimalPos = digitCount;
                }

                if (thousandPos >= 0)
                {
                    if (thousandPos == decimalPos)
                    {
                        scaleAdjust -= thousandCount * 3;
                    }
                    else
                    {
                        thousandSeps = true;
                    }
                }

                if (dig != 0)
                {
                    number.Scale += scaleAdjust;
                    int pos = scientific ? digitCount : number.Scale + digitCount - decimalPos;
                    RoundNumber(ref number, pos, isCorrectlyRounded: false);
                    if (dig == 0)
                    {
                        src = FindSection(format, 2);
                        if (src != section)
                        {
                            section = src;
                            continue;
                        }
                    }
                }
                else
                {
                    if (number.Kind != NumberBufferKind.FloatingPoint)
                    {
                        // The integer types don't have a concept of -0 and decimal always format -0 as 0
                        number.IsNegative = false;
                    }
                    number.Scale = 0;      // Decimals with scale ('0.00') should be rounded.
                }

                break;
            }

            firstDigit = firstDigit < decimalPos ? decimalPos - firstDigit : 0;
            lastDigit = lastDigit > decimalPos ? decimalPos - lastDigit : 0;
            if (scientific)
            {
                digPos = decimalPos;
                adjust = 0;
            }
            else
            {
                digPos = number.Scale > decimalPos ? number.Scale : decimalPos;
                adjust = number.Scale - decimalPos;
            }
            src = section;

            // Adjust can be negative, so we make this an int instead of an unsigned int.
            // Adjust represents the number of characters over the formatting e.g. format string is "0000" and you are trying to
            // format 100000 (6 digits). Means adjust will be 2. On the other hand if you are trying to format 10 adjust will be
            // -2 and we'll need to fixup these digits with 0 padding if we have 0 formatting as in this example.
            Span<int> thousandsSepPos = stackalloc int[4];
            int thousandsSepCtr = -1;

            if (thousandSeps)
            {
                // We need to precompute this outside the number formatting loop
                if (info.NumberGroupSeparator.Length > 0)
                {
                    // We need this array to figure out where to insert the thousands separator. We would have to traverse the string
                    // backwards. PIC formatting always traverses forwards. These indices are precomputed to tell us where to insert
                    // the thousands separator so we can get away with traversing forwards. Note we only have to compute up to digPos.
                    // The max is not bound since you can have formatting strings of the form "000,000..", and this
                    // should handle that case too.

                    int[] groupDigits = info.NumberGroupSizes();

                    int groupSizeIndex = 0;     // Index into the groupDigits array.
                    int groupTotalSizeCount = 0;
                    int groupSizeLen = groupDigits.Length;    // The length of groupDigits array.
                    if (groupSizeLen != 0)
                    {
                        groupTotalSizeCount = groupDigits[groupSizeIndex];   // The current running total of group size.
                    }
                    int groupSize = groupTotalSizeCount;

                    int totalDigits = digPos + ((adjust < 0) ? adjust : 0); // Actual number of digits in o/p
                    int numDigits = (firstDigit > totalDigits) ? firstDigit : totalDigits;
                    while (numDigits > groupTotalSizeCount)
                    {
                        if (groupSize == 0)
                        {
                            break;
                        }

                        ++thousandsSepCtr;
                        if (thousandsSepCtr >= thousandsSepPos.Length)
                        {
                            var newThousandsSepPos = new int[thousandsSepPos.Length * 2];
                            thousandsSepPos.CopyTo(newThousandsSepPos);
                            thousandsSepPos = newThousandsSepPos;
                        }

                        thousandsSepPos[thousandsSepCtr] = groupTotalSizeCount;
                        if (groupSizeIndex < groupSizeLen - 1)
                        {
                            groupSizeIndex++;
                            groupSize = groupDigits[groupSizeIndex];
                        }
                        groupTotalSizeCount += groupSize;
                    }
                }
            }

            if (number.IsNegative && (section == 0) && (number.Scale != 0))
            {
                vlb.Append(info.NegativeSignTChar<T>());
            }

            bool decimalWritten = false;

            int cur = 0;

            while (src < format.Length && (ch = format[src++]) != 0 && ch != ';')
            {
                if (adjust > 0)
                {
                    switch (ch)
                    {
                        case '#':
                        case '0':
                        case '.':
                            while (adjust > 0)
                            {
                                // digPos will be one greater than thousandsSepPos[thousandsSepCtr] since we are at
                                // the character after which the groupSeparator needs to be appended.
                                vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, cur) != 0 ? Unsafe.Add(ref dig, cur++) : '0'));
                                if (thousandSeps && digPos > 1 && thousandsSepCtr >= 0)
                                {
                                    if (digPos == thousandsSepPos[thousandsSepCtr] + 1)
                                    {
                                        vlb.Append(info.NumberGroupSeparatorTChar<T>());
                                        thousandsSepCtr--;
                                    }
                                }
                                digPos--;
                                adjust--;
                            }
                            break;
                    }
                }

                switch (ch)
                {
                    case '#':
                    case '0':
                        {
                            if (adjust < 0)
                            {
                                adjust++;
                                ch = digPos <= firstDigit ? '0' : '\0';
                            }
                            else
                            {
                                ch = Unsafe.Add(ref dig, cur) != 0 ? (char)Unsafe.Add(ref dig, cur++) : digPos > lastDigit ? '0' : '\0';
                            }

                            if (ch != 0)
                            {
                                vlb.Append(CastFrom<T>(ch));
                                if (thousandSeps && digPos > 1 && thousandsSepCtr >= 0)
                                {
                                    if (digPos == thousandsSepPos[thousandsSepCtr] + 1)
                                    {
                                        vlb.Append(info.NumberGroupSeparatorTChar<T>());
                                        thousandsSepCtr--;
                                    }
                                }
                            }

                            digPos--;
                            break;
                        }

                    case '.':
                        {
                            if (digPos != 0 || decimalWritten)
                            {
                                // For compatibility, don't echo repeated decimals
                                break;
                            }

                            // If the format has trailing zeros or the format has a decimal and digits remain
                            if (lastDigit < 0 || (decimalPos < digitCount && Unsafe.Add(ref dig, cur) != 0))
                            {
                                vlb.Append(info.NumberDecimalSeparatorTChar<T>());
                                decimalWritten = true;
                            }
                            break;
                        }

                    case '\x2030':
                        vlb.Append(info.PerMilleSymbolTChar<T>());
                        break;

                    case '%':
                        vlb.Append(info.PercentSymbolTChar<T>());
                        break;

                    case ',':
                        break;

                    case '\'':
                    case '"':
                        while (src < format.Length && format[src] != 0 && format[src] != ch)
                        {
                            AppendUnknownChar(ref vlb, format[src++]);
                        }

                        if (src < format.Length && format[src] != 0)
                        {
                            src++;
                        }
                        break;

                    case '\\':
                        if (src < format.Length && format[src] != 0)
                        {
                            AppendUnknownChar(ref vlb, format[src++]);
                        }
                        break;

                    case 'E':
                    case 'e':
                        {
                            bool positiveSign = false;
                            int i = 0;
                            if (scientific)
                            {
                                if (src < format.Length && format[src] == '0')
                                {
                                    // Handles E0, which should format the same as E-0
                                    i++;
                                }
                                else if (src + 1 < format.Length && format[src] == '+' && format[src + 1] == '0')
                                {
                                    // Handles E+0
                                    positiveSign = true;
                                }
                                else if (src + 1 < format.Length && format[src] == '-' && format[src + 1] == '0')
                                {
                                    // Handles E-0
                                    // Do nothing, this is just a place holder s.t. we don't break out of the loop.
                                }
                                else
                                {
                                    vlb.Append(CastFrom<T>(ch));
                                    break;
                                }

                                while (++src < format.Length && format[src] == '0')
                                {
                                    i++;
                                }

                                if (i > 10)
                                {
                                    i = 10;
                                }

                                int exp = dig == 0 ? 0 : number.Scale - decimalPos;
                                FormatExponent(ref vlb, info, exp, ch, i, positiveSign);
                                scientific = false;
                            }
                            else
                            {
                                vlb.Append(CastFrom<T>(ch));
                                if (src < format.Length)
                                {
                                    if (format[src] == '+' || format[src] == '-')
                                    {
                                        AppendUnknownChar(ref vlb, format[src++]);
                                    }

                                    while (src < format.Length && format[src] == '0')
                                    {
                                        AppendUnknownChar(ref vlb, format[src++]);
                                    }
                                }
                            }
                            break;
                        }

                    default:
                        AppendUnknownChar(ref vlb, ch);
                        break;
                }
            }

            if (number.IsNegative && (section == 0) && (number.Scale == 0) && (vlb.Length > 0))
            {
                vlb.Insert(0, info.NegativeSignTChar<T>());
            }
        }

        private static void FormatCurrency<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, int nMaxDigits, NumberFormatInfo info)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            string fmt = number.IsNegative ?
                s_negCurrencyFormats[info.CurrencyNegativePattern] :
                s_posCurrencyFormats[info.CurrencyPositivePattern];

            foreach (char ch in fmt)
            {
                switch (ch)
                {
                    case '#':
                        FormatFixed(ref vlb, ref number, nMaxDigits, info.CurrencyGroupSizes(), info.CurrencyDecimalSeparatorTChar<T>(), info.CurrencyGroupSeparatorTChar<T>());
                        break;

                    case '-':
                        vlb.Append(info.NegativeSignTChar<T>());
                        break;

                    case '$':
                        vlb.Append(info.CurrencySymbolTChar<T>());
                        break;

                    default:
                        vlb.Append(CastFrom<T>(ch));
                        break;
                }
            }
        }

        private static void FormatFixed<T>(
            ref ValueListBuilder<T> vlb, ref NumberBuffer number,
            int nMaxDigits, int[]? groupDigits,
            ReadOnlySpan<T> sDecimal, ReadOnlySpan<T> sGroup)
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            int digPos = number.Scale;

            ref var dig = ref MemoryMarshal.GetReference(number.Digits);
            int ix = 0;

            if (digPos > 0)
            {
                if (groupDigits != null)
                {
                    int groupSizeIndex = 0;                             // Index into the groupDigits array.
                    int bufferSize = digPos;                            // The length of the result buffer string.
                    int groupSize = 0;                                  // The current group size.

                    // Find out the size of the string buffer for the result.
                    if (groupDigits.Length != 0) // You can pass in 0 length arrays
                    {
                        int groupSizeCount = groupDigits[groupSizeIndex];   // The current total of group size.

                        while (digPos > groupSizeCount)
                        {
                            groupSize = groupDigits[groupSizeIndex];
                            if (groupSize == 0)
                            {
                                break;
                            }

                            bufferSize += sGroup.Length;
                            if (groupSizeIndex < groupDigits.Length - 1)
                            {
                                groupSizeIndex++;
                            }

                            groupSizeCount += groupDigits[groupSizeIndex];
                            ThrowHelper.ThrowIfNegative(groupSizeCount | bufferSize, string.Empty); // If we overflow
                        }

                        groupSize = groupSizeCount == 0 ? 0 : groupDigits[0]; // If you passed in an array with one entry as 0, groupSizeCount == 0
                    }

                    groupSizeIndex = 0;
                    int digitCount = 0;
                    int digLength = number.DigitsCount;
                    int digStart = (digPos < digLength) ? digPos : digLength;
                    ref var spanPtr = ref MemoryMarshal.GetReference(vlb.AppendSpan(bufferSize));

                    int ps = bufferSize - 1;
                    for (int i = digPos - 1; i >= 0; i--)
                    {
                        Unsafe.Add(ref spanPtr, ps--) = CastFrom<T>((i < digStart) ? (char)Unsafe.Add(ref dig, ix + i) : '0');

                        if (groupSize > 0)
                        {
                            digitCount++;
                            if ((digitCount == groupSize) && (i != 0))
                            {
                                for (int j = sGroup.Length - 1; j >= 0; j--)
                                {
                                    Unsafe.Add(ref spanPtr, ps--) = sGroup[j];
                                }

                                if (groupSizeIndex < groupDigits.Length - 1)
                                {
                                    groupSizeIndex++;
                                    groupSize = groupDigits[groupSizeIndex];
                                }
                                digitCount = 0;
                            }
                        }
                    }

                    Debug.Assert(ps >= -1, "Underflow");
                    ix += digStart;
                }
                else
                {
                    do
                    {
                        vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix) != 0 ? Unsafe.Add(ref dig, ix++) : '0'));
                    }
                    while (--digPos > 0);
                }
            }
            else
            {
                vlb.Append(CastFrom<T>('0'));
            }

            if (nMaxDigits > 0)
            {
                vlb.Append(sDecimal);
                if ((digPos < 0) && (nMaxDigits > 0))
                {
                    int zeroes = Math.Min(-digPos, nMaxDigits);
                    for (int i = 0; i < zeroes; i++)
                    {
                        vlb.Append(CastFrom<T>('0'));
                    }
                    digPos += zeroes;
                    nMaxDigits -= zeroes;
                }

                while (nMaxDigits > 0)
                {
                    vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix) != 0 ? Unsafe.Add(ref dig, ix++) : '0'));
                    nMaxDigits--;
                }
            }
        }

        /// <summary>Appends a char to the builder when the char is not known to be ASCII.</summary>
        /// <remarks>This requires a helper as if the character isn't ASCII, for UTF-8 encoding it will result in multiple bytes added.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void AppendUnknownChar<T>(ref ValueListBuilder<T> vlb, char ch)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            if (Unsafe.SizeOf<T>() == sizeof(char) || (uint)ch <= '\x007f')
            {
                vlb.Append(CastFrom<T>(ch));
            }
            else
            {
                AppendNonAsciiBytes(ref vlb, ch);
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            static void AppendNonAsciiBytes(ref ValueListBuilder<T> vlb, char ch)
            {
#if NET7_0_OR_GREATER
                var r = new Rune(ch);
                r.EncodeToUtf8(MemoryMarshal.AsBytes(vlb.AppendSpan(r.Utf8SequenceLength)));
#else
                var bytes = Encoding.UTF8.GetBytes($"{ch}").AsSpan();
                bytes.CopyTo(MemoryMarshal.AsBytes(vlb.AppendSpan(bytes.Length)));

#endif
            }
        }

        private static void FormatNumber<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, int nMaxDigits, NumberFormatInfo info)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            string fmt = number.IsNegative ?
                s_negNumberFormats[info.NumberNegativePattern] :
                PosNumberFormat;

            foreach (char ch in fmt)
            {
                switch (ch)
                {
                    case '#':
                        FormatFixed(ref vlb, ref number, nMaxDigits, info.NumberGroupSizes(), info.NumberDecimalSeparatorTChar<T>(), info.NumberGroupSeparatorTChar<T>());
                        break;

                    case '-':
                        vlb.Append(info.NegativeSignTChar<T>());
                        break;

                    default:
                        vlb.Append(CastFrom<T>(ch));
                        break;
                }
            }
        }

        private static void FormatScientific<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, int nMaxDigits, NumberFormatInfo info, char expChar)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));


            ref var dig = ref MemoryMarshal.GetReference(number.Digits);
            int ix = 0;

            vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix) != 0 ? Unsafe.Add(ref dig, ix++) : '0'));

            if (nMaxDigits != 1) // For E0 we would like to suppress the decimal point
            {
                vlb.Append(info.NumberDecimalSeparatorTChar<T>());
            }

            while (--nMaxDigits > 0)
            {
                vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix) != 0 ? Unsafe.Add(ref dig, ix++) : '0'));
            }

            int e = number.Digits[0] == 0 ? 0 : number.Scale - 1;
            FormatExponent(ref vlb, info, e, expChar, 3, true);
        }

        private static void FormatExponent<T>(ref ValueListBuilder<T> vlb, NumberFormatInfo info, int value, char expChar, int minDigits, bool positiveSign)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            vlb.Append(CastFrom<T>(expChar));

            if (value < 0)
            {
                vlb.Append(info.NegativeSignTChar<T>());
                value = -value;
            }
            else
            {
                if (positiveSign)
                {
                    vlb.Append(info.PositiveSignTChar<T>());
                }
            }

            Span<T> digits = stackalloc T[MaxUInt32DecDigits];
            int len = UInt32ToDecChars(digits, (uint)value, minDigits);
            vlb.Append(digits.Slice(0, len));
        }

        private static void FormatGeneral<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, int nMaxDigits, NumberFormatInfo info, char expChar, bool suppressScientific)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            int digPos = number.Scale;
            bool scientific = false;

            if (!suppressScientific)
            {
                // Don't switch to scientific notation
                if (digPos > nMaxDigits || digPos < -3)
                {
                    digPos = 1;
                    scientific = true;
                }
            }


            ref var dig = ref MemoryMarshal.GetReference(number.Digits);
            int ix = 0;

            if (digPos > 0)
            {
                do
                {
                    vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix) != 0 ? Unsafe.Add(ref dig, ix++) : '0'));
                }
                while (--digPos > 0);
            }
            else
            {
                vlb.Append(CastFrom<T>('0'));
            }

            if (Unsafe.Add(ref dig, ix) != 0 || digPos < 0)
            {
                vlb.Append(info.NumberDecimalSeparatorTChar<T>());

                while (digPos < 0)
                {
                    vlb.Append(CastFrom<T>('0'));
                    digPos++;
                }

                while (Unsafe.Add(ref dig, ix) != 0)
                {
                    vlb.Append(CastFrom<T>(Unsafe.Add(ref dig, ix++)));
                }
            }

            if (scientific)
            {
                FormatExponent(ref vlb, info, number.Scale - 1, expChar, 2, true);
            }
        }

        private static void FormatPercent<T>(ref ValueListBuilder<T> vlb, ref NumberBuffer number, int nMaxDigits, NumberFormatInfo info)
            where T : unmanaged
        {
            Debug.Assert(Unsafe.SizeOf<T>() == sizeof(char) || Unsafe.SizeOf<T>() == sizeof(byte));

            string fmt = number.IsNegative ?
                s_negPercentFormats[info.PercentNegativePattern] :
                s_posPercentFormats[info.PercentPositivePattern];

            foreach (char ch in fmt)
            {
                switch (ch)
                {
                    case '#':
                        FormatFixed(ref vlb, ref number, nMaxDigits, info.PercentGroupSizes(), info.PercentDecimalSeparatorTChar<T>(), info.PercentGroupSeparatorTChar<T>());
                        break;

                    case '-':
                        vlb.Append(info.NegativeSignTChar<T>());
                        break;

                    case '%':
                        vlb.Append(info.PercentSymbolTChar<T>());
                        break;

                    default:
                        vlb.Append(CastFrom<T>(ch));
                        break;
                }
            }
        }

        internal static void RoundNumber(ref NumberBuffer number, int pos, bool isCorrectlyRounded)
        {

            ref var dig = ref MemoryMarshal.GetReference(number.Digits);

            int i = 0;
            while (i < pos && Unsafe.Add(ref dig, i) != '\0')
            {
                i++;
            }

            if ((i == pos) && ShouldRoundUp(Unsafe.Add(ref dig, i), number.Kind, isCorrectlyRounded))
            {
                while (i > 0 && Unsafe.Add(ref dig, i - 1) == '9')
                {
                    i--;
                }

                if (i > 0)
                {
                    Unsafe.Add(ref dig, i - 1)++;
                }
                else
                {
                    number.Scale++;
                    dig = (byte)('1');
                    i = 1;
                }
            }
            else
            {
                while (i > 0 && Unsafe.Add(ref dig, i - 1) == '0')
                {
                    i--;
                }
            }

            if (i == 0)
            {
                if (number.Kind != NumberBufferKind.FloatingPoint)
                {
                    // The integer types don't have a concept of -0 and decimal always format -0 as 0
                    number.IsNegative = false;
                }
                number.Scale = 0;      // Decimals with scale ('0.00') should be rounded.
            }

            Unsafe.Add(ref dig, i) = (byte)('\0');
            number.DigitsCount = i;
            number.CheckConsistency();

            static bool ShouldRoundUp(byte digit, NumberBufferKind numberKind, bool isCorrectlyRounded)
            {
                // We only want to round up if the digit is greater than or equal to 5 and we are
                // not rounding a floating-point number. If we are rounding a floating-point number
                // we have one of two cases.
                //
                // In the case of a standard numeric-format specifier, the exact and correctly rounded
                // string will have been produced. In this scenario, pos will have pointed to the
                // terminating null for the buffer and so this will return false.
                //
                // However, in the case of a custom numeric-format specifier, we currently fall back
                // to generating Single/DoublePrecisionCustomFormat digits and then rely on this
                // function to round correctly instead. This can unfortunately lead to double-rounding
                // bugs but is the best we have right now due to back-compat concerns.

                if ((digit == '\0') || isCorrectlyRounded)
                {
                    // Fast path for the common case with no rounding
                    return false;
                }

                // Values greater than or equal to 5 should round up, otherwise we round down. The IEEE
                // 754 spec actually dictates that ties (exactly 5) should round to the nearest even number
                // but that can have undesired behavior for custom numeric format strings. This probably
                // needs further thought for .NET 5 so that we can be spec compliant and so that users
                // can get the desired rounding behavior for their needs.

                return digit >= '5';
            }
        }

        private static int FindSection(ReadOnlySpan<char> format, int section)
        {
            int src;
            char ch;

            if (section == 0)
            {
                return 0;
            }

            src = 0;
            while (true)
            {
                if (src >= format.Length)
                {
                    return 0;
                }

                switch (ch = format[src++])
                {
                    case '\'':
                    case '"':
                        while (src < format.Length && format[src] != 0 && format[src++] != ch) ;
                        break;

                    case '\\':
                        if (src < format.Length && format[src] != 0)
                        {
                            src++;
                        }
                        break;

                    case ';':
                        if (--section != 0)
                        {
                            break;
                        }

                        if (src < format.Length && format[src] != 0 && format[src] != ';')
                        {
                            return src;
                        }
                        goto case '\0';

                    case '\0':
                        return 0;
                }
            }
        }

#if SYSTEM_PRIVATE_CORELIB
        private static int[] NumberGroupSizes(this NumberFormatInfo info) => info._numberGroupSizes;

        private static int[] CurrencyGroupSizes(this NumberFormatInfo info) => info._currencyGroupSizes;

        private static int[] PercentGroupSizes(this NumberFormatInfo info) => info._percentGroupSizes;
#else

        private static int[] NumberGroupSizes(this NumberFormatInfo info) => info.NumberGroupSizes;

        private static int[] CurrencyGroupSizes(this NumberFormatInfo info) => info.CurrencyGroupSizes;

        private static int[] PercentGroupSizes(this NumberFormatInfo info) => info.PercentGroupSizes;
#endif
    }
}

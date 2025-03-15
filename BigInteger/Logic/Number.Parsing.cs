// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Kzrnm.Numerics.Logic
{
    static partial class Number
    {
        private static bool TryParseNumber<T>(ref ReadOnlySpan<T> str, NumberStyles styles, ref NumberBuffer number, NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert((styles & (NumberStyles.AllowHexSpecifier
#if NET8_0_OR_GREATER
                | NumberStyles.AllowBinarySpecifier
#endif
                )) == 0);

            const int StateSign = 0x0001;
            const int StateParens = 0x0002;
            const int StateDigits = 0x0004;
            const int StateNonZero = 0x0008;
            const int StateDecimal = 0x0010;
            const int StateCurrency = 0x0020;

            Debug.Assert(number.DigitsCount == 0);
            Debug.Assert(number.Scale == 0);
            Debug.Assert(!number.IsNegative);
            Debug.Assert(!number.HasNonZeroTail);

            number.CheckConsistency();

            ReadOnlySpan<T> decSep;                                 // decimal separator from NumberFormatInfo.
            ReadOnlySpan<T> groupSep;                               // group separator from NumberFormatInfo.
            ReadOnlySpan<T> currSymbol = ReadOnlySpan<T>.Empty; // currency symbol from NumberFormatInfo.

            bool parsingCurrency = false;
            if ((styles & NumberStyles.AllowCurrencySymbol) != 0)
            {
                currSymbol = info.CurrencySymbolTChar<T>();

                // The idea here is to match the currency separators and on failure match the number separators to keep the perf of VB's IsNumeric fast.
                // The values of decSep are setup to use the correct relevant separator (currency in the if part and decimal in the else part).
                decSep = info.CurrencyDecimalSeparatorTChar<T>();
                groupSep = info.CurrencyGroupSeparatorTChar<T>();
                parsingCurrency = true;
            }
            else
            {
                decSep = info.NumberDecimalSeparatorTChar<T>();
                groupSep = info.NumberGroupSeparatorTChar<T>();
            }

            int state = 0;
            var p = str;
            uint ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
            int len;

            while (true)
            {
                // Eat whitespace unless we've found a sign which isn't followed by a currency symbol.
                // "-Kr 1231.47" is legal but "- 1231.47" is not.
                if (!IsWhite(ch) || (styles & NumberStyles.AllowLeadingWhite) == 0 || ((state & StateSign) != 0 && (state & StateCurrency) == 0 && info.NumberNegativePattern != 2))
                {
                    if (((styles & NumberStyles.AllowLeadingSign) != 0) && (state & StateSign) == 0 && ((len = MatchChars(p, info.PositiveSignTChar<T>())) >= 0 || ((len = MatchNegativeSignChars(p, info)) >= 0 && (number.IsNegative = true))))
                    {
                        state |= StateSign;
                        p = p[^len..];
                    }
                    else if (ch == '(' && ((styles & NumberStyles.AllowParentheses) != 0) && ((state & StateSign) == 0))
                    {
                        state |= StateSign | StateParens;
                        number.IsNegative = true;
                        p = p[1..];
                    }
                    else if (!currSymbol.IsEmpty && (len = MatchChars(p, currSymbol)) >= 0)
                    {
                        state |= StateCurrency;
                        currSymbol = ReadOnlySpan<T>.Empty;
                        // We already found the currency symbol. There should not be more currency symbols. Set
                        // currSymbol to NULL so that we won't search it again in the later code path.
                        p = p[^len..];
                    }
                    else
                    {
                        break;
                    }
                }
                ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
            }

            int digCount = 0;
            int digEnd = 0;
            int maxDigCount = number.Digits.Length - 1;
            int numberOfTrailingZeros = 0;

            while (true)
            {
                if (IsDigit(ch))
                {
                    state |= StateDigits;

                    if (ch != '0' || (state & StateNonZero) != 0)
                    {
                        if (digCount < maxDigCount)
                        {
                            number.Digits[digCount] = (byte)ch;
                            if ((ch != '0') || (number.Kind != NumberBufferKind.Integer))
                            {
                                digEnd = digCount + 1;
                            }
                        }
                        else if (ch != '0')
                        {
                            // For decimal and binary floating-point numbers, we only
                            // need to store digits up to maxDigCount. However, we still
                            // need to keep track of whether any additional digits past
                            // maxDigCount were non-zero, as that can impact rounding
                            // for an input that falls evenly between two representable
                            // results.

                            number.HasNonZeroTail = true;
                        }

                        if ((state & StateDecimal) == 0)
                        {
                            number.Scale++;
                        }

                        if (digCount < maxDigCount)
                        {
                            // Handle a case like "53.0". We need to ignore trailing zeros in the fractional part for floating point numbers, so we keep a count of the number of trailing zeros and update digCount later
                            if (ch == '0')
                            {
                                numberOfTrailingZeros++;
                            }
                            else
                            {
                                numberOfTrailingZeros = 0;
                            }
                        }
                        digCount++;
                        state |= StateNonZero;
                    }
                    else if ((state & StateDecimal) != 0)
                    {
                        number.Scale--;
                    }
                    p = p[1..];
                }
                else if (((styles & NumberStyles.AllowDecimalPoint) != 0) && ((state & StateDecimal) == 0) && ((len = MatchChars(p, decSep)) >= 0 || (parsingCurrency && (state & StateCurrency) == 0 && (len = MatchChars(p, info.NumberDecimalSeparatorTChar<T>())) >= 0)))
                {
                    state |= StateDecimal;
                    p = p[^len..];
                }
                else if (((styles & NumberStyles.AllowThousands) != 0) && ((state & StateDigits) != 0) && ((state & StateDecimal) == 0) && ((len = MatchChars(p, groupSep)) >= 0 || (parsingCurrency && (state & StateCurrency) == 0 && (len = MatchChars(p, info.NumberGroupSeparatorTChar<T>())) >= 0)))
                {
                    p = p[^len..];
                }
                else
                {
                    break;
                }
                ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
            }

            bool negExp = false;
            number.DigitsCount = digEnd;
            number.Digits[digEnd] = (byte)'\0';
            if ((state & StateDigits) != 0)
            {
                if ((ch == 'E' || ch == 'e') && ((styles & NumberStyles.AllowExponent) != 0))
                {
                    var temp = p;
                    p = p[1..];
                    ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                    if ((len = MatchChars(p, info.PositiveSignTChar<T>())) >= 0)
                    {
                        ch = (p = p[^len..]).Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                    }
                    else if ((len = MatchNegativeSignChars(p, info)) >= 0)
                    {
                        ch = (p = p[^len..]).Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                        negExp = true;
                    }
                    if (IsDigit(ch))
                    {
                        int exp = 0;
                        do
                        {
                            // Check if we are about to overflow past our limit of 9 digits
                            if (exp >= 100_000_000)
                            {
                                // Set exp to Int.MaxValue to signify the requested exponent is too large. This will lead to an OverflowException later.
                                exp = int.MaxValue;
                                number.Scale = 0;

                                // Finish parsing the number, a FormatException could still occur later on.
                                while (IsDigit(ch))
                                {
                                    p = p[1..];
                                    ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                                }
                                break;
                            }

                            exp = (exp * 10) + (int)(ch - '0');
                            p = p[1..];
                            ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                        } while (IsDigit(ch));
                        if (negExp)
                        {
                            exp = -exp;
                        }
                        number.Scale += exp;
                    }
                    else
                    {
                        p = temp;
                        ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                    }
                }

                if (number.Kind == NumberBufferKind.FloatingPoint && !number.HasNonZeroTail)
                {
                    // Adjust the number buffer for trailing zeros
                    int numberOfFractionalDigits = digEnd - number.Scale;
                    if (numberOfFractionalDigits > 0)
                    {
                        numberOfTrailingZeros = Math.Min(numberOfTrailingZeros, numberOfFractionalDigits);
                        Debug.Assert(numberOfTrailingZeros >= 0);
                        number.DigitsCount = digEnd - numberOfTrailingZeros;
                        number.Digits[number.DigitsCount] = (byte)'\0';
                    }
                }

                while (true)
                {
                    if (!IsWhite(ch) || (styles & NumberStyles.AllowTrailingWhite) == 0)
                    {
                        if ((styles & NumberStyles.AllowTrailingSign) != 0 && ((state & StateSign) == 0) && ((len = MatchChars(p, info.PositiveSignTChar<T>())) >= 0 || (((len = MatchNegativeSignChars(p, info)) >= 0) && (number.IsNegative = true))))
                        {
                            state |= StateSign;
                            p = p[^len..];
                        }
                        else if (ch == ')' && ((state & StateParens) != 0))
                        {
                            state &= ~StateParens;
                            p = p[1..];
                        }
                        else if (!currSymbol.IsEmpty && (len = MatchChars(p, currSymbol)) >= 0)
                        {
                            currSymbol = ReadOnlySpan<T>.Empty;
                            p = p[^len..];
                        }
                        else
                        {
                            break;
                        }
                    }
                    ch = p.Length > 0 ? T.CastToUInt32(p[0]) : '\0';
                }
                if ((state & StateParens) == 0)
                {
                    if ((state & StateNonZero) == 0)
                    {
                        if (number.Kind != NumberBufferKind.Decimal)
                        {
                            number.Scale = 0;
                        }
                        if ((number.Kind == NumberBufferKind.Integer) && (state & StateDecimal) == 0)
                        {
                            number.IsNegative = false;
                        }
                    }
                    str = p;
                    return true;
                }
            }
            str = p;
            return false;
        }

        internal static bool TryStringToNumber<T>(ReadOnlySpan<T> value, NumberStyles styles, ref NumberBuffer number, NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(info != null);

            var p = value;

            if (!TryParseNumber(ref p, styles, ref number, info)
                || 0 < p.Length && !TrailingZeros(value, value.Length - p.Length))
            {
                number.CheckConsistency();
                return false;
            }

            number.CheckConsistency();
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)] // rare slow path that shouldn't impact perf of the main use case
        private static bool TrailingZeros<T>(ReadOnlySpan<T> value, int index)
            where T : unmanaged, IUtfChar<T>
        {
            // For compatibility, we need to allow trailing zeros at the end of a number string
            return !value.Slice(index).ContainsAnyExcept(T.CastFrom('\0'));
        }

        private static bool IsWhite(uint ch) => (ch == 0x20) || ((ch - 0x09) <= (0x0D - 0x09));

        private static bool IsDigit(uint ch) => (ch - '0') <= 9;

        private static bool IsSpaceReplacingChar(uint c) => (c == '\u00a0') || (c == '\u202f');

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int MatchNegativeSignChars<T>(ReadOnlySpan<T> p, NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            // returns remaining p

            var len = MatchChars(p, info.NegativeSignTChar<T>());

            if ((len < 0) && info.AllowHyphenDuringParsing() && p.Length > 0 && (T.CastToUInt32(p[0]) == '-'))
            {
                len = p.Length - 1;
            }

            return len;
        }

        private static int MatchChars<T>(ReadOnlySpan<T> p, ReadOnlySpan<T> value)
            where T : unmanaged, IUtfChar<T>
        {
            // returns remaining p

            if (value.Length > 0)
            {
                // We only hurt the failure case
                // This fix is for French or Kazakh cultures. Since a user cannot type 0xA0 or 0x202F as a
                // space character we use 0x20 space character instead to mean the same.
                while (true)
                {
                    uint cp = p.IsEmpty ? '\0' : T.CastToUInt32(p[0]);
                    uint val = T.CastToUInt32(value[0]);

                    if ((cp != val) && !(IsSpaceReplacingChar(val) && (cp == '\u0020')))
                    {
                        break;
                    }

                    p = p.Slice(1);
                    value = value.Slice(1);

                    if (value.Length == 0)
                    {
                        return p.Length;
                    }
                }
            }

            return -1;
        }
    }
}

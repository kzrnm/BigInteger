// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Kzrnm.Numerics.Logic
{
    using static SR;
#if Embedding
    public
#else
    internal
#endif
    static partial class Number
    {
        private const NumberStyles InvalidNumberStyles = ~(NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                                                           | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign
                                                           | NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint
                                                           | NumberStyles.AllowThousands | NumberStyles.AllowExponent
                                                           | NumberStyles.AllowCurrencySymbol | NumberStyles.AllowHexSpecifier
#if NET8_0_OR_GREATER
                                                           | NumberStyles.AllowBinarySpecifier
#endif
                                                        );

#if NET8_0_OR_GREATER
        private static ReadOnlySpan<uint> UInt32PowersOfTen => [1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000];
#else
        private static ReadOnlySpan<uint> UInt32PowersOfTen => _UInt32PowersOfTen;
        private static readonly uint[] _UInt32PowersOfTen = new uint[] { 1, 10, 100, 1000, 10000, 100000, 1000000, 10000000, 100000000, 1000000000 };
#endif

        [DoesNotReturn]
        internal static void ThrowOverflowOrFormatException(ParsingStatus status) => throw GetException(status);

        private static Exception GetException(ParsingStatus status)
        {
            return status == ParsingStatus.Failed
                ? new FormatException(Overflow_ParseBigInteger)
                : new OverflowException(Overflow_ParseBigInteger);
        }

        internal static bool TryValidateParseStyleInteger(NumberStyles style, [NotNullWhen(false)] out ArgumentException? e)
        {
            // Check for undefined flags
            if ((style & InvalidNumberStyles) != 0)
            {
                e = new ArgumentException(Argument_InvalidNumberStyles, nameof(style));
                return false;
            }
            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            { // Check for hex number
                if ((style & ~NumberStyles.HexNumber) != 0)
                {
                    e = new ArgumentException(Argument_InvalidHexStyle, nameof(style));
                    return false;
                }
            }
            e = null;
            return true;
        }

        internal static ParsingStatus TryParseBigInteger<T>(ReadOnlySpan<T> value, NumberStyles style, NumberFormatInfo info, out BigInteger result)
            where T : unmanaged, IBinaryInteger<T>
        {
            if (!TryValidateParseStyleInteger(style, out ArgumentException? e))
            {
                throw e; // TryParse still throws ArgumentException on invalid NumberStyles
            }

            if ((style & NumberStyles.AllowHexSpecifier) != 0)
            {
                return TryParseBigIntegerHexOrBinaryNumberStyle<BigIntegerHexParser<T>, T>(value, style, out result);
            }

#if NET8_0_OR_GREATER
            if ((style & NumberStyles.AllowBinarySpecifier) != 0)
            {
                return TryParseBigIntegerHexOrBinaryNumberStyle<BigIntegerBinaryParser<T>, T>(value, style, out result);
            }
#endif

            return TryParseBigIntegerNumber(value, style, info, out result);
        }

        internal static ParsingStatus TryParseBigIntegerNumber<T>(ReadOnlySpan<T> value, NumberStyles style, NumberFormatInfo info, out BigInteger result)
            where T : unmanaged, IBinaryInteger<T>
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

            var number = new NumberBuffer(NumberBufferKind.Integer, buffer);

            if (!NumberBuffer.TryStringToNumber(value, style, ref number, info))
            {
                result = default;
                ret = ParsingStatus.Failed;
            }
            else
            {
                ret = NumberToBigInteger(ref number, out result);
            }

            if (arrayFromPool != null)
                ArrayPool<byte>.Shared.Return(arrayFromPool);

            return ret;
        }

        internal static BigInteger ParseBigInteger<T>(ReadOnlySpan<T> value, NumberStyles style, NumberFormatInfo info)
            where T : unmanaged, IBinaryInteger<T>
        {
            if (!TryValidateParseStyleInteger(style, out ArgumentException? e))
            {
                throw e;
            }

            ParsingStatus status = TryParseBigInteger(value, style, info, out BigInteger result);
            if (status != ParsingStatus.OK)
            {
                ThrowOverflowOrFormatException(status);
            }

            return result;
        }

        internal static ParsingStatus TryParseBigIntegerHexOrBinaryNumberStyle<TParser, T>(ReadOnlySpan<T> value, NumberStyles style, out BigInteger result)
            where TParser : struct, IBigIntegerHexOrBinaryParser<TParser, T>
            where T : unmanaged, IBinaryInteger<T>
        {
            static bool IsWhite(uint ch) => (ch == 0x20) || ((ch - 0x09) <= (0x0D - 0x09));

            int whiteIndex;

            // Skip past any whitespace at the beginning.
            if ((style & NumberStyles.AllowLeadingWhite) != 0)
            {
                for (whiteIndex = 0; whiteIndex < value.Length; whiteIndex++)
                {
                    if (!IsWhite(CastToUInt32(value[whiteIndex])))
                        break;
                }

                value = value[whiteIndex..];
            }

            // Skip past any whitespace at the end.
            if ((style & NumberStyles.AllowTrailingWhite) != 0)
            {
                for (whiteIndex = value.Length - 1; whiteIndex >= 0; whiteIndex--)
                {
                    if (!IsWhite(CastToUInt32(value[whiteIndex])))
                        break;
                }

                value = value[..(whiteIndex + 1)];
            }

            if (value.IsEmpty)
            {
                goto FailExit;
            }

            // Remember the sign from original leading input
            // Invalid digits will be caught in parsing below
            uint signBits = TParser.GetSignBitsIfValid(CastToUInt32(value[0]));

            // Start from leading blocks. Leading blocks can be unaligned, or whole of 0/F's that need to be trimmed.
            int leadingBitsCount = value.Length % TParser.DigitsPerBlock;

            uint leading = signBits;
            // First parse unaligned leading block if exists.
            if (leadingBitsCount != 0)
            {
                if (!TParser.TryParseUnalignedBlock(value[0..leadingBitsCount], out leading))
                {
                    goto FailExit;
                }

                // Fill leading sign bits
                leading |= signBits << (leadingBitsCount * TParser.BitsPerDigit);
                value = value[leadingBitsCount..];
            }

            // Skip all the blocks consists of the same bit of sign
            while (!value.IsEmpty && leading == signBits)
            {
                if (!TParser.TryParseSingleBlock(value[0..TParser.DigitsPerBlock], out leading))
                {
                    goto FailExit;
                }
                value = value[TParser.DigitsPerBlock..];
            }

            if (value.IsEmpty)
            {
                // There's nothing beyond significant leading block. Return it as the result.
                if ((int)(leading ^ signBits) >= 0)
                {
                    // Small value that fits in Int32.
                    // Delegate to the constructor for int.MinValue handling.
                    result = new BigInteger((int)leading);
                    return ParsingStatus.OK;
                }
                else if (leading != 0)
                {
                    // The sign of result differs with leading digit.
                    // Require to store in _bits.

                    // Positive: sign=1, bits=[leading]
                    // Negative: sign=-1, bits=[(leading ^ -1) + 1]=[-leading]
#if NET8_0_OR_GREATER
                    result = new BigInteger((int)signBits | 1, [(leading ^ signBits) - signBits]);
#else
                    result = new BigInteger((int)signBits | 1, new[] { (leading ^ signBits) - signBits });
#endif
                    return ParsingStatus.OK;
                }
                else
                {
                    // -1 << 32, which requires an additional uint
#if NET8_0_OR_GREATER
                    result = new BigInteger(-1, [0, 1]);
#else
                    result = new BigInteger(-1, new uint[] { 0, 1 });
#endif
                    return ParsingStatus.OK;
                }
            }

            // Now the size of bits array can be calculated, except edge cases of -2^32N
            int wholeBlockCount = value.Length / TParser.DigitsPerBlock;
            int totalUIntCount = wholeBlockCount + 1;

            // Early out for too large input
            if (totalUIntCount > BigInteger.MaxLength)
            {
                result = default;
                return ParsingStatus.Overflow;
            }

            uint[] bits = new uint[totalUIntCount];
            Span<uint> wholeBlockDestination = bits.AsSpan(0, wholeBlockCount);

            if (!TParser.TryParseWholeBlocks(value, wholeBlockDestination))
            {
                goto FailExit;
            }

            bits[^1] = leading;

            if (signBits != 0)
            {
                // For negative values, negate the whole array
                if (bits.AsSpan().ContainsAnyExcept(0u))
                {
                    NumericsHelpers.DangerousMakeTwosComplement(bits);
                }
                else
                {
                    // For negative values with all-zero trailing digits,
                    // It requires additional leading 1.
                    bits = new uint[bits.Length + 1];
                    bits[^1] = 1;
                }

                result = new BigInteger(-1, bits);
                return ParsingStatus.OK;
            }
            else
            {
                Debug.Assert(leading != 0);

                // For positive values, it's done
                result = new BigInteger(1, bits);
                return ParsingStatus.OK;
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
        // `1233`, which is approx the upper bound of most RSA key lengths, covers the majority
        // of most common inputs and allows for the less naive algorithm to be used for
        // large/uncommon inputs.
        //
#if DEBUG && !Embedding
        // Mutable for unit testing...
        public static
#else
        public const
#endif
        int
            BigIntegerParseNaiveThreshold = 1233,
            BigIntegerParseNaiveThresholdInRecursive = 1 << 7;

        private static ParsingStatus NumberToBigInteger(ref NumberBuffer number, out BigInteger result)
        {
            if (number.Scale == int.MaxValue)
            {
                result = default;
                return ParsingStatus.Overflow;
            }

            if (number.Scale < 0)
            {
                result = default;
                return ParsingStatus.Failed;
            }

            uint[]? base1E9FromPool = null;
            scoped Span<uint> base1E9;

            {
                ReadOnlySpan<byte> intDigits = number.Digits.Slice(0, Math.Min(number.Scale, number.DigitsCount));
                int intDigitsEnd = intDigits.IndexOf<byte>(0);
                if (intDigitsEnd < 0)
                {
                    // Check for nonzero digits after the decimal point.
                    ReadOnlySpan<byte> fracDigitsSpan = number.Digits.Slice(intDigits.Length);
                    foreach (byte digitChar in fracDigitsSpan)
                    {
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
                }
                else
                    intDigits = intDigits.Slice(0, intDigitsEnd);

                int base1E9Length = (intDigits.Length + PowersOf1e9.MaxPartialDigits - 1) / PowersOf1e9.MaxPartialDigits;
                base1E9 = (
                    base1E9Length <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : base1E9FromPool = ArrayPool<uint>.Shared.Rent(base1E9Length)).Slice(0, base1E9Length);

                int di = base1E9Length;
                ReadOnlySpan<byte> leadingDigits = intDigits[..(intDigits.Length % PowersOf1e9.MaxPartialDigits)];
                if (leadingDigits.Length != 0)
                {
#if NET8_0_OR_GREATER
                    uint.TryParse(leadingDigits, out base1E9[--di]);
#else
                    UIntTryParse(leadingDigits, out base1E9[--di]);
#endif
                }

                intDigits = intDigits.Slice(leadingDigits.Length);
                Debug.Assert(intDigits.Length % PowersOf1e9.MaxPartialDigits == 0);

                for (--di; di >= 0; --di)
                {
#if NET8_0_OR_GREATER
                    uint.TryParse(intDigits.Slice(0, PowersOf1e9.MaxPartialDigits), out base1E9[di]);
#else
                    UIntTryParse(intDigits.Slice(0, PowersOf1e9.MaxPartialDigits), out base1E9[di]);
#endif
                    intDigits = intDigits.Slice(PowersOf1e9.MaxPartialDigits);
                }
                Debug.Assert(intDigits.Length == 0);
            }

            const double digitRatio = 0.10381025297; // log_{2^32}(10)
            int resultLength = checked((int)(digitRatio * number.Scale) + 1 + 2);
            uint[]? resultBufferFromPool = null;
            Span<uint> resultBuffer = (
                resultLength <= BigIntegerCalculator.StackAllocThreshold
                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                : resultBufferFromPool = ArrayPool<uint>.Shared.Rent(resultLength)).Slice(0, resultLength);
            resultBuffer.Clear();

            int totalDigitCount = Math.Min(number.DigitsCount, number.Scale);
            int trailingZeroCount = number.Scale - totalDigitCount;

            if (number.Scale <= BigIntegerParseNaiveThreshold)
            {
                Naive(base1E9, trailingZeroCount, resultBuffer);
            }
            else
            {
                DivideAndConquer(base1E9, trailingZeroCount, resultBuffer);
            }

            result = new BigInteger(resultBuffer, number.IsNegative);

            if (base1E9FromPool != null)
                ArrayPool<uint>.Shared.Return(base1E9FromPool);
            if (resultBufferFromPool != null)
                ArrayPool<uint>.Shared.Return(resultBufferFromPool);

            return ParsingStatus.OK;

            static void DivideAndConquer(ReadOnlySpan<uint> base1E9, int trailingZeroCount, scoped Span<uint> bits)
            {
                int valueDigits = (base1E9.Length - 1) * PowersOf1e9.MaxPartialDigits + CountDigits(base1E9[^1]);

                int powersOf1e9BufferLength = PowersOf1e9.GetBufferSize(Math.Max(valueDigits, trailingZeroCount + 1), out int maxIndex);
                uint[]? powersOf1e9BufferFromPool = null;
                Span<uint> powersOf1e9Buffer = (
                    (uint)powersOf1e9BufferLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : powersOf1e9BufferFromPool = ArrayPool<uint>.Shared.Rent(powersOf1e9BufferLength)).Slice(0, powersOf1e9BufferLength);
                powersOf1e9Buffer.Clear();

                PowersOf1e9 powersOf1e9 = new PowersOf1e9(powersOf1e9Buffer);

                if (trailingZeroCount > 0)
                {
                    int leadingLength = checked((int)(digitRatio * PowersOf1e9.MaxPartialDigits * base1E9.Length) + 3);
                    uint[]? leadingFromPool = null;
                    Span<uint> leading = (
                        leadingLength <= BigIntegerCalculator.StackAllocThreshold
                        ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                        : leadingFromPool = ArrayPool<uint>.Shared.Rent(leadingLength)).Slice(0, leadingLength);
                    leading.Clear();

                    Recursive(powersOf1e9, maxIndex, base1E9, leading);
                    leading = leading.Slice(0, BigIntegerCalculator.ActualLength(leading));

                    powersOf1e9.MultiplyPowerOfTen(leading, trailingZeroCount, bits);

                    if (leadingFromPool != null)
                        ArrayPool<uint>.Shared.Return(leadingFromPool);
                }
                else
                {
                    Recursive(powersOf1e9, maxIndex, base1E9, bits);
                }

                if (powersOf1e9BufferFromPool != null)
                    ArrayPool<uint>.Shared.Return(powersOf1e9BufferFromPool);
            }

            static void Recursive(in PowersOf1e9 powersOf1e9, int powersOf1e9Index, ReadOnlySpan<uint> base1E9, Span<uint> bits)
            {
                Debug.Assert(!bits.ContainsAnyExcept(0u));
                Debug.Assert(BigIntegerParseNaiveThresholdInRecursive > 1);

                base1E9 = base1E9.Slice(0, BigIntegerCalculator.ActualLength(base1E9));
                if (base1E9.Length < BigIntegerParseNaiveThresholdInRecursive)
                {
                    NaiveBase1E9ToBits(base1E9, bits);
                    return;
                }

                int multiplier1E9Length = 1 << powersOf1e9Index;
                while (base1E9.Length <= multiplier1E9Length)
                {
                    multiplier1E9Length = 1 << (--powersOf1e9Index);
                }
                ReadOnlySpan<uint> multiplier = powersOf1e9.GetSpan(powersOf1e9Index);
                int multiplierTrailingZeroCount = PowersOf1e9.OmittedLength(powersOf1e9Index);

                Debug.Assert(multiplier1E9Length < base1E9.Length && base1E9.Length <= multiplier1E9Length * 2);

                int bufferLength = checked((int)(digitRatio * PowersOf1e9.MaxPartialDigits * multiplier1E9Length) + 1 + 2);
                uint[]? bufferFromPool = null;
                scoped Span<uint> buffer = (
                    bufferLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : bufferFromPool = ArrayPool<uint>.Shared.Rent(bufferLength)).Slice(0, bufferLength);
                buffer.Clear();

                Recursive(powersOf1e9, powersOf1e9Index - 1, base1E9[multiplier1E9Length..], buffer);

                ReadOnlySpan<uint> buffer2 = buffer.Slice(0, BigIntegerCalculator.ActualLength(buffer));
                Span<uint> bitsUpper = bits.Slice(multiplierTrailingZeroCount, buffer2.Length + multiplier.Length);
                BigIntegerCalculator.Multiply(buffer2, multiplier, bitsUpper);

                buffer.Clear();

                Recursive(powersOf1e9, powersOf1e9Index - 1, base1E9[..multiplier1E9Length], buffer);

                BigIntegerCalculator.AddSelf(bits, buffer.Slice(0, BigIntegerCalculator.ActualLength(buffer)));

                if (bufferFromPool != null)
                    ArrayPool<uint>.Shared.Return(bufferFromPool);
            }

            static void Naive(ReadOnlySpan<uint> base1E9, int trailingZeroCount, scoped Span<uint> bits)
            {
                if (base1E9.Length == 0)
                {
                    // number is 0.
                    return;
                }
                int resultLength = NaiveBase1E9ToBits(base1E9, bits);

                int trailingPartialCount = Math.DivRem(trailingZeroCount, PowersOf1e9.MaxPartialDigits, out int remainingTrailingZeroCount);
                for (int i = 0; i < trailingPartialCount; i++)
                {
                    uint carry = MultiplyAdd(bits.Slice(0, resultLength), PowersOf1e9.TenPowMaxPartial, 0);
                    Debug.Assert(bits[resultLength] == 0);
                    if (carry != 0)
                        bits[resultLength++] = carry;
                }

                if (remainingTrailingZeroCount != 0)
                {
                    uint multiplier = UInt32PowersOfTen[remainingTrailingZeroCount];
                    uint carry = MultiplyAdd(bits.Slice(0, resultLength), multiplier, 0);
                    Debug.Assert(bits[resultLength] == 0);
                    if (carry != 0)
                        bits[resultLength++] = carry;
                }
            }

            static int NaiveBase1E9ToBits(ReadOnlySpan<uint> base1E9, Span<uint> bits)
            {
                if (base1E9.Length == 0)
                    return 0;

                int resultLength = 1;
                bits[0] = base1E9[^1];
                for (int i = base1E9.Length - 2; i >= 0; i--)
                {
                    uint carry = MultiplyAdd(bits.Slice(0, resultLength), PowersOf1e9.TenPowMaxPartial, base1E9[i]);
                    Debug.Assert(bits[resultLength] == 0);
                    if (carry != 0)
                        bits[resultLength++] = carry;
                }
                return resultLength;
            }

            static uint MultiplyAdd(Span<uint> bits, uint multiplier, uint addValue)
            {
                uint carry = addValue;

                for (int i = 0; i < bits.Length; i++)
                {
                    ulong p = (ulong)multiplier * bits[i] + carry;
                    bits[i] = (uint)p;
                    carry = (uint)(p >> 32);
                }
                return carry;
            }
        }

        private static string? FormatBigIntegerToHex<T>(bool targetSpan, BigInteger value, char format, int digits, NumberFormatInfo info, Span<T> destination, out int charsWritten, out bool spanSuccess)
            where T : unmanaged, IEquatable<T>
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

            var sb = new ValueListBuilder<T>(stackalloc T[128]); // each byte is typically two chars

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
                    sb.Append(
                        CastFrom<T>(head < 10 ?
                        (byte)(head + '0')
                        : format == 'X'
                            ? (byte)(head + ('A' - 10))
                            : (byte)(head + ('a' - 10))));
                    cur--;
                }
            }

            if (cur > -1)
            {
                var chars = sb.AppendSpan((cur + 1) * 2);
                int charsPos = 0;
                while (cur > -1)
                {
                    byte b = bits[cur--];
                    var hi = b >> 4;
                    var lo = b & 0xF;
                    chars[charsPos++] = CastFrom<T>(hi < 10 ?
                        (byte)(hi + '0')
                        : format == 'X'
                            ? (byte)(hi + ('A' - 10))
                            : (byte)(hi + ('a' - 10)));
                    chars[charsPos++] = CastFrom<T>(lo < 10 ?
                        (byte)(lo + '0')
                        : format == 'X'
                            ? (byte)(lo + ('A' - 10))
                            : (byte)(lo + ('a' - 10)));
                }
            }

            if (digits > sb.Length)
            {
                // Insert leading zeros, e.g. user specified "X5" so we create "0ABCD" instead of "ABCD"
                sb.Insert(
                    0,
                    CastFrom<T>(value._sign >= 0 ? '0' : (format == 'x') ? 'f' : 'F'),
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
                return sb.ToStringAndRelease();
            }
        }

        private static string? FormatBigIntegerToBinary<T>(bool targetSpan, BigInteger value, int digits, Span<T> destination, out int charsWritten, out bool spanSuccess)
            where T : unmanaged, IEquatable<T>
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

                throw new FormatException(Format_TooLarge);
            }

            int charsForBits = (int)tmpCharCount;

            Debug.Assert(digits < Array.MaxLength);
            int charsIncludeDigits = Math.Max(digits, charsForBits);

            if (targetSpan && charsIncludeDigits > destination.Length)
            {
                charsWritten = 0;
                spanSuccess = false;
                return null;
            }


            try
            {
                scoped ValueListBuilder<T> sb;
                if (targetSpan)
                {
                    // Because we have ensured destination can take actual char length, so now just use ValueListBuilder<char> as wrapper so that subsequent logic can be reused by 2 flows (targetSpan and non-targetSpan);
                    // meanwhile there is no need to copy to destination again after format data for targetSpan flow.
                    sb = new ValueListBuilder<T>(destination);
                }
                else
                {
                    // each byte is typically eight chars
                    sb = charsIncludeDigits > 512
                        ? new ValueListBuilder<T>(charsIncludeDigits)
                        : new ValueListBuilder<T>(stackalloc T[512]);
                }

                if (digits > charsForBits)
                {
                    sb.AppendSpan(digits - charsForBits).Fill(CastFrom<T>(value._sign >= 0 ? '0' : '1'));
                }

                AppendByte(ref sb, highByte, charsInHighByte - 1);

                for (int i = bytes.Length - 2; i >= 0; i--)
                {
                    AppendByte(ref sb, bytes[i]);
                }

                Debug.Assert(sb.Length == charsIncludeDigits);

                if (targetSpan)
                {
                    sb.Dispose();
                    charsWritten = charsIncludeDigits;
                    spanSuccess = true;
                    return null;
                }

                charsWritten = 0;
                spanSuccess = false;
                return sb.ToStringAndRelease();
            }
            finally
            {
                if (arrayToReturnToPool is not null)
                {
                    ArrayPool<byte>.Shared.Return(arrayToReturnToPool);
                }
            }

            static void AppendByte(ref ValueListBuilder<T> sb, byte b, int startHighBit = 7)
            {
                for (int i = startHighBit; i >= 0; i--)
                {
                    sb.Append(CastFrom<T>((byte)('0' + ((b >> i) & 1))));
                }
            }
        }

        internal static string FormatBigInteger(BigInteger value, string? format, NumberFormatInfo info)
        {
            return FormatBigInteger<char>(targetSpan: false, value, format, format, info, default, out _, out _)!;
        }

        internal static bool TryFormatBigInteger(BigInteger value, ReadOnlySpan<char> format, NumberFormatInfo info, Span<char> destination, out int charsWritten)
        {
            FormatBigInteger(targetSpan: true, value, null, format, info, destination, out charsWritten, out bool spanSuccess);
            return spanSuccess;
        }
        internal static bool TryFormatBigInteger(BigInteger value, ReadOnlySpan<char> format, NumberFormatInfo info, Span<byte> destination, out int charsWritten)
        {
            FormatBigInteger(targetSpan: true, value, null, format, info, destination, out charsWritten, out bool spanSuccess);
            return spanSuccess;
        }

        private static string? FormatBigInteger<T>(
            bool targetSpan, BigInteger value,
            string? formatString, ReadOnlySpan<char> formatSpan,
            NumberFormatInfo info, Span<T> destination, out int charsWritten, out bool spanSuccess)
            where T : unmanaged, IEquatable<T>
        {
            Debug.Assert(formatString == null || formatString.Length == formatSpan.Length);

            int digits = 0;
            char fmt = ParseFormatSpecifier(formatSpan, out digits);
            if (fmt == 'x' || fmt == 'X')
            {
                return FormatBigIntegerToHex(targetSpan, value, fmt, digits, info, destination, out charsWritten, out spanSuccess);
            }
            if (fmt == 'b' || fmt == 'B')
            {
                return FormatBigIntegerToBinary(targetSpan, value, digits, destination, out charsWritten, out spanSuccess);
            }

            if (value._bits == null)
            {
                if (fmt == 'g' || fmt == 'G' || fmt == 'r' || fmt == 'R')
                {
                    formatSpan = formatString = digits > 0 ? $"D{digits}" : "D";
                }

                if (targetSpan)
                {
#if NET8_0_OR_GREATER
                    spanSuccess = typeof(T) == typeof(byte)
                        ? value._sign.TryFormat(SpanCast<T, byte>(destination), out charsWritten, formatSpan, info)
                        : value._sign.TryFormat(SpanCast<T, char>(destination), out charsWritten, formatSpan, info);
#else
                    if (typeof(T) == typeof(byte))
                    {
                        Span<char> c = stackalloc char[512];
                        spanSuccess = value._sign.TryFormat(c, out charsWritten, formatSpan, info);
                        if (spanSuccess)
                        {
                            charsWritten = Encoding.UTF8.GetBytes(c[..charsWritten], MemoryMarshal.Cast<T, byte>(destination));
                        }
                    }
                    else
                    {
                        spanSuccess = value._sign.TryFormat(SpanCast<T, char>(destination), out charsWritten, formatSpan, info);
                    }
#endif
                    return null;
                }
                else
                {
                    Debug.Assert(formatString != null);
                    charsWritten = 0;
                    spanSuccess = false;
                    return value._sign.ToString(formatString, info);
                }
            }

            // The Ratio is calculated as: log_{10^9}(2^32) = 1.0703288734719332
            // value._bits.Length represents the number of digits when considering value
            // in base 2^32. This means it satisfies the inequality:
            // value._bits.Length - 1 <= log_{2^32}(value) < value._bits.Length
            //
            // When converting value to a decimal string, it is first converted to
            // base 1,000,000,000.
            //
            // Dividing the equation by log_{2^32}(10^9),which is equivalent to
            // multiplying by log_{10^9}(2^32), and using the base change formula,
            // we get:
            // M - log_{10^9}(2^32) <= log_{10^9}(value) < M <= Ceiling(M)
            // where M is log_{10^9}(2^32)*value._bits.Length.
            // In other words, the number of digits of value in base 1,000,000,000 is at most Ceiling(M).
            const double digitRatio = 1.070328873472;
            Debug.Assert(BigInteger.MaxLength * digitRatio + 1 < Array.MaxLength); // won't overflow

            int base1E9BufferLength = (int)(value._bits.Length * digitRatio) + 1;
            uint[]? base1E9BufferFromPool = null;
            Span<uint> base1E9Buffer = ((uint)base1E9BufferLength <= BigIntegerCalculator.StackAllocThreshold ?
                stackalloc uint[base1E9BufferLength] :
                (base1E9BufferFromPool = ArrayPool<uint>.Shared.Rent(base1E9BufferLength))).Slice(0, base1E9BufferLength);
            base1E9Buffer.Clear();


            BigIntegerToBase1E9(value._bits, base1E9Buffer, out int written);
            ReadOnlySpan<uint> base1E9Value = base1E9Buffer[..written];

            int valueDigits = (base1E9Value.Length - 1) * PowersOf1e9.MaxPartialDigits + CountDigits(base1E9Value[^1]);

            string? strResult;

            if (fmt == 'g' || fmt == 'G' || fmt == 'd' || fmt == 'D' || fmt == 'r' || fmt == 'R')
            {
                int strDigits = Math.Max(digits, valueDigits);
                var sNegative = value.Sign < 0 ? info.NegativeSignTChar<T>() : null;
                int strLength = strDigits + sNegative.Length;

                if (targetSpan)
                {
                    if (destination.Length < strLength)
                    {
                        spanSuccess = false;
                        charsWritten = 0;
                    }
                    else
                    {
                        sNegative.CopyTo(destination);
                        BigIntegerToDecChars(destination.Slice(0, strLength), base1E9Value, digits);
                        charsWritten = strLength;
                        spanSuccess = true;
                    }
                    strResult = null;
                }
                else
                {
                    spanSuccess = false;
                    charsWritten = 0;
#if NET9_0_OR_GREATER
                    strResult = string.Create(strLength, new StrState(digits, base1E9Value, SpanCast<T, char>(sNegative)), static (span, state) =>
                    {
                        state.sNegative.CopyTo(span);
                        BigIntegerToDecChars(span, state.base1E9Value, state.digits);
                    });
#else
                    char[]? numberBufferToReturn = null;
                    Span<char> numberBuffer = (strLength <= CharStackBufferSize ?
                        stackalloc char[strLength] :
                        (numberBufferToReturn = ArrayPool<char>.Shared.Rent(strLength))).Slice(0, strLength);

                    info.NegativeSign?.CopyTo(numberBuffer);
                    BigIntegerToDecChars(numberBuffer, base1E9Value, digits);

                    strResult = new string(numberBuffer);

                    if (numberBufferToReturn != null)
                        ArrayPool<char>.Shared.Return(numberBufferToReturn);
#endif
                }
            }
            else
            {
                int numberBufferLength = valueDigits + 1;
                byte[]? numberBufferToReturn = null;
                Span<byte> numberBuffer = (numberBufferLength <= CharStackBufferSize ?
                    stackalloc byte[numberBufferLength] :
                    (numberBufferToReturn = ArrayPool<byte>.Shared.Rent(numberBufferLength))).Slice(0, numberBufferLength);

                scoped NumberBuffer number = new NumberBuffer(NumberBufferKind.Integer, numberBuffer);
                BigIntegerToDecChars(numberBuffer.Slice(0, valueDigits), base1E9Value, valueDigits);
                number.Digits[^1] = 0;
                number.DigitsCount = valueDigits;
                number.Scale = valueDigits;
                number.IsNegative = value.Sign < 0;

                scoped var vlb = new ValueListBuilder<T>(stackalloc T[CharStackBufferSize]); // arbitrary stack cut-off

                if (fmt != 0)
                {
                    NumberToString(ref vlb, ref number, fmt, digits, info);
                }
                else
                {
                    NumberToStringFormat(ref vlb, ref number, formatSpan, info);
                }

                if (targetSpan)
                {
                    spanSuccess = vlb.TryCopyTo(destination, out charsWritten);
                    strResult = null;
                }
                else
                {
                    charsWritten = 0;
                    spanSuccess = false;
                    strResult = vlb.AsSpan().ToString();
                }

                vlb.Dispose();
                if (numberBufferToReturn != null)
                {
                    ArrayPool<byte>.Shared.Return(numberBufferToReturn);
                }
            }

            if (base1E9BufferFromPool != null)
            {
                ArrayPool<uint>.Shared.Return(base1E9BufferFromPool);
            }

            return strResult;
        }

#if NET9_0_OR_GREATER
        [StructLayout(LayoutKind.Auto)]
        readonly ref struct StrState(int digits, ReadOnlySpan<uint> base1E9Value, ReadOnlySpan<char> sNegative)
        {
            public readonly int digits = digits;
            public readonly ReadOnlySpan<uint> base1E9Value = base1E9Value;
            public readonly ReadOnlySpan<char> sNegative = sNegative;
        }

#endif

        private static int BigIntegerToDecChars<T>(Span<T> buffer, ReadOnlySpan<uint> base1E9Value, int digits)
            where T : unmanaged
        {
            Debug.Assert(base1E9Value[^1] != 0, "Leading zeros should be trimmed by caller.");

            // The base 10^9 value is in reverse order
            for (int i = 0; i < base1E9Value.Length - 1; i++)
            {
                int len = UInt32ToDecChars(buffer, base1E9Value[i], PowersOf1e9.MaxPartialDigits);
                buffer = buffer.Slice(0, len);
                digits -= PowersOf1e9.MaxPartialDigits;
            }

            return UInt32ToDecChars(buffer, base1E9Value[^1], digits);
        }

#if DEBUG && !Embedding
        // Mutable for unit testing...
        public static
#else
        public const
#endif
            int ToStringNaiveThreshold = BigIntegerCalculator.DivideBurnikelZieglerThreshold;
        private static void BigIntegerToBase1E9(ReadOnlySpan<uint> bits, Span<uint> base1E9Buffer, out int base1E9Written)
        {
            Debug.Assert(ToStringNaiveThreshold >= 2);

            if (bits.Length <= ToStringNaiveThreshold)
            {
                Naive(bits, base1E9Buffer, out base1E9Written);
                return;
            }

            PowersOf1e9.FloorBufferSize(bits.Length, out int powersOf1e9BufferLength, out int maxIndex);
            uint[]? powersOf1e9BufferFromPool = null;
            Span<uint> powersOf1e9Buffer = (
                powersOf1e9BufferLength <= BigIntegerCalculator.StackAllocThreshold
                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                : powersOf1e9BufferFromPool = ArrayPool<uint>.Shared.Rent(powersOf1e9BufferLength)).Slice(0, powersOf1e9BufferLength);
            powersOf1e9Buffer.Clear();

            PowersOf1e9 powersOf1e9 = new PowersOf1e9(powersOf1e9Buffer);

            DivideAndConquer(powersOf1e9, maxIndex, bits, base1E9Buffer, out base1E9Written);

            if (powersOf1e9BufferFromPool != null)
            {
                ArrayPool<uint>.Shared.Return(powersOf1e9BufferFromPool);
            }

            static void DivideAndConquer(in PowersOf1e9 powersOf1e9, int powersIndex, ReadOnlySpan<uint> bits, Span<uint> base1E9Buffer, out int base1E9Written)
            {
                Debug.Assert(bits.Length == 0 || bits[^1] != 0);
                Debug.Assert(powersIndex >= 0);

                if (bits.Length <= ToStringNaiveThreshold)
                {
                    Naive(bits, base1E9Buffer, out base1E9Written);
                    return;
                }

                ReadOnlySpan<uint> powOfTen = powersOf1e9.GetSpan(powersIndex);
                int omittedLength = PowersOf1e9.OmittedLength(powersIndex);

                while (bits.Length < powOfTen.Length + omittedLength || BigIntegerCalculator.Compare(bits.Slice(omittedLength), powOfTen) < 0)
                {
                    --powersIndex;
                    powOfTen = powersOf1e9.GetSpan(powersIndex);
                    omittedLength = PowersOf1e9.OmittedLength(powersIndex);
                }

                int upperLength = bits.Length - powOfTen.Length - omittedLength + 1;
                uint[]? upperFromPool = null;
                Span<uint> upper = ((uint)upperLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : upperFromPool = ArrayPool<uint>.Shared.Rent(upperLength)).Slice(0, upperLength);

                int lowerLength = bits.Length;
                uint[]? lowerFromPool = null;
                Span<uint> lower = ((uint)lowerLength <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : lowerFromPool = ArrayPool<uint>.Shared.Rent(lowerLength)).Slice(0, lowerLength);

                bits.Slice(0, omittedLength).CopyTo(lower);
                BigIntegerCalculator.Divide(bits.Slice(omittedLength), powOfTen, upper, lower.Slice(omittedLength));

                Debug.Assert(upper.ContainsAnyExcept(0u));

                int lower1E9Length = 1 << powersIndex;

                DivideAndConquer(
                    powersOf1e9,
                    powersIndex - 1,
                    lower.Slice(0, BigIntegerCalculator.ActualLength(lower)),
                    base1E9Buffer,
                    out int lowerWritten);

                if (lowerFromPool != null)
                    ArrayPool<uint>.Shared.Return(lowerFromPool);

                Debug.Assert(lower1E9Length >= lowerWritten);

                DivideAndConquer(
                    powersOf1e9,
                    powersIndex - 1,
                    upper.Slice(0, BigIntegerCalculator.ActualLength(upper)),
                    base1E9Buffer.Slice(lower1E9Length),
                    out base1E9Written);

                if (upperFromPool != null)
                    ArrayPool<uint>.Shared.Return(upperFromPool);

                base1E9Written += lower1E9Length;
            }

            static void Naive(ReadOnlySpan<uint> bits, Span<uint> base1E9Buffer, out int base1E9Written)
            {
                base1E9Written = 0;

                for (int iuSrc = bits.Length; --iuSrc >= 0;)
                {
                    uint uCarry = bits[iuSrc];
                    Span<uint> base1E9 = base1E9Buffer.Slice(0, base1E9Written);
                    for (int iuDst = 0; iuDst < base1E9.Length; iuDst++)
                    {
                        Debug.Assert(base1E9[iuDst] < PowersOf1e9.TenPowMaxPartial);

                        // Use X86Base.DivRem when stable
                        ulong uuRes = NumericsHelpers.MakeUInt64(base1E9[iuDst], uCarry);
                        var quo = uuRes / PowersOf1e9.TenPowMaxPartial;
                        var rem = uuRes - quo * PowersOf1e9.TenPowMaxPartial;
                        uCarry = (uint)quo;
                        base1E9[iuDst] = (uint)rem;
                    }
                    if (uCarry != 0)
                    {
                        var quo = uCarry / PowersOf1e9.TenPowMaxPartial;
                        base1E9Buffer[base1E9Written++] = uCarry - quo * PowersOf1e9.TenPowMaxPartial;
                        uCarry = quo;
                        if (uCarry != 0)
                            base1E9Buffer[base1E9Written++] = uCarry;
                    }
                }
            }
        }

        internal readonly ref struct PowersOf1e9
        {
            // Holds 1000000000^(1<<<n).
            private readonly ReadOnlySpan<uint> pow1E9;
            public const uint TenPowMaxPartial = 1000000000;
            public const int MaxPartialDigits = 9;

            // indexes[i] is pre-calculated length of (10^9)^i
            // This means that pow1E9[indexes[i-1]..indexes[i]] equals 1000000000 * (1<<i)
            //
            // The `indexes` are calculated as follows
            //    const double digitRatio = 0.934292276687070661; // log_{2^32}(10^9)
            //    int[] indexes = new int[32];
            //    indexes[0] = 0;
            //    for (int i = 0; i + 1 < indexes.Length; i++)
            //    {
            //        int length = unchecked((int)(digitRatio * (1 << i)) + 1);
            //        length -= (9*(1<<i)) >> 5;
            //        indexes[i+1] = indexes[i] + length;
            //    }
#if NET8_0_OR_GREATER
            private static ReadOnlySpan<int> Indexes =>
            [
                0,
                1,
                3,
                6,
                12,
                23,
                44,
                86,
                170,
                338,
                673,
                1342,
                2680,
                5355,
                10705,
                21405,
                42804,
                85602,
                171198,
                342390,
                684773,
                1369538,
                2739067,
                5478125,
                10956241,
                21912473,
                43824936,
                87649862,
                175299713,
                484817143,
                969634274,
                1939268536,
            ];
#else
            private static ReadOnlySpan<int> Indexes => _Indexes;
            private static readonly int[] _Indexes = new[]
            {
                0,
                1,
                3,
                6,
                12,
                23,
                44,
                86,
                170,
                338,
                673,
                1342,
                2680,
                5355,
                10705,
                21405,
                42804,
                85602,
                171198,
                342390,
                684773,
                1369538,
                2739067,
                5478125,
                10956241,
                21912473,
                43824936,
                87649862,
                175299713,
                484817143,
                969634274,
                1939268536,
            };
#endif

            // The PowersOf1e9 structure holds 1000000000^(1<<<n). However, if the lower element is zero,
            // it is truncated. Therefore, if the lower element becomes zero in the process of calculating
            // 1000000000^(1<<<n), it must be truncated. If 1000000000^(1<<<<n) is calculated in advance
            // for less than 6, there is no need to consider the case where the lower element becomes zero
            // during the calculation process, since 1000000000^(1<<<<n) mod 32 is always zero.
#if NET8_0_OR_GREATER
            private static ReadOnlySpan<uint> LeadingPowers1E9 =>
            [
                // 1000000000^(1<<0)
                1000000000,
                // 1000000000^(1<<1)
                2808348672,
                232830643,
                // 1000000000^(1<<2)
                3008077584,
                2076772117,
                12621774,
                // 1000000000^(1<<3)
                4130660608,
                835571558,
                1441351422,
                977976457,
                264170013,
                37092,
                // 1000000000^(1<<4)
                767623168,
                4241160024,
                1260959332,
                2541775228,
                2965753944,
                1796720685,
                484800439,
                1311835347,
                2945126454,
                3563705203,
                1375821026,
                // 1000000000^(1<<5)
                3940379521,
                184513341,
                2872588323,
                2214530454,
                38258512,
                2980860351,
                114267010,
                2188874685,
                234079247,
                2101059099,
                1948702207,
                947446250,
                864457656,
                507589568,
                1321007357,
                3911984176,
                1011110295,
                2382358050,
                2389730781,
                730678769,
                440721283,
            ];
#else
            private static ReadOnlySpan<uint> LeadingPowers1E9 => _LeadingPowers1E9;
            private static readonly uint[] _LeadingPowers1E9 = new uint[]
            {
                // 1000000000^(1<<0)
                1000000000,
                // 1000000000^(1<<1)
                2808348672,
                232830643,
                // 1000000000^(1<<2)
                3008077584,
                2076772117,
                12621774,
                // 1000000000^(1<<3)
                4130660608,
                835571558,
                1441351422,
                977976457,
                264170013,
                37092,
                // 1000000000^(1<<4)
                767623168,
                4241160024,
                1260959332,
                2541775228,
                2965753944,
                1796720685,
                484800439,
                1311835347,
                2945126454,
                3563705203,
                1375821026,
                // 1000000000^(1<<5)
                3940379521,
                184513341,
                2872588323,
                2214530454,
                38258512,
                2980860351,
                114267010,
                2188874685,
                234079247,
                2101059099,
                1948702207,
                947446250,
                864457656,
                507589568,
                1321007357,
                3911984176,
                1011110295,
                2382358050,
                2389730781,
                730678769,
                440721283,
            };
#endif

            public PowersOf1e9(Span<uint> pow1E9)
            {
                Debug.Assert(pow1E9.Length >= 1);
                Debug.Assert(Indexes[6] == LeadingPowers1E9.Length);
                if (pow1E9.Length <= LeadingPowers1E9.Length)
                {
                    this.pow1E9 = LeadingPowers1E9;
                    return;
                }
                LeadingPowers1E9.CopyTo(pow1E9.Slice(0, LeadingPowers1E9.Length));
                this.pow1E9 = pow1E9;

                ReadOnlySpan<uint> src = pow1E9.Slice(Indexes[5], Indexes[6] - Indexes[5]);
                int toExclusive = Indexes[6];
                for (int i = 6; i + 1 < Indexes.Length; i++)
                {
                    Debug.Assert(2 * src.Length - (Indexes[i + 1] - Indexes[i]) is 0 or 1);
                    if (pow1E9.Length - toExclusive < (src.Length << 1))
                        break;
                    Span<uint> dst = pow1E9.Slice(toExclusive, src.Length << 1);
                    BigIntegerCalculator.Square(src, dst);
                    int from = toExclusive;
                    toExclusive = Indexes[i + 1];
                    src = pow1E9.Slice(from, toExclusive - from);
                    Debug.Assert(toExclusive == pow1E9.Length || pow1E9[toExclusive] == 0);
                }
            }

            public static int GetBufferSize(int digits, out int maxIndex)
            {
                uint scale1E9 = (uint)(digits - 1) / MaxPartialDigits;
                maxIndex = BitOperations.Log2(scale1E9);
                int index = maxIndex + 1;
                int bufferSize;
                if ((uint)index < (uint)Indexes.Length)
                    bufferSize = Indexes[index];
                else
                {
                    maxIndex = Indexes.Length - 2;
                    bufferSize = Indexes[^1];
                }

                return ++bufferSize;
            }

            public ReadOnlySpan<uint> GetSpan(int index)
            {
                // Returns 1E9^(1<<index) >> (32*(9*(1<<index)/32))
                int from = Indexes[index];
                int toExclusive = Indexes[index + 1];
                return pow1E9.Slice(from, toExclusive - from);
            }

            public static int OmittedLength(int index)
            {
                // Returns 9*(1<<index)/32
                return (MaxPartialDigits * (1 << index)) >> 5;
            }

            public static void FloorBufferSize(int size, out int bufferSize, out int maxIndex)
            {
                Debug.Assert(size > 0);

                // binary search
                // size < Indexes[hi+1] - Indexes[hi]
                // size >= Indexes[lo+1] - Indexes[lo]
                int hi = Indexes.Length - 1;
                maxIndex = 0;
                while (maxIndex + 1 < hi)
                {
                    int i = (hi + maxIndex) >> 1;
                    if (size < Indexes[i + 1] - Indexes[i])
                    {
                        hi = i;
                    }
                    else
                    {
                        maxIndex = i;
                    }
                }
                bufferSize = Indexes[maxIndex + 1] + 1;
            }

            public void MultiplyPowerOfTen(ReadOnlySpan<uint> left, int trailingZeroCount, Span<uint> bits)
            {
                Debug.Assert(trailingZeroCount >= 0);
                if (trailingZeroCount < UInt32PowersOfTen.Length)
                {
                    BigIntegerCalculator.Multiply(left, UInt32PowersOfTen[trailingZeroCount], bits.Slice(0, left.Length + 1));
                    return;
                }

                uint[]? powersOfTenFromPool = null;

                Span<uint> powersOfTen = (
                    bits.Length <= BigIntegerCalculator.StackAllocThreshold
                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                    : powersOfTenFromPool = ArrayPool<uint>.Shared.Rent(bits.Length)).Slice(0, bits.Length);
                scoped Span<uint> powersOfTen2 = bits;

                int trailingPartialCount = Math.DivRem(trailingZeroCount, MaxPartialDigits, out int remainingTrailingZeroCount);

                int fi = BitOperations.TrailingZeroCount(trailingPartialCount);
                int omittedLength = OmittedLength(fi);

                // Copy first
                ReadOnlySpan<uint> first = GetSpan(fi);
                int curLength = first.Length;
                trailingPartialCount >>= fi;
                trailingPartialCount >>= 1;

                if ((BitOperations.PopCount((uint)trailingPartialCount) & 1) != 0)
                {
                    powersOfTen2 = powersOfTen;
                    powersOfTen = bits;
                    powersOfTen2.Clear();
                }

                first.CopyTo(powersOfTen);

                for (++fi; trailingPartialCount != 0; ++fi, trailingPartialCount >>= 1)
                {
                    Debug.Assert(fi + 1 < Indexes.Length);
                    if ((trailingPartialCount & 1) != 0)
                    {
                        omittedLength += OmittedLength(fi);

                        ReadOnlySpan<uint> power = GetSpan(fi);
                        Span<uint> src = powersOfTen.Slice(0, curLength);
                        Span<uint> dst = powersOfTen2.Slice(0, curLength += power.Length);

                        BigIntegerCalculator.Multiply(src, power, dst);

                        Span<uint> tmp = powersOfTen;
                        powersOfTen = powersOfTen2;
                        powersOfTen2 = tmp;
                        powersOfTen2.Clear();

                        // Trim
                        while (--curLength >= 0 && powersOfTen[curLength] == 0) ;
                        ++curLength;
                    }
                }

                Debug.Assert(Unsafe.AreSame(ref bits[0], ref powersOfTen2[0]));

                powersOfTen = powersOfTen.Slice(0, curLength);
                Span<uint> bits2 = bits.Slice(omittedLength, curLength += left.Length);

                BigIntegerCalculator.Multiply(left, powersOfTen, bits2);

                if (powersOfTenFromPool != null)
                    ArrayPool<uint>.Shared.Return(powersOfTenFromPool);

                if (remainingTrailingZeroCount > 0)
                {
                    uint multiplier = UInt32PowersOfTen[remainingTrailingZeroCount];
                    uint carry = 0;
                    for (int i = 0; i < bits2.Length; i++)
                    {
                        ulong p = (ulong)multiplier * bits2[i] + carry;
                        bits2[i] = (uint)p;
                        carry = (uint)(p >> 32);
                    }

                    if (carry != 0)
                    {
                        bits[omittedLength + curLength] = carry;
                    }
                }
            }
        }
    }

    internal interface IBigIntegerHexOrBinaryParser<TParser, T>
        where TParser : struct, IBigIntegerHexOrBinaryParser<TParser, T>
        where T : unmanaged, IBinaryInteger<T>
    {
        static abstract int BitsPerDigit { get; }

        static virtual int DigitsPerBlock => sizeof(uint) * 8 / TParser.BitsPerDigit;

        static abstract NumberStyles BlockNumberStyle { get; }

        static abstract uint GetSignBitsIfValid(uint ch);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static virtual bool TryParseUnalignedBlock(ReadOnlySpan<T> input, out uint result)
        {
            if (typeof(T) == typeof(char))
            {
                return uint.TryParse(SpanCast<T, char>(input), TParser.BlockNumberStyle, null, out result);
            }

            if (typeof(T) == typeof(byte))
            {
#if NET8_0_OR_GREATER
                return uint.TryParse(SpanCast<T, byte>(input), TParser.BlockNumberStyle, null, out result);
#else
                return Utf8Parser.TryParse(SpanCast<T, byte>(input), out result, out _, 'x');
#endif
            }
            result = 0;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static virtual bool TryParseSingleBlock(ReadOnlySpan<T> input, out uint result)
            => TParser.TryParseUnalignedBlock(input, out result);

        static virtual bool TryParseWholeBlocks(ReadOnlySpan<T> input, Span<uint> destination)
        {
            Debug.Assert(destination.Length * TParser.DigitsPerBlock == input.Length);
            ref T lastWholeBlockStart = ref Unsafe.Add(ref MemoryMarshal.GetReference(input), input.Length - TParser.DigitsPerBlock);

            for (int i = 0; i < destination.Length; i++)
            {
                if (!TParser.TryParseSingleBlock(
                    MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Subtract(ref lastWholeBlockStart, i * TParser.DigitsPerBlock), TParser.DigitsPerBlock),
                    out destination[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    internal readonly struct BigIntegerHexParser<T> : IBigIntegerHexOrBinaryParser<BigIntegerHexParser<T>, T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public static int BitsPerDigit => 4;

        public static NumberStyles BlockNumberStyle => NumberStyles.AllowHexSpecifier;

        // A valid ASCII hex digit is positive (0-7) if it starts with 00110
        public static uint GetSignBitsIfValid(uint ch) => (uint)((ch & 0b_1111_1000) == 0b_0011_0000 ? 0 : -1);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryParseWholeBlocks(ReadOnlySpan<T> input, Span<uint> destination)
        {
            if (typeof(T) == typeof(char))
            {
                if (!HexConverter.TryDecode(SpanCast<T, char>(input), MemoryMarshal.AsBytes(destination), out _))
                    return false;
            }
            else if (typeof(T) == typeof(byte))
            {
                if (!HexConverter.TryDecode(SpanCast<T, byte>(input), MemoryMarshal.AsBytes(destination), out _))
                    return false;
            }
            else return false;

            if (BitConverter.IsLittleEndian)
            {
                MemoryMarshal.AsBytes(destination).Reverse();
            }
            else
            {
                destination.Reverse();
            }

            return true;
        }
    }
#if NET8_0_OR_GREATER
    internal readonly struct BigIntegerBinaryParser<T> : IBigIntegerHexOrBinaryParser<BigIntegerBinaryParser<T>, T>
        where T : unmanaged, IBinaryInteger<T>
    {
        public static int BitsPerDigit => 1;

        public static NumberStyles BlockNumberStyle => NumberStyles.AllowBinarySpecifier;

        // Taking the LSB is enough for distinguishing 0/1
        public static uint GetSignBitsIfValid(uint ch) => (uint)(((int)ch << 31) >> 31);
    }
#endif
}

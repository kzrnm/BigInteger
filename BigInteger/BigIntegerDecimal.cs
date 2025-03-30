using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kzrnm.Numerics
{
    using Decimal;
    using System.Buffers.Text;

    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct BigIntegerDecimal
        : IComparable,
#if NET7_0_OR_GREATER
          ISpanFormattable,
          INumber<BigIntegerDecimal>,
          ISignedNumber<BigIntegerDecimal>,
#endif
          IComparable<BigIntegerDecimal>,
          IEquatable<BigIntegerDecimal>
    {
        /*
         * Original is System.Numerics.BigInteger
         *
         * Copyright (c) .NET Foundation and Contributors
         * Released under the MIT license
         * https://github.com/dotnet/runtime/blob/master/LICENSE.TXT
         */
        internal const string LISENCE = @"
Original is System.Numerics.BigInteger

Copyright (c) .NET Foundation and Contributors
Released under the MIT license
https://github.com/dotnet/runtime/blob/master/LICENSE.TXT
";

        internal readonly int _sign; // Do not rename (binary serialization)
        internal readonly uint[]? _dig; // Do not rename (binary serialization)

        // We have to make a choice of how to represent int.MinValue. This is the one
        // value that fits in an int, but whose negation does not fit in an int.
        // We choose to use a large representation, so we're symmetric with respect to negation.
        private static readonly BigIntegerDecimal s_bnMinInt = new BigIntegerDecimal(-1, new uint[] { 147483648, 2 });
        private static readonly BigIntegerDecimal s_bnOneInt = new BigIntegerDecimal(1);
        private static readonly BigIntegerDecimal s_bnZeroInt = new BigIntegerDecimal(0);
        private static readonly BigIntegerDecimal s_bnMinusOneInt = new BigIntegerDecimal(-1);

        public BigIntegerDecimal(long value) : this(NumericsHelpers.Abs(value))
        {

            _sign = _dig != null
                ? Math.Sign(value)
                : value < 0
                   ? -_sign
                   : _sign;

            AssertValid();
        }

        public BigIntegerDecimal(ulong value)
        {
            const ulong B = BigIntegerCalculator.Base;
            const ulong B2 = B * B;
            if (value < B)
            {
                _sign = (int)value;
            }
            else if (value < B2)
            {
                _sign = 1;
#if NET7_0_OR_GREATER
                var (q, r1) = Math.DivRem(value, B);
#else
                var q = value / B;
                var r1 = value - q * B;
#endif
                _dig = new uint[] { (uint)r1, (uint)q };
            }
            else
            {
                _sign = 1;
#if NET7_0_OR_GREATER
                var (q, r1) = Math.DivRem(value, B);
                (q, var r2) = Math.DivRem(q, B);
#else
                var q = value / B;
                var r1 = value - q * B;
                value = q;
                q = value / B;
                var r2 = value - q * B;
#endif
                _dig = new uint[] { (uint)r1, (uint)r2, (uint)q };
            }

            AssertValid();
        }

        internal BigIntegerDecimal(int n, uint[]? rgu)
        {
            if ((rgu is not null) && (rgu.Length > MaxLength))
            {
                ThrowHelper.ThrowOverflowException();
            }

            _sign = n;
            _dig = rgu;

            AssertValid();
        }

        /// <summary>
        /// Constructor used during bit manipulation and arithmetic.
        /// When possible the value will be packed into  _sign to conserve space.
        /// </summary>
        /// <param name="value">The absolute value of the number</param>
        /// <param name="negative">The bool indicating the sign of the value.</param>
        private BigIntegerDecimal(ReadOnlySpan<uint> value, bool negative)
        {
            if (value.Length > MaxLength)
            {
                ThrowHelper.ThrowOverflowException();
            }

            int len;

            // Try to conserve space as much as possible by checking for wasted leading span entries
            // sometimes the span has leading zeros from bit manipulation operations & and ^
            for (len = value.Length; len > 0 && value[len - 1] == 0; len--) ;

#if NET8_0_OR_GREATER
            Debug.Assert(!value.ContainsAnyInRange((uint)BigIntegerCalculator.Base, uint.MaxValue));
#endif

            if (len == 0)
            {
                this = s_bnZeroInt;
            }
            else if (len == 1 && value[0] <= int.MaxValue)
            {
                _sign = negative ? -(int)value[0] : (int)value[0];
                _dig = null;
            }
            else
            {
                _sign = negative ? -1 : +1;
                _dig = value.Slice(0, len).ToArray();
            }
            AssertValid();
        }

        public static BigIntegerDecimal Zero { get { return s_bnZeroInt; } }

        public static BigIntegerDecimal One { get { return s_bnOneInt; } }

        public static BigIntegerDecimal MinusOne { get { return s_bnMinusOneInt; } }

        internal static int MaxLength =>
#if NET7_0_OR_GREATER
            Array.MaxLength
#else
            0X7FFFFFC7
#endif
            / sizeof(uint);

        public bool IsZero { get { AssertValid(); return _sign == 0; } }

        public bool IsOne { get { AssertValid(); return _sign == 1 && _dig == null; } }

        public bool IsEven { get { AssertValid(); return _dig == null ? (_sign & 1) == 0 : (_dig[0] & 1) == 0; } }

        public int Sign
        {
            get { AssertValid(); return Math.Sign(_sign); }
        }

        public static BigIntegerDecimal Parse(string value)
        {
            return Parse(value, NumberStyles.Integer);
        }

        public static BigIntegerDecimal Parse(string value, NumberStyles style)
        {
            return Parse(value, style, NumberFormatInfo.CurrentInfo);
        }

        public static BigIntegerDecimal Parse(string value, IFormatProvider? provider)
        {
            return Parse(value, NumberStyles.Integer, NumberFormatInfo.GetInstance(provider));
        }

        public static BigIntegerDecimal Parse(string value, NumberStyles style, IFormatProvider? provider)
        {
#if NET7_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(value);
#else
            ThrowHelper.ThrowIfNull(value);
#endif
            return Parse(value.AsSpan(), style, NumberFormatInfo.GetInstance(provider));
        }

        public static bool TryParse([NotNullWhen(true)] string? value, out BigIntegerDecimal result)
        {
            return TryParse(value, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse([NotNullWhen(true)] string? value, NumberStyles style, IFormatProvider? provider, out BigIntegerDecimal result)
        {
            return TryParse(value.AsSpan(), style, NumberFormatInfo.GetInstance(provider), out result);
        }

        public static BigIntegerDecimal Parse(ReadOnlySpan<char> value, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            if (!TryParse(value, style, provider, out BigIntegerDecimal result))
                throw new FormatException();
            return result;
        }

        public static bool TryParse(ReadOnlySpan<char> value, out BigIntegerDecimal result)
        {
            return TryParse(value, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out BigIntegerDecimal result)
        {
            ValidateParseStyleInteger(style);

            return TryParseBigIntegerNumber(value, style, NumberFormatInfo.GetInstance(provider), out result) == ParsingStatus.OK;
        }


        public static BigIntegerDecimal Parse(ReadOnlySpan<byte> utf8, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            if (!TryParse(utf8, style, provider, out BigIntegerDecimal result))
                throw new FormatException();
            return result;
        }
        public static bool TryParse(ReadOnlySpan<byte> utf8, out BigIntegerDecimal result)
        {
            return TryParse(utf8, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse(ReadOnlySpan<byte> utf8, NumberStyles style, IFormatProvider? provider, out BigIntegerDecimal result)
        {
            ValidateParseStyleInteger(style);

            return TryParseBigIntegerNumber(utf8, style, NumberFormatInfo.GetInstance(provider), out result) == ParsingStatus.OK;
        }
        static ParsingStatus TryParseBigIntegerNumber<T>(ReadOnlySpan<T> value, NumberStyles style, NumberFormatInfo info, out BigIntegerDecimal result)
            where T : unmanaged, IEquatable<T>
        {
            scoped Span<byte> buffer;
            byte[]? arrayFromPool = null;

            if (value.Length == 0)
            {
                result = default;
                return ParsingStatus.Failed;
            }

            int bufferLength = value.Length + 1 + 10;
            buffer = bufferLength < 255
                ? stackalloc byte[bufferLength]
                : (arrayFromPool = ArrayPool<byte>.Shared.Rent(bufferLength));

            ParsingStatus ret;

            var number = new NumberBuffer(NumberBufferKind.Integer, buffer);

            if (!NumberBuffer.TryStringToNumber(value, style, ref number, info))
            {
                result = default;
                ret = ParsingStatus.Failed;
            }
            else
            {
                ret = NumberToBigIntegerDecimal(ref number, out result);
            }

            if (arrayFromPool != null)
                ArrayPool<byte>.Shared.Return(arrayFromPool);

            return ret;
        }

        static ParsingStatus NumberToBigIntegerDecimal(ref NumberBuffer number, out BigIntegerDecimal result)
        {
            const uint B = BigIntegerCalculator.Base;
            const int BaseLog = BigIntegerCalculator.BaseLog;
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

            int totalDigitCount = Math.Min(number.DigitsCount, number.Scale);
            int trailingZeroCount = number.Scale - totalDigitCount;
            {
                trailingZeroCount = Math.DivRem(trailingZeroCount, BaseLog, out int trailing);

                for (int i = 0; i < trailing; i++)
                    number.Digits[number.DigitsCount++] = (byte)'0';
                number.Digits[number.DigitsCount] = 0;
            }

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

            int base1E9Length = trailingZeroCount + (intDigits.Length + BaseLog - 1) / BaseLog;
            var base1E9 = new uint[base1E9Length];

            int di = base1E9Length;
            var leadingDigits = intDigits[..(intDigits.Length % BaseLog)];

            if (leadingDigits.Length != 0)
            {
#if NET8_0_OR_GREATER
                uint.TryParse(leadingDigits, out base1E9[--di]);
#else
                SR.UIntTryParse(leadingDigits, out base1E9[--di]);
#endif
                intDigits = intDigits.Slice(leadingDigits.Length);
            }

            for (--di; di >= trailingZeroCount; --di)
            {
#if NET8_0_OR_GREATER
                uint.TryParse(intDigits.Slice(0, BaseLog), out base1E9[di]);
#else
                SR.UIntTryParse(intDigits.Slice(0, BaseLog), out base1E9[di]);
#endif
                intDigits = intDigits.Slice(BaseLog);
            }
            Debug.Assert(intDigits.Length == 0);

            if (base1E9.Length == 0)
                result = default;
            else if (base1E9.Length == 1)
            {
                var v = base1E9[0];
                result = v < B
                    ? new BigIntegerDecimal((number.IsNegative ? -1 : 1) * (int)v, null)
                    : new BigIntegerDecimal(number.IsNegative ? -1 : 1, base1E9);
            }
            else
                result = new BigIntegerDecimal(number.IsNegative ? -1 : 1, base1E9);

            return ParsingStatus.OK;
        }
        static void ValidateParseStyleInteger(NumberStyles style)
        {
            // Check for undefined flags
            const NumberStyles InvalidNumberStyles = ~(NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite
                                                          | NumberStyles.AllowLeadingSign | NumberStyles.AllowTrailingSign
                                                          | NumberStyles.AllowParentheses | NumberStyles.AllowDecimalPoint
                                                          | NumberStyles.AllowThousands | NumberStyles.AllowExponent
                                                          | NumberStyles.AllowCurrencySymbol);

            if ((style & InvalidNumberStyles) != 0)
                Throw();
            void Throw()
                => throw new ArgumentException(SR.Argument_InvalidNumberStyles, nameof(style));
        }

        public static int Compare(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.CompareTo(right);
        }

        public static BigIntegerDecimal Abs(BigIntegerDecimal value)
        {
            return (value >= Zero) ? value : -value;
        }

        public static BigIntegerDecimal Add(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left + right;
        }

        public static BigIntegerDecimal Subtract(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left - right;
        }

        public static BigIntegerDecimal Multiply(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left * right;
        }

        public static BigIntegerDecimal Divide(BigIntegerDecimal dividend, BigIntegerDecimal divisor)
        {
            return dividend / divisor;
        }

        public static BigIntegerDecimal Remainder(BigIntegerDecimal dividend, BigIntegerDecimal divisor)
        {
            return dividend % divisor;
        }

        public static BigIntegerDecimal DivRem(BigIntegerDecimal dividend, BigIntegerDecimal divisor, out BigIntegerDecimal remainder)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._dig == null;
            bool trivialDivisor = divisor._dig == null;

            if (trivialDividend && trivialDivisor)
            {
                BigIntegerDecimal quotient;
                quotient = Math.DivRem(dividend._sign, divisor._sign, out var remainder32);
                remainder = remainder32;
                return quotient;
            }

            if (trivialDividend)
            {
                // The divisor is non-trivial
                // and therefore the bigger one
                remainder = dividend;
                return s_bnZeroInt;
            }

            Debug.Assert(dividend._dig != null);

            if (trivialDivisor)
            {
                uint rest;

                uint[]? digitsFromPool = null;
                int size = dividend._dig.Length;
                Span<uint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                    : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                try
                {
                    // may throw DivideByZeroException
                    BigIntegerCalculator.Divide(dividend._dig, NumericsHelpers.Abs(divisor._sign), quotient, out rest);

                    remainder = dividend._sign < 0 ? -rest : rest;
                    return new BigIntegerDecimal(quotient, (dividend._sign < 0) ^ (divisor._sign < 0));
                }
                finally
                {
                    if (digitsFromPool != null)
                        ArrayPool<uint>.Shared.Return(digitsFromPool);
                }
            }

            Debug.Assert(divisor._dig != null);

            if (dividend._dig.Length < divisor._dig.Length)
            {
                remainder = dividend;
                return s_bnZeroInt;
            }
            else
            {
                uint[]? remainderFromPool = null;
                int size = dividend._dig.Length;
                Span<uint> rest = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : remainderFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                uint[]? quotientFromPool = null;
                size = dividend._dig.Length - divisor._dig.Length + 1;
                Span<uint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Divide(dividend._dig, divisor._dig, quotient, rest);

                remainder = new BigIntegerDecimal(rest, dividend._sign < 0);
                var result = new BigIntegerDecimal(quotient, (dividend._sign < 0) ^ (divisor._sign < 0));

                if (remainderFromPool != null)
                    ArrayPool<uint>.Shared.Return(remainderFromPool);

                if (quotientFromPool != null)
                    ArrayPool<uint>.Shared.Return(quotientFromPool);

                return result;
            }
        }

        public static BigIntegerDecimal Negate(BigIntegerDecimal value)
        {
            return -value;
        }

#if false
        public static BigIntegerDecimal GreatestCommonDivisor(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            left.AssertValid();
            right.AssertValid();

            bool trivialLeft = left._dig == null;
            bool trivialRight = right._dig == null;

            if (trivialLeft && trivialRight)
            {
                return BigIntegerCalculator.Gcd(NumericsHelpers.Abs(left._sign), NumericsHelpers.Abs(right._sign));
            }

            if (trivialLeft)
            {
                Debug.Assert(right._dig != null);
                return left._sign != 0
                    ? BigIntegerCalculator.Gcd(right._dig, NumericsHelpers.Abs(left._sign))
                    : new BigIntegerDecimal(right._dig, negative: false);
            }

            if (trivialRight)
            {
                Debug.Assert(left._dig != null);
                return right._sign != 0
                    ? BigIntegerCalculator.Gcd(left._dig, NumericsHelpers.Abs(right._sign))
                    : new BigIntegerDecimal(left._dig, negative: false);
            }

            Debug.Assert(left._dig != null && right._dig != null);

            if (BigIntegerCalculator.Compare(left._dig, right._dig) < 0)
            {
                return GreatestCommonDivisor(right._dig, left._dig);
            }
            else
            {
                return GreatestCommonDivisor(left._dig, right._dig);
            }
        }

        private static BigIntegerDecimal GreatestCommonDivisor(ReadOnlySpan<ulong> leftBits, ReadOnlySpan<ulong> rightBits)
        {
            Debug.Assert(BigIntegerCalculator.Compare(leftBits, rightBits) >= 0);

            uint[]? digitsFromPool = null;
            BigIntegerDecimal result;

            // Short circuits to spare some allocations...
            if (rightBits.Length == 1)
            {
                ulong temp = BigIntegerCalculator.Remainder(leftBits, rightBits[0]);
                result = BigIntegerCalculator.Gcd(rightBits[0], temp);
            }
            else if (rightBits.Length == 2)
            {
                Span<ulong> bits = (leftBits.Length <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc ulong[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<ulong>.Shared.Rent(leftBits.Length)).Slice(0, leftBits.Length);

                BigIntegerCalculator.Remainder(leftBits, rightBits, bits);

                ulong left = ((ulong)rightBits[1] << 32) | rightBits[0];
                ulong right = ((ulong)bits[1] << 32) | bits[0];

                result = BigIntegerCalculator.Gcd(left, right);
            }
            else
            {
                Span<ulong> bits = (leftBits.Length <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc ulong[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<ulong>.Shared.Rent(leftBits.Length)).Slice(0, leftBits.Length);

                BigIntegerCalculator.Gcd(leftBits, rightBits, bits);
                result = new BigIntegerDecimal(bits, negative: false);
            }

            if (digitsFromPool != null)
                ArrayPool<ulong>.Shared.Return(digitsFromPool);

            return result;
        }
#endif

        public static BigIntegerDecimal Max(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            if (left.CompareTo(right) < 0)
                return right;
            return left;
        }

        public static BigIntegerDecimal Min(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            if (left.CompareTo(right) <= 0)
                return left;
            return right;
        }

#if false
        public static BigIntegerDecimal ModPow(BigIntegerDecimal value, BigIntegerDecimal exponent, BigIntegerDecimal modulus)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNegative(exponent.Sign);
#else
            static void ArgumentOutOfRangeExceptionThrowIfNegative(int v)
            {
                if (v < 0) throw new ArgumentOutOfRangeException();
            }
            ArgumentOutOfRangeExceptionThrowIfNegative(exponent.Sign);
#endif

            value.AssertValid();
            exponent.AssertValid();
            modulus.AssertValid();

            bool trivialValue = value._dig == null;
            bool trivialExponent = exponent._dig == null;
            bool trivialModulus = modulus._dig == null;

            BigIntegerDecimal result;

            if (trivialModulus)
            {
                uint bits = trivialValue && trivialExponent ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
                            trivialValue ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._dig!, NumericsHelpers.Abs(modulus._sign)) :
                            trivialExponent ? BigIntegerCalculator.Pow(value._dig!, NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
                            BigIntegerCalculator.Pow(value._dig!, exponent._dig!, NumericsHelpers.Abs(modulus._sign));

                result = value._sign < 0 && !exponent.IsEven ? -1 * bits : bits;
            }
            else
            {
                int size = (modulus._dig?.Length ?? 1) << 1;
                uint[]? digitsFromPool = null;
                Span<ulong> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc ulong[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<ulong>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();
                if (trivialValue)
                {
                    if (trivialExponent)
                    {
                        BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), modulus._dig!, bits);
                    }
                    else
                    {
                        BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._dig!, modulus._dig!, bits);
                    }
                }
                else if (trivialExponent)
                {
                    BigIntegerCalculator.Pow(value._dig!, NumericsHelpers.Abs(exponent._sign), modulus._dig!, bits);
                }
                else
                {
                    BigIntegerCalculator.Pow(value._dig!, exponent._dig!, modulus._dig!, bits);
                }

                result = new BigIntegerDecimal(bits, value._sign < 0 && !exponent.IsEven);

                if (digitsFromPool != null)
                    ArrayPool<ulong>.Shared.Return(digitsFromPool);
            }

            return result;
        }

        public static BigIntegerDecimal Pow(BigIntegerDecimal value, int exponent)
        {
#if NET8_0_OR_GREATER
            ThrowHelper.ThrowIfNegative(exponent);
#else
            static void ArgumentOutOfRangeExceptionThrowIfNegative(int v)
            {
                if (v < 0) throw new ArgumentOutOfRangeException();
            }
            ArgumentOutOfRangeExceptionThrowIfNegative(exponent);
#endif

            value.AssertValid();

            if (exponent == 0)
                return s_bnOneInt;
            if (exponent == 1)
                return value;

            bool trivialValue = value._dig == null;

            uint power = (uint)NumericsHelpers.Abs(exponent);
            uint[]? digitsFromPool = null;
            BigIntegerDecimal result;

            if (trivialValue)
            {
                if (value._sign == 1)
                    return value;
                if (value._sign == -1)
                    return (exponent & 1) != 0 ? value : s_bnOneInt;
                if (value._sign == 0)
                    return value;

                int size = BigIntegerCalculator.PowBound(power, 1);
                Span<ulong> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc ulong[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<ulong>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), power, bits);
                result = new BigIntegerDecimal(bits, value._sign < 0 && (exponent & 1) != 0);
            }
            else
            {
                int size = BigIntegerCalculator.PowBound(power, value._dig!.Length);
                Span<ulong> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc ulong[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<ulong>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Pow(value._dig, power, bits);
                result = new BigIntegerDecimal(bits, value._sign < 0 && (exponent & 1) != 0);
            }

            if (digitsFromPool != null)
                ArrayPool<ulong>.Shared.Return(digitsFromPool);

            return result;
        }
#endif

        public override int GetHashCode()
        {
            AssertValid();

            if (_dig is null)
                return _sign;

            HashCode hash = default;
#if NET7_0_OR_GREATER
            hash.AddBytes(MemoryMarshal.AsBytes(_dig.AsSpan()));
#else
            foreach (var v in _dig.AsSpan())
                hash.Add(v);
#endif
            hash.Add(_sign);
            return hash.ToHashCode();
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            AssertValid();

            return obj is BigIntegerDecimal other && Equals(other);
        }

        public bool Equals(BigIntegerDecimal other)
        {
            AssertValid();
            other.AssertValid();

            return _sign == other._sign && _dig.AsSpan().SequenceEqual(other._dig);
        }

        public int CompareTo(BigIntegerDecimal other)
        {
            AssertValid();
            other.AssertValid();

            if ((_sign ^ other._sign) < 0)
            {
                // Different signs, so the comparison is easy.
                return _sign < 0 ? -1 : +1;
            }

            // Same signs
            if (_dig == null)
            {
                if (other._dig == null)
                    return _sign < other._sign ? -1 : _sign > other._sign ? +1 : 0;
                return Math.Sign(-other._sign);
            }
            if (other._dig == null)
                return Math.Sign(_sign);

            int bitsResult = BigIntegerCalculator.Compare(_dig, other._dig);
            return _sign < 0 ? -bitsResult : bitsResult;
        }

        public int CompareTo(object? obj)
        {
            if (obj == null)
                return 1;
            if (obj is BigIntegerDecimal bigInt)
                return CompareTo(bigInt);
            throw new ArgumentException(SR.Argument_MustBeBigInt, nameof(obj));
        }

        public override string ToString()
        {
            const int BaseLog = BigIntegerCalculator.BaseLog;
            if (_dig == null)
            {
                return _sign.ToString();
            }
#if NET9_0_OR_GREATER
            var length = BaseLog * (_dig.Length - 1) + CountDigits(_dig[^1]);
            if (_sign < 0)
                ++length;

            return string.Create(length, this, (chars, bd) => bd.TryFormat(chars, out _));
#else
            var length = BaseLog * (_dig.Length - 1) + CountDigits(_dig[^1]);
            if (_sign < 0)
                ++length;
            char[]? chars = null;
            var sb = (length <= 512
                    ? stackalloc char[512]
                    : (chars = ArrayPool<char>.Shared.Rent(length))).Slice(0, length);
            if (!TryFormat(sb, out int charsWritten))
                throw new FormatException();

            Debug.Assert(charsWritten == length);
            return sb[..charsWritten].ToString();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int CountDigits(uint value)
        {
            long tableValue = BitOperations.Log2(value) switch
            {
                0 => 4294967296,
                1 => 8589934582,
                2 => 8589934582,
                3 => 8589934582,
                4 => 12884901788,
                5 => 12884901788,
                6 => 12884901788,
                7 => 17179868184,
                8 => 17179868184,
                9 => 17179868184,
                10 => 21474826480,
                11 => 21474826480,
                12 => 21474826480,
                13 => 21474826480,
                14 => 25769703776,
                15 => 25769703776,
                16 => 25769703776,
                17 => 30063771072,
                18 => 30063771072,
                19 => 30063771072,
                20 => 34349738368,
                21 => 34349738368,
                22 => 34349738368,
                23 => 34349738368,
                24 => 38554705664,
                25 => 38554705664,
                26 => 38554705664,
                _ => 41949672960,
            };
            return (int)((value + tableValue) >> 32);
        }

        public string ToString(IFormatProvider? provider)
            => ToString();

        public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format)
            => ToString();

        public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? provider)
            => ToString();

#if Embedding
        [SourceExpander.NotEmbeddingSource]
#endif
        private string DebuggerDisplay
        {
            get
            {
                // For very big numbers, ToString can be too long or even timeout for Visual Studio to display
                // Display a fast estimated value instead

                // Use ToString for small values

                if ((_dig is null) || (_dig.Length <= 2))
                {
                    return ToString();
                }


                int exponent = BigIntegerCalculator.BaseLog * (_dig.Length - 1);

                string upper = _dig[^1].ToString();
                exponent += upper.Length - 1;

                string signStr = _sign < 0 ? NumberFormatInfo.CurrentInfo.NegativeSign : "";

                // Use about a half of the precision of double
                return $"{signStr}{upper[0]}.{(upper.Length >= 2 ? upper[1..] : "0")}e+{exponent}";
            }
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => TryFormatCore(destination, out charsWritten, format, provider);

        public bool TryFormat(Span<byte> utf8Destination, out int bytesWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            => TryFormatCore(utf8Destination, out bytesWritten, format, provider);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryFormatSmall<T>(int sign, Span<T> destination, out int charsWritten)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
                return sign.TryFormat(MemoryMarshal.AsBytes(destination), out charsWritten);
            if (typeof(T) == typeof(char))
                return sign.TryFormat(MemoryMarshal.Cast<T, char>(destination), out charsWritten);

            charsWritten = 0;
            return false;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryFormatSmallD9<T>(uint sign, Span<T> destination, out int charsWritten)
            where T : unmanaged
        {
            if (typeof(T) == typeof(byte))
                return Utf8Formatter.TryFormat(sign, MemoryMarshal.AsBytes(destination), out charsWritten, new StandardFormat('D', 9));
            if (typeof(T) == typeof(char))
                return sign.TryFormat(MemoryMarshal.Cast<T, char>(destination), out charsWritten, "D9");

            charsWritten = 0;
            return false;
        }
        bool TryFormatCore<T>(Span<T> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
            where T : unmanaged
        {
            const int BaseLog = BigIntegerCalculator.BaseLog;
            if (_dig == null)
            {
                return TryFormatSmall(_sign, destination, out charsWritten);
            }
            Span<T> firstBuffer = stackalloc T[10];
            if (!TryFormatSmall((int)_dig[^1], firstBuffer, out var first))
            {
                charsWritten = 0;
                return false;
            }
            firstBuffer = firstBuffer[..first];
            var digits = _dig.AsSpan(0, _dig.Length - 1);
            charsWritten = first + digits.Length * BaseLog + (_sign < 0 ? 1 : 0);
            if (destination.Length < charsWritten)
            {
                charsWritten = 0;
                return false;
            }
            destination = destination[..charsWritten];

            if (_sign < 0)
            {
                destination[0] = SR.CastFrom<T>('-');
                destination = destination[1..];
            }

            firstBuffer.CopyTo(destination);
            destination = destination[first..];

            for (int i = digits.Length - 1; i >= 0; i--)
            {
                TryFormatSmallD9(digits[i], destination, out _);
                destination = destination[BaseLog..];
            }

            return true;
        }

        private static BigIntegerDecimal Add(ReadOnlySpan<uint> leftBits, int leftSign, ReadOnlySpan<uint> rightBits, int rightSign)
        {
            bool trivialLeft = leftBits.IsEmpty;
            bool trivialRight = rightBits.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerDecimal result;
            uint[]? digitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!rightBits.IsEmpty);

                int size = rightBits.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(rightBits, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }
            else if (trivialRight)
            {
                Debug.Assert(!leftBits.IsEmpty);

                int size = leftBits.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(leftBits, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }
            else if (leftBits.Length < rightBits.Length)
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = rightBits.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(rightBits, leftBits, bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }
            else
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = leftBits.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(leftBits, rightBits, bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }

            if (digitsFromPool != null)
                ArrayPool<uint>.Shared.Return(digitsFromPool);

            return result;
        }

        public static BigIntegerDecimal operator -(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._dig == null && right._dig == null)
                return (long)left._sign - right._sign;

            if (left._sign < 0 != right._sign < 0)
                return Add(left._dig, left._sign, right._dig, -1 * right._sign);
            return Subtract(left._dig, left._sign, right._dig, right._sign);
        }

        private static BigIntegerDecimal Subtract(ReadOnlySpan<uint> leftBits, int leftSign, ReadOnlySpan<uint> rightBits, int rightSign)
        {
            bool trivialLeft = leftBits.IsEmpty;
            bool trivialRight = rightBits.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerDecimal result;
            uint[]? digitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!rightBits.IsEmpty);

                int size = rightBits.Length;
                Span<uint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(rightBits, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerDecimal(bits, leftSign >= 0);
            }
            else if (trivialRight)
            {
                Debug.Assert(!leftBits.IsEmpty);

                int size = leftBits.Length;
                Span<uint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(leftBits, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }
            else if (BigIntegerCalculator.Compare(leftBits, rightBits) < 0)
            {
                int size = rightBits.Length;
                Span<uint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(rightBits, leftBits, bits);
                result = new BigIntegerDecimal(bits, leftSign >= 0);
            }
            else
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = leftBits.Length;
                Span<uint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(leftBits, rightBits, bits);
                result = new BigIntegerDecimal(bits, leftSign < 0);
            }

            if (digitsFromPool != null)
                ArrayPool<uint>.Shared.Return(digitsFromPool);

            return result;
        }

        //
        // Explicit Conversions From BigInteger
        //

        public static explicit operator byte(BigIntegerDecimal value)
        {
            return checked((byte)((int)value));
        }

        /// <summary>Explicitly converts a big integer to a <see cref="char" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="char" /> value.</returns>
        public static explicit operator char(BigIntegerDecimal value)
        {
            return checked((char)((int)value));
        }

        public static explicit operator short(BigIntegerDecimal value)
        {
            return checked((short)((int)value));
        }

        public static explicit operator int(BigIntegerDecimal value)
        {
            value.AssertValid();
            checked
            {
                if (value._dig == null)
                {
                    return (int)value._sign;
                }
                if (value._dig.Length > 1)
                {
                    // More than 32 bits
                    throw new OverflowException(SR.Overflow_Int32);
                }
            }
            return unchecked(-(int)value._dig[0]);
        }

        public static explicit operator long(BigIntegerDecimal value)
        {
            value.AssertValid();
            if (value._dig == null)
            {
                return value._sign;
            }


            ulong uu = 0;
            checked
            {
                switch (value._dig.Length)
                {
                    case 2:
                        uu += value._dig[1];
                        uu *= BigIntegerCalculator.Base;
                        goto case 1;
                    case 1:
                        uu += value._dig[0];
                        goto case 0;
                    case 0:
                        break;
                    default:
                        throw new OverflowException(SR.Overflow_Int64);
                }
            }


            long ll = value._sign > 0 ? unchecked((long)uu) : unchecked(-(long)uu);
            if ((ll > 0 && value._sign > 0) || (ll < 0 && value._sign < 0))
            {
                // Signs match, no overflow
                return ll;
            }
            throw new OverflowException(SR.Overflow_Int64);
        }

#if NET7_0_OR_GREATER
        /// <summary>Explicitly converts a big integer to a <see cref="Int128" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="Int128" /> value.</returns>
        public static explicit operator Int128(BigIntegerDecimal value)
        {
            value.AssertValid();

            if (value._dig is null)
            {
                return value._sign;
            }

            UInt128 uu = 0;
            checked
            {
                switch (value._dig.Length)
                {
                    case 3:
                        uu = value._dig[2];
                        uu *= BigIntegerCalculator.Base;
                        goto case 2;
                    case 2:
                        uu += value._dig[1];
                        uu *= BigIntegerCalculator.Base;
                        goto case 1;
                    case 1:
                        uu += value._dig[0];
                        goto case 0;
                    case 0:
                        break;
                    default:
                        throw new OverflowException(SR.Overflow_Int64);
                }
            }


            Int128 ll = value._sign > 0 ? unchecked((Int128)uu) : unchecked(-(Int128)uu);
            if ((ll > 0 && value._sign > 0) || (ll < 0 && value._sign < 0))
            {
                // Signs match, no overflow
                return ll;
            }
            throw new OverflowException(SR.Overflow_Int64);
        }
#endif

        public static explicit operator sbyte(BigIntegerDecimal value)
        {
            return checked((sbyte)((int)value));
        }


        public static explicit operator ushort(BigIntegerDecimal value)
        {
            return checked((ushort)((int)value));
        }

        public static explicit operator uint(BigIntegerDecimal value)
        {
            value.AssertValid();
            if (value._dig == null)
            {
                return checked((uint)value._sign);
            }
            else if (value._dig.Length > 1 || value._sign < 0)
            {
                throw new OverflowException(SR.Overflow_UInt32);
            }
            else
            {
                return checked((uint)value._dig[0]);
            }
        }

        public static explicit operator ulong(BigIntegerDecimal value)
        {
            value.AssertValid();
            if (value._dig == null)
            {
                return checked((ulong)value._sign);
            }

            ulong uu = 0;
            checked
            {
                switch (value._dig.Length)
                {
                    case 2:
                        uu += value._dig[1];
                        uu *= BigIntegerCalculator.Base;
                        goto case 1;
                    case 1:
                        uu += value._dig[0];
                        goto case 0;
                    case 0:
                        break;
                    default:
                        throw new OverflowException(SR.Overflow_Int64);
                }
            }

            return uu;
        }

#if NET7_0_OR_GREATER
        /// <summary>Explicitly converts a big integer to a <see cref="UInt128" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="UInt128" /> value.</returns>
        public static explicit operator UInt128(BigIntegerDecimal value)
        {
            value.AssertValid();

            if (value._dig is null)
            {
                return checked((UInt128)value._sign);
            }

            UInt128 uu = 0;
            checked
            {
                switch (value._dig.Length)
                {
                    case 3:
                        uu = value._dig[2];
                        uu *= BigIntegerCalculator.Base;
                        goto case 2;
                    case 2:
                        uu += value._dig[1];
                        uu *= BigIntegerCalculator.Base;
                        goto case 1;
                    case 1:
                        uu += value._dig[0];
                        goto case 0;
                    case 0:
                        break;
                    default:
                        throw new OverflowException(SR.Overflow_Int64);
                }
            }
            return uu;
        }
#endif


        //
        // Implicit Conversions To BigInteger
        //

        public static implicit operator BigIntegerDecimal(byte value)
        {
            return new BigIntegerDecimal(value);
        }

        /// <summary>Implicitly converts a <see cref="char" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerDecimal(char value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(short value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(int value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(long value)
        {
            return new BigIntegerDecimal(value);
        }

#if NET7_0_OR_GREATER
        /// <summary>Implicitly converts a <see cref="Int128" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerDecimal(Int128 value)
        {
            if (Int128.IsNegative(value))
            {
                if (value == Int128.MinValue)
                    return -(BigIntegerDecimal)((UInt128)Int128.MaxValue + 1);
                return -(BigIntegerDecimal)(UInt128)(-Int128.Abs(value));
            }
            return (BigIntegerDecimal)(UInt128)value;
        }
#endif

        public static implicit operator BigIntegerDecimal(sbyte value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(ushort value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(uint value)
        {
            return new BigIntegerDecimal(value);
        }

        public static implicit operator BigIntegerDecimal(ulong value)
        {
            return new BigIntegerDecimal(value);
        }

#if NET7_0_OR_GREATER
        /// <summary>Implicitly converts a <see cref="UInt128" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerDecimal(UInt128 value)
        {
            UInt128 B = BigIntegerCalculator.Base;

            (value, var rem0) = UInt128.DivRem(value, B);
            (value, var rem1) = UInt128.DivRem(value, B);
            var rem2 = (uint)value;

            return new BigIntegerDecimal(stackalloc uint[3] { (uint)rem0, (uint)rem1, rem2 }, false);
        }
#endif

        public static BigIntegerDecimal operator -(BigIntegerDecimal value)
        {
            value.AssertValid();
            return new BigIntegerDecimal(-value._sign, value._dig);
        }

        public static BigIntegerDecimal operator +(BigIntegerDecimal value)
        {
            value.AssertValid();
            return value;
        }

        public static BigIntegerDecimal operator ++(BigIntegerDecimal value)
        {
            return value + One;
        }

        public static BigIntegerDecimal operator --(BigIntegerDecimal value)
        {
            return value - One;
        }

        public static BigIntegerDecimal operator +(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._dig == null && right._dig == null)
                return (long)left._sign + right._sign;

            if (left._sign < 0 != right._sign < 0)
                return Subtract(left._dig, left._sign, right._dig, -1 * right._sign);
            return Add(left._dig, left._sign, right._dig, right._sign);
        }

        public static BigIntegerDecimal operator *(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._dig == null && right._dig == null)
                return (long)left._sign * right._sign;

            return Multiply(left._dig, left._sign, right._dig, right._sign);
        }

        private static BigIntegerDecimal Multiply(ReadOnlySpan<uint> left, int leftSign, ReadOnlySpan<uint> right, int rightSign)
        {
            bool trivialLeft = left.IsEmpty;
            bool trivialRight = right.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerDecimal result;
            uint[]? digitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!right.IsEmpty);

                int size = right.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Multiply(right, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerDecimal(bits, (leftSign < 0) ^ (rightSign < 0));
            }
            else if (trivialRight)
            {
                Debug.Assert(!left.IsEmpty);

                int size = left.Length + 1;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Multiply(left, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerDecimal(bits, (leftSign < 0) ^ (rightSign < 0));
            }
            else if (left == right)
            {
                int size = left.Length + right.Length;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Square(left, bits);
                result = new BigIntegerDecimal(bits, (leftSign < 0) ^ (rightSign < 0));
            }
            else
            {
                Debug.Assert(!left.IsEmpty && !right.IsEmpty);

                int size = left.Length + right.Length;
                Span<uint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Multiply(left, right, bits);
                result = new BigIntegerDecimal(bits, (leftSign < 0) ^ (rightSign < 0));
            }

            if (digitsFromPool != null)
                ArrayPool<uint>.Shared.Return(digitsFromPool);

            return result;
        }

        public static BigIntegerDecimal operator /(BigIntegerDecimal dividend, BigIntegerDecimal divisor)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._dig == null;
            bool trivialDivisor = divisor._dig == null;

            if (trivialDividend && trivialDivisor)
            {
                return dividend._sign / divisor._sign;
            }

            if (trivialDividend)
            {
                // The divisor is non-trivial
                // and therefore the bigger one
                return s_bnZeroInt;
            }

            uint[]? quotientFromPool = null;

            if (trivialDivisor)
            {
                Debug.Assert(dividend._dig != null);

                int size = dividend._dig.Length;
                Span<uint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                try
                {
                    //may throw DivideByZeroException
                    BigIntegerCalculator.Divide(dividend._dig, NumericsHelpers.Abs(divisor._sign), quotient);
                    return new BigIntegerDecimal(quotient, (dividend._sign < 0) ^ (divisor._sign < 0));
                }
                finally
                {
                    if (quotientFromPool != null)
                        ArrayPool<uint>.Shared.Return(quotientFromPool);
                }
            }

            Debug.Assert(dividend._dig != null && divisor._dig != null);

            if (dividend._dig.Length < divisor._dig.Length)
            {
                return s_bnZeroInt;
            }
            else
            {
                int size = dividend._dig.Length - divisor._dig.Length + 1;
                Span<uint> quotient = ((uint)size < BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Divide(dividend._dig, divisor._dig, quotient);
                var result = new BigIntegerDecimal(quotient, (dividend._sign < 0) ^ (divisor._sign < 0));

                if (quotientFromPool != null)
                    ArrayPool<uint>.Shared.Return(quotientFromPool);

                return result;
            }
        }

        public static BigIntegerDecimal operator %(BigIntegerDecimal dividend, BigIntegerDecimal divisor)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._dig == null;
            bool trivialDivisor = divisor._dig == null;

            if (trivialDividend && trivialDivisor)
            {
                return dividend._sign % divisor._sign;
            }

            if (trivialDividend)
            {
                // The divisor is non-trivial
                // and therefore the bigger one
                return dividend;
            }

            if (trivialDivisor)
            {
                Debug.Assert(dividend._dig != null);
                long remainder = (long)BigIntegerCalculator.Remainder(dividend._dig, NumericsHelpers.Abs(divisor._sign));
                return dividend._sign < 0 ? -1 * remainder : remainder;
            }

            Debug.Assert(dividend._dig != null && divisor._dig != null);

            if (dividend._dig.Length < divisor._dig.Length)
            {
                return dividend;
            }

            uint[]? digitsFromPool = null;
            int size = dividend._dig.Length;
            Span<uint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                            ? stackalloc uint[BigIntegerCalculator.StackAllocThreshold]
                            : digitsFromPool = ArrayPool<uint>.Shared.Rent(size)).Slice(0, size);

            BigIntegerCalculator.Remainder(dividend._dig, divisor._dig, bits);
            var result = new BigIntegerDecimal(bits, dividend._sign < 0);

            if (digitsFromPool != null)
                ArrayPool<uint>.Shared.Return(digitsFromPool);

            return result;
        }

        public static bool operator <(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.CompareTo(right) > 0;
        }
        public static bool operator >=(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(BigIntegerDecimal left, long right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerDecimal left, long right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerDecimal left, long right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BigIntegerDecimal left, long right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerDecimal left, long right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerDecimal left, long right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(long left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) > 0;
        }

        public static bool operator <=(long left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) >= 0;
        }

        public static bool operator >(long left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) < 0;
        }

        public static bool operator >=(long left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) <= 0;
        }

        public static bool operator ==(long left, BigIntegerDecimal right)
        {
            return right.Equals(left);
        }

        public static bool operator !=(long left, BigIntegerDecimal right)
        {
            return !right.Equals(left);
        }

        public static bool operator <(BigIntegerDecimal left, ulong right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerDecimal left, ulong right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerDecimal left, ulong right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BigIntegerDecimal left, ulong right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerDecimal left, ulong right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerDecimal left, ulong right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(ulong left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) > 0;
        }

        public static bool operator <=(ulong left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) >= 0;
        }

        public static bool operator >(ulong left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) < 0;
        }

        public static bool operator >=(ulong left, BigIntegerDecimal right)
        {
            return right.CompareTo(left) <= 0;
        }

        public static bool operator ==(ulong left, BigIntegerDecimal right)
        {
            return right.Equals(left);
        }

        public static bool operator !=(ulong left, BigIntegerDecimal right)
        {
            return !right.Equals(left);
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            const ulong B = BigIntegerCalculator.Base;
            if (_dig != null)
            {
                // _sign must be +1 or -1 when _bits is non-null
                Debug.Assert(_sign == 1 || _sign == -1);
                // _bits must contain at least 1 element or be null
                Debug.Assert(_dig.Length > 0);
                // Wasted space: leading zeros could have been truncated
                Debug.Assert(_dig[_dig.Length - 1] != 0);
                // Arrays larger than this can't fit into a Span<byte>
                Debug.Assert(_dig.Length <= MaxLength);

                foreach (var d in _dig)
                {
                    Debug.Assert(d < B);
                }
            }
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.DivRem(TSelf, TSelf)" />
        public static (BigIntegerDecimal Quotient, BigIntegerDecimal Remainder) DivRem(BigIntegerDecimal left, BigIntegerDecimal right)
        {
            BigIntegerDecimal quotient = DivRem(left, right, out BigIntegerDecimal remainder);
            return (quotient, remainder);
        }

#if NET7_0_OR_GREATER
        //
        // IAdditiveIdentity
        //

        /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity" />
        static BigIntegerDecimal IAdditiveIdentity<BigIntegerDecimal, BigIntegerDecimal>.AdditiveIdentity => Zero;
        //
        // IBinaryInteger
        //


        //
        // IMultiplicativeIdentity
        //

        /// <inheritdoc cref="IMultiplicativeIdentity{TSelf, TResult}.MultiplicativeIdentity" />
        static BigIntegerDecimal IMultiplicativeIdentity<BigIntegerDecimal, BigIntegerDecimal>.MultiplicativeIdentity => One;
        //
        // INumber
        //

        /// <inheritdoc cref="INumber{TSelf}.Clamp(TSelf, TSelf, TSelf)" />
        public static BigIntegerDecimal Clamp(BigIntegerDecimal value, BigIntegerDecimal min, BigIntegerDecimal max)
        {
            value.AssertValid();

            min.AssertValid();
            max.AssertValid();

            if (min > max)
            {
                ThrowMinMaxException(min, max);
            }

            if (value < min)
            {
                return min;
            }
            else if (value > max)
            {
                return max;
            }

            return value;

            [DoesNotReturn]
            static void ThrowMinMaxException<T>(T min, T max)
            {
                throw new ArgumentException(SR.Format(SR.Argument_MinMaxValue, min, max));
            }
        }

        /// <inheritdoc cref="INumber{TSelf}.CopySign(TSelf, TSelf)" />
        public static BigIntegerDecimal CopySign(BigIntegerDecimal value, BigIntegerDecimal sign)
        {
            value.AssertValid();
            sign.AssertValid();

            int currentSign = Math.Sign(value._sign);
            int targetSign = Math.Sign(sign._sign);

            return (currentSign == targetSign) ? value : -value;
        }

        /// <inheritdoc cref="INumber{TSelf}.MaxNumber(TSelf, TSelf)" />
        static BigIntegerDecimal INumber<BigIntegerDecimal>.MaxNumber(BigIntegerDecimal x, BigIntegerDecimal y) => Max(x, y);

        /// <inheritdoc cref="INumber{TSelf}.MinNumber(TSelf, TSelf)" />
        static BigIntegerDecimal INumber<BigIntegerDecimal>.MinNumber(BigIntegerDecimal x, BigIntegerDecimal y) => Min(x, y);

        /// <inheritdoc cref="INumber{TSelf}.Sign(TSelf)" />
        static int INumber<BigIntegerDecimal>.Sign(BigIntegerDecimal value)
        {
            value.AssertValid();
            return int.Sign(value._sign);
        }


        //
        // INumberBase
        //

        /// <inheritdoc cref="INumberBase{TSelf}.Radix" />
        static int INumberBase<BigIntegerDecimal>.Radix => 10;

        /// <inheritdoc cref="INumberBase{TSelf}.CreateChecked{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerDecimal CreateChecked<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerDecimal result;

            if (typeof(TOther) == typeof(BigIntegerDecimal))
            {
                result = (BigIntegerDecimal)(object)value;
            }
            else if (!TryConvertFromChecked(value, out result) && !TOther.TryConvertToChecked(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.CreateSaturating{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerDecimal CreateSaturating<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerDecimal result;

            if (typeof(TOther) == typeof(BigIntegerDecimal))
            {
                result = (BigIntegerDecimal)(object)value;
            }
            else if (!TryConvertFromSaturating(value, out result) && !TOther.TryConvertToSaturating(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerDecimal CreateTruncating<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerDecimal result;

            if (typeof(TOther) == typeof(BigIntegerDecimal))
            {
                result = (BigIntegerDecimal)(object)value;
            }
            else if (!TryConvertFromTruncating(value, out result) && !TOther.TryConvertToTruncating(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsCanonical(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsCanonical(BigIntegerDecimal value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsComplexNumber(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsComplexNumber(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsEvenInteger(TSelf)" />
        public static bool IsEvenInteger(BigIntegerDecimal value)
        {
            value.AssertValid();

            if (value._dig is null)
            {
                return (value._sign & 1) == 0;
            }
            return (value._dig[0] & 1) == 0;
        }


        /// <inheritdoc cref="INumberBase{TSelf}.IsFinite(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsFinite(BigIntegerDecimal value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsImaginaryNumber(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsImaginaryNumber(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsInfinity(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsInfinity(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsInteger(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsInteger(BigIntegerDecimal value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNaN(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsNaN(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNegative(TSelf)" />
        public static bool IsNegative(BigIntegerDecimal value)
        {
            value.AssertValid();
            return value._sign < 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsNegativeInfinity(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsNegativeInfinity(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNormal(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsNormal(BigIntegerDecimal value) => (value != 0);

        /// <inheritdoc cref="INumberBase{TSelf}.IsOddInteger(TSelf)" />
        public static bool IsOddInteger(BigIntegerDecimal value)
        {
            value.AssertValid();

            if (value._dig is null)
            {
                return (value._sign & 1) != 0;
            }
            return (value._dig[0] & 1) != 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsPositive(TSelf)" />
        public static bool IsPositive(BigIntegerDecimal value)
        {
            value.AssertValid();
            return value._sign >= 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsPositiveInfinity(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsPositiveInfinity(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsRealNumber(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsRealNumber(BigIntegerDecimal value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsSubnormal(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsSubnormal(BigIntegerDecimal value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsZero(TSelf)" />
        static bool INumberBase<BigIntegerDecimal>.IsZero(BigIntegerDecimal value)
        {
            value.AssertValid();
            return value._sign == 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.MaxMagnitude(TSelf, TSelf)" />
        public static BigIntegerDecimal MaxMagnitude(BigIntegerDecimal x, BigIntegerDecimal y)
        {
            x.AssertValid();
            y.AssertValid();

            BigIntegerDecimal ax = Abs(x);
            BigIntegerDecimal ay = Abs(y);

            if (ax > ay)
            {
                return x;
            }

            if (ax == ay)
            {
                return IsNegative(x) ? y : x;
            }

            return y;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.MaxMagnitudeNumber(TSelf, TSelf)" />
        static BigIntegerDecimal INumberBase<BigIntegerDecimal>.MaxMagnitudeNumber(BigIntegerDecimal x, BigIntegerDecimal y) => MaxMagnitude(x, y);

        /// <inheritdoc cref="INumberBase{TSelf}.MinMagnitude(TSelf, TSelf)" />
        public static BigIntegerDecimal MinMagnitude(BigIntegerDecimal x, BigIntegerDecimal y)
        {
            x.AssertValid();
            y.AssertValid();

            BigIntegerDecimal ax = Abs(x);
            BigIntegerDecimal ay = Abs(y);

            if (ax < ay)
            {
                return x;
            }

            if (ax == ay)
            {
                return IsNegative(x) ? x : y;
            }

            return y;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.MinMagnitudeNumber(TSelf, TSelf)" />
        static BigIntegerDecimal INumberBase<BigIntegerDecimal>.MinMagnitudeNumber(BigIntegerDecimal x, BigIntegerDecimal y) => MinMagnitude(x, y);

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromChecked{TOther}(TOther, out TSelf)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertFromChecked<TOther>(TOther value, out BigIntegerDecimal result) => TryConvertFromChecked(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromChecked<TOther>(TOther value, out BigIntegerDecimal result)
            where TOther : INumberBase<TOther>
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualValue = (byte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualValue = (char)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualValue = (short)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualValue = (int)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualValue = (long)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualValue = (Int128)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualValue = (ushort)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualValue = (uint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualValue = (ulong)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualValue = (UInt128)(object)value;
                result = actualValue;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromSaturating{TOther}(TOther, out TSelf)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertFromSaturating<TOther>(TOther value, out BigIntegerDecimal result) => TryConvertFromSaturating(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromSaturating<TOther>(TOther value, out BigIntegerDecimal result)
            where TOther : INumberBase<TOther>
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualValue = (byte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualValue = (char)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualValue = (short)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualValue = (int)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualValue = (long)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualValue = (Int128)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualValue = (ushort)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualValue = (uint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualValue = (ulong)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualValue = (UInt128)(object)value;
                result = actualValue;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromTruncating{TOther}(TOther, out TSelf)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertFromTruncating<TOther>(TOther value, out BigIntegerDecimal result) => TryConvertFromTruncating(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromTruncating<TOther>(TOther value, out BigIntegerDecimal result)
            where TOther : INumberBase<TOther>
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualValue = (byte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualValue = (char)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualValue = (short)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualValue = (int)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualValue = (long)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualValue = (Int128)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualValue = (ushort)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualValue = (uint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualValue = (ulong)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualValue = (UInt128)(object)value;
                result = actualValue;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToChecked{TOther}(TSelf, out TOther)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertToChecked<TOther>(BigIntegerDecimal value, [MaybeNullWhen(false)] out TOther result)
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualResult = checked((byte)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualResult = checked((char)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualResult = checked((short)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualResult = checked((int)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualResult = checked((long)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualResult = checked((Int128)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualResult = checked((sbyte)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualResult = checked((ushort)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualResult = checked((uint)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualResult = checked((ulong)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualResult = checked((UInt128)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToSaturating{TOther}(TSelf, out TOther)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertToSaturating<TOther>(BigIntegerDecimal value, [MaybeNullWhen(false)] out TOther result)
            => TryConvertToSaturatingImpl(value, out result);

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToTruncating{TOther}(TSelf, out TOther)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerDecimal>.TryConvertToTruncating<TOther>(BigIntegerDecimal value, [MaybeNullWhen(false)] out TOther result)
            => TryConvertToSaturatingImpl(value, out result);

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToSaturating{TOther}(TSelf, out TOther)" />
        [MethodImpl(256)]
        static bool TryConvertToSaturatingImpl<TOther>(BigIntegerDecimal value, [MaybeNullWhen(false)] out TOther result)
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? byte.MinValue : byte.MaxValue;
                }
                else
                {
                    actualResult = (value._sign >= byte.MaxValue) ? byte.MaxValue :
                                   (value._sign <= byte.MinValue) ? byte.MinValue : (byte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? char.MinValue : char.MaxValue;
                }
                else
                {
                    actualResult = (value._sign >= char.MaxValue) ? char.MaxValue :
                                   (value._sign <= char.MinValue) ? char.MinValue : (char)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? short.MinValue : short.MaxValue;
                }
                else
                {
                    actualResult = (value._sign >= short.MaxValue) ? short.MaxValue :
                                   (value._sign <= short.MinValue) ? short.MinValue : (short)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? int.MinValue : int.MaxValue;
                }
                else
                {
                    actualResult = value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualResult = (value >= long.MaxValue) ? long.MaxValue :
                                    (value <= long.MinValue) ? long.MinValue : (long)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualResult = (value >= Int128.MaxValue) ? Int128.MaxValue :
                                      (value <= Int128.MinValue) ? Int128.MinValue : (Int128)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? sbyte.MinValue : sbyte.MaxValue;
                }
                else
                {
                    actualResult = (value._sign >= sbyte.MaxValue) ? sbyte.MaxValue :
                                   (value._sign <= sbyte.MinValue) ? sbyte.MinValue : (sbyte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualResult;

                if (value._dig is not null)
                {
                    actualResult = IsNegative(value) ? ushort.MinValue : ushort.MaxValue;
                }
                else
                {
                    actualResult = (value._sign >= ushort.MaxValue) ? ushort.MaxValue :
                                   (value._sign <= ushort.MinValue) ? ushort.MinValue : (ushort)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualResult = (value >= uint.MaxValue) ? uint.MaxValue :
                                    IsNegative(value) ? uint.MinValue : (uint)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualResult = (value >= ulong.MaxValue) ? ulong.MaxValue :
                                     IsNegative(value) ? ulong.MinValue : (ulong)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualResult = (value >= UInt128.MaxValue) ? UInt128.MaxValue :
                                       IsNegative(value) ? UInt128.MinValue : (UInt128)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        //
        // ISignedNumber
        //

        /// <inheritdoc cref="ISignedNumber{TSelf}.NegativeOne" />
        static BigIntegerDecimal ISignedNumber<BigIntegerDecimal>.NegativeOne => MinusOne;
#endif
        //
        // IParsable
        //

        /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigIntegerDecimal result) => TryParse(s, NumberStyles.Integer, provider, out result);

        //
        // ISpanParsable
        //

        /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)" />
        public static BigIntegerDecimal Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, NumberStyles.Integer, provider);

        /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)" />
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigIntegerDecimal result) => TryParse(s, NumberStyles.Integer, provider, out result);


        public static BigIntegerDecimal Parse(ReadOnlySpan<byte> utf8, IFormatProvider? provider) => Parse(utf8, NumberStyles.Integer, provider);

        public static bool TryParse(ReadOnlySpan<byte> utf8, IFormatProvider? provider, out BigIntegerDecimal result) => TryParse(utf8, NumberStyles.Integer, provider, out result);
    }
}

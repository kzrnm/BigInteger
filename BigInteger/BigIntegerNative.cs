using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Kzrnm.Numerics.Experiment;

namespace Kzrnm.Numerics
{
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public struct BigIntegerNative
        : IComparable,
          IComparable<BigIntegerNative>,
          IEquatable<BigIntegerNative>,
          ISpanFormattable,
          IBinaryInteger<BigIntegerNative>,
          ISignedNumber<BigIntegerNative>
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

        internal const uint kuMaskHighBit = unchecked((uint)int.MinValue);

        // For values int.MinValue < n <= int.MaxValue, the value is stored in sign
        // and _bits is null. For all other values, sign is +1 or -1 and the bits are in _bits
        internal readonly int _sign; // Do not rename (binary serialization)
        internal readonly nuint[]? _bits; // Do not rename (binary serialization)

        internal const int kcbitUint = 32;
        internal const int kcbitUlong = 64;
        internal static int kcbitNUint => 8 * Unsafe.SizeOf<nuint>();
        internal static int kcByteNUint => Unsafe.SizeOf<nuint>();
        internal const int DecimalScaleFactorMask = 0x00FF0000;

        // We have to make a choice of how to represent int.MinValue. This is the one
        // value that fits in an int, but whose negation does not fit in an int.
        // We choose to use a large representation, so we're symmetric with respect to negation.
        private static readonly BigIntegerNative s_bnMinInt = new BigIntegerNative(-1, new nuint[] { kuMaskHighBit });
        private static readonly BigIntegerNative s_bnOneInt = new BigIntegerNative(1);
        private static readonly BigIntegerNative s_bnZeroInt = new BigIntegerNative(0);
        private static readonly BigIntegerNative s_bnMinusOneInt = new BigIntegerNative(-1);

        public BigIntegerNative(int value)
        {
            if (value == int.MinValue)
                this = s_bnMinInt;
            else
            {
                _sign = value;
                _bits = null;
            }
            AssertValid();
        }

        public BigIntegerNative(uint value)
        {
            if (value <= int.MaxValue)
            {
                _sign = (int)value;
                _bits = null;
            }
            else
            {
                _sign = +1;
                _bits = new nuint[1];
                _bits[0] = value;
            }
            AssertValid();
        }

        public BigIntegerNative(long value)
        {
            if (int.MinValue < value && value <= int.MaxValue)
            {
                _sign = (int)value;
                _bits = null;
            }
            else if (value == int.MinValue)
            {
                this = s_bnMinInt;
            }
            else
            {
                ulong x;
                if (value < 0)
                {
                    x = unchecked((ulong)-value);
                    _sign = -1;
                }
                else
                {
                    x = (ulong)value;
                    _sign = +1;
                }

                if (Environment.Is64BitProcess || x <= uint.MaxValue)
                {
                    _bits = new nuint[1];
                    _bits[0] = (nuint)x;
                }
                else
                {
                    _bits = new nuint[2];
                    _bits[0] = unchecked((uint)x);
                    _bits[1] = (uint)(x >> kcbitUint);
                }
            }

            AssertValid();
        }

        public BigIntegerNative(ulong value)
        {
            if (value <= int.MaxValue)
            {
                _sign = (int)value;
                _bits = null;
            }
            else if (Environment.Is64BitProcess || value <= uint.MaxValue)
            {
                _bits = new nuint[1];
                _bits[0] = (nuint)value;
            }
            else
            {
                _bits = new nuint[2];
                _bits[0] = unchecked((uint)value);
                _bits[1] = (uint)(value >> kcbitUint);
            }
            AssertValid();
        }

        public BigIntegerNative(float value) : this((double)value)
        {
        }

        public BigIntegerNative(double value)
        {
            if (!double.IsFinite(value))
            {
                if (double.IsInfinity(value))
                {
                    throw new OverflowException(SR.Overflow_BigIntInfinity);
                }
                else // NaN
                {
                    throw new OverflowException(SR.Overflow_NotANumber);
                }
            }

            _sign = 0;
            _bits = null;

            int sign, exp;
            ulong man;
            NumericsHelpers.GetDoubleParts(value, out sign, out exp, out man, out _);
            Debug.Assert(sign == +1 || sign == -1);

            if (man == 0)
            {
                this = Zero;
                return;
            }

            Debug.Assert(man < 1UL << 53);
            Debug.Assert(exp <= 0 || man >= 1UL << 52);

            if (exp <= 0)
            {
                if (exp <= -kcbitUlong)
                {
                    this = Zero;
                    return;
                }
                this = man >> -exp;
                if (sign < 0)
                    _sign = -_sign;
            }
            else if (exp <= 11)
            {
                this = man << exp;
                if (sign < 0)
                    _sign = -_sign;
            }
            else
            {
                // Overflow into at least 3 uints.
                // Move the leading 1 to the high bit.
                man <<= 11;
                exp -= 11;

                // Compute cu and cbit so that exp == 32 * cu - cbit and 0 <= cbit < 32.
                int cu = (exp - 1) / kcbitUint + 1;
                int cbit = cu * kcbitUint - exp;
                Debug.Assert(0 <= cbit && cbit < kcbitUint);
                Debug.Assert(cu >= 1);

                // Populate the uints.
                if (Environment.Is64BitProcess)
                {
                    _bits = new nuint[cu + 1];
                    _bits[cu] = unchecked((nuint)(man >> cbit));
                    if (cbit > 0)
                        _bits[cu - 1] = (nuint)(man << kcbitUlong - cbit);
                }
                else
                {
                    _bits = new nuint[cu + 2];
                    _bits[cu + 1] = (nuint)(man >> cbit + kcbitUint);
                    _bits[cu] = unchecked((nuint)(man >> cbit));
                    if (cbit > 0)
                        _bits[cu - 1] = unchecked((uint)man) << kcbitUint - cbit;
                }
                _sign = sign;
            }

            AssertValid();
        }

        public BigIntegerNative(decimal value)
        {
            // First truncate to get scale to 0 and extract bits
            Span<int> bits = stackalloc int[4];
            decimal.GetBits(decimal.Truncate(value), bits);

            Debug.Assert(bits.Length == 4 && (bits[3] & DecimalScaleFactorMask) == 0);

            int signMask = unchecked((int)kuMaskHighBit);
            int size = 3;
            while (size > 0 && bits[size - 1] == 0)
                size--;
            if (size == 0)
            {
                this = s_bnZeroInt;
            }
            else if (size == 1 && bits[0] > 0)
            {
                // bits[0] is the absolute value of this decimal
                // if bits[0] < 0 then it is too large to be packed into _sign
                _sign = bits[0];
                _sign *= (bits[3] & signMask) != 0 ? -1 : +1;
                _bits = null;
            }
            else
            {
                if (Environment.Is64BitProcess)
                {
                    _bits = new nuint[(size + 1) / 2];

                    unchecked
                    {
                        _bits[0] = (uint)bits[0];
                        if (size > 1)
                            _bits[0] |= (nuint)bits[1] << kcbitUint;
                        if (size > 2)
                            _bits[1] = (uint)bits[2];
                    }
                }
                else
                {
                    _bits = new nuint[size];

                    unchecked
                    {
                        _bits[0] = (nuint)bits[0];
                        if (size > 1)
                            _bits[1] = (nuint)bits[1];
                        if (size > 2)
                            _bits[2] = (nuint)bits[2];
                    }
                }

                _sign = (bits[3] & signMask) != 0 ? -1 : +1;
            }
            AssertValid();
        }

        /// <summary>
        /// Creates a  BigIntegerNative  from a little-endian twos-complement byte array.
        /// </summary>
        /// <param name="value"></param>
        public BigIntegerNative(byte[] value) :
            this(new ReadOnlySpan<byte>(value ?? throw new ArgumentNullException(nameof(value))))
        {
        }

        public BigIntegerNative(ReadOnlySpan<byte> value, bool isUnsigned = false, bool isBigEndian = false)
        {
            int byteCount = value.Length;

            bool isNegative;
            if (byteCount > 0)
            {
                byte mostSignificantByte = isBigEndian ? value[0] : value[byteCount - 1];
                isNegative = (mostSignificantByte & 0x80) != 0 && !isUnsigned;

                if (mostSignificantByte == 0)
                {
                    // Try to conserve space as much as possible by checking for wasted leading byte[] entries
                    if (isBigEndian)
                    {
                        int offset = 1;

                        while (offset < byteCount && value[offset] == 0)
                        {
                            offset++;
                        }

                        value = value.Slice(offset);
                        byteCount = value.Length;
                    }
                    else
                    {
                        byteCount -= 2;

                        while (byteCount >= 0 && value[byteCount] == 0)
                        {
                            byteCount--;
                        }

                        byteCount++;
                    }
                }
            }
            else
            {
                isNegative = false;
            }

            if (byteCount == 0)
            {
                // BigInteger.Zero
                _sign = 0;
                _bits = null;
                AssertValid();
                return;
            }

            if (byteCount <= 4)
            {
                _sign = isNegative ? unchecked((int)0xffffffff) : 0;

                if (isBigEndian)
                {
                    for (int i = 0; i < byteCount; i++)
                    {
                        _sign = _sign << 8 | value[i];
                    }
                }
                else
                {
                    for (int i = byteCount - 1; i >= 0; i--)
                    {
                        _sign = _sign << 8 | value[i];
                    }
                }

                _bits = null;
                if (_sign < 0 && !isNegative)
                {
                    // Int32 overflow
                    // Example: Int64 value 2362232011 (0xCB, 0xCC, 0xCC, 0x8C, 0x0)
                    // can be naively packed into 4 bytes (due to the leading 0x0)
                    // it overflows into the int32 sign bit
                    _bits = new nuint[1] { unchecked((uint)_sign) };
                    _sign = +1;
                }
                if (_sign == int.MinValue)
                {
                    this = s_bnMinInt;
                }
            }
            else
            {
                int wholeNUIntCount = Math.DivRem(byteCount, kcByteNUint, out int unalignedBytes);
                nuint[] val = new nuint[wholeNUIntCount + (unalignedBytes == 0 ? 0 : 1)];

                // Copy the bytes to the nuint array, apart from those which represent the
                // most significant uint if it's not a full four bytes.
                // The nuints are stored in 'least significant first' order.
                if (isBigEndian)
                {
                    // The bytes parameter is in big-endian byte order.
                    // We need to read the nuints out in reverse.

                    Span<byte> nuintBytes = MemoryMarshal.AsBytes(val.AsSpan(0, wholeNUIntCount));

                    // We need to slice off the remainder from the beginning.
                    value.Slice(unalignedBytes).CopyTo(nuintBytes);

                    nuintBytes.Reverse();
                }
                else
                {
                    // The bytes parameter is in little-endian byte order.
                    // We can just copy the bytes directly into the nuint array.

                    value.Slice(0, wholeNUIntCount * kcByteNUint).CopyTo(MemoryMarshal.AsBytes<nuint>(val));
                }

                // In both of the above cases on big-endian architecture, we need to perform
                // an endianness swap on the resulting uints.
                if (!BitConverter.IsLittleEndian)
                {
#if NET8_0_OR_GREATER
                    BinaryPrimitives.ReverseEndianness(val.AsSpan(0, wholeNUIntCount), val);
#else
                    foreach (ref var v in val.AsSpan(0, wholeNUIntCount))
                    {
                        if (Environment.Is64BitProcess)
                            v = (nuint)BinaryPrimitives.ReverseEndianness(v);
                        else
                            v = BinaryPrimitives.ReverseEndianness((uint)v);
                    }
#endif
                }

                // Copy the last uint specially if it's not aligned
                if (unalignedBytes != 0)
                {
                    if (isNegative)
                    {
                        val[wholeNUIntCount] = nuint.MaxValue;
                    }

                    if (isBigEndian)
                    {
                        for (int curByte = 0; curByte < unalignedBytes; curByte++)
                        {
                            byte curByteValue = value[curByte];
                            val[wholeNUIntCount] = val[wholeNUIntCount] << 8 | curByteValue;
                        }
                    }
                    else
                    {
                        for (int curByte = byteCount - 1; curByte >= byteCount - unalignedBytes; curByte--)
                        {
                            byte curByteValue = value[curByte];
                            val[wholeNUIntCount] = val[wholeNUIntCount] << 8 | curByteValue;
                        }
                    }
                }

                if (isNegative)
                {
                    NumericsHelpers.DangerousMakeTwosComplement(val); // Mutates val

                    // Pack _bits to remove any wasted space after the twos complement
                    int len = val.Length - 1;
                    while (len >= 0 && val[len] == 0) len--;
                    len++;

                    if (len == 1)
                    {
                        if (val[0] == 1)
                        {

                            this = s_bnMinusOneInt;
                            return;
                        }
                        else if (val[0] <= kuMaskHighBit)
                        {
                            if (val[0] == kuMaskHighBit)
                            {
                                // abs(Int32.MinValue)
                                this = s_bnMinInt;
                            }
                            else
                            {
                                _sign = -(int)val[0];
                                _bits = null;
                            }
                            AssertValid();
                            return;
                        }
                    }

                    if (len != val.Length)
                    {
                        _sign = -1;
                        _bits = new nuint[len];
                        Array.Copy(val, _bits, len);
                    }
                    else
                    {
                        _sign = -1;
                        _bits = val;
                    }
                }
                else
                {
                    _sign = +1;
                    _bits = val;
                }
            }
            AssertValid();
        }

        internal BigIntegerNative(int n, nuint[]? rgu)
        {
            if (rgu is not null && rgu.Length > MaxLength)
            {
                ThrowHelper.ThrowOverflowException();
            }

            _sign = n;
            _bits = rgu;

            AssertValid();
        }

        /// <summary>
        /// Constructor used during bit manipulation and arithmetic.
        /// When possible the value will be packed into  _sign to conserve space.
        /// </summary>
        /// <param name="value">The absolute value of the number</param>
        /// <param name="negative">The bool indicating the sign of the value.</param>
        private BigIntegerNative(ReadOnlySpan<nuint> value, bool negative)
        {
            if (value.Length > MaxLength)
            {
                ThrowHelper.ThrowOverflowException();
            }

            int len;

            // Try to conserve space as much as possible by checking for wasted leading span entries
            // sometimes the span has leading zeros from bit manipulation operations & and ^
            for (len = value.Length; len > 0 && value[len - 1] == 0; len--) ;

            if (len == 0)
            {
                this = s_bnZeroInt;
            }
            else if (len == 1 && value[0] < kuMaskHighBit)
            {
                // Values like (Int32.MaxValue+1) are stored as "0x80000000" and as such cannot be packed into _sign
                _sign = negative ? -(int)value[0] : (int)value[0];
                _bits = null;
            }
            else
            {
                _sign = negative ? -1 : +1;
                _bits = value.Slice(0, len).ToArray();
            }
            AssertValid();
        }

        /// <summary>
        /// Create a  BigIntegerNative  from a little-endian twos-complement nuint span.
        /// </summary>
        /// <param name="value"></param>
        private BigIntegerNative(Span<nuint> value)
        {
            if (value.Length > MaxLength)
            {
                ThrowHelper.ThrowOverflowException();
            }

            int dwordCount = value.Length;
            bool isNegative = dwordCount > 0 && ((nint)value[dwordCount - 1]) < 0;

            // Try to conserve space as much as possible by checking for wasted leading span entries
            while (dwordCount > 0 && value[dwordCount - 1] == 0) dwordCount--;

            if (dwordCount == 0)
            {
                // BigInteger.Zero
                this = s_bnZeroInt;
                AssertValid();
                return;
            }
            if (dwordCount == 1)
            {
                nuint v = value[0];
                if (isNegative)
                {
                    Debug.Assert(value.Length == 1);
                    v = (nuint)(-(nint)v);

                    if (v == kuMaskHighBit)
                    {
                        this = s_bnMinInt;
                        AssertValid();
                        return;
                    }
                }

                if (v < kuMaskHighBit)
                {
                    _sign = unchecked((int)v);
                    _bits = null;
                }
                else
                {
                    _sign = isNegative ? -1 : +1;
                    _bits = new nuint[1] { v };
                }
                AssertValid();
                return;
            }

            if (!isNegative)
            {
                // Handle the simple positive value cases where the input is already in sign magnitude
                _sign = +1;
                value = value.Slice(0, dwordCount);
                _bits = value.ToArray();
                AssertValid();
                return;
            }

            // Finally handle the more complex cases where we must transform the input into sign magnitude
            NumericsHelpers.DangerousMakeTwosComplement(value); // mutates val

            // Pack _bits to remove any wasted space after the twos complement
            int len = value.Length;
            while (len > 0 && value[len - 1] == 0) len--;

            // The number is represented by a single dword
            if (len == 1 && value[0] <= kuMaskHighBit)
            {
                if (value[0] == 1 /* abs(-1) */)
                {
                    this = s_bnMinusOneInt;
                }
                else if (value[0] == kuMaskHighBit /* abs(Int32.MinValue) */)
                {
                    this = s_bnMinInt;
                }
                else
                {
                    _sign = -(int)value[0];
                    _bits = null;
                }
            }
            else
            {
                _sign = -1;
                _bits = value.Slice(0, len).ToArray();
            }
            AssertValid();
            return;
        }

        public static BigIntegerNative Zero { get { return s_bnZeroInt; } }

        public static BigIntegerNative One { get { return s_bnOneInt; } }

        public static BigIntegerNative MinusOne { get { return s_bnMinusOneInt; } }

        internal static int MaxLength => Array.MaxLength / kcByteNUint;

        public bool IsPowerOfTwo
        {
            get
            {
                AssertValid();

                if (_bits == null)
                    return BitOperations.IsPow2(_sign);

                if (_sign != 1)
                    return false;
                int iu = _bits.Length - 1;
                if (!BitOperations.IsPow2(_bits[iu]))
                    return false;
                while (--iu >= 0)
                {
                    if (_bits[iu] != 0)
                        return false;
                }
                return true;
            }
        }

        public bool IsZero { get { AssertValid(); return _sign == 0; } }

        public bool IsOne { get { AssertValid(); return _sign == 1 && _bits == null; } }

        public bool IsEven { get { AssertValid(); return _bits == null ? (_sign & 1) == 0 : (_bits[0] & 1) == 0; } }

        public int Sign
        {
            get { AssertValid(); return (_sign >> kcbitUint - 1) - (-_sign >> kcbitUint - 1); }
        }

        public static BigIntegerNative Parse(string value)
        {
            return Parse(value, NumberStyles.Integer);
        }

        public static BigIntegerNative Parse(string value, NumberStyles style)
        {
            return Parse(value, style, NumberFormatInfo.CurrentInfo);
        }

        public static BigIntegerNative Parse(string value, IFormatProvider? provider)
        {
            return Parse(value, NumberStyles.Integer, NumberFormatInfo.GetInstance(provider));
        }

        public static BigIntegerNative Parse(string value, NumberStyles style, IFormatProvider? provider)
        {
            ArgumentNullException.ThrowIfNull(value);
            return Parse(value.AsSpan(), style, NumberFormatInfo.GetInstance(provider));
        }

        public static bool TryParse([NotNullWhen(true)] string? value, out BigIntegerNative result)
        {
            return TryParse(value, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse([NotNullWhen(true)] string? value, NumberStyles style, IFormatProvider? provider, out BigIntegerNative result)
        {
            return TryParse(value.AsSpan(), style, NumberFormatInfo.GetInstance(provider), out result);
        }

        public static BigIntegerNative Parse(ReadOnlySpan<char> value, NumberStyles style = NumberStyles.Integer, IFormatProvider? provider = null)
        {
            return Number.ParseBigInteger(value, style, NumberFormatInfo.GetInstance(provider));
        }

        public static bool TryParse(ReadOnlySpan<char> value, out BigIntegerNative result)
        {
            return TryParse(value, NumberStyles.Integer, NumberFormatInfo.CurrentInfo, out result);
        }

        public static bool TryParse(ReadOnlySpan<char> value, NumberStyles style, IFormatProvider? provider, out BigIntegerNative result)
        {
            return Number.TryParseBigInteger(value, style, NumberFormatInfo.GetInstance(provider), out result) == Number.ParsingStatus.OK;
        }

        public static int Compare(BigIntegerNative left, BigIntegerNative right)
        {
            return left.CompareTo(right);
        }

        public static BigIntegerNative Abs(BigIntegerNative value)
        {
            return value >= Zero ? value : -value;
        }

        public static BigIntegerNative Add(BigIntegerNative left, BigIntegerNative right)
        {
            return left + right;
        }

        public static BigIntegerNative Subtract(BigIntegerNative left, BigIntegerNative right)
        {
            return left - right;
        }

        public static BigIntegerNative Multiply(BigIntegerNative left, BigIntegerNative right)
        {
            return left * right;
        }

        public static BigIntegerNative Divide(BigIntegerNative dividend, BigIntegerNative divisor)
        {
            return dividend / divisor;
        }

        public static BigIntegerNative Remainder(BigIntegerNative dividend, BigIntegerNative divisor)
        {
            return dividend % divisor;
        }

        public static BigIntegerNative DivRem(BigIntegerNative dividend, BigIntegerNative divisor, out BigIntegerNative remainder)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._bits == null;
            bool trivialDivisor = divisor._bits == null;

            if (trivialDividend && trivialDivisor)
            {
                BigIntegerNative quotient;
                quotient = Math.DivRem(dividend._sign, divisor._sign, out int remainder32);
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

            Debug.Assert(dividend._bits != null);

            if (trivialDivisor)
            {
                uint rest;

                nuint[]? bitsFromPool = null;
                int size = dividend._bits.Length;
                Span<nuint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                    : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                try
                {
                    // may throw DivideByZeroException
                    BigIntegerCalculator.Divide(dividend._bits, NumericsHelpers.Abs(divisor._sign), quotient, out rest);
                    Debug.Assert(rest <= int.MaxValue);

                    remainder = dividend._sign < 0 ? -(int)rest : rest;
                    return new BigIntegerNative(quotient, dividend._sign < 0 ^ divisor._sign < 0);
                }
                finally
                {
                    if (bitsFromPool != null)
                        ArrayPool<nuint>.Shared.Return(bitsFromPool);
                }
            }

            Debug.Assert(divisor._bits != null);

            if (dividend._bits.Length < divisor._bits.Length)
            {
                remainder = dividend;
                return s_bnZeroInt;
            }
            else
            {
                nuint[]? remainderFromPool = null;
                int size = dividend._bits.Length;
                Span<nuint> rest = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : remainderFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                nuint[]? quotientFromPool = null;
                size = dividend._bits.Length - divisor._bits.Length + 1;
                Span<nuint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Divide(dividend._bits, divisor._bits, quotient, rest);

                remainder = new BigIntegerNative(rest, dividend._sign < 0);
                var result = new BigIntegerNative(quotient, dividend._sign < 0 ^ divisor._sign < 0);

                if (remainderFromPool != null)
                    ArrayPool<nuint>.Shared.Return(remainderFromPool);

                if (quotientFromPool != null)
                    ArrayPool<nuint>.Shared.Return(quotientFromPool);

                return result;
            }
        }

        public static BigIntegerNative Negate(BigIntegerNative value)
        {
            return -value;
        }

        public static double Log(BigIntegerNative value)
        {
            return Log(value, Math.E);
        }

        public static double Log(BigIntegerNative value, double baseValue)
        {
            if (value._sign < 0 || baseValue == 1.0D)
                return double.NaN;
            if (baseValue == double.PositiveInfinity)
                return value.IsOne ? 0.0D : double.NaN;
            if (baseValue == 0.0D && !value.IsOne)
                return double.NaN;
            if (value._bits == null)
                return Math.Log(value._sign, baseValue);

            nuint h = value._bits[value._bits.Length - 1];
            nuint m = value._bits.Length > 1 ? value._bits[value._bits.Length - 2] : 0;
            nuint l = value._bits.Length > 2 ? value._bits[value._bits.Length - 3] : 0;

            if (Environment.Is64BitProcess)
            {
                // Measure the exact bit count
                int c = BitOperations.LeadingZeroCount((uint)h);
                long b = (long)value._bits.Length * 32 - c;

                // Extract most significant bits
                ulong x = h << 32 + c | m << c | l >> 32 - c;

                // Let v = value, b = bit count, x = v/2^b-64
                // log ( v/2^b-64 * 2^b-64 ) = log ( x ) + log ( 2^b-64 )
                return Math.Log(x, baseValue) + (b - 64) / Math.Log(baseValue, 2);
            }
            else
            {
                // Measure the exact bit count
                int c = BitOperations.LeadingZeroCount((ulong)h);
                long b = (long)value._bits.Length * 64 - c;

                // Extract most significant bits
                UInt128 x = h << 64 + c | m << c | l >> 64 - c;

                // Let v = value, b = bit count, x = v/2^b-128
                // log ( v/2^b-128 * 2^b-128 ) = log ( x ) + log ( 2^b-128 )
                return Math.Log((double)x, baseValue) + (b - 128) / Math.Log(baseValue, 2);
            }
        }

        public static double Log10(BigIntegerNative value)
        {
            return Log(value, 10);
        }


#if false
        public static BigIntegerNative GreatestCommonDivisor(BigIntegerNative left, BigIntegerNative right)
        {
            left.AssertValid();
            right.AssertValid();

            bool trivialLeft = left._bits == null;
            bool trivialRight = right._bits == null;

            if (trivialLeft && trivialRight)
            {
                return BigIntegerCalculator.Gcd(NumericsHelpers.Abs(left._sign), NumericsHelpers.Abs(right._sign));
            }

            if (trivialLeft)
            {
                Debug.Assert(right._bits != null);
                return left._sign != 0
                    ? BigIntegerCalculator.Gcd(right._bits, NumericsHelpers.Abs(left._sign))
                    : new BigIntegerNative(right._bits, negative: false);
            }

            if (trivialRight)
            {
                Debug.Assert(left._bits != null);
                return right._sign != 0
                    ? BigIntegerCalculator.Gcd(left._bits, NumericsHelpers.Abs(right._sign))
                    : new BigIntegerNative(left._bits, negative: false);
            }

            Debug.Assert(left._bits != null && right._bits != null);

            if (BigIntegerCalculator.Compare(left._bits, right._bits) < 0)
            {
                return GreatestCommonDivisor(right._bits, left._bits);
            }
            else
            {
                return GreatestCommonDivisor(left._bits, right._bits);
            }
        }

        private static BigIntegerNative GreatestCommonDivisor(ReadOnlySpan<nuint> leftBits, ReadOnlySpan<nuint> rightBits)
        {
            Debug.Assert(BigIntegerCalculator.Compare(leftBits, rightBits) >= 0);

            nuint[]? bitsFromPool = null;
            BigIntegerNative result;

            // Short circuits to spare some allocations...
            if (rightBits.Length == 1)
            {
                uint temp = BigIntegerCalculator.Remainder(leftBits, rightBits[0]);
                result = BigIntegerCalculator.Gcd(rightBits[0], temp);
            }
            else if (rightBits.Length == 2)
            {
                Span<nuint> bits = (leftBits.Length <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(leftBits.Length)).Slice(0, leftBits.Length);

                BigIntegerCalculator.Remainder(leftBits, rightBits, bits);

                ulong left = (ulong)rightBits[1] << 32 | rightBits[0];
                ulong right = (ulong)bits[1] << 32 | bits[0];

                result = BigIntegerCalculator.Gcd(left, right);
            }
            else
            {
                Span<nuint> bits = (leftBits.Length <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(leftBits.Length)).Slice(0, leftBits.Length);

                BigIntegerCalculator.Gcd(leftBits, rightBits, bits);
                result = new BigIntegerNative(bits, negative: false);
            }

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }
#endif

        public static BigIntegerNative Max(BigIntegerNative left, BigIntegerNative right)
        {
            if (left.CompareTo(right) < 0)
                return right;
            return left;
        }

        public static BigIntegerNative Min(BigIntegerNative left, BigIntegerNative right)
        {
            if (left.CompareTo(right) <= 0)
                return left;
            return right;
        }

#if false
        public static BigIntegerNative ModPow(BigIntegerNative value, BigIntegerNative exponent, BigIntegerNative modulus)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(exponent.Sign);
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

            bool trivialValue = value._bits == null;
            bool trivialExponent = exponent._bits == null;
            bool trivialModulus = modulus._bits == null;

            BigIntegerNative result;

            if (trivialModulus)
            {
                uint bits = trivialValue && trivialExponent ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
                            trivialValue ? BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._bits!, NumericsHelpers.Abs(modulus._sign)) :
                            trivialExponent ? BigIntegerCalculator.Pow(value._bits!, NumericsHelpers.Abs(exponent._sign), NumericsHelpers.Abs(modulus._sign)) :
                            BigIntegerCalculator.Pow(value._bits!, exponent._bits!, NumericsHelpers.Abs(modulus._sign));

                result = value._sign < 0 && !exponent.IsEven ? -1 * bits : bits;
            }
            else
            {
                int size = (modulus._bits?.Length ?? 1) << 1;
                nuint[]? bitsFromPool = null;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();
                if (trivialValue)
                {
                    if (trivialExponent)
                    {
                        BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), NumericsHelpers.Abs(exponent._sign), modulus._bits!, bits);
                    }
                    else
                    {
                        BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), exponent._bits!, modulus._bits!, bits);
                    }
                }
                else if (trivialExponent)
                {
                    BigIntegerCalculator.Pow(value._bits!, NumericsHelpers.Abs(exponent._sign), modulus._bits!, bits);
                }
                else
                {
                    BigIntegerCalculator.Pow(value._bits!, exponent._bits!, modulus._bits!, bits);
                }

                result = new BigIntegerNative(bits, value._sign < 0 && !exponent.IsEven);

                if (bitsFromPool != null)
                    ArrayPool<nuint>.Shared.Return(bitsFromPool);
            }

            return result;
        }

        public static BigIntegerNative Pow(BigIntegerNative value, int exponent)
        {
#if NET8_0_OR_GREATER
            ArgumentOutOfRangeException.ThrowIfNegative(exponent);
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

            bool trivialValue = value._bits == null;

            uint power = NumericsHelpers.Abs(exponent);
            nuint[]? bitsFromPool = null;
            BigIntegerNative result;

            if (trivialValue)
            {
                if (value._sign == 1)
                    return value;
                if (value._sign == -1)
                    return (exponent & 1) != 0 ? value : s_bnOneInt;
                if (value._sign == 0)
                    return value;

                int size = BigIntegerCalculator.PowBound(power, 1);
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Pow(NumericsHelpers.Abs(value._sign), power, bits);
                result = new BigIntegerNative(bits, value._sign < 0 && (exponent & 1) != 0);
            }
            else
            {
                int size = BigIntegerCalculator.PowBound(power, value._bits!.Length);
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Pow(value._bits, power, bits);
                result = new BigIntegerNative(bits, value._sign < 0 && (exponent & 1) != 0);
            }

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }
#endif

        public override int GetHashCode()
        {
            AssertValid();

            if (_bits is null)
                return _sign;

            HashCode hash = default;
            hash.AddBytes(MemoryMarshal.AsBytes(_bits.AsSpan()));
            hash.Add(_sign);
            return hash.ToHashCode();
        }

        public override bool Equals([NotNullWhen(true)] object? obj)
        {
            AssertValid();

            return obj is BigIntegerNative other && Equals(other);
        }

        public bool Equals(long other)
        {
            AssertValid();

            if (_bits == null)
                return _sign == other;

            if (Environment.Is64BitProcess)
            {
                if ((_sign ^ other) < 0 || _bits.Length > 1)
                    return false;

                ulong uu = other < 0 ? (ulong)-other : (ulong)other;
                return _bits[0] == uu;
            }
            else
            {
                if ((_sign ^ other) < 0 || _bits.Length > 2)
                    return false;

                ulong uuOther = other < 0 ? (ulong)-other : (ulong)other;
                ulong uu = _bits.Length == 2 ? NumericsHelpers.MakeUInt64((uint)_bits[1], (uint)_bits[0]) : _bits[0];
                return uu == uuOther;
            }
        }

        public bool Equals(ulong other)
        {
            AssertValid();

            if (_sign < 0)
                return false;
            if (_bits == null)
                return (ulong)_sign == other;

            if (Environment.Is64BitProcess)
            {
                if (_bits.Length > 1)
                    return false;

                return _bits[0] == other;
            }
            else
            {
                ulong uu = _bits.Length == 2 ? NumericsHelpers.MakeUInt64((uint)_bits[1], (uint)_bits[0]) : _bits[0];
                return uu == other;
            }
        }

        public bool Equals(BigIntegerNative other)
        {
            AssertValid();
            other.AssertValid();

            return _sign == other._sign && _bits.AsSpan().SequenceEqual(other._bits);
        }

        public int CompareTo(long other)
        {
            AssertValid();

            if (_bits == null)
                return ((long)_sign).CompareTo(other);
            if ((_sign ^ other) < 0 || _bits.Length > 2)
                return _sign;


            if (Environment.Is64BitProcess)
            {
                if (_bits.Length > 1)
                    return +1;

                return _sign * _bits[0].CompareTo(other);
            }
            else
            {
                ulong uuOther = other < 0 ? (ulong)-other : (ulong)other;
                ulong uu = _bits.Length == 2 ? NumericsHelpers.MakeUInt64((uint)_bits[1], (uint)_bits[0]) : _bits[0];
                return _sign * uu.CompareTo(uuOther);
            }
        }

        public int CompareTo(ulong other)
        {
            AssertValid();

            if (_sign < 0)
                return -1;
            if (_bits == null)
                return ((ulong)_sign).CompareTo(other);

            if (Environment.Is64BitProcess)
            {
                if (_bits.Length > 1)
                    return +1;

                return _bits[0].CompareTo(other);
            }
            else
            {
                if (_bits.Length > 2)
                    return +1;
                ulong uu = _bits.Length == 2 ? NumericsHelpers.MakeUInt64((uint)_bits[1], (uint)_bits[0]) : _bits[0];
                return uu.CompareTo(other);
            }
        }

        public int CompareTo(BigIntegerNative other)
        {
            AssertValid();
            other.AssertValid();

            if ((_sign ^ other._sign) < 0)
            {
                // Different signs, so the comparison is easy.
                return _sign < 0 ? -1 : +1;
            }

            // Same signs
            if (_bits == null)
            {
                if (other._bits == null)
                    return _sign < other._sign ? -1 : _sign > other._sign ? +1 : 0;
                return -other._sign;
            }
            if (other._bits == null)
                return _sign;

            int bitsResult = BigIntegerCalculator.Compare(_bits, other._bits);
            return _sign < 0 ? -bitsResult : bitsResult;
        }

        public int CompareTo(object? obj)
        {
            if (obj == null)
                return 1;
            if (obj is BigIntegerNative bigInt)
                return CompareTo(bigInt);
            throw new ArgumentException(SR.Argument_MustBeBigInt, nameof(obj));
        }

        /// <summary>
        /// Returns the value of this  BigIntegerNative  as a little-endian twos-complement
        /// byte array, using the fewest number of bytes possible. If the value is zero,
        /// return an array of one byte whose element is 0x00.
        /// </summary>
        /// <returns></returns>
        public byte[] ToByteArray() => ToByteArray(isUnsigned: false, isBigEndian: false);

        /// <summary>
        /// Returns the value of this  BigIntegerNative  as a byte array using the fewest number of bytes possible.
        /// If the value is zero, returns an array of one byte whose element is 0x00.
        /// </summary>
        /// <param name="isUnsigned">Whether or not an unsigned encoding is to be used</param>
        /// <param name="isBigEndian">Whether or not to write the bytes in a big-endian byte order</param>
        /// <returns></returns>
        /// <exception cref="OverflowException">
        ///   If <paramref name="isUnsigned"/> is <c>true</c> and <see cref="Sign"/> is negative.
        /// </exception>
        /// <remarks>
        /// The integer value <c>33022</c> can be exported as four different arrays.
        ///
        /// <list type="bullet">
        ///   <item>
        ///     <description>
        ///       <c>(isUnsigned: false, isBigEndian: false)</c> => <c>new byte[] { 0xFE, 0x80, 0x00 }</c>
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       <c>(isUnsigned: false, isBigEndian: true)</c> => <c>new byte[] { 0x00, 0x80, 0xFE }</c>
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       <c>(isUnsigned: true, isBigEndian: false)</c> => <c>new byte[] { 0xFE, 0x80 }</c>
        ///     </description>
        ///   </item>
        ///   <item>
        ///     <description>
        ///       <c>(isUnsigned: true, isBigEndian: true)</c> => <c>new byte[] { 0x80, 0xFE }</c>
        ///     </description>
        ///   </item>
        /// </list>
        /// </remarks>
        public byte[] ToByteArray(bool isUnsigned = false, bool isBigEndian = false)
        {
            int ignored = 0;
            return TryGetBytes(GetBytesMode.AllocateArray, default, isUnsigned, isBigEndian, ref ignored)!;
        }

        /// <summary>
        /// Copies the value of this  BigIntegerNative  as little-endian twos-complement
        /// bytes, using the fewest number of bytes possible. If the value is zero,
        /// outputs one byte whose element is 0x00.
        /// </summary>
        /// <param name="destination">The destination span to which the resulting bytes should be written.</param>
        /// <param name="bytesWritten">The number of bytes written to <paramref name="destination"/>.</param>
        /// <param name="isUnsigned">Whether or not an unsigned encoding is to be used</param>
        /// <param name="isBigEndian">Whether or not to write the bytes in a big-endian byte order</param>
        /// <returns>true if the bytes fit in <paramref name="destination"/>; false if not all bytes could be written due to lack of space.</returns>
        /// <exception cref="OverflowException">If <paramref name="isUnsigned"/> is <c>true</c> and <see cref="Sign"/> is negative.</exception>
        public bool TryWriteBytes(Span<byte> destination, out int bytesWritten, bool isUnsigned = false, bool isBigEndian = false)
        {
            bytesWritten = 0;
            if (TryGetBytes(GetBytesMode.Span, destination, isUnsigned, isBigEndian, ref bytesWritten) == null)
            {
                bytesWritten = 0;
                return false;
            }
            return true;
        }

        internal bool TryWriteOrCountBytes(Span<byte> destination, out int bytesWritten, bool isUnsigned = false, bool isBigEndian = false)
        {
            bytesWritten = 0;
            return TryGetBytes(GetBytesMode.Span, destination, isUnsigned, isBigEndian, ref bytesWritten) != null;
        }

        /// <summary>Gets the number of bytes that will be output by <see cref="ToByteArray(bool, bool)"/> and <see cref="TryWriteBytes(Span{byte}, out int, bool, bool)"/>.</summary>
        /// <returns>The number of bytes.</returns>
        public int GetByteCount(bool isUnsigned = false)
        {
            int count = 0;
            // Big or Little Endian doesn't matter for the byte count.
            const bool IsBigEndian = false;
            TryGetBytes(GetBytesMode.Count, default, isUnsigned, IsBigEndian, ref count);
            return count;
        }

        /// <summary>Mode used to enable sharing <see cref="TryGetBytes(GetBytesMode, Span{byte}, bool, bool, ref int)"/> for multiple purposes.</summary>
        private enum GetBytesMode
        {
            AllocateArray,
            Count,
            Span
        }

        /// <summary>Shared logic for <see cref="ToByteArray(bool, bool)"/>, <see cref="TryWriteBytes(Span{byte}, out int, bool, bool)"/>, and <see cref="GetByteCount"/>.</summary>
        /// <param name="mode">Which entry point is being used.</param>
        /// <param name="destination">The destination span, if mode is <see cref="GetBytesMode.Span"/>.</param>
        /// <param name="isUnsigned">True to never write a padding byte, false to write it if the high bit is set.</param>
        /// <param name="isBigEndian">True for big endian byte ordering, false for little endian byte ordering.</param>
        /// <param name="bytesWritten">
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.AllocateArray"/>, ignored.
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.Count"/>, the number of bytes that would be written.
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.Span"/>, the number of bytes written to the span or that would be written if it were long enough.
        /// </param>
        /// <returns>
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.AllocateArray"/>, the result array.
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.Count"/>, null.
        /// If <paramref name="mode"/>==<see cref="GetBytesMode.Span"/>, non-null if the span was long enough, null if there wasn't enough room.
        /// </returns>
        /// <exception cref="OverflowException">If <paramref name="isUnsigned"/> is <c>true</c> and <see cref="Sign"/> is negative.</exception>
        private byte[]? TryGetBytes(GetBytesMode mode, Span<byte> destination, bool isUnsigned, bool isBigEndian, ref int bytesWritten)
        {
            Debug.Assert(mode == GetBytesMode.AllocateArray || mode == GetBytesMode.Count || mode == GetBytesMode.Span, $"Unexpected mode {mode}.");
            Debug.Assert(mode == GetBytesMode.Span || destination.IsEmpty, $"If we're not in span mode, we shouldn't have been passed a destination.");

            int sign = _sign;
            if (sign == 0)
            {
                switch (mode)
                {
                    case GetBytesMode.AllocateArray:
                        return new byte[] { 0 };
                    case GetBytesMode.Count:
                        bytesWritten = 1;
                        return null;
                    default: // case GetBytesMode.Span:
                        bytesWritten = 1;
                        if (destination.Length != 0)
                        {
                            destination[0] = 0;
                            return Array.Empty<byte>();
                        }
                        return null;
                }
            }

            if (isUnsigned && sign < 0)
            {
                throw new OverflowException(SR.Overflow_Negative_Unsigned);
            }

            byte highByte;
            int nonZeroDwordIndex = 0;
            nuint highDword;
            nuint[]? bits = _bits;
            if (bits == null)
            {
                highByte = (byte)(sign < 0 ? 0xff : 0x00);
                highDword = unchecked((nuint)sign);
            }
            else if (sign == -1)
            {
                highByte = 0xff;

                // If sign is -1, we will need to two's complement bits.
                // Previously this was accomplished via NumericsHelpers.DangerousMakeTwosComplement(),
                // however, we can do the two's complement on the stack so as to avoid
                // creating a temporary copy of bits just to hold the two's complement.
                // One special case in DangerousMakeTwosComplement() is that if the array
                // is all zeros, then it would allocate a new array with the high-order
                // nuint set to 1 (for the carry). In our usage, we will not hit this case
                // because a bits array of all zeros would represent 0, and this case
                // would be encoded as _bits = null and _sign = 0.
                Debug.Assert(bits.Length > 0);
                Debug.Assert(bits[bits.Length - 1] != 0);
                while (bits[nonZeroDwordIndex] == 0U)
                {
                    nonZeroDwordIndex++;
                }

                highDword = ~bits[bits.Length - 1];
                if (bits.Length - 1 == nonZeroDwordIndex)
                {
                    // This will not overflow because highDword is less than or equal to uint.MaxValue - 1.
                    Debug.Assert(highDword <= nuint.MaxValue - 1);
                    highDword += 1U;
                }
            }
            else
            {
                Debug.Assert(sign == 1);
                highByte = 0x00;
                highDword = bits[bits.Length - 1];
            }

            int msbIndex = BitOperations.Log2(highDword ^ unchecked((nuint)(sbyte)highByte)) >> 3;
            byte msb = unchecked((byte)(highDword >> 8 * msbIndex));

            // Ensure high bit is 0 if positive, 1 if negative
            bool needExtraByte = (msb & 0x80) != (highByte & 0x80) && !isUnsigned;
            int length = msbIndex + 1 + (needExtraByte ? 1 : 0);
            if (bits != null)
            {
                length = checked(kcByteNUint * (bits.Length - 1) + length);
            }

            byte[] array;
            switch (mode)
            {
                case GetBytesMode.AllocateArray:
                    destination = array = new byte[length];
                    break;
                case GetBytesMode.Count:
                    bytesWritten = length;
                    return null;
                default: // case GetBytesMode.Span:
                    bytesWritten = length;
                    if (destination.Length < length)
                    {
                        return null;
                    }
                    array = Array.Empty<byte>();
                    break;
            }

            int curByte = isBigEndian ? length - 1 : 0;
            int increment = isBigEndian ? -1 : 1;

            if (bits != null)
            {
                for (int i = 0; i < bits.Length - 1; i++)
                {
                    nuint dword = bits[i];

                    if (sign == -1)
                    {
                        dword = ~dword;
                        if (i <= nonZeroDwordIndex)
                        {
                            dword = unchecked(dword + 1U);
                        }
                    }

                    destination[curByte] = unchecked((byte)dword);
                    curByte += increment;
                    destination[curByte] = unchecked((byte)(dword >> 8));
                    curByte += increment;
                    destination[curByte] = unchecked((byte)(dword >> 16));
                    curByte += increment;
                    destination[curByte] = unchecked((byte)(dword >> 24));
                    curByte += increment;
                    if (Environment.Is64BitProcess)
                    {
                        destination[curByte] = unchecked((byte)(dword >> 32));
                        curByte += increment;
                        destination[curByte] = unchecked((byte)(dword >> 40));
                        curByte += increment;
                        destination[curByte] = unchecked((byte)(dword >> 48));
                        curByte += increment;
                        destination[curByte] = unchecked((byte)(dword >> 56));
                        curByte += increment;
                    }
                }
            }

            Debug.Assert(0 <= msbIndex && msbIndex < kcByteNUint);
            destination[curByte] = unchecked((byte)highDword);
            for (int i = 1; i <= msbIndex; i++)
            {
                curByte += increment;
                destination[curByte] = unchecked((byte)(highDword >> (i << 3)));
            }

            // Assert we're big endian, or little endian consistency holds.
            Debug.Assert(isBigEndian || !needExtraByte && curByte == length - 1 || needExtraByte && curByte == length - 2);
            // Assert we're little endian, or big endian consistency holds.
            Debug.Assert(!isBigEndian || !needExtraByte && curByte == 0 || needExtraByte && curByte == 1);

            if (needExtraByte)
            {
                curByte += increment;
                destination[curByte] = highByte;
            }

            return array;
        }

        /// <summary>
        /// Converts the value of this  BigIntegerNative  to a little-endian twos-complement
        /// uint span allocated by the caller using the fewest number of uints possible.
        /// </summary>
        /// <param name="buffer">Pre-allocated buffer by the caller.</param>
        /// <returns>The actual number of copied elements.</returns>
        private int WriteTo(Span<nuint> buffer)
        {
            Debug.Assert(_bits is null || _sign == 0 ? buffer.Length == 2 : buffer.Length >= _bits.Length + 1);

            nuint highDWord;

            if (_bits is null)
            {
                buffer[0] = unchecked((nuint)_sign);
                highDWord = _sign < 0 ? nuint.MaxValue : 0;
            }
            else
            {
                _bits.CopyTo(buffer);
                buffer = buffer.Slice(0, _bits.Length + 1);
                if (_sign == -1)
                {
                    NumericsHelpers.DangerousMakeTwosComplement(buffer.Slice(0, buffer.Length - 1));  // Mutates dwords
                    highDWord = nuint.MaxValue;
                }
                else
                    highDWord = 0;
            }

            // Find highest significant byte and ensure high bit is 0 if positive, 1 if negative
            int msb = buffer.Length - 2;
            while (msb > 0 && buffer[msb] == highDWord)
            {
                msb--;
            }

            // Ensure high bit is 0 if positive, 1 if negative
            bool needExtraByte = Environment.Is64BitProcess
                ? (buffer[msb] & 0x8000_0000_0000_0000) != (highDWord & 0x8000_0000_0000_0000)
                : (buffer[msb] & 0x8000_0000) != (highDWord & 0x8000_0000);
            int count;

            if (needExtraByte)
            {
                count = msb + 2;
                buffer = buffer.Slice(0, count);
                buffer[buffer.Length - 1] = highDWord;
            }
            else
            {
                count = msb + 1;
            }

            return count;
        }

        public override string ToString()
        {
            return Number.FormatBigInteger(this, null, NumberFormatInfo.CurrentInfo);
        }

        public string ToString(IFormatProvider? provider)
        {
            return Number.FormatBigInteger(this, null, NumberFormatInfo.GetInstance(provider));
        }

        public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format)
        {
            return Number.FormatBigInteger(this, format, NumberFormatInfo.CurrentInfo);
        }

        public string ToString([StringSyntax(StringSyntaxAttribute.NumericFormat)] string? format, IFormatProvider? provider)
        {
            return Number.FormatBigInteger(this, format, NumberFormatInfo.GetInstance(provider));
        }

        private string DebuggerDisplay
        {
            get
            {
                // For very big numbers, ToString can be too long or even timeout for Visual Studio to display
                // Display a fast estimated value instead

                // Use ToString for small values

                if (_bits is null || _bits.Length <= 4)
                {
                    return ToString();
                }
                // Estimate the value x as `L * 2^n`, while L is the value of high bits, and n is the length of low bits
                // Represent L as `k * 10^i`, then `x = L * 2^n = k * 10^(i + (n * log10(2)))`
                // Let `m = n * log10(2)`, the final result would be `x = (k * 10^(m - [m])) * 10^(i+[m])`

                const double log10Of2 = 0.3010299956639812; // Log10(2)
                ulong highBits;
                double lowBitsCount32;  // if Length > int.MaxValue/32, counting in bits can cause overflow
                if (Environment.Is64BitProcess)
                {
                    highBits = _bits[^1];
                    lowBitsCount32 = 2 * (_bits.Length - 1);
                    if (highBits <= uint.MaxValue)
                    {
                        highBits = (highBits << kcbitUint) | (_bits[^2] >> kcbitUint);
                        --lowBitsCount32;
                    }
                }
                else
                {
                    highBits = ((ulong)_bits[^1] << kcbitUint) + _bits[^2];
                    lowBitsCount32 = _bits.Length - 2;
                }

                double exponentLow = lowBitsCount32 * kcbitUint * log10Of2;

                // Max possible length of _bits is int.MaxValue of bytes,
                // thus max possible value of  BigIntegerNative  is 2^(8*Array.MaxLength)-1 which is larger than 10^(2^33)
                // Use long to avoid potential overflow
                long exponent = (long)exponentLow;
                double significand = highBits * Math.Pow(10, exponentLow - exponent);

                // scale significand to [1, 10)
                double log10 = Math.Log10(significand);
                if (log10 >= 1)
                {
                    exponent += (long)log10;
                    significand /= Math.Pow(10, Math.Floor(log10));
                }

                // The digits can be incorrect because of floating point errors and estimation in Log and Exp
                // Keep some digits in the significand. 8 is arbitrarily chosen, about half of the precision of double
                significand = Math.Round(significand, 8);

                if (significand >= 10.0)
                {
                    // 9.9999999999999 can be rounded to 10, make the display to be more natural
                    significand /= 10.0;
                    exponent++;
                }

                string signStr = _sign < 0 ? NumberFormatInfo.CurrentInfo.NegativeSign : "";

                // Use about a half of the precision of double
                return $"{signStr}{significand:F8}e+{exponent}";
            }
        }

        public bool TryFormat(Span<char> destination, out int charsWritten, [StringSyntax(StringSyntaxAttribute.NumericFormat)] ReadOnlySpan<char> format = default, IFormatProvider? provider = null)
        {
            return Number.TryFormatBigInteger(this, format, NumberFormatInfo.GetInstance(provider), destination, out charsWritten);
        }

        private static BigIntegerNative Add(ReadOnlySpan<nuint> leftBits, int leftSign, ReadOnlySpan<nuint> rightBits, int rightSign)
        {
            bool trivialLeft = leftBits.IsEmpty;
            bool trivialRight = rightBits.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerNative result;
            nuint[]? bitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!rightBits.IsEmpty);

                int size = rightBits.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(rightBits, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }
            else if (trivialRight)
            {
                Debug.Assert(!leftBits.IsEmpty);

                int size = leftBits.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(leftBits, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }
            else if (leftBits.Length < rightBits.Length)
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = rightBits.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(rightBits, leftBits, bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }
            else
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = leftBits.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Add(leftBits, rightBits, bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }

        public static BigIntegerNative operator -(BigIntegerNative left, BigIntegerNative right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._bits == null && right._bits == null)
                return (long)left._sign - right._sign;

            if (left._sign < 0 != right._sign < 0)
                return Add(left._bits, left._sign, right._bits, -1 * right._sign);
            return Subtract(left._bits, left._sign, right._bits, right._sign);
        }

        private static BigIntegerNative Subtract(ReadOnlySpan<nuint> leftBits, int leftSign, ReadOnlySpan<nuint> rightBits, int rightSign)
        {
            bool trivialLeft = leftBits.IsEmpty;
            bool trivialRight = rightBits.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerNative result;
            nuint[]? bitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!rightBits.IsEmpty);

                int size = rightBits.Length;
                Span<nuint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(rightBits, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerNative(bits, leftSign >= 0);
            }
            else if (trivialRight)
            {
                Debug.Assert(!leftBits.IsEmpty);

                int size = leftBits.Length;
                Span<nuint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(leftBits, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }
            else if (BigIntegerCalculator.Compare(leftBits, rightBits) < 0)
            {
                int size = rightBits.Length;
                Span<nuint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(rightBits, leftBits, bits);
                result = new BigIntegerNative(bits, leftSign >= 0);
            }
            else
            {
                Debug.Assert(!leftBits.IsEmpty && !rightBits.IsEmpty);

                int size = leftBits.Length;
                Span<nuint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Subtract(leftBits, rightBits, bits);
                result = new BigIntegerNative(bits, leftSign < 0);
            }

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }

        //
        // Explicit Conversions From BigInteger
        //

        public static explicit operator byte(BigIntegerNative value)
        {
            return checked((byte)(int)value);
        }

        /// <summary>Explicitly converts a big integer to a <see cref="char" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="char" /> value.</returns>
        public static explicit operator char(BigIntegerNative value)
        {
            return checked((char)(int)value);
        }

        public static explicit operator decimal(BigIntegerNative value)
        {
            value.AssertValid();
            if (value._bits == null)
                return value._sign;

            int lo = 0, mi = 0, hi = 0;

            int length = value._bits.Length;
            if (Environment.Is64BitProcess)
            {
                if (length > 2 || length == 1 && value._bits[1] > uint.MaxValue) throw new OverflowException(SR.Overflow_Decimal);

                unchecked
                {
                    if (length > 1) hi = (int)value._bits[1];
                    if (length > 0)
                    {
                        mi = (int)(value._bits[0] >> kcbitUint);
                        lo = (int)value._bits[0];
                    }
                }
            }
            else
            {
                if (length > 3) throw new OverflowException(SR.Overflow_Decimal);

                unchecked
                {
                    if (length > 2) hi = (int)value._bits[2];
                    if (length > 1) mi = (int)value._bits[1];
                    if (length > 0) lo = (int)value._bits[0];
                }
            }

            return new decimal(lo, mi, hi, value._sign < 0, 0);
        }

        public static explicit operator double(BigIntegerNative value)
        {
            value.AssertValid();

            int sign = value._sign;
            nuint[]? bits = value._bits;

            if (bits == null)
                return sign;

            int length = bits.Length;

            // The maximum exponent for doubles is 1023, which corresponds to a uint bit length of 32.
            // All BigIntegers with bits[] longer than 32 evaluate to Double.Infinity (or NegativeInfinity).
            // Cases where the exponent is between 1024 and 1035 are handled in NumericsHelpers.GetDoubleFromParts.
            int infinityLength = 1024 / kcbitNUint;

            if (length > infinityLength)
            {
                if (sign == 1)
                    return double.PositiveInfinity;
                else
                    return double.NegativeInfinity;
            }

            if (Environment.Is64BitProcess)
            {
                ulong h = bits[length - 1];
                ulong l = length > 1 ? bits[length - 2] : 0;

                int z = BitOperations.LeadingZeroCount(h);

                int exp = (length - 1) * kcbitUlong - z;
                ulong man = h << z | l >> kcbitUlong - z;

                return NumericsHelpers.GetDoubleFromParts(sign, exp, man);
            }
            else
            {
                ulong h = bits[length - 1];
                ulong m = length > 1 ? bits[length - 2] : 0;
                ulong l = length > 2 ? bits[length - 3] : 0;

                int z = BitOperations.LeadingZeroCount((nuint)h);

                int exp = (length - 2) * kcbitUint - z;
                ulong man = h << kcbitUint + z | m << z | l >> kcbitUint - z;

                return NumericsHelpers.GetDoubleFromParts(sign, exp, man);
            }
        }

        /// <summary>Explicitly converts a big integer to a <see cref="Half" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="Half" /> value.</returns>
        public static explicit operator Half(BigIntegerNative value)
        {
            return (Half)(double)value;
        }

        public static explicit operator short(BigIntegerNative value)
        {
            return checked((short)(int)value);
        }

        public static explicit operator int(BigIntegerNative value)
        {
            value.AssertValid();
            if (value._bits == null)
            {
                return value._sign;  // Value packed into int32 sign
            }
            if (value._bits.Length > 1)
            {
                // More than 32 bits
                throw new OverflowException(SR.Overflow_Int32);
            }
            if (value._sign > 0)
            {
                return checked((int)value._bits[0]);
            }
            if (value._bits[0] > kuMaskHighBit)
            {
                // Value > Int32.MinValue
                throw new OverflowException(SR.Overflow_Int32);
            }
            return unchecked(-(int)value._bits[0]);
        }

        public static explicit operator long(BigIntegerNative value)
        {
            value.AssertValid();
            if (value._bits == null)
            {
                return value._sign;
            }

            if (Environment.Is64BitProcess)
            {
                if (value._bits.Length > 1)
                {
                    // More than 64 bits
                    throw new OverflowException(SR.Overflow_Int64);
                }
                if (value._sign > 0)
                {
                    return checked((long)value._bits[0]);
                }
                if (value._bits[0] > unchecked((ulong)long.MinValue))
                {
                    // Value > Int64.MinValue
                    throw new OverflowException(SR.Overflow_Int64);
                }
                return unchecked(-(long)value._bits[0]);
            }
            else
            {
                int len = value._bits.Length;
                if (len > 2)
                {
                    throw new OverflowException(SR.Overflow_Int64);
                }

                ulong uu;
                if (len > 1)
                {
                    uu = NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0]);
                }
                else
                {
                    uu = value._bits[0];
                }

                long ll = value._sign > 0 ? unchecked((long)uu) : unchecked(-(long)uu);
                if (ll > 0 && value._sign > 0 || ll < 0 && value._sign < 0)
                {
                    // Signs match, no overflow
                    return ll;
                }
                throw new OverflowException(SR.Overflow_Int64);
            }
        }

        /// <summary>Explicitly converts a big integer to a <see cref="Int128" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="Int128" /> value.</returns>
        public static explicit operator Int128(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return value._sign;
            }

            int len = value._bits.Length;
            UInt128 uu;

            if (Environment.Is64BitProcess)
            {
                if (len > 2)
                {
                    throw new OverflowException(SR.Overflow_Int128);
                }

                uu = new UInt128(
                    len > 1 ? value._bits[1] : 0,
                    value._bits[0]
                );
            }
            else
            {
                if (len > 4)
                {
                    throw new OverflowException(SR.Overflow_Int128);
                }


                if (len > 2)
                {
                    uu = new UInt128(
                        NumericsHelpers.MakeUInt64(len > 3 ? (uint)value._bits[3] : 0, (uint)value._bits[2]),
                        NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0])
                    );
                }
                else if (len > 1)
                {
                    uu = NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0]);
                }
                else
                {
                    uu = value._bits[0];
                }
            }

            Int128 ll = value._sign > 0 ? unchecked((Int128)uu) : unchecked(-(Int128)uu);

            if (ll > 0 && value._sign > 0 || ll < 0 && value._sign < 0)
            {
                // Signs match, no overflow
                return ll;
            }
            throw new OverflowException(SR.Overflow_Int128);
        }

        /// <summary>Explicitly converts a big integer to a <see cref="nint" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="nint" /> value.</returns>
        public static explicit operator nint(BigIntegerNative value)
        {
            if (Environment.Is64BitProcess)
            {
                return (nint)(long)value;
            }
            else
            {
                return (int)value;
            }
        }

        public static explicit operator sbyte(BigIntegerNative value)
        {
            return checked((sbyte)(int)value);
        }

        public static explicit operator float(BigIntegerNative value)
        {
            return (float)(double)value;
        }

        public static explicit operator ushort(BigIntegerNative value)
        {
            return checked((ushort)(int)value);
        }

        public static explicit operator uint(BigIntegerNative value)
        {
            value.AssertValid();
            if (value._bits == null)
            {
                return checked((uint)value._sign);
            }
            else if (value._bits.Length > 1 || value._sign < 0)
            {
                throw new OverflowException(SR.Overflow_UInt32);
            }
            else
            {
                return checked((uint)value._bits[0]);
            }
        }

        public static explicit operator ulong(BigIntegerNative value)
        {
            value.AssertValid();
            if (value._bits == null)
            {
                return checked((ulong)value._sign);
            }

            int len = value._bits.Length;
            if (Environment.Is64BitProcess)
            {
                if (len > 1 || value._sign < 0)
                {
                    throw new OverflowException(SR.Overflow_UInt64);
                }
            }
            else
            {
                if (len > 2 || value._sign < 0)
                {
                    throw new OverflowException(SR.Overflow_UInt64);
                }

                if (len > 1)
                {
                    return NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0]);
                }
            }
            return value._bits[0];
        }

        /// <summary>Explicitly converts a big integer to a <see cref="UInt128" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="UInt128" /> value.</returns>
        public static explicit operator UInt128(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return checked((UInt128)value._sign);
            }

            int len = value._bits.Length;

            if (Environment.Is64BitProcess)
            {
                if (len > 2 || value._sign < 0)
                {
                    throw new OverflowException(SR.Overflow_Int128);
                }

                return new UInt128(
                    len > 1 ? value._bits[1] : 0,
                    value._bits[0]
                );
            }
            else
            {
                if (len > 4 || value._sign < 0)
                {
                    throw new OverflowException(SR.Overflow_Int128);
                }


                if (len > 2)
                {
                    return new UInt128(
                        NumericsHelpers.MakeUInt64(len > 3 ? (uint)value._bits[3] : 0, (uint)value._bits[2]),
                        NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0])
                    );
                }
                else if (len > 1)
                {
                    return NumericsHelpers.MakeUInt64((uint)value._bits[1], (uint)value._bits[0]);
                }
                else
                {
                    return value._bits[0];
                }
            }
        }

        /// <summary>Explicitly converts a big integer to a <see cref="nuint" /> value.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to <see cref="nuint" /> value.</returns>
        public static explicit operator nuint(BigIntegerNative value)
        {
            if (Environment.Is64BitProcess)
            {
                return (nuint)(ulong)value;
            }
            else
            {
                return (uint)value;
            }
        }

        //
        // Explicit Conversions To BigInteger
        //

        public static explicit operator BigIntegerNative(decimal value)
        {
            return new BigIntegerNative(value);
        }

        public static explicit operator BigIntegerNative(double value)
        {
            return new BigIntegerNative(value);
        }

        /// <summary>Explicitly converts a <see cref="Half" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static explicit operator BigIntegerNative(Half value)
        {
            return new BigIntegerNative((float)value);
        }

        /// <summary>Explicitly converts a <see cref="Complex" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static explicit operator BigIntegerNative(Complex value)
        {
            if (value.Imaginary != 0)
            {
                ThrowHelper.ThrowOverflowException();
            }
            return (BigIntegerNative)value.Real;
        }

        public static explicit operator BigIntegerNative(float value)
        {
            return new BigIntegerNative(value);
        }

        //
        // Implicit Conversions To BigInteger
        //

        public static implicit operator BigIntegerNative(byte value)
        {
            return new BigIntegerNative(value);
        }

        /// <summary>Implicitly converts a <see cref="char" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerNative(char value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(short value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(int value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(long value)
        {
            return new BigIntegerNative(value);
        }

        /// <summary>Implicitly converts a <see cref="Int128" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerNative(Int128 value)
        {
            int sign;
            nuint[]? bits;

            if (int.MinValue < value && value <= int.MaxValue)
            {
                sign = (int)value;
                bits = null;
            }
            else if (value == int.MinValue)
            {
                return s_bnMinInt;
            }
            else
            {
                UInt128 x;
                if (value < 0)
                {
                    x = unchecked((UInt128)(-value));
                    sign = -1;
                }
                else
                {
                    x = (UInt128)value;
                    sign = +1;
                }

                if (Environment.Is64BitProcess)
                {
                    if (x <= ulong.MaxValue)
                    {
                        bits = new nuint[1];
                        bits[0] = (nuint)(x >> kcbitUlong * 0);
                    }
                    else
                    {
                        bits = new nuint[2];
                        bits[0] = (nuint)(x >> kcbitUlong * 0);
                        bits[1] = (nuint)(x >> kcbitUlong * 1);
                    }
                }
                else
                {
                    if (x <= uint.MaxValue)
                    {
                        bits = new nuint[1];
                        bits[0] = (uint)(x >> kcbitUint * 0);
                    }
                    else if (x <= ulong.MaxValue)
                    {
                        bits = new nuint[2];
                        bits[0] = (uint)(x >> kcbitUint * 0);
                        bits[1] = (uint)(x >> kcbitUint * 1);
                    }
                    else if (x <= new UInt128(0x0000_0000_FFFF_FFFF, 0xFFFF_FFFF_FFFF_FFFF))
                    {
                        bits = new nuint[3];
                        bits[0] = (uint)(x >> kcbitUint * 0);
                        bits[1] = (uint)(x >> kcbitUint * 1);
                        bits[2] = (uint)(x >> kcbitUint * 2);
                    }
                    else
                    {
                        bits = new nuint[4];
                        bits[0] = (uint)(x >> kcbitUint * 0);
                        bits[1] = (uint)(x >> kcbitUint * 1);
                        bits[2] = (uint)(x >> kcbitUint * 2);
                        bits[3] = (uint)(x >> kcbitUint * 3);
                    }
                }
            }

            return new BigIntegerNative(sign, bits);
        }

        /// <summary>Implicitly converts a <see cref="nint" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerNative(nint value)
        {
            if (Environment.Is64BitProcess)
            {
                return new BigIntegerNative(value);
            }
            else
            {
                return new BigIntegerNative((int)value);
            }
        }

        public static implicit operator BigIntegerNative(sbyte value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(ushort value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(uint value)
        {
            return new BigIntegerNative(value);
        }

        public static implicit operator BigIntegerNative(ulong value)
        {
            return new BigIntegerNative(value);
        }

        /// <summary>Implicitly converts a <see cref="UInt128" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerNative(UInt128 value)
        {
            int sign = +1;
            nuint[]? bits;

            if (value <= int.MaxValue)
            {
                sign = (int)value;
                bits = null;
            }
            else if (Environment.Is64BitProcess)
            {
                if (value <= ulong.MaxValue)
                {
                    bits = new nuint[1];
                    bits[0] = (nuint)(value >> kcbitUlong * 0);
                }
                else
                {
                    bits = new nuint[2];
                    bits[0] = (nuint)(value >> kcbitUlong * 0);
                    bits[1] = (nuint)(value >> kcbitUlong * 1);
                }
            }
            else
            {
                if (value <= uint.MaxValue)
                {
                    bits = new nuint[1];
                    bits[0] = (uint)(value >> kcbitUint * 0);
                }
                else if (value <= ulong.MaxValue)
                {
                    bits = new nuint[2];
                    bits[0] = (uint)(value >> kcbitUint * 0);
                    bits[1] = (uint)(value >> kcbitUint * 1);
                }
                else if (value <= new UInt128(0x0000_0000_FFFF_FFFF, 0xFFFF_FFFF_FFFF_FFFF))
                {
                    bits = new nuint[3];
                    bits[0] = (uint)(value >> kcbitUint * 0);
                    bits[1] = (uint)(value >> kcbitUint * 1);
                    bits[2] = (uint)(value >> kcbitUint * 2);
                }
                else
                {
                    bits = new nuint[4];
                    bits[0] = (uint)(value >> kcbitUint * 0);
                    bits[1] = (uint)(value >> kcbitUint * 1);
                    bits[2] = (uint)(value >> kcbitUint * 2);
                    bits[3] = (uint)(value >> kcbitUint * 3);
                }
            }

            return new BigIntegerNative(sign, bits);
        }

        /// <summary>Implicitly converts a <see cref="nuint" /> value to a big integer.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns><paramref name="value" /> converted to a big integer.</returns>
        public static implicit operator BigIntegerNative(nuint value)
        {
            if (Environment.Is64BitProcess)
            {
                return new BigIntegerNative(value);
            }
            else
            {
                return new BigIntegerNative((uint)value);
            }
        }

        public static BigIntegerNative operator &(BigIntegerNative left, BigIntegerNative right)
        {
            if (left.IsZero || right.IsZero)
            {
                return Zero;
            }

            if (left._bits is null && right._bits is null)
            {
                return left._sign & right._sign;
            }

            nuint xExtend = left._sign < 0 ? nuint.MaxValue : 0;
            nuint yExtend = right._sign < 0 ? nuint.MaxValue : 0;

            nuint[]? leftBufferFromPool = null;
            int size = (left._bits?.Length ?? 1) + 1;
            Span<nuint> x = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : leftBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            x = x.Slice(0, left.WriteTo(x));

            nuint[]? rightBufferFromPool = null;
            size = (right._bits?.Length ?? 1) + 1;
            Span<nuint> y = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : rightBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            y = y.Slice(0, right.WriteTo(y));

            nuint[]? resultBufferFromPool = null;
            size = Math.Max(x.Length, y.Length);
            Span<nuint> z = (size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : resultBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

            for (int i = 0; i < z.Length; i++)
            {
                nuint xu = (uint)i < (uint)x.Length ? x[i] : xExtend;
                nuint yu = (uint)i < (uint)y.Length ? y[i] : yExtend;
                z[i] = xu & yu;
            }

            if (leftBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(leftBufferFromPool);

            if (rightBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(rightBufferFromPool);

            var result = new BigIntegerNative(z);

            if (resultBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(resultBufferFromPool);

            return result;
        }

        public static BigIntegerNative operator |(BigIntegerNative left, BigIntegerNative right)
        {
            if (left.IsZero)
                return right;
            if (right.IsZero)
                return left;

            if (left._bits is null && right._bits is null)
            {
                return left._sign | right._sign;
            }

            nuint xExtend = left._sign < 0 ? nuint.MaxValue : 0;
            nuint yExtend = right._sign < 0 ? nuint.MaxValue : 0;

            nuint[]? leftBufferFromPool = null;
            int size = (left._bits?.Length ?? 1) + 1;
            Span<nuint> x = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : leftBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            x = x.Slice(0, left.WriteTo(x));

            nuint[]? rightBufferFromPool = null;
            size = (right._bits?.Length ?? 1) + 1;
            Span<nuint> y = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : rightBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            y = y.Slice(0, right.WriteTo(y));

            nuint[]? resultBufferFromPool = null;
            size = Math.Max(x.Length, y.Length);
            Span<nuint> z = (size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : resultBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

            for (int i = 0; i < z.Length; i++)
            {
                nuint xu = (uint)i < (uint)x.Length ? x[i] : xExtend;
                nuint yu = (uint)i < (uint)y.Length ? y[i] : yExtend;
                z[i] = xu | yu;
            }

            if (leftBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(leftBufferFromPool);

            if (rightBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(rightBufferFromPool);

            var result = new BigIntegerNative(z);

            if (resultBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(resultBufferFromPool);

            return result;
        }

        public static BigIntegerNative operator ^(BigIntegerNative left, BigIntegerNative right)
        {
            if (left._bits is null && right._bits is null)
            {
                return left._sign ^ right._sign;
            }

            nuint xExtend = left._sign < 0 ? nuint.MaxValue : 0;
            nuint yExtend = right._sign < 0 ? nuint.MaxValue : 0;

            nuint[]? leftBufferFromPool = null;
            int size = (left._bits?.Length ?? 1) + 1;
            Span<nuint> x = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : leftBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            x = x.Slice(0, left.WriteTo(x));

            nuint[]? rightBufferFromPool = null;
            size = (right._bits?.Length ?? 1) + 1;
            Span<nuint> y = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : rightBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
            y = y.Slice(0, right.WriteTo(y));

            nuint[]? resultBufferFromPool = null;
            size = Math.Max(x.Length, y.Length);
            Span<nuint> z = (size <= BigIntegerCalculator.StackAllocThreshold
                         ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                         : resultBufferFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

            for (int i = 0; i < z.Length; i++)
            {
                nuint xu = (uint)i < (uint)x.Length ? x[i] : xExtend;
                nuint yu = (uint)i < (uint)y.Length ? y[i] : yExtend;
                z[i] = xu ^ yu;
            }

            if (leftBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(leftBufferFromPool);

            if (rightBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(rightBufferFromPool);

            var result = new BigIntegerNative(z);

            if (resultBufferFromPool != null)
                ArrayPool<nuint>.Shared.Return(resultBufferFromPool);

            return result;
        }

        public static BigIntegerNative operator <<(BigIntegerNative value, int shift)
        {
            if (shift == 0)
                return value;

            if (shift == int.MinValue)
                return value >> int.MaxValue >> 1;

            if (shift < 0)
                return value >> -shift;

            int digitShift = Math.DivRem(shift, kcbitNUint, out int smallShift);

            nuint[]? xdFromPool = null;
            int xl = value._bits?.Length ?? 1;
            Span<nuint> xd = (xl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : xdFromPool = ArrayPool<nuint>.Shared.Rent(xl)).Slice(0, xl);
            bool negx = value.GetPartsForBitManipulation(xd);

            int zl = xl + digitShift + 1;
            nuint[]? zdFromPool = null;
            Span<nuint> zd = ((uint)zl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : zdFromPool = ArrayPool<nuint>.Shared.Rent(zl)).Slice(0, zl);
            zd.Clear();

            nuint carry = 0;
            if (smallShift == 0)
            {
                for (int i = 0; i < xd.Length; i++)
                {
                    zd[i + digitShift] = xd[i];
                }
            }
            else
            {
                int carryShift = kcbitNUint - smallShift;
                int i;
                for (i = 0; i < xd.Length; i++)
                {
                    nuint rot = xd[i];
                    zd[i + digitShift] = rot << smallShift | carry;
                    carry = rot >> carryShift;
                }
            }

            zd[zd.Length - 1] = carry;

            var result = new BigIntegerNative(zd, negx);

            if (xdFromPool != null)
                ArrayPool<nuint>.Shared.Return(xdFromPool);
            if (zdFromPool != null)
                ArrayPool<nuint>.Shared.Return(zdFromPool);

            return result;
        }

        public static BigIntegerNative operator >>(BigIntegerNative value, int shift)
        {
            if (shift == 0)
                return value;

            if (shift == int.MinValue)
                return value << int.MaxValue << 1;

            if (shift < 0)
                return value << -shift;

            int digitShift = Math.DivRem(shift, kcbitNUint, out int smallShift);

            BigIntegerNative result;

            nuint[]? xdFromPool = null;
            int xl = value._bits?.Length ?? 1;
            Span<nuint> xd = (xl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : xdFromPool = ArrayPool<nuint>.Shared.Rent(xl)).Slice(0, xl);

            bool negx = value.GetPartsForBitManipulation(xd);
            bool trackSignBit = false;

            if (negx)
            {
                if (shift >= (long)kcbitNUint * xd.Length)
                {
                    result = MinusOne;
                    goto exit;
                }

                NumericsHelpers.DangerousMakeTwosComplement(xd); // Mutates xd

                // For a shift of N x 32 bit,
                // We check for a special case where its sign bit could be outside the uint array after 2's complement conversion.
                // For example given [0xFFFFFFFF, 0xFFFFFFFF, 0xFFFFFFFF], its 2's complement is [0x01, 0x00, 0x00]
                // After a 32 bit right shift, it becomes [0x00, 0x00] which is [0x00, 0x00] when converted back.
                // The expected result is [0x00, 0x00, 0xFFFFFFFF] (2's complement) or [0x00, 0x00, 0x01] when converted back
                // If the 2's component's last element is a 0, we will track the sign externally
                trackSignBit = smallShift == 0 && xd[xd.Length - 1] == 0;
            }

            nuint[]? zdFromPool = null;
            int zl = Math.Max(xl - digitShift, 0) + (trackSignBit ? 1 : 0);
            Span<nuint> zd = ((uint)zl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : zdFromPool = ArrayPool<nuint>.Shared.Rent(zl)).Slice(0, zl);
            zd.Clear();

            if (smallShift == 0)
            {
                for (int i = xd.Length - 1; i >= digitShift; i--)
                {
                    zd[i - digitShift] = xd[i];
                }
            }
            else
            {
                int carryShift = kcbitNUint - smallShift;
                nuint carry = 0;
                for (int i = xd.Length - 1; i >= digitShift; i--)
                {
                    nuint rot = xd[i];
                    if (negx && i == xd.Length - 1)
                        // Sign-extend the first shift for negative ints then let the carry propagate
                        zd[i - digitShift] = rot >> smallShift | 0xFFFFFFFF << carryShift;
                    else
                        zd[i - digitShift] = rot >> smallShift | carry;
                    carry = rot << carryShift;
                }
            }

            if (negx)
            {
                // Set the tracked sign to the last element
                if (trackSignBit)
                    zd[zd.Length - 1] = 0xFFFFFFFF;

                NumericsHelpers.DangerousMakeTwosComplement(zd); // Mutates zd
            }

            result = new BigIntegerNative(zd, negx);

            if (zdFromPool != null)
                ArrayPool<nuint>.Shared.Return(zdFromPool);
            exit:
            if (xdFromPool != null)
                ArrayPool<nuint>.Shared.Return(xdFromPool);

            return result;
        }

        public static BigIntegerNative operator ~(BigIntegerNative value)
        {
            return -(value + One);
        }

        public static BigIntegerNative operator -(BigIntegerNative value)
        {
            value.AssertValid();
            return new BigIntegerNative(-value._sign, value._bits);
        }

        public static BigIntegerNative operator +(BigIntegerNative value)
        {
            value.AssertValid();
            return value;
        }

        public static BigIntegerNative operator ++(BigIntegerNative value)
        {
            return value + One;
        }

        public static BigIntegerNative operator --(BigIntegerNative value)
        {
            return value - One;
        }

        public static BigIntegerNative operator +(BigIntegerNative left, BigIntegerNative right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._bits == null && right._bits == null)
                return (long)left._sign + right._sign;

            if (left._sign < 0 != right._sign < 0)
                return Subtract(left._bits, left._sign, right._bits, -1 * right._sign);
            return Add(left._bits, left._sign, right._bits, right._sign);
        }

        public static BigIntegerNative operator *(BigIntegerNative left, BigIntegerNative right)
        {
            left.AssertValid();
            right.AssertValid();

            if (left._bits == null && right._bits == null)
                return (long)left._sign * right._sign;

            return Multiply(left._bits, left._sign, right._bits, right._sign);
        }

        private static BigIntegerNative Multiply(ReadOnlySpan<nuint> left, int leftSign, ReadOnlySpan<nuint> right, int rightSign)
        {
            bool trivialLeft = left.IsEmpty;
            bool trivialRight = right.IsEmpty;

            Debug.Assert(!(trivialLeft && trivialRight), "Trivial cases should be handled on the caller operator");

            BigIntegerNative result;
            nuint[]? bitsFromPool = null;

            if (trivialLeft)
            {
                Debug.Assert(!right.IsEmpty);

                int size = right.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Multiply(right, NumericsHelpers.Abs(leftSign), bits);
                result = new BigIntegerNative(bits, leftSign < 0 ^ rightSign < 0);
            }
            else if (trivialRight)
            {
                Debug.Assert(!left.IsEmpty);

                int size = left.Length + 1;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Multiply(left, NumericsHelpers.Abs(rightSign), bits);
                result = new BigIntegerNative(bits, leftSign < 0 ^ rightSign < 0);
            }
            else if (left == right)
            {
                int size = left.Length + right.Length;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Square(left, bits);
                result = new BigIntegerNative(bits, leftSign < 0 ^ rightSign < 0);
            }
            else if (left.Length < right.Length)
            {
                Debug.Assert(!left.IsEmpty && !right.IsEmpty);

                int size = left.Length + right.Length;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Multiply(right, left, bits);
                result = new BigIntegerNative(bits, leftSign < 0 ^ rightSign < 0);
            }
            else
            {
                Debug.Assert(!left.IsEmpty && !right.IsEmpty);

                int size = left.Length + right.Length;
                Span<nuint> bits = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);
                bits.Clear();

                BigIntegerCalculator.Multiply(left, right, bits);
                result = new BigIntegerNative(bits, leftSign < 0 ^ rightSign < 0);
            }

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }

        public static BigIntegerNative operator /(BigIntegerNative dividend, BigIntegerNative divisor)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._bits == null;
            bool trivialDivisor = divisor._bits == null;

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

            nuint[]? quotientFromPool = null;

            if (trivialDivisor)
            {
                Debug.Assert(dividend._bits != null);

                int size = dividend._bits.Length;
                Span<nuint> quotient = ((uint)size <= BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                try
                {
                    //may throw DivideByZeroException
                    BigIntegerCalculator.Divide(dividend._bits, NumericsHelpers.Abs(divisor._sign), quotient);
                    return new BigIntegerNative(quotient, dividend._sign < 0 ^ divisor._sign < 0);
                }
                finally
                {
                    if (quotientFromPool != null)
                        ArrayPool<nuint>.Shared.Return(quotientFromPool);
                }
            }

            Debug.Assert(dividend._bits != null && divisor._bits != null);

            if (dividend._bits.Length < divisor._bits.Length)
            {
                return s_bnZeroInt;
            }
            else
            {
                int size = dividend._bits.Length - divisor._bits.Length + 1;
                Span<nuint> quotient = ((uint)size < BigIntegerCalculator.StackAllocThreshold
                                    ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                                    : quotientFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

                BigIntegerCalculator.Divide(dividend._bits, divisor._bits, quotient);
                var result = new BigIntegerNative(quotient, dividend._sign < 0 ^ divisor._sign < 0);

                if (quotientFromPool != null)
                    ArrayPool<nuint>.Shared.Return(quotientFromPool);

                return result;
            }
        }

        public static BigIntegerNative operator %(BigIntegerNative dividend, BigIntegerNative divisor)
        {
            dividend.AssertValid();
            divisor.AssertValid();

            bool trivialDividend = dividend._bits == null;
            bool trivialDivisor = divisor._bits == null;

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
                Debug.Assert(dividend._bits != null);
                uint remainder = BigIntegerCalculator.Remainder(dividend._bits, NumericsHelpers.Abs(divisor._sign));
                return dividend._sign < 0 ? -1 * remainder : remainder;
            }

            Debug.Assert(dividend._bits != null && divisor._bits != null);

            if (dividend._bits.Length < divisor._bits.Length)
            {
                return dividend;
            }

            nuint[]? bitsFromPool = null;
            int size = dividend._bits.Length;
            Span<nuint> bits = (size <= BigIntegerCalculator.StackAllocThreshold
                            ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                            : bitsFromPool = ArrayPool<nuint>.Shared.Rent(size)).Slice(0, size);

            BigIntegerCalculator.Remainder(dividend._bits, divisor._bits, bits);
            var result = new BigIntegerNative(bits, dividend._sign < 0);

            if (bitsFromPool != null)
                ArrayPool<nuint>.Shared.Return(bitsFromPool);

            return result;
        }

        public static bool operator <(BigIntegerNative left, BigIntegerNative right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerNative left, BigIntegerNative right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerNative left, BigIntegerNative right)
        {
            return left.CompareTo(right) > 0;
        }
        public static bool operator >=(BigIntegerNative left, BigIntegerNative right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerNative left, BigIntegerNative right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerNative left, BigIntegerNative right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(BigIntegerNative left, long right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerNative left, long right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerNative left, long right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BigIntegerNative left, long right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerNative left, long right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerNative left, long right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(long left, BigIntegerNative right)
        {
            return right.CompareTo(left) > 0;
        }

        public static bool operator <=(long left, BigIntegerNative right)
        {
            return right.CompareTo(left) >= 0;
        }

        public static bool operator >(long left, BigIntegerNative right)
        {
            return right.CompareTo(left) < 0;
        }

        public static bool operator >=(long left, BigIntegerNative right)
        {
            return right.CompareTo(left) <= 0;
        }

        public static bool operator ==(long left, BigIntegerNative right)
        {
            return right.Equals(left);
        }

        public static bool operator !=(long left, BigIntegerNative right)
        {
            return !right.Equals(left);
        }

        public static bool operator <(BigIntegerNative left, ulong right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(BigIntegerNative left, ulong right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(BigIntegerNative left, ulong right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(BigIntegerNative left, ulong right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static bool operator ==(BigIntegerNative left, ulong right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BigIntegerNative left, ulong right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(ulong left, BigIntegerNative right)
        {
            return right.CompareTo(left) > 0;
        }

        public static bool operator <=(ulong left, BigIntegerNative right)
        {
            return right.CompareTo(left) >= 0;
        }

        public static bool operator >(ulong left, BigIntegerNative right)
        {
            return right.CompareTo(left) < 0;
        }

        public static bool operator >=(ulong left, BigIntegerNative right)
        {
            return right.CompareTo(left) <= 0;
        }

        public static bool operator ==(ulong left, BigIntegerNative right)
        {
            return right.Equals(left);
        }

        public static bool operator !=(ulong left, BigIntegerNative right)
        {
            return !right.Equals(left);
        }

        /// <summary>
        /// Gets the number of bits required for shortest two's complement representation of the current instance without the sign bit.
        /// </summary>
        /// <returns>The minimum non-negative number of bits in two's complement notation without the sign bit.</returns>
        /// <remarks>This method returns 0 iff the value of current object is equal to <see cref="Zero"/> or <see cref="MinusOne"/>. For positive integers the return value is equal to the ordinary binary representation string length.</remarks>
        public long GetBitLength()
        {
            AssertValid();

            nuint highValue;
            int bitsArrayLength;
            int sign = _sign;
            nuint[]? bits = _bits;

            if (bits == null)
            {
                bitsArrayLength = 1;
                highValue = (nuint)(sign < 0 ? -sign : sign);
            }
            else
            {
                bitsArrayLength = bits.Length;
                highValue = bits[bitsArrayLength - 1];
            }

            long bitLength = bitsArrayLength * kcbitNUint - BitOperations.LeadingZeroCount(highValue);

            if (sign >= 0)
                return bitLength;

            // When negative and IsPowerOfTwo, the answer is (bitLength - 1)

            // Check highValue
            if ((highValue & highValue - 1) != 0)
                return bitLength;

            // Check the rest of the bits (if present)
            for (int i = bitsArrayLength - 2; i >= 0; i--)
            {
                // bits array is always non-null when bitsArrayLength >= 2
                if (bits![i] == 0)
                    continue;

                return bitLength;
            }

            return bitLength - 1;
        }

        /// <summary>
        /// Encapsulate the logic of normalizing the "small" and "large" forms of BigInteger
        /// into the "large" form so that Bit Manipulation algorithms can be simplified.
        /// </summary>
        /// <param name="xd">
        /// The nuint array containing the entire big integer in "large" (denormalized) form.
        /// E.g., the number one (1) and negative one (-1) are both stored as 0x00000001
        ///  BigIntegerNative  values Int32.MinValue &lt; x &lt;= Int32.MaxValue are converted to this
        /// format for convenience.
        /// </param>
        /// <returns>True for negative numbers.</returns>
        private bool GetPartsForBitManipulation(Span<nuint> xd)
        {
            Debug.Assert(_bits is null ? xd.Length == 1 : xd.Length == _bits.Length);

            if (_bits is null)
            {
                xd[0] = (nuint)(_sign < 0 ? -_sign : _sign);
            }
            else
            {
                _bits.CopyTo(xd);
            }
            return _sign < 0;
        }

        [Conditional("DEBUG")]
        private void AssertValid()
        {
            if (_bits != null)
            {
                // _sign must be +1 or -1 when _bits is non-null
                Debug.Assert(_sign == 1 || _sign == -1);
                // _bits must contain at least 1 element or be null
                Debug.Assert(_bits.Length > 0);
                // Wasted space: _bits[0] could have been packed into _sign
                Debug.Assert(_bits.Length > 1 || _bits[0] >= kuMaskHighBit);
                // Wasted space: leading zeros could have been truncated
                Debug.Assert(_bits[_bits.Length - 1] != 0);
                // Arrays larger than this can't fit into a Span<byte>
                Debug.Assert(_bits.Length <= MaxLength);
            }
            else
            {
                // Int32.MinValue should not be stored in the _sign field
                Debug.Assert(_sign > int.MinValue);
            }
        }

        //
        // IAdditiveIdentity
        //

        /// <inheritdoc cref="IAdditiveIdentity{TSelf, TResult}.AdditiveIdentity" />
        static BigIntegerNative IAdditiveIdentity<BigIntegerNative, BigIntegerNative>.AdditiveIdentity => Zero;
        //
        // IBinaryInteger
        //

        /// <inheritdoc cref="IBinaryInteger{TSelf}.DivRem(TSelf, TSelf)" />
        public static (BigIntegerNative Quotient, BigIntegerNative Remainder) DivRem(BigIntegerNative left, BigIntegerNative right)
        {
            BigIntegerNative quotient = DivRem(left, right, out BigIntegerNative remainder);
            return (quotient, remainder);
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.LeadingZeroCount(TSelf)" />
        public static BigIntegerNative LeadingZeroCount(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return BitOperations.LeadingZeroCount((uint)value._sign);
            }

            // When the value is positive, we just need to get the lzcnt of the most significant bits
            // Otherwise, we're negative and the most significant bit is always set.

            return value._sign >= 0 ? BitOperations.LeadingZeroCount(value._bits[^1]) : 0;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.PopCount(TSelf)" />
        public static BigIntegerNative PopCount(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return BitOperations.PopCount((uint)value._sign);
            }

            ulong result = 0;

            if (value._sign >= 0)
            {
                // When the value is positive, we simply need to do a popcount for all bits

                for (int i = 0; i < value._bits.Length; i++)
                {
                    nuint part = value._bits[i];
                    result += (uint)BitOperations.PopCount(part);
                }
            }
            else
            {
                // When the value is negative, we need to popcount the two's complement representation
                // We'll do this "inline" to avoid needing to unnecessarily allocate.

                int i = 0;
                nuint part;

                do
                {
                    // Simply process bits, adding the carry while the previous value is zero

                    part = ~value._bits[i] + 1;
                    result += (uint)BitOperations.PopCount(part);

                    i++;
                }
                while (part == 0 && i < value._bits.Length);

                while (i < value._bits.Length)
                {
                    // Then process the remaining bits only utilizing the one's complement

                    part = ~value._bits[i];
                    result += (uint)BitOperations.PopCount(part);

                    i++;
                }
            }

            return result;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.RotateLeft(TSelf, int)" />
        public static BigIntegerNative RotateLeft(BigIntegerNative value, int rotateAmount)
        {
            value.AssertValid();
            int byteCount = (value._bits is null) ? sizeof(int) : (value._bits.Length * kcByteNUint);

            // Normalize the rotate amount to drop full rotations
            rotateAmount = (int)(rotateAmount % (byteCount * 8L));

            if (rotateAmount == 0)
                return value;

            if (rotateAmount == int.MinValue)
                return RotateRight(RotateRight(value, int.MaxValue), 1);

            if (rotateAmount < 0)
                return RotateRight(value, -rotateAmount);

            int digitShift = Math.DivRem(rotateAmount, kcbitNUint, out int smallShift);

            nuint[]? xdFromPool = null;
            int xl = value._bits?.Length ?? 1;

            Span<nuint> xd = xl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : xdFromPool = ArrayPool<nuint>.Shared.Rent(xl);
            xd = xd.Slice(0, xl);

            bool negx = value.GetPartsForBitManipulation(xd);

            int zl = xl;
            nuint[]? zdFromPool = null;

            Span<nuint> zd = zl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : zdFromPool = ArrayPool<nuint>.Shared.Rent(zl);
            zd = zd.Slice(0, zl);

            zd.Clear();

            if (negx)
            {
                NumericsHelpers.DangerousMakeTwosComplement(xd);
            }

            if (smallShift == 0)
            {
                int dstIndex = 0;
                int srcIndex = xd.Length - digitShift;

                do
                {
                    // Copy last digitShift elements from xd to the start of zd
                    zd[dstIndex] = xd[srcIndex];

                    dstIndex++;
                    srcIndex++;
                }
                while (srcIndex < xd.Length);

                srcIndex = 0;

                while (dstIndex < zd.Length)
                {
                    // Copy remaining elements from start of xd to end of zd
                    zd[dstIndex] = xd[srcIndex];

                    dstIndex++;
                    srcIndex++;
                }
            }
            else
            {
                int carryShift = kcbitNUint - smallShift;

                int dstIndex = 0;
                int srcIndex = 0;

                nuint carry = 0;

                if (digitShift == 0)
                {
                    carry = xd[^1] >> carryShift;
                }
                else
                {
                    srcIndex = xd.Length - digitShift;
                    carry = xd[srcIndex - 1] >> carryShift;
                }

                do
                {
                    nuint part = xd[srcIndex];

                    zd[dstIndex] = part << smallShift | carry;
                    carry = part >> carryShift;

                    dstIndex++;
                    srcIndex++;
                }
                while (srcIndex < xd.Length);

                srcIndex = 0;

                while (dstIndex < zd.Length)
                {
                    nuint part = xd[srcIndex];

                    zd[dstIndex] = part << smallShift | carry;
                    carry = part >> carryShift;

                    dstIndex++;
                    srcIndex++;
                }
            }

            if (negx && (int)zd[^1] < 0)
            {
                NumericsHelpers.DangerousMakeTwosComplement(zd);
            }
            else
            {
                negx = false;
            }

            var result = new BigIntegerNative(zd, negx);

            if (xdFromPool != null)
                ArrayPool<nuint>.Shared.Return(xdFromPool);
            if (zdFromPool != null)
                ArrayPool<nuint>.Shared.Return(zdFromPool);

            return result;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.RotateRight(TSelf, int)" />
        public static BigIntegerNative RotateRight(BigIntegerNative value, int rotateAmount)
        {
            value.AssertValid();
            int byteCount = value._bits is null ? sizeof(int) : value._bits.Length * kcByteNUint;

            // Normalize the rotate amount to drop full rotations
            rotateAmount = (int)(rotateAmount % (byteCount * 8L));

            if (rotateAmount == 0)
                return value;

            if (rotateAmount == int.MinValue)
                return RotateLeft(RotateLeft(value, int.MaxValue), 1);

            if (rotateAmount < 0)
                return RotateLeft(value, -rotateAmount);

            int digitShift = Math.DivRem(rotateAmount, kcbitNUint, out int smallShift);

            nuint[]? xdFromPool = null;
            int xl = value._bits?.Length ?? 1;

            Span<nuint> xd = xl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : xdFromPool = ArrayPool<nuint>.Shared.Rent(xl);
            xd = xd.Slice(0, xl);

            bool negx = value.GetPartsForBitManipulation(xd);

            int zl = xl;
            nuint[]? zdFromPool = null;

            Span<nuint> zd = zl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : zdFromPool = ArrayPool<nuint>.Shared.Rent(zl);
            zd = zd.Slice(0, zl);

            zd.Clear();

            if (negx)
            {
                NumericsHelpers.DangerousMakeTwosComplement(xd);
            }

            if (smallShift == 0)
            {
                int dstIndex = 0;
                int srcIndex = digitShift;

                do
                {
                    // Copy first digitShift elements from xd to the end of zd
                    zd[dstIndex] = xd[srcIndex];

                    dstIndex++;
                    srcIndex++;
                }
                while (srcIndex < xd.Length);

                srcIndex = 0;

                while (dstIndex < zd.Length)
                {
                    // Copy remaining elements from end of xd to start of zd
                    zd[dstIndex] = xd[srcIndex];

                    dstIndex++;
                    srcIndex++;
                }
            }
            else
            {
                int carryShift = kcbitNUint - smallShift;

                int dstIndex = 0;
                int srcIndex = digitShift;

                nuint carry = 0;

                if (digitShift == 0)
                {
                    carry = xd[^1] << carryShift;
                }
                else
                {
                    carry = xd[srcIndex - 1] << carryShift;
                }

                do
                {
                    nuint part = xd[srcIndex];

                    zd[dstIndex] = part >> smallShift | carry;
                    carry = part << carryShift;

                    dstIndex++;
                    srcIndex++;
                }
                while (srcIndex < xd.Length);

                srcIndex = 0;

                while (dstIndex < zd.Length)
                {
                    nuint part = xd[srcIndex];

                    zd[dstIndex] = part >> smallShift | carry;
                    carry = part << carryShift;

                    dstIndex++;
                    srcIndex++;
                }
            }

            if (negx && (int)zd[^1] < 0)
            {
                NumericsHelpers.DangerousMakeTwosComplement(zd);
            }
            else
            {
                negx = false;
            }

            var result = new BigIntegerNative(zd, negx);

            if (xdFromPool != null)
                ArrayPool<nuint>.Shared.Return(xdFromPool);
            if (zdFromPool != null)
                ArrayPool<nuint>.Shared.Return(zdFromPool);

            return result;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.TrailingZeroCount(TSelf)" />
        public static BigIntegerNative TrailingZeroCount(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return BitOperations.TrailingZeroCount(value._sign);
            }

            ulong result = 0;

            // Both positive values and their two's-complement negative representation will share the same TrailingZeroCount,
            // so the sign of value does not matter and both cases can be handled in the same way

            nuint part = value._bits[0];

            for (int i = 1; part == 0 && i < value._bits.Length; i++)
            {
                part = value._bits[i];
                result += (uint)kcbitNUint;
            }

            result += (uint)BitOperations.TrailingZeroCount(part);

            return result;
        }
        /// <inheritdoc cref="IBinaryInteger{TSelf}.TryReadBigEndian(ReadOnlySpan{byte}, bool, out TSelf)" />
        static bool IBinaryInteger<BigIntegerNative>.TryReadBigEndian(ReadOnlySpan<byte> source, bool isUnsigned, out BigIntegerNative value)
        {
            value = new BigIntegerNative(source, isUnsigned, isBigEndian: true);
            return true;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.TryReadLittleEndian(ReadOnlySpan{byte}, bool, out TSelf)" />
        static bool IBinaryInteger<BigIntegerNative>.TryReadLittleEndian(ReadOnlySpan<byte> source, bool isUnsigned, out BigIntegerNative value)
        {
            value = new BigIntegerNative(source, isUnsigned, isBigEndian: false);
            return true;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.GetShortestBitLength()" />
        int IBinaryInteger<BigIntegerNative>.GetShortestBitLength()
        {
            AssertValid();
            nuint[]? bits = _bits;

            if (bits is null)
            {
                int value = _sign;

                if (value >= 0)
                {
                    return kcbitNUint - BitOperations.LeadingZeroCount((uint)value);
                }
                else
                {
                    return kcbitNUint + 1 - BitOperations.LeadingZeroCount((uint)~value);
                }
            }

            int result = (bits.Length - 1) * kcbitNUint;

            if (_sign >= 0)
            {
                result += kcbitNUint - BitOperations.LeadingZeroCount(bits[^1]);
            }
            else
            {
                nuint part = ~bits[^1] + 1;

                // We need to remove the "carry" (the +1) if any of the initial
                // bytes are not zero. This ensures we get the correct two's complement
                // part for the computation.

                for (int index = 0; index < bits.Length - 1; index++)
                {
                    if (bits[index] != 0)
                    {
                        part -= 1;
                        break;
                    }
                }

                result += kcbitNUint + 1 - BitOperations.LeadingZeroCount(~part);
            }

            return result;
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.GetByteCount()" />
        int IBinaryInteger<BigIntegerNative>.GetByteCount() => GetGenericMathByteCount();

        /// <inheritdoc cref="IBinaryInteger{TSelf}.TryWriteBigEndian(Span{byte}, out int)" />
        bool IBinaryInteger<BigIntegerNative>.TryWriteBigEndian(Span<byte> destination, out int bytesWritten)
        {
            AssertValid();
            nuint[]? bits = _bits;

            int byteCount = GetGenericMathByteCount();

            if (destination.Length >= byteCount)
            {
                if (bits is null)
                {
                    int value = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(_sign) : _sign;
                    Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
                }
                else if (_sign >= 0)
                {
                    // When the value is positive, we simply need to copy all bits as big endian

                    ref byte startAddress = ref MemoryMarshal.GetReference(destination);
                    ref byte address = ref Unsafe.Add(ref startAddress, (bits.Length - 1) * kcByteNUint);

                    for (int i = 0; i < bits.Length; i++)
                    {
                        nuint part = bits[i];

                        if (BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Subtract(ref address, kcByteNUint);
                    }
                }
                else
                {
                    // When the value is negative, we need to copy the two's complement representation
                    // We'll do this "inline" to avoid needing to unnecessarily allocate.

                    ref byte startAddress = ref MemoryMarshal.GetReference(destination);
                    ref byte address = ref Unsafe.Add(ref startAddress, byteCount - kcByteNUint);

                    int i = 0;
                    nuint part;

                    do
                    {
                        // first do complement and +1 as long as carry is needed
                        part = ~bits[i] + 1;

                        if (BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Subtract(ref address, kcByteNUint);

                        i++;
                    }
                    while (part == 0 && i < bits.Length);

                    while (i < bits.Length)
                    {
                        // now ones complement is sufficient
                        part = ~bits[i];

                        if (BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Subtract(ref address, kcByteNUint);

                        i++;
                    }

                    if (Unsafe.AreSame(ref address, ref startAddress))
                    {
                        // We need one extra part to represent the sign as the most
                        // significant bit of the two's complement value was 0.
                        Unsafe.WriteUnaligned(ref address, nuint.MaxValue);
                    }
                    else
                    {
                        // Otherwise we should have been precisely one part behind address
                        Debug.Assert(Unsafe.AreSame(ref startAddress, ref Unsafe.Add(ref address, kcByteNUint)));
                    }
                }

                bytesWritten = byteCount;
                return true;
            }
            else
            {
                bytesWritten = 0;
                return false;
            }
        }

        /// <inheritdoc cref="IBinaryInteger{TSelf}.TryWriteLittleEndian(Span{byte}, out int)" />
        bool IBinaryInteger<BigIntegerNative>.TryWriteLittleEndian(Span<byte> destination, out int bytesWritten)
        {
            AssertValid();
            nuint[]? bits = _bits;

            int byteCount = GetGenericMathByteCount();

            if (destination.Length >= byteCount)
            {
                if (bits is null)
                {
                    int value = BitConverter.IsLittleEndian ? _sign : BinaryPrimitives.ReverseEndianness(_sign);
                    Unsafe.WriteUnaligned(ref MemoryMarshal.GetReference(destination), value);
                }
                else if (_sign >= 0)
                {
                    // When the value is positive, we simply need to copy all bits as little endian

                    ref byte address = ref MemoryMarshal.GetReference(destination);

                    for (int i = 0; i < bits.Length; i++)
                    {
                        nuint part = bits[i];

                        if (!BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Add(ref address, kcByteNUint);
                    }
                }
                else
                {
                    // When the value is negative, we need to copy the two's complement representation
                    // We'll do this "inline" to avoid needing to unnecessarily allocate.

                    ref byte address = ref MemoryMarshal.GetReference(destination);
                    ref byte lastAddress = ref Unsafe.Add(ref address, byteCount - kcByteNUint);

                    int i = 0;
                    nuint part;

                    do
                    {
                        // first do complement and +1 as long as carry is needed
                        part = ~bits[i] + 1;

                        if (!BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Add(ref address, kcByteNUint);

                        i++;
                    }
                    while (part == 0 && i < bits.Length);

                    while (i < bits.Length)
                    {
                        // now ones complement is sufficient
                        part = ~bits[i];

                        if (!BitConverter.IsLittleEndian)
                        {
#if NET8_0_OR_GREATER
                            part = BinaryPrimitives.ReverseEndianness(part);
#else
                            if (Environment.Is64BitProcess)
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((ulong)part);
                            }
                            else
                            {
                                part = (nuint)BinaryPrimitives.ReverseEndianness((uint)part);
                            }
#endif
                        }

                        Unsafe.WriteUnaligned(ref address, part);
                        address = ref Unsafe.Add(ref address, kcByteNUint);

                        i++;
                    }

                    if (Unsafe.AreSame(ref address, ref lastAddress))
                    {
                        // We need one extra part to represent the sign as the most
                        // significant bit of the two's complement value was 0.
                        Unsafe.WriteUnaligned(ref address, nuint.MaxValue);
                    }
                    else
                    {
                        // Otherwise we should have been precisely one part ahead address
                        Debug.Assert(Unsafe.AreSame(ref lastAddress, ref Unsafe.Subtract(ref address, kcByteNUint)));
                    }
                }

                bytesWritten = byteCount;
                return true;
            }
            else
            {
                bytesWritten = 0;
                return false;
            }
        }

        private int GetGenericMathByteCount()
        {
            AssertValid();
            nuint[]? bits = _bits;

            if (bits is null)
            {
                return sizeof(int);
            }

            int result = bits.Length * kcByteNUint;

            if (_sign < 0)
            {
                nuint part = ~bits[^1] + 1;

                // We need to remove the "carry" (the +1) if any of the initial
                // bytes are not zero. This ensures we get the correct two's complement
                // part for the computation.

                for (int index = 0; index < bits.Length - 1; index++)
                {
                    if (bits[index] != 0)
                    {
                        part -= 1;
                        break;
                    }
                }

                if ((int)part >= 0)
                {
                    // When the most significant bit of the part is zero
                    // we need another part to represent the value.
                    result += kcByteNUint;
                }
            }

            return result;
        }

        //
        // IBinaryNumber
        //

        /// <inheritdoc cref="IBinaryNumber{TSelf}.AllBitsSet" />
        static BigIntegerNative IBinaryNumber<BigIntegerNative>.AllBitsSet => MinusOne;

        /// <inheritdoc cref="IBinaryNumber{TSelf}.IsPow2(TSelf)" />
        public static bool IsPow2(BigIntegerNative value) => value.IsPowerOfTwo;

        /// <inheritdoc cref="IBinaryNumber{TSelf}.Log2(TSelf)" />
        public static BigIntegerNative Log2(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._sign < 0)
            {
                ThrowHelper.ThrowValueArgumentOutOfRange_NeedNonNegNumException();
            }

            if (value._bits is null)
            {
                return 31 ^ BitOperations.LeadingZeroCount((uint)(value._sign | 1));
            }

            return ((value._bits.Length * kcByteNUint) - 1) ^ BitOperations.LeadingZeroCount(value._bits[^1]);
        }
        //
        // IMultiplicativeIdentity
        //

        /// <inheritdoc cref="IMultiplicativeIdentity{TSelf, TResult}.MultiplicativeIdentity" />
        static BigIntegerNative IMultiplicativeIdentity<BigIntegerNative, BigIntegerNative>.MultiplicativeIdentity => One;
        //
        // INumber
        //

        /// <inheritdoc cref="INumber{TSelf}.Clamp(TSelf, TSelf, TSelf)" />
        public static BigIntegerNative Clamp(BigIntegerNative value, BigIntegerNative min, BigIntegerNative max)
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
        public static BigIntegerNative CopySign(BigIntegerNative value, BigIntegerNative sign)
        {
            value.AssertValid();
            sign.AssertValid();

            int currentSign = value._sign;

            if (value._bits is null)
            {
                currentSign = currentSign >= 0 ? 1 : -1;
            }

            int targetSign = sign._sign;

            if (sign._bits is null)
            {
                targetSign = targetSign >= 0 ? 1 : -1;
            }

            return currentSign == targetSign ? value : -value;
        }

        /// <inheritdoc cref="INumber{TSelf}.MaxNumber(TSelf, TSelf)" />
        static BigIntegerNative INumber<BigIntegerNative>.MaxNumber(BigIntegerNative x, BigIntegerNative y) => Max(x, y);

        /// <inheritdoc cref="INumber{TSelf}.MinNumber(TSelf, TSelf)" />
        static BigIntegerNative INumber<BigIntegerNative>.MinNumber(BigIntegerNative x, BigIntegerNative y) => Min(x, y);

        /// <inheritdoc cref="INumber{TSelf}.Sign(TSelf)" />
        static int INumber<BigIntegerNative>.Sign(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return int.Sign(value._sign);
            }

            return value._sign;
        }


        //
        // INumberBase
        //

        /// <inheritdoc cref="INumberBase{TSelf}.Radix" />
        static int INumberBase<BigIntegerNative>.Radix => 2;

        /// <inheritdoc cref="INumberBase{TSelf}.CreateChecked{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerNative CreateChecked<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerNative result;

            if (typeof(TOther) == typeof(BigIntegerNative))
            {
                result = (BigIntegerNative)(object)value;
            }
            else if (!TryConvertFromChecked(value, out result) && !TOther.TryConvertToChecked(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.CreateSaturating{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerNative CreateSaturating<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerNative result;

            if (typeof(TOther) == typeof(BigIntegerNative))
            {
                result = (BigIntegerNative)(object)value;
            }
            else if (!TryConvertFromSaturating(value, out result) && !TOther.TryConvertToSaturating(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.CreateTruncating{TOther}(TOther)" />
        [MethodImpl(256)]
        public static BigIntegerNative CreateTruncating<TOther>(TOther value)
            where TOther : INumberBase<TOther>
        {
            BigIntegerNative result;

            if (typeof(TOther) == typeof(BigIntegerNative))
            {
                result = (BigIntegerNative)(object)value;
            }
            else if (!TryConvertFromTruncating(value, out result) && !TOther.TryConvertToTruncating(value, out result))
            {
                ThrowHelper.ThrowNotSupportedException();
            }

            return result;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsCanonical(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsCanonical(BigIntegerNative value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsComplexNumber(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsComplexNumber(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsEvenInteger(TSelf)" />
        public static bool IsEvenInteger(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return (value._sign & 1) == 0;
            }
            return (value._bits[0] & 1) == 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsFinite(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsFinite(BigIntegerNative value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsImaginaryNumber(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsImaginaryNumber(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsInfinity(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsInfinity(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsInteger(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsInteger(BigIntegerNative value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNaN(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsNaN(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNegative(TSelf)" />
        public static bool IsNegative(BigIntegerNative value)
        {
            value.AssertValid();
            return value._sign < 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsNegativeInfinity(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsNegativeInfinity(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsNormal(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsNormal(BigIntegerNative value) => value != 0;

        /// <inheritdoc cref="INumberBase{TSelf}.IsOddInteger(TSelf)" />
        public static bool IsOddInteger(BigIntegerNative value)
        {
            value.AssertValid();

            if (value._bits is null)
            {
                return (value._sign & 1) != 0;
            }
            return (value._bits[0] & 1) != 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsPositive(TSelf)" />
        public static bool IsPositive(BigIntegerNative value)
        {
            value.AssertValid();
            return value._sign >= 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.IsPositiveInfinity(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsPositiveInfinity(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsRealNumber(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsRealNumber(BigIntegerNative value) => true;

        /// <inheritdoc cref="INumberBase{TSelf}.IsSubnormal(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsSubnormal(BigIntegerNative value) => false;

        /// <inheritdoc cref="INumberBase{TSelf}.IsZero(TSelf)" />
        static bool INumberBase<BigIntegerNative>.IsZero(BigIntegerNative value)
        {
            value.AssertValid();
            return value._sign == 0;
        }

        /// <inheritdoc cref="INumberBase{TSelf}.MaxMagnitude(TSelf, TSelf)" />
        public static BigIntegerNative MaxMagnitude(BigIntegerNative x, BigIntegerNative y)
        {
            x.AssertValid();
            y.AssertValid();

            BigIntegerNative ax = Abs(x);
            BigIntegerNative ay = Abs(y);

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
        static BigIntegerNative INumberBase<BigIntegerNative>.MaxMagnitudeNumber(BigIntegerNative x, BigIntegerNative y) => MaxMagnitude(x, y);

        /// <inheritdoc cref="INumberBase{TSelf}.MinMagnitude(TSelf, TSelf)" />
        public static BigIntegerNative MinMagnitude(BigIntegerNative x, BigIntegerNative y)
        {
            x.AssertValid();
            y.AssertValid();

            BigIntegerNative ax = Abs(x);
            BigIntegerNative ay = Abs(y);

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
        static BigIntegerNative INumberBase<BigIntegerNative>.MinMagnitudeNumber(BigIntegerNative x, BigIntegerNative y) => MinMagnitude(x, y);

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertFromChecked{TOther}(TOther, out TSelf)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerNative>.TryConvertFromChecked<TOther>(TOther value, out BigIntegerNative result) => TryConvertFromChecked(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromChecked<TOther>(TOther value, out BigIntegerNative result)
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
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualValue = (decimal)(object)value;
                result = (BigIntegerNative)actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualValue = (double)(object)value;
                result = checked((BigIntegerNative)actualValue);
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualValue = (Half)(object)value;
                result = checked((BigIntegerNative)actualValue);
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
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualValue = (nint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualValue = (float)(object)value;
                result = checked((BigIntegerNative)actualValue);
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
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualValue = (nuint)(object)value;
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
        static bool INumberBase<BigIntegerNative>.TryConvertFromSaturating<TOther>(TOther value, out BigIntegerNative result) => TryConvertFromSaturating(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromSaturating<TOther>(TOther value, out BigIntegerNative result)
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
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualValue = (decimal)(object)value;
                result = (BigIntegerNative)actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualValue = (double)(object)value;
                result = double.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualValue = (Half)(object)value;
                result = Half.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
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
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualValue = (nint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualValue = (float)(object)value;
                result = float.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
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
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualValue = (nuint)(object)value;
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
        static bool INumberBase<BigIntegerNative>.TryConvertFromTruncating<TOther>(TOther value, out BigIntegerNative result) => TryConvertFromTruncating(value, out result);

        [MethodImpl(256)]
        private static bool TryConvertFromTruncating<TOther>(TOther value, out BigIntegerNative result)
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
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualValue = (decimal)(object)value;
                result = (BigIntegerNative)actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualValue = (double)(object)value;
                result = double.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualValue = (Half)(object)value;
                result = Half.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
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
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualValue = (nint)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualValue = (sbyte)(object)value;
                result = actualValue;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualValue = (float)(object)value;
                result = float.IsNaN(actualValue) ? Zero : (BigIntegerNative)actualValue;
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
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualValue = (nuint)(object)value;
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
        static bool INumberBase<BigIntegerNative>.TryConvertToChecked<TOther>(BigIntegerNative value, [MaybeNullWhen(false)] out TOther result)
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
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualResult = checked((decimal)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualResult = (double)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualResult = (Half)value;
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
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualResult = checked((nint)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualResult = checked((sbyte)value);
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualResult = checked((float)value);
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
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualResult = checked((nuint)value);
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
        static bool INumberBase<BigIntegerNative>.TryConvertToSaturating<TOther>(BigIntegerNative value, [MaybeNullWhen(false)] out TOther result)
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? byte.MinValue : byte.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= byte.MaxValue ? byte.MaxValue :
                                   value._sign <= byte.MinValue ? byte.MinValue : (byte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? char.MinValue : char.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= char.MaxValue ? char.MaxValue :
                                   value._sign <= char.MinValue ? char.MinValue : (char)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualResult = value >= new Int128(0x0000_0000_FFFF_FFFF, 0xFFFF_FFFF_FFFF_FFFF) ? decimal.MaxValue :
                                       value <= new Int128(0xFFFF_FFFF_0000_0000, 0x0000_0000_0000_0001) ? decimal.MinValue : (decimal)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualResult = (double)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualResult = (Half)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? short.MinValue : short.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= short.MaxValue ? short.MaxValue :
                                   value._sign <= short.MinValue ? short.MinValue : (short)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? int.MinValue : int.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= int.MaxValue ? int.MaxValue :
                                   value._sign <= int.MinValue ? int.MinValue : value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(long))
            {
                long actualResult = value >= long.MaxValue ? long.MaxValue :
                                    value <= long.MinValue ? long.MinValue : (long)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualResult = value >= Int128.MaxValue ? Int128.MaxValue :
                                      value <= Int128.MinValue ? Int128.MinValue : (Int128)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualResult = value >= nint.MaxValue ? nint.MaxValue :
                                    value <= nint.MinValue ? nint.MinValue : (nint)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? sbyte.MinValue : sbyte.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= sbyte.MaxValue ? sbyte.MaxValue :
                                   value._sign <= sbyte.MinValue ? sbyte.MinValue : (sbyte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualResult = (float)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? ushort.MinValue : ushort.MaxValue;
                }
                else
                {
                    actualResult = value._sign >= ushort.MaxValue ? ushort.MaxValue :
                                   value._sign <= ushort.MinValue ? ushort.MinValue : (ushort)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualResult = value >= uint.MaxValue ? uint.MaxValue :
                                    IsNegative(value) ? uint.MinValue : (uint)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualResult = value >= ulong.MaxValue ? ulong.MaxValue :
                                     IsNegative(value) ? ulong.MinValue : (ulong)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualResult = value >= UInt128.MaxValue ? UInt128.MaxValue :
                                       IsNegative(value) ? UInt128.MinValue : (UInt128)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualResult = value >= nuint.MaxValue ? nuint.MaxValue :
                                     IsNegative(value) ? nuint.MinValue : (nuint)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else
            {
                result = default;
                return false;
            }
        }

        /// <inheritdoc cref="INumberBase{TSelf}.TryConvertToTruncating{TOther}(TSelf, out TOther)" />
        [MethodImpl(256)]
        static bool INumberBase<BigIntegerNative>.TryConvertToTruncating<TOther>(BigIntegerNative value, [MaybeNullWhen(false)] out TOther result)
        {
            if (typeof(TOther) == typeof(byte))
            {
                byte actualResult;

                if (value._bits is not null)
                {
                    nuint bits = value._bits[0];

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = (byte)bits;
                }
                else
                {
                    actualResult = (byte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(char))
            {
                char actualResult;

                if (value._bits is not null)
                {
                    nuint bits = value._bits[0];

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = (char)bits;
                }
                else
                {
                    actualResult = (char)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(decimal))
            {
                decimal actualResult = value >= new Int128(0x0000_0000_FFFF_FFFF, 0xFFFF_FFFF_FFFF_FFFF) ? decimal.MaxValue :
                                       value <= new Int128(0xFFFF_FFFF_0000_0000, 0x0000_0000_0000_0001) ? decimal.MinValue : (decimal)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(double))
            {
                double actualResult = (double)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Half))
            {
                Half actualResult = (Half)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(short))
            {
                short actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? (short)(~value._bits[0] + 1) : (short)value._bits[0];
                }
                else
                {
                    actualResult = (short)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(int))
            {
                int actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? (int)(~value._bits[0] + 1) : (int)value._bits[0];
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
                long actualResult;

                if (value._bits is not null)
                {
                    if (Environment.Is64BitProcess)
                    {
                        actualResult = IsNegative(value) ? (int)(~value._bits[0] + 1) : (int)value._bits[0];
                    }
                    else
                    {
                        ulong bits = 0;

                        if (value._bits.Length >= 2)
                        {
                            bits = value._bits[1];
                            bits <<= 32;
                        }

                        bits |= value._bits[0];

                        if (IsNegative(value))
                        {
                            bits = ~bits + 1;
                        }

                        actualResult = (long)bits;
                    }
                }
                else
                {
                    actualResult = value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(Int128))
            {
                Int128 actualResult;

                if (value._bits is not null)
                {
                    ulong lowerBits = 0;
                    ulong upperBits = 0;

                    if (Environment.Is64BitProcess)
                    {
                        if (value._bits.Length >= 2)
                        {
                            upperBits = value._bits[1];
                        }
                        lowerBits = value._bits[0];
                    }
                    else
                    {
                        if (value._bits.Length >= 4)
                        {
                            upperBits = value._bits[3];
                            upperBits <<= 32;
                        }

                        if (value._bits.Length >= 3)
                        {
                            upperBits |= value._bits[2];
                        }

                        if (value._bits.Length >= 2)
                        {
                            lowerBits = value._bits[1];
                            lowerBits <<= 32;
                        }

                        lowerBits |= value._bits[0];
                    }
                    UInt128 bits = new UInt128(upperBits, lowerBits);

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = (Int128)bits;
                }
                else
                {
                    actualResult = value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(nint))
            {
                nint actualResult;

                if (value._bits is not null)
                {
                    actualResult = (nint)value._bits[0];
                }
                else
                {
                    actualResult = value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(sbyte))
            {
                sbyte actualResult;

                if (value._bits is not null)
                {
                    actualResult = IsNegative(value) ? (sbyte)(~value._bits[0] + 1) : (sbyte)value._bits[0];
                }
                else
                {
                    actualResult = (sbyte)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(float))
            {
                float actualResult = (float)value;
                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ushort))
            {
                ushort actualResult;

                if (value._bits is not null)
                {
                    nuint bits = value._bits[0];

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = (ushort)bits;
                }
                else
                {
                    actualResult = (ushort)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(uint))
            {
                uint actualResult;

                if (value._bits is not null)
                {
                    nuint bits = value._bits[0];

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = (uint)bits;
                }
                else
                {
                    actualResult = (uint)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(ulong))
            {
                ulong actualResult;

                if (value._bits is not null)
                {
                    if (Environment.Is64BitProcess)
                    {
                        actualResult = value._bits[0];
                    }
                    else
                    {
                        ulong bits = 0;

                        if (value._bits.Length >= 2)
                        {
                            bits = value._bits[1];
                            bits <<= 32;
                        }

                        bits |= value._bits[0];

                        if (IsNegative(value))
                        {
                            bits = ~bits + 1;
                        }

                        actualResult = bits;
                    }
                }
                else
                {
                    actualResult = (ulong)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(UInt128))
            {
                UInt128 actualResult;

                if (value._bits is not null)
                {
                    ulong lowerBits = 0;
                    ulong upperBits = 0;

                    if (Environment.Is64BitProcess)
                    {
                        if (value._bits.Length >= 2)
                        {
                            upperBits = value._bits[1];
                        }
                        lowerBits = value._bits[0];
                    }
                    else
                    {
                        if (value._bits.Length >= 4)
                        {
                            upperBits = value._bits[3];
                            upperBits <<= 32;
                        }

                        if (value._bits.Length >= 3)
                        {
                            upperBits |= value._bits[2];
                        }

                        if (value._bits.Length >= 2)
                        {
                            lowerBits = value._bits[1];
                            lowerBits <<= 32;
                        }

                        lowerBits |= value._bits[0];
                    }
                    UInt128 bits = new UInt128(upperBits, lowerBits);

                    if (IsNegative(value))
                    {
                        bits = ~bits + 1;
                    }

                    actualResult = bits;
                }
                else
                {
                    actualResult = (UInt128)value._sign;
                }

                result = (TOther)(object)actualResult;
                return true;
            }
            else if (typeof(TOther) == typeof(nuint))
            {
                nuint actualResult;

                if (value._bits is not null)
                {
                    actualResult = value._bits[0];
                }
                else
                {
                    actualResult = (nuint)value._sign;
                }

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
        // IParsable
        //

        /// <inheritdoc cref="IParsable{TSelf}.TryParse(string?, IFormatProvider?, out TSelf)" />
        public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out BigIntegerNative result) => TryParse(s, NumberStyles.Integer, provider, out result);


        //
        // IShiftOperators
        //

        /// <inheritdoc cref="IShiftOperators{TSelf, TOther, TResult}.op_UnsignedRightShift(TSelf, TOther)" />
        public static BigIntegerNative operator >>>(BigIntegerNative value, int shiftAmount)
        {
            value.AssertValid();

            if (shiftAmount == 0)
                return value;

            if (shiftAmount == int.MinValue)
                return value << int.MaxValue << 1;

            if (shiftAmount < 0)
                return value << -shiftAmount;

            int digitShift = Math.DivRem(shiftAmount, kcbitNUint, out int smallShift);

            BigIntegerNative result;

            nuint[]? xdFromPool = null;
            int xl = value._bits?.Length ?? 1;
            Span<nuint> xd = (xl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : xdFromPool = ArrayPool<nuint>.Shared.Rent(xl)).Slice(0, xl);

            bool negx = value.GetPartsForBitManipulation(xd);

            if (negx)
            {
                if (shiftAmount >= (long)kcbitNUint * xd.Length)
                {
                    result = MinusOne;
                    goto exit;
                }

                NumericsHelpers.DangerousMakeTwosComplement(xd); // Mutates xd
            }

            nuint[]? zdFromPool = null;
            int zl = Math.Max(xl - digitShift, 0);
            Span<nuint> zd = ((uint)zl <= BigIntegerCalculator.StackAllocThreshold
                          ? stackalloc nuint[BigIntegerCalculator.StackAllocThreshold]
                          : zdFromPool = ArrayPool<nuint>.Shared.Rent(zl)).Slice(0, zl);
            zd.Clear();

            if (smallShift == 0)
            {
                for (int i = xd.Length - 1; i >= digitShift; i--)
                {
                    zd[i - digitShift] = xd[i];
                }
            }
            else
            {
                int carryShift = kcbitNUint - smallShift;
                nuint carry = 0;
                for (int i = xd.Length - 1; i >= digitShift; i--)
                {
                    nuint rot = xd[i];
                    zd[i - digitShift] = (rot >>> smallShift) | carry;
                    carry = rot << carryShift;
                }
            }

            if (negx && (int)zd[^1] < 0)
            {
                NumericsHelpers.DangerousMakeTwosComplement(zd);
            }
            else
            {
                negx = false;
            }

            result = new BigIntegerNative(zd, negx);

            if (zdFromPool != null)
                ArrayPool<nuint>.Shared.Return(zdFromPool);
            exit:
            if (xdFromPool != null)
                ArrayPool<nuint>.Shared.Return(xdFromPool);

            return result;
        }

        //
        // ISignedNumber
        //

        /// <inheritdoc cref="ISignedNumber{TSelf}.NegativeOne" />
        static BigIntegerNative ISignedNumber<BigIntegerNative>.NegativeOne => MinusOne;
        //
        // ISpanParsable
        //

        /// <inheritdoc cref="ISpanParsable{TSelf}.Parse(ReadOnlySpan{char}, IFormatProvider?)" />
        public static BigIntegerNative Parse(ReadOnlySpan<char> s, IFormatProvider? provider) => Parse(s, NumberStyles.Integer, provider);

        /// <inheritdoc cref="ISpanParsable{TSelf}.TryParse(ReadOnlySpan{char}, IFormatProvider?, out TSelf)" />
        public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, out BigIntegerNative result) => TryParse(s, NumberStyles.Integer, provider, out result);
    }
}

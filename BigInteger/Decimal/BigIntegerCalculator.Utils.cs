// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace Kzrnm.Numerics.Decimal
{
    internal static partial class BigIntegerCalculator
    {
        internal const ulong Base = 1_000_000_000_000_000_000;
        internal const ulong BaseSqrt = 1_000_000_000;
        internal const int BaseLog = 18;
        static readonly ulong[] UInt64PowersOfTen = new ulong[]
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
       };

#if DEBUG
        // Mutable for unit testing...
        internal static
#else
        internal const
#endif
        int StackAllocThreshold = 64;

        public static int Compare(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
        {
            Debug.Assert(left.Length <= right.Length || left.Slice(right.Length).Trim(0u).Length > 0);
            Debug.Assert(left.Length >= right.Length || right.Slice(left.Length).Trim(0u).Length > 0);

            if (left.Length != right.Length)
                return left.Length < right.Length ? -1 : 1;

            int iv = left.Length;
            while (--iv >= 0 && left[iv] == right[iv]) ;

            if (iv < 0)
                return 0;
            return left[iv] < right[iv] ? -1 : 1;
        }

        private static int CompareActual(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right)
        {
            if (left.Length != right.Length)
            {
                if (left.Length < right.Length)
                {
                    if (ActualLength(right.Slice(left.Length)) > 0)
                        return -1;
                    right = right.Slice(0, left.Length);
                }
                else
                {
                    if (ActualLength(left.Slice(right.Length)) > 0)
                        return +1;
                    left = left.Slice(0, right.Length);
                }
            }
            return Compare(left, right);
        }

        private static int ActualLength(ReadOnlySpan<ulong> value)
        {
            // Since we're reusing memory here, the actual length
            // of a given value may be less then the array's length

            int length = value.Length;

            while (length > 0 && value[length - 1] == 0)
                --length;
            return length;
        }

        private static int Reduce(Span<ulong> bits, ReadOnlySpan<ulong> modulus)
        {
            // Executes a modulo operation using the divide operation.

            if (bits.Length >= modulus.Length)
            {
                DivRem(bits, modulus, default);

                return ActualLength(bits.Slice(0, modulus.Length));
            }
            return bits.Length;
        }

        [Conditional("DEBUG")]
        public static void DummyForDebug(Span<ulong> bits)
        {
            // Reproduce the case where the return value of `stackalloc uint` is not initialized to zero.
            bits.Fill(0xCD);
        }

        [MethodImpl(256)]
        public static ulong DivRem(ulong hi, ulong lo, ulong d, out ulong rem)
        {
            Debug.Assert(hi < d);
            ulong q;
            if (hi == 0)
            {
                (q, rem) = Math.DivRem(lo, d);
                return q;
            }
            // To 64 bits
            hi = Math.BigMul(hi, Base, out var hilo2);
            lo += hilo2;
            if (lo < hilo2)
                ++hi;

            return DivRem64(hi, lo, d, out rem);
        }

        [MethodImpl(256)]
        public static UInt128 DivRem128(ulong hi, ulong lo, ulong d, out ulong rem)
        {
            if (hi < d)
                return new(0, DivRem64(hi, lo, d, out rem));

            var qhi = DivRem64(0, hi, d, out var r);
            return new UInt128(qhi, DivRem64(r, lo, d, out rem));
        }

        [MethodImpl(256)]
        public static ulong DivRem64(ulong hi, ulong lo, ulong d, out ulong rem)
        {
            Debug.Assert(hi < d);
            ulong q;
            if (hi == 0)
            {
                (q, rem) = Math.DivRem(lo, d);
                return q;
            }

            int shift = BitOperations.LeadingZeroCount(d);
            if (shift != 0)
            {
                int backShift = 64 - shift;
                d <<= shift;
                hi = (hi << shift) | (lo >> backShift);
                lo <<= shift;
            }

            var lohi = lo >> 32;
            var lolo = (uint)lo;

            q = D3n2n(hi, lohi, d, out ulong r1) << 32;
            q |= D3n2n(r1, lolo, d, out rem);
            if (shift != 0)
                rem >>= shift;
            return q;

            [MethodImpl(256)]
            static ulong D3n2n(ulong a12, ulong a3, ulong b, out ulong rem)
            {
                var b1 = b >> 32;
                var b2 = (uint)b;
                ulong quo;
                (quo, rem) = Math.DivRem(a12, b1);
                var d = quo * b2;

                var hi = 0ul;
                rem <<= 32;

                rem += a3;
                if (rem < a3) ++hi;
                if (rem < d) --hi;
                rem -= d;
                while (hi != 0)
                {
                    --quo;
                    rem += b;
                    if (rem < b) ++hi;
                }

                return quo;
            }
        }

        /// <summary>
        /// [Return, <paramref name="low"/>] <paramref name="a"/> * <paramref name="b"/>
        /// </summary>
        [MethodImpl(256)]
        public static ulong BigMul(ulong a, ulong b, out ulong low)
        {
            Debug.Assert(a < Base);
            Debug.Assert(b < Base);
            var (aHi, aLo) = Math.DivRem(a, BaseSqrt);
            var (bHi, bLo) = Math.DivRem(b, BaseSqrt);

            var hi = aHi * bHi;
            low = aLo * bLo;

            var mi = aLo * bHi + aHi * bLo;

            var (mh, ml) = Math.DivRem(mi, BaseSqrt);
            low += ml * BaseSqrt;
            if (low >= Base)
            {
                low -= Base;
                ++hi;
            }
            return hi + mh;
        }

        /// <summary>
        /// [Return, <paramref name="low"/>] <paramref name="a"/> * <paramref name="b"/> + <paramref name="c"/>
        /// </summary>
        [MethodImpl(256)]
        public static ulong BigMulAdd(ulong a, ulong b, ulong c, out ulong low)
        {
            var upper = BigMul(a, b, out low);
            upper += SafeAdd(ref low, c);
            return upper;
        }

        /// <returns>(a+b)%Base</returns>
        [MethodImpl(256)]
        public static uint SafeAdd(ref ulong a, ulong b)
        {
            a += b;
            if (a >= Base)
            {
                a -= Base;
                return 1;
            }
            return 0;
        }

        [MethodImpl(256)]
        public static long DivRemBase(long v, out ulong remainder)
        {
            const long B = (long)Base;
            var q = v / B;
            var rem = v - q * B;

            if (rem < 0)
            {
                rem += B;
                --q;
            }
            remainder = (ulong)rem;
            return q;
        }
    }
}

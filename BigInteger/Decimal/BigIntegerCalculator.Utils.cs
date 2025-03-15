// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kzrnm.Numerics.Decimal
{
#if Embedding
    public
#else
    internal
#endif
    static partial class BigIntegerCalculator
    {
        internal const uint Base = 1_000_000_000;
        internal const int BaseLog = 9;
#if NET8_0_OR_GREATER
        static ReadOnlySpan<uint> UInt32PowersOfTen =>
        [
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
       ];
#else
        static readonly uint[] UInt32PowersOfTen = new uint[]
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
       };
#endif

#if DEBUG
        // Mutable for unit testing...
        internal static
#else
        internal const
#endif
        int StackAllocThreshold = 64;

        public static int Compare(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
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

        private static int CompareActual(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right)
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

        private static int ActualLength(ReadOnlySpan<uint> value)
        {
            // Since we're reusing memory here, the actual length
            // of a given value may be less then the array's length

            int length = value.Length;

            while (length > 0 && value[length - 1] == 0)
                --length;
            return length;
        }

        private static int Reduce(Span<uint> bits, ReadOnlySpan<uint> modulus)
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
        public static void InitializeForDebug(Span<uint> bits)
        {
            // Reproduce the case where the return value of `stackalloc uint` is not initialized to zero.
            bits.Fill(0xCD);
        }
        [MethodImpl(256)]
        static uint DivRemBase(uint v, out uint remainder)
        {
            var q = v / Base;
            remainder = v - q * Base;
            return q;
        }
        [MethodImpl(256)]
        static ulong DivRemBase(ulong v, out uint remainder)
        {
            var q = v / Base;
            remainder = (uint)(v - q * Base);
            return q;
        }
        [MethodImpl(256)]
        static long DivRemBase(long v, out uint remainder)
        {
            var q = v / Base;
            var rem = v - q * Base;
            if (rem < 0)
            {
                rem += Base;
                --q;
            }
            remainder = (uint)rem;
            return q;
        }
    }
}

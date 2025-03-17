// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
#if NET7_0_OR_GREATER
using System.Numerics;
using System.Runtime.Intrinsics;
#endif

namespace Kzrnm.Numerics.Logic
{
    internal static class NumericsHelpers
    {
        private const int kcbitUint = 32;

        public static void GetDoubleParts(double dbl, out int sign, out int exp, out ulong man, out bool fFinite)
        {
#if NET7_0_OR_GREATER
            var bits = BitConverter.DoubleToUInt64Bits(dbl);
#else
            ulong bits;
            unsafe
            {
                bits = *(ulong*)&dbl;
            }
#endif

            sign = 1 - ((int)(bits >> 62) & 2);
            man = bits & 0x000FFFFFFFFFFFFF;
            exp = (int)(bits >> 52) & 0x7FF;
            if (exp == 0)
            {
                // Denormalized number.
                fFinite = true;
                if (man != 0)
                    exp = -1074;
            }
            else if (exp == 0x7FF)
            {
                // NaN or Infinite.
                fFinite = false;
                exp = int.MaxValue;
            }
            else
            {
                fFinite = true;
                man |= 0x0010000000000000;
                exp -= 1075;
            }
        }

        public static double GetDoubleFromParts(int sign, int exp, ulong man)
        {
            ulong bits;

            if (man == 0)
            {
                bits = 0;
            }
            else
            {
                // Normalize so that 0x0010 0000 0000 0000 is the highest bit set.
                int cbitShift = LeadingZeroCount(man) - 11;
                if (cbitShift < 0)
                    man >>= -cbitShift;
                else
                    man <<= cbitShift;
                exp -= cbitShift;
                Debug.Assert((man & 0xFFF0000000000000) == 0x0010000000000000);

                // Move the point to just behind the leading 1: 0x001.0 0000 0000 0000
                // (52 bits) and skew the exponent (by 0x3FF == 1023).
                exp += 1075;

                if (exp >= 0x7FF)
                {
                    // Infinity.
                    bits = 0x7FF0000000000000;
                }
                else if (exp <= 0)
                {
                    // Denormalized.
                    exp--;
                    if (exp < -52)
                    {
                        // Underflow to zero.
                        bits = 0;
                    }
                    else
                    {
                        bits = man >> -exp;
                        Debug.Assert(bits != 0);
                    }
                }
                else
                {
                    // Mask off the implicit high bit.
                    bits = (man & 0x000FFFFFFFFFFFFF) | ((ulong)exp << 52);
                }
            }

            if (sign < 0)
                bits |= 0x8000000000000000;

#if NET7_0_OR_GREATER
            return BitConverter.UInt64BitsToDouble(bits);
#else
            unsafe
            {
                return *(double*)&bits;
            }
#endif
        }

        // Do an in-place two's complement. "Dangerous" because it causes
        // a mutation and needs to be used with care for immutable types.
        public static void DangerousMakeTwosComplement(Span<uint> d)
        {
            // Given a number:
            //     XXXXXXXXXXXY00000
            // where Y is non-zero,
            // The result of two's complement is
            //     AAAAAAAAAAAB00000
            // where A = ~X and B = -Y

            // Trim trailing 0s (at the first in little endian array)
            int i = d.IndexOfAnyExcept(0u);

            if ((uint)i >= (uint)d.Length)
            {
                return;
            }

            // Make the first non-zero element to be two's complement
            d[i] = (uint)(-(int)d[i]);
            d = d.Slice(i + 1);

            if (d.IsEmpty)
            {
                return;
            }

            DangerousMakeOnesComplement(d);
        }

        // Do an in-place one's complement. "Dangerous" because it causes
        // a mutation and needs to be used with care for immutable types.
        public static void DangerousMakeOnesComplement(Span<uint> d)
        {
            // Given a number:
            //     XXXXXXXXXXX
            // where Y is non-zero,
            // The result of one's complement is
            //     AAAAAAAAAAA
            // where A = ~X

            int offset = 0;
            ref uint start = ref MemoryMarshal.GetReference(d);

#if NET8_0_OR_GREATER
            while (Vector512.IsHardwareAccelerated && d.Length - offset >= Vector512<uint>.Count)
            {
                Vector512<uint> complement = ~Vector512.LoadUnsafe(ref start, (nuint)offset);
                Vector512.StoreUnsafe(complement, ref start, (nuint)offset);
                offset += Vector512<uint>.Count;
            }
#elif NET7_0_OR_GREATER
            while (Vector256.IsHardwareAccelerated && d.Length - offset >= Vector256<uint>.Count)
            {
                Vector256<uint> complement = ~Vector256.LoadUnsafe(ref start, (nuint)offset);
                Vector256.StoreUnsafe(complement, ref start, (nuint)offset);
                offset += Vector256<uint>.Count;
            }

            while (Vector128.IsHardwareAccelerated && d.Length - offset >= Vector128<uint>.Count)
            {
                Vector128<uint> complement = ~Vector128.LoadUnsafe(ref start, (nuint)offset);
                Vector128.StoreUnsafe(complement, ref start, (nuint)offset);
                offset += Vector128<uint>.Count;
            }
#endif

            for (; offset < d.Length; offset++)
            {
                d[offset] = ~d[offset];
            }
        }

        public static ulong MakeUInt64(uint uHi, uint uLo)
        {
            return ((ulong)uHi << kcbitUint) | uLo;
        }

        public static uint Abs(int a)
        {
            unchecked
            {
                uint mask = (uint)(a >> 31);
                return ((uint)a ^ mask) - mask;
            }
        }
#if !NET7_0_OR_GREATER
        static ReadOnlySpan<byte> Log2DeBruijn => new byte[]
        {
            00, 09, 01, 10, 13, 21, 02, 29,
            11, 14, 16, 18, 22, 25, 03, 30,
            08, 12, 20, 28, 15, 17, 24, 07,
            19, 27, 23, 06, 26, 05, 04, 31,
        };
        public static int Log2(uint value)
        {

            // No AggressiveInlining due to large method size
            // Has conventional contract 0->0 (Log(0) is undefined)

            // Fill trailing zeros with ones, eg 00010010 becomes 00011111
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Unsafe.AddByteOffset(
                // Using deBruijn sequence, k=2, n=5 (2^5=32) : 0b_0000_0111_1100_0100_1010_1100_1101_1101u
                ref MemoryMarshal.GetReference(Log2DeBruijn),
                // uint|long -> IntPtr cast on 32-bit platforms does expensive overflow checks not needed here
                (IntPtr)(int)((value * 0x07C4ACDDu) >> 27));
        }
#endif


        [MethodImpl(256)]
        public static int LeadingZeroCount(uint v)
#if NET7_0_OR_GREATER
            => BitOperations.LeadingZeroCount(v);
#else
        {
            if (v == 0)
                return 32;

            return 31 ^ Log2(v);
        }
#endif

        [MethodImpl(256)]
        public static int LeadingZeroCount(ulong v)
#if NET7_0_OR_GREATER
            => BitOperations.LeadingZeroCount(v);
#else
        {
            uint hi = (uint)(v >> 32);

            if (hi == 0)
            {
                return 32 + LeadingZeroCount((uint)v);
            }

            return LeadingZeroCount(hi);
        }
#endif
    }
}
#if !NET7_0_OR_GREATER
using System.Runtime.CompilerServices;

namespace System.Numerics
{
    public static class BitOperations
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Log2(uint value)
        {
            // The 0->0 contract is fulfilled by setting the LSB to 1.
            // Log(1) is 0, and setting the LSB for values > 1 does not change the log2 result.
            value |= 1;

            // value    lzcnt   actual  expected
            // ..0001   31      31-31    0
            // ..0010   30      31-30    1
            // 0010..    2      31-2    29
            // 0100..    1      31-1    30
            // 1000..    0      31-0    31

            // No AggressiveInlining due to large method size
            // Has conventional contract 0->0 (Log(0) is undefined)

            // Fill trailing zeros with ones, eg 00010010 becomes 00011111
            value |= value >> 01;
            value |= value >> 02;
            value |= value >> 04;
            value |= value >> 08;
            value |= value >> 16;

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return Log2DeBruijn((value * 0x07C4ACDDu) >> 27);
        }
        static byte Log2DeBruijn(uint v)
        {
            switch (v)
            {
                case 0: return 0;
                case 1: return 9;
                case 2: return 1;
                case 3: return 10;
                case 4: return 13;
                case 5: return 21;
                case 6: return 2;
                case 7: return 29;
                case 8: return 11;
                case 9: return 14;
                case 10: return 16;
                case 11: return 18;
                case 12: return 22;
                case 13: return 25;
                case 14: return 3;
                case 15: return 30;
                case 16: return 8;
                case 17: return 12;
                case 18: return 20;
                case 19: return 28;
                case 20: return 15;
                case 21: return 17;
                case 22: return 24;
                case 23: return 7;
                case 24: return 19;
                case 25: return 27;
                case 26: return 23;
                case 27: return 6;
                case 28: return 26;
                case 29: return 5;
                case 30: return 4;
                default: return 31;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(int value)
            => TrailingZeroCount((uint)value);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int TrailingZeroCount(uint value)
        {
            if (value == 0)
                return 32;

            // uint.MaxValue >> 27 is always in range [0 - 31] so we use Unsafe.AddByteOffset to avoid bounds check
            return TrailingZeroCountDeBruijn(((value & (uint)-(int)value) * 0x077CB531u) >> 27); // Multi-cast mitigates redundant conv.u8
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static byte TrailingZeroCountDeBruijn(uint v)
        {
            switch (v)
            {
                case 0: return 0;
                case 1: return 1;
                case 2: return 28;
                case 3: return 2;
                case 4: return 29;
                case 5: return 14;
                case 6: return 24;
                case 7: return 3;
                case 8: return 30;
                case 9: return 22;
                case 10: return 20;
                case 11: return 15;
                case 12: return 25;
                case 13: return 17;
                case 14: return 4;
                case 15: return 8;
                case 16: return 31;
                case 17: return 27;
                case 18: return 13;
                case 19: return 23;
                case 20: return 21;
                case 21: return 19;
                case 22: return 16;
                case 23: return 7;
                case 24: return 26;
                case 25: return 12;
                case 26: return 18;
                case 27: return 6;
                case 28: return 11;
                case 29: return 5;
                case 30: return 10;
                default: return 9;
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LeadingZeroCount(uint value)
        {

            // Unguarded fallback contract is 0->31, BSR contract is 0->undefined
            if (value == 0)
            {
                return 32;
            }

            return 31 ^ Log2(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int PopCount(uint value)
        {
            const uint c1 = 0x_55555555u;
            const uint c2 = 0x_33333333u;
            const uint c3 = 0x_0F0F0F0Fu;
            const uint c4 = 0x_01010101u;

            value -= (value >> 1) & c1;
            value = (value & c2) + ((value >> 2) & c2);
            value = (((value + (value >> 4)) & c3) * c4) >> 24;

            return (int)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RoundUpToPowerOf2(uint value)
        {
            // Based on https://graphics.stanford.edu/~seander/bithacks.html#RoundUpPowerOf2
            --value;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
        }
    }
}
#endif
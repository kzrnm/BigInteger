// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;


#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.Intrinsics.Arm;
#endif

namespace Kzrnm.Numerics
{
    internal static class HexConverter
    {
        public static bool TryDecode(ReadOnlySpan<char> chars, Span<byte> bytes, out int charsProcessed)
        {
#if NETCOREAPP3_1_OR_GREATER
            if (BitConverter.IsLittleEndian && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) &&
                chars.Length >= Vector128<ushort>.Count * 2)
            {
                return TryDecodeFromUtf16_Vector128(chars, bytes, out charsProcessed);
            }
            return TryDecodeScalar(chars, bytes, out charsProcessed);
        }

        public static bool TryDecode(ReadOnlySpan<byte> chars, Span<byte> bytes, out int charsProcessed)
        {
            if (BitConverter.IsLittleEndian && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) &&
                chars.Length >= Vector128<ushort>.Count * 2)
            {
                return TryDecodeFromUtf8_Vector128(chars, bytes, out charsProcessed);
            }
            return TryDecodeScalar(chars, bytes, out charsProcessed);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryDecodeFromUtf16_Vector128(ReadOnlySpan<char> chars, Span<byte> bytes, out int charsProcessed)
        {
            nuint offset = 0;
            if (BitConverter.IsLittleEndian && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) &&
                chars.Length >= Vector128<ushort>.Count * 2)
            {
                nuint lengthSubTwoVector128 = (nuint)chars.Length - ((nuint)Vector128<ushort>.Count * 2);

                ref ushort srcRef = ref Unsafe.As<char, ushort>(ref MemoryMarshal.GetReference(chars));
                ref byte destRef = ref MemoryMarshal.GetReference(bytes);

                do
                {
                    // The algorithm is UTF8 so we'll be loading two UTF-16 vectors to narrow them into a
                    // single UTF8 ASCII vector - the implementation can be shared with UTF8 paths.
                    Vector128<ushort> vec1 = Vector128.LoadUnsafe(ref srcRef, offset);
                    Vector128<ushort> vec2 = Vector128.LoadUnsafe(ref srcRef, offset + (nuint)Vector128<ushort>.Count);
                    if (((vec1 | vec2) & Vector128.Create(unchecked((ushort)~0x007F))) != Vector128<ushort>.Zero)
                        break;

                    Vector128<byte> vec = ExtractAsciiVector(vec1, vec2);

                    if (!TryDecodeFromUtf8_Vector128(vec, ref Unsafe.Add(ref destRef, offset / 2)))
                        break;

                    offset += (nuint)Vector128<ushort>.Count * 2;
                    if (offset == (nuint)chars.Length)
                    {
                        charsProcessed = chars.Length;
                        return true;
                    }
                    // Overlap with the current chunk for trailing elements
                    if (offset > lengthSubTwoVector128)
                    {
                        offset = lengthSubTwoVector128;
                    }
                }
                while (true);
            }

            // Fall back to the scalar routine in case of invalid input.
            bool fallbackResult = TryDecodeScalar(chars.Slice((int)offset), bytes.Slice((int)(offset / 2)), out int fallbackProcessed);
            charsProcessed = (int)offset + fallbackProcessed;
            return fallbackResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryDecodeFromUtf8_Vector128(ReadOnlySpan<byte> chars, Span<byte> bytes, out int charsProcessed)
        {
            nuint offset = 0;
            if (BitConverter.IsLittleEndian && (Ssse3.IsSupported || AdvSimd.Arm64.IsSupported) &&
                chars.Length >= Vector128<byte>.Count)
            {
                nuint lengthSubTwoVector128 = (nuint)chars.Length - (nuint)Vector128<byte>.Count;

                ref byte srcRef = ref MemoryMarshal.GetReference(chars);
                ref byte destRef = ref MemoryMarshal.GetReference(bytes);

                do
                {
                    // The algorithm is UTF8 so we'll be loading two UTF-16 vectors to narrow them into a
                    // single UTF8 ASCII vector - the implementation can be shared with UTF8 paths.
                    Vector128<byte> vec = Vector128.LoadUnsafe(ref srcRef, offset);
                    if ((vec & Vector128.Create((byte)128)) != Vector128<byte>.Zero)
                        break;

                    if (!TryDecodeFromUtf8_Vector128(vec, ref Unsafe.Add(ref destRef, offset / 2)))
                        break;

                    offset += (nuint)Vector128<byte>.Count;
                    if (offset == (nuint)chars.Length)
                    {
                        charsProcessed = chars.Length;
                        return true;
                    }
                    // Overlap with the current chunk for trailing elements
                    if (offset > lengthSubTwoVector128)
                    {
                        offset = lengthSubTwoVector128;
                    }
                }
                while (true);
            }

            // Fall back to the scalar routine in case of invalid input.
            bool fallbackResult = TryDecodeScalar(chars.Slice((int)offset), bytes.Slice((int)(offset / 2)), out int fallbackProcessed);
            charsProcessed = (int)offset + fallbackProcessed;
            return fallbackResult;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool TryDecodeFromUtf8_Vector128(Vector128<byte> vec, ref byte dst)
        {
            // Based on "Algorithm #3" https://github.com/WojciechMula/toys/blob/master/simd-parse-hex/geoff_algorithm.cpp
            // by Geoff Langdale and Wojciech Mula
            // Move digits '0'..'9' into range 0xf6..0xff.
            Vector128<byte> t1 = vec + Vector128.Create((byte)(0xFF - '9'));
            // And then correct the range to 0xf0..0xf9.
            // All other bytes become less than 0xf0.
            Vector128<byte> t2 = SubtractSaturate(t1, Vector128.Create((byte)6));
            // Convert into uppercase 'a'..'f' => 'A'..'F' and
            // move hex letter 'A'..'F' into range 0..5.
            Vector128<byte> t3 = (vec & Vector128.Create((byte)0xDF)) - Vector128.Create((byte)'A');
            // And correct the range into 10..15.
            // The non-hex letters bytes become greater than 0x0f.
            Vector128<byte> t4 = AddSaturate(t3, Vector128.Create((byte)10));
            // Convert '0'..'9' into nibbles 0..9. Non-digit bytes become
            // greater than 0x0f. Finally choose the result: either valid nibble (0..9/10..15)
            // or some byte greater than 0x0f.
            Vector128<byte> nibbles = Vector128.Min(t2 - Vector128.Create((byte)0xF0), t4);
            // Any high bit is a sign that input is not a valid hex data

            if (AddSaturate(nibbles, Vector128.Create((byte)(127 - 15))).ExtractMostSignificantBits() != 0)
            {
                // Input is either non-ASCII or invalid hex data
                return false;
            }
            Vector128<byte> output;
            if (Ssse3.IsSupported)
            {
                output = Ssse3.MultiplyAddAdjacent(nibbles,
                    Vector128.Create((short)0x0110).AsSByte()).AsByte();
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                // Workaround for missing MultiplyAddAdjacent on ARM
                Vector128<short> even = AdvSimd.Arm64.TransposeEven(nibbles, Vector128<byte>.Zero).AsInt16();
                Vector128<short> odd = AdvSimd.Arm64.TransposeOdd(nibbles, Vector128<byte>.Zero).AsInt16();
                even = AdvSimd.ShiftLeftLogical(even, 4).AsInt16();
                output = AdvSimd.AddSaturate(even, odd).AsByte();
            }
            else
            {
                // We explicitly recheck each IsSupported query to ensure that the trimmer can see which paths are live/dead
                ThrowHelper.ThrowNotSupportedException();
                output = default;
            }
            // Accumulate output in lower INT64 half and take care about endianness
            output = Vector128.Shuffle(output, Vector128.Create((byte)0, 2, 4, 6, 8, 10, 12, 14, 0, 0, 0, 0, 0, 0, 0, 0));
            // Store 8 bytes in dest by given offset
            Unsafe.WriteUnaligned(ref dst, output.AsUInt64().ToScalar());

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<byte> AddSaturate(Vector128<byte> left, Vector128<byte> right)
        {
            if (Sse2.IsSupported)
            {
                return Sse2.AddSaturate(left, right);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                return AdvSimd.AddSaturate(left, right);
            }
            else
            {
                ThrowHelper.ThrowNotSupportedException();
                return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<byte> SubtractSaturate(Vector128<byte> left, Vector128<byte> right)
        {
            if (Sse2.IsSupported)
            {
                return Sse2.SubtractSaturate(left, right);
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                return AdvSimd.SubtractSaturate(left, right);
            }
            else
            {
                ThrowHelper.ThrowNotSupportedException();
                return default;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static Vector128<byte> ExtractAsciiVector(Vector128<ushort> vectorFirst, Vector128<ushort> vectorSecond)
        {
            // Narrows two vectors of words [ w7 w6 w5 w4 w3 w2 w1 w0 ] and [ w7' w6' w5' w4' w3' w2' w1' w0' ]
            // to a vector of bytes [ b7 ... b0 b7' ... b0'].

            // prefer architecture specific intrinsic as they don't perform additional AND like Vector128.Narrow does
            if (Sse2.IsSupported)
            {
                return Sse2.PackUnsignedSaturate(vectorFirst.AsInt16(), vectorSecond.AsInt16());
            }
            else if (AdvSimd.Arm64.IsSupported)
            {
                return AdvSimd.Arm64.UnzipEven(vectorFirst.AsByte(), vectorSecond.AsByte());
            }
            else
            {
                return Vector128.Narrow(vectorFirst, vectorSecond);
            }
        }

        private static bool TryDecodeScalar<T>(ReadOnlySpan<T> chars, Span<byte> bytes, out int charsProcessed)
        {
#endif
            Debug.Assert(chars.Length % 2 == 0, "Un-even number of characters provided");
            Debug.Assert(chars.Length / 2 == bytes.Length, "Target buffer not right-sized for provided characters");

            int i = 0;
            int j = 0;
            int byteLo = 0;
            int byteHi = 0;
            while (j < bytes.Length)
            {
                byteLo = FromChar(chars[i + 1]);
                byteHi = FromChar(chars[i]);

                // byteHi hasn't been shifted to the high half yet, so the only way the bitwise or produces this pattern
                // is if either byteHi or byteLo was not a hex character.
                if ((byteLo | byteHi) == 0xFF)
                    break;

                bytes[j++] = (byte)((byteHi << 4) | byteLo);
                i += 2;
            }

            if (byteLo == 0xFF)
                i++;

            charsProcessed = i;
            return (byteLo | byteHi) != 0xFF;
        }

        [MethodImpl(256)]
        static int FromChar<T>(T c)
        {
            var i = SR.CastToUInt32(c);
            return i >= CharToHexLookup.Length ? 0xFF : CharToHexLookup[(int)i];
        }

        /// <summary>Map from an ASCII char to its hex value, e.g. arr['b'] == 11. 0xFF means it's not a hex digit.</summary>
        static ReadOnlySpan<byte> CharToHexLookup => new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
            0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
            0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
            0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf
       };
    }
}

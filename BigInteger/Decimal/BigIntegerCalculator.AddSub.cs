// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kzrnm.Numerics.Decimal
{
    static partial class BigIntegerCalculator
    {
        private const int CopyToThreshold = 8;

        private static void CopyTail(ReadOnlySpan<uint> source, Span<uint> dest, int start)
        {
            source.Slice(start).CopyTo(dest.Slice(start));
        }

        public static void Add(ReadOnlySpan<uint> left, uint right, Span<uint> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(bits.Length == left.Length + 1);
            Debug.Assert(right < Base);

            Add(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: right);
        }

        public static void Add(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(bits.Length == left.Length + 1);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref uint resultPtr = ref MemoryMarshal.GetReference(bits);
            ref uint rightPtr = ref MemoryMarshal.GetReference(right);
            ref uint leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            uint carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            do
            {
                carry = DivRemBase(carry + Unsafe.Add(ref leftPtr, i) + Unsafe.Add(ref rightPtr, i), out Unsafe.Add(ref resultPtr, i));
                i++;
            } while (i < right.Length);

            Add(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void AddSelf(Span<uint> left, ReadOnlySpan<uint> right)
        {
            Debug.Assert(left.Length >= right.Length);

            int i = 0;
            uint carry = 0;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref var leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.

            for (; i < right.Length; i++)
            {
                carry = DivRemBase(Unsafe.Add(ref leftPtr, i) + carry + right[i], out Unsafe.Add(ref leftPtr, i));
            }
            for (; carry != 0 && i < left.Length; i++)
            {
                ref var result = ref left[i];
                result += carry;
                if (result >= Base)
                    carry = 1;
                else
                    carry = 0;
            }

            Debug.Assert(carry == 0);
        }

        public static void Subtract(ReadOnlySpan<uint> left, uint right, Span<uint> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(left[0] >= right || left.Length >= 2);
            Debug.Assert(bits.Length == left.Length);
            Debug.Assert(right < Base);

            Subtract(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: -(int)right);
        }

        public static void Subtract(ReadOnlySpan<uint> left, ReadOnlySpan<uint> right, Span<uint> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);
            Debug.Assert(bits.Length == left.Length);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref var resultPtr = ref MemoryMarshal.GetReference(bits);
            ref var rightPtr = ref MemoryMarshal.GetReference(right);
            ref var leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            long carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            do
            {
                carry = DivRemBase(carry + Unsafe.Add(ref leftPtr, i) - Unsafe.Add(ref rightPtr, i), out Unsafe.Add(ref resultPtr, i));
                i++;
            } while (i < right.Length);

            Subtract(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void SubtractSelf(Span<uint> left, ReadOnlySpan<uint> right)
        {
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);

            int i = 0;
            long carry = 0;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref var rightPtr = ref MemoryMarshal.GetReference(right);
            ref var leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a - b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.

            for (; i < right.Length; i++)
            {
                carry = DivRemBase(carry + Unsafe.Add(ref leftPtr, i) - right[i], out Unsafe.Add(ref leftPtr, i));
            }
            for (; carry != 0 && i < left.Length; i++)
            {
                carry = DivRemBase(carry + left[i], out left[i]);
            }

            Debug.Assert(carry == 0);
        }

        [MethodImpl(256)]
        private static void Add(ReadOnlySpan<uint> left, Span<uint> bits, ref uint resultPtr, int startIndex, uint initialCarry)
        {
            // Executes the addition for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            int i = startIndex;
            uint carry = initialCarry;

            if (left.Length <= CopyToThreshold)
            {
                for (; i < left.Length; i++)
                {
                    ref var result = ref Unsafe.Add(ref resultPtr, i);
                    result = left[i] + carry;
                    carry = 0;
                    if (result >= Base)
                    {
                        ++carry;
                        result -= Base;
                    }
                }
                Unsafe.Add(ref resultPtr, left.Length) = carry;
                Debug.Assert(carry < Base);
            }
            else
            {
                for (; i < left.Length;)
                {
                    ref var result = ref Unsafe.Add(ref resultPtr, i);
                    result = left[i] + carry;
                    carry = 0;
                    if (result >= Base)
                    {
                        ++carry;
                        result -= Base;
                    }
                    i++;

                    // Once carry is set to 0 it can not be 1 anymore.
                    // So the tail of the loop is just the movement of argument values to result span.
                    if (carry == 0)
                    {
                        break;
                    }
                }
                Unsafe.Add(ref resultPtr, left.Length) = carry;

                if (i < left.Length)
                {
                    CopyTail(left, bits, i);
                }
            }
        }

        [MethodImpl(256)]
        private static void Subtract(ReadOnlySpan<uint> left, Span<uint> bits, ref uint resultPtr, int startIndex, long initialCarry)
        {
            // Executes the addition for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            int i = startIndex;
            long carry = initialCarry;

            if (left.Length <= CopyToThreshold)
            {
                for (; i < left.Length; i++)
                {
                    carry = DivRemBase(carry + left[i], out Unsafe.Add(ref resultPtr, i));
                }
            }
            else
            {
                for (; i < left.Length;)
                {
                    carry = DivRemBase(carry + left[i], out Unsafe.Add(ref resultPtr, i));
                    i++;

                    // Once carry is set to 0 it can not be 1 anymore.
                    // So the tail of the loop is just the movement of argument values to result span.
                    if (carry == 0)
                    {
                        break;
                    }
                }

                if (i < left.Length)
                {
                    CopyTail(left, bits, i);
                }
            }
        }
    }
}

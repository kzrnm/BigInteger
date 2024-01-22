// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kzrnm.Numerics.Decimal
{
    internal static partial class BigIntegerCalculator
    {
        private const int CopyToThreshold = 8;

        private static void CopyTail(ReadOnlySpan<ulong> source, Span<ulong> dest, int start)
        {
            source.Slice(start).CopyTo(dest.Slice(start));
        }

        public static void Add(ReadOnlySpan<ulong> left, ulong right, Span<ulong> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(bits.Length == left.Length + 1);
            Debug.Assert(right < Base);

            Add(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: right);
        }

        public static void Add(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(bits.Length == left.Length + 1);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref ulong resultPtr = ref MemoryMarshal.GetReference(bits);
            ref ulong rightPtr = ref MemoryMarshal.GetReference(right);
            ref ulong leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            ulong carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            do
            {
                carry += Unsafe.Add(ref leftPtr, i);
                carry += Unsafe.Add(ref rightPtr, i);
                carry = DivRemBase(carry, out var rem);
                Unsafe.Add(ref resultPtr, i) = rem;
                i++;
            } while (i < right.Length);

            Add(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void AddSelf(Span<ulong> left, ReadOnlySpan<ulong> right)
        {
            Debug.Assert(left.Length >= right.Length);

            int i = 0;
            ulong carry = 0L;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref ulong leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.

            for (; i < right.Length; i++)
            {
                ulong digit = (Unsafe.Add(ref leftPtr, i) + carry) + right[i];
                carry = DivRemBase(digit, out var rem);
                Unsafe.Add(ref leftPtr, i) = rem;
            }
            for (; carry != 0 && i < left.Length; i++)
            {
                ulong digit = left[i] + carry;
                carry = DivRemBase(digit, out var rem);
                left[i] = rem;
            }

            Debug.Assert(carry == 0);
        }

        public static void Subtract(ReadOnlySpan<ulong> left, ulong right, Span<ulong> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(left[0] >= right || left.Length >= 2);
            Debug.Assert(bits.Length == left.Length);
            Debug.Assert(right < Base);

            Subtract(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: -(long)right);
        }

        public static void Subtract(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);
            Debug.Assert(bits.Length == left.Length);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref ulong resultPtr = ref MemoryMarshal.GetReference(bits);
            ref ulong rightPtr = ref MemoryMarshal.GetReference(right);
            ref ulong leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            long carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            do
            {
                carry += (long)Unsafe.Add(ref leftPtr, i);
                carry -= (long)Unsafe.Add(ref rightPtr, i);
                carry = DivRemBase(carry, out var rem);
                Unsafe.Add(ref resultPtr, i) = rem;
                i++;
            } while (i < right.Length);

            Subtract(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void SubtractSelf(Span<ulong> left, ReadOnlySpan<ulong> right)
        {
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);

            int i = 0;
            long carry = 0L;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref ulong leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a - b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.

            for (; i < right.Length; i++)
            {
                long digit = ((long)Unsafe.Add(ref leftPtr, i) + carry) - (long)right[i];
                carry = DivRemBase(digit, out var rem);
                Unsafe.Add(ref leftPtr, i) = rem;
            }
            for (; carry != 0 && i < left.Length; i++)
            {
                long digit = (long)left[i] + carry;
                carry = DivRemBase(digit, out var rem);
                left[i] = rem;
            }

            Debug.Assert(carry == 0);
        }

        [MethodImpl(256)]
        private static void Add(ReadOnlySpan<ulong> left, Span<ulong> bits, ref ulong resultPtr, int startIndex, ulong initialCarry)
        {
            // Executes the addition for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            int i = startIndex;
            ulong carry = initialCarry;

            if (left.Length <= CopyToThreshold)
            {
                for (; i < left.Length; i++)
                {
                    carry += left[i];
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, i) = rem;
                }
                {
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, left.Length) = rem;
                }
                Debug.Assert(carry == 0);
            }
            else
            {
                for (; i < left.Length;)
                {
                    carry += left[i];
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, i) = rem;
                    i++;

                    // Once carry is set to 0 it can not be 1 anymore.
                    // So the tail of the loop is just the movement of argument values to result span.
                    if (carry == 0)
                    {
                        break;
                    }
                }
                {
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, left.Length) = rem;
                    Debug.Assert(carry == 0);
                }

                if (i < left.Length)
                {
                    CopyTail(left, bits, i);
                }
            }
        }

        [MethodImpl(256)]
        private static void Subtract(ReadOnlySpan<ulong> left, Span<ulong> bits, ref ulong resultPtr, int startIndex, long initialCarry)
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
                    carry += (long)left[i];
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, i) = rem;
                }
            }
            else
            {
                for (; i < left.Length;)
                {
                    carry += (long)left[i];
                    carry = DivRemBase(carry, out var rem);
                    Unsafe.Add(ref resultPtr, i) = rem;
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

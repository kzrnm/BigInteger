// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kzrnm.Numerics.Experiment
{
    internal static partial class BigIntegerCalculator
    {
        private const int CopyToThreshold = 8;

        private static void CopyTail(ReadOnlySpan<nuint> source, Span<nuint> dest, int start)
        {
            source.Slice(start).CopyTo(dest.Slice(start));
        }

        public static void Add(ReadOnlySpan<nuint> left, nuint right, Span<nuint> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(bits.Length == left.Length + 1);

            Add(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: right);
        }

        public static void Add(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(bits.Length == left.Length + 1);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref nuint resultPtr = ref MemoryMarshal.GetReference(bits);
            ref nuint rightPtr = ref MemoryMarshal.GetReference(right);
            ref nuint leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            ulong carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            do
            {
                if (Environment.Is64BitProcess)
                {
                    ulong hi = 0;
                    nuint leftElement = Unsafe.Add(ref leftPtr, i);
                    nuint rightElement = Unsafe.Add(ref rightPtr, i);
                    carry += leftElement;
                    if (carry < leftElement)
                        ++hi;
                    carry += rightElement;
                    if (carry < rightElement)
                        ++hi;
                    Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                    carry = hi;
                }
                else
                {
                    carry += Unsafe.Add(ref leftPtr, i);
                    carry += Unsafe.Add(ref rightPtr, i);
                    Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                    carry >>= 32;
                }
                i++;
            } while (i < right.Length);

            Add(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void AddSelf(Span<nuint> left, ReadOnlySpan<nuint> right)
        {
            Debug.Assert(left.Length >= right.Length);

            int i = 0;
            ulong carry = 0;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref nuint leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.

            if (Environment.Is64BitProcess)
            {
                for (; i < right.Length; i++)
                {
                    ulong hi = 0;
                    ref nuint digit = ref Unsafe.Add(ref leftPtr, i);
                    digit += (nuint)carry;
                    if (digit < carry)
                        ++hi;
                    digit += right[i];
                    if (digit < right[i])
                        ++hi;
                    carry = hi;
                }
                for (; carry != 0 && i < left.Length; i++)
                {
                    ulong digit = left[i] + carry;
                    left[i] = (nuint)digit;
                    carry = digit < carry ? 1u : 0;
                }
            }
            else
            {
                for (; i < right.Length; i++)
                {
                    ulong digit = (Unsafe.Add(ref leftPtr, i) + carry) + right[i];
                    Unsafe.Add(ref leftPtr, i) = unchecked((uint)digit);
                    carry = digit >> 32;
                }
                for (; carry != 0 && i < left.Length; i++)
                {
                    ulong digit = left[i] + carry;
                    left[i] = (uint)digit;
                    carry = digit >> 32;
                }
            }

            Debug.Assert(carry == 0);
        }

        public static void Subtract(ReadOnlySpan<nuint> left, uint right, Span<nuint> bits)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(left[0] >= right || left.Length >= 2);
            Debug.Assert(bits.Length == left.Length);

            Subtract(left, bits, ref MemoryMarshal.GetReference(bits), startIndex: 0, initialCarry: (long)-right);
        }

        public static void Subtract(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> bits)
        {
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);
            Debug.Assert(bits.Length == left.Length);

            // Switching to managed references helps eliminating
            // index bounds check for all buffers.
            ref nuint resultPtr = ref MemoryMarshal.GetReference(bits);
            ref nuint rightPtr = ref MemoryMarshal.GetReference(right);
            ref nuint leftPtr = ref MemoryMarshal.GetReference(left);

            int i = 0;
            long carry = 0;

            // Executes the "grammar-school" algorithm for computing z = a + b.
            // While calculating z_i = a_i + b_i we take care of overflow:
            // Since a_i + b_i + c <= 2(2^32 - 1) + 1 = 2^33 - 1, our carry c
            // has always the value 1 or 0; hence, we're safe here.

            if (Environment.Is64BitProcess)
            {
                do
                {
                    long hi = carry >> 63;
                    carry += (long)Unsafe.Add(ref leftPtr, i);
                    if ((ulong)carry < Unsafe.Add(ref leftPtr, i))
                        ++hi;
                    if ((ulong)carry < Unsafe.Add(ref rightPtr, i))
                        --hi;
                    carry -= (long)Unsafe.Add(ref rightPtr, i);
                    Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                    carry = hi;
                    i++;
                } while (i < right.Length);
            }
            else
            {
                do
                {
                    carry += (long)Unsafe.Add(ref leftPtr, i);
                    carry -= (long)Unsafe.Add(ref rightPtr, i);
                    Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                    carry >>= 32;
                    i++;
                } while (i < right.Length);
            }

            Subtract(left, bits, ref resultPtr, startIndex: i, initialCarry: carry);
        }

        private static void SubtractSelf(Span<nuint> left, ReadOnlySpan<nuint> right)
        {
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(CompareActual(left, right) >= 0);

            int i = 0;
            long carry = 0;

            // Switching to managed references helps eliminating
            // index bounds check...
            ref nuint leftPtr = ref MemoryMarshal.GetReference(left);

            // Executes the "grammar-school" algorithm for computing z = a - b.
            // Same as above, but we're writing the result directly to a and
            // stop execution, if we're out of b and c is already 0.


            if (Environment.Is64BitProcess)
            {
                for (; i < right.Length; i++)
                {
                    long hi = carry >> 63;
                    long digit = (long)Unsafe.Add(ref leftPtr, i);
                    digit += carry;
                    if ((ulong)digit < (ulong)carry)
                        ++hi;
                    if ((ulong)digit < right[i])
                        --hi;
                    digit -= (long)right[i];

                    Unsafe.Add(ref leftPtr, i) = unchecked((nuint)digit);
                    carry = hi;
                }
                for (; carry != 0 && i < left.Length; i++)
                {
                    long hi = carry >> 63;
                    long digit = (long)left[i] + carry;
                    if ((ulong)digit < (ulong)carry)
                        ++hi;
                    carry = hi;
                    left[i] = (nuint)digit;
                }
            }
            else
            {
                for (; i < right.Length; i++)
                {
                    long digit = ((long)Unsafe.Add(ref leftPtr, i) + carry) - (long)right[i];
                    Unsafe.Add(ref leftPtr, i) = unchecked((uint)digit);
                    carry = digit >> 32;
                }
                for (; carry != 0 && i < left.Length; i++)
                {
                    long digit = (long)left[i] + carry;
                    left[i] = (uint)digit;
                    carry = digit >> 32;
                }
            }

            Debug.Assert(carry == 0);
        }

        [MethodImpl(256)]
        private static void Add(ReadOnlySpan<nuint> left, Span<nuint> bits, ref nuint resultPtr, int startIndex, ulong initialCarry)
        {
            // Executes the addition for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            int i = startIndex;
            ulong carry = initialCarry;

            if (Environment.Is64BitProcess)
            {
                if (left.Length <= CopyToThreshold)
                {
                    for (; i < left.Length; i++)
                    {
                        ulong hi = 0;
                        carry += left[i];
                        if (carry < left[i])
                            ++hi;
                        Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                        carry = hi;
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((nuint)carry);
                }
                else
                {
                    for (; i < left.Length;)
                    {
                        ulong hi = 0;
                        carry += left[i];
                        if (carry < left[i])
                            ++hi;
                        Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                        i++;
                        carry = hi;

                        // Once carry is set to 0 it can not be 1 anymore.
                        // So the tail of the loop is just the movement of argument values to result span.
                        if (hi == 0)
                        {
                            break;
                        }
                        carry = hi;
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((nuint)carry);

                    if (i < left.Length)
                    {
                        CopyTail(left, bits, i);
                    }
                }
            }
            else
            {
                if (left.Length <= CopyToThreshold)
                {
                    for (; i < left.Length; i++)
                    {
                        carry += left[i];
                        Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                        carry >>= 32;
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((uint)carry);
                }
                else
                {
                    for (; i < left.Length;)
                    {
                        carry += left[i];
                        Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                        i++;
                        carry >>= 32;

                        // Once carry is set to 0 it can not be 1 anymore.
                        // So the tail of the loop is just the movement of argument values to result span.
                        if (carry == 0)
                        {
                            break;
                        }
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((uint)carry);

                    if (i < left.Length)
                    {
                        CopyTail(left, bits, i);
                    }
                }
            }
        }

        [MethodImpl(256)]
        private static void Subtract(ReadOnlySpan<nuint> left, Span<nuint> bits, ref nuint resultPtr, int startIndex, long initialCarry)
        {
            // Executes the addition for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            int i = startIndex;
            long carry = initialCarry;

            if (Environment.Is64BitProcess)
            {
                long carryHi = carry >> 63;
                if (left.Length <= CopyToThreshold)
                {
                    for (; i < left.Length; i++)
                    {
                        carry += (long)left[i];
                        if ((ulong)carry < left[i])
                            ++carryHi;
                        Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                        carry = carryHi;
                        carryHi = carry >> 63;
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((nuint)carry);
                }
                else
                {
                    for (; i < left.Length;)
                    {
                        carry += (long)left[i];
                        if ((ulong)carry < left[i])
                            ++carryHi;
                        Unsafe.Add(ref resultPtr, i) = unchecked((nuint)carry);
                        i++;

                        // Once carry is set to 0 it can not be 1 anymore.
                        // So the tail of the loop is just the movement of argument values to result span.
                        if (carryHi == 0)
                        {
                            break;
                        }
                        carry = carryHi;
                        carryHi = carry >> 63;
                    }

                    if (i < left.Length)
                    {
                        CopyTail(left, bits, i);
                    }
                }
            }
            else
            {
                if (left.Length <= CopyToThreshold)
                {
                    for (; i < left.Length; i++)
                    {
                        carry += (long)left[i];
                        Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                        carry >>= 32;
                    }

                    Unsafe.Add(ref resultPtr, left.Length) = unchecked((uint)carry);
                }
                else
                {
                    for (; i < left.Length;)
                    {
                        carry += (long)left[i];
                        Unsafe.Add(ref resultPtr, i) = unchecked((uint)carry);
                        i++;
                        carry >>= 32;

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
}

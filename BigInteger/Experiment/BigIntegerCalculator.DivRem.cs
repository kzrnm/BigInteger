// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Numerics;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Kzrnm.Numerics.Experiment
{
    internal static partial class BigIntegerCalculator
    {
#if DEBUG
        // Mutable for unit testing...
        internal static
#else
        internal const
#endif
        int DivideThreshold = 32;

        public static void Divide(ReadOnlySpan<nuint> left, uint right, Span<nuint> quotient, out uint remainder)
        {
            DummyForDebug(quotient);
            DivideImpl(left, right, quotient, out ulong carry);
            remainder = (uint)carry;
        }

        public static void Divide(ReadOnlySpan<nuint> left, uint right, Span<nuint> quotient)
        {
            DummyForDebug(quotient);
            DivideImpl(left, right, quotient, out _);
        }

        private static void DivideImpl(ReadOnlySpan<nuint> left, uint right, Span<nuint> quotient, out ulong carry)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(quotient.Length == left.Length);
            DummyForDebug(quotient);

            carry = 0;

            if (Environment.Is64BitProcess)
            {
                for (int i = left.Length - 1; i >= 0; i--)
                {
                    {
                        ulong value = (carry << 32) | (left[i] >> 32);
                        ulong digit = value / right;
                        quotient[i] = (nuint)(digit << 32);
                        carry = value - digit * right;
                    }
                    {
                        ulong value = (carry << 32) | (uint)left[i];
                        ulong digit = value / right;
                        quotient[i] |= (uint)digit;
                        carry = value - digit * right;
                    }
                }
            }
            else
            {
                // Executes the division for one big and one 32-bit integer.
                // Thus, we've similar code than below, but there is no loop for
                // processing the 32-bit integer, since it's a single element.

                for (int i = left.Length - 1; i >= 0; i--)
                {
                    ulong value = (carry << 32) | left[i];
                    ulong digit = value / right;
                    quotient[i] = (uint)digit;
                    carry = value - digit * right;
                }
            }
        }

        public static uint Remainder(ReadOnlySpan<nuint> left, uint right)
        {
            Debug.Assert(left.Length >= 1);

            // Same as above, but only computing the remainder.
            ulong carry = 0UL;
            if (Environment.Is64BitProcess)
            {
                for (int i = left.Length - 1; i >= 0; i--)
                {
                    {
                        ulong value = (carry << 32) | (left[i] >> 32);
                        carry = value % right;
                    }
                    {
                        ulong value = (carry << 32) | (uint)left[i];
                        carry = value % right;
                    }
                }
            }
            else
            {
                for (int i = left.Length - 1; i >= 0; i--)
                {
                    ulong value = (carry << 32) | left[i];
                    carry = value % right;
                }
            }


            return (uint)carry;
        }

        public static void Divide(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient, Span<nuint> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1);
            Debug.Assert(remainder.Length == left.Length);
            DummyForDebug(quotient);
            DummyForDebug(remainder);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
            {
                left.CopyTo(remainder);
                DivideGrammarSchool(remainder, right, quotient);
            }
            else
                DivideBurnikelZiegler(left, right, quotient, remainder);
        }

        public static void Divide(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1);
            DummyForDebug(quotient);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
            {
                // Same as above, but only returning the quotient.

                nuint[]? leftCopyFromPool = null;

                // NOTE: left will get overwritten, we need a local copy
                // However, mutated left is not used afterwards, so use array pooling or stack alloc
                Span<nuint> leftCopy = (left.Length <= StackAllocThreshold ?
                                      stackalloc nuint[StackAllocThreshold]
                                      : leftCopyFromPool = ArrayPool<nuint>.Shared.Rent(left.Length)).Slice(0, left.Length);
                left.CopyTo(leftCopy);

                DivideGrammarSchool(leftCopy, right, quotient);

                if (leftCopyFromPool != null)
                    ArrayPool<nuint>.Shared.Return(leftCopyFromPool);
            }
            else
                DivideBurnikelZiegler(left, right, quotient, default);

        }

        public static void Remainder(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(remainder.Length == left.Length);
            DummyForDebug(remainder);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
            {
                // Same as above, but only returning the remainder.

                left.CopyTo(remainder);
                DivideGrammarSchool(remainder, right, default);
            }
            else
            {
                int quotientLength = left.Length - right.Length + 1;
                nuint[]? quotientFromPool = null;

                Span<nuint> quotient = (quotientLength <= StackAllocThreshold ?
                                      stackalloc nuint[StackAllocThreshold]
                                      : quotientFromPool = ArrayPool<nuint>.Shared.Rent(quotientLength)).Slice(0, quotientLength);

                DivideBurnikelZiegler(left, right, quotient, remainder);

                if (quotientFromPool != null)
                    ArrayPool<nuint>.Shared.Return(quotientFromPool);
            }
        }

        private static void DivRem(Span<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient)
        {
            // quotient = left / right;
            // left %= right;

            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1
                || quotient.Length == 0);
            DummyForDebug(quotient);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
                DivideGrammarSchool(left, right, quotient);
            else
            {
                nuint[]? leftCopyFromPool = null;

                // NOTE: left will get overwritten, we need a local copy
                // However, mutated left is not used afterwards, so use array pooling or stack alloc
                Span<nuint> leftCopy = (left.Length <= StackAllocThreshold ?
                                      stackalloc nuint[StackAllocThreshold]
                                      : leftCopyFromPool = ArrayPool<nuint>.Shared.Rent(left.Length)).Slice(0, left.Length);
                left.CopyTo(leftCopy);

                nuint[]? quotientActualFromPool = null;
                scoped Span<nuint> quotientActual;

                if (quotient.Length == 0)
                {
                    int quotientLength = left.Length - right.Length + 1;

                    quotientActual = (quotientLength <= StackAllocThreshold ?
                                stackalloc nuint[StackAllocThreshold]
                                : quotientActualFromPool = ArrayPool<nuint>.Shared.Rent(quotientLength)).Slice(0, quotientLength);
                }
                else
                    quotientActual = quotient;

                DivideBurnikelZiegler(leftCopy, right, quotientActual, left);

                if (quotientActualFromPool != null)
                    ArrayPool<nuint>.Shared.Return(quotientActualFromPool);
                if (leftCopyFromPool != null)
                    ArrayPool<nuint>.Shared.Return(leftCopyFromPool);
            }
        }
        private static void DivideGrammarSchool(Span<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(
                quotient.Length == 0
                || quotient.Length == left.Length - right.Length + 1
                || (CompareActual(left.Slice(left.Length - right.Length), right) < 0 && quotient.Length == left.Length - right.Length));

            if (Environment.Is64BitProcess)
                DivideGrammarSchool(
                    MemoryMarshal.Cast<nuint, ulong>(left),
                    MemoryMarshal.Cast<nuint, ulong>(right),
                    MemoryMarshal.Cast<nuint, ulong>(quotient));
            else
                DivideGrammarSchool(
                        MemoryMarshal.Cast<nuint, uint>(left),
                        MemoryMarshal.Cast<nuint, uint>(right),
                        MemoryMarshal.Cast<nuint, uint>(quotient));
        }

        private static void DivideGrammarSchool(Span<uint> left, ReadOnlySpan<uint> right, Span<uint> quotient)
        {
            Debug.Assert(!Environment.Is64BitProcess);

            // Executes the "grammar-school" algorithm for computing q = a / b.
            // Before calculating q_i, we get more bits into the highest bit
            // block of the divisor. Thus, guessing digits of the quotient
            // will be more precise. Additionally we'll get r = a % b.

            uint divHi = right[right.Length - 1];
            uint divLo = right.Length > 1 ? right[right.Length - 2] : 0;

            // We measure the leading zeros of the divisor
            int shift = BitOperations.LeadingZeroCount(divHi);
            int backShift = 32 - shift;

            // And, we make sure the most significant bit is set
            if (shift > 0)
            {
                uint divNx = right.Length > 2 ? right[right.Length - 3] : 0;

                divHi = (divHi << shift) | (divLo >> backShift);
                divLo = (divLo << shift) | (divNx >> backShift);
            }

            // Then, we divide all of the bits as we would do it using
            // pen and paper: guessing the next digit, subtracting, ...
            for (int i = left.Length; i >= right.Length; i--)
            {
                int n = i - right.Length;
                uint t = (uint)i < (uint)left.Length ? left[i] : 0;

                ulong valHi = ((ulong)t << 32) | left[i - 1];
                uint valLo = i > 1 ? (uint)left[i - 2] : 0;

                // We shifted the divisor, we shift the dividend too
                if (shift > 0)
                {
                    uint valNx = i > 2 ? (uint)left[i - 3] : 0;

                    valHi = (valHi << shift) | (valLo >> backShift);
                    valLo = (valLo << shift) | (valNx >> backShift);
                }

                // First guess for the current digit of the quotient,
                // which naturally must have only 32 bits...
                ulong digit = valHi / divHi;
                if (digit > 0xFFFFFFFF)
                    digit = 0xFFFFFFFF;


                // Our first guess may be a little bit to big
                while (DivideGuessTooBig(digit, valHi, valLo, divHi, divLo))
                    --digit;

                if (digit > 0)
                {
                    // Now it's time to subtract our current quotient
                    uint carry = SubtractDivisor(left.Slice(n), right, digit);
                    if (carry != t)
                    {
                        Debug.Assert(carry == t + 1);

                        // Our guess was still exactly one too high
                        carry = AddDivisor(left.Slice(n), right);
                        --digit;

                        Debug.Assert(carry == 1);
                    }
                }

                // We have the digit!
                if ((uint)n < (uint)quotient.Length)
                    quotient[n] = (uint)digit;

                if ((uint)i < (uint)left.Length)
                    left[i] = 0;
            }

            static uint AddDivisor(Span<uint> left, ReadOnlySpan<uint> right)
            {
                Debug.Assert(left.Length >= right.Length);

                // Repairs the dividend, if the last subtract was too much

                ulong carry = 0UL;

                for (int i = 0; i < right.Length; i++)
                {
                    ref uint leftElement = ref left[i];
                    ulong digit = (leftElement + carry) + right[i];
                    leftElement = unchecked((uint)digit);
                    carry = digit >> 32;
                }

                return (uint)carry;
            }

            static uint SubtractDivisor(Span<uint> left, ReadOnlySpan<uint> right, ulong q)
            {
                Debug.Assert(left.Length >= right.Length);
                Debug.Assert(q <= 0xFFFFFFFF);

                // Combines a subtract and a multiply operation, which is naturally
                // more efficient than multiplying and then subtracting...

                ulong carry = 0UL;

                for (int i = 0; i < right.Length; i++)
                {
                    carry += right[i] * q;
                    uint digit = unchecked((uint)carry);
                    carry >>= 32;
                    ref uint leftElement = ref left[i];
                    if (leftElement < digit)
                        ++carry;
                    leftElement -= digit;
                }

                return (uint)carry;
            }

            static bool DivideGuessTooBig(ulong q, ulong valHi, uint valLo, uint divHi, uint divLo)
            {
                Debug.Assert(q <= 0xFFFFFFFF);

                // We multiply the two most significant limbs of the divisor
                // with the current guess for the quotient. If those are bigger
                // than the three most significant limbs of the current dividend
                // we return true, which means the current guess is still too big.

                ulong chkHi = divHi * q;
                ulong chkLo = divLo * q;

                chkHi += (chkLo >> 32);
                uint chkLoUInt32 = (uint)(chkLo);

                return (chkHi > valHi) || ((chkHi == valHi) && (chkLoUInt32 > valLo));
            }
        }

        private static void DivideGrammarSchool(Span<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient)
        {
            Debug.Assert(Environment.Is64BitProcess);

            // Executes the "grammar-school" algorithm for computing q = a / b.
            // Before calculating q_i, we get more bits into the highest bit
            // block of the divisor. Thus, guessing digits of the quotient
            // will be more precise. Additionally we'll get r = a % b.

            ulong divHi = right[right.Length - 1];
            ulong divLo = right.Length > 1 ? right[right.Length - 2] : 0;

            // We measure the leading zeros of the divisor
            int shift = BitOperations.LeadingZeroCount(divHi);
            int backShift = 64 - shift;

            // And, we make sure the most significant bit is set
            if (shift > 0)
            {
                ulong divNx = right.Length > 2 ? right[right.Length - 3] : 0;

                divHi = (divHi << shift) | (divLo >> backShift);
                divLo = (divLo << shift) | (divNx >> backShift);
            }

            // Then, we divide all of the bits as we would do it using
            // pen and paper: guessing the next digit, subtracting, ...
            for (int i = left.Length; i >= right.Length; i--)
            {
                int n = i - right.Length;

                ulong t = (uint)i < (uint)left.Length ? left[i] : 0;
                ulong valHi = t;
                ulong valMi = left[i - 1];
                ulong valLo = i > 1 ? left[i - 2] : 0;

                // We shifted the divisor, we shift the dividend too
                if (shift > 0)
                {
                    ulong valNx = i > 2 ? left[i - 3] : 0;

                    valHi = (valHi << shift) | (valMi >> backShift);
                    valMi = (valMi << shift) | (valLo >> backShift);
                    valLo = (valLo << shift) | (valNx >> backShift);
                }

                // First guess for the current digit of the quotient,
                // which naturally must have only 64 bits...
                ulong digit = valHi >= divHi ? 0xFFFFFFFFFFFFFFFF : DivRem64(valHi, valMi, divHi, out _);

                // Our first guess may be a little bit to big
                while (DivideGuessTooBig(digit, valHi, valMi, valLo, divHi, divLo))
                    --digit;

                if (digit > 0)
                {
                    // Now it's time to subtract our current quotient
                    ulong carry = SubtractDivisor(left.Slice(n), right, digit);
                    if (carry != t)
                    {
                        Debug.Assert(carry == t + 1);

                        // Our guess was still exactly one too high
                        carry = AddDivisor(left.Slice(n), right);
                        --digit;

                        Debug.Assert(carry == 1);
                    }
                }

                // We have the digit!
                if ((uint)n < (uint)quotient.Length)
                    quotient[n] = digit;

                if ((uint)i < (uint)left.Length)
                    left[i] = 0;
            }

            static ulong AddDivisor(Span<ulong> left, ReadOnlySpan<ulong> right)
            {
                Debug.Assert(left.Length >= right.Length);

                // Repairs the dividend, if the last subtract was too much

                ulong carry = 0UL;

                for (int i = 0; i < right.Length; i++)
                {
                    ulong hi = 0;
                    ref ulong leftElement = ref left[i];
                    leftElement += carry;
                    if (leftElement < carry)
                        ++hi;
                    leftElement += right[i];
                    if (leftElement < right[i])
                        ++hi;

                    carry = hi;
                }

                return carry;
            }

            static ulong SubtractDivisor(Span<ulong> left, ReadOnlySpan<ulong> right, ulong q)
            {
                Debug.Assert(left.Length >= right.Length);

                // Combines a subtract and a multiply operation, which is naturally
                // more efficient than multiplying and then subtracting...

                ulong carry = 0;
                ulong carryHi = 0;

                for (int i = 0; i < right.Length; i++)
                {
                    ulong hi = Math.BigMul(right[i], q, out ulong digit);
                    digit += carry;
                    if (digit < carry)
                        ++hi;
                    carry = carryHi + hi;
                    carryHi = carry < hi ? 1u : 0;

                    ref ulong leftElement = ref left[i];
                    if (leftElement < digit)
                    {
                        if (++carry == 0)
                            ++carryHi;
                    }
                    leftElement -= digit;
                }

                return carry;
            }

            static bool DivideGuessTooBig(ulong q, ulong valHi, ulong valMi, ulong valLo, ulong divHi, ulong divLo)
            {
                // We multiply the two most significant limbs of the divisor
                // with the current guess for the quotient. If those are bigger
                // than the three most significant limbs of the current dividend
                // we return true, which means the current guess is still too big.

                ulong chkHi = Math.BigMul(divHi, q, out ulong chkMi1);
                ulong chkMi2 = Math.BigMul(divLo, q, out ulong chkLo);

                ulong chkMi = chkMi1 + chkMi2;
                if (chkMi < chkMi1)
                    ++chkHi;

                if (chkHi > valHi)
                    return true;
                if (chkHi == valHi)
                {
                    if (chkMi > valMi)
                        return true;
                    if (chkMi == valMi)
                        return chkLo > valLo;
                }

                return false;
            }
        }

        private static void DivideBurnikelZiegler(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient, Span<nuint> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1);
            Debug.Assert(remainder.Length == left.Length
                        || remainder.Length == 0);

            // Executes the Burnikel-Ziegler algorithm for computing q = a / b.
            //
            // Burnikel, C., Ziegler, J.: Fast recursive division. Research Report MPI-I-98-1-022, MPI Saarbrücken, 1998


            // Fast recursive division: Algorithm 3
            int n;
            {
                int m = (int)BitOperations.RoundUpToPowerOf2((uint)(right.Length + DivideThreshold - 1) / (uint)DivideThreshold);

                int j = (right.Length + m - 1) / m; // Ceil(right.Length/m)
                n = j * m;
            }

            int sigmaDigit = n - right.Length;
            int sigmaSmall = BitOperations.LeadingZeroCount(right[^1]);

            nuint[]? bFromPool = null;

            Span<nuint> b = (n <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : bFromPool = ArrayPool<nuint>.Shared.Rent(n)).Slice(0, n);

            int aLength = left.Length + sigmaDigit;

            // if: BitOperations.LeadingZeroCount(left[^1]) < sigmaSmall, requires one more digit obviously.
            // if: BitOperations.LeadingZeroCount(left[^1]) == sigmaSmall, requires one more digit, because the leftmost bit of a must be 0.

            if (BitOperations.LeadingZeroCount(left[^1]) <= sigmaSmall)
                ++aLength;

            nuint[]? aFromPool = null;

            Span<nuint> a = (aLength <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : aFromPool = ArrayPool<nuint>.Shared.Rent(aLength)).Slice(0, aLength);

            // 4. normalize
            static void Normalize(ReadOnlySpan<nuint> src, int sigmaDigit, int sigmaSmall, Span<nuint> bits)
            {
                int bitSize = 8 * Unsafe.SizeOf<nuint>();
                Debug.Assert((uint)sigmaSmall <= bitSize);
                Debug.Assert(src.Length + sigmaDigit <= bits.Length);

                bits.Slice(0, sigmaDigit).Clear();
                Span<nuint> dst = bits.Slice(sigmaDigit);
                src.CopyTo(dst);
                dst.Slice(src.Length).Clear();

                if (sigmaSmall != 0)
                {
                    // Left shift
                    int carryShift = bitSize - sigmaSmall;
                    nuint carry = 0;
                    for (int i = 0; i < bits.Length; i++)
                    {
                        nuint carryTmp = bits[i] >> carryShift;
                        bits[i] = bits[i] << sigmaSmall | carry;
                        carry = carryTmp;
                    }
                    Debug.Assert(carry == 0);
                }
            }

            Normalize(left, sigmaDigit, sigmaSmall, a);
            Normalize(right, sigmaDigit, sigmaSmall, b);


            int t = Math.Max(2, (a.Length + n - 1) / n); // Max(2, Ceil(a.Length/n))
            Debug.Assert(t < a.Length || (t == a.Length && (int)a[^1] >= 0));

            nuint[]? rFromPool = null;
            Span<nuint> r = ((n + 1) <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : rFromPool = ArrayPool<nuint>.Shared.Rent(n + 1)).Slice(0, n + 1);

            nuint[]? zFromPool = null;
            Span<nuint> z = (2 * n <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : zFromPool = ArrayPool<nuint>.Shared.Rent(2 * n)).Slice(0, 2 * n);
            a.Slice((t - 2) * n).CopyTo(z);
            z.Slice(a.Length - (t - 2) * n).Clear();

            Span<nuint> quotientUpper = quotient.Slice((t - 2) * n);
            if (quotientUpper.Length < n)
            {
                nuint[]? qFromPool = null;
                Span<nuint> q = (n <= StackAllocThreshold ?
                                stackalloc nuint[StackAllocThreshold]
                                : qFromPool = ArrayPool<nuint>.Shared.Rent(n)).Slice(0, n);

                BurnikelZieglerD2n1n(z, b, q, r);

                Debug.Assert(q.Slice(quotientUpper.Length).Trim(0u).Length == 0);
                q.Slice(0, quotientUpper.Length).CopyTo(quotientUpper);

                if (qFromPool != null)
                    ArrayPool<nuint>.Shared.Return(qFromPool);
            }
            else
            {
                BurnikelZieglerD2n1n(z, b, quotientUpper.Slice(0, n), r);
                quotientUpper.Slice(n).Clear();
            }

            for (int i = t - 3; i >= 0; i--)
            {
                a.Slice(i * n, n).CopyTo(z);
                r.Slice(0, n).CopyTo(z.Slice(n));
                BurnikelZieglerD2n1n(z, b, quotient.Slice(i * n, n), r);
            }

            if (zFromPool != null)
                ArrayPool<nuint>.Shared.Return(zFromPool);
            if (bFromPool != null)
                ArrayPool<nuint>.Shared.Return(bFromPool);
            if (aFromPool != null)
                ArrayPool<nuint>.Shared.Return(aFromPool);

            Debug.Assert(r[^1] == 0);
            Debug.Assert(r.Slice(0, sigmaDigit).Trim(0u).Length == 0);
            if (remainder.Length != 0)
            {
                Span<nuint> rt = r.Slice(sigmaDigit);
                remainder.Slice(rt.Length).Clear();

                if (sigmaSmall != 0)
                {
                    // Right shift
                    int bitSize = 8 * Unsafe.SizeOf<nuint>();
                    int carryShift = bitSize - sigmaSmall;
                    nuint carry = 0;
                    for (int i = rt.Length - 1; i >= 0; i--)
                    {
                        nuint carryTmp = rt[i] << carryShift;
                        rt[i] = rt[i] >> sigmaSmall | carry;
                        carry = carryTmp;
                    }
                    Debug.Assert(carry == 0);
                }

                rt.CopyTo(remainder);
            }

            if (rFromPool != null)
                ArrayPool<nuint>.Shared.Return(rFromPool);
        }

        private static void BurnikelZieglerFallback(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient, Span<nuint> remainder)
        {
            // Fast recursive division: Algorithm 1
            // 1. If n is odd or smaller than some convenient constant

            Debug.Assert(left.Length == 2 * right.Length);
            Debug.Assert(CompareActual(left.Slice(right.Length), right) < 0);
            Debug.Assert(quotient.Length == right.Length);
            Debug.Assert(remainder.Length >= right.Length + 1);
            Debug.Assert(right[^1] > 0);

            left = left.Slice(0, ActualLength(left));

            if (left.Length < right.Length)
            {
                quotient.Clear();
                left.CopyTo(remainder);
                remainder.Slice(left.Length).Clear();
            }
            else
            {
                nuint[]? r1FromPool = null;
                Span<nuint> r1 = (left.Length <= StackAllocThreshold ?
                                stackalloc nuint[StackAllocThreshold]
                                : r1FromPool = ArrayPool<nuint>.Shared.Rent(left.Length)).Slice(0, left.Length);

                left.CopyTo(r1);
                int quotientLength = Math.Min(left.Length - right.Length + 1, quotient.Length);

                quotient.Slice(quotientLength).Clear();
                DivideGrammarSchool(r1, right, quotient.Slice(0, quotientLength));

                if (r1.Length < remainder.Length)
                {
                    remainder.Slice(r1.Length).Clear();
                    r1.CopyTo(remainder);
                }
                else
                {
                    Debug.Assert(r1.Slice(remainder.Length).Trim(0u).Length == 0);
                    r1.Slice(0, remainder.Length).CopyTo(remainder);
                }

                if (r1FromPool != null)
                    ArrayPool<nuint>.Shared.Return(r1FromPool);
            }
        }


        private static void BurnikelZieglerD2n1n(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> quotient, Span<nuint> remainder)
        {
            // Fast recursive division: Algorithm 1
            Debug.Assert(left.Length == 2 * right.Length);
            Debug.Assert(CompareActual(left.Slice(right.Length), right) < 0);
            Debug.Assert(quotient.Length == right.Length);
            Debug.Assert(remainder.Length >= right.Length + 1);
            Debug.Assert(right[^1] > 0);

            if (right.Length % 2 != 0 || right.Length < DivideThreshold)
            {
                BurnikelZieglerFallback(left, right, quotient, remainder);
                return;
            }

            int halfN = right.Length >> 1;

            nuint[]? r1FromPool = null;
            Span<nuint> r1 = ((right.Length + 1) <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : r1FromPool = ArrayPool<nuint>.Shared.Rent(right.Length + 1)).Slice(0, right.Length + 1);

            BurnikelZieglerD3n2n(left.Slice(right.Length), left.Slice(halfN, halfN), right, quotient.Slice(halfN), r1);
            BurnikelZieglerD3n2n(r1.Slice(0, right.Length), left.Slice(0, halfN), right, quotient.Slice(0, halfN), remainder);

            if (r1FromPool != null)
                ArrayPool<nuint>.Shared.Return(r1FromPool);
        }

        private static void BurnikelZieglerD3n2n(ReadOnlySpan<nuint> left12, ReadOnlySpan<nuint> left3, ReadOnlySpan<nuint> right, Span<nuint> quotient, Span<nuint> remainder)
        {
            // Fast recursive division: Algorithm 2
            Debug.Assert(right.Length % 2 == 0);
            Debug.Assert(left12.Length == right.Length);
            Debug.Assert(2 * left3.Length == right.Length);
            Debug.Assert(2 * quotient.Length == right.Length);
            Debug.Assert(remainder.Length >= right.Length + 1);
            Debug.Assert(right[^1] > 0);

            int halfN = right.Length >> 1;

            ReadOnlySpan<nuint> a1 = left12.Slice(halfN);
            ReadOnlySpan<nuint> b1 = right.Slice(halfN);
            ReadOnlySpan<nuint> b2 = right.Slice(0, halfN);
            Span<nuint> r1 = remainder.Slice(halfN);

            if (CompareActual(a1, b1) < 0)
            {
                BurnikelZieglerD2n1n(left12, b1, quotient, r1);
            }
            else
            {
                quotient.Fill(uint.MaxValue);

                nuint[]? bbFromPool = null;

                Span<nuint> bb = (left12.Length <= StackAllocThreshold ?
                                stackalloc nuint[StackAllocThreshold]
                                : bbFromPool = ArrayPool<nuint>.Shared.Rent(left12.Length)).Slice(0, left12.Length);
                b1.CopyTo(bb.Slice(halfN));
                r1.Clear();

                SubtractSelf(bb, b1);
                SubtractSelf(r1, bb);

                if (bbFromPool != null)
                    ArrayPool<nuint>.Shared.Return(bbFromPool);
            }


            nuint[]? dFromPool = null;

            Span<nuint> d = (right.Length <= StackAllocThreshold ?
                            stackalloc nuint[StackAllocThreshold]
                            : dFromPool = ArrayPool<nuint>.Shared.Rent(right.Length)).Slice(0, right.Length);
            d.Clear();

            MultiplyActual(quotient, b2, d);

            // R = [R1, A3]
            left3.CopyTo(remainder.Slice(0, halfN));

            Span<nuint> rr = remainder.Slice(0, d.Length + 1);

            while (CompareActual(rr, d) < 0)
            {
                AddSelf(rr, right);
                int qi = -1;
                while (quotient[++qi] == 0) ;
                Debug.Assert((uint)qi < (uint)quotient.Length);
                --quotient[qi];
                quotient.Slice(0, qi).Fill(nuint.MaxValue);
            }

            SubtractSelf(rr, d);

            if (dFromPool != null)
                ArrayPool<nuint>.Shared.Return(dFromPool);

            static void MultiplyActual(ReadOnlySpan<nuint> left, ReadOnlySpan<nuint> right, Span<nuint> bits)
            {
                Debug.Assert(bits.Length == left.Length + right.Length);

                left = left.Slice(0, ActualLength(left));
                right = right.Slice(0, ActualLength(right));
                bits = bits.Slice(0, left.Length + right.Length);

                if (left.Length < right.Length)
                    Multiply(right, left, bits);
                else
                    Multiply(left, right, bits);
            }
        }
    }
}

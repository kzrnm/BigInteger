// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;

namespace Kzrnm.Numerics.Decimal
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

        public static void Divide(ReadOnlySpan<ulong> left, ulong right, Span<ulong> quotient, out ulong remainder)
        {
            DummyForDebug(quotient);
            var carry = 0ul;
            DivideImpl(left, right, quotient, ref carry);
            remainder = (ulong)carry;
        }

        public static void Divide(ReadOnlySpan<ulong> left, ulong right, Span<ulong> quotient)
        {
            DummyForDebug(quotient);
            var carry = 0ul;
            DivideImpl(left, right, quotient, ref carry);
        }

        static void DivideImpl(ReadOnlySpan<ulong> left, ulong right, Span<ulong> quotient, ref ulong carry)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(quotient.Length == left.Length);
            DummyForDebug(quotient);

            // Executes the division for one big and one 32-bit integer.
            // Thus, we've similar code than below, but there is no loop for
            // processing the 32-bit integer, since it's a single element.

            for (int i = left.Length - 1; i >= 0; i--)
            {
                quotient[i] = DivRem(carry, left[i], right, out carry);
            }
        }

        public static ulong Remainder(ReadOnlySpan<ulong> left, ulong right)
        {
            Debug.Assert(left.Length >= 1);

            // Same as above, but only computing the remainder.
            ulong carry = 0UL;
            for (int i = left.Length - 1; i >= 0; i--)
            {
                DivRem(carry, left[i], right, out carry);
            }

            return carry;
        }

        public static void Divide(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1);
            Debug.Assert(remainder.Length == left.Length);
            DummyForDebug(quotient);
            DummyForDebug(remainder);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
                DivideGrammarSchool(left, right, quotient, remainder);
            else
                DivideBurnikelZiegler(left, right, quotient, remainder);
        }

        public static void Divide(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1);
            DummyForDebug(quotient);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
            {
                ulong[]? remainderFromPool = null;
                Span<ulong> remainder = (left.Length <= StackAllocThreshold ?
                                      stackalloc ulong[StackAllocThreshold]
                                      : remainderFromPool = ArrayPool<ulong>.Shared.Rent(left.Length)).Slice(0, left.Length);

                DivideGrammarSchool(left, right, quotient, remainder);
                if (remainderFromPool != null)
                    ArrayPool<ulong>.Shared.Return(remainderFromPool);
            }
            else
                DivideBurnikelZiegler(left, right, quotient, default);

        }

        public static void Remainder(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(remainder.Length == left.Length);
            DummyForDebug(remainder);


            int quotientLength = left.Length - right.Length + 1;
            ulong[]? quotientFromPool = null;

            Span<ulong> quotient = (quotientLength <= StackAllocThreshold ?
                                  stackalloc ulong[StackAllocThreshold]
                                  : quotientFromPool = ArrayPool<ulong>.Shared.Rent(quotientLength)).Slice(0, quotientLength);

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
                DivideGrammarSchool(left, right, default, remainder);
            else
                DivideBurnikelZiegler(left, right, quotient, remainder);

            if (quotientFromPool != null)
                ArrayPool<ulong>.Shared.Return(quotientFromPool);

        }

        static void DivRem(Span<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient)
        {
            // quotient = left / right;
            // left %= right;

            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(quotient.Length == left.Length - right.Length + 1
                || quotient.Length == 0);
            DummyForDebug(quotient);

            ulong[]? leftCopyFromPool = null;
            Span<ulong> leftCopy = (left.Length <= StackAllocThreshold ?
                                  stackalloc ulong[StackAllocThreshold]
                                  : leftCopyFromPool = ArrayPool<ulong>.Shared.Rent(left.Length)).Slice(0, left.Length);
            left.CopyTo(leftCopy);

            ulong[]? quotientActualFromPool = null;
            scoped Span<ulong> quotientActual;

            if (quotient.Length == 0)
            {
                int quotientLength = left.Length - right.Length + 1;

                quotientActual = (quotientLength <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : quotientActualFromPool = ArrayPool<ulong>.Shared.Rent(quotientLength)).Slice(0, quotientLength);
            }
            else
                quotientActual = quotient;

            if (right.Length < DivideThreshold || left.Length - right.Length < DivideThreshold)
                DivideGrammarSchool(leftCopy, right, quotient, left);
            else
                DivideBurnikelZiegler(leftCopy, right, quotientActual, left);

            if (quotientActualFromPool != null)
                ArrayPool<ulong>.Shared.Return(quotientActualFromPool);
            if (leftCopyFromPool != null)
                ArrayPool<ulong>.Shared.Return(leftCopyFromPool);
        }

        static void DivideGrammarSchool(ReadOnlySpan<ulong> left, scoped ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(remainder.Length == left.Length);
            Debug.Assert(
                quotient.Length == 0
                || quotient.Length == left.Length - right.Length + 1
                || (CompareActual(left.Slice(left.Length - right.Length), right) < 0 && quotient.Length == left.Length - right.Length));
            ulong mul = Base / (right[^1] + 1);
            ulong[]? leftMulFromPool = null;
            ulong[]? rightMulFromPool = null;
            Span<ulong> leftMul = (left.Length + 1 < StackAllocThreshold
                    ? stackalloc ulong[StackAllocThreshold]
                    : leftMulFromPool = ArrayPool<ulong>.Shared.Rent(left.Length + 1)).Slice(0, left.Length + 1);

            leftMul[^1] = 0;
            scoped ReadOnlySpan<ulong> rightMul;

            if (mul != 1)
            {
                {
                    var tmp = (right.Length < StackAllocThreshold
                           ? stackalloc ulong[StackAllocThreshold]
                           : rightMulFromPool = ArrayPool<ulong>.Shared.Rent(right.Length)).Slice(0, right.Length);

                    int i = 0;
                    ulong carry = 0UL;

                    for (; i < right.Length; i++)
                    {
                        carry = BigMulAdd(right[i], mul, carry, out tmp[i]);
                    }
                    Debug.Assert(carry == 0);
                    rightMul = tmp;
                }
                {
                    int i = 0;
                    ulong carry = 0UL;

                    for (; i < left.Length; i++)
                    {
                        carry = BigMulAdd(left[i], mul, carry, out leftMul[i]);
                    }
                    leftMul[i] = carry;
                }
            }
            else
            {
                rightMul = right;
                left.CopyTo(leftMul);
            }
            leftMul = leftMul.TrimEnd(0u);
            Debug.Assert((uint)(leftMul.Length - left.Length) <= 1);

            DivideGrammarSchool(leftMul, rightMul, quotient);


            if (mul != 1)
            {
                ulong carry = leftMul.Length > remainder.Length ? leftMul[^1] : 0;
                for (int i = remainder.Length - 1; i >= 0; i--)
                {
                    remainder[i] = DivRem(carry, leftMul[i], mul, out carry);
                }
            }
            else
            {
                remainder.Clear();
                leftMul.TrimEnd(0u).CopyTo(remainder);
            }

            if (leftMulFromPool != null)
                ArrayPool<ulong>.Shared.Return(leftMulFromPool);
            if (rightMulFromPool != null)
                ArrayPool<ulong>.Shared.Return(rightMulFromPool);
        }

        static void DivideGrammarSchool(Span<ulong> left, scoped ReadOnlySpan<ulong> right, Span<ulong> quotient)
        {
            Debug.Assert(left.Length >= 1);
            Debug.Assert(right.Length >= 1);
            Debug.Assert(left.Length >= right.Length);
            Debug.Assert(
                quotient.Length == 0
                || quotient.Length == left.Length - right.Length + 1
                || (CompareActual(left.Slice(left.Length - right.Length), right) < 0 && quotient.Length == left.Length - right.Length));

            Debug.Assert(right[^1] >= Base / 2);

            // Executes the "grammar-school" algorithm for computing q = a / b.
            // Before calculating q_i, we get more bits into the highest bit
            // block of the divisor. Thus, guessing digits of the quotient
            // will be more precise. Additionally we'll get r = a % b.

            ulong divHi = right[right.Length - 1];
            ulong divLo = right.Length > 1 ? right[right.Length - 2] : 0;


            // Then, we divide all of the bits as we would do it using
            // pen and paper: guessing the next digit, subtracting, ...
            for (int i = left.Length; i >= right.Length; i--)
            {
                int n = i - right.Length;
                ulong valHi = (uint)i < (uint)left.Length ? left[i] : 0;
                ulong valMi = left[i - 1];
                ulong valLo = i > 1 ? left[i - 2] : 0;

                // First guess for the current digit of the quotient,
                // which naturally must have only 32 bits...
                ulong digit = valHi >= divHi ? ulong.MaxValue : DivRem(valHi, valMi, divHi, out _);

                // Our first guess may be a little bit to big
                while (DivideGuessTooBig(digit, valHi, valMi, valLo, divHi, divLo))
                    --digit;

                if (digit > 0)
                {
                    // Now it's time to subtract our current quotient
                    ulong carry = SubtractDivisor(left.Slice(n), right, digit);
                    if (carry != valHi)
                    {
                        Debug.Assert(carry == valHi + 1);

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
                    ref var leftElement = ref left[i];
                    var hi = SafeAdd(ref leftElement, carry);
                    hi += SafeAdd(ref leftElement, right[i]);
                    carry = hi;
                }

                return carry;
            }


            static ulong SubtractDivisor(Span<ulong> left, ReadOnlySpan<ulong> right, ulong q)
            {
                Debug.Assert(left.Length >= right.Length);

                // Combines a subtract and a multiply operation, which is naturally
                // more efficient than multiplying and then subtracting...

                ulong carry = 0UL;

                for (int i = 0; i < right.Length; i++)
                {
                    carry = BigMulAdd(right[i], q, carry, out var digit);
                    ref var leftElement = ref left[i];
                    if (leftElement < digit)
                    {
                        ++carry;
                        leftElement += Base;
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


                var chkHi = BigMul(divHi, q, out var chkMi);
                chkHi += SafeAdd(ref chkMi, BigMul(divLo, q, out var chkLo));

                if (chkHi > valHi)
                    return true;
                if (chkHi == valHi)
                {
                    return (chkMi > valMi) || ((chkMi == valHi) && (chkLo > valLo));
                }
                return false;
            }
        }
        static void DivideBurnikelZiegler(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
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
            var sigmaMul = Base / (right[^1] + 1);

            ulong[]? bFromPool = null;

            Span<ulong> b = (n <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : bFromPool = ArrayPool<ulong>.Shared.Rent(n)).Slice(0, n);

            int aLength = left.Length + sigmaDigit;

            // if: BitOperations.LeadingZeroCount(left[^1]) < sigmaSmall, requires one more digit obviously.
            // if: BitOperations.LeadingZeroCount(left[^1]) == sigmaSmall, requires one more digit, because the leftmost bit of a must be 0.
            {
                if (Math.BigMul(left[^1], sigmaMul, out var aTop) > 0 || aTop >= Base / 10)
                    ++aLength;
            }


            ulong[]? aFromPool = null;

            Span<ulong> a = (aLength <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : aFromPool = ArrayPool<ulong>.Shared.Rent(aLength)).Slice(0, aLength);

            // 4. normalize
            static void Normalize(ReadOnlySpan<ulong> src, int sigmaDigit, ulong sigmaMul, Span<ulong> bits)
            {
                Debug.Assert(src.Length + sigmaDigit <= bits.Length);

                bits.Slice(0, sigmaDigit).Clear();
                Span<ulong> dst = bits.Slice(sigmaDigit);
                src.CopyTo(dst);
                dst.Slice(src.Length).Clear();

                if (sigmaMul > 1)
                {
                    int i = 0;
                    ulong carry = 0UL;

                    for (; i < dst.Length; i++)
                    {
                        carry = BigMulAdd(dst[i], sigmaMul, carry, out dst[i]);
                    }
                    Debug.Assert(carry == 0);
                }
            }

            Normalize(left, sigmaDigit, sigmaMul, a);
            Normalize(right, sigmaDigit, sigmaMul, b);

            int t = Math.Max(2, (a.Length + n - 1) / n); // Max(2, Ceil(a.Length/n))
            Debug.Assert(t < a.Length || (t == a.Length && (int)a[^1] >= 0));



            ulong[]? rFromPool = null;
            Span<ulong> r = ((n + 1) <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : rFromPool = ArrayPool<ulong>.Shared.Rent(n + 1)).Slice(0, n + 1);

            ulong[]? zFromPool = null;
            Span<ulong> z = (2 * n <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : zFromPool = ArrayPool<ulong>.Shared.Rent(2 * n)).Slice(0, 2 * n);
            a.Slice((t - 2) * n).CopyTo(z);
            z.Slice(a.Length - (t - 2) * n).Clear();

            Span<ulong> quotientUpper = quotient.Slice((t - 2) * n);

            if (quotientUpper.Length < n)
            {
                ulong[]? qFromPool = null;
                Span<ulong> q = (n <= StackAllocThreshold ?
                                stackalloc ulong[StackAllocThreshold]
                                : qFromPool = ArrayPool<ulong>.Shared.Rent(n)).Slice(0, n);

                BurnikelZieglerD2n1n(z, b, q, r);

                Debug.Assert(q.Slice(quotientUpper.Length).Trim(0u).Length == 0);
                q.Slice(0, quotientUpper.Length).CopyTo(quotientUpper);

                if (qFromPool != null)
                    ArrayPool<ulong>.Shared.Return(qFromPool);
            }
            else
            {
                BurnikelZieglerD2n1n(z, b, quotientUpper.Slice(0, n), r);
                quotientUpper.Slice(n).Clear();
            }

            if (t > 2)
            {
                a.Slice((t - 3) * n, n).CopyTo(z);
                r.Slice(0, n).CopyTo(z.Slice(n));

                for (int i = t - 3; i > 0; i--)
                {
                    BurnikelZieglerD2n1n(z, b, quotient.Slice(i * n, n), r);

                    a.Slice((i - 1) * n, n).CopyTo(z);
                    r.Slice(0, n).CopyTo(z.Slice(n));
                }

                BurnikelZieglerD2n1n(z, b, quotient.Slice(0, n), r);
            }

            if (zFromPool != null)
                ArrayPool<ulong>.Shared.Return(zFromPool);
            if (bFromPool != null)
                ArrayPool<ulong>.Shared.Return(bFromPool);
            if (aFromPool != null)
                ArrayPool<ulong>.Shared.Return(aFromPool);

            Debug.Assert(r[^1] == 0);
            Debug.Assert(r.Slice(0, sigmaDigit).Trim(0u).Length == 0);
            if (remainder.Length != 0)
            {
                Span<ulong> rt = r.Slice(sigmaDigit);
                remainder.Slice(rt.Length).Clear();

                if (sigmaMul > 1)
                {
                    ulong carry = 0;
                    for (int i = rt.Length - 1; i >= 0; i--)
                    {
                        remainder[i] = DivRem(carry, rt[i], sigmaMul, out carry);
                    }
                }
                else
                    rt.CopyTo(remainder);
            }

            if (rFromPool != null)
                ArrayPool<ulong>.Shared.Return(rFromPool);
        }


        private static void BurnikelZieglerFallback(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
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
            else if (right.Length == 1)
            {
                ulong carry;

                if (quotient.Length < left.Length)
                {
                    Debug.Assert(quotient.Length + 1 == left.Length);
                    Debug.Assert(left[^1] < right[0]);

                    carry = left[^1];
                    DivideImpl(left.Slice(0, quotient.Length), right[0], quotient, ref carry);
                }
                else
                {
                    carry = 0;
                    quotient.Slice(left.Length).Clear();
                    DivideImpl(left, right[0], quotient, ref carry);
                }

                if (remainder.Length != 0)
                {
                    remainder.Slice(1).Clear();
                    remainder[0] = carry;
                }
            }
            else
            {
                ulong[]? r1FromPool = null;
                Span<ulong> r1 = (left.Length <= StackAllocThreshold ?
                                stackalloc ulong[StackAllocThreshold]
                                : r1FromPool = ArrayPool<ulong>.Shared.Rent(left.Length)).Slice(0, left.Length);

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
                    ArrayPool<ulong>.Shared.Return(r1FromPool);
            }
        }
        private static void BurnikelZieglerD2n1n(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
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

            ulong[]? r1FromPool = null;
            Span<ulong> r1 = ((right.Length + 1) <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : r1FromPool = ArrayPool<ulong>.Shared.Rent(right.Length + 1)).Slice(0, right.Length + 1);

            BurnikelZieglerD3n2n(left.Slice(right.Length), left.Slice(halfN, halfN), right, quotient.Slice(halfN), r1);
            BurnikelZieglerD3n2n(r1.Slice(0, right.Length), left.Slice(0, halfN), right, quotient.Slice(0, halfN), remainder);

            if (r1FromPool != null)
                ArrayPool<ulong>.Shared.Return(r1FromPool);
        }

        private static void BurnikelZieglerD3n2n(ReadOnlySpan<ulong> left12, ReadOnlySpan<ulong> left3, ReadOnlySpan<ulong> right, Span<ulong> quotient, Span<ulong> remainder)
        {
            // Fast recursive division: Algorithm 2
            Debug.Assert(right.Length % 2 == 0);
            Debug.Assert(left12.Length == right.Length);
            Debug.Assert(2 * left3.Length == right.Length);
            Debug.Assert(2 * quotient.Length == right.Length);
            Debug.Assert(remainder.Length >= right.Length + 1);
            Debug.Assert(right[^1] > 0);

            int halfN = right.Length >> 1;

            var a1 = left12.Slice(halfN);
            var b1 = right.Slice(halfN);
            var b2 = right.Slice(0, halfN);
            var r1 = remainder.Slice(halfN);

            if (CompareActual(a1, b1) < 0)
            {
                BurnikelZieglerD2n1n(left12, b1, quotient, r1);
            }
            else
            {
                quotient.Fill(uint.MaxValue);

                ulong[]? bbFromPool = null;

                Span<ulong> bb = (left12.Length <= StackAllocThreshold ?
                                stackalloc ulong[StackAllocThreshold]
                                : bbFromPool = ArrayPool<ulong>.Shared.Rent(left12.Length)).Slice(0, left12.Length);
                b1.CopyTo(bb.Slice(halfN));
                r1.Clear();

                SubtractSelf(bb, b1);
                SubtractSelf(r1, bb);

                if (bbFromPool != null)
                    ArrayPool<ulong>.Shared.Return(bbFromPool);
            }


            ulong[]? dFromPool = null;

            Span<ulong> d = (right.Length <= StackAllocThreshold ?
                            stackalloc ulong[StackAllocThreshold]
                            : dFromPool = ArrayPool<ulong>.Shared.Rent(right.Length)).Slice(0, right.Length);
            d.Clear();

            MultiplyActual(quotient, b2, d);

            // R = [R1, A3]
            left3.CopyTo(remainder.Slice(0, halfN));

            var rr = remainder.Slice(0, d.Length + 1);

            while (CompareActual(rr, d) < 0)
            {
                AddSelf(rr, right);
                int qi = -1;
                while (quotient[++qi] == 0) ;
                Debug.Assert((uint)qi < (uint)quotient.Length);
                --quotient[qi];
                quotient.Slice(0, qi).Fill(Base - 1);
            }

            SubtractSelf(rr, d);

            if (dFromPool != null)
                ArrayPool<ulong>.Shared.Return(dFromPool);

            static void MultiplyActual(ReadOnlySpan<ulong> left, ReadOnlySpan<ulong> right, Span<ulong> bits)
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

using Kzrnm.Numerics.Logic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Kzrnm.Numerics.Test
{
    using BigInteger = MyBigInteger;
    public class BigIntegerTests : BigIntegerTestsBase<BigInteger>
    {
        [Fact]
        public void DivideBound()
        {
            var right = (BigInteger.One << (BigIntegerCalculator.DivideBurnikelZieglerThreshold * 4 * 32 - 1))
                + (BigInteger.One << (BigIntegerCalculator.DivideBurnikelZieglerThreshold * 2 * 32)) - 1;
            var rem = right - 1;

            for (int i = 0; i < 130; i++)
            {
                var qi = BigIntegerCalculator.DivideBurnikelZieglerThreshold * 8 * 32 * 4 - 10 + i;
                var q = (BigInteger.One << qi) - 1;
                var left = q * right + rem;

                var (q2, r2) = BigInteger.DivRem(left, right);
                q2.ShouldBe(q);
                r2.ShouldBe(rem);
            }
        }

        [Fact]
        public void Powers1e9()
        {
            var buffer = new uint[5356];
            var powersOf1e9 = new Number.PowersOf1e9(buffer);
            var f9 = (uint)Math.Pow(5, 9);
            for (int i = 0; i < 13; i++)
            {
                var powers = powersOf1e9.GetSpan(i);
                (BitOperations.TrailingZeroCount(powers[0]) + 32 * Number.PowersOf1e9.OmittedLength(i)).ShouldBe(9 * (1 << i));
                for (int j = (1 << i) - 1; j >= 0; j--)
                {
                    var quo = new uint[powers.Length];
                    BigIntegerCalculator.Divide(powers, f9, quo, out var remainder);
                    remainder.ShouldBe(0u);
                    powers = quo.AsSpan().TrimEnd(0u);
                }

                powers.Length.ShouldBe(1);
                int shift = (9 * (1 << i) - 32 * Number.PowersOf1e9.OmittedLength(i));
                powers[0].ShouldBe(1u << shift);
            }
        }

        public static IEnumerable<TheoryDataRow<string>> ParseHex_Data()
        {
            for (int i = 1; i < 200; i++)
            {
                yield return "F" + new string('0', i) + "1";
                yield return "F" + new string('0', i - 1) + "1";

                for (int j = 1; j < 6; j++)
                {
                    yield return new string('F', j) + new string('0', i);
                }

                yield return "8" + new string('0', i);
            }

            var rnd = new Random(227);
            for (int len = 100; len < 200; len++)
            {
                for (int k = 0; k < 100; k++)
                {
                    yield return new string(Enumerable.Repeat(rnd, len).Select(rnd => ToCharUpper(rnd.Next())).ToArray());
                }
            }

            static char ToCharUpper(int value)
            {
                value &= 0xF;
                value += '0';

                if (value > '9')
                {
                    value += ('A' - ('9' + 1));
                }

                return (char)value;
            }
        }

        [Theory]
        [MemberData(nameof(ParseHex_Data))]
        public void ParseHex(string s)
        {
            var expected = OrigBigInteger.Parse(s, NumberStyles.HexNumber);
            Equal(BigInteger.Parse(s, NumberStyles.HexNumber), expected);

            BigInteger.TryParse(s, NumberStyles.HexNumber, null, out var result).ShouldBeTrue();
            Equal(result, expected);

#if NET7_0_OR_GREATER
            Equal(BigInteger.Parse(Encoding.UTF8.GetBytes(s), NumberStyles.HexNumber), expected);
#endif
        }

#if NET9_0_OR_GREATER

        public static IEnumerable<TheoryDataRow<string>> ParseBin_Data()
        {
            yield return "0";
            yield return "0111111111111111111111111111111111";
            yield return "111111111111111111111111111111110";
            yield return "100000000000000000000000000000001";
            yield return "100000000000000000000000000000000";

            for (int i = 0; i < 300; i++)
            {
                yield return "1" + new string('0', i);
                yield return "01" + new string('0', i);
            }
        }

        [Theory]
        [MemberData(nameof(ParseBin_Data))]
        public void ParseBin(string s)
        {
            var expected = OrigBigInteger.Parse(s, NumberStyles.BinaryNumber);
            Equal(BigInteger.Parse(s, NumberStyles.BinaryNumber), expected);

            BigInteger.TryParse(s, NumberStyles.BinaryNumber, null, out var result).ShouldBeTrue();
            Equal(result, expected);

            Equal(BigInteger.Parse(Encoding.UTF8.GetBytes(s), NumberStyles.BinaryNumber), expected);
        }
#endif

        [Fact]
        public void RandomDivRem()
        {
            var rnd = new Random(227);
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                bytes1.AsSpan().Fill(255);
                bytes2[^1] = 0x80;

                var left = new BigInteger(bytes1, isUnsigned: true);
                var right = new BigInteger(bytes2, isUnsigned: true);

                var quo = OrigBigInteger.DivRem(left, right, out var rem);
                var (quo2, rem2) = BigInteger.DivRem(left, right);
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                rnd.NextBytes(bytes1);
                bytes2[^1] = 0x80;

                var left = new BigInteger(bytes1, isUnsigned: true);
                var right = new BigInteger(bytes2, isUnsigned: true);

                var quo = OrigBigInteger.DivRem(left, right, out var rem);
                var (quo2, rem2) = BigInteger.DivRem(left, right);
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                rnd.NextBytes(bytes1);
                rnd.NextBytes(bytes2);

                var left = new BigInteger(bytes1, isUnsigned: true);
                var right = new BigInteger(bytes2, isUnsigned: true);

                var quo = OrigBigInteger.DivRem(left, right, out var rem);
                var (quo2, rem2) = BigInteger.DivRem(left, right);
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[rnd.Next(700, 1200)];
                var bytes2 = new byte[rnd.Next(400, bytes1.Length * 2 / 3)];

                rnd.NextBytes(bytes1);
                rnd.NextBytes(bytes2);

                var left = new BigInteger(bytes1, isUnsigned: true);
                var right = new BigInteger(bytes2, isUnsigned: true);

                var quo = OrigBigInteger.DivRem(left, right, out var rem);
                var (quo2, rem2) = BigInteger.DivRem(left, right);
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
        }

        [Fact]
        public void ToStringTest()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: true);
                OrigBigInteger expected = my;
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
                (-my).ToString().ShouldBe($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: false);
                OrigBigInteger expected = my;
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(1000, 2000)];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: false);
                OrigBigInteger expected = my;
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
            }
            for (int i = 1; i < 50; i++)
            {
                var bytes = new byte[i];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: true);
                OrigBigInteger expected = my;
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
                (-my).ToString().ShouldBe($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                int num = rnd.Next() - int.MaxValue / 2;
                BigInteger my = num;
                OrigBigInteger expected = num;
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
            }
        }


        [Fact]
        public void ToStringBoundTest()
        {
            foreach (var s in new[]
            {
                new string('9', 9* (1<<10))+new string('9', 9* (1<<10)),
                "1"+new string('0', 9* (1<<10))+new string('9', 9* (1<<10)),
                "1"+new string('0', 9* (1<<10)-1)+"1"+new string('9', 9* (1<<10)),
                "1"+new string('0', 9* (1<<11)-1)+"1",
            })
            {
                OrigBigInteger expected = OrigBigInteger.Parse(s);
                BigInteger my = new BigInteger(expected.ToByteArray(), isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().ShouldBe(expectedStr);
            }
        }

        [Fact]
        public void ParseAndToStringHexTest()
        {
            Test("F0000001");

            foreach (int i in new int[] { 865, 20161 })
            {
                Test(new string('7', i));
                Test(new string('F', i));
            }

            for (int i = 1; i < 50; i++)
                Test(new string('0', i));

            for (int i = 0; i < 50; i++)
                Test("1" + new string('0', i));

            foreach (int i in new int[] { 1000, 10000 })
                Test("1" + new string('0', i));

            foreach (int i in new int[] { 1000, 10000 })
                Test(new string('1', i) + new string('0', i));

            for (int i = 1; i < 50; i++)
                Test(new string('1', i) + new string('0', i));

            for (int i = 1; i < 50; i++)
                Test(new string('1', 500 + i) + new string('0', 3000 + i));
            for (int i = 1; i < 50; i++)
                Test(new string('1', 3000 + i) + new string('0', 3000 + i));
            for (int i = 1; i < 50; i++)
                Test("1" + new string('0', 3000 + i));

            foreach (var v in PlusMinus(0))
                Test(v.ToString("X"));
            foreach (var v in PlusMinus(int.MinValue))
                Test(v.ToString("X"));
            foreach (var v in PlusMinus(int.MaxValue))
                Test(v.ToString("X"));
            foreach (var v in PlusMinus(long.MinValue))
                Test(v.ToString("X"));
            foreach (var v in PlusMinus(long.MaxValue))
                Test(v.ToString("X"));

            var rnd = new Random(227);
            for (int i = 0; i < 100; i++)
                Test(rnd.GetRandomDigits(rnd.Next(2000, 5000)));

            void Test(string s)
            {
                var my = BigInteger.Parse(s, NumberStyles.HexNumber, null);
                var orig = OrigBigInteger.Parse(s, NumberStyles.HexNumber, null);
                Equal(my, orig);

#if NET7_0_OR_GREATER
                my = BigInteger.Parse(Encoding.UTF8.GetBytes(s), NumberStyles.HexNumber, null);
                Equal(my, orig);
#endif
            }
        }

#if NET7_0_OR_GREATER
        [Fact]
        public void ParseAndFormatCurrency()
        {
            Test("¤999,999,999,999,999,999,999,999,999,999,999,999,999,999,999,999,999,999,999.00");

            void Test(string s)
            {
                var num = BigInteger.Parse(s, NumberStyles.Currency, CultureInfo.InvariantCulture);
                num.ToString("C", CultureInfo.InvariantCulture).ShouldBe(s);
                {
                    var TryFormatUtf8Delegate = (TryFormatUtf8)Delegate.CreateDelegate(typeof(TryFormatUtf8), num, TryFormatUtf8Method);
                    var dst = new byte[Encoding.UTF8.GetByteCount(s)];
                    TryFormatUtf8Delegate(dst, out int bytesWritten, "C", CultureInfo.InvariantCulture).ShouldBeTrue();
                    bytesWritten.ShouldBe(Encoding.UTF8.GetByteCount(s));
                    dst.ShouldBe(Encoding.UTF8.GetBytes(s));
                }
            }
        }
#endif
    }

    [Collection(nameof(DisableParallelization))]
    public class BigIntegerThresholdTests : ThresholdTestsBase
    {
        [Fact]
        public void ParseTrailingZero()
        {
            RunWithFakeThreshold(Number.BigIntegerParseNaiveThresholdInRecursive, 12, () =>
                RunWithFakeThreshold(Number.BigIntegerParseNaiveThreshold, 0, () =>
                {
                    var rnd = new Random(227);
                    for (int i = 0; i < 1000; i++)
                    {
                        var s = rnd.GetRandomDigits(rnd.Next(1, 100)) + "1" + new string('0', 8);
                        var num = BigInteger.Parse(s, null);
                        $"{num}".ShouldBe(s);
                        num.ToString().ShouldBe(s);
                    }
                })
            );
        }
    }
}

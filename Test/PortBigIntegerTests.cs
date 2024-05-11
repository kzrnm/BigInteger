using Kzrnm.Numerics.Logic;
using System.Globalization;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    using BigInteger = PortBigInteger;
    public class PortBigIntegerTests : BigIntegerTestsBase<BigInteger>
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
                q2.Should().Be(q);
                r2.Should().Be(rem);
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
                (BitOperations.TrailingZeroCount(powers[0]) + 32 * Number.PowersOf1e9.OmittedLength(i)).Should().Be(9 * (1 << i));
                for (int j = (1 << i) - 1; j >= 0; j--)
                {
                    var quo = new uint[powers.Length];
                    BigIntegerCalculator.Divide(powers, f9, quo, out var remainder);
                    remainder.Should().Be(0);
                    powers = quo.AsSpan().TrimEnd(0u);
                }

                powers.Length.Should().Be(1);
                int shift = (9 * (1 << i) - 32 * Number.PowersOf1e9.OmittedLength(i));
                powers[0].Should().Be(1u << shift);
            }
        }

        [Fact]
        public void RandomDivRem()
        {
            var rnd = new Random(227);
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                bytes1.AsSpan().Fill(255);
                bytes2[^1] = 0x80;

                var (quo, rem) = OrigBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                var (quo2, rem2) = BigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                rnd.NextBytes(bytes1);
                bytes2[^1] = 0x80;

                var (quo, rem) = OrigBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                var (quo2, rem2) = BigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 4];
                var bytes2 = new byte[BigIntegerCalculator.DivideBurnikelZieglerThreshold * sizeof(uint) / sizeof(byte) * 2];

                rnd.NextBytes(bytes1);
                rnd.NextBytes(bytes2);

                var (quo, rem) = OrigBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                var (quo2, rem2) = BigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                Equal(quo2, quo);
                Equal(rem2, rem);
            }
            for (int i = 0; i < 100; i++)
            {
                var bytes1 = new byte[rnd.Next(700, 1200)];
                var bytes2 = new byte[rnd.Next(400, bytes1.Length * 2 / 3)];

                rnd.NextBytes(bytes1);
                rnd.NextBytes(bytes2);

                var (quo, rem) = OrigBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                var (quo2, rem2) = BigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
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
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(1000, 2000)];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 1; i < 50; i++)
            {
                var bytes = new byte[i];
                rnd.NextBytes(bytes);
                var my = new BigInteger(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                int num = rnd.Next() - int.MaxValue / 2;
                BigInteger my = num;
                OrigBigInteger expected = num;
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
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
                my.ToString().Should().Be(expectedStr);
            }
        }
    }

    public class PortBigIntegerThresholdTests : ThresholdTestsBase
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
                        $"{num}".Should().Be(s);
                        num.ToString().Should().Be(s);
                    }
                })
            );
        }
    }
}

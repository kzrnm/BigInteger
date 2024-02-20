using Kzrnm.Numerics.Logic;
using System.Globalization;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    public class BigIntegerTests : BigIntegerTestsBase<MyBigInteger>
    {
        [Fact]
        public void DivideBound()
        {
            var right = (MyBigInteger.One << (BigIntegerCalculator.DivideBurnikelZieglerThreshold * 4 * 32 - 1))
                + (MyBigInteger.One << (BigIntegerCalculator.DivideBurnikelZieglerThreshold * 2 * 32)) - 1;
            var rem = right - 1;

            for (int i = 0; i < 130; i++)
            {
                var qi = BigIntegerCalculator.DivideBurnikelZieglerThreshold * 8 * 32 * 4 - 10 + i;
                var q = (MyBigInteger.One << qi) - 1;
                var left = q * right + rem;

                var (q2, r2) = MyBigInteger.DivRem(left, right);
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
        public void ParseHex()
        {
            for (int i = 1; i < 300; i++)
            {
                var s = "F" + new string('0', i) + "1";
                Equal(MyBigInteger.Parse(s, NumberStyles.HexNumber), OrigBigInteger.Parse(s, NumberStyles.HexNumber));
            }
            for (int i = 1; i < 300; i++)
            {
                MyBigInteger.TryParse("F" + new string('0', i - 1) + "1", NumberStyles.HexNumber, null, out var result);
                result.Should().Be((MyBigInteger.MinusOne << (4 * i)) + 1);
            }
            for (int i = 0; i < 300; i++)
            {
                for (int j = 1; j < 6; j++)
                {
                    MyBigInteger.TryParse(new string('F', j) + new string('0', i), NumberStyles.HexNumber, null, out var result);
                    result.Should().Be(MyBigInteger.MinusOne << (4 * i));
                }
            }
            for (int i = 0; i < 300; i++)
            {
                MyBigInteger.TryParse("8" + new string('0', i), NumberStyles.HexNumber, null, out var result);
                result.Should().Be(MyBigInteger.MinusOne << (3 + 4 * i));
            }

            var rnd = new Random(227);
            for (int len = 1; len < 200; len++)
            {
                for (int k = 0; k < 60; k++)
                {
                    var s = Enumerable.Repeat(rnd, len).Select(rnd => HexConverter.ToCharUpper(rnd.Next())).ToArray();
                    Equal(MyBigInteger.Parse(s, NumberStyles.HexNumber), OrigBigInteger.Parse(s, NumberStyles.HexNumber));
                }
            }
        }

#if NET8_0_OR_GREATER
        [Fact]
        public void ParseBin()
        {
            MyBigInteger.Parse("0", NumberStyles.BinaryNumber).Should().Be(0);
            MyBigInteger.Parse("0111111111111111111111111111111111", NumberStyles.BinaryNumber).Should().Be(0x1FFFFFFFFL);
            MyBigInteger.Parse("111111111111111111111111111111110", NumberStyles.BinaryNumber).Should().Be(-2);
            MyBigInteger.Parse("100000000000000000000000000000001", NumberStyles.BinaryNumber).Should().Be((-1L << 32) + 1);
            MyBigInteger.Parse("100000000000000000000000000000000", NumberStyles.BinaryNumber).Should().Be(-1L << 32);

            for (int i = 0; i < 300; i++)
            {
                MyBigInteger.TryParse("1" + new string('0', i), NumberStyles.BinaryNumber, null, out var result);
                result.Should().Be(MyBigInteger.MinusOne << i);
            }

            var rnd = new Random(227);
            for (int len = 1; len < 5000; len++)
            {
                for (int k = 0; k < 20; k++)
                {
                    var s = Enumerable.Repeat(rnd, len).Select(rnd => (char)(rnd.Next(2) + '0')).ToArray();
                    MyBigInteger.Parse(s, NumberStyles.BinaryNumber);
                    //.ToByteArray()
                    //.Should().Equal(BigIntegerNative.Parse(s, NumberStyles.BinaryNumber).ToByteArray());

                }
            }
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

                var (quo, rem) = OrigBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
                var (quo2, rem2) = MyBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
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
                var (quo2, rem2) = MyBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
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
                var (quo2, rem2) = MyBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
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
                var (quo2, rem2) = MyBigInteger.DivRem(new(bytes1, isUnsigned: true), new(bytes2, isUnsigned: true));
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
                var my = new MyBigInteger(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new MyBigInteger(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(1000, 2000)];
                rnd.NextBytes(bytes);
                var my = new MyBigInteger(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 1; i < 50; i++)
            {
                var bytes = new byte[i];
                rnd.NextBytes(bytes);
                var my = new MyBigInteger(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                int num = rnd.Next() - int.MaxValue / 2;
                MyBigInteger my = num;
                OrigBigInteger expected = num;
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
        }
    }

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
                        var num = MyBigInteger.Parse(s, null);
                        $"{num}".Should().Be(s);
                        num.ToString().Should().Be(s);
                    }
                })
            );
        }
    }
}

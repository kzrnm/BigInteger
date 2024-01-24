using Kzrnm.Numerics.Logic;
using System.Globalization;

namespace Kzrnm.Numerics.Test
{
    public class BigIntegerTests : BigIntegerTestsBase<MyBigInteger>
    {
        [Fact]
        public void ParseHex()
        {
            for (int i = 0; i < 40; i++)
            {
                MyBigInteger.TryParse("8" + new string('0', i), NumberStyles.HexNumber, null, out var result);
                result.Should().Be(MyBigInteger.MinusOne << (3 + 4 * i));
            }
            for (int i = 0; i < 40; i++)
            {
                MyBigInteger.TryParse("F" + new string('0', i), NumberStyles.HexNumber, null, out var result);
                result.Should().Be(MyBigInteger.MinusOne << (4 * i));
            }

            var rnd = new Random(227);
            for (int len = 1; len < 5000; len++)
            {
                for (int k = 0; k < 20; k++)
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
            MyBigInteger.Parse("0111111111111111111111111111111111", NumberStyles.BinaryNumber).Should().Be(0x1FFFFFFFFL);
            MyBigInteger.Parse("100000000000000000000000000000000", NumberStyles.BinaryNumber).Should().Be(-1L << 32);

            for (int i = 0; i < 40; i++)
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
                    MyBigInteger.Parse(s, NumberStyles.BinaryNumber).ToByteArray()
                        .Should().Equal(BigIntegerNative.Parse(s, NumberStyles.BinaryNumber).ToByteArray());

                }
            }
        }
#endif

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
}

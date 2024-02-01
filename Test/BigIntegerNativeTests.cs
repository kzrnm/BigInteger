using Kzrnm.Numerics.Experiment;

namespace Kzrnm.Numerics.Test
{
    public class BigIntegerNativeTests : BigIntegerTestsBase<BigIntegerNative>
    {
        [Fact]
        public void ToStringTest()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new BigIntegerNative(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(100, 1000)];
                rnd.NextBytes(bytes);
                var my = new BigIntegerNative(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[rnd.Next(1000, 2000)];
                rnd.NextBytes(bytes);
                var my = new BigIntegerNative(bytes, isUnsigned: false);
                var expected = new OrigBigInteger(bytes, isUnsigned: false);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
            for (int i = 1; i < 50; i++)
            {
                var bytes = new byte[i];
                rnd.NextBytes(bytes);
                var my = new BigIntegerNative(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 0; i < 50; i++)
            {
                int num = rnd.Next() - int.MaxValue / 2;
                BigIntegerNative my = num;
                OrigBigInteger expected = num;
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
        }
    }

    public class BigIntegerNativeThresholdTests : ThresholdTestsBase
    {
        [Fact]
        public void ParseTrailingZero()
        {
            RunWithFakeThreshold(Number.s_naiveThreshold, 0, () =>
            {
                var rnd = new Random(227);
                for (int i = 0; i < 1000; i++)
                {
                    var s = rnd.GetRandomDigits(rnd.Next(1, 100)) + "1" + new string('0', 8);
                    var num = MyBigInteger.Parse(s, null);
                    $"{num}".Should().Be(s);
                    num.ToString().Should().Be(s);
                }
            });
        }
    }
}

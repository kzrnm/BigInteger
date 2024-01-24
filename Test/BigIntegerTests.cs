namespace Kzrnm.Numerics.Test
{
    public class BigIntegerTests : BigIntegerTestsBase<MyBigInteger>
    {

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

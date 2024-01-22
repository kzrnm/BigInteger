namespace Kzrnm.Numerics.Test
{
    public class BigIntegerDecimalTests
    {
        public static IEnumerable<object[]> Values()
        {
            yield return new object[] { "1", "2" };
            for (int i = 6; i < 8; i++)
            {
                var ss = new string('1', 1 << i);
                yield return new object[] { ss, "2" };
                for (int j = 0; j < 8; j++)
                    yield return new object[] { ss, new string('1', 1 << j) };
            }
            var ones = Enumerable.Range(1, 35).Select(i => new string('1', i)).ToArray();
            var nines = Enumerable.Range(1, 35).Select(i => new string('9', i)).ToArray();
            foreach (var ss in nines)
                foreach (var tt in ones)
                    yield return new object[] { ss, tt };

            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var ss = rnd.GetRandomDigits(rnd.Next(50, 220));
                var tt = rnd.GetRandomDigits(ss.Length / 2);
                yield return new object[] { ss, tt };
            }
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Add(string s, string t)
        {
            (BigIntegerDecimal.Parse(s) + BigIntegerDecimal.Parse(t)).ToString()
                .Should()
                .Be((OrigBigInteger.Parse(s) + OrigBigInteger.Parse(t)).ToString());
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Subtract(string s, string t)
        {
            (BigIntegerDecimal.Parse(s) - BigIntegerDecimal.Parse(t)).ToString()
                .Should()
                .Be((OrigBigInteger.Parse(s) - OrigBigInteger.Parse(t)).ToString());
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Multiply(string s, string t)
        {
            (BigIntegerDecimal.Parse(s) * BigIntegerDecimal.Parse(t)).ToString()
                .Should()
                .Be((OrigBigInteger.Parse(s) * OrigBigInteger.Parse(t)).ToString());
        }

        public static IEnumerable<object[]> DivData()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 200; i++)
            {
                var length = rnd.Next(18 * 32, 2000);
                yield return new object[] { rnd.GetRandomDigits(length * 2), rnd.GetRandomDigits(length) };
            }
        }

        [Theory]
        [MemberData(nameof(Values))]
        [MemberData(nameof(DivData))]
        public void DivRem(string s, string t)
        {
            var (quo, rem) = OrigBigInteger.DivRem(OrigBigInteger.Parse(s), OrigBigInteger.Parse(t));
            var quoStr = quo.ToString();
            var remStr = rem.ToString();

            var ss = BigIntegerDecimal.Parse(s);
            var tt = BigIntegerDecimal.Parse(t);
            var (quo2, rem2) = BigIntegerDecimal.DivRem(ss, tt);
            quo2.ToString().Should().Be(quoStr);
            (ss / tt).ToString().Should().Be(quoStr);
            rem2.ToString().Should().Be(remStr);
            (ss % tt).ToString().Should().Be(remStr);
        }

        [Fact]
        public void ParseAndToStringTest()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var s = "1" + new string('0', i);
                var my = BigIntegerDecimal.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.ToString().Should().Be(orig.ToString());
            }
            for (int i = 1; i < 50; i++)
            {
                var s = new string('9', i);
                var my = BigIntegerDecimal.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.ToString().Should().Be(orig.ToString());
            }
            for (int i = 1; i < 50; i++)
            {
                var s = new string('1', i) + new string('0', i);
                var my = BigIntegerDecimal.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.ToString().Should().Be(orig.ToString());
            }

            for (int i = 0; i < 50; i++)
            {
                var s = rnd.GetRandomDigits(rnd.Next(100, 1000));
                var my = BigIntegerDecimal.Parse(s);
                var expected = OrigBigInteger.Parse(s);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
            for (int i = 1; i < 50; i++)
            {
                var s = rnd.GetRandomDigits(i);
                var my = BigIntegerDecimal.Parse(s);
                var expected = OrigBigInteger.Parse(s);
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
                (-my).ToString().Should().Be($"-{expectedStr}");
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kzrnm.Numerics.Test
{
    public class BigIntegerTests
    {
        public static IEnumerable<object[]> Values()
        {
            yield return new object[] { "1", "2" };

            yield return new object[] { "4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401509523685376", "4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401507376201728" };
            yield return new object[] { "4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401507376201728", "4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401509523685376" };

            foreach (var sign in new[] { "", "-" })
            {
                yield return new object[] { $"{sign}{int.MaxValue}", "1" };
                yield return new object[] { "1", $"{sign}{int.MaxValue}" };
                yield return new object[] { $"{sign}{int.MaxValue}", "-1" };
                yield return new object[] { "-1", $"{sign}{int.MaxValue}" };
            }
            {
                yield return new object[] { $"{int.MinValue}", "1" };
                yield return new object[] { "1", $"{int.MinValue}" };
                yield return new object[] { $"{int.MinValue}", "-1" };
                yield return new object[] { "-1", $"{int.MinValue}" };
            }

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
            (MyBigInteger.Parse(s) + MyBigInteger.Parse(t)).Should().Equal(OrigBigInteger.Parse(s) + OrigBigInteger.Parse(t));
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Subtract(string s, string t)
        {
            (MyBigInteger.Parse(s) - MyBigInteger.Parse(t)).Should().Equal(OrigBigInteger.Parse(s) - OrigBigInteger.Parse(t));
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Multiply(string s, string t)
        {
            (MyBigInteger.Parse(s) * MyBigInteger.Parse(t)).Should().Equal(OrigBigInteger.Parse(s) * OrigBigInteger.Parse(t));
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void DivRem(string s, string t)
        {
            var (quo, rem) = OrigBigInteger.DivRem(OrigBigInteger.Parse(s), OrigBigInteger.Parse(t));

            var ss = MyBigInteger.Parse(s);
            var tt = MyBigInteger.Parse(t);
            (ss / tt).Should().Equal(quo);
            (ss % tt).Should().Equal(rem);
            var (quo2, rem2) = MyBigInteger.DivRem(ss, tt);
            quo2.Should().Equal(quo);
            rem2.Should().Equal(rem);
        }

        [Fact]
        public void ParseAndToStringTest()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var s = "1" + new string('0', i);
                var my = MyBigInteger.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.Should().Equal(orig);
                my.ToString().Should().Be(orig.ToString());
            }
            for (int i = 1; i < 50; i++)
            {
                var s = new string('9', i);
                var my = MyBigInteger.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.Should().Equal(orig);
                my.ToString().Should().Be(orig.ToString());
            }
            for (int i = 1; i < 50; i++)
            {
                var s = new string('1', i) + new string('0', i);
                var my = MyBigInteger.Parse(s);
                var orig = OrigBigInteger.Parse(s);
                my.Should().Equal(orig);
                my.ToString().Should().Be(orig.ToString());
            }

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
                var my = num;
                var expected = num;
                var expectedStr = expected.ToString();
                my.ToString().Should().Be(expectedStr);
            }
        }
    }
}

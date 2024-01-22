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
            for (int i = 6; i < 8; i++)
            {
                var ss = new string('1', 1 << i);
                yield return new object[] { ss, "2" };
                for (int j = 0; j < 8; j++)
                    yield return new object[] { ss, new string('1', 1 << j) };
            }
            for (int i = 1; i < 35; i++)
            {
                var ss = new string('1', i);
                yield return new object[] { ss, ss };
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
        public void Divide(string s, string t)
        {
            (MyBigInteger.Parse(s) / MyBigInteger.Parse(t)).Should().Equal(OrigBigInteger.Parse(s) / OrigBigInteger.Parse(t));
        }

        [Theory]
        [MemberData(nameof(Values))]
        public void Modulo(string s, string t)
        {
            (MyBigInteger.Parse(s) % MyBigInteger.Parse(t)).Should().Equal(OrigBigInteger.Parse(s) % OrigBigInteger.Parse(t));
        }

        [Fact]
        public void ParseTest()
        {
            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var s = "1" + new string('0', i);
                (MyBigInteger.Parse(s)).Should().Equal(OrigBigInteger.Parse(s));
            }
            for (int i = 1; i < 50; i++)
            {
                var s = new string('1', i) + new string('0', i);
                (MyBigInteger.Parse(s)).Should().Equal(OrigBigInteger.Parse(s));
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
                my.ToString().Should().Be(expected.ToString());
            }
            for (int i = 0; i < 50; i++)
            {
                var bytes = new byte[i];
                rnd.NextBytes(bytes);
                var my = new MyBigInteger(bytes, isUnsigned: true);
                var expected = new OrigBigInteger(bytes, isUnsigned: true);
                my.ToString().Should().Be(expected.ToString());
            }
            for (int i = 0; i < 50; i++)
            {
                int num = rnd.Next() - int.MaxValue / 2;
                var my = num;
                var expected = num;
                my.ToString().Should().Be(expected.ToString());
            }
        }
    }
}

using System.Globalization;
using System.Reflection;
using System.Text;

namespace Kzrnm.Numerics.Test
{
    public abstract class BigIntegerTestsBase<T> where T : struct
    {
        static Func<string, IFormatProvider?, T> ParseDelegate
            = (Func<string, IFormatProvider?, T>)typeof(T).GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, binder: null, [typeof(string), typeof(IFormatProvider)], modifiers: null)!
            .CreateDelegate(typeof(Func<string, IFormatProvider?, T>));

        static Func<string, NumberStyles, IFormatProvider?, T> ParseStyleDelegate
            = (Func<string, NumberStyles, IFormatProvider?, T>)typeof(T).GetMethod("Parse", BindingFlags.Static | BindingFlags.Public, binder: null, [typeof(string), typeof(NumberStyles), typeof(IFormatProvider)], modifiers: null)!
            .CreateDelegate(typeof(Func<string, NumberStyles, IFormatProvider?, T>));

        protected delegate bool TryFormatUtf8(Span<byte> utf8Destination, out int bytesWritten, ReadOnlySpan<char> format = default, IFormatProvider? provider = null);
        protected static MethodInfo TryFormatUtf8Method
            = typeof(T).GetMethod(
                "TryFormat",
                BindingFlags.Public | BindingFlags.Instance,
                binder: null,
                [typeof(Span<byte>), typeof(int).MakeByRefType(), typeof(ReadOnlySpan<char>), typeof(IFormatProvider)],
                modifiers: null)!;

        static Func<T, T, (T, T)> DivRemDelegate
            = (Func<T, T, (T, T)>)typeof(T).GetMethod("DivRem", BindingFlags.Static | BindingFlags.Public, binder: null, [typeof(T), typeof(T)], modifiers: null)!
            .CreateDelegate(typeof(Func<T, T, (T, T)>));

        protected static dynamic Parse(string s, IFormatProvider? provider)
            => ParseDelegate.Invoke(s, provider)!;
        protected static dynamic Parse(string s, NumberStyles styles, IFormatProvider? provider)
            => ParseStyleDelegate.Invoke(s, styles, provider)!;

        public record BigIntegerData(string Left, string Right)
        {
            public OrigBigInteger OrigLeft { get; } = OrigBigInteger.Parse(Left);
            public OrigBigInteger OrigRight { get; } = OrigBigInteger.Parse(Right);
            public override string ToString() => $"Left: {Left.Length}, Right: {Right.Length}";
        }
        public static IEnumerable<BigIntegerData> Values()
        {
            yield return new BigIntegerData("1", "2");
            yield return new BigIntegerData("0", new string('1', 40));
            yield return new BigIntegerData("0", "2");

            yield return new BigIntegerData("4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401509523685376", "4149515568880992958512407863691161151012446232242436899995657329690652811412908146399707048947103794288197886611300789182395151075411775307886874834113963687061181803401507376201728");
            yield return new BigIntegerData("3254524275368499316718987238542010791047407685861758100326628572335879727", "831130247779490037441982534999103126");


            var nums = new[]
            {
                "1",
                $"{int.MaxValue}",
                $"{int.MaxValue + 1L}",
                $"{uint.MaxValue}",
                $"{uint.MaxValue+1UL}",
                $"{long.MaxValue}",
                $"{long.MaxValue+1UL}",
                $"{ulong.MaxValue}",
                $"{OrigBigInteger.One<<64}",
                $"{(OrigBigInteger.One<<127)-1}",
                $"{OrigBigInteger.One<<127}",
                $"{(OrigBigInteger.One<<127)+1}",
                $"{(OrigBigInteger.One<<128)-1}",
                $"{OrigBigInteger.One<<128}",
                $"{(OrigBigInteger.One<<128)+1}",
            };

            foreach (var sign1 in new[] { "", "+", "-" })
                foreach (var sign2 in new[] { "", "+", "-" })
                    foreach (var num1 in nums)
                        foreach (var num2 in nums)
                            yield return new BigIntegerData($"{sign1}{num1}", $"{sign2}{num2}");

            for (int i = 6; i < 8; i++)
            {
                var ss = new string('1', 1 << i);
                yield return new BigIntegerData(ss, "2");
                for (int j = 0; j < 8; j++)
                    yield return new BigIntegerData(ss, new string('1', 1 << j));
            }
            var ones = Enumerable.Range(1, 35).Select(i => new string('1', i)).ToArray();
            var nines = Enumerable.Range(1, 35).Select(i => new string('9', i)).ToArray();
            foreach (var ss in nines)
                foreach (var tt in ones)
                    yield return new BigIntegerData(ss, tt);

            var rnd = new Random(227);
            for (int i = 0; i < 50; i++)
            {
                var ss = rnd.GetRandomDigits(rnd.Next(50, 220));
                var tt = rnd.GetRandomDigits(ss.Length / 2);
                yield return new BigIntegerData(ss, tt);
            }
        }
        public static IEnumerable<BigIntegerData> MultiplyValues()
        {
            for (int i = 0; i < 600; i++)
            {
                var s = "1" + new string('0', 600);
                yield return new BigIntegerData(s, s);
            }

            var rnd = new Random(227);
            for (int i = 0; i < 200; i++)
            {
                var length = rnd.Next(18 * 32, 2000);
                yield return new BigIntegerData(rnd.GetRandomDigits(length * 2), rnd.GetRandomDigits(length));
                yield return new BigIntegerData(rnd.GetRandomDigits(length * 2 + 1), rnd.GetRandomDigits(length));
            }
            for (int i = 0; i < 400; i++)
            {
                var length1 = rnd.Next(1000, 2500);
                var length2 = rnd.Next(1000, 2500);
                yield return new BigIntegerData(rnd.GetRandomDigits(length1), rnd.GetRandomDigits(length2));
            }
            for (int i = 0; i < 400; i++)
            {
                var length1 = rnd.Next(100, 200);
                var length2 = rnd.Next(100, 200);
                yield return new BigIntegerData(rnd.GetRandomDigits(length1), rnd.GetRandomDigits(length2));
            }

            for (int i = 0; i < 400; i++)
            {
                var length1 = rnd.Next(100, 200);
                var length2 = rnd.Next(100, 200);
                yield return new BigIntegerData(rnd.GetRandomDigits(length1), rnd.GetRandomDigits(length2));
            }

        }
        public static IEnumerable<BigIntegerData> DivValues()
        {
            foreach (var d in MultiplyValues())
                yield return d;
            var rnd = new Random(227);
            for (int i = 0; i < 400; i++)
            {
                yield return new BigIntegerData(rnd.GetRandomDigits(3 * 4932 - 10 + i), new string('9', 4932));
            }
        }


        [Fact]
        public void Add()
        {
            foreach (var data in Values())
            {
                Equal(Parse(data.Left, null) + Parse(data.Right, null), data.OrigLeft + data.OrigRight);
                Equal(-Parse(data.Left, null) + -Parse(data.Right, null), -data.OrigLeft + -data.OrigRight);
            }
        }

        [Fact]
        public void Subtract()
        {
            foreach (var data in Values())
            {
                Equal(Parse(data.Left, null) - Parse(data.Right, null), data.OrigLeft - data.OrigRight);
                Equal(-Parse(data.Left, null) - -Parse(data.Right, null), -data.OrigLeft - -data.OrigRight);
            }
        }

        [Fact]
        public void Multiply()
        {
            foreach (var data in Values().Concat(MultiplyValues()))
            {
                Equal(Parse(data.Left, null) * Parse(data.Right, null), data.OrigLeft * data.OrigRight);
                Equal(Parse(data.Left, null) * -Parse(data.Right, null), data.OrigLeft * -data.OrigRight);
                Equal(-Parse(data.Left, null) * -Parse(data.Right, null), -data.OrigLeft * -data.OrigRight);
            }
        }

        [Fact]
        public void DivRem()
        {
            foreach (var data in Values().Concat(DivValues()))
            {
                var quo = OrigBigInteger.DivRem(data.OrigLeft, data.OrigRight, out var rem);

                var ss = Parse(data.Left, null);
                var tt = Parse(data.Right, null);

                var (quo2, rem2) = ((T, T))DivRemDelegate.Invoke(ss, tt)!;
                Equal(quo2, quo);
                Equal(rem2, rem);

                Equal((ss / tt), quo);
                Equal((ss % tt), rem);
            }
        }


        [Fact]
        public void ParseAndFormat()
        {
            Test("9999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999999");
            Test("-1111111111111111111111111111111111111111");
            for (int i = 1; i < 1000; i++)
            {
                var s = new string('9', i);
                Test(s);
                Test($"-{s}");
            }

            void Test(string s, string format = "")
            {
                var num = (T)Parse(s, null);
                $"{num}".ShouldBe(s);
                num.ToString().ShouldBe(s);
#if NET7_0_OR_GREATER
                {
                    var TryFormatUtf8Delegate = (TryFormatUtf8)Delegate.CreateDelegate(typeof(TryFormatUtf8), num, TryFormatUtf8Method);
                    var dst = new byte[s.Length];
                    TryFormatUtf8Delegate(dst, out int bytesWritten).ShouldBeTrue();
                    bytesWritten.ShouldBe(s.Length);
                    dst.ShouldBe(System.Text.Encoding.UTF8.GetBytes(s));
                }
#endif
            }
        }

        [Fact]
        public void ParseAndToStringTest()
        {
            foreach (int i in new int[] { 865, 20161 })
                Test(new string('9', i));

            for (int i = 1; i < 50; i++)
                Test(new string('0', i));

            for (int i = 0; i < 50; i++)
                Test("1" + new string('0', i));

            foreach (int i in new int[] { 1000, 10000 })
                Test("1" + new string('0', i));

            foreach (int i in new int[] { 1000, 10000 })
                Test(new string('1', i) + new string('0', i));

            for (int i = 1; i < 50; i++)
                Test(new string('9', i));

            for (int i = 1; i < 50; i++)
                Test(new string('1', i) + new string('0', i));

            for (int i = 1; i < 50; i++)
                Test(new string('1', 500 + i) + new string('0', 3000 + i));
            for (int i = 1; i < 50; i++)
                Test(new string('1', 3000 + i) + new string('0', 3000 + i));
            for (int i = 1; i < 50; i++)
                Test("1" + new string('0', 3000 + i));

            foreach (var v in PlusMinus(0))
                Test(v.ToString());
            foreach (var v in PlusMinus(int.MinValue))
                Test(v.ToString());
            foreach (var v in PlusMinus(int.MaxValue))
                Test(v.ToString());
            foreach (var v in PlusMinus(long.MinValue))
                Test(v.ToString());
            foreach (var v in PlusMinus(long.MaxValue))
                Test(v.ToString());

            var rnd = new Random(227);
            for (int i = 0; i < 100; i++)
                Test(rnd.GetRandomDigits(rnd.Next(2000, 5000)));

            void Test(string s)
            {
                var my = Parse(s, null);
                var orig = OrigBigInteger.Parse(s);
                Equal(my, orig);
            }
        }

        protected static IEnumerable<OrigBigInteger> PlusMinus(OrigBigInteger v) => [v - 1, v, v + 1];

        public static void Equal(T actual, OrigBigInteger expected)
        {
            Impl();
            void Impl()
            {
#if NET8_0_OR_GREATER
                var bytesObj = actual.GetType().GetMethod(nameof(OrigBigInteger.ToByteArray), BindingFlags.Public | BindingFlags.Instance, [])
                    ?.Invoke(actual, []);
                if (bytesObj is byte[] bytes)
                {
                    var expectedBytes = expected.ToByteArray();
                    bytes.ShouldBe(expectedBytes);
                }
#endif
                actual.ToString().ShouldBe(expected.ToString());
            }
        }
    }
}

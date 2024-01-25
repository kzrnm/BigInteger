using Kzrnm.Competitive.IO;
using Kzrnm.Numerics.Experiment;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    internal abstract class BigIntegerDivideTest<T> : BaseSolver where T : INumber<T>, IParsable<T>
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = T.Parse(cr.Ascii(), null);
                var b = T.Parse(cr.Ascii(), null);

                cw.WriteLineJoin(a / b, a % b);
            }
        }
    }
    internal class MyBigIntegerDivideTest : BigIntegerDivideTest<BigInteger> { }
    internal class BigIntegerDecimalDivideTest : BigIntegerDivideTest<BigIntegerDecimal> { }
    internal class BigIntegerNativeDivideTest : BigIntegerDivideTest<BigIntegerNative> { }

    internal class MyBigIntegerDivideTest2 : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = BigInteger.Parse(cr.Ascii(), null);
                var b = BigInteger.Parse(cr.Ascii(), null);

                var quotient = BigInteger.DivRem(a, b, out var remainder);

                cw.WriteLineJoin(quotient, remainder);
            }
        }
    }
    internal class BigIntegerDecimalDivideTest2 : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = BigIntegerDecimal.Parse(cr.Ascii(), null);
                var b = BigIntegerDecimal.Parse(cr.Ascii(), null);

                var quotient = BigIntegerDecimal.DivRem(a, b, out var remainder);

                cw.WriteLineJoin(quotient, remainder);
            }
        }
    }
    internal class BigIntegerNativeDivideTest2 : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = BigIntegerNative.Parse(cr.Ascii(), null);
                var b = BigIntegerNative.Parse(cr.Ascii(), null);

                var quotient = BigIntegerNative.DivRem(a, b, out var remainder);

                cw.WriteLineJoin(quotient, remainder);
            }
        }
    }
}
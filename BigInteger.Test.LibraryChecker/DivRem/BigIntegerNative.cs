using Kzrnm.Competitive.IO;
using Kzrnm.Numerics.Experiment;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class BigIntegerNativeDivRemTest : BaseSolver
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

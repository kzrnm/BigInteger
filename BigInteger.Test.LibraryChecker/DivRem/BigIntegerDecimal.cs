using Kzrnm.Competitive.IO;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class BigIntegerDecimalDivRemTest : BaseSolver
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
}

using Kzrnm.Competitive.IO;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class MyBigIntegerDivRemTest : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = BigInteger.Parse(cr.StringChars(), null);
                var b = BigInteger.Parse(cr.StringChars(), null);

                var quotient = BigInteger.DivRem(a, b, out var remainder);

                cw.WriteLineJoin(quotient, remainder);
            }
        }
    }
}

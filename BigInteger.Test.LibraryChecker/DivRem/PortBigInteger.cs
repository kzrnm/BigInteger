using Kzrnm.Competitive.IO;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class PortBigIntegerDivRemTest : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = Port.BigInteger.Parse(cr.Ascii(), null);
                var b = Port.BigInteger.Parse(cr.Ascii(), null);

                var quotient = Port.BigInteger.DivRem(a, b, out var remainder);

                cw.WriteLineJoin(quotient, remainder);
            }
        }
    }
}

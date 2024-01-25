using Kzrnm.Competitive.IO;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    internal abstract class BigIntegerMultiplyTest<T> : BaseSolver where T : INumber<T>, IParsable<T>
    {
        public override string Url => "https://judge.yosupo.jp/problem/multiplication_of_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = T.Parse(cr.Ascii(), null);
                var b = T.Parse(cr.Ascii(), null);

                cw.WriteLine(a * b);
            }
        }
    }
}
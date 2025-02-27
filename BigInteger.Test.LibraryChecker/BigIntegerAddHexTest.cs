using Kzrnm.Competitive.IO;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    internal abstract class BigIntegerAddHexTest<T> : BaseSolver where T : INumber<T>, IParsable<T>, IFormattable
    {
        public override string Url => "https://judge.yosupo.jp/problem/addition_of_hex_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = ParseHex<T>(cr.StringChars());
                var b = ParseHex<T>(cr.StringChars());
                cw.WriteLine(FormatHex(a + b));
            }
        }
    }
}
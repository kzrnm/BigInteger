using Kzrnm.Competitive.IO;
using Kzrnm.Numerics.Experiment;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class BigIntegerNativeDivRemHexTest : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_hex_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = ParseHex<BigIntegerNative>(cr.AsciiChars());
                var b = ParseHex<BigIntegerNative>(cr.AsciiChars());

                var quotient = BigIntegerNative.DivRem(a, b, out var remainder);
                cw.Write(FormatHex(quotient));
                cw.Write(' ');
                cw.WriteLine(FormatHex(remainder));
            }
        }
    }
}

using Kzrnm.Competitive.IO;

namespace Kzrnm.Numerics.Test.DivRem
{
    internal class MyBigIntegerDivRemHexTest : BaseSolver
    {
        public override string Url => "https://judge.yosupo.jp/problem/division_of_hex_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
#if NET8_0_OR_GREATER
                var a = ParseHex<BigInteger>(cr.Ascii().AsSpan());
                var b = ParseHex<BigInteger>(cr.Ascii().AsSpan());
#else
                var a = ParseHex<BigInteger>(cr.StringChars());
                var b = ParseHex<BigInteger>(cr.StringChars());
#endif

                var quotient = BigInteger.DivRem(a, b, out var remainder);
                cw.Write(FormatHex(quotient));
                cw.Write(' ');
                cw.WriteLine(FormatHex(remainder));
            }
        }
    }
}

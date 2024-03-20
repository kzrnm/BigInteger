﻿using Kzrnm.Competitive.IO;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    internal abstract class BigIntegerMultiplyHexTest<T> : BaseSolver where T : INumber<T>, IParsable<T>, IFormattable
    {
        public override string Url => "https://judge.yosupo.jp/problem/multiplication_of_hex_big_integers";
        public override void Solve(ConsoleReader cr, Utf8ConsoleWriter cw)
        {
            int n = cr.Int();
            while (--n >= 0)
            {
                var a = ParseHex<T>(cr.AsciiChars());
                var b = ParseHex<T>(cr.AsciiChars());
                cw.WriteLine(FormatHex(a * b));
            }
        }
    }
}
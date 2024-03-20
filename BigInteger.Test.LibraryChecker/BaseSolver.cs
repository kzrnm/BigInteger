using Kzrnm.Competitive.IO;
using System.Globalization;
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    internal abstract class BaseSolver : CompetitiveVerifier.ProblemSolver
    {
        public override void Solve()
        {
            using var cw = new Utf8ConsoleWriter();
            Solve(new ConsoleReader(), cw);
        }
        public abstract void Solve(ConsoleReader cr, Utf8ConsoleWriter cw);

        public static T ParseHex<T>(char[] v) where T : INumber<T>
        {
            if (v[0] == '-')
            {
                v[0] = '0';
                return -T.Parse(v, NumberStyles.HexNumber, null);
            }
            var vv = new char[v.Length + 1];
            vv[0] = '0';
            v.CopyTo(vv, 1);
            return T.Parse(vv, NumberStyles.HexNumber, null);
        }
        public static ReadOnlySpan<char> FormatHex<T>(T v) where T : INumber<T>
        {
            if (T.IsZero(v)) return "0";
            if (T.IsNegative(v))
            {
                var rt = (-v).ToString("X", null).AsSpan().TrimStart('0');
                var vv = new char[rt.Length + 1].AsSpan();
                vv[0] = '-';
                rt.CopyTo(vv[1..]);
                return vv;
            }
            return v.ToString("X", null).AsSpan().TrimStart('0');
        }
    }
}
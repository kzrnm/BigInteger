using FluentAssertions.Numeric;

namespace Kzrnm.Numerics.Test
{
    public static class BigIntegerTestUtility
    {
        public static void Equal(this NumericAssertions<MyBigInteger> a, OrigBigInteger expected)
        {
            a.Subject.Should().NotBeNull();
            a.Subject!.Value.ToByteArray().Should().Equal(expected.ToByteArray());
        }
        public static NumericAssertions<MyBigInteger> Should(this MyBigInteger a)
        {
            return new NumericAssertions<MyBigInteger>(a);
        }

        public static string GetRandomDigits(this Random rnd, int length)
        {
            var chs = Enumerable.Repeat(rnd, length - 1)
                .Select(r => (char)('0' + r.Next(10)))
                .Prepend((char)('1' + rnd.Next(9)))
                .ToArray();
            return new string(chs);
        }
    }
}
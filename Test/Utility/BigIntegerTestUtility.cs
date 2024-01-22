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
    }
}
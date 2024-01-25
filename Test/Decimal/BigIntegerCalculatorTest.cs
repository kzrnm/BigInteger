namespace Kzrnm.Numerics.Decimal
{
    public class BigIntegerCalculatorTest
    {
        public static IEnumerable<object[]> DivRem_Data()
        {
            var upper = 899999999999999999ul;
            for (int i = 0; i < 18; i++)
            {
                yield return new object[] { upper };
                upper /= 10;
            }
        }

        [Theory]
        [MemberData(nameof(DivRem_Data))]
        public void DivRem(ulong upper)
        {
            var lower = 123456789123456789ul;
            var div = 987654321987654321ul;
            var quo = BigIntegerCalculator.DivRem(upper, lower, div, out var rem);
            var (quo128, rem128) = UInt128.DivRem((UInt128)upper * BigIntegerCalculator.Base + lower, div);
            ((UInt128)quo).Should().Be(quo128);
            ((UInt128)rem).Should().Be(rem128);
        }
    }
}

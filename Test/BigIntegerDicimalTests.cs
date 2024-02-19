using Kzrnm.Numerics.Logic;

namespace Kzrnm.Numerics.Test
{
    public class BigIntegerDecimalTests : BigIntegerTestsBase<BigIntegerDecimal>
    {
        [Fact]
        public void DivideBound()
        {
            var right = BigIntegerDecimal.Parse("1"
                + new string('0', BigIntegerCalculator.DivideThreshold * 2 * 9 - 1)
                + new string('9', BigIntegerCalculator.DivideThreshold * 2 * 9));
            var rem = right - 1;

            for (int i = 0; i < 130; i++)
            {
                var qi = BigIntegerCalculator.DivideThreshold * 8 * 32 * 4 - 10 + i;
                var q = BigIntegerDecimal.Parse(new string('9', qi));
                var left = q * right + rem;

                var (q2, r2) = BigIntegerDecimal.DivRem(left, right);
                q2.Should().Be(q);
                r2.Should().Be(rem);
            }
        }
    }
}

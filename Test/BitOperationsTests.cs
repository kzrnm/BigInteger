#if !NETCOREAPP3_1_OR_GREATER
using System.Numerics;

namespace Kzrnm.Numerics.Test
{
    public class BitOperationsTests
    {
        [Fact]
        public void Log2()
        {
            Test(1u);

            for (int i = 1; i < 31; i++)
            {
                Test(1u << i);
                Test((1u << i) | 1);
            }

            var rnd = new Random();
            for (int i = 0; i < 300; i++)
                Test((uint)rnd.Next());

            void Test(uint v)
            {
                var u = v | 1;
                int i;
                for (i = 0; u != 0; i++)
                    u >>= 1;
                BitOperations.Log2(v).ShouldBe(i - 1);
            }
        }

        [Fact]
        public void LeadingZeroCount()
        {
            Test(1u);

            for (int i = 1; i < 31; i++)
            {
                Test(1u << i);
                Test((1u << i) | 1);
            }

            var rnd = new Random();
            for (int i = 0; i < 300; i++)
                Test((uint)rnd.Next());

            void Test(uint v)
            {
                int i;
                for (i = 0; i < 32; i++)
                    if ((v & (0x80000000u >> i)) != 0)
                        break;
                BitOperations.LeadingZeroCount(v).ShouldBe(i);
            }
        }

        [Fact]
        public void TrailingZeroCount()
        {
            Test(1u);

            for (int i = 1; i < 31; i++)
            {
                Test(1u << i);
                Test((1u << i) | 1);
            }

            var rnd = new Random();
            for (int i = 0; i < 300; i++)
                Test((uint)rnd.Next());

            void Test(uint v)
            {
                int i;
                for (i = 0; i < 32; i++)
                    if ((v & (1u << i)) != 0)
                        break;
                BitOperations.TrailingZeroCount(v).ShouldBe(i);
            }
        }

        [Fact]
        public void PopCount()
        {
            Test(1u);

            for (int i = 1; i < 31; i++)
            {
                Test(1u << i);
                Test((1u << i) | 1);
            }

            var rnd = new Random();
            for (int i = 0; i < 300; i++)
                Test((uint)rnd.Next());

            void Test(uint v)
            {
                int sum = 0;
                for (int i = 0; i < 32; i++)
                    if ((v & (1u << i)) != 0)
                        ++sum;
                BitOperations.PopCount(v).ShouldBe(sum);
            }
        }

        [Fact]
        public void RoundUpToPowerOf2()
        {
            Test(1u);

            for (int i = 1; i < 31; i++)
            {
                Test(1u << i);
                Test((1u << i) | 1);
            }

            var rnd = new Random();
            for (int i = 0; i < 300; i++)
                Test((uint)rnd.Next());

            void Test(uint v)
            {
                uint pow = 1;
                for (int i = 0; i < 32; i++, pow <<= 1)
                    if (v <= pow)
                        break;
                BitOperations.RoundUpToPowerOf2(v).ShouldBe(pow);
            }
        }
    }
}
#endif
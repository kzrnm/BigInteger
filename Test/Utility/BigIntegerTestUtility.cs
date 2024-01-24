namespace Kzrnm.Numerics.Test
{
    public static class BigIntegerTestUtility
    {
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
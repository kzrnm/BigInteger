using System.Diagnostics.CodeAnalysis;
using System;

namespace Kzrnm.Numerics
{
    internal static class ThrowHelper
    {
        [DoesNotReturn]
        internal static void ThrowOverflowException()
        {
            throw new OverflowException();
        }

        [DoesNotReturn]
        internal static void ThrowNotSupportedException()
        {
            throw new NotSupportedException();
        }

        [DoesNotReturn]
        internal static void ThrowValueArgumentOutOfRange_NeedNonNegNumException()
        {
            throw new ArgumentOutOfRangeException("value", "NeedNonNegNum");
        }
    }
    internal static class SR
    {
        public static string Argument_BadFormatSpecifier => nameof(Argument_BadFormatSpecifier);
        public static string Overflow_UInt64 => nameof(Overflow_UInt64);
        public static string Overflow_UInt32 => nameof(Overflow_UInt32);
        public static string Overflow_UInt128 => nameof(Overflow_UInt128);
        public static string Overflow_ParseBigInteger => nameof(Overflow_ParseBigInteger);
        public static string Overflow_NotANumber => nameof(Overflow_NotANumber);
        public static string Overflow_Negative_Unsigned => nameof(Overflow_Negative_Unsigned);
        public static string Overflow_Int64 => nameof(Overflow_Int64);
        public static string Overflow_Int32 => nameof(Overflow_Int32);
        public static string Overflow_Int128 => nameof(Overflow_Int128);
        public static string Overflow_Decimal => nameof(Overflow_Decimal);
        public static string Overflow_BigIntInfinity => nameof(Overflow_BigIntInfinity);
        public static string Format_TooLarge => nameof(Format_TooLarge);
        public static string Format(params object?[] _) => nameof(Format);
        public static string Argument_MustBeBigInt => nameof(Argument_MustBeBigInt);
        public static string Argument_MinMaxValue => nameof(Argument_MinMaxValue);
        public static string Argument_InvalidNumberStyles => nameof(Argument_InvalidNumberStyles);
        public static string Argument_InvalidHexStyle => nameof(Argument_InvalidHexStyle);
    }
}

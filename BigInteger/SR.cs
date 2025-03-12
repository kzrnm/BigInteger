using System.Diagnostics.CodeAnalysis;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
#if !NET8_0_OR_GREATER
using System.Text;
#endif
#if !NET7_0_OR_GREATER
using System.Collections.Generic;
#endif

namespace Kzrnm.Numerics
{
#if !NET8_0_OR_GREATER
    internal static class SpanHelper
    {
        public static bool ContainsAnyExcept<T>(this Span<T> s, T v) where T : IEquatable<T>? => s.IndexOfAnyExcept(v) >= 0;
        public static bool ContainsAnyExcept<T>(this ReadOnlySpan<T> s, T v) where T : IEquatable<T>? => s.IndexOfAnyExcept(v) >= 0;
#if !NET7_0_OR_GREATER
        public static int IndexOfAnyExcept<T>(this Span<T> s, T v) where T : IEquatable<T>? => IndexOfAnyExcept((ReadOnlySpan<T>)s, v);
        public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> s, T v) where T : IEquatable<T>?
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(s[i], v))
                    return i;
            }
            return -1;
        }
#endif
    }
#endif
    internal enum ParsingStatus
    {
        OK,
        Failed,
        Overflow
    }
    internal enum NumberBufferKind : byte
    {
        Unknown = 0,
        Integer = 1,
        Decimal = 2,
        FloatingPoint = 3,
    }
    internal static class ThrowHelper
    {
        public static void ThrowFormatException_BadFormatSpecifier() => new FormatException();
        public static void ThrowIfNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
            where T : INumberBase<T>
        {
            if (T.IsNegative(value))
                Throw(paramName, value);
            void Throw(string? paramName, T value)
                => throw new ArgumentOutOfRangeException(paramName, value, "MustBeNonNegative");
        }

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
#if !NET8_0_OR_GREATER
        public static bool UIntTryParse(ReadOnlySpan<byte> s, out uint v)
        {
            v = 0;
            if (s.Length > 32)
                return false;

            Span<char> c = stackalloc char[32];
            c = c.Slice(0, s.Length);
            for (int i = c.Length - 1; i >= 0; i--)
            {
                c[i] = (char)s[i];
            }

            return uint.TryParse(c, out v);
        }
#endif
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

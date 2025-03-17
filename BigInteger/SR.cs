using System.Diagnostics.CodeAnalysis;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Buffers.Text;
#if !NET7_0_OR_GREATER
using System.Buffers;
using System.Collections.Generic;
#endif

namespace Kzrnm.Numerics
{
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
#if !NET7_0_OR_GREATER
        [DoesNotReturn]
        internal static void ThrowIfNull(object value)
        {
            if (value == null)
                Throw();

            void Throw() => throw new ArgumentNullException();
        }
#endif
        public static void ThrowFormatException_BadFormatSpecifier() => new FormatException();
        public static void ThrowIfNegative<T>(T value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
#if NET7_0_OR_GREATER
            where T : INumberBase<T>
        {
            if (T.IsNegative(value))
#else
            where T : struct, IComparable<T>
        {
            if (value.CompareTo(default) < 0)
#endif
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T CastFrom<T>(uint v)
        {
            if (typeof(T) == typeof(char))
            {
                var u = (char)v;
                return Unsafe.As<char, T>(ref u);
            }
            if (typeof(T) == typeof(byte))
            {
                var u = (byte)v;
                return Unsafe.As<byte, T>(ref u);
            }
            return default!;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint CastToUInt32<T>(T v)
        {
            if (typeof(T) == typeof(char))
                return Unsafe.As<T, char>(ref v);
            if (typeof(T) == typeof(byte))
                return Unsafe.As<T, byte>(ref v);
            return 0;
        }
#if !NET8_0_OR_GREATER
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
        public static int LastIndexOfAnyExcept<T>(this Span<T> s, T v) where T : IEquatable<T>? => LastIndexOfAnyExcept((ReadOnlySpan<T>)s, v);
        public static int LastIndexOfAnyExcept<T>(this ReadOnlySpan<T> s, T v) where T : IEquatable<T>?
        {
            for (int i = s.Length - 1; i >= 0; i--)
            {
                if (!EqualityComparer<T>.Default.Equals(s[i], v))
                    return i;
            }
            return -1;
        }
        public static Span<T> TrimEnd<T>(this Span<T> s, T v) where T : IEquatable<T>?
            => s.Slice(0, s.LastIndexOfAnyExcept(v) + 1);
        public static ReadOnlySpan<T> TrimEnd<T>(this ReadOnlySpan<T> s, T v) where T : IEquatable<T>?
            => s.Slice(0, s.LastIndexOfAnyExcept(v) + 1);
#endif

#if !NET8_0_OR_GREATER
        public static bool UIntTryParse(ReadOnlySpan<byte> s, out uint v)
            => Utf8Parser.TryParse(s, out v, out _);

        public static bool TryFormat(this uint v, Span<byte> bytes, out int bytesWritten) => Utf8Formatter.TryFormat(v, bytes, out bytesWritten);
        public static bool TryFormat(this int v, Span<byte> bytes, out int bytesWritten) => Utf8Formatter.TryFormat(v, bytes, out bytesWritten);
#if !NET7_0_OR_GREATER
        public static bool UIntTryParse(ReadOnlySpan<char> s, out uint v)
        {
            Span<byte> bytes = s.Length < 256
                ? stackalloc byte[256]
                : ArrayPool<byte>.Shared.Rent(s.Length);
            bytes = bytes.Slice(0, s.Length);
            for (int i = 0; i < s.Length; i++)
                bytes[i] = (byte)s[i];
            return Utf8Parser.TryParse(bytes, out v, out _);
        }
        public static bool UIntTryParseX(ReadOnlySpan<char> s, out uint v)
        {
            Span<byte> bytes = s.Length < 256
                ? stackalloc byte[256]
                : ArrayPool<byte>.Shared.Rent(s.Length);
            bytes = bytes.Slice(0, s.Length);
            for (int i = 0; i < s.Length; i++)
                bytes[i] = (byte)s[i];
            return UIntTryParseX(bytes, out v);
        }
        public static bool UIntTryParseX(ReadOnlySpan<byte> s, out uint v)
            => Utf8Parser.TryParse(s, out v, out _, 'X');

        public static bool TryFormat(this uint v, Span<char> chars, out int charsWritten, string? format = null)
        {
            var uFormat = StandardFormat.Parse(format);

            Span<byte> bytes = chars.Length < 256
                ? stackalloc byte[256]
                : ArrayPool<byte>.Shared.Rent(chars.Length);
            if (!Utf8Formatter.TryFormat(v, bytes, out charsWritten, uFormat))
                return false;
            for (int i = charsWritten - 1; i >= 0; i--)
                chars[i] = (char)bytes[i];
            return true;
        }
        public static bool TryFormat(this int v, Span<char> chars, out int charsWritten)
        {
            Span<byte> bytes = chars.Length < 256
                ? stackalloc byte[256]
                : ArrayPool<byte>.Shared.Rent(chars.Length);
            if (!Utf8Formatter.TryFormat(v, bytes, out charsWritten))
                return false;
            for (int i = charsWritten - 1; i >= 0; i--)
                chars[i] = (char)bytes[i];
            return true;
        }
#endif
#endif
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

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

namespace Kzrnm.Numerics.Logic
{
    // Polyfill CoreLib internal interfaces and methods
    // Define necessary members only
#if Embedding
    public
#else
    internal
#endif
    interface IUtfChar<TSelf> :
        IEquatable<TSelf>
        where TSelf : unmanaged, IUtfChar<TSelf>
    {
        public static abstract TSelf CastFrom(byte value);

        public static abstract TSelf CastFrom(char value);

        public static abstract TSelf CastFrom(int value);

        public static abstract TSelf CastFrom(uint value);

        public static abstract TSelf CastFrom(ulong value);

        public static abstract uint CastToUInt32(TSelf value);
    }


#if NET8_0_OR_GREATER
#pragma warning disable CA1067 // Polyfill only type
    internal readonly struct Utf16Char(char ch) : IUtfChar<Utf16Char>
#pragma warning restore CA1067
    {
        private readonly char value = ch;
#else
    internal readonly struct Utf16Char : IUtfChar<Utf16Char>
    {
        public Utf16Char(char ch) { value = ch; }
        private readonly char value;
#endif

        public static Utf16Char CastFrom(byte value) => new((char)value);
        public static Utf16Char CastFrom(char value) => new(value);
        public static Utf16Char CastFrom(int value) => new((char)value);
        public static Utf16Char CastFrom(uint value) => new((char)value);
        public static Utf16Char CastFrom(ulong value) => new((char)value);
        public static uint CastToUInt32(Utf16Char value) => value.value;
        public bool Equals(Utf16Char other) => value == other.value;
    }

#if NET8_0_OR_GREATER
#pragma warning disable CA1067 // Polyfill only type
    internal readonly struct Utf8Char(byte ch) : IUtfChar<Utf8Char>
#pragma warning restore CA1067
    {
        private readonly byte value = ch;
#else
    internal readonly struct Utf8Char : IUtfChar<Utf8Char>
    {
        public Utf8Char(byte ch) { value = ch; }
        private readonly byte value;
#endif
        public static Utf8Char CastFrom(byte value) => new(value);
        public static Utf8Char CastFrom(char value) => new((byte)value);
        public static Utf8Char CastFrom(int value) => new((byte)value);
        public static Utf8Char CastFrom(uint value) => new((byte)value);
        public static Utf8Char CastFrom(ulong value) => new((byte)value);
        public static uint CastToUInt32(Utf8Char value) => value.value;
        public bool Equals(Utf8Char other) => value == other.value;
    }

    static partial class Number
    {
        internal static bool AllowHyphenDuringParsing(this NumberFormatInfo info)
        {
            string negativeSign = info.NegativeSign;
            return negativeSign.Length == 1 &&
                   negativeSign[0] switch
                   {
                       '\u2012' or         // Figure Dash
                       '\u207B' or         // Superscript Minus
                       '\u208B' or         // Subscript Minus
                       '\u2212' or         // Minus Sign
                       '\u2796' or         // Heavy Minus Sign
                       '\uFE63' or         // Small Hyphen-Minus
                       '\uFF0D' => true,   // Fullwidth Hyphen-Minus
                       _ => false
                   };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PositiveSignTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.PositiveSign);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NegativeSignTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.NegativeSign);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencySymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.CurrencySymbol);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentSymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.PercentSymbol);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PerMilleSymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.PerMilleSymbol);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencyDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.CurrencyDecimalSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencyGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.CurrencyGroupSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NumberDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.NumberDecimalSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NumberGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.NumberGroupSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.PercentDecimalSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged, IUtfChar<T>
        {
            Debug.Assert(typeof(T) == typeof(Utf16Char));
            return MemoryMarshal.Cast<char, T>(info.PercentGroupSeparator);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CountDigits(uint value)
        {
            long tableValue = BitOperations.Log2(value) switch
            {
                0 => 4294967296,
                1 => 8589934582,
                2 => 8589934582,
                3 => 8589934582,
                4 => 12884901788,
                5 => 12884901788,
                6 => 12884901788,
                7 => 17179868184,
                8 => 17179868184,
                9 => 17179868184,
                10 => 21474826480,
                11 => 21474826480,
                12 => 21474826480,
                13 => 21474826480,
                14 => 25769703776,
                15 => 25769703776,
                16 => 25769703776,
                17 => 30063771072,
                18 => 30063771072,
                19 => 30063771072,
                20 => 34349738368,
                21 => 34349738368,
                22 => 34349738368,
                23 => 34349738368,
                24 => 38554705664,
                25 => 38554705664,
                26 => 38554705664,
                _ => 41949672960,
            };
            return (int)((value + tableValue) >> 32);
        }
    }
}

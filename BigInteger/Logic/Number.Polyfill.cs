// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Kzrnm.Numerics.Logic
{
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
        static ReadOnlySpan<T> StrToSpan<T>(string v)
            where T : unmanaged
        {
            if (typeof(T) == typeof(char))
                return SR.SpanCast<char, T>(v.AsSpan());
            if (typeof(T) == typeof(byte))
                return SR.SpanCast<byte, T>(Encoding.UTF8.GetBytes(v));
            return default;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PositiveSignTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.PositiveSign);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NegativeSignTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.NegativeSign);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencySymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.CurrencySymbol);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentSymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.PercentSymbol);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PerMilleSymbolTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.PerMilleSymbol);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencyDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.CurrencyDecimalSeparator);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> CurrencyGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.CurrencyGroupSeparator);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NumberDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.NumberDecimalSeparator);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> NumberGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.NumberGroupSeparator);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentDecimalSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.PercentDecimalSeparator);


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ReadOnlySpan<T> PercentGroupSeparatorTChar<T>(this NumberFormatInfo info)
            where T : unmanaged
            => StrToSpan<T>(info.PercentGroupSeparator);

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

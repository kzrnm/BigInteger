// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Kzrnm.Numerics.Decimal
{
    internal static class NumericsHelpers
    {
        [MethodImpl(256)]
        [DebuggerStepThrough]
        public static ulong Abs(long a)
        {
            unchecked
            {
                ulong mask = (ulong)(a >> 63);
                return ((ulong)a ^ mask) - mask;
            }
        }
    }
}
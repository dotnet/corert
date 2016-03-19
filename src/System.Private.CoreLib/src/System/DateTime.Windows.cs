// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;

namespace System
{
    public partial struct DateTime : IComparable, IFormattable, IComparable<DateTime>, IEquatable<DateTime>, IConvertible
    {
        public unsafe static DateTime UtcNow
        {
            get
            {
                Contract.Ensures(Contract.Result<DateTime>().Kind == DateTimeKind.Utc);
                Interop.FILETIME filetime;
                Interop.mincore.GetSystemTimeAsFileTime(out filetime);
                long ticks = *(long*)&filetime;
                // For performance, use a private constructor that does not validate arguments.
                return new DateTime(((ulong)(ticks + FileTimeOffset)) | KindUtc);
            }
        }
    }
}

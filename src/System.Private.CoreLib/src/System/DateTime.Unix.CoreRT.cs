// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.Contracts;

namespace System
{
    public partial struct DateTime
    {
        public static DateTime UtcNow
        {
            get
            {
                Contract.Ensures(Contract.Result<DateTime>().Kind == DateTimeKind.Utc);
                // For performance, use a private constructor that does not validate arguments.
                return new DateTime(((ulong)(Interop.Sys.GetSystemTimeAsTicks() + TicksTo1970)) | KindUtc);
            }
        }
    }
}

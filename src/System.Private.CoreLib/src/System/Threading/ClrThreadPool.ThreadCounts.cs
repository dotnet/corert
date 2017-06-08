// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        [StructLayout(LayoutKind.Explicit)]
        struct ThreadCounts
        {
            [FieldOffset(0)]
            public short maxWorking;
            [FieldOffset(2)]
            public short numActive;
            [FieldOffset(4)]
            public short numWorking;
            [FieldOffset(0)]
            private long asLong;

            public static ThreadCounts VolatileReadCounts(ref ThreadCounts counts)
            {
                return new ThreadCounts
                {
                    asLong = Volatile.Read(ref counts.asLong)
                };
            }

            public static ThreadCounts CompareExchangeCounts(ref ThreadCounts location, ThreadCounts newCounts, ThreadCounts oldCounts)
            {
                ThreadCounts result = new ThreadCounts
                {
                    asLong = Interlocked.CompareExchange(ref location.asLong, newCounts.asLong, oldCounts.asLong)
                };

                if (result.asLong == oldCounts.asLong)
                {
                    ValidateCounts(result);
                    ValidateCounts(newCounts);
                }
                return result;
            }

            public static bool operator ==(ThreadCounts lhs, ThreadCounts rhs) => lhs.asLong == rhs.asLong;

            public static bool operator !=(ThreadCounts lhs, ThreadCounts rhs) => lhs.asLong != rhs.asLong;

            public override bool Equals(object obj)
            {
                return obj is ThreadCounts counts && this.asLong == counts.asLong;
            }

            public override int GetHashCode()
            {
                return (int)(asLong >> 8) ^ maxWorking;
            }

            private static void ValidateCounts(ThreadCounts counts)
            {
                Debug.Assert(counts.maxWorking > 0);
                Debug.Assert(counts.numActive >= 0);
                Debug.Assert(counts.numWorking >= 0);
                Debug.Assert(counts.numWorking <= counts.numActive);
            }
        }
    }
}

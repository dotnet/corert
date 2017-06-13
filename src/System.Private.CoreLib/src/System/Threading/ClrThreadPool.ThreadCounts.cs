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
            private long _asLong;

            public static ThreadCounts VolatileReadCounts(ref ThreadCounts counts)
            {
                return new ThreadCounts
                {
                    _asLong = Volatile.Read(ref counts._asLong)
                };
            }

            public static ThreadCounts CompareExchangeCounts(ref ThreadCounts location, ThreadCounts newCounts, ThreadCounts oldCounts)
            {
                ThreadCounts result = new ThreadCounts
                {
                    _asLong = Interlocked.CompareExchange(ref location._asLong, newCounts._asLong, oldCounts._asLong)
                };

                if (result._asLong == oldCounts._asLong)
                {
                    ValidateCounts(result);
                    ValidateCounts(newCounts);
                }
                return result;
            }

            public static bool operator ==(ThreadCounts lhs, ThreadCounts rhs) => lhs._asLong == rhs._asLong;

            public static bool operator !=(ThreadCounts lhs, ThreadCounts rhs) => lhs._asLong != rhs._asLong;

            public override bool Equals(object obj)
            {
                return obj is ThreadCounts counts && this._asLong == counts._asLong;
            }

            public override int GetHashCode()
            {
                return (int)(_asLong >> 8) ^ maxWorking;
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

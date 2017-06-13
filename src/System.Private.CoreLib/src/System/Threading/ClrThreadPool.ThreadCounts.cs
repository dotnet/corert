// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Threading
{
    internal static partial class ClrThreadPool
    {
        /// <summary>
        /// Tracks information on the number of threads we want/have in different states in our thread pool.
        /// </summary>
        [StructLayout(LayoutKind.Explicit)]
        struct ThreadCounts
        {
            /// <summary>
            /// Max possible thread pool threads we want to have.
            /// </summary>
            [FieldOffset(0)]
            public short numThreadsGoal;

            /// <summary>
            /// Number of thread pool threads that currently exist.
            /// </summary>
            [FieldOffset(2)]
            public short numExistingThreads;

            /// <summary>
            /// Number of threads processing work items.
            /// </summary>
            [FieldOffset(4)]
            public short numProcessingWork;

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
                return (int)(_asLong >> 8) ^ numThreadsGoal;
            }

            private static void ValidateCounts(ThreadCounts counts)
            {
                Debug.Assert(counts.numThreadsGoal > 0);
                Debug.Assert(counts.numExistingThreads >= 0);
                Debug.Assert(counts.numProcessingWork >= 0);
                Debug.Assert(counts.numProcessingWork <= counts.numExistingThreads);
            }
        }
    }
}

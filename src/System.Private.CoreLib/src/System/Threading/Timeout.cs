// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// 

// 

//

using System.Threading;
using System;

namespace System.Threading
{
    // A constant used by methods that take a timeout (Object.Wait, Thread.Sleep
    // etc) to indicate that no timeout should occur.
    //
    // <TODO>@todo: this should become an enum.</TODO>
    //This class has only static members and does not require serialization.
    public static class Timeout
    {
        public static readonly TimeSpan InfiniteTimeSpan = new TimeSpan(0, 0, 0, 0, Timeout.Infinite);

        public const int Infinite = -1;
        internal const uint UnsignedInfinite = unchecked((uint)-1);
    }
}

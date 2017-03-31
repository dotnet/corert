// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Collections.Concurrent;

namespace System
{
    internal partial class CurrentSystemTimeZone
    {
        private DaylightTime GetCachedDaylightChanges(int year) => _daylightChangesUnifier.GetOrAdd(year);

        private sealed class DaylightChangesUnifier : ConcurrentUnifier<int, DaylightTime>
        {
            protected sealed override DaylightTime Factory(int key) => CurrentSystemTimeZone.CreateDaylightChanges(key);
        }

        private readonly DaylightChangesUnifier _daylightChangesUnifier = new DaylightChangesUnifier();
    }
}

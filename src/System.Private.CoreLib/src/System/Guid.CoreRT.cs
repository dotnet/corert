// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.Runtime.CompilerServices;

namespace System
{
    partial struct Guid
    {
        internal bool Equals(ref Guid g)
        {
            // Now compare each of the elements
            return g._a == _a &&
                Unsafe.Add(ref g._a, 1) == Unsafe.Add(ref _a, 1) &&
                Unsafe.Add(ref g._a, 2) == Unsafe.Add(ref _a, 2) &&
                Unsafe.Add(ref g._a, 3) == Unsafe.Add(ref _a, 3);
        }
    }
}

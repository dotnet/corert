// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace System.Collections
{
    // An IEqualityComparer is a mechanism to consume custom performant comparison infrastructure
    // that can be consumed by some of the common collections.
    [System.Runtime.InteropServices.ComVisible(true)]
    public interface IEqualityComparer
    {
        bool Equals(Object x, Object y);
        int GetHashCode(Object obj);
    }
}

// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System.Runtime.InteropServices.WindowsRuntime
{
    /// <summary>
    /// IReadOnlyCollection is generic, so we can't invoke Count without knowing T.  Instead, we 
    // introduce a new non-generic interface for our adapters, so we can have a non-generic helper 
    // for the call.
    /// </summary>
    public interface IReadOnlyCollectionAdapter
    {
        int Count { get; }
    }
    
    public interface IReadOnlyListAdapter<out T> : IReadOnlyCollectionAdapter
    {
        T this[int index] { get; }
    }
}

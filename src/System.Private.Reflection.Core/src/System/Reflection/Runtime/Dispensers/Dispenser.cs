// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // Abstract base for reflection caches.
    //
    internal abstract class Dispenser<K, V>
        where K : IEquatable<K>
        where V : class
    {
        public abstract V GetOrAdd(K key);
    }
}



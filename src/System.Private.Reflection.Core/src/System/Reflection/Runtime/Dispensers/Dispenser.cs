// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;

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



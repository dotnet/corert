// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;
using global::System.Collections.Concurrent;

using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // This dispenser stores every instance using weak references.
    //
    internal sealed class DispenserThatReusesAsLongAsValueIsAlive<K, V> : Dispenser<K, V>
        where K : IEquatable<K>
        where V : class
    {
        public DispenserThatReusesAsLongAsValueIsAlive(Func<K, V> factory)
        {
            _concurrentUnifier = new FactoryConcurrentUnifierW(factory);
        }

        public sealed override V GetOrAdd(K key)
        {
            return _concurrentUnifier.GetOrAdd(key);
        }

        private sealed class FactoryConcurrentUnifierW : ConcurrentUnifierW<K, V>
        {
            public FactoryConcurrentUnifierW(Func<K, V> factory)
            {
                _factory = factory;
            }

            protected sealed override V Factory(K key)
            {
                return _factory(key);
            }

            private Func<K, V> _factory;
        }

        private FactoryConcurrentUnifierW _concurrentUnifier;
    }
}


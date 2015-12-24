// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // This dispenser always creates things anew.
    //
    internal sealed class DispenserThatAlwaysCreates<K, V> : Dispenser<K, V>
        where K : IEquatable<K>
        where V : class
    {
        public DispenserThatAlwaysCreates(Func<K, V> factory)
        {
            _factory = factory;
        }

        public sealed override V GetOrAdd(K key)
        {
            return _factory(key);
        }

        private Func<K, V> _factory;
    }
}



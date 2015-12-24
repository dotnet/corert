// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Diagnostics;
using global::System.Runtime.CompilerServices;

namespace System.Reflection.Runtime.Dispensers
{
    internal sealed class DispenserThatReusesAsLongAsKeyIsAlive<K, V> : Dispenser<K, V>
        where K : class, IEquatable<K>
        where V : class
    {
        public DispenserThatReusesAsLongAsKeyIsAlive(Func<K, V> factory)
        {
            _createValueCallback = CreateValue;
            _conditionalWeakTable = new ConditionalWeakTable<K, V>();
            _factory = factory;
        }

        public sealed override V GetOrAdd(K key)
        {
            return _conditionalWeakTable.GetValue(key, _createValueCallback);
        }

        private V CreateValue(K key)
        {
            return _factory(key);
        }

        private Func<K, V> _factory;
        private ConditionalWeakTable<K, V> _conditionalWeakTable;
        private ConditionalWeakTable<K, V>.CreateValueCallback _createValueCallback;
    }
}


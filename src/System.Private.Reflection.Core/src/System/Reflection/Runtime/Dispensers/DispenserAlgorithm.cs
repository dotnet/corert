// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // A parameterizable monikor for the various cache algorithms available. 
    //
    internal sealed class DispenserAlgorithm
    {
        public static readonly DispenserAlgorithm CreateAlways = new DispenserAlgorithm();   // Always create a new object (i.e. no caching at all.)
        public static readonly DispenserAlgorithm ReuseAlways = new DispenserAlgorithm();   // Every object is saved permanently (i.e. complete unification.)
        public static readonly DispenserAlgorithm ReuseAsLongAsValueIsAlive = new DispenserAlgorithm();   // Every object is saved using weak references.

        //
        // Associates the value with key using a hash table but does not prevent key from gc'd. 
        // Restriction: The algorithm uses ConditionalWeakTable so it is subject to the following limitations: 
        //   The key cannot be a value type.
        //   Keys are compared using Object.ReferenceEquals. 
        //
        public static readonly DispenserAlgorithm ReuseAsLongAsKeyIsAlive = new DispenserAlgorithm();
    }
}



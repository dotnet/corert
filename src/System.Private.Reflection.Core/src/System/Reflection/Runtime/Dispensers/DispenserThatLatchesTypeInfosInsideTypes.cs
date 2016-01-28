// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Diagnostics;
using global::System.Reflection.Runtime.TypeInfos;

using global::Internal.Reflection.Core.NonPortable;

namespace System.Reflection.Runtime.Dispensers
{
    //
    // This dispenser uses RuntimeType to store a reference to its RuntimeTypeInfo.
    //
    internal sealed class DispenserThatLatchesTypeInfosInsideTypes : Dispenser<RuntimeType, RuntimeTypeInfo>
    {
        public DispenserThatLatchesTypeInfosInsideTypes(Func<RuntimeType, RuntimeTypeInfo> factory)
        {
            _factory = factory;
        }

        public sealed override RuntimeTypeInfo GetOrAdd(RuntimeType key)
        {
            return key.InternalGetLatchedRuntimeTypeInfo<RuntimeTypeInfo>(_factory);
        }

        private Func<RuntimeType, RuntimeTypeInfo> _factory;
    }
}


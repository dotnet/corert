// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // Constructed generic types that have an EEType available.
    //
    internal sealed class RuntimeEEConstructedGenericType : RuntimeConstructedGenericType
    {
        internal RuntimeEEConstructedGenericType(EETypePtr eeType)
            : base()
        {
            _runtimeTypeHandle = new RuntimeTypeHandle(eeType);
        }

        public sealed override bool Equals(Object obj)
        {
            return InternalIsEqual(obj);  // Do not change this - see comments in RuntimeType.cs regarding Equals()
        }

        public sealed override int GetHashCode()
        {
            return _runtimeTypeHandle.GetHashCode();
        }

        public sealed override RuntimeTypeHandle TypeHandle
        {
            get
            {
                return _runtimeTypeHandle;
            }
        }

        public sealed override bool InternalTryGetTypeHandle(out RuntimeTypeHandle typeHandle)
        {
            typeHandle = _runtimeTypeHandle;
            return true;
        }

        //
        // Pay-for-play safe implementation of TypeInfo.ContainsGenericParameters()
        //
        public sealed override bool InternalIsOpen
        {
            get
            {
                return false;  // Anything that has an EEType cannot be open.
            }
        }

        protected sealed override ConstructedGenericTypeKey CreateDelayedConstructedGenericTypeKeyIfAvailable()
        {
            ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
            if (callbacks == null)
                return ConstructedGenericTypeKey.Unavailable;
            RuntimeTypeHandle genericTypeDefinitionHandle;
            RuntimeTypeHandle[] genericTypeArgumentHandles;
            if (!callbacks.TryGetConstructedGenericTypeComponents(_runtimeTypeHandle, out genericTypeDefinitionHandle, out genericTypeArgumentHandles))
                return ConstructedGenericTypeKey.Unavailable;
            return new ConstructedGenericTypeKey(genericTypeDefinitionHandle.ToRuntimeType(), genericTypeArgumentHandles.ToRuntimeTypeArray());
        }

        protected sealed override String LastResortToString
        {
            get
            {
                return _runtimeTypeHandle.LastResortToString;
            }
        }

        private RuntimeTypeHandle _runtimeTypeHandle;
    }
}


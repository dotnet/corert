// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents a pointer that has an EEType.
    //
    internal sealed class RuntimeEEPointerType : RuntimePointerType
    {
        internal RuntimeEEPointerType(EETypePtr eeType)
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

        protected sealed override RuntimeType CreateDelayedRuntimeElementTypeIfAvailable()
        {
            ReflectionExecutionDomainCallbacks callbacks = RuntimeAugments.CallbacksIfAvailable;
            if (callbacks == null)
                return null;
            RuntimeTypeHandle targetTypeHandle;
            if (!callbacks.TryGetPointerTypeTargetType(_runtimeTypeHandle, out targetTypeHandle))
                return null;
            return targetTypeHandle.ToRuntimeType();
        }

        private RuntimeTypeHandle _runtimeTypeHandle;
    }
}



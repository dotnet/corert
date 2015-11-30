// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Collections.Generic;

using Internal.Runtime.Augments;

namespace Internal.Reflection.Core.NonPortable
{
    //
    // This class represents an array (zero lower bound) that has an EEType.
    //
    internal sealed class RuntimeEEArrayType : RuntimeArrayType
    {
        internal RuntimeEEArrayType(EETypePtr eeType)
            : base(false, 1)
        {
            _runtimeTypeHandle = new RuntimeTypeHandle(eeType);
        }

        internal RuntimeEEArrayType(EETypePtr eeType, int rank)
            : base(true, rank)
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

            RuntimeTypeHandle elementTypeHandle;

            if (!InternalIsMultiDimArray)
            {
                if (!callbacks.TryGetArrayTypeElementType(_runtimeTypeHandle, out elementTypeHandle))
                    return null;
            }
            else
            {
                if (!callbacks.TryGetMultiDimArrayTypeElementType(_runtimeTypeHandle, Rank, out elementTypeHandle))
                    return null;
            }

            return ReflectionCoreNonPortable.GetRuntimeTypeForEEType(elementTypeHandle.ToEETypePtr());
        }

        private RuntimeTypeHandle _runtimeTypeHandle;
    }
}


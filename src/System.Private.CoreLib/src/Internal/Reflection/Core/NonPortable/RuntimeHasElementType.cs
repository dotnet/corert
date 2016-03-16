// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Internal.Runtime.Augments;

#if ENABLE_REFLECTION_TRACE
using Internal.Reflection.Tracing;
#endif

namespace Internal.Reflection.Core.NonPortable
{
    //
    // Common base for RuntimeArrayType, RuntimeByRefType, RuntimePointerType
    //
    internal abstract class RuntimeHasElementType : RuntimeType, IKeyedItem<RuntimeType>
    {
        protected RuntimeHasElementType()
            : base()
        {
        }

        protected RuntimeHasElementType(RuntimeType runtimeElementType)
            : base()
        {
            _lazyRuntimeElementType = runtimeElementType;
        }

        public sealed override String FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_FullName(this);
#endif

                String elementFullName = GetElementType().FullName;
                if (elementFullName == null)
                    return null;
                return elementFullName + Suffix;
            }
        }

        public sealed override String Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.Type_Namespace(this);
#endif

                return GetElementType().Namespace;
            }
        }

        public sealed override String ToString()
        {
            RuntimeType runtimeElementType = this.RuntimeElementTypeIfAvailable;
            if (runtimeElementType == null)
                return "Type:0x" + this.GetHashCode().ToString("x8");
            else
                return runtimeElementType.ToString() + Suffix;
        }

        public sealed override RuntimeType InternalRuntimeElementType
        {
            get
            {
                PrepareKey();
                return Key;
            }
        }

        public sealed override String InternalGetNameIfAvailable(ref RuntimeType rootCauseForFailure)
        {
            String elementTypeName = InternalRuntimeElementType.InternalGetNameIfAvailable(ref rootCauseForFailure);
            if (elementTypeName == null)
            {
                rootCauseForFailure = InternalRuntimeElementType;
                return null;
            }
            return elementTypeName + Suffix;
        }

        public sealed override String InternalFullNameOfAssembly
        {
            get
            {
                return this.InternalRuntimeElementType.InternalFullNameOfAssembly;
            }
        }

        //
        // Implements IKeyedItem.PrepareKey.
        // 
        // This method is the keyed item's chance to do any lazy evaluation needed to produce the key quickly. 
        // Concurrent unifiers are guaranteed to invoke this method at least once and wait for it
        // to complete before invoking the Key property. The unifier lock is NOT held across the call.
        //
        // PrepareKey() must be idempodent and thread-safe. It may be invoked multiple times and concurrently.
        //
        public void PrepareKey()
        {
            RuntimeType runtimeElementType = this.RuntimeElementTypeIfAvailable;
            if (runtimeElementType == null)
                throw RuntimeAugments.Callbacks.CreateMissingMetadataException(this);
        }

        //
        // Implements IKeyedItem.Key.
        // 
        // Produce the key. This is a high-traffic property and is called while the hash table's lock is held. Thus, it should
        // return a precomputed stored value and refrain from invoking other methods. If the keyed item wishes to
        // do lazy evaluation of the key, it should do so in the PrepareKey() method.
        //
        public RuntimeType Key
        {
            get
            {
                Debug.Assert(_lazyRuntimeElementType != null,
                    "IKeyedItem violation: Key invoked before PrepareKey().");

                return _lazyRuntimeElementType;
            }
        }

        //
        // The string to append to the element type name to get this type's name.
        //
        protected abstract String Suffix { get; }

        //
        // If the subclass did not provide the key during construction, it must override this. We will invoke it during PrepareKey()
        // to get the key in a delayed fashion.
        //
        protected virtual RuntimeType CreateDelayedRuntimeElementTypeIfAvailable()
        {
            Debug.Assert(false, "CreateDelayedRuntimeElementType() should not have been called since we provided the key during construction.");
            throw new NotSupportedException(); // Already gave you the key during construction!            
        }

        private RuntimeType RuntimeElementTypeIfAvailable
        {
            get
            {
                if (_lazyRuntimeElementType == null)
                    _lazyRuntimeElementType = CreateDelayedRuntimeElementTypeIfAvailable();
                return _lazyRuntimeElementType;
            }
        }

        private volatile RuntimeType _lazyRuntimeElementType;
    }
}




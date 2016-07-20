// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Collections.Concurrent;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;

using global::Internal.Reflection.Tracing;
using global::Internal.Reflection.Core.NonPortable;


namespace System.Reflection.Runtime.TypeInfos
{
    //
    // The runtime's implementation of TypeInfo's for the "HasElement" subclass of types. 
    //
    internal abstract partial class RuntimeHasElementTypeInfo : RuntimeTypeInfo, IKeyedItem<RuntimeHasElementTypeInfo.UnificationKey>
    {
        protected RuntimeHasElementTypeInfo(UnificationKey key)
            : base()
        {
            _key = key;
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
        }

        //
        // Implements IKeyedItem.Key.
        // 
        // Produce the key. This is a high-traffic property and is called while the hash table's lock is held. Thus, it should
        // return a precomputed stored value and refrain from invoking other methods. If the keyed item wishes to
        // do lazy evaluation of the key, it should do so in the PrepareKey() method.
        //
        public UnificationKey Key
        {
            get
            {
                return _key;
            }
        }

        public sealed override Assembly Assembly
        {
            get
            {
                return _key.ElementType.Assembly;
            }
        }

        //
        // Left unsealed because this implemention is correct for ByRefs and Pointers but not Arrays.
        //
        public override TypeAttributes Attributes
        {
            get
            {
                Debug.Assert(IsByRef || IsPointer);
                return TypeAttributes.AnsiClass;
            }
        }

        public sealed override bool ContainsGenericParameters
        {
            get
            {
                return _key.ElementType.ContainsGenericParameters;
            }
        }

        public sealed override string FullName
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_FullName(this);
#endif
                string elementFullName = _key.ElementType.FullName;
                if (elementFullName == null)
                    return null;
                return elementFullName + Suffix;
            }
        }

        public sealed override string Namespace
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_Namespace(this);
#endif
                return _key.ElementType.Namespace;
            }
        }

        public sealed override string ToString()
        {
            return _key.ElementType.ToString() + Suffix;
        }

        protected sealed override int InternalGetHashCode()
        {
            return _key.ElementType.GetHashCode();
        }

        internal sealed override Type InternalDeclaringType
        {
            get
            {
                return null;
            }
        }

        internal sealed override string InternalGetNameIfAvailable(ref Type rootCauseForFailure)
        {
            string elementTypeName = _key.ElementType.InternalGetNameIfAvailable(ref rootCauseForFailure);
            if (elementTypeName == null)
            {
                rootCauseForFailure = _key.ElementType.AsType();
                return null;
            }
            return elementTypeName + Suffix;
        }

        internal sealed override string InternalFullNameOfAssembly
        {
            get
            {
                return _key.ElementType.InternalFullNameOfAssembly;
            }
        }

        internal sealed override RuntimeTypeInfo InternalRuntimeElementType
        {
            get
            {
                return _key.ElementType;
            }
        }

        internal sealed override RuntimeTypeHandle InternalTypeHandleIfAvailable
        {
            get
            {
                return _key.TypeHandle;
            }
        }

        protected abstract string Suffix { get; }

        private readonly UnificationKey _key;
    }
}


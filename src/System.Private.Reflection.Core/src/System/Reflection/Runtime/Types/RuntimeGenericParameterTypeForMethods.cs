// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using global::System;
using global::System.Reflection;
using global::System.Diagnostics;
using global::System.Collections.Generic;
using global::System.Collections.Concurrent;
using global::System.Reflection.Runtime.General;
using global::System.Reflection.Runtime.TypeInfos;
using global::System.Reflection.Runtime.MethodInfos;
using global::System.Reflection.Runtime.CustomAttributes;

using global::Internal.Reflection.Core.NonPortable;

using global::Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.Types
{
    //
    // The runtime's implementation of System.Type for generic parameters for methods.
    //

    internal sealed class RuntimeGenericParameterTypeForMethods : RuntimeGenericParameterType, IKeyedItem<RuntimeGenericParameterTypeForMethods.UnificationKey>
    {
        internal RuntimeGenericParameterTypeForMethods(MetadataReader reader, GenericParameterHandle genericParameterHandle, RuntimeNamedMethodInfo declaringRuntimeNamedMethodInfo)
            : base(reader, genericParameterHandle)
        {
            _declaringRuntimeNamedMethodInfo = declaringRuntimeNamedMethodInfo;
        }

        public sealed override Type DeclaringType
        {
            get
            {
                return _declaringRuntimeNamedMethodInfo.DeclaringType;
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
        }

        //
        // Implements IKeyedItem.Key.
        // 
        // Produce the key. This is a high-traffic property and is called while the hash table's lock is held. Thus, it should
        // return a precomputed stored value and refrain from invoking other methods. If the keyed item wishes to
        // do lazy evaluation of the key, it should do so in the PrepareKey() method.
        //
        public RuntimeGenericParameterTypeForMethods.UnificationKey Key
        {
            get
            {
                return new UnificationKey(_declaringRuntimeNamedMethodInfo, this.Reader, this.GenericParameterHandle);
            }
        }

        internal sealed override RuntimeMethodInfo DeclaringMethod
        {
            get
            {
                return _declaringRuntimeNamedMethodInfo;
            }
        }

        internal sealed override TypeContext TypeContext
        {
            get
            {
                TypeContext typeContext = this.DeclaringType.GetRuntimeTypeInfo<RuntimeTypeInfo>().TypeContext;
                return new TypeContext(typeContext.GenericTypeArguments, _declaringRuntimeNamedMethodInfo.RuntimeGenericArgumentsOrParameters);
            }
        }

        private RuntimeNamedMethodInfo _declaringRuntimeNamedMethodInfo;


        //
        // Key for unification.
        //
        internal struct UnificationKey : IEquatable<UnificationKey>
        {
            public UnificationKey(RuntimeNamedMethodInfo methodOwner, MetadataReader reader, GenericParameterHandle genericParameterHandle)
            {
                _methodOwner = methodOwner;
                _genericParameterHandle = genericParameterHandle;
                _reader = reader;
            }

            public RuntimeNamedMethodInfo MethodOwner
            {
                get
                {
                    return _methodOwner;
                }
            }

            public MetadataReader Reader
            {
                get
                {
                    return _reader;
                }
            }

            public GenericParameterHandle GenericParameterHandle
            {
                get
                {
                    return _genericParameterHandle;
                }
            }

            public override bool Equals(Object obj)
            {
                if (!(obj is UnificationKey))
                    return false;
                return Equals((UnificationKey)obj);
            }

            public bool Equals(UnificationKey other)
            {
                if (!(this._genericParameterHandle.Equals(other._genericParameterHandle)))
                    return false;
                if (!(this._reader == other._reader))
                    return false;
                if (!this._methodOwner.Equals(other._methodOwner))
                    return false;
                return true;
            }

            public override int GetHashCode()
            {
                return this._genericParameterHandle.GetHashCode();
            }

            private RuntimeNamedMethodInfo _methodOwner;
            private MetadataReader _reader;
            private GenericParameterHandle _genericParameterHandle;
        }
    }
}



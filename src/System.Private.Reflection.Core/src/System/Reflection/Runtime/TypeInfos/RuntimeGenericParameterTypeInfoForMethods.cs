// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Reflection.Runtime.Types;
using System.Reflection.Runtime.General;
using System.Reflection.Runtime.MethodInfos;


using Internal.Reflection.Tracing;

using Internal.Metadata.NativeFormat;

namespace System.Reflection.Runtime.TypeInfos
{
    internal sealed partial class RuntimeGenericParameterTypeInfoForMethods : RuntimeGenericParameterTypeInfo, IKeyedItem<RuntimeGenericParameterTypeInfoForMethods.UnificationKey>
    {
        private RuntimeGenericParameterTypeInfoForMethods(MetadataReader reader, GenericParameterHandle genericParameterHandle, RuntimeNamedMethodInfo declaringRuntimeNamedMethodInfo)
           : base(reader, genericParameterHandle)
        {
            DeclaringRuntimeNamedMethodInfo = declaringRuntimeNamedMethodInfo;
        }

        public sealed override MethodBase DeclaringMethod
        {
            get
            {
#if ENABLE_REFLECTION_TRACE
                if (ReflectionTrace.Enabled)
                    ReflectionTrace.TypeInfo_DeclaringMethod(this);
#endif
                return DeclaringRuntimeNamedMethodInfo;
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
        public UnificationKey Key
        {
            get
            {
                return new UnificationKey(DeclaringRuntimeNamedMethodInfo, Reader, GenericParameterHandle);
            }
        }

        internal sealed override Type InternalDeclaringType
        {
            get
            {
                return DeclaringRuntimeNamedMethodInfo.DeclaringType;
            }
        }

        internal sealed override TypeContext TypeContext
        {
            get
            {
                TypeContext typeContext = this.DeclaringType.GetRuntimeTypeInfo<RuntimeTypeInfo>().TypeContext;
                return new TypeContext(typeContext.GenericTypeArguments, DeclaringRuntimeNamedMethodInfo.RuntimeGenericArgumentsOrParameters);
            }
        }

        internal RuntimeNamedMethodInfo DeclaringRuntimeNamedMethodInfo { get; }
    }
}

